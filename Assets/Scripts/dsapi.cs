using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using System.IO;

public class dsapi : MonoBehaviour
{
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
    [Range(1, 1000)] public int maxTokens = 300;

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
        //thinkingIndicator.text = "阿贝多思考中……";
        StartCoroutine(BlinkText());
        // 修复2：构建符合API规范的结构体
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
            AppendToChat($"流萤: {botMessage}");
            messages.Add(new MessageData
            {
                role = "assistant",
                content = botMessage
            });
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