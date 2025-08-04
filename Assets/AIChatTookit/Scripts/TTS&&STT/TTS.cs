using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

public class TTS : MonoBehaviour
{
    /// <summary>
    /// API endpoint for text-to-speech synthesis
    /// 用于TTS的API地址
    /// </summary>
    [SerializeField] protected string m_PostURL = string.Empty;//用于TTS的api地址

    /// <summary>
    /// Timer for measuring method execution time
    /// </summary>
    [SerializeField] protected Stopwatch stopwatch = new Stopwatch();//用于测量方法执行时间的计时器。

    /// <summary>
    /// Synthesize speech audio from text
    /// </summary>
    /// <param name="_msg">Text input to synthesize</param>
    /// <param name="_callback">Callback receiving the generated AudioClip</param>
    /// 文字合成，并生成音频
    public virtual void Speak(string _msg, Action<AudioClip> _callback) { }

    /// <summary>
    /// Synthesize speech audio and return additional synthesis data
    /// </summary>
    /// <param name="_msg">Text input to synthesize</param>
    /// <param name="_callback">Callback receiving both the AudioClip and related synthesis data</param>
// 合成语音生成音频，同时返回合成的文本
    public virtual void Speak(string _msg, Action<AudioClip, string> _callback) { }
}