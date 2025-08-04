//using UnityEngine;
//using UnityEngine.UI;
//using System;
//using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine.EventSystems;

//public class SpeechRecognizer : MonoBehaviour
//{
//    [Header("UI Components")]
//    [SerializeField] private Button m_RecordButton;
//    [SerializeField] private Text m_ButtonText;
//    [SerializeField] private InputField m_ResultInputField;
//    [SerializeField] private Text m_StatusText;

//    [Header("Settings")]
//    [SerializeField] private string pythonIP = "127.0.0.1";
//    [SerializeField] private int sendPort = 31417;   // Unity -> Python
//    [SerializeField] private int receivePort = 31418; // Python -> Unity

//    private UdpClient sendClient;
//    private UdpClient receiveClient;
//    private bool isRecording = false;
//    private Coroutine statusResetCoroutine;

//    void Start()
//    {
//        // ��ʼ��UI
//        if (m_ButtonText) m_ButtonText.text = "Hold to Speak";
//        if (m_StatusText) m_StatusText.text = "";

//        // ���ð�ť�¼�
//        if (m_RecordButton)
//        {
//            EventTrigger trigger = m_RecordButton.gameObject.AddComponent<EventTrigger>();

//            // �����¼�
//            var pointerDown = new EventTrigger.Entry();
//            pointerDown.eventID = EventTriggerType.PointerDown;
//            pointerDown.callback.AddListener((e) => StartRecording());
//            trigger.triggers.Add(pointerDown);

//            // ̧���¼�
//            var pointerUp = new EventTrigger.Entry();
//            pointerUp.eventID = EventTriggerType.PointerUp;
//            pointerUp.callback.AddListener((e) => StopRecording());
//            trigger.triggers.Add(pointerUp);
//        }

//        // ��ʼ��UDP�ͻ���
//        InitializeUDP();
//    }

//    void InitializeUDP()
//    {
//        try
//        {
//            // �������Ϳͻ��ˣ�Unity -> Python��
//            sendClient = new UdpClient();

//            // �������տͻ��ˣ�Python -> Unity��
//            receiveClient = new UdpClient(receivePort);
//            receiveClient.BeginReceive(ReceiveCallback, null);

//            Debug.Log($"UDP initialized. Listening on port {receivePort}");
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"UDP initialization failed: {e.Message}");
//        }
//    }

//    void StartRecording()
//    {
//        if (isRecording) return;

//        isRecording = true;

//        // ����UI
//        if (m_ButtonText) m_ButtonText.text = "Recording...";
//        if (m_StatusText)
//        {
//            m_StatusText.text = "Listening...";
//            if (statusResetCoroutine != null) StopCoroutine(statusResetCoroutine);
//        }

//        // ��Python���Ϳ�ʼ¼������
//        SendCommand("START");
//    }

//    void StopRecording()
//    {
//        if (!isRecording) return;

//        isRecording = false;

//        // ����UI
//        if (m_ButtonText) m_ButtonText.text = "Hold to Speak";
//        if (m_StatusText)
//        {
//            m_StatusText.text = "Processing...";
//            if (statusResetCoroutine != null) StopCoroutine(statusResetCoroutine);
//        }

//        // ��Python����ֹͣ¼������
//        SendCommand("STOP");
//    }

//    void SendCommand(string command)
//    {
//        try
//        {
//            byte[] data = Encoding.UTF8.GetBytes(command);
//            sendClient.Send(data, data.Length, pythonIP, sendPort);
//            Debug.Log($"Sent command: {command}");
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"Failed to send command: {e.Message}");
//        }
//    }

//    private void ReceiveCallback(IAsyncResult result)
//    {
//        try
//        {
//            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Any, 0);
//            byte[] receivedData = receiveClient.EndReceive(result, ref remoteEP);

//            // ����������һ����Ϣ
//            receiveClient.BeginReceive(ReceiveCallback, null);

//            string resultText = Encoding.UTF8.GetString(receivedData);
//            Debug.Log($"Received recognition result: {resultText}");

//            // �����̸߳���UI
//            UnityMainThreadDispatcher.Instance1().Enqueue1(() => ProcessRecognitionResult(resultText));
//        }
//        catch (ObjectDisposedException)
//        {
//            // �����ر�
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"Receive callback error: {e.Message}");
//        }
//    }

//    void ProcessRecognitionResult(string resultText)
//    {
//        // ���������
//        if (m_ResultInputField)
//        {
//            m_ResultInputField.text = resultText;
//        }

//        // ����״̬�ı�
//        if (m_StatusText)
//        {
//            m_StatusText.text = "Recognition complete!";
//            statusResetCoroutine = StartCoroutine(ResetStatusText());
//        }
//    }

//    IEnumerator ResetStatusText()
//    {
//        yield return new WaitForSeconds(3f);
//        if (m_StatusText) m_StatusText.text = "";
//        statusResetCoroutine = null;
//    }

//    void OnDestroy()
//    {
//        // ������Դ
//        if (sendClient != null)
//        {
//            sendClient.Close();
//            sendClient = null;
//        }

//        if (receiveClient != null)
//        {
//            receiveClient.Close();
//            receiveClient = null;
//        }
//    }
//}

//// ���̵߳������������û�еĻ���
//public class UnityMainThreadDispatcher : MonoBehaviour
//{
//    private static UnityMainThreadDispatcher instance1;
//    private readonly Queue<Action> actions1 = new Queue<Action>();

//    public static UnityMainThreadDispatcher Instance1()
//    {
//        if (instance1 == null)
//        {
//            GameObject go = new GameObject("MainThreadDispatcher");
//            instance1 = go.AddComponent<UnityMainThreadDispatcher>();
//            DontDestroyOnLoad(go);
//        }
//        return instance1;
//    }

//    public void Enqueue1(Action action)
//    {
//        lock (actions1)
//        {
//            actions1.Enqueue(action);
//        }
//    }

//    void Update()
//    {
//        lock (actions1)
//        {
//            while (actions1.Count > 0)
//            {
//                actions1.Dequeue().Invoke();
//            }
//        }
//    }
//}