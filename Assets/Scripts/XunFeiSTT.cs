//using UnityEngine;
//using UnityEngine.UI;
//using UnityEngine.EventSystems;
//using System;
//using System.Collections;
//using System.IO;
//using System.Text;
//using System.Security.Cryptography;
//using System.Net.Http;
//using System.Threading.Tasks;
//using System.Collections.Generic;

//public class XunfeiSTT : MonoBehaviour
//{
//    #region UI Settings
//    [Header("UI Settings")]
//    [SerializeField] private Button voiceInputButton;
//    [SerializeField] private Text voiceButtonText;
//    [SerializeField] private Text recognizedText;
//    #endregion

//    #region Audio Settings
//    [Header("Audio Settings")]
//    [SerializeField] private int sampleRate = 16000;
//    [SerializeField] private int maxRecordingTime = 60;
//    #endregion

//    #region Xunfei API Settings
//    [Header("Xunfei API Settings")]
//    [SerializeField] private string appId = "your_app_id";
//    [SerializeField] private string apiKey = "your_api_key";
//    [SerializeField] private string apiSecret = "your_api_secret";
//    [SerializeField] private string language = "en_us";
//    [SerializeField] private string accent = "mandarin";
//    #endregion

//    private AudioClip recordingClip;
//    private bool isRecording = false;
//    private string microphoneDevice;
//    private string fullResult = "";
//    private float startRecordingTime;
//    private Coroutine recordingCoroutine;
//    private HttpClient httpClient;

//    void Start()
//    {
//        // ��ʼ��HttpClient
//        httpClient = new HttpClient();
//        httpClient.Timeout = TimeSpan.FromSeconds(30);

//        // ��ʼ����˷�
//        if (Microphone.devices.Length > 0)
//        {
//            microphoneDevice = Microphone.devices[0];
//            Debug.Log("Using microphone: " + microphoneDevice);
//        }
//        else
//        {
//            Debug.LogError("No microphone devices found!");
//            voiceButtonText.text = "����˷�";
//        }

//        // ���ð�ť�¼�
//        SetupButtonEvents();
//    }

//    void SetupButtonEvents()
//    {
//        if (voiceInputButton == null) return;

//        EventTrigger trigger = voiceInputButton.gameObject.GetComponent<EventTrigger>();
//        if (trigger == null) trigger = voiceInputButton.gameObject.AddComponent<EventTrigger>();

//        // �����¼�
//        EventTrigger.Entry pointerDown = new EventTrigger.Entry
//        {
//            eventID = EventTriggerType.PointerDown
//        };
//        pointerDown.callback.AddListener((data) => { StartRecording(); });

//        // �ɿ��¼�
//        EventTrigger.Entry pointerUp = new EventTrigger.Entry
//        {
//            eventID = EventTriggerType.PointerUp
//        };
//        pointerUp.callback.AddListener((data) => { StopRecording(); });

//        trigger.triggers.Add(pointerDown);
//        trigger.triggers.Add(pointerUp);
//    }

//    public void StartRecording()
//    {
//        if (isRecording) return;

//        // ���ý��
//        fullResult = "";
//        recognizedText.text = "����¼��...";
//        voiceButtonText.text = "�ɿ�ʶ��";

//        // ��ʼ¼��
//        isRecording = true;
//        recordingClip = Microphone.Start(microphoneDevice, false, maxRecordingTime, sampleRate);
//        startRecordingTime = Time.time;
//    }

//    public void StopRecording()
//    {
//        if (!isRecording) return;

//        voiceButtonText.text = "��ס˵��";
//        recognizedText.text = "ʶ����...";

//        // ֹͣ¼��
//        Microphone.End(microphoneDevice);
//        isRecording = false;

//        // ����¼�����ݲ�����
//        StartCoroutine(ProcessAndSendAudio());
//    }

//    IEnumerator ProcessAndSendAudio()
//    {
//        // �ȴ�¼������
//        while (Microphone.IsRecording(microphoneDevice))
//        {
//            yield return null;
//        }

//        // ��ȡ¼������
//        float[] samples = new float[recordingClip.samples * recordingClip.channels];
//        recordingClip.GetData(samples, 0);

//        // ת��Ϊ16λPCM
//        byte[] pcmData = ConvertToPCM(samples);

//        // ���͵�Ѷ��API
//        StartCoroutine(SendToXunfeiAPI(pcmData));
//    }

//    byte[] ConvertToPCM(float[] samples)
//    {
//        byte[] pcmData = new byte[samples.Length * 2];
//        for (int i = 0; i < samples.Length; i++)
//        {
//            short sample = (short)(samples[i] * short.MaxValue);
//            pcmData[i * 2] = (byte)(sample & 0xFF);
//            pcmData[i * 2 + 1] = (byte)(sample >> 8);
//        }
//        return pcmData;
//    }

