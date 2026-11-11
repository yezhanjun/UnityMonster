using MTPSKIT;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT
{

    [CreateAssetMenu(fileName = "GameSettings", menuName = "DNTools/GameSettings")]
    public class GameSettingsSO : ScriptableObject
    {
        public MapRepresenter[] Maps;



        public int[] GameDurations; //(in minutes)

        [System.Serializable]
        public class Map
        {
            public string MapName = "example map";
            public int MapBuildSettingsID;
            public Gamemodes[] AvailableGamemodes;
            public int[] MaxPlayers;
        }

        public enum Gamemodes
        {
            None,
            DeathMatch,
            TeamDeathMatch,
        }
    }

}