using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace DNServerList
{
    public class WebRequestManager : MonoBehaviour
    {
        public delegate void RequestDataHanlder(string downloadHandler = "", int responseCode = 0);

        //public static WebRequestManager Instance;

        //List<Coroutine> _webRequests = new List<Coroutine>();

        [SerializeField] public string _domain = "localhost:5000";

        private void Awake()
        {
            //Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        private void OnDestroy()
        {
            StopAllCoroutines();
           // Instance = null;
           /* for (int i = 0; i < _webRequests.Count; i++)
            {
                Coroutine coroutine = _webRequests[i];
                if (coroutine != null)
                {
                    StopCoroutine(coroutine);
                    _webRequests[i] = null;
                }
            }*/
        }

        public void SetDomain(string domain)
        {
            _domain = domain;
        }

        public void Post(string endPoint, object data, RequestDataHanlder receivedMessageMethod = null, RequestDataHanlder errorMethod = null, bool showLoadingScreen = false) //timeout time and method for timeout
        {
            byte[] jsonToSend = Encoding.UTF8.GetBytes(JsonUtility.ToJson(data));
            StartCoroutine(C_post($"{_domain}{endPoint}", jsonToSend, receivedMessageMethod, errorMethod, showLoadingScreen));
        }
        public void PostJson(string endPoint, string data, RequestDataHanlder receivedMessageMethod = null, RequestDataHanlder errorMethod = null, bool showLoadingScreen = false) //timeout time and method for timeout
        {
            byte[] jsonToSend = Encoding.UTF8.GetBytes(data);
            StartCoroutine(C_post($"{_domain}{endPoint}", jsonToSend, receivedMessageMethod, errorMethod, showLoadingScreen));
        }
        public void Get(string endPoint, RequestDataHanlder receivedMessageMethod = null, RequestDataHanlder errorMethod = null, bool showLoadingScreen = false)
        {
            StartCoroutine(C_get($"{_domain}{endPoint}", receivedMessageMethod, errorMethod, showLoadingScreen));
        }

        IEnumerator C_post(string endPoint, byte[] jsonToSend, RequestDataHanlder receivedMessageMethod, RequestDataHanlder errorMethod, bool showLoadingScreen = false)
        {
            UnityWebRequest www = new UnityWebRequest(endPoint, "POST");

            www.uploadHandler = new UploadHandlerRaw(jsonToSend);
            www.downloadHandler = new DownloadHandlerBuffer();
            www.SetRequestHeader("Content-Type", "application/json");

            yield return www.SendWebRequest();

            if (errorMethod != null && (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError))
            {
                errorMethod();
            }
            else if (receivedMessageMethod != null && www.result == UnityWebRequest.Result.Success)
            {
                receivedMessageMethod(www.downloadHandler.text, (int)www.responseCode);
            }

            www.uploadHandler.Dispose();
            www.downloadHandler.Dispose();          
        }

        IEnumerator C_get(string endPoint, RequestDataHanlder receivedMessageMethod, RequestDataHanlder errorMethod, bool showLoadingScreen = false)
        {

            using (UnityWebRequest www = UnityWebRequest.Get(endPoint))
            {
                yield return www.SendWebRequest();

                if (errorMethod != null && (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError))
                {
                    errorMethod();
                }
                else if (receivedMessageMethod != null)
                {
                    receivedMessageMethod(www.downloadHandler.text, (int)www.responseCode);
                }
            }
        }

        public static T Deserialize<T>(string json)
        {
            T obj = Activator.CreateInstance<T>();
            MemoryStream ms = new MemoryStream(Encoding.Unicode.GetBytes(json));
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(obj.GetType());
            obj = (T)serializer.ReadObject(ms);
            ms.Close();
            return obj;
        }
    }
}