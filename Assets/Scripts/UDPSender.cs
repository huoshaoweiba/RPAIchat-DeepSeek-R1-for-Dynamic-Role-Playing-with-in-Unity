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
    #region TTS模型设置
    [Header("聊天模型")]
    [SerializeField] public DeepSeekDialogueManager m_ChatModel;
    [Header("发音器官")]
    [SerializeField] private AudioSource m_AudioSource;
    #endregion

    #region STT Settings
    [Header("语音输入按钮")]
    [SerializeField] private Button m_VoiceInputButton;
    [Header("语音按钮文本")]
    [SerializeField] private TMP_Text m_VoiceButtonText;
    [Header("识别结果文本")]
    [SerializeField] private TMP_Text m_RecognizedText;
    [Header("语音输入框")]
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
        // 确保主线程调度器存在
        //UnityMainThreadDispatcher.Instance();
        _ = UnityMainThreadDispatcher.Instance;

        // 输入框连接一下
        m_InputWord = m_ChatModel.userInputField;
        
        // 生成唯一标识符
        uniqueId = $"TTStool_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

        KillTargetPythonProcess();

        // 设置端点
        pythonEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 31415);
        unityEP = new IPEndPoint(IPAddress.Any, 31416);

        // 创建UDP客户端
        sendClient = new UdpClient();
        receiveClient = new UdpClient(unityEP);
        receiveClient.BeginReceive(ReceiveCallback, null);

        StartPythonProcess();
        RegisterButtonEvents();

    }
    //20250614
    #region Voice Input // 语音输入区域
    [Header("语音识别脚本")]
    public STT m_SpeechToText;
    [SerializeField] private bool m_AutoSend = true; // 是否自动发送
    [SerializeField] private Button m_VoiceInputBotton; // 语音输入按钮
    [SerializeField] private Text m_VoiceBottonText; // 语音按钮文本
    //[SerializeField] private Text m_RecordTips; // 录音提示
    [SerializeField] private VoiceInputs m_VoiceInputs; // 语音输入组件


    // 注册按钮事件
    private void RegistButtonEvent()
    {
        if (m_VoiceInputBotton == null || m_VoiceInputBotton.GetComponent<EventTrigger>()) // 如果按钮为空或已经添加了事件触发器
            return; // 直接返回

        EventTrigger _trigger = m_VoiceInputBotton.gameObject.AddComponent<EventTrigger>(); // 为按钮添加事件触发器组件

        EventTrigger.Entry _pointDown_entry = new EventTrigger.Entry(); // 创建按下事件条目
        _pointDown_entry.eventID = EventTriggerType.PointerDown; // 设置事件类型为按下
        _pointDown_entry.callback = new EventTrigger.TriggerEvent(); // 初始化回调事件

        EventTrigger.Entry _pointUp_entry = new EventTrigger.Entry(); // 创建抬起事件条目
        _pointUp_entry.eventID = EventTriggerType.PointerUp; // 设置事件类型为抬起
        _pointUp_entry.callback = new EventTrigger.TriggerEvent(); // 初始化回调事件

        _pointDown_entry.callback.AddListener(delegate { StartRecord(); }); // 为按下事件添加开始录音回调
        _pointUp_entry.callback.AddListener(delegate { StopRecord(); }); // 为抬起事件添加停止录音回调

        _trigger.triggers.Add(_pointDown_entry); // 将按下事件条目添加到触发器中
        _trigger.triggers.Add(_pointUp_entry); // 将抬起事件条目添加到触发器中
    }

    // 开始录音
    public void StartRecord()
    {
        m_VoiceBottonText.text = "Recording..."; // 设置语音按钮文本为“正在录音”
        m_VoiceInputs.StartRecordAudio(); // 调用语音输入组件开始录音
    }

    // 停止录音
    public void StopRecord()
    {
        m_VoiceBottonText.text = "Hold to record"; // 设置语音按钮文本为“按住录音”
        //m_RecordTips.text = "Processing..."; // 设置录音提示为“正在处理”
        m_InputWord.text = "语音识别中..."; // 设置录音提示为“正在处理”
        m_VoiceInputs.StopRecordAudio(AcceptClip); // 调用语音输入组件停止录音并处理音频
    }

    // 发送消息
    public void SendData()
    {
        if (m_InputWord.text.Equals("")) // 如果输入框为空
            return; // 直接返回

        //if (m_CreateVoiceMode) // 如果是创建语音模式
        //{
        //    CallBack(m_InputWord.text); // 调用回调函数
        //    m_InputWord.text = ""; // 清空输入框
        //    return;
        //}

        //m_ChatHistory.Add(m_InputWord.text); // 将输入的消息添加到聊天历史中
        string _msg = m_InputWord.text; // 获取输入的消息

        m_ChatModel.OnSendMessage(_msg); // 调用聊天模型发送消息

        m_InputWord.text = ""; // 清空输入框
    }

    // 接收音频剪辑
    private void AcceptClip(AudioClip _audioClip)
    {
        if (m_SpeechToText == null) // 如果没有语音到文本组件
            return; // 直接返回

        m_SpeechToText.SpeechToText(_audioClip, DealingTextCallback); // 调用语音到文本组件将音频转换为文本并设置回调
    }

    // 处理文本回调
    private void DealingTextCallback(string _msg)
    {
        //m_RecordTips.text = _msg; // 设置录音提示为转换后的文本
        //m_RecordTips.text = _msg; // 设置录音提示为转换后的文本
        //StartCoroutine(SetTextVisible(m_RecordTips)); // 开始协程设置文本可见性

        if (m_AutoSend) // 如果自动发送
        {
            m_ChatModel.OnSendMessage(_msg); // 发送转换后的文本
            return;
        }

        m_InputWord.text = _msg; // 将转换后的文本设置为输入框内容
    }

    // 设置文本可见性协程
    private IEnumerator SetTextVisible(Text _textbox)
    {
        yield return new WaitForSeconds(3f); // 等待3秒
        _textbox.text = ""; // 清空文本
    }
    #endregion
