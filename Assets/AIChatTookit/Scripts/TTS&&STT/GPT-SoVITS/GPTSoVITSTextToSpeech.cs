using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Newtonsoft.Json;

public class GPTSoVITSTextToSpeech : TTS
{
    #region 配置参数
    [Header("参考音频文件配置")]
    [SerializeField] private AudioClip m_ReferenceClip = null; // 参考音频文件
    [Header("参考音频对应文本内容")]
    [SerializeField] private string m_ReferenceText = ""; // 参考音频的文本内容
    [Header("参考音频语言")]
    [SerializeField] private Language m_ReferenceTextLan = Language.英语; // 参考音频语言
    [Header("合成音频语言")]
    [SerializeField] private Language m_TargetTextLan = Language.英语; // 合成音频语言
    private string m_AudioBase64String = ""; // 参考音频的base64编码
    [SerializeField] private string m_SplitType = "标点"; // 文本分割方式
    [SerializeField] private int m_Top_k = 5; // 采样参数：top_k
    [SerializeField] private float m_Top_p = 1; // 采样参数：top_p
    [SerializeField] private float m_Temperature = 1; // 采样参数：温度
    [SerializeField] private bool m_TextReferenceMode = false; // 文本参考模式
    #endregion

    private void Awake()
    {
        AudioTurnToBase64(); // 初始化时将参考音频转为Base64
    }

    /// <summary>
    /// 语音合成接口（返回合成文本）
    /// </summary>
    /// <param name="_msg">需要合成的文本</param>
    /// <param name="_callback">回调函数(AudioClip, 合成文本)</param>
    public override void Speak(string _msg, Action<AudioClip, string> _callback)
    {
        StartCoroutine(GetVoice(_msg, _callback)); // 启动合成协程

    }

    private void SaveJsonToFile(string json)
    {
        string filePath = Application.persistentDataPath + "/request.json"; // 保存路径
        System.IO.File.WriteAllText(filePath, json); // 写入文件
        Debug.Log($"JSON 已保存到文件: {filePath}");
    }

   

    /// <summary>
    /// 获取合成音频
    /// </summary>
    /// <param name="_msg">合成文本</param>
    /// <param name="_callback">回调函数</param>
    /// 

    private IEnumerator GetVoice(string _msg, Action<AudioClip, string> _callback)
    {
        stopwatch.Restart(); // 启动计时器
        //20250613
        Debug.Log($"请求地址为: {m_PostURL}");

        // 构建请求数据
        string _postJson = GetPostJson(_msg);
        //string _postJson = GetPostJson(_msg);
        SaveJsonToFile(_postJson); // 保存 JSON 到文件
        Debug.Log("Post JSON 已保存到文件，可在编辑器控制台查看路径");

        if (string.IsNullOrEmpty(_postJson))
        {
            Debug.LogError("JSON 数据构建失败");
            yield break;
        }

        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        //using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "GET"))
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(_postJson); // UTF-8编码避免中文乱码[3](@ref)
            request.uploadHandler = new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest(); // 发送网络请求

            if (request.responseCode == 200) // 请求成功
            {
                Debug.Log("请求成功200！");
                string _text = request.downloadHandler.text;
                Response _response = JsonUtility.FromJson<Response>(_text);
                if (_response == null || _response.data == null || _response.data.Count == 0)
                {
                    Debug.LogError("API 响应数据格式错误");
                    yield break;
                }

                //string _wavPath = _response.data[0].name;
                string _wavPath = "D:\\Love & Peace\\06Edge专清\\新建文件夹\\GPT - SoVITS - v4 - 20250419\\TEMP\\gradio\\test\\audio.wav";

                if (string.IsNullOrEmpty(_wavPath))
                {
                    // 合成失败时重试
                    StartCoroutine(GetVoice(_msg, _callback));
                }
                else
                {
                    // 获取生成的音频文件
                    StartCoroutine(GetAudioFromFile(_wavPath, _msg, _callback));
                }
            }
            else // 请求失败
            {
                //string _wavPath = _response.data[0].name;
                string _wavPath = "D://Love & Peace//06Edge专清//新建文件夹//GPT - SoVITS - v4 - 20250419//TEMP//gradio//test//audio.wav";

                if (string.IsNullOrEmpty(_wavPath))
                {
                    // 合成失败时重试
                    StartCoroutine(GetVoice(_msg, _callback));
                }
                else
                {
                    // 获取生成的音频文件
                    StartCoroutine(GetAudioFromFile(_wavPath, _msg, _callback));
                }
                //Debug.Log("Post JSON:\n" + _postJson);


                Debug.LogError($"语音合成失败: {request.error}");
                string allowHeader = request.GetResponseHeader("Allow");
                if (!string.IsNullOrEmpty(allowHeader))
                {
                    Debug.LogWarning($"服务端允许的方法: {allowHeader}");
                }
            }
        }

