using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT.UI;

namespace MTPSKIT.Gameplay
{
    public partial class PlayerInstance : NetworkBehaviour
    {
        [SyncVar] public int RainGold;
        [SyncVar] public int RainExperience;
        [SyncVar] internal int RainSteak, RainGlasses, RainTooth;
        int rainSyringes, rainStickyBombs, rainStunGrenades, rainBehemoths, rainFocusCrystals, rainWisps;
        int rainBaseHealth;
        int rainWarbanners, rainUkuleles, rainGasoline, rainIceRings, rainFireRings;
        int rainShieldGenerators, rainBucklers, rainOpals, rainBaubles, rainSeeds;
        RangeResearchItems.PersistentItems rainPersistentItems;
        [SyncVar] internal RangeResearchItems.Equipment RainEquipment;

        [Server]
        internal void RecordRainChestItems(RangeResearchItems items)
        {
            RainSteak = items.Steak;
            RainGlasses = items.Glasses;
            RainTooth = items.Tooth;
            rainPersistentItems = new RangeResearchItems.PersistentItems(items);
            RainEquipment = items.Equipped;
            rainSyringes = MyCharacter.ResearchSyringes;
            rainStickyBombs = MyCharacter.ResearchStickyBombs;
            rainStunGrenades = MyCharacter.ResearchStunGrenades;
            rainBehemoths = MyCharacter.ResearchBehemoths;
            rainFocusCrystals = MyCharacter.ResearchFocusCrystals;
            rainWisps = MyCharacter.ResearchWisps;
            rainWarbanners = MyCharacter.ResearchWarbanners;
            rainUkuleles = MyCharacter.ResearchUkuleles;
            rainGasoline = MyCharacter.ResearchGasoline;
            rainIceRings = MyCharacter.ResearchIceRings;
            rainFireRings = MyCharacter.ResearchFireRings;
            rainShieldGenerators = MyCharacter.ResearchShieldGenerators;
            rainBucklers = MyCharacter.ResearchBucklers;
            rainOpals = MyCharacter.ResearchOpals;
            rainBaubles = MyCharacter.ResearchBaubles;
            rainSeeds = MyCharacter.ResearchSeeds;
        }

        [Server]
        internal void GrantRainExperience(int reward)
        {
            int previousLevel = RainLevel;
            RainExperience = (int)System.Math.Min(int.MaxValue, (long)RainExperience + reward);
            int level = RainLevel;
            if (level == previousLevel || !MyCharacter || MyCharacter.CurrentHealth <= 0) return;
            MyCharacter.RainAttackPower = RainAttackAtLevel(level);
            MyCharacter.RainHealthRegen = RainRegenAtLevel(level);
            int increase = RainLevelHealthBonus(level) - RainLevelHealthBonus(previousLevel);
            MyCharacter.ServerSetMaximumHealth(MyCharacter.MaxHealth + increase);
            MyCharacter.ServerHeal(increase, MyCharacter.netId, allowOverhealBarrier: false);
            if (MyCharacter.ResearchWarbanners > 0 && GameManager.Gamemode is RangeGamemode rain)
                rain.PlaceRainBanner(MyCharacter.transform.position, MyCharacter.ResearchWarbanners);
        }

        int RainLevelHealthBonus(int level) => Mathf.RoundToInt(rainBaseHealth * .30f) * (level - 1);
        static float RainAttackAtLevel(int level) => CharacterInstance.RainBaseAttackPower * (1f + .20f * (level - 1));
        static float RainRegenAtLevel(int level) => 1f + .20f * (level - 1);

        [TargetRpc]
        internal void TargetRecordRainFirstClear(NetworkConnection connection)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (isLocalPlayer && SteamRoomSession.Instance) SteamRoomSession.Instance.GrantRainFirstClear();
#endif
        }