//20250614
    void StartPythonProcess()
    {
        string pythonPath = Path.Combine(Application.dataPath, "PY", "TTStool.py");
        if (!File.Exists(pythonPath))
        {
            UnityEngine.Debug.LogError($"Python脚本不存在: {pythonPath}");
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

        // 处理输出
        process.OutputDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data))
            {
                UnityEngine.Debug.Log($"Python输出: {e.Data}");

                // 尝试从输出中提取进程ID
                if (e.Data.StartsWith("TTStool PID:"))
                {
                    if (int.TryParse(e.Data.Split(':')[1].Trim(), out int pid))
                    {
                        pythonProcessId = pid;
                        UnityEngine.Debug.Log($"记录目标Python进程ID: {pid}");
                    }
                }
            }
        };

        // 处理错误
        process.ErrorDataReceived += (s, e) => {
            if (!string.IsNullOrEmpty(e.Data))
                UnityEngine.Debug.LogError($"Python错误: {e.Data}");
        };

        // 进程退出时处理
        process.Exited += (s, e) => {
            UnityEngine.Debug.Log("Python进程已退出");
            pythonProcessId = -1;
        };

        // 启动进程
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            UnityEngine.Debug.Log($"启动Python进程: {process.Id}");

            // 记录进程ID作为备选
            pythonProcessId = process.Id;
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"启动Python进程失败: {e.Message}");
        }
    }

    private string GetPythonExecutable()
    {
        // 根据平台选择合适的Python可执行文件
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
        // 方法1: 使用记录的进程ID
        if (pythonProcessId > 0)
        {
            try
            {
                Process targetProc = Process.GetProcessById(pythonProcessId);
                if (!targetProc.HasExited)
                {
                    UnityEngine.Debug.Log($"结束目标Python进程: {pythonProcessId}");
                    targetProc.Kill();
                    targetProc.WaitForExit(1000);
                }
            }
            catch (ArgumentException)
            {
                // 进程不存在
                UnityEngine.Debug.Log($"目标进程 {pythonProcessId} 不存在");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"结束目标进程失败: {ex.Message}");
            }
            finally
            {
                pythonProcessId = -1;
            }
        }

        // 方法2: 使用进程名称和命令行参数匹配
        Process[] allProcesses = Process.GetProcessesByName(GetPythonExecutable().Replace(".exe", ""));
        foreach (Process proc in allProcesses)
        {
            try
            {
                if (proc.Id == Process.GetCurrentProcess().Id) continue;

                // 使用更简单的方法获取命令行参数
                string commandLine = GetProcessCommandLineSimple(proc);
                if (!string.IsNullOrEmpty(commandLine) &&
                    commandLine.Contains($"--unique-id \"{uniqueId}\""))
                {
                    UnityEngine.Debug.Log($"结束关联Python进程: {proc.Id}");
                    proc.Kill();
                    proc.WaitForExit(1000);
                }
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"结束进程失败: {ex.Message}");
            }
        }
    }

    // 简单的进程命令行获取方法（不依赖WMI）
    private string GetProcessCommandLineSimple(Process process)
    {
        try
        {
            // 在Windows上，我们可以尝试获取进程启动信息
            if (Application.platform == RuntimePlatform.WindowsEditor ||
                Application.platform == RuntimePlatform.WindowsPlayer)
            {
                // 注意：这只能获取当前进程启动的进程信息
                if (process.StartInfo != null)
                {
                    return process.StartInfo.Arguments;
                }
            }

            // 其他平台或无法获取的情况，返回空
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

            // // 使用单例调主线程调度器处理:原本是:Instance().xxx

            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                UnityEngine.Debug.Log($"收到音频路径: {audioPath}");
                StartCoroutine(LoadAndPlayAudio(audioPath));
            });

            // 继续监听
            receiveClient.BeginReceive(ReceiveCallback, null);
        }
        catch (ObjectDisposedException)
        {
            // 正常关闭
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"接收回调错误: {e.Message}");
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
    //                UnityEngine.Debug.Log($"识别结果: {recognizedText}");
    //                StartCoroutine(ClearRecognizedText());
    //            }
    //            else
    //            {
    //                UnityEngine.Debug.Log($"收到音频路径: {message}");
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
    //        UnityEngine.Debug.LogError($"接收回调错误: {e.Message}");
    //    }
    //}


    IEnumerator LoadAndPlayAudio(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            UnityEngine.Debug.LogError("音频路径为空");
            yield break;
        }

        // 处理Windows路径
        string uri = filePath.Replace("\\", "/");
        if (!uri.StartsWith("file://"))
        {
            uri = "file:///" + uri;
        }

        UnityEngine.Debug.Log($"加载音频: {uri}");

        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(uri, AudioType.WAV))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                UnityEngine.Debug.LogError($"音频加载失败: {www.error}\nURL: {uri}");
                yield break;
            }

            AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
            if (clip == null)
            {
                UnityEngine.Debug.LogError("无法创建音频剪辑");
                yield break;
            }

            m_AudioSource.clip = clip;
            m_AudioSource.Play();
            UnityEngine.Debug.Log("播放音频");
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
            UnityEngine.Debug.LogWarning("没有有效的消息可发送");
            return;
        }

        string message = m_ChatModel.botMessage;
        byte[] data = Encoding.UTF8.GetBytes(message);

        try
        {
            sendClient.Send(data, data.Length, pythonEP);
            UnityEngine.Debug.Log($"发送到Python: {message}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"发送失败: {e.Message}");
        }
    }

    //public void SendTextToPython()
    //{
    //    if (m_ChatModel == null || string.IsNullOrEmpty(m_ChatModel.botMessage))
    //    {
    //        UnityEngine.Debug.LogWarning("没有有效的消息可发送");
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
    //        UnityEngine.Debug.Log($"发送到Python: {message}");
    //    }
    //    catch (Exception e)
    //    {
    //        UnityEngine.Debug.LogError($"发送失败: {e.Message}");
    //    }
    //}

    void OnApplicationQuit()
    {
        UnityEngine.Debug.Log("清理资源...");

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
                UnityEngine.Debug.Log($"结束Python进程句柄: {process.Id}");
                process.Kill();
                process.WaitForExit(1000);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"结束进程失败: {ex.Message}");
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
    //    m_VoiceButtonText.text = "松开识别";
    //    m_RecognizedText.text = "正在录音...";
    //    //SendDataToPython("START_RECORD");
    //}

    //public void StopRecord()
    //{
    //    m_VoiceButtonText.text = "按住说话";
    //    m_RecognizedText.text = "识别中...";
    //    //SendDataToPython("STOP_RECORD");
    //}

    private IEnumerator ClearRecognizedText()
    {
        yield return new WaitForSeconds(5f);
        m_RecognizedText.text = "";
    }
}



// 主线程调度器
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
//                    UnityEngine.Debug.LogError($"主线程任务错误: {e.Message}");
//                }
//            }
//        }
//    }
//}