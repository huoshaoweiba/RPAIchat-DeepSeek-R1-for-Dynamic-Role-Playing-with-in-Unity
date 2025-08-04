using System; // 使用System命名空间
using System.Collections; // 使用System.Collections命名空间
using System.Collections.Generic; // 使用System.Collections.Generic命名空间
using UnityEngine; // 使用Unity引擎命名空间

// 定义一个名为VoiceInputs的类，继承自MonoBehaviour，表示这是一个Unity组件
public class VoiceInputs : MonoBehaviour
{
    // 定义一个公共字段，表示录音时长（单位：秒）
    public int m_RecordingLength = 5;

    // 定义一个公共字段，用于存储录音结果
    public AudioClip recording;

    // 定义一个序列化字段，表示SignalManager组件（用于WebGL平台）
    [SerializeField] private SignalManager signalManager;

    // 开始录音的方法
    public void StartRecordAudio()
    {
        // 使用预处理器判断是否是WebGL平台且不是Unity编辑器
#if UNITY_WEBGL && !UNITY_EDITOR
        // 如果是WebGL平台，则调用SignalManager的StartRecordBinding方法开始录音
        signalManager.onAudioClipDone = null; // 清空回调事件
        signalManager.StartRecordBinding();
#else
        // 如果不是WebGL平台，则使用Unity的Microphone类开始录音
        // 参数说明：
        // - null：表示使用默认麦克风设备
        // - false：表示录音完成后不循环播放
        // - m_RecordingLength：录音时长
        // - 16000：录音采样率
        recording = Microphone.Start(null, false, m_RecordingLength, 16000);
#endif
    }

    // 停止录音的方法
    public void StopRecordAudio(Action<AudioClip> _callback)
    {
        // 使用预处理器判断是否是WebGL平台且不是Unity编辑器
#if UNITY_WEBGL && !UNITY_EDITOR
        // 如果是WebGL平台，则调用SignalManager的StopRecordBinding方法停止录音
        // 并将回调事件设置为传入的_callback
        signalManager.onAudioClipDone += _callback;
        signalManager.StopRecordBinding();
#else
        // 如果不是WebGL平台，则使用Unity的Microphone类停止录音
        Microphone.End(null); // 停止录音
        _callback(recording); // 调用回调方法，传入录音结果
#endif
    }
}