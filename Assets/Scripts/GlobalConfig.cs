// GlobalConfig.cs
using UnityEngine;

public class GlobalConfig : MonoBehaviour
{
    public static GlobalConfig Instance;

    [Header("TTS ����")]
    public string ttsMainFilePath;   // TTS���ļ�·��
    public bool textToSoundEnabled = true; // �Ƿ�����TTS
    public string serviceUrl = "http://localhost:8000/tts"; // TTS�����ַ
    public string roleName = "Ĭ�Ͻ�ɫ"; // ��ɫ����

    [Header("��Ƶ����")]
    public AudioClip defaultErrorClip; // ����ʱʹ�õı�����Ƶ

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // �糡������
        }
        else
        {
            Destroy(gameObject);
        }
    }
}