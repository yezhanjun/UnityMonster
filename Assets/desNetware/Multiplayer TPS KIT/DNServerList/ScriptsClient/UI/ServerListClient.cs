using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Networking;

namespace DNServerList
{
    [RequireComponent(typeof(WebRequestManager))]
    public class ServerListClient : MonoBehaviour
    {
        public static ServerListClient Singleton { get; set; }

        [Header("Server list")]
        public UnityEvent <LobbyData[], int, int> OnServerListReceived;
        public UnityEvent<string, ushort> OnCreatedLobbySuccesfully;
        public UnityEvent OnLobbyCouldNotBeCreated;
        public UnityEvent OnConnectionError;

        [Header("Lobby codes")]
        public UnityEvent<string, ushort> OnLobbyFoundByCode;
        public UnityEvent OnLobbyNotFound;

        WebRequestManager _webRequestManager;

        private void OnEnable()
        {
            if (Singleton)
            {
                
                Destroy(Singleton.gameObject);
            }

            Singleton = this;

            _webRequestManager = gameObject.GetComponent<WebRequestManager>();
        }
        private void OnDisable()
        {
            Singleton = null;
        }

        public void GetServerList()
        {
            _webRequestManager.Get("/getserverlist", OnSuccess, OnError);

            void OnSuccess(string data, int code)
            {
                Lobbies ReceivedLobbiesJson = JsonUtility.FromJson<Lobbies>(data);
                LobbyData[] ReceivedLobbies = new LobbyData[ReceivedLobbiesJson.lobbies.Length];
                
                for (int i = 0; i < ReceivedLobbiesJson.lobbies.Length; i++)
                {
                    ReceivedLobbies[i] = WebRequestManager.Deserialize<LobbyData>(ReceivedLobbiesJson.lobbies[i]);
                }

                OnServerListReceived?.Invoke(ReceivedLobbies, 
                    System.Convert.ToInt32(ReceivedLobbiesJson.thisLobbiesStartingIndex),
                    System.Convert.ToInt32(ReceivedLobbiesJson.totalLobbiesCount));
            }

            void OnError(string data, int code)
            {
                OnConnectionError?.Invoke();
            }
        }

        public void GetServerByCode(string code)
        {
            string url = $"/getprivategame/{code}";
            _webRequestManager.Get(url, OnSuccess, (string data, int code)=>OnConnectionError?.Invoke());

            void OnSuccess(string data, int code)
            {
                if (code != 202) return;

                PlayerConnectToRoomRequest connectInfo = JsonUtility.FromJson<PlayerConnectToRoomRequest>(data);

                if (!string.IsNullOrEmpty(connectInfo.address))
                    OnLobbyFoundByCode?.Invoke(connectInfo.address, System.Convert.ToUInt16(connectInfo.port));
                else
                    OnLobbyNotFound?.Invoke();
            }
            
        }

        public void SendCreateLobbyRequest<T>(T gameProperties, bool isPrivate)
        {
            CreateGameContract form = new CreateGameContract();
            form.metadata = JsonUtility.ToJson(gameProperties);
            form.isPrivate = isPrivate;

            string finalForm = JsonUtility.ToJson(form);

            _webRequestManager.PostJson("/createpublicgame", finalForm, OnSuccess, OnError);

            void OnSuccess(string data, int code)
            {

                if (code == 202)
                {
                    PlayerConnectToRoomRequest connectInfo = JsonUtility.FromJson<PlayerConnectToRoomRequest>(data);
                    OnCreatedLobbySuccesfully?.Invoke(connectInfo.address, System.Convert.ToUInt16(connectInfo.port));
                }
                else
                    OnLobbyCouldNotBeCreated?.Invoke();
            }

            void OnError(string data, int code)
            {
                OnConnectionError?.Invoke();
            }
        }
    }

    [System.Serializable]
    public class Lobbies
    {
        public string[] lobbies;
        public int thisLobbiesStartingIndex;
        public int totalLobbiesCount;
    }

    [System.Serializable]
    public class LobbyData
    {
        public string metadata;
        public string address;
        public string port;
    }

    struct CreateGameContract
    {
        public string metadata;
        public bool isPrivate;
    }

    [System.Serializable]
    public class PlayerConnectToRoomRequest
    {
        public string address;
        public ushort port;
    }
}