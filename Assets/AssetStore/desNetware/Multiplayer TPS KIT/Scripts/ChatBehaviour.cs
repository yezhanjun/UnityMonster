using UnityEngine;
using Mirror;
using System;
using MTPSKIT.Gameplay;
using MTPSKIT.UI;
using MTPSKIT.Gameplay.Gamemodes;

namespace MTPSKIT
{
    public class ChatBehaviour : NetworkBehaviour
    {

        private PlayerInstance _playerInstance;
        public static ChatBehaviour _instance { get; private set; }

        public bool ChatWriting { private set; get; } = false;

        private void Awake()
        {
            _playerInstance = GetComponent<PlayerInstance>();
        }
        [Command(channel = 0)]
        public void CmdRelayClientMessage(string message)
        {
            RpcHandleChatClientMessage(GameTools.CheckMessageLength(message));
        }
        [ClientRpc]
        public void RpcHandleChatClientMessage(string message)
        {
            //         OnMessage?.Invoke();

            if (!ClientInterfaceManager.Instance) return;
            Color colorForNickaname = _playerInstance.Team == -1 ? Color.white : ClientInterfaceManager.Instance.UIColorSet.TeamColors[_playerInstance.Team];

            //make message from player nickname and his message
            string newMessage = $" {"<b>" + $"<color=#{ColorUtility.ToHtmlStringRGBA(colorForNickaname)}>" + _playerInstance.playerName + "</b>" + "</color>" + ": " + message}";

            //write it to UI
            ChatUI._instance.WriteMessageToChat(newMessage);
        }
    }
}