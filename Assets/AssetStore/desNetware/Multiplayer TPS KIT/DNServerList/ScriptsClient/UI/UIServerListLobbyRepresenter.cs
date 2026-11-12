using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DNServerList.UI
{
    public class UIServerListLobbyRepresenter : MonoBehaviour
    {
        public string ServerAddress;
        public ushort Port;

        public virtual void Draw(string data) { }

        public void Setup(LobbyData lobbyData) 
        {
            ServerAddress = lobbyData.address;
            Port = System.Convert.ToUInt16(lobbyData.port);

            Draw(lobbyData.metadata);
        }
    }
}
