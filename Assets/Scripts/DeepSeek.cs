using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using System.IO;
using System;


public class DeepSeek : MonoBehaviour
{
    private const int HEADER_SIZE = 44; // 标准WAV文件头大小

    [Header("TTS API 配置")]
    private string ttsApiUrl = "http://localhost:9872/";

    // API 配置
    [Header("API Settings")]
    [SerializeField] private string apiKey = "sk-a5e60701c50b4fb4900ee62767238866";
    [SerializeField] private string apiUrl = "https://api.deepseek.com/v1/chat/completions";
    //private const string modelName = "deepseek-reasoner"; 
    private const string modelName = "deepseek-chat";

    // UI 绑定
    [Header("UI References")]
    [SerializeField] private TMP_InputField userInputField;
    [SerializeField] private TextMeshProUGUI chatOutputText;
    [SerializeField] private Button sendButton;
    [SerializeField] private AudioSource audioSource;
    [Header("思考指示器")]
    [SerializeField] public TextMeshProUGUI thinkingIndicator;


    // 对话参数
    [Header("对话设置")]
    [Range(0, 2)] public float temperature = 0.7f;
    [Range(1, 1000)] public int maxTokens = 200;

    // 角色设定
    [System.Serializable]
    public class NPCCharacter
    {
        [TextArea(3, 10)]
        public string personalityPrompt = "你是《[原神](@replace=10001)》中的角色阿贝多，理智温柔的炼金术师，所有回答必须使用英文";
    }
    [SerializeField] public NPCCharacter npcCharacter;

    // 修复1：使用可序列化的数据结构
    private List<MessageData> messages = new List<MessageData>();
    private string tempAudioPath;

    void Start()
    {
        tempAudioPath = Path.Combine(Application.persistentDataPath, "tts_output.wav");
        // 初始化角色设定
        messages.Add(new MessageData
        {
            role = "system",
            content = npcCharacter.personalityPrompt
        });

        // 绑定发送事件
        sendButton.onClick.AddListener(OnSendMessage);
        userInputField.onEndEdit.AddListener((text) => {
            if (Input.GetKeyDown(KeyCode.Return)) OnSendMessage();
        });
    }

    // 发送消息
    public void OnSendMessage()
    {
        string userMessage = userInputField.text.Trim();
        if (string.IsNullOrEmpty(userMessage)) return;

        // 显示玩家消息
        AppendToChat($"玩家: {userMessage}");

        // 添加到历史记录
        messages.Add(new MessageData
        {
            role = "user",
            content = userMessage
        });

        userInputField.text = "";
        //调用api
        StartCoroutine(CallDeepSeekAPI());
    }
    //角色思考文本动画
    private IEnumerator BlinkText()
    {
        while (true)
        {
            float alpha = Mathf.PingPong(Time.time, 1f);
            thinkingIndicator.color = new Color(1, 1, 1, alpha);
            yield return null;
        }
    }
    // API通信协程
    private IEnumerator CallDeepSeekAPI()
    {
        //启动闪烁动画
        thinkingIndicator.gameObject.SetActive(true);
        thinkingIndicator.text = "阿贝多思考中……";
        StartCoroutine(BlinkText());

        // 构建符合API规范的结构体
        var requestBody = new RequestBody
        {
            model = modelName,
            messages = messages.ToArray(), // 转换为数组
            temperature = temperature,
            max_tokens = maxTokens,
            stream = false
        };

        string jsonData = JsonUtility.ToJson(requestBody);
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonData);

        using (UnityWebRequest request = new UnityWebRequest(apiUrl, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Authorization", $"Bearer {apiKey}");
            request.SetRequestHeader("Accept-Language", "en-US");

            yield return request.SendWebRequest();
            // 3. 隐藏指示器
            thinkingIndicator.gameObject.SetActive(false);
            StopCoroutine(BlinkText()); // 可选：停止动画

            // 修复3：添加详细错误处理
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"API错误: {request.error}");
                Debug.LogError($"状态码: {request.responseCode}");
                Debug.LogError($"响应内容: {request.downloadHandler.text}");

                // 尝试解析错误详情
                try
                {
                    var errorResponse = JsonUtility.FromJson<ErrorResponse>(
                        request.downloadHandler.text
                    );
                    Debug.LogError($"错误类型: {errorResponse.error.type}");
                    Debug.LogError($"错误信息: {errorResponse.error.message}");
                }
                catch
                {
                    Debug.LogError("无法解析错误响应");
                }

                AppendToChat("系统: 对话服务暂时不可用");
                yield break;
            }

