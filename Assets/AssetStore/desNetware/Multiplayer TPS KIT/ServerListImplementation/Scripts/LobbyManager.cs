using DNServerList;
using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT
{
    public class LobbyManager : NetworkBehaviour
    {
        [SerializeField] Text _accesCodeUIText;

        private void Awake()
        {
            _accesCodeUIText.text = string.Empty;
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            NetworkServer.Spawn(gameObject);
        }

        private void OnPlayerConnected(NetworkConnection conn)
        {
            if (!string.IsNullOrEmpty(ServerCommunicator.AccesCode))
                TargetRpcReceiveAccessCode(conn, ServerCommunicator.AccesCode);
        }

        [TargetRpc]
        void TargetRpcReceiveAccessCode(NetworkConnection conn, string accessCode)
        {
            _accesCodeUIText.text = accessCode;
        }
    }
}