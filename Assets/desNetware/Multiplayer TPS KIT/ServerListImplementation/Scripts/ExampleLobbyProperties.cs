using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT
{

    [System.Serializable]
    public class ExampleLobbyProperties
    {
        public string ServerName;
        public int GamemodeID;
        public int MapID;
        public int CurrentPlayers;
        public int MaxPlayers;
    }
}