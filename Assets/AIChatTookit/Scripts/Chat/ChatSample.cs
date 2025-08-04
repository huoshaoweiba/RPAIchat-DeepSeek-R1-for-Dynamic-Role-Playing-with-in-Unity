using System.Collections; // 使用系统集合命名空间
using System.Collections.Generic; // 使用泛型集合命名空间
using UnityEngine; // 使用Unity引擎命名空间
using UnityEngine.EventSystems; // 使用Unity事件系统命名空间
using UnityEngine.UI; // 使用Unity UI命名空间
using WebGLSupport; // 使用WebGL支持命名空间


// 定义一个名为ChatSample的类，继承自MonoBehaviour，表示这是一个Unity组件
public class ChatSample : MonoBehaviour
{
    // 序列化字段，可以在Unity编辑器中直接设置
    [SerializeField] private ChatSetting m_ChatSettings; // 聊天设置

    #region UI Elements // UI元素区域
    [SerializeField] private GameObject m_ChatPanel; // 聊天面板
    [SerializeField] public InputField m_InputWord; // 输入框
    [SerializeField] private Text m_TextBack; // 文本反馈显示
    [SerializeField] private AudioSource m_AudioSource; // 音频源
    [SerializeField] private Button m_CommitMsgBtn; // 发送消息按钮
    #endregion

    #region Speech Settings // 语音设置区域
    [SerializeField] private Animator m_Animator; // 动画控制器
    [SerializeField] private bool m_IsVoiceMode = true; // 是否开启语音模式
    [SerializeField] private bool m_CreateVoiceMode = false; // 是否创建语音模式
    #endregion

    // 在Awake方法中初始化按钮事件监听、按钮事件注册和WebGL输入设置
    private void Awake()
    {
        m_CommitMsgBtn.onClick.AddListener(delegate { SendData(); }); // 为发送消息按钮添加点击事件监听
        RegistButtonEvent(); // 注册按钮事件
        InputSettingWhenWebgl(); // 设置WebGL输入
    }

    #region Message Handling // 消息处理区域
    // 设置WebGL输入
    private void InputSettingWhenWebgl()
    {
#if UNITY_WEBGL // 如果是WebGL平台
        m_InputWord.gameObject.AddComponent<WebGLSupport.WebGLInput>(); // 为输入框添加WebGL输入组件
#endif
    }

    // 发送消息
    public void SendData()
    {
        if (m_InputWord.text.Equals("")) // 如果输入框为空
            return; // 直接返回

        if (m_CreateVoiceMode) // 如果是创建语音模式
        {
            CallBack(m_InputWord.text); // 调用回调函数
            m_InputWord.text = ""; // 清空输入框
            return;
        }

        m_ChatHistory.Add(m_InputWord.text); // 将输入的消息添加到聊天历史中
        string _msg = m_InputWord.text; // 获取输入的消息

        m_ChatSettings.m_ChatModel.PostMsg(_msg, CallBack); // 调用聊天模型发送消息并设置回调

        m_InputWord.text = ""; // 清空输入框
        m_TextBack.text = "Thinking..."; // 设置文本反馈为“思考中”
        SetAnimator("state", 1); // 设置动画状态为1
    }

    // 重载的发送消息方法，用于直接发送字符串消息
    public void SendData(string _postWord)
    {
        if (_postWord.Equals("")) // 如果消息为空
            return; // 直接返回

        if (m_CreateVoiceMode) // 如果是创建语音模式
        {
            CallBack(_postWord); // 调用回调函数
            m_InputWord.text = ""; // 清空输入框
            return;
        }

        m_ChatHistory.Add(_postWord); // 将消息添加到聊天历史中
        string _msg = _postWord; // 获取消息

        m_ChatSettings.m_ChatModel.PostMsg(_msg, CallBack); // 调用聊天模型发送消息并设置回调

        m_InputWord.text = ""; // 清空输入框
        m_TextBack.text = "Thinking..."; // 设置文本反馈为“思考中”
        SetAnimator("state", 1); // 设置动画状态为1
    }

    // 回调函数
    private void CallBack(string _response)
    {
        _response = _response.Trim(); // 去除响应消息的前后空格
        m_TextBack.text = ""; // 清空文本反馈

        Debug.Log("AI response: " + _response); // 在控制台打印AI响应
        m_ChatHistory.Add(_response); // 将响应添加到聊天历史中

        if (!m_IsVoiceMode || m_ChatSettings.m_TextToSpeech == null) // 如果不是语音模式或没有文本到语音组件
        {
            StartTypeWords(_response); // 开始逐字显示响应消息
            return;
        }

        m_ChatSettings.m_TextToSpeech.Speak(_response, PlayVoice); // 调用文本到语音组件播放响应消息
    }
    #endregion

