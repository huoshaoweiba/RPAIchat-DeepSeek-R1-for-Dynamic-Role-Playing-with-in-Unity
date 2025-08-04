using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using static GPTSoVITSTextToSpeech;

public class GPTSoVITSFASTAPI : TTS
{
    #region ��������
    [Header("�ο���Ƶ·������GPT-SoVITS��Ŀ�µ����·��")]
    [SerializeField] private string m_ReferWavPath = string.Empty;//�ο���Ƶ·��
    [Header("�ο���Ƶ����������")]
    [SerializeField] private string m_ReferenceText = "";//�ο���Ƶ�ı�
    [Header("�ο���Ƶ������")]
    [SerializeField] private Language m_ReferenceTextLan = Language.英语;//�ο���Ƶ������
    [Header("�ϳ���Ƶ������")]
    [SerializeField] private Language m_TargetTextLan = Language.英语;//�ϳ���Ƶ������

    #endregion

    /// <summary>
    /// ����ϳɣ����غϳ��ı�
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    public override void Speak(string _msg, Action<AudioClip, string> _callback)
    {
        StartCoroutine(GetVoice(_msg, _callback));
    }

    /// <summary>
    /// �ϳ���Ƶ
    /// </summary>
    /// <param name="_msg"></param>
    /// <param name="_callback"></param>
    /// <returns></returns>
    private IEnumerator GetVoice(string _msg, Action<AudioClip, string> _callback)
    {
        stopwatch.Restart();
        //���ͱ���
        RequestData _requestData = new RequestData
        {
            refer_wav_path = m_ReferWavPath,
            prompt_text = m_ReferenceText,
            prompt_language = m_ReferenceTextLan.ToString(),
            text = _msg,
            text_language = m_TargetTextLan.ToString()
        };

        string _postJson = JsonUtility.ToJson(_requestData);//����

        using (UnityWebRequest request = new UnityWebRequest(m_PostURL, "POST"))
        {
            byte[] data = System.Text.Encoding.UTF8.GetBytes(_postJson);
            request.uploadHandler = (UploadHandler)new UploadHandlerRaw(data);
            request.downloadHandler = new DownloadHandlerAudioClip(m_PostURL, AudioType.WAV);

            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.responseCode == 200)
            {
                AudioClip audioClip = ((DownloadHandlerAudioClip)request.downloadHandler).audioClip;
                _callback(audioClip, _msg);
            }
            else
            {
                Debug.LogError("请求错误: " + request.error);
            }
        }

    }


    #region ���ݶ���

    [Serializable]
    public class RequestData
    {
        public string refer_wav_path = string.Empty;//�ο���Ƶ·��
        public string prompt_text = string.Empty;//�ο���Ƶ�ı�
        public string prompt_language = string.Empty;//�ο���Ƶ����
        public string text = string.Empty;//�ϳ��ı�
        public string text_language = string.Empty;//�ϳ���������
    }



    #endregion



}