        readonly HashSet<string> serverRainMonsterLogs = new HashSet<string>(System.StringComparer.Ordinal);
        readonly HashSet<string> reportedRainMonsterLogs = new HashSet<string>(System.StringComparer.Ordinal);
        readonly List<string> pendingRainMonsterLogReports = new List<string>(RainMonsterLogs.Entries.Length);

        internal bool ServerHasRainMonsterLog(string logId) =>
            serverRainMonsterLogs.Contains(logId);

        [Server]
        internal void ServerGrantRainMonsterLog(string logId)
        {
            if (!RainMonsterLogs.IsKnown(logId)) return;
            serverRainMonsterLogs.Add(logId);
            TargetRecordRainMonsterLog(connectionToClient, logId);
        }

        [Command]
        void CmdReportRainMonsterLogs(string[] unlocked)
        {
            if (GameManager.Gamemode is not RangeGamemode mode || !mode.IsRainCoop ||
                unlocked == null || unlocked.Length > RainMonsterLogs.Entries.Length) return;
            foreach (string logId in unlocked)
                if (RainMonsterLogs.IsKnown(logId)) serverRainMonsterLogs.Add(logId);
        }

        void ReportRainMonsterLogs()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (!isLocalPlayer || !SteamRoomSession.Instance ||
                GameManager.Gamemode is not RangeGamemode mode || !mode.IsRainCoop) return;
            var session = SteamRoomSession.Instance;
            pendingRainMonsterLogReports.Clear();
            foreach (var entry in RainMonsterLogs.Entries)
                if (!reportedRainMonsterLogs.Contains(entry.UnlockableId) && session.HasRainMonsterLog(entry.UnlockableId))
                {
                    reportedRainMonsterLogs.Add(entry.UnlockableId);
                    pendingRainMonsterLogReports.Add(entry.UnlockableId);
                }
            if (pendingRainMonsterLogReports.Count != 0)
                CmdReportRainMonsterLogs(pendingRainMonsterLogReports.ToArray());
#endif
        }

        [TargetRpc]
        void TargetRecordRainMonsterLog(NetworkConnection connection, string logId)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (isLocalPlayer && SteamRoomSession.Instance) SteamRoomSession.Instance.GrantRainMonsterLog(logId);
