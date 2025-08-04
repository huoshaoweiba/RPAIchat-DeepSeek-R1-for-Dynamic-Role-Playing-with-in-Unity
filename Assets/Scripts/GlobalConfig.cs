// GlobalConfig.cs
using UnityEngine;

public class GlobalConfig : MonoBehaviour
{
    public static GlobalConfig Instance;

    [Header("TTS 配置")]
    public string ttsMainFilePath;   // TTS主文件路径
    public bool textToSoundEnabled = true; // 是否启用TTS
    public string serviceUrl = "http://localhost:8000/tts"; // TTS服务地址
    public string roleName = "默认角色"; // 角色名称

    [Header("音频设置")]
    public AudioClip defaultErrorClip; // 错误时使用的备用音频

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 跨场景保留
        }
        else
        {
            Destroy(gameObject);
        }
    }
}