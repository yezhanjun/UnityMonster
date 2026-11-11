using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using Mirror.SimpleWeb;
using System.Text.RegularExpressions;
using UnityEngine.SceneManagement;
using MTPSKIT.Gameplay;
using System;

namespace MTPSKIT
{

    public class DNNetworkManager : NetworkManager
    {
        public const string MainMenuScene = "Assets/Baseline/Scenes/Startup.unity";
        public static DNNetworkManager Instance;

        //this event exist to send late players data about gamemode and equipment of other players.
        public delegate void NewPlayerJoinedTheGame(NetworkConnectionToClient conn);
        public NewPlayerJoinedTheGame OnNewPlayerConnected { get; set; }

        public delegate void PlayerDisconnected(NetworkConnectionToClient conn);
        public PlayerDisconnected OnPlayerDisconnected { get; set; }
        public Action<ushort> Callback_OnNetworkTransportPortSet { get; internal set; }
        bool _playerRequestPending;
        SceneOperation _clientSceneOperation;

        public override void OnStartClient()
        {
            _playerRequestPending = false;
            _clientSceneOperation = SceneOperation.Normal;
            base.OnStartClient();
        }

        public override void OnClientNotReady()
        {
            // A new server-controlled scene transition permits a new player request.
            _playerRequestPending = false;
            base.OnClientNotReady();
        }

        public override void OnClientChangeScene(string scene, SceneOperation operation, bool customHandling)
        {
            _clientSceneOperation = operation;
            base.OnClientChangeScene(scene, operation, customHandling);
        }

        public override void OnClientConnect()
        {
            bool configured = autoCreatePlayer;
            bool shouldRequest = configured && !clientLoadedScene && NetworkClient.localPlayer == null;
            autoCreatePlayer = shouldRequest && !_playerRequestPending;
            try
            {
                base.OnClientConnect();
                if (autoCreatePlayer && NetworkClient.ready) _playerRequestPending = true;
            }
            finally { autoCreatePlayer = configured; }
        }

        public override void OnClientSceneChanged()
        {
            bool configured = autoCreatePlayer;
            bool shouldRequest = configured && _clientSceneOperation == SceneOperation.Normal &&
                NetworkClient.connection != null && NetworkClient.connection.isAuthenticated && NetworkClient.localPlayer == null;
            autoCreatePlayer = shouldRequest && !_playerRequestPending;
            try
            {
                base.OnClientSceneChanged();
                if (autoCreatePlayer && NetworkClient.ready) _playerRequestPending = true;
            }
            finally { autoCreatePlayer = configured; }
        }

        public override void Awake()
        {
            if (Instance)
            {
                Debug.LogError("Fatal error, two instances of Custom Network Manager");
                if (Instance == this) return;
                Destroy(gameObject);
                return;
            }

            // A legacy Hub launched from the Editor must still return to the current menu.
            offlineScene = SceneUtility.GetBuildIndexByScenePath(MainMenuScene) >= 0
                ? MainMenuScene
                : SceneManager.GetActiveScene().path;
            base.Awake();
            foreach (string resource in FpsGunBinding.ResourceNames)
            {
                var prefab = Resources.Load<GameObject>(resource);
                if (prefab && !spawnPrefabs.Contains(prefab)) spawnPrefabs.Add(prefab);
            }
            var ward = Resources.Load<GameObject>(Gameplay.RainQueenWardHealth.ResourcePath);
            if (ward && !spawnPrefabs.Contains(ward)) spawnPrefabs.Add(ward);

            Instance = this;
            autoCreatePlayer = true;

        }

        public void SetTransportPort(ushort port) 
        {
            Callback_OnNetworkTransportPortSet?.Invoke(port);
        }


        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            if (conn.identity != null) return;
            base.OnServerAddPlayer(conn);
            OnNewPlayerConnected?.Invoke(conn);
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            OnPlayerDisconnected?.Invoke(conn);
            base.OnServerDisconnect(conn);
        }

        void OnReceivedPlayerInputMessage(NetworkConnectionToClient conn, ClientSendInputMessage msg)
        {
            try
            {
                if (GameManager.PlayersByConnectionID.TryGetValue(conn.connectionId, out PlayerInstance pi))
                {
                    if (pi.MyCharacter)
                        pi.MyCharacter.ReadAndApplyInputFromClient(msg);
                }
            }
            catch (Exception ex){ Debug.Log($"{ex.Message}"); Debug.Log($"{ex}"); }
        }

        public void SetAddressAndPort(string address, ushort port)
        {
            if (string.IsNullOrEmpty(address)) return;

            SetTransportPort((ushort)System.Convert.ToInt32(port));
            networkAddress = address;
        }

        public override void OnStartServer()
        {
            NetworkServer.RegisterHandler<ClientSendInputMessage>(OnReceivedPlayerInputMessage);
        }
        public override void OnStopServer()
        {
            NetworkServer.UnregisterHandler<ClientSendInputMessage>();
        }
    }

    public struct ClientSendInputMessage : NetworkMessage
    {
        public byte Movement;
        public sbyte LookX;
        public short LookY;
        public byte ActionCodes; //sprint, fire1, fire2, and 5 free inputs to utilize   
    }
}
