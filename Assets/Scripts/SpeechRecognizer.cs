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
//        // 初始化UI
//        if (m_ButtonText) m_ButtonText.text = "Hold to Speak";
//        if (m_StatusText) m_StatusText.text = "";

//        // 设置按钮事件
//        if (m_RecordButton)
//        {
//            EventTrigger trigger = m_RecordButton.gameObject.AddComponent<EventTrigger>();

//            // 按下事件
//            var pointerDown = new EventTrigger.Entry();
//            pointerDown.eventID = EventTriggerType.PointerDown;
//            pointerDown.callback.AddListener((e) => StartRecording());
//            trigger.triggers.Add(pointerDown);

//            // 抬起事件
//            var pointerUp = new EventTrigger.Entry();
//            pointerUp.eventID = EventTriggerType.PointerUp;
//            pointerUp.callback.AddListener((e) => StopRecording());
//            trigger.triggers.Add(pointerUp);
//        }

//        // 初始化UDP客户端
//        InitializeUDP();
//    }

//    void InitializeUDP()
//    {
//        try
//        {
//            // 创建发送客户端（Unity -> Python）
//            sendClient = new UdpClient();

//            // 创建接收客户端（Python -> Unity）
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

//        // 更新UI
//        if (m_ButtonText) m_ButtonText.text = "Recording...";
//        if (m_StatusText)
//        {
//            m_StatusText.text = "Listening...";
//            if (statusResetCoroutine != null) StopCoroutine(statusResetCoroutine);
//        }

//        // 向Python发送开始录音命令
//        SendCommand("START");
//    }

//    void StopRecording()
//    {
//        if (!isRecording) return;

//        isRecording = false;

//        // 更新UI
//        if (m_ButtonText) m_ButtonText.text = "Hold to Speak";
//        if (m_StatusText)
//        {
//            m_StatusText.text = "Processing...";
//            if (statusResetCoroutine != null) StopCoroutine(statusResetCoroutine);
//        }

//        // 向Python发送停止录音命令
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

//            // 继续监听下一条消息
//            receiveClient.BeginReceive(ReceiveCallback, null);

//            string resultText = Encoding.UTF8.GetString(receivedData);
//            Debug.Log($"Received recognition result: {resultText}");

//            // 在主线程更新UI
//            UnityMainThreadDispatcher.Instance1().Enqueue1(() => ProcessRecognitionResult(resultText));
//        }
//        catch (ObjectDisposedException)
//        {
//            // 正常关闭
//        }
//        catch (Exception e)
//        {
//            Debug.LogError($"Receive callback error: {e.Message}");
//        }
//    }

//    void ProcessRecognitionResult(string resultText)
//    {
//        // 更新输入框
//        if (m_ResultInputField)
//        {
//            m_ResultInputField.text = resultText;
//        }

//        // 更新状态文本
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
//        // 清理资源
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

//// 主线程调度器（如果还没有的话）
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