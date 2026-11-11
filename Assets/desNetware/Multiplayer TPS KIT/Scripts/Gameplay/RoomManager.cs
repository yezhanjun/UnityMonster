using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using System;
using MTPSKIT.Gameplay;
using UnityEngine.Networking;
using System.Linq;
using MTPSKIT.UI;
using Mirror.SimpleWeb;

namespace MTPSKIT.Gameplay.Gamemodes {

    /// <summary>
    /// this class is responsible for initializing proper gamemode
    /// </summary>
    public class RoomManager : NetworkBehaviour
    {
        public static RoomManager _instance;

        float tickDuration;

        //events for all clients
        public delegate void PlayerKilledByPlayer(uint victimID, uint killerID);
        public PlayerKilledByPlayer Client_PlayerKilledByPlayer;

        public static Action RoomTick;
        float tickTimer = 0.0f;

        private void Awake()
        {
            _instance = this;
        }
        private void Start()
        {
            
            if (GameplayCamera._instance == null) 
            {
                Debug.Log("MTPSKIT: Gameplay camera not found in the scene, creating one");
                Instantiate(ClientInterfaceManager.Instance.GameplayCamera, transform.position, transform.rotation);
            }

            tickDuration = 1f / DNNetworkManager.Instance.sendRate;

            if (isServer)
            {
                //If respawn cooldown is 0 set it to default value
                if (RoomSetup.Properties.P_RespawnCooldown <= 0f)
                    RoomSetup.Properties.P_RespawnCooldown = 6f;

                Gamemode _requestedGamemode = null;

                Gamemode[] _avaibleGamemodes = GetComponents<Gamemode>();
                for (int i = 0; i < _avaibleGamemodes.Length; i++)
                {
                    if (_avaibleGamemodes[i].Indicator == RoomSetup.Properties.P_Gamemode) _requestedGamemode = _avaibleGamemodes[i];
                }

                if (_requestedGamemode != null)
                {
                    _requestedGamemode.SetupGamemode(RoomSetup.Properties);
                }
                else
                {
                    Debug.Log("MTPSKIT: This map does not support this gamemode: " + RoomSetup.Properties.P_Gamemode + ", Initializing" + _avaibleGamemodes[0].Indicator + " instead");

                    //if requested gamemode is not supported on this map, than initialize first supported one
                    _avaibleGamemodes[0].SetupGamemode(RoomSetup.Properties);
                }
            }
        }

        private void Update()
        {
            if (tickTimer <= Time.time)
            {
                Physics.SyncTransforms();
                RoomTick?.Invoke();
                tickTimer = Time.time + tickDuration;
            }
        }
        private void OnDestroy()
        {
            GameManager.ClearGameData();
        }
    }
}
