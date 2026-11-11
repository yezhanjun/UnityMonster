using DNServerList;
using DNServerList.UI;
using Mirror;
using Mirror.SimpleWeb;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MTPSKIT;

namespace MTPSKIT
{

    public class LobbyTile : UIServerListLobbyRepresenter
    {
        [SerializeField] Text _roomName;
        [SerializeField] Text _gamemodeName;
        [SerializeField] Text _mapName;
        [SerializeField] Text _playerCount;

        [SerializeField] Button _connectButton;

        [SerializeField] GameSettingsSO _gameSettings;

        [SerializeField] UILoadingScreen _loadingScreen;

        public override void Draw(string lobbyData)
        {
            print(lobbyData);
            ExampleLobbyProperties data = JsonUtility.FromJson<ExampleLobbyProperties>(lobbyData);

            MapRepresenter map = _gameSettings.Maps[data.MapID];

            _mapName.text = map.Name;

            _roomName.text = data.ServerName;

            _gamemodeName.text = (map.AvailableGamemodes != null && map.AvailableGamemodes.Length > 0) ?
                MTPSKIT.ChineseUI.GamemodeName(map.AvailableGamemodes[data.GamemodeID].ToString()) :
                string.Empty;

            _playerCount.text = map.MaxPlayersPresets != null && map.MaxPlayersPresets.Length > 0 ?
                $"{data.CurrentPlayers}/{map.MaxPlayersPresets[data.MaxPlayers]}" :
                data.CurrentPlayers.ToString();

            _connectButton.onClick.AddListener(ButtonConnect);
        }

        void ButtonConnect()
        {
            _loadingScreen.ShowLoadingScreen("正在连接对局…", 5f);
            StartCoroutine(Connect());
            IEnumerator Connect()
            {
                yield return new WaitForEndOfFrame();
                DNNetworkManager networkManager = DNNetworkManager.Instance;

                networkManager.networkAddress = ServerAddress;
                networkManager.SetAddressAndPort(ServerAddress, Port);
                networkManager.StartClient();
            }
        }
    }
}