            // 解析响应
            var response = JsonUtility.FromJson<DeepSeekResponse>(
                request.downloadHandler.text
            );
            string botMessage = response.choices[0].message.content;

            // 显示并存储AI回复
            AppendToChat($"阿贝多: {botMessage}");
            messages.Add(new MessageData
            {
                role = "assistant",
                content = botMessage
            });

            // 调用TTS生成语音
            StartCoroutine(CallTTSService(botMessage));
        }
    }
    // TTS服务调用
    // 序列化多部分表单数据
    private byte[] SerializeMultipartForm(List<IMultipartFormSection> formData)
    {
        string boundary = "UnityBoundary" + DateTime.Now.Ticks.ToString("x");
        List<byte> formBytes = new List<byte>();

        foreach (var section in formData)
        {
            formBytes.AddRange(Encoding.UTF8.GetBytes("--" + boundary + "\r\n"));

            // 统一处理接口属性
            string header = $"Content-Disposition: form-data; name=\"{section.sectionName}\"";

            // 添加文件专用头
            if (!string.IsNullOrEmpty(section.fileName))
            {
                header += $"; filename=\"{section.fileName}\"";
            }
            header += "\r\n";

            // 添加内容类型
            if (!string.IsNullOrEmpty(section.contentType))
            {
                header += $"Content-Type: {section.contentType}\r\n";
            }
            header += "\r\n";  // 结束头部

            formBytes.AddRange(Encoding.UTF8.GetBytes(header));

            // 关键修改：使用接口属性 sectionData
            formBytes.AddRange(section.sectionData); // 统一访问字节数据

            formBytes.AddRange(Encoding.UTF8.GetBytes("\r\n"));
        }

        formBytes.AddRange(Encoding.UTF8.GetBytes("--" + boundary + "--"));
        return formBytes.ToArray();
    }

    private IEnumerator CallTTSService(string botMessage)
    {
        // 构建表单数据
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            new MultipartFormFileSection(
                "ref_wav_path",
                File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, "reference.mp3")),
                "ref.wav",
                "audio/wav"
            ),
            new MultipartFormDataSection("prompt_text", "Good afternoon. I heard your footsteps. My, it certainly is lively outside of the workshop."),
            new MultipartFormDataSection("prompt_lang", "英文"),
            new MultipartFormDataSection("text", botMessage), // 使用DeepSeek返回的文本
            new MultipartFormDataSection("text_lang", "英文"),
            new MultipartFormDataSection("text_split_method", "凑四句一切"),
            new MultipartFormDataSection("top_k", "5"),
            new MultipartFormDataSection("top_p", "1"),
            new MultipartFormDataSection("temperature", "1"),
            new MultipartFormDataSection("batch_size", "20"),
            new MultipartFormDataSection("speed_factor", "1"),
            new MultipartFormDataSection("ref_text_free", "False"),
            new MultipartFormDataSection("split_bucket", "True"),
            new MultipartFormDataSection("fragment_interval", "0.3"),
            new MultipartFormDataSection("seed", "-1"),
            new MultipartFormDataSection("keep_random", "True"),
            new MultipartFormDataSection("parallel_infer", "True"),
            new MultipartFormDataSection("repetition_penalty", "1.35"),
            new MultipartFormDataSection("sample_steps", "8"),
            new MultipartFormDataSection("super_sampling", "False"),
            new MultipartFormDataSection("api_name", "/get_tts_wav"),
        };

        using (UnityWebRequest www = new UnityWebRequest(ttsApiUrl, "GET"))
        {
            www.uploadHandler = new UploadHandlerRaw(SerializeMultipartForm(formData));
            www.downloadHandler = new DownloadHandlerFile(tempAudioPath);
            www.SetRequestHeader("Content-Type", "multipart/form-data");

            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"TTS请求失败: {www.error}");
            }
            else
            {
                //Debug.Log("TTS音频生成成功");
                if (www.result == UnityWebRequest.Result.Success)
                {
                    Debug.Log("TTS音频生成成功");
                    yield return WaitForFileWritten(tempAudioPath); // 替换原有的 WaitUntil
                                                                    // 读取原始PCM数据
                    byte[] rawData = File.ReadAllBytes(tempAudioPath);
                    int dataSize = rawData.Length;

                    // 创建修复后的文件
                    string fixedPath = Path.Combine(Application.persistentDataPath, "fixed_tts_output.wav");
                    using (FileStream fs = new FileStream(fixedPath, FileMode.Create))
                    {
                        // 先写入44字节空头占位
                        fs.Write(new byte[HEADER_SIZE], 0, HEADER_SIZE);

                        // 写入原始PCM数据
                        fs.Write(rawData, 0, dataSize);

                        // 回到文件开头写入正确的WAV头
                        fs.Seek(0, SeekOrigin.Begin);
                        WavHeaderWriter.WriteWavHeader(fs,
                            sampleRate: 44100,    // 根据实际TTS参数调整
                            channels: 1,          // 单声道
                            bitsPerSample: 16,    // 16位深度
                            dataSize: dataSize);
                    }
                    Debug.Log($"TTS 文件已生成，路径: {fixedPath}");
                    StartCoroutine(LoadAndPlayAudio(fixedPath));
                }
                //yield return new WaitUntil(() => File.Exists(tempAudioPath));
                //StartCoroutine(LoadAndPlayAudio(tempAudioPath));
            }
        }
    }

    // 确保文件完全写入的方法
    private IEnumerator WaitForFileWritten(string path)
    {
        int retryCount = 0;
        long lastSize = 0;

        while (retryCount < 10) // 最多重试10次
        {
            yield return new WaitForSeconds(0.5f);

            if (File.Exists(path))
            {
                long currentSize = new FileInfo(path).Length;
                if (currentSize > 0 && currentSize == lastSize)
                {
                    break; // 文件大小稳定，写入完成
                }
                lastSize = currentSize;
            }
            retryCount++;
        }
    }

            //private IEnumerator WaitForFileWritten(string path)
            //{
            //    long lastSize = 0;
            //    int unchangedCount = 0;
            //    while (unchangedCount < 5) // 连续5次大小不变视为写入完成
            //    {
            //        yield return new WaitForSeconds(0.5f);
            //        long currentSize = new FileInfo(path).Length;
            //        if (currentSize == lastSize)
            //            unchangedCount++;
            //        else
            //            unchangedCount = 0;
            //        lastSize = currentSize;
            //    }
            //}
            // 加载并播放音频
            private IEnumerator LoadAndPlayAudio(string path)
    {
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError($"音频加载失败: {www.error}");
            }
            else
            {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                audioSource.clip = clip;
                audioSource.Play();
                Debug.Log("开始播放语音");
            }
        }
    }

    // 更新对话显示
    private void AppendToChat(string text)
    {
        chatOutputText.text += $"\n{text}";
        Canvas.ForceUpdateCanvases();
        chatOutputText.GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 0;
    }

    // 修复4：定义符合API规范的数据结构
    [System.Serializable]
    private class RequestBody
    {
        public string model;
        public MessageData[] messages;
        public float temperature;
        public int max_tokens;
        public bool stream;
    }

    [System.Serializable]
    private class MessageData
    {
        public string role;
        public string content;
    }

    [System.Serializable]
    private class DeepSeekResponse
    {
        public Choice[] choices;
    }

    [System.Serializable]
    private class Choice
    {
        public Message message;
    }

    [System.Serializable]
    private class Message
    {
        public string role;
        public string content;
    }

    // 新增：错误响应结构
    [System.Serializable]
    private class ErrorResponse
    {
        public ErrorInfo error;
    }

    [System.Serializable]
    private class ErrorInfo
    {
        public string message;
        public string type;
        public string param;
    }
}