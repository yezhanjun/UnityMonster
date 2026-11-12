using System.Collections.Generic;
using UnityEngine;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;

namespace MTPSKIT
{
    public static class GameManager
    {
        public static PlayerInstance myPlayerInstance { set; get; }
        public static Dictionary<uint, PlayerInstance> Players { get; set; } = new Dictionary<uint, PlayerInstance>();

        //does not include bots
        public static Dictionary<int, PlayerInstance> PlayersByConnectionID { get; set; } = new Dictionary<int, PlayerInstance>();
        public static Dictionary<uint, Health> HealthInstances { get; set; } = new Dictionary<uint, Health>();
        

        public static Gamemode Gamemode { get; private set; }
        public delegate void OnGamemodeSet(Gamemode gamemode);
        public static OnGamemodeSet GameEvent_OnGamemodeSet { set; get; }

        public static void SetGamemode(Gamemode gamemodeToSet)
        {
            Gamemode = gamemodeToSet;
            GameEvent_OnGamemodeSet?.Invoke(Gamemode);
        }

        #region player instance
        public static void AddPlayerInstance(PlayerInstance pi)
        {
            if (!Players.ContainsKey(pi.netId))
            {
                Players.Add(pi.netId, pi);
            }
        }
        public static void RemovePlayerInstance(PlayerInstance pi, uint netID)
        {

            if (Players.ContainsKey(netID))
            {
                Players.Remove(netID);
            }

            if (Gamemode) Gamemode.Server_OnPlayerRemoved(pi);
        }
        #endregion

        #region health instances
        public static void AddHealthInstance(Health _h)
        {
            if (!HealthInstances.ContainsKey(_h.netId))
            {
                HealthInstances.Add(_h.netId, _h);
            }
        }
        public static void RemoveHealthInstance(uint netID)
        {
            if (HealthInstances.ContainsKey(netID))
            {
                HealthInstances.Remove(netID);
            }
        }
        public static Health GetHealthInstance(uint id)
        {
            if (HealthInstances.ContainsKey(id))
                return HealthInstances[id];
            else return null;
        }
        #endregion
        public static void ClearGameData()
        {
            Players.Clear();
            HealthInstances.Clear();
        }


        public static void SetLayerRecursively(GameObject go, int layerNumber)
        {
            go.layer = layerNumber;
            foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
            {
                trans.gameObject.layer = layerNumber;
            }
        }

        #region constants
        /// <summary>
        /// layer for guns hitscan
        /// </summary>
        public const int fireLayer = (1 << 8 | 1 << 0);
        public const int characterLayer = (1 << 6 | 1 << 0);
        public const int environmentLayer = (1 << 0);
        public const int rigidbodyLayer = (1 << 9 | 1 << 7);
        public static int interactLayerMask = (1 << 0 | 1 << (int)GameLayers.item);

        /// <summary>
        /// Amount of time that item can exist after being dropped
        /// </summary>
        public const float TimeOfLivingLonelyItem = 10f;

        #endregion

    }

    /// <summary>
    /// game layers with respective indexes, first 6 layers are built in and cannot be changed, so we start from index 6
    /// </summary>
    public enum GameLayers
    {
        character = 6,
        item = 7,
        hitbox = 8,
        ragdoll = 9,
        throwables = 10,
        trigger = 11,
        launchedThrowables = 12,
    }
}