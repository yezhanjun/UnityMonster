using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT;
namespace MTPSKIT
{
    /// <summary>
    /// user interface class for user to be able to specify game parameters like map, gamemode
    /// max players and game duration
    /// </summary>
    public class RoomCreator : MonoBehaviour
    {
        public TMPro.TMP_Dropdown MapselectionDropdown;
        public TMPro.TMP_Dropdown GamemodeSelectionDropdown;
        public TMPro.TMP_Dropdown GameDurationDropdown;
        public TMPro.TMP_Dropdown PlayerNumberDropdown;

        public Button StartGameButton;
        public Button ServerOnlyButton;
        public Toggle BotsToggle;

        DNNetworkManager _networkManager;

        //user input
        int _selectedMapID;
        int _selectedTimeDurationID;
        int _selectedPlayerNumberOptionID;
        Gamemodes _selectedGamemode;

        [Header("Options for player to choose from")]
        public MapRepresenter[] Maps;
        public int[] TimeOptionsInMinutes = { 2, 5, 10 };
        public int[] PlayerNumberOptions = { 2, 4, 6, 8, 12 };

        void Start()
        {
            _networkManager = DNNetworkManager.Instance;
            if (!MapselectionDropdown) return;

            List<string> mapOptions = new List<string>();

            for (int i = 0; i < Maps.Length; i++)
            {
                mapOptions.Add(Maps[i].Name);
            }

            MapselectionDropdown.ClearOptions();
            MapselectionDropdown.AddOptions(mapOptions);

            MapselectionDropdown.onValueChanged.AddListener(OnMapselected);
            GamemodeSelectionDropdown.onValueChanged.AddListener(OnGamemodeSelected);
            GameDurationDropdown.onValueChanged.AddListener(OnGameDurationSelected);
            PlayerNumberDropdown.onValueChanged.AddListener(OnPlayerNumberOption);

            //game duration options
            List<string> durationOptions = new List<string>();

            for (int i = 0; i < TimeOptionsInMinutes.Length; i++)
            {
                durationOptions.Add(TimeOptionsInMinutes[i].ToString() + " 分钟");
            }

            GameDurationDropdown.AddOptions(durationOptions);

            List<string> playerNumberOptions = new List<string>();
            //player number options
            for (int i = 0; i < PlayerNumberOptions.Length; i++)
            {
                playerNumberOptions.Add(PlayerNumberOptions[i].ToString() + " 人");
            }
            PlayerNumberDropdown.ClearOptions();
            PlayerNumberDropdown.AddOptions(playerNumberOptions);

            StartGameButton.onClick.AddListener(StartGame);
            ServerOnlyButton.onClick.AddListener(StartServer);

            OnMapselected(0);
        }
        void OnMapselected(int mapID)
        {
            _selectedMapID = mapID;
            OnGamemodeSelected(0);

            //fill gamemodes dropdown with options avaible for given map
            Gamemodes[] avaibleGamemodesForThisMap = Maps[mapID].AvailableGamemodes;

            List<string> gamemodeOptions = new List<string>();

            for (int i = 0; i < avaibleGamemodesForThisMap.Length; i++)
            {
                gamemodeOptions.Add(MTPSKIT.ChineseUI.GamemodeName(avaibleGamemodesForThisMap[i].ToString()));
            }

            GamemodeSelectionDropdown.ClearOptions();
            GamemodeSelectionDropdown.AddOptions(gamemodeOptions);
        }

        /// <summary>
        /// trigged by selecting gamemode in UI room creator, tells game which gamemode to setup
        /// </summary>
        /// <param name="gamemodeID"></param>
        void OnGamemodeSelected(int gamemodeID) //gamemode ID is relevant to gamemodes order in their enum
        {
            _selectedGamemode = Maps[_selectedMapID].AvailableGamemodes != null && Maps[_selectedMapID].AvailableGamemodes.Length > 0 ? Maps[_selectedMapID].AvailableGamemodes[gamemodeID] : Gamemodes.None;
            BotsToggle.interactable = _selectedGamemode != Gamemodes.Battlefield;
            if (!BotsToggle.interactable) BotsToggle.isOn = false;
            if ((IsRangeScene(Maps[_selectedMapID].Scene)))
            {
                int index = System.Array.IndexOf(PlayerNumberOptions, 12);
                if (index >= 0)
                {
                    PlayerNumberDropdown.SetValueWithoutNotify(index);
                    OnPlayerNumberOption(index);
                }
                BotsToggle.isOn = true;
            }
        }
        void OnGameDurationSelected(int timeOptionID)
        {
            _selectedTimeDurationID = timeOptionID;
        }
        void OnPlayerNumberOption(int playerOptionID)
        {
            _selectedPlayerNumberOptionID = playerOptionID;
        }

