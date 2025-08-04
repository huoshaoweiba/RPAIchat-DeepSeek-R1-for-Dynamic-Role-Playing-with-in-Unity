using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using System.Text;
using System.Net.Sockets;
using System.Net;
using System;
using System.IO;
using UnityEngine.Networking;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

public class UDPSender : MonoBehaviour
{
    #region TTSģ������
    [Header("����ģ��")]
    [SerializeField] public DeepSeekDialogueManager m_ChatModel;
    [Header("��������")]
    [SerializeField] private AudioSource m_AudioSource;
    #endregion

    #region STT Settings
    [Header("�������밴ť")]
    [SerializeField] private Button m_VoiceInputButton;
    [Header("������ť�ı�")]
    [SerializeField] private TMP_Text m_VoiceButtonText;
    [Header("ʶ�����ı�")]
    [SerializeField] private TMP_Text m_RecognizedText;
    [Header("���������")]
    [SerializeField] public TMP_InputField m_InputWord;
    #endregion

    private Process process;
    private UdpClient sendClient;
    private UdpClient receiveClient;
    private IPEndPoint pythonEP;
    private IPEndPoint unityEP;
    private int pythonProcessId = -1;
    private string uniqueId;

    void Start()
    {
        // ȷ�����̵߳���������
        //UnityMainThreadDispatcher.Instance();
        _ = UnityMainThreadDispatcher.Instance;

        // ���������һ��
        m_InputWord = m_ChatModel.userInputField;
        
        // ����Ψһ��ʶ��
        uniqueId = $"TTStool_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        KillTargetPythonProcess();

        // ���ö˵�
        pythonEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 31415);
        unityEP = new IPEndPoint(IPAddress.Any, 31416);

        // ����UDP�ͻ���
        sendClient = new UdpClient();
        receiveClient = new UdpClient(unityEP);
        receiveClient.BeginReceive(ReceiveCallback, null);

        StartPythonProcess();
        RegisterButtonEvents();

    }
    //20250614
    #region Voice Input // ������������
    [Header("����ʶ��ű�")]
    public STT m_SpeechToText;
    [SerializeField] private bool m_AutoSend = true; // �Ƿ��Զ�����
    [SerializeField] private Button m_VoiceInputBotton; // �������밴ť
    [SerializeField] private Text m_VoiceBottonText; // ������ť�ı�
    //[SerializeField] private Text m_RecordTips; // ¼����ʾ
    [SerializeField] private VoiceInputs m_VoiceInputs; // �����������


    // ע�ᰴť�¼�
    private void RegistButtonEvent()
    {
        if (m_VoiceInputBotton == null || m_VoiceInputBotton.GetComponent<EventTrigger>()) // �����ťΪ�ջ��Ѿ�������¼�������
            return; // ֱ�ӷ���

        EventTrigger _trigger = m_VoiceInputBotton.gameObject.AddComponent<EventTrigger>(); // Ϊ��ť����¼����������

        EventTrigger.Entry _pointDown_entry = new EventTrigger.Entry(); // ���������¼���Ŀ
        _pointDown_entry.eventID = EventTriggerType.PointerDown; // �����¼�����Ϊ����
        _pointDown_entry.callback = new EventTrigger.TriggerEvent(); // ��ʼ���ص��¼�

        EventTrigger.Entry _pointUp_entry = new EventTrigger.Entry(); // ����̧���¼���Ŀ
        _pointUp_entry.eventID = EventTriggerType.PointerUp; // �����¼�����Ϊ̧��
        _pointUp_entry.callback = new EventTrigger.TriggerEvent(); // ��ʼ���ص��¼�

        _pointDown_entry.callback.AddListener(delegate { StartRecord(); }); // Ϊ�����¼���ӿ�ʼ¼���ص�
        _pointUp_entry.callback.AddListener(delegate { StopRecord(); }); // Ϊ̧���¼����ֹͣ¼���ص�

        _trigger.triggers.Add(_pointDown_entry); // �������¼���Ŀ��ӵ���������
        _trigger.triggers.Add(_pointUp_entry); // ��̧���¼���Ŀ��ӵ���������
    }

    // ��ʼ¼��
    public void StartRecord()
    {
        m_VoiceBottonText.text = "Recording..."; // ����������ť�ı�Ϊ������¼����
        m_VoiceInputs.StartRecordAudio(); // �����������������ʼ¼��
    }

    // ֹͣ¼��
    public void StopRecord()
    {
        m_VoiceBottonText.text = "Hold to record"; // ����������ť�ı�Ϊ����ס¼����
        //m_RecordTips.text = "Processing..."; // ����¼����ʾΪ�����ڴ���
        m_InputWord.text = "����ʶ����..."; // ����¼����ʾΪ�����ڴ���
        m_VoiceInputs.StopRecordAudio(AcceptClip); // ���������������ֹͣ¼����������Ƶ
    }

    // ������Ϣ
    public void SendData()
    {
        if (m_InputWord.text.Equals("")) // ��������Ϊ��
            return; // ֱ�ӷ���

        //if (m_CreateVoiceMode) // ����Ǵ�������ģʽ
        //{
        //    CallBack(m_InputWord.text); // ���ûص�����
        //    m_InputWord.text = ""; // ��������
        //    return;
        //}

        //m_ChatHistory.Add(m_InputWord.text); // ���������Ϣ��ӵ�������ʷ��
        string _msg = m_InputWord.text; // ��ȡ�������Ϣ

        m_ChatModel.OnSendMessage(_msg); // ��������ģ�ͷ�����Ϣ

        m_InputWord.text = ""; // ��������
    }

    // ������Ƶ����
    private void AcceptClip(AudioClip _audioClip)
    {
        if (m_SpeechToText == null) // ���û���������ı����
            return; // ֱ�ӷ���

        m_SpeechToText.SpeechToText(_audioClip, DealingTextCallback); // �����������ı��������Ƶת��Ϊ�ı������ûص�
    }

    // �����ı��ص�
    private void DealingTextCallback(string _msg)
    {
        //m_RecordTips.text = _msg; // ����¼����ʾΪת������ı�
        //m_RecordTips.text = _msg; // ����¼����ʾΪת������ı�
        //StartCoroutine(SetTextVisible(m_RecordTips)); // ��ʼЭ�������ı��ɼ���

        if (m_AutoSend) // ����Զ�����
        {
            m_ChatModel.OnSendMessage(_msg); // ����ת������ı�
            return;
        }

        m_InputWord.text = _msg; // ��ת������ı�����Ϊ���������
    }

    // �����ı��ɼ���Э��
    private IEnumerator SetTextVisible(Text _textbox)
    {
        yield return new WaitForSeconds(3f); // �ȴ�3��
        _textbox.text = ""; // ����ı�
    }
    #endregion
