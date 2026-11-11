using DNServerList;
using Mirror;
using Mirror.SimpleWeb;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{

    public class UIGetGame : MonoBehaviour
    {
        [SerializeField] UILoadingScreen _loadingScreen;

        [SerializeField] InputField _ifAccess;
        [SerializeField] Button _btnConnect;

        [SerializeField] Text _feedback;

        private void Awake()
        {
            _feedback.text = string.Empty;
        }

        public void SendRequest()
        {
            string code = _ifAccess.text;

            if (code.Length != 5)
            {
                _feedback.text = "请输入有效的房间码";
                return;
            }

            ServerListClient.Singleton.GetServerByCode(code);
        }

        public void OnLobbyFound(string address, ushort port)
        {
            _loadingScreen.ShowLoadingScreen("已找到房间！\n正在连接…", 5f);
            StartCoroutine(Connect());
            IEnumerator Connect()
            {
                yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(0.2f);
                DNNetworkManager networkManager = DNNetworkManager.Instance;

                networkManager.networkAddress = address;
                networkManager.SetTransportPort(port);
                networkManager.StartClient();
            }
        }
        public void OnLobbyNotFound()
        {
            _loadingScreen.ShowMessageScreen("未找到房间", 2f);
        }

    }
}