    #region Voice Input // 语音输入区域
    [SerializeField] private bool m_AutoSend = true; // 是否自动发送
    [SerializeField] private Button m_VoiceInputBotton; // 语音输入按钮
    [SerializeField] private Text m_VoiceBottonText; // 语音按钮文本
    [SerializeField] private Text m_RecordTips; // 录音提示
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
        m_RecordTips.text = "Processing..."; // 设置录音提示为“正在处理”
        m_VoiceInputs.StopRecordAudio(AcceptClip); // 调用语音输入组件停止录音并处理音频
    }

    // 接收音频剪辑
    private void AcceptClip(AudioClip _audioClip)
    {
        if (m_ChatSettings.m_SpeechToText == null) // 如果没有语音到文本组件
            return; // 直接返回

        m_ChatSettings.m_SpeechToText.SpeechToText(_audioClip, DealingTextCallback); // 调用语音到文本组件将音频转换为文本并设置回调
    }

    // 处理文本回调
    private void DealingTextCallback(string _msg)
    {
        m_RecordTips.text = _msg; // 设置录音提示为转换后的文本
        StartCoroutine(SetTextVisible(m_RecordTips)); // 开始协程设置文本可见性

        if (m_AutoSend) // 如果自动发送
        {
            SendData(_msg); // 发送转换后的文本
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

    #region Voice Synthesis // 语音合成区域
    // 播放语音
    private void PlayVoice(AudioClip _clip, string _response)
    {
        m_AudioSource.clip = _clip; // 设置音频源的音频剪辑
        m_AudioSource.Play(); // 播放音频
        Debug.Log("Audio length: " + _clip.length); // 在控制台打印音频长度
        StartTypeWords(_response); // 开始逐字显示响应消息
        SetAnimator("state", 2); // 设置动画状态为2
    }
    #endregion

    #region Typewriter Effect // 打字机效果区域
    [SerializeField] private float m_WordWaitTime = 0.2f; // 每个字的等待时间
    [SerializeField] private bool m_WriteState = false; // 写入状态

    // 开始逐字显示消息
    private void StartTypeWords(string _msg)
    {
        if (_msg == "") // 如果消息为空
            return; // 直接返回

        m_WriteState = true; // 设置写入状态为true
        StartCoroutine(SetTextPerWord(_msg)); // 开始协程逐字显示消息
    }

    // 逐字显示消息协程
    private IEnumerator SetTextPerWord(string _msg)
    {
        int currentPos = 0; // 当前显示的字符位置
        while (m_WriteState) // 当写入状态为true时
        {
            yield return new WaitForSeconds(m_WordWaitTime); // 等待指定时间
            currentPos++; // 当前位置加1
            m_TextBack.text = _msg.Substring(0, currentPos); // 设置文本反馈为当前显示的消息
            m_WriteState = currentPos < _msg.Length; // 如果当前位置小于消息长度，则继续写入
        }
        SetAnimator("state", 0); // 设置动画状态为0
    }
    #endregion

    #region Chat History // 聊天历史区域
    [SerializeField] private List<string> m_ChatHistory; // 聊天历史列表
    [SerializeField] private List<GameObject> m_TempChatBox; // 临时聊天框列表
    [SerializeField] private GameObject m_HistoryPanel; // 聊天历史面板
    [SerializeField] private RectTransform m_rootTrans; // 根布局变换
    [SerializeField] private ChatPrefab m_PostChatPrefab; // 发送消息聊天预制体
    [SerializeField] private ChatPrefab m_RobotChatPrefab; // 机器人消息聊天预制体
    [SerializeField] private ScrollRect m_ScroTectObject; // 滚动视图

    // 打开并获取聊天历史
    public void OpenAndGetHistory()
    {
        m_ChatPanel.SetActive(false); // 关闭聊天面板
        m_HistoryPanel.SetActive(true); // 打开聊天历史面板
        ClearChatBox(); // 清空临时聊天框
        StartCoroutine(GetHistoryChatInfo()); // 开始协程获取聊天历史信息
    }

    // 返回聊天模式
    public void BackChatMode()
    {
        m_ChatPanel.SetActive(true); // 打开聊天面板
        m_HistoryPanel.SetActive(false); // 关闭聊天历史面板
    }

    // 清空临时聊天框
    private void ClearChatBox()
    {
        while (m_TempChatBox.Count != 0) // 当临时聊天框列表不为空时
        {
            if (m_TempChatBox[0]) // 如果第一个元素存在
            {
                Destroy(m_TempChatBox[0].gameObject); // 销毁第一个元素的游戏对象
                m_TempChatBox.RemoveAt(0); // 从列表中移除第一个元素
            }
        }
        m_TempChatBox.Clear(); // 清空临时聊天框列表
    }

    // 获取聊天历史信息协程
    private IEnumerator GetHistoryChatInfo()
    {
        yield return new WaitForEndOfFrame(); // 等待一帧

        for (int i = 0; i < m_ChatHistory.Count; i++) // 遍历聊天历史
        {
            if (i % 2 == 0) // 如果是偶数索引（发送的消息）
            {
                ChatPrefab _sendChat = Instantiate(m_PostChatPrefab, m_rootTrans.transform); // 实例化发送消息聊天预制体
                _sendChat.SetText(m_ChatHistory[i]); // 设置消息文本
                m_TempChatBox.Add(_sendChat.gameObject); // 将实例化的聊天框添加到临时聊天框列表
                continue;
            }

            ChatPrefab _reChat = Instantiate(m_RobotChatPrefab, m_rootTrans.transform); // 实例化机器人消息聊天预制体
            _reChat.SetText(m_ChatHistory[i]); // 设置消息文本
            m_TempChatBox.Add(_reChat.gameObject); // 将实例化的聊天框添加到临时聊天框列表
        }

        LayoutRebuilder.ForceRebuildLayoutImmediate(m_rootTrans); // 强制立即重建布局
        StartCoroutine(TurnToLastLine()); // 开始协程滚动到最后一行
    }

    // 滚动到最后一行协程
    private IEnumerator TurnToLastLine()
    {
        yield return new WaitForEndOfFrame(); // 等待一帧
        m_ScroTectObject.verticalNormalizedPosition = 0; // 设置滚动视图的垂直归一化位置为0
    }
    #endregion

    // 设置动画参数
    private void SetAnimator(string _para, int _value)
    {
        if (m_Animator == null) // 如果动画控制器为空
            return; // 直接返回

        m_Animator.SetInteger(_para, _value); // 设置动画控制器的整数参数
    }
}