//20250614
    void StartPythonProcess()
    {
        string pythonPath = Path.Combine(Application.dataPath, "PY", "TTStool.py");
        if (!File.Exists(pythonPath))
        {
            UnityEngine.Debug.LogError($"Python�ű�������: {pythonPath}");
            return;
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = GetPythonExecutable();
        startInfo.Arguments = $"\"{pythonPath}\" --unique-id \"{uniqueId}\"";
        startInfo.CreateNoWindow = true;
        startInfo.UseShellExecute = false;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = Encoding.UTF8;
        startInfo.StandardErrorEncoding = Encoding.UTF8;

        process = new Process();
        process.StartInfo = startInfo;
        process.EnableRaisingEvents = true;

        // �������
        process.OutputDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log($"Python���: {e.Data}");

                // ���Դ��������ȡ����ID
                if (e.Data.StartsWith("TTStool PID:"))
                {
                    if (int.TryParse(e.Data.Split(':')[1].Trim(), out int pid))
                    {
                        pythonProcessId = pid;
                        UnityEngine.Debug.Log($"��¼Ŀ��Python����ID: {pid}");
                    }
                }
            }
        };

        // �������
        process.ErrorDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError($"Python����: {e.Data}");
        };

        // �����˳�ʱ����
        process.Exited += (s, e) => {
            UnityEngine.Debug.Log("Python�������˳�");
            pythonProcessId = -1;
        };

        // ��������
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            UnityEngine.Debug.Log($"����Python����: {process.Id}");

            // ��¼����ID��Ϊ��ѡ
            pythonProcessId = process.Id;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"����Python����ʧ��: {e.Message}");
        }
    }

    private string GetPythonExecutable()
    {
        // ����ƽ̨ѡ����ʵ�Python��ִ���ļ�
        if (Application.platform == RuntimePlatform.WindowsEditor ||
            Application.platform == RuntimePlatform.WindowsPlayer)
        {
            return "python.exe";
        }
        else
        {
            return "python3";
        }
    }

    private void KillTargetPythonProcess()
    {
        // ����1: ʹ�ü�¼�Ľ���ID
        if (pythonProcessId > 0)
        {
            try
            {
                Process targetProc = Process.GetProcessById(pythonProcessId);
                if (!targetProc.HasExited)
                {
                    UnityEngine.Debug.Log($"����Ŀ��Python����: {pythonProcessId}");
                    targetProc.Kill();
                    targetProc.WaitForExit(1000);
                }
            }
            catch (ArgumentException)
            {
                // ���̲�����
                UnityEngine.Debug.Log($"Ŀ����� {pythonProcessId} ������");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"����Ŀ�����ʧ��: {ex.Message}");
            }
            finally
            {
                pythonProcessId = -1;
            }
        }

        // ����2: ʹ�ý������ƺ������в���ƥ��
        Process[] allProcesses = Process.GetProcessesByName(GetPythonExecutable().Replace(".exe", ""));
        foreach (Process proc in allProcesses)
        {
            try
            {
                if (proc.Id == Process.GetCurrentProcess().Id) continue;

                // ʹ�ø��򵥵ķ�����ȡ�����в���
                string commandLine = GetProcessCommandLineSimple(proc);
                if (!string.IsNullOrEmpty(commandLine) &&
                    commandLine.Contains($"--unique-id \"{uniqueId}\""))
                {
                    UnityEngine.Debug.Log($"��������Python����: {proc.Id}");
                    proc.Kill();
                    proc.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"��������ʧ��: {ex.Message}");
            }
        }
    }

    // �򵥵Ľ��������л�ȡ������������WMI��
    private string GetProcessCommandLineSimple(Process process)
    {
        try
        {
            // ��Windows�ϣ����ǿ��Գ��Ի�ȡ����������Ϣ
            if (Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer)
            {
                // ע�⣺��ֻ�ܻ�ȡ��ǰ���������Ľ�����Ϣ
                if (process.StartInfo != null)
                {
                    return process.StartInfo.Arguments;
                }
            }

            // ����ƽ̨���޷���ȡ����������ؿ�
            return string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private void ReceiveCallback(System.IAsyncResult result)
    {
        try
        {
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
            byte[] data = receiveClient.EndReceive(result, ref remoteEP);
            string audioPath = Encoding.UTF8.GetString(data);

            // // ʹ�õ��������̵߳���������:ԭ����:Instance().xxx

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                UnityEngine.Debug.Log($"�յ���Ƶ·��: {audioPath}");
                StartCoroutine(LoadAndPlayAudio(audioPath));
            });

            // ��������
            receiveClient.BeginReceive(ReceiveCallback, null);
        }
        catch (ObjectDisposedException)
        {
            // �����ر�
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"���ջص�����: {e.Message}");
        }
    }

    //private void ReceiveCallback(System.IAsyncResult result)
    //{
    //    try
    //    {
    //        IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
    //        byte[] data = receiveClient.EndReceive(result, ref remoteEP);
    //        string message = Encoding.UTF8.GetString(data);

    //        UnityMainThreadDispatcher.Instance().Enqueue(() => {
    //            if (message.StartsWith("STT_RESULT:"))
    //            {
    //                string recognizedText = message.Substring("STT_RESULT:".Length);
    //                m_RecognizedText.text = recognizedText;
    //                UnityEngine.Debug.Log($"ʶ����: {recognizedText}");
    //                StartCoroutine(ClearRecognizedText());
    //            }
    //            else
    //            {
    //                UnityEngine.Debug.Log($"�յ���Ƶ·��: {message}");
    //                StartCoroutine(LoadAndPlayAudio(message));
    //            }
    //        });

    //        receiveClient.BeginReceive(ReceiveCallback, null);
    //    }
    //    catch (ObjectDisposedException)
    //    {
    //    }
    //    catch (Exception e)
    //    {
    //        UnityEngine.Debug.LogError($"���ջص�����: {e.Message}");
    //    }
    //}


    IEnumerator LoadAndPlayAudio(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            UnityEngine.Debug.LogError("��Ƶ·��Ϊ��");
            yield break;
        }

        // ����Windows·��
        string uri = filePath.Replace("\\", "/");
        if (!uri.StartsWith("file://"))
        {
            uri = "file:///" + uri;
        }

        UnityEngine.Debug.Log($"������Ƶ: {uri}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"��Ƶ����ʧ��: {www.error}\nURL: {uri}");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (clip == null)
            {
                UnityEngine.Debug.LogError("�޷�������Ƶ����");
                yield break;
            }

            m_AudioSource.clip = clip;
            m_AudioSource.Play();
            UnityEngine.Debug.Log("������Ƶ");
        }
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(1))
        {
            SendTextToPython();
        }
    }

    public void SendTextToPython()
    {
        if (m_ChatModel == null || string.IsNullOrEmpty(m_ChatModel.botMessage))
        {
            UnityEngine.Debug.LogWarning("û����Ч����Ϣ�ɷ���");
            return;
        }

        string message = m_ChatModel.botMessage;
        byte[] data = Encoding.UTF8.GetBytes(message);

        try
        {
            sendClient.Send(data, data.Length, pythonEP);
            UnityEngine.Debug.Log($"���͵�Python: {message}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"����ʧ��: {e.Message}");
        }
    }

    //public void SendTextToPython()
    //{
    //    if (m_ChatModel == null || string.IsNullOrEmpty(m_ChatModel.botMessage))
    //    {
    //        UnityEngine.Debug.LogWarning("û����Ч����Ϣ�ɷ���");
    //        return;
    //    }

    //    string message = m_ChatModel.botMessage;
    //    SendDataToPython("TTS_TEXT:" + message);
    //    //SendDataToPython(message);
    //}

    //private void SendDataToPython(string message)
    //{
    //    try
    //    {
    //        byte[] data = Encoding.UTF8.GetBytes(message);
    //        sendClient.Send(data, data.Length, pythonEP);
    //        UnityEngine.Debug.Log($"���͵�Python: {message}");
    //    }
    //    catch (Exception e)
    //    {
    //        UnityEngine.Debug.LogError($"����ʧ��: {e.Message}");
    //    }
    //}

    void OnApplicationQuit()
    {
        UnityEngine.Debug.Log("������Դ...");

        if (sendClient != null)
        {
            sendClient.Close();
            sendClient = null;
        }

        if (receiveClient != null)
        {
            receiveClient.Close();
            receiveClient = null;
        }

        KillTargetPythonProcess();

        if (process != null && !process.HasExited)
        {
            try
            {
                UnityEngine.Debug.Log($"����Python���̾��: {process.Id}");
                process.Kill();
                process.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"��������ʧ��: {ex.Message}");
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private void RegisterButtonEvents()
    {
        if (m_VoiceInputButton == null) return;
        EventTrigger trigger = m_VoiceInputButton.gameObject.AddComponent<EventTrigger>();
        EventTrigger.Entry pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((data) => { StartRecord(); });
        trigger.triggers.Add(pointerDown);
        EventTrigger.Entry pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((data) => { StopRecord(); });
        trigger.triggers.Add(pointerUp);
    }

    //public void StartRecord()
    //{
    //    m_VoiceButtonText.text = "�ɿ�ʶ��";
    //    m_RecognizedText.text = "����¼��...";
    //    //SendDataToPython("START_RECORD");
    //}

    //public void StopRecord()
    //{
    //    m_VoiceButtonText.text = "��ס˵��";
    //    m_RecognizedText.text = "ʶ����...";
    //    //SendDataToPython("STOP_RECORD");
    //}

    private IEnumerator ClearRecognizedText()
    {
        yield return new WaitForSeconds(5f);
        m_RecognizedText.text = "";
    }
}



// ���̵߳�����
//public class UnityMainThreadDispatcher : MonoBehaviour
//{
//    private static UnityMainThreadDispatcher instance;
//    private readonly Queue<System.Action> actions = new Queue<System.Action>();

//    public static UnityMainThreadDispatcher Instance()
//    {
//        if (instance == null)
//        {
//            GameObject go = new GameObject("MainThreadDispatcher");
//            instance = go.AddComponent<UnityMainThreadDispatcher>();
//            DontDestroyOnLoad(go);
//        }
//        return instance;
//    }


//    public void Enqueue(System.Action action)
//    {
//        lock (actions)
//        {
//            actions.Enqueue(action);
//        }
//    }

//    void Update()
//    {
//        lock (actions)
//        {
//            while (actions.Count > 0)
//            {
//                try
//                {
//                    actions.Dequeue().Invoke();
//                }
//                catch (Exception e)
//                {
//                    UnityEngine.Debug.LogError($"���߳��������: {e.Message}");
//                }
//            }
//        }
//    }
//}