//    IEnumerator SendToXunfeiAPI(byte[] audioData)
//    {
//        // ������֤URL
//        string requestUrl = GenerateXunfeiRequestUrl();
//        Debug.Log("Sending to: " + requestUrl);

//        try
//        {
//            // �����ಿ�ֱ�����
//            var form = new MultipartFormDataContent();

//            // ���ͷ������
//            form.Add(new StringContent(appId), "app_id");
//            form.Add(new StringContent(language), "language");
//            form.Add(new StringContent(accent), "accent");
//            form.Add(new StringContent("audio/L16;rate=16000"), "aue");
//            form.Add(new StringContent("raw"), "encoding");

//            // �����Ƶ����
//            var audioContent = new ByteArrayContent(audioData);
//            audioContent.Headers.Add("Content-Type", "application/octet-stream");
//            form.Add(audioContent, "audio", "recording.wav");

//            // ��������
//            Task<HttpResponseMessage> sendTask = httpClient.PostAsync(requestUrl, form);
//            yield return new WaitUntil(() => sendTask.IsCompleted);

//            if (sendTask.Result.IsSuccessStatusCode)
//            {
//                // ������Ӧ
//                Task<string> readTask = sendTask.Result.Content.ReadAsStringAsync();
//                yield return new WaitUntil(() => readTask.IsCompleted);

//                ProcessXunfeiResponse(readTask.Result);
//            }
//            else
//            {
//                Debug.LogError($"API request failed: {sendTask.Result.StatusCode}");
//                recognizedText.text = "ʶ��ʧ��";
//            }
//        }
//        catch (Exception e)
//        {
//            Debug.LogError("Request exception: " + e.Message);
//            recognizedText.text = "�������";
//        }
//    }

//    string GenerateXunfeiRequestUrl()
//    {
//        // ����ʱ�����RFC1123��ʽ��
//        DateTime now = DateTime.UtcNow;
//        string date = now.ToString("r");

//        // ����ǩ��ԭʼ�ַ���
//        string signatureOrigin = $"host: api.xfyun.cn\ndate: {date}\nPOST /v2/iat HTTP/1.1";

//        // ����hmac-sha256����
//        byte[] signatureBytes = null;
//        using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(apiSecret)))
//        {
//            signatureBytes = hmac.ComputeHash(Encoding.UTF8.GetBytes(signatureOrigin));
//        }
//        string signatureSha = Convert.ToBase64String(signatureBytes);

//        // ������Ȩ�ַ���
//        string authorizationOrigin = $"api_key=\"{apiKey}\", algorithm=\"hmac-sha256\", headers=\"host date request-line\", signature=\"{signatureSha}\"";
//        string authorization = Convert.ToBase64String(Encoding.UTF8.GetBytes(authorizationOrigin));

//        // ����URL
//        return $"https://api.xfyun.cn/v2/iat?authorization={Uri.EscapeDataString(authorization)}&date={Uri.EscapeDataString(date)}&host=api.xfyun.cn";
//    }

//    void ProcessXunfeiResponse(string response)
//    {
//        try
//        {
//            // ����JSON��Ӧ
//            var json = JsonUtility.FromJson<XunfeiResponse>(response);

//            if (json != null && json.code == "0" && json.data != null)
//            {
//                // ��ȡʶ����
//                string result = "";
//                foreach (var item in json.data.result.ws)
//                {
//                    foreach (var cw in item.cw)
//                    {
//                        result += cw.w;
//                    }
//                }

//                // ����UI
//                recognizedText.text = result;
//            }
//            else
//            {
//                Debug.LogError($"API error: {json?.desc ?? "Unknown error"}");
//                recognizedText.text = "ʶ��ʧ��";
//            }
//        }
//        catch (Exception e)
//        {
//            Debug.LogError("Response parsing error: " + e.Message);
//            recognizedText.text = "��������";
//        }
//    }

//    void Update()
//    {
//        // ��ʾ¼��ʱ��
//        if (isRecording)
//        {
//            float elapsed = Time.time - startRecordingTime;
//            voiceButtonText.text = $"¼����: {elapsed:F1}��";
//        }
//    }

//    void OnDestroy()
//    {
//        // ������Դ
//        if (httpClient != null)
//        {
//            httpClient.Dispose();
//        }
//    }

//    #region Data Structures for JSON Serialization
//    [System.Serializable]
//    public class XunfeiResponse
//    {
//        public string code;
//        public string message;
//        public string sid;
//        public string desc;
//        public ResponseData data;
//    }

//    [System.Serializable]
//    public class ResponseData
//    {
//        public ResultData result;
//    }

//    [System.Serializable]
//    public class ResultData
//    {
//        public WSData[] ws;
//    }

//    [System.Serializable]
//    public class WSData
//    {
//        public CWData[] cw;
//        public int bg;
//        public int ed;
//    }

//    [System.Serializable]
//    public class CWData
//    {
//        public float sc;
//        public string w;
//    }
//    #endregion
//}