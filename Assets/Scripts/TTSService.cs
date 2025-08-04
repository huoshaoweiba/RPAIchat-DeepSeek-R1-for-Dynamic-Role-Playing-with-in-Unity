using UnityEngine;
using UnityEngine.Networking;
using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Text;

public class TTSService : MonoBehaviour
{
    public AudioSource audioSource; // Unity音频源组件
    private string apiUrl = "http://localhost:9872/get_tts_wav";
    private string tempAudioPath;

    void Start()
    {
        tempAudioPath = Path.Combine(Application.persistentDataPath, "temp_tts.wav");
    }

    // 调用TTS服务并播放结果
    public void GenerateAndPlayAudio()
    {
        StartCoroutine(CallTTSService(
            refWavPath: Path.Combine(Application.streamingAssetsPath, "reference.mp3"),
            promptText: "Good afternoon. I heard your footsteps. My, it certainly is lively outside of the workshop",
            text: "I was just conducting some alchemical research, but I'm happy to take a moment to converse.",
            language: "英文"
        ));
    }

    private IEnumerator CallTTSService(string refWavPath, string promptText, string text, string language)
    {
        // 1. 准备表单数据
        List<IMultipartFormSection> formData = new List<IMultipartFormSection>
        {
            // 添加参考音频文件
            new MultipartFormFileSection("ref_wav_path", File.ReadAllBytes(refWavPath), "ref.wav", "audio/wav"),
            
            // 添加文本参数
            new MultipartFormDataSection("prompt_text", promptText),
            new MultipartFormDataSection("prompt_language", language),
            new MultipartFormDataSection("text", text),
            new MultipartFormDataSection("text_language", language),
            new MultipartFormDataSection("how_to_cut", "凑四句一切"),
            new MultipartFormDataSection("top_k", "15"),
            new MultipartFormDataSection("top_p", "1"),
            new MultipartFormDataSection("temperature", "1"),
            new MultipartFormDataSection("ref_free", "false"),
            new MultipartFormDataSection("speed", "1"),
            new MultipartFormDataSection("if_freeze", "false"),
            new MultipartFormDataSection("sample_steps", "8"),
            new MultipartFormDataSection("if_sr", "false"),
            new MultipartFormDataSection("pause_second", "0.3")
        };

        // 2. 发送请求
        using (UnityWebRequest www = new UnityWebRequest(apiUrl, "POST"))
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
                Debug.Log("TTS音频生成成功");
                yield return new WaitUntil(() => File.Exists(tempAudioPath));
                StartCoroutine(LoadAndPlayAudio(tempAudioPath));
            }
        }
    }

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
                Debug.Log("开始播放TTS音频");
            }
        }
    }

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
}