        stopwatch.Stop(); // 停止计时器
        Debug.Log($"GPT-SoVITS合成耗时: {stopwatch.Elapsed.TotalSeconds}秒");
    }

    /// <summary>
    /// 构建POST请求的JSON数据
    /// </summary>
    /// <param name="_msg">待合成文本</param>
    private string GetPostJson(string _msg)
    {
        // 验证参考音频配置
        if (string.IsNullOrEmpty(m_ReferenceText) || m_ReferenceClip == null)
        {
            Debug.LogError("GPT-SoVITS未配置参考音频或参考文本");
            return null;
        }

        //20250613
        var jsonData = new
        {
            text = _msg,
            text_lang = m_TargetTextLan.ToString(),
            ref_audio_path = $"data:audio/wav;base64,{m_AudioBase64String}",
            aux_ref_audio_paths = new List<string> { $"data:audio/wav;base64,{m_AudioBase64String}" },
            prompt_text = m_ReferenceText,
            prompt_lang = m_ReferenceTextLan.ToString(),
            top_k = m_Top_k,
            top_p = m_Top_p,
            temperature = m_Temperature,
            text_split_method = m_SplitType,
            batch_size = 20,
            speed_factor = 1,
            ref_text_free = m_TextReferenceMode,
            split_bucket = true,
            fragment_interval = 0.3,
            seed = -1,
            keep_random = true,
            parallel_infer = true,
            repetition_penalty = 1.35,
            sample_steps = "32",
            super_sampling = false,
            api_name = "/inference"
            //api_name = "/get_tts_wav"
        };

        //20250613
        //构建JSON数据结构
       // var jsonData = new
       //{
       //    data = new List<object>
       //    {
       //         new { name = "audio.wav", data = $"data:audio/wav;base64,{m_AudioBase64String}" },
       //         m_ReferenceText,
       //         m_ReferenceTextLan.ToString(),
       //         _msg,
       //         m_TargetTextLan.ToString(),
       //         m_SplitType,
       //         m_Top_k,
       //         m_Top_p,
       //         m_Temperature,
       //         m_TextReferenceMode
       //    }
       //};


        return JsonConvert.SerializeObject(jsonData, Formatting.Indented); // JSON序列化
    }

    /// <summary>
    /// 将音频转换为Base64编码
    /// </summary>
    private void AudioTurnToBase64()
    {
        if (m_ReferenceClip == null)
        {
            Debug.LogError("GPT-SoVITS未配置参考音频");
            return;
        }

        // 音频转字节数组
        byte[] audioData = WavUtility.FromAudioClip(m_ReferenceClip);
        // 字节数组转Base64
        m_AudioBase64String = Convert.ToBase64String(audioData);
    }

    /// <summary>
    /// 从本地加载合成音频文件
    /// </summary>
    /// <param name="_path">音频文件路径</param>
    /// <param name="_msg">合成文本</param>
    /// <param name="_callback">回调函数</param>
    private IEnumerator GetAudioFromFile(string _path, string _msg, Action<AudioClip, string> _callback)
    {
        string filePath = "file://" + _path;
        Debug.Log(filePath);
        using (UnityWebRequest request = UnityWebRequestMultimedia.GetAudioClip(filePath, AudioType.WAV))
        {
            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success)
            {
                // 获取音频剪辑
                AudioClip audioClip = DownloadHandlerAudioClip.GetContent(request);
                _callback(audioClip, _msg); // 回调结果
            }
            else
            {
                Debug.LogError($"音频加载失败: {request.error}");
            }
        }
    }

    #region 数据模型
    /*
    POST请求数据结构示例：
    {
        "data": [
            {"name":"audio.wav","data":"data:audio/wav;base64,UklGRiQAAABXQVZFZm10IBAAAAABAAEARKwAAIhYAQACABAAZGF0YQAAAAA="},
            "hello world",
            "中文",
            "hello world",
            "中文",
        ]
    }   
    */

    /*
    API响应数据结构：
    {
        "data": [{
            "name": "E:\\AIProjects\\GPT-SoVITS\\TEMP\\tmp53eoney1.wav",
            "data": null,
            "is_file": true
        }],
        "is_generating": true,
        "duration": 7.899699926376343,
        "average_duration": 7.899699926376343
    }
    */

    [Serializable]
    public class Response
    {
        public List<AudioBack> data = new List<AudioBack>();
        public bool is_generating = true;
        public float duration;
        public float average_duration;
    }

    [Serializable]
    public class AudioBack
    {
        public string name = string.Empty;
        public string data = string.Empty;
        public bool is_file = true;
    }

    /// <summary>
    /// 支持的语言类型
    /// </summary>
    public enum Language
    {
        中文,
        英语,
        日语,
        中英混合,
        日英混合,
        多语种混合
    }
    #endregion
}