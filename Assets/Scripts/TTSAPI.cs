//using UnityEngine;
//using UnityEngine.Networking;
//using System.Collections;
//using System.Text;
//using Newtonsoft.Json;
//using System.IO;
//using System.Diagnostics;
//using Debug = UnityEngine.Debug;
//using static Unity.VisualScripting.Member;
//using System.Text.RegularExpressions;
//using UnityEngine.Windows;

//public class TTSAPI : MonoBehaviour
//{
//    [Header("��Ƶ����")]
//    public AudioSource audioSource;

//    private Process pythonProcess; // �洢���������ʵ��

///*    void Start()
//    {
//        if (useTTSOnBeginning)
//        {
//            ExecuteBatFile();
//        }
//    }

//    void OnApplicationQuit()
//    {
//        if (useTTSOnBeginning)
//        {
//            TerminateBatProcess();
//        }
//    }

//    private void ExecuteBatFile()
//    {
        
//        try
//        {
//            string pythonExePath = GlobalConfig.instance.ttsMainFilePath + "\\runtime\\python.exe";
//            string pythonScriptPath = GlobalConfig.instance.ttsMainFilePath + "\\api.py"; // Ҫ���е�Python�ű�����·��
//            string scriptArguments = "-s \"SoVITS_weights_v2/kaituozhe_e10_s1190.pth\" -g \"GPT_weights_v2/kaituozhe-e10.ckpt\" -dr  \"examples/kaituozhe.wav\" -dt \"��Ȼ����˵�Ų����ˣ�����ķ�Ӧ���Ǻ���ʵ�ġ����Ƕ�ȥ������ķ�ɣ�\" -dl \"zh\" -a \"0.0.0.0\" -sm \"n\""; // �ű���������ѡ��
//            // ����ļ��Ƿ����
//            if (!System.IO.File.Exists(pythonExePath) || !System.IO.File.Exists(pythonScriptPath))
//            {
//                Debug.LogError("Python��������ű��ļ�������, pythonExePath: " + pythonExePath);
//                return;
//            }

//            // ���ý��̲���
//            ProcessStartInfo startInfo = new ProcessStartInfo
//            {
//                FileName = pythonExePath, // ָ��Python������·��
//                Arguments = $"{pythonScriptPath} {" " + scriptArguments}", // ƴ�ӽű�·���Ͳ���
//                UseShellExecute = false, // ��ʹ��ϵͳ��ǣ������ض��������
//                RedirectStandardOutput = true, // �ض�������Ա���־�鿴
//                RedirectStandardError = true,
//                CreateNoWindow = true, // ����ʱ��ʾ�����д��ڣ�����ʱ����Ϊtrue��
//                WorkingDirectory = Path.GetDirectoryName(pythonScriptPath) // ���ýű�����Ŀ¼Ϊ����Ŀ¼
//            };

//            // �������̲���ȡ���
//            pythonProcess = new Process { StartInfo = startInfo };
//            pythonProcess.Start();

//            // �����������ѡ�����ڵ��ԣ�
//            pythonProcess.OutputDataReceived += (sender, e) => Debug.Log($"Python���: {e.Data}");
//            pythonProcess.BeginOutputReadLine();

//            Debug.Log("Python���������ɹ�");
//        }
//        catch (System.Exception e)
//        {
//            Debug.LogError($"����ʧ��: {e.Message}");
//        }
//    }

//    private void TerminateBatProcess()
//    {
//        if (pythonProcess != null && !pythonProcess.HasExited)
//        {
//            try
//            {
//                // ǿ����ֹ���̼��������ӽ��̣��ؼ���
//                pythonProcess.Kill(); // true�����ݹ���ֹ�ӽ���
//                pythonProcess.WaitForExit(2000); // �ȴ�2��ȷ����ֹ
//                Debug.Log("Python��������ֹ");
//            }
//            catch (System.Exception e)
//            {
//                Debug.LogError($"��ֹʧ��: {e.Message}");
//            }
//        }
//    }*/

//    public IEnumerator SendTTSRequest(string input)
//    {
//        if (!GlobalConfig.instance.textToSoundEnabled)
//        {
//            yield break;
//        }
//        //�򵥵Ĵ����ı����Ƴ�һЩ��������������˵��������
//        string txt = WashTTSText(input);

//        // ����JSON�����壨���ݷ���˲���Ҫ������ֶ�����
//        var requestBody = new
//        {
//            text = txt,          // ��Ӧ���������е� -dt���ı����ݣ�
//            text_language = "zh"
//        };

//        // ���������л�ΪJSON�ַ���
//        string json = JsonConvert.SerializeObject(requestBody);
//        byte[] bodyRaw = Encoding.UTF8.GetBytes(json);

//        using (UnityWebRequest request = new UnityWebRequest(GlobalConfig.instance.serviceUrl, "POST"))
//        {
//            // ��������ͷΪJSON��ʽ
//            request.SetRequestHeader("Content-Type", "application/json");
//            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
//            request.downloadHandler = new DownloadHandlerAudioClip(request.url, AudioType.OGGVORBIS);  // ����OGG��ʽ
//            request.timeout = 120;  // �ӳ���ʱʱ�䣨������Ƶ���ܽ�����

//            yield return request.SendWebRequest();

//            if (request.result == UnityWebRequest.Result.Success)
//            {
//                AudioClip clip = DownloadHandlerAudioClip.GetContent(request);
//                if (clip != null)
//                {
//                    audioSource.clip = clip;
//                    audioSource.Play();
//                    Debug.Log("�������ųɹ���ttsText: " + txt);
//                }

//                HistoryManager.AddDialogue(GlobalConfig.instance.roleName, txt, clip);
//            }
//            else
//            {
//                Debug.LogError($"�޷�������Ƶ����");  // �ؼ��Ŵ���Ϣ
//                HistoryManager.AddDialogue(GlobalConfig.instance.roleName, txt);
//                yield return null;
//            }
//            StartCoroutine(TriggerCallbackWhenDone()); //����idle״̬
//            request.Dispose();
//        }
//    }

//    private IEnumerator TriggerCallbackWhenDone()
//    {
//        yield return new WaitUntil(() => !audioSource.isPlaying);
//        EventCenter.EventTrigger("SetIdleState");
//    }

//    //��ttsģ���������ϴ��ϴȥһЩ�����ʺ�������
//    private string WashTTSText(string text)
//    {
//        if (string.IsNullOrEmpty(text)) return text;

//        // ����ƥ���������Ŷԣ�Բ���š������š������ţ������ڲ�����
//        // ģʽ˵����
//        // \( [^)]* \)  ƥ��Բ�����ڵ����ݣ���Ƕ�ף�
//        // \*.*?(\*|$) ƥ�������Ǻ��ڵ�����
//        string pattern = @"\*.*?\*|\(.*?\)|\��.*?\��";

//        // ȫ���滻ƥ�䵽�����ż��ڲ�����Ϊ���ַ���
//        return Regex.Replace(text, pattern, string.Empty);
//    }

//}