        //write parameters and start game as host
        void StartGame()
        {
            if ((IsRangeScene(Maps[_selectedMapID].Scene)))
                RangeCharacterSelection.Show(this, StartConfiguredGame);
            else StartConfiguredGame();
        }

        void StartConfiguredGame()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (SteamRoomSession.Instance && SteamRoomSession.Instance.Busy) return;
#endif
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            _networkManager.GetComponent<DNTransportImplementer>().RestoreLocalTransport();
            ConfigureRoom();
            _networkManager.StartHost();
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        public void StartSteamGame()
        {
            if (!MapselectionDropdown) return;
            if (SteamRoomSession.Instance.Busy || Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            if ((IsRangeScene(Maps[_selectedMapID].Scene)))
                RangeCharacterSelection.Show(this, CreateSteamRoom);
            else CreateSteamRoom();
        }

        void CreateSteamRoom()
        {
            ConfigureRoom();
            SteamRoomSession.Instance.Create("TPSShoot 房间", Maps[_selectedMapID].Scene,
                _selectedGamemode.ToString(), PlayerNumberOptions[_selectedPlayerNumberOptionID], null);
        }
#endif

        void ConfigureRoom()
        {
            _networkManager.onlineScene = Maps[_selectedMapID].Scene;

            RoomSetup.Properties.P_Gamemode = _selectedGamemode;
            RoomSetup.Properties.P_FillEmptySlotsWithBots = BotsToggle
                ? BotsToggle.isOn
                : _selectedGamemode != Gamemodes.Battlefield && !IsRainScene(Maps[_selectedMapID].Scene);
            RoomSetup.Properties.P_GameDuration = TimeOptionsInMinutes[_selectedTimeDurationID] * 60;
            RoomSetup.Properties.P_RespawnCooldown = 4f;

            //player count
            int maxPlayers = PlayerNumberOptions[_selectedPlayerNumberOptionID];
            RoomSetup.Properties.P_MaxPlayers = maxPlayers;
            _networkManager.maxConnections = maxPlayers;

        }

        public const string TrainingScenePath = "Assets/Game/Maps/TrainingWhitebox/TrainingWhitebox.unity";

        public const string ModularTrainingScenePath = "Assets/Game/Maps/TrainingRange/TrainingRange.unity";

        public static bool IsTrainingScene(string scene)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(scene);
            return string.Equals(name, "TrainingRange", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "TrainingWhitebox", System.StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "map_training", System.StringComparison.OrdinalIgnoreCase);
        }

        static bool IsRangeScene(string scene) => IsTrainingScene(scene) ||
            scene.EndsWith("/map_range.unity", System.StringComparison.OrdinalIgnoreCase);

        public static bool IsRainScene(string scene) =>
            !string.IsNullOrEmpty(scene) && (scene == "map_rain_coop" ||
            scene.EndsWith("/map_rain_coop.unity", System.StringComparison.OrdinalIgnoreCase));

        // New front-end owns selection; the existing room configuration remains authoritative.
        public void LaunchFromFrontEnd(int map, int character, bool steam)
        {
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            if (map < 0 || map >= Maps.Length) return;
            _networkManager = DNNetworkManager.Instance;
            _selectedMapID = map;
            if (MapselectionDropdown)
            {
                OnGamemodeSelected(0);
                if (IsRainScene(Maps[map].Scene)) BotsToggle.isOn = false;
            }
            else
            {
                var modes = Maps[map].AvailableGamemodes;
                _selectedGamemode = modes != null && modes.Length > 0 ? modes[0] : Gamemodes.None;
                _selectedPlayerNumberOptionID = Mathf.Max(0, System.Array.IndexOf(PlayerNumberOptions, 12));
            }
            RangeCharacterSelection.SetSelected(character);
            if (steam)
            {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (SteamRoomSession.Instance && !SteamRoomSession.Instance.Busy) CreateSteamRoom();
#endif
            }
            else StartConfiguredGame();
        }

        void StartServer() 
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (SteamRoomSession.Instance && SteamRoomSession.Instance.Busy) return;
#endif
            if (Mirror.NetworkClient.active || Mirror.NetworkServer.active) return;
            if (!_networkManager.GetComponent<DNTransportImplementer>().RestoreLocalTransport()) return;
            _networkManager.onlineScene = Maps[_selectedMapID].Scene;

            RoomSetup.Properties.P_Gamemode = _selectedGamemode;
            RoomSetup.Properties.P_FillEmptySlotsWithBots = BotsToggle.isOn;
            RoomSetup.Properties.P_GameDuration = TimeOptionsInMinutes[_selectedTimeDurationID] * 60;
            RoomSetup.Properties.P_RespawnCooldown = 4f;

            //player count
            int maxPlayers = PlayerNumberOptions[_selectedPlayerNumberOptionID];
            RoomSetup.Properties.P_MaxPlayers = maxPlayers;
            _networkManager.maxConnections = maxPlayers;

            _networkManager.StartServer();
        }
    }
}