#endif
        }

        public int RainLevel
        {
            get
            {
                int level = 1;
                while (RainExperience >= RainImported.RunLedger.Experience(level + 1)) level++;
                return level;
            }
        }

        internal bool TryInteractRainNode()
        {
            if (!isOwned || !(GameManager.Gamemode is RangeGamemode mode) || !mode.IsRainCoop) return false;
            int beacon = mode.NearbyRainBeacon(MyCharacter);
            if (beacon >= 0)
            {
                CmdActivateRainBeacon(beacon);
                return true;
            }
            int index = mode.NearbyRainChest(MyCharacter);
            if (index < 0) return false;
            CmdBuyRainChest(index);
            return true;
        }

        internal void RequestUseRangeEquipment()
        {
            if (!isOwned || GameManager.Gamemode is not RangeGamemode mode) return;
            if (isServer) ServerTryUseRangeEquipment();
            else if (mode.IsRainCoop && RainEquipment != RangeResearchItems.Equipment.None) CmdUseRainEquipment();
        }

        [Command]
        void CmdUseRainEquipment()
        {
            if (GameManager.Gamemode is RangeGamemode mode && mode.IsRainCoop)
                ServerTryUseRangeEquipment();
        }

        [Server]
        internal bool ServerTryUseRangeEquipment()
        {
            if (BOT || GameManager.Gamemode is not RangeGamemode mode || !MyCharacter ||
                !MyCharacter.TryGetComponent<RangeResearchItems>(out var items) || !items.TryUseEquipment())
                return false;

            if (mode.IsRainCoop) RecordRainChestItems(items);
            return true;
        }

        [Command]
        void CmdActivateRainBeacon(int index)
        {
            if (GameManager.Gamemode is RangeGamemode mode) mode.ActivateRainBeacon(this, index);
        }

        [Command]
        void CmdBuyRainChest(int index)
        {
            if (GameManager.Gamemode is RangeGamemode mode) mode.BuyRainChest(this, index);
        }
        public string playerName;
        [SyncVar(hook = nameof(OnCharacterSelectionChanged))] int characterSelection;
        void OnCharacterSelectionChanged(int previous, int next) => RangeRobotVisual.Apply(MyCharacter,
            BOT && GameManager.Gamemode is RangeGamemode ? 2 + (int)(netId % RangeRobotVisual.SuperBotNames.Length) : next);
        public int Team { private set; get; } = -1; //-1 for no team

        public int Kills;
        public int Deaths;
        public int Assists;

        int[] _lodout;

        public bool SpawnCooldown = true;

        public CharacterInstance MyCharacter { private set; get; }

        public bool BOT { private set; get; } = false;


        public delegate void OnReceivedTeamResponse(int team, int permissionCode);
        public OnReceivedTeamResponse PlayerEvent_OnReceivedTeamResponse { set; get; }


        //counting for respawn
        Coroutine _spawnCooldownCoroutine;

        bool _registeredPlayerInstance = false;
        bool _subbedToNetworkManager;
        bool _serverInstance;

        [Space]
        [Header("Ping")]
        [SerializeField] float _pingUpdateInterval = 1f;
        float _pingUpdateTimer;
        public ushort ClientPing; //ping info sent to clients

        uint _netID;
        int _connID = -1;

        private void Start()
        {
            _netID = netId;

            DNNetworkManager.Instance.OnNewPlayerConnected += UpdateDataForLatePlayer;
            _subbedToNetworkManager = true;

            RegisterPlayerInstance();

            if (isServer)
            {
                ServerRegisterPlayerInstance();

                if (connectionToClient != null)
                {
                    GameManager.Gamemode.Relay_NewClientJoined(connectionToClient, this.netIdentity);

                    _connID = connectionToClient.connectionId;
                    GameManager.PlayersByConnectionID.Add(_connID, this);
                }
            }

            if (BOT && isServer)
                //tell everyone on chat that bot has joined
                GameManager.Gamemode.Server_WriteToChat($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.cyan)}>" + playerName + " 加入了对局 </color>");

            if (isOwned)
            {
                GameManager.myPlayerInstance = this;
                CmdReceivePlayerData(UserSettings.UserNickname, RangeCharacterSelection.Selected);
            }
        }

        private void Update()
        {
            TickHeroLoadout();
            if (isOwned)
            {
                _pingUpdateTimer += Time.deltaTime;
                if (_pingUpdateInterval < _pingUpdateTimer)
                {
                    float rttms = (float)NetworkTime.rtt * 1000f;
                    CmdClientSendPing((ushort)rttms);
                    ReportRainMonsterLogs();
                    ClientPing = (ushort)rttms;
                    _pingUpdateTimer = 0f;
                }
            }

            if (!MyCharacter || (MyCharacter && MyCharacter.CurrentHealth <= 0))
            {
                if (isOwned)
                {
                    if (Input.GetKeyDown(KeyCode.Space))
                    {
                        CmdProcessSpawnRequest();
                    }
                }

                if (SpawnCooldown && BOT && isServer)
                    Server_SpawnRequest();
            }
        }

        public void RegisterPlayerInstance() 
        {
            if (!_registeredPlayerInstance)
            {
                GameManager.AddPlayerInstance(this);
                _registeredPlayerInstance = true;
            }
        }
        public void ServerRegisterPlayerInstance()
        {
            GameManager.Gamemode.Server_OnPlayerInstanceAdded(this);

            _serverInstance = true;
        }


        public void SetAsBot(bool bot = true) 
        {
            BOT = bot;
            RpcSetAsBot(bot);
        }
        void RpcSetAsBot(bool bot) { 
            if (isServer) return;
            BOT = bot; }

        [Command]
        void CmdProcessSpawnRequest()
        {
            Server_SpawnRequest();
        }
        public void Server_SpawnRequest()
        {
            if(Team != -1 && SpawnCooldown && GameManager.Gamemode.LetPlayersSpawnOnTheirOwn)
                GameManager.Gamemode.PlayerSpawnCharacterRequest(this);
        }

        [Server]
        public bool ServerSetRangeHero(int selection)
        {
            if(!(GameManager.Gamemode is RangeGamemode)||!MyCharacter||MyCharacter.CurrentHealth<=0)return false;
            if(!RangeCharacterSelection.IsAvailable(selection))return false;
            characterSelection=selection;
            Jotunnslayer.GodSkillGm.Stop(MyCharacter);
            MyCharacter.ServerSetHeroBonuses(default);
            MyCharacter.HeroTemporaryArmor = 0;
            MyCharacter.HeroArmorUntil = 0;
            sentHero = -1;
            nextHeroAggregate = 0;
            HeroQ = HeroE = HeroV = HeroPassive1 = HeroPassive2 = HeroPassive3 = HeroTrait = "";
            HeroWeapon = -1;
            appliedTrait = null;
            RangeRobotVisual.Apply(MyCharacter,selection);
            return true;
        }

        public void SpawnCharacter(Transform spawnPoint)
        {
            if (Team == -1) return; //dont spawn character if we are not assigned to a team

            SpawnCooldown = false;

            if (_spawnCooldownCoroutine != null)
            {
                StopCoroutine(_spawnCooldownCoroutine);
                _spawnCooldownCoroutine = null;
            }

            //despawn previous character if it exist
            DespawnCharacterIfExist();

            //if we have more players than spawnpoint and we spawn them all at once, then adding position randomness around spawnpoint
            //will eliminate situations where players are spawned on exactly same positions, causing them to stuck
            float radiusFromSpawnpoint = 0.4f;
            Vector3 spawnPositionRandomness;
            spawnPositionRandomness = Quaternion.Euler(0, Random.Range(0, 360), 0) * (new Vector3(0, 0, radiusFromSpawnpoint));

            MyCharacter = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), spawnPoint.position + spawnPositionRandomness, spawnPoint.rotation).GetComponent<CharacterInstance>();
            MyCharacter.GetComponent<CharacterMotor>().BindHeroPlayer(this);
            MyCharacter.lookInput.y = spawnPoint.eulerAngles.y;

            #region assign items
            //Assign player lodout, so appropriate items will be spawned
            CharacterItemManager characterItemManager = MyCharacter.GetComponent<CharacterItemManager>();

            //if this player intance is bot, then randomize his lodout every time he spawns, just for gameplay variety
            if (BOT && (_lodout == null || _lodout.Length <= 0))
            {
                List<int> randomItems = new List<int>();

                for (int i = 0; i < ItemManager.Instance.SlotsLodout.Length; i++)
                {
                    randomItems.Add(Random.Range(0, ItemManager.Instance.SlotsLodout[i].availableItemsForSlot.Length));
                }
                _lodout = randomItems.ToArray();
            }

            //if no special equipment is required just spawn character with items that player wants
            if(_lodout != null && _lodout.Length > 0)
                for (int i = 0; i < _lodout.Length; i++)
                {
                    if (_lodout[i] < 0) continue;

                    if (i >= characterItemManager.Slots.Count) break;

                    if (i >= ItemManager.Instance.SlotsLodout.Length || _lodout[i] >= ItemManager.Instance.SlotsLodout[i].availableItemsForSlot.Length) continue;

                    characterItemManager.Slots[i].ItemOnSpawn = ItemManager.Instance.SlotsLodout[i].availableItemsForSlot[_lodout[i]].gameObject;
                }
            #endregion

            // Wendy starts with the existing networked AAA rifle. Explicit hero loadouts still override it.
            if (!BOT && characterSelection == RangeCharacterSelection.FirstPortable &&
                GameManager.Gamemode is RangeGamemode && characterItemManager.Slots.Count > 0)
                characterItemManager.Slots[0].ItemOnSpawn = Resources.Load<GameObject>("FpsRifle");

            if (BOT && GameManager.Gamemode is RangeGamemode range)
            {
                foreach (var slot in characterItemManager.Slots) slot.ItemOnSpawn = null;
                MyCharacter.MaxHealth = MyCharacter.CurrentHealth = range.RobotHealth;
            }

            GameManager.Gamemode.ConfigurePlayerCharacter(MyCharacter);
            if (!BOT && GameManager.Gamemode is RangeGamemode rain && rain.IsRainCoop)
            {
                rainBaseHealth = MyCharacter.MaxHealth;
                MyCharacter.RainAttackPower = RainAttackAtLevel(RainLevel);
                MyCharacter.RainHealthRegen = RainRegenAtLevel(RainLevel);
                MyCharacter.ResearchSyringes = rainSyringes;
                MyCharacter.ResearchStickyBombs = rainStickyBombs;
                MyCharacter.ResearchStunGrenades = rainStunGrenades;
                MyCharacter.ResearchBehemoths = rainBehemoths;
                MyCharacter.ResearchFocusCrystals = rainFocusCrystals;
                MyCharacter.ResearchWisps = rainWisps;
                MyCharacter.ResearchWarbanners = rainWarbanners;
                MyCharacter.ResearchUkuleles = rainUkuleles;
                MyCharacter.ResearchGasoline = rainGasoline;
                MyCharacter.ResearchIceRings = rainIceRings;
                MyCharacter.ResearchFireRings = rainFireRings;
                if (RainSteak > 0 || RainGlasses > 0 || RainTooth > 0 || rainSyringes > 0 ||
                    rainStickyBombs > 0 || rainStunGrenades > 0 || rainBehemoths > 0 || rainFocusCrystals > 0 ||
                    rainWisps > 0 || rainWarbanners > 0 || rainUkuleles > 0 || rainGasoline > 0 ||
                    rainIceRings > 0 || rainFireRings > 0 || rainBucklers > 0 || rainOpals > 0 || rainBaubles > 0 ||
                    rainSeeds > 0 || rainPersistentItems.NeedsRestore)
                {
                    if (!MyCharacter.TryGetComponent<RangeResearchItems>(out var rainItems))
                        rainItems = MyCharacter.gameObject.AddComponent<RangeResearchItems>();
                    rainItems.RestoreRainChestItems(RainSteak, RainGlasses, RainTooth);
                    rainPersistentItems.Restore(rainItems);
                }
                MyCharacter.MaxHealth = rainBaseHealth + RainLevelHealthBonus(RainLevel) + RainSteak * 25 + rainPersistentItems.BonusHealth;
                if (MyCharacter.TryGetComponent<RangeResearchItems>(out var restoredInventory))
                    MyCharacter.MaxHealth = restoredInventory.RecalculateItemMaximum(MyCharacter.MaxHealth);
                MyCharacter.CurrentHealth = MyCharacter.MaxHealth;
                MyCharacter.ResearchShieldGenerators = rainShieldGenerators;
                MyCharacter.ResearchBucklers = rainBucklers;
                MyCharacter.ResearchOpals = rainOpals;
                MyCharacter.ResearchBaubles = rainBaubles;
                MyCharacter.ResearchSeeds = rainSeeds;
                MyCharacter.RefreshResearchShield();
            }
            MyCharacter.Team = Team;
            NetworkServer.Spawn(MyCharacter.gameObject, connectionToClient);

            MyCharacter.Team = Team;
            MyCharacter.Server_OnHealthDepleted += OnCharacterHealthDepleted;
            MyCharacter.Server_KilledCharacter += PlayerKilled;
            MyCharacter.Server_OnEarnedAssist += OnEarnedAssist;

            if (BOT)
                MyCharacter.SetAsBOT(BOT);

            WritePlayerData(playerName, Team, MyCharacter.netIdentity, BOT);
            RpcReceivePlayerData(playerName, MyCharacter.netIdentity, Team, BOT);
        }

        public void DespawnCharacterIfExist()
        {
            if (MyCharacter == null) return;
            //despawn previous character

            MyCharacter.CharacterItemManager.OnDespawnCharacter(); //when despawning character despawn also its equipment
            MyCharacter.Server_OnHealthDepleted -= OnCharacterHealthDepleted;
            MyCharacter.Server_KilledCharacter -= PlayerKilled;
            MyCharacter.Server_OnEarnedAssist -= OnEarnedAssist;

            NetworkServer.Destroy(MyCharacter.gameObject);
        }

        private void OnCharacterHealthDepleted(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce)
        {
            //count death for scoreboard
            Deaths++;
            Server_UpdateStatsForAllClients();
            CountCooldown();
        }

        private void OnEarnedAssist(Health victimID)
        {
            Assists++;
            RpcUpdateStats(Kills, Deaths, Assists);
        }

        void PlayerKilled(Health health)
        {
            Kills++;
            Server_UpdateStatsForAllClients();

            if (BOT)
                GetComponent<ChatBehaviour>().RpcHandleChatClientMessage($"我击杀了 {health.CharacterName}！");
        }


        /// <summary>
        /// wait to be able to respawn
        /// </summary>
        public void CountCooldown() 
        {
            SpawnCooldown = false;

            if (_spawnCooldownCoroutine != null) 
            {
                StopCoroutine(_spawnCooldownCoroutine);
                _spawnCooldownCoroutine = null;
            }

            _spawnCooldownCoroutine = StartCoroutine(CountSpawnCooldown());
            IEnumerator CountSpawnCooldown()
            {
                yield return new WaitForSeconds(RoomSetup.Properties.P_RespawnCooldown);
                SpawnCooldown = true;

                if (GameManager.Gamemode.LetPlayersSpawnOnTheirOwn) 
                    Server_SpawnRequest();
            }
        }

        #region update stats
        public void Server_UpdateStatsForAllClients()
        {
            RpcUpdateStats(Kills, Deaths, Assists);
        }
        [ClientRpc]
        void RpcUpdateStats(int kills, int deaths, int assists)
        {
            if (isServer) return;
            Kills = kills;
            Deaths = deaths;
            Assists = assists;
        }
        #endregion

        #region SendPlayerData

        [Command]
        void CmdReceivePlayerData(string _username, int selection)
        {
            characterSelection = RangeCharacterSelection.IsPortable(selection) && RangeCharacterSelection.IsAvailable(selection)
                ? selection : GameManager.Gamemode is RangeGamemode && selection == 1 ? 1 : 0;
            RangeRobotVisual.Apply(MyCharacter, characterSelection);
            playerName = _username;
            RpcReceivePlayerData(_username, (MyCharacter? MyCharacter.netIdentity: null), Team, BOT);

            //tell everyone on chat that player joined
            GameManager.Gamemode.Server_WriteToChat($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.cyan)}>" + playerName + " 加入了对局 </color>");
        }

        [ClientRpc]
        void RpcReceivePlayerData(string _username, NetworkIdentity _charNetIdentity, int team, bool isBot)
        {
            WritePlayerData(_username, team, _charNetIdentity, isBot);

            if (_charNetIdentity && isOwned) 
            {
                PlayerGameplayInput.Instance.AssignCharacterToBeControlledByPlayer(_charNetIdentity.GetComponent<CharacterInstance>());
            }
        }

        //update for late players
        void UpdateDataForLatePlayer(NetworkConnection conn)
        {
            TargetRpcReceiveDataForLatePlayer(conn, playerName, MyCharacter ? MyCharacter.netIdentity : null, Kills, Deaths, Team, BOT);
        }

        [TargetRpc]
        void TargetRpcReceiveDataForLatePlayer(NetworkConnection conn, string username, NetworkIdentity charNetIdentity, int kills, int deaths, int team, bool isBot)
        {
            Team = team;
            Kills = kills;
            Deaths = deaths;

            WritePlayerData(username, team, charNetIdentity, isBot);
        }

        void WritePlayerData(string username, int team, NetworkIdentity charNetIdentity, bool isBot)
        {
            BOT = isBot;

            playerName = username;
            Team = team;

            //for inspector so we can better tell who is who
            gameObject.name = "Player: " + username;

            if (charNetIdentity != null)
            {
                MyCharacter = charNetIdentity.GetComponent<CharacterInstance>();
                MyCharacter.GetComponent<CharacterMotor>().BindHeroPlayer(this);
                RangeRobotVisual.Apply(MyCharacter, BOT && GameManager.Gamemode is RangeGamemode
                    ? 2 + (int)(netId % RangeRobotVisual.SuperBotNames.Length) : characterSelection);
                MyCharacter.CharacterName = username;
                MyCharacter.Team = team;
                if (BOT && GameManager.Gamemode is RangeGamemode && isClient)
                    RangeRobotHealthBars.Register(MyCharacter);
                //for inspector so we can better whose character is whose
                MyCharacter.gameObject.name = "Character: " + username;
            }

            if (isOwned)
                ClientFrontend.ThisClientTeam = team;

        }
        #endregion

        //launched by UI
        public void ClientRequestJoiningTeam(int team) 
        {
            ProcessClientRequestToJoinTeam(team);
        }

        //here we can check for example if team that client wants to join to is not full 
        [Command]
        void ProcessClientRequestToJoinTeam(int team) 
        {
            ProcessRequestToJoinTeam(team);
        }
        public void ProcessRequestToJoinTeam(int team)
        {
            GameManager.Gamemode.PlayerRequestToJoinTeam(this, team);
        }

        [ClientRpc]
        public void RpcTeamJoiningResponse(int team, int permissionCode) {
            //for UI to subscribe to
            PlayerEvent_OnReceivedTeamResponse?.Invoke(team, permissionCode);
        }
        public void ServerSetTeam(int team) 
        {
            Team=team;
            RpcSetTeam(team);
        }
        [ClientRpc]
        void RpcSetTeam(int team) 
        {
            Team = team;
        }


        #region lodout
        [Command]
        public void CmdSendNewLodoutInfo(int[] loadoutInfo, bool applyInRange)
        {
            _lodout = loadoutInfo;
            if (applyInRange && !BOT && MyCharacter && MyCharacter.CurrentHealth > 0 && GameManager.Gamemode is RangeGamemode)
                GameManager.Gamemode.PlayerSpawnCharacterRequest(this);
        }
        #endregion

        #region ping
        [Command]
        void CmdClientSendPing(ushort ping)
        {
            ClientPing = (byte)ping;
            RpcSendPingInfoToPlayer(ClientPing);
        }
        [ClientRpc(includeOwner = false)]
        void RpcSendPingInfoToPlayer(ushort ping)
        {
            ClientPing = ping;
        }
        #endregion

        private void OnDestroy()
        {
            if(_subbedToNetworkManager)
                DNNetworkManager.Instance.OnNewPlayerConnected -= UpdateDataForLatePlayer;

            if (_registeredPlayerInstance)
            {
                GameManager.RemovePlayerInstance(this, _netID);
            }

            if(_serverInstance)
                GameManager.Gamemode.Server_OnPlayerInstanceRemoved(this);

            if (_connID != -1) 
            {
                GameManager.PlayersByConnectionID.Remove(_connID);
                GameManager.Gamemode.Relay_ClientDisconnected(connectionToClient, this.netIdentity);
            }
        }
    }

}
