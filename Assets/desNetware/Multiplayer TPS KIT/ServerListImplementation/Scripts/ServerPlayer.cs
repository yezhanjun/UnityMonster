using DNServerList;
using Mirror;
using Mirror.SimpleWeb;
using MTPSKIT;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace MTPSKIT
{
    public class ServerPlayer : MonoBehaviour
    {

        [SerializeField] GameSettingsSO _gameSettings;

#if !UNITY_WEBGL
        ExampleLobbyProperties _thisLobbyProperties;
        int currentConnectedPlayers = 0;

#endif

        public void StartServer(ushort port, string formInJson)
        {

#if !UNITY_WEBGL
            DNNetworkManager.Instance.OnNewPlayerConnected += OnPlayerCountChanged;
            DNNetworkManager.Instance.OnPlayerDisconnected += OnPlayerCountChanged;

            ExampleCreateLobbyForm form = JsonUtility.FromJson<ExampleCreateLobbyForm>(formInJson);

            DNNetworkManager networkManager = DNNetworkManager.Instance;

            networkManager.offlineScene = SceneUtility.GetScenePathByBuildIndex(0);

            networkManager.onlineScene = _gameSettings.Maps[System.Convert.ToInt32(form.mapID)].Scene;


            RoomSetup.Properties.P_Gamemode = _gameSettings.Maps[form.mapID].AvailableGamemodes[form.gamemodeID];
            RoomSetup.Properties.P_FillEmptySlotsWithBots = form.spawnBots > 0;
            RoomSetup.Properties.P_GameDuration = Mathf.FloorToInt(_gameSettings.GameDurations[form.gameDuration] * 60);

            int maxPlayers = 2;

            if (form.maxPlayers < _gameSettings.Maps[form.mapID].MaxPlayersPresets.Length)
                maxPlayers = _gameSettings.Maps[form.mapID].MaxPlayersPresets[form.maxPlayers];
            else
            {
                Debug.LogWarning($"Player number count index out of range, index: {form.maxPlayers}, size:{_gameSettings.Maps[form.mapID].MaxPlayersPresets.Length} ");
            }

            RoomSetup.Properties.P_MaxPlayers = maxPlayers;
            networkManager.maxConnections = maxPlayers;

            RoomSetup.Properties.P_RespawnCooldown = 6f;


            networkManager.SetTransportPort(port);
            networkManager.StartServer();

            ExampleLobbyProperties lobbyProperties = new ExampleLobbyProperties
            {
                ServerName = string.IsNullOrEmpty(form.serverName) ? $"Lobby{Random.Range(0, 1000)}" : form.serverName,
                MapID = form.mapID,
                GamemodeID = form.gamemodeID,
                MaxPlayers = form.maxPlayers,
                CurrentPlayers = currentConnectedPlayers,
            };

            _thisLobbyProperties = lobbyProperties;
            ServerCommunicator.Singleton.OnGameReady(JsonUtility.ToJson(lobbyProperties));
#endif
        }




#if !UNITY_WEBGL
        void OnPlayerCountChanged(NetworkConnectionToClient conn)
        {
            _thisLobbyProperties.CurrentPlayers = NetworkServer.connections.Count;

            ServerCommunicator.Singleton.OnPlayerCountChanged(NetworkServer.connections.Count, JsonUtility.ToJson(_thisLobbyProperties));
        }
#endif
    }
}