using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.UI;
namespace MTPSKIT.Gameplay.Gamemodes
{
    [RequireComponent(typeof(RoomManager))]
    public class Gamemode : NetworkBehaviour
    {
        protected GamemodeState State = GamemodeState.None;

        /// <summary>
        /// Gamemode indicator, its needed so game knows which gamemode player selected and which to initialize on ROOMMANAGER object
        /// </summary>
        public Gamemodes Indicator { get; protected set; } = Gamemodes.None;

        /// <summary>
        /// for bots to recognize if they have to also attack their team and for health to know if apply friendly fire damage
        /// </summary>
        public bool FFA = false;
        public int MaxTeamSize = 4;

        private int timeToEnd = 0;

        public int GameDuration = 60;

        public delegate void Gamemode_Timer(int seconds);
        public Gamemode_Timer GamemodeEvent_Timer;

        public delegate void PlayerKilledByPlayer(Health victim, CharacterPart hittedPart, AttackType attackType, Health killer, Health assist);
        public PlayerKilledByPlayer Client_PlayerKilledByPlayer { get; set; }

        /// <summary>
        /// Determines if players spawn by simple cooldown or their respawn is completely dependent on gamemode
        /// </summary>
        public bool LetPlayersSpawnOnTheirOwn { protected set; get; } = true;

        public bool FriendyFire { protected set; get; } = true;

        Coroutine _timerCounter;
        Coroutine _delaySwithGamemodeState;

        /// <summary>
        /// players sorted by their teams in lists
        /// </summary>
        protected List<Team> _teams = new List<Team>() { new Team(), new Team() };



        public bool PeacufulBots { get; set; }

        protected virtual void Awake()
        {
            PeacufulBots = false;
            LetPlayersSpawnOnTheirOwn = true;
        }
        protected virtual void Start()
        {
        }

        protected void StopTimer() 
        {
            if (_timerCounter != null)
            {
                StopCoroutine(_timerCounter);
                _timerCounter = null;
            }
        }

        protected void CountTimer(int seconds)
        {
            StopTimer();

            _timerCounter = StartCoroutine(CountTime(seconds));

            IEnumerator CountTime(int seconds)
            {
                timeToEnd = seconds;
                while (timeToEnd > 0)
                {
                    timeToEnd--;
                    RpcUpdateTimer(timeToEnd);
                    yield return new WaitForSeconds(1f);
                }
                TimerEnded();
            }
        }

        [ClientRpc]
        void RpcUpdateTimer(int seconds)
        {
            GamemodeEvent_Timer?.Invoke(seconds);
        }

        public virtual void Server_OnPlayerInstanceAdded(PlayerInstance player) 
        {
  
        }

        public virtual void Server_OnPlayerInstanceRemoved(PlayerInstance player)
        {

            //if player disconnects and was in certain team, then remove him from it
            if (player.Team != -1)
                _teams[player.Team].PlayerInstances.Remove(player);

            if(isServer)
            //say on chat that someone left the game
            Server_WriteToChat($"<color=#{ColorUtility.ToHtmlStringRGBA(Color.red)}>" + player.playerName + " 离开了对局</color>");

            //check if game can still run
            CheckTeamStates();
        }
        public virtual void Server_OnPlayerRemoved(PlayerInstance player)
        {
            if (!isServer) return;
        }

        public void DelaySetGamemodeState(GamemodeState state, float delay)
        {
            if (_delaySwithGamemodeState != null)
            {
                StopCoroutine(_delaySwithGamemodeState);
                _delaySwithGamemodeState = null;
            }

            _delaySwithGamemodeState = StartCoroutine(ESwitchGamemodeState());

            IEnumerator ESwitchGamemodeState()
            {
                yield return new WaitForSeconds(delay);
                SwitchGamemodeState(state);
            }
        }
        protected virtual void SwitchGamemodeState(GamemodeState state)
        {
            State = state;

            switch (State)
            {
                case GamemodeState.WaitingForPlayers:
                    GamemodeMessage("等待玩家加入…", 999f);
                    StopTimer();
                    break;

                case GamemodeState.Warmup:
                    GamemodeMessage("对局将在 10 秒后开始", 4f);
                    CountTimer(10);
                    break;
            }
        }

        public virtual GameObject GetPlayerCharacterPrefab() => DNNetworkManager.Instance.spawnPrefabs[0];
        public virtual void ConfigurePlayerCharacter(CharacterInstance character) {}

        public virtual void PlayerSpawnCharacterRequest(PlayerInstance playerInstance)
        {
           
        }

        

        public virtual void Relay_NewClientJoined(NetworkConnection conn, NetworkIdentity player)
        {

            if (!isServer) return;

            if (RoomSetup.Properties.P_FillEmptySlotsWithBots) 
            {
                List<PlayerInstance> players = new List<PlayerInstance>(GameManager.Players.Values);
                //if we let player connect, and there are as much "Players" as MaxSlot, then we have a bot and need to vanish him to make
                //place for new player
                if (players.Count >= RoomSetup.Properties.P_MaxPlayers)
                {
                    for (int i = 0; i < players.Count; i++)
                    {
                        if (players[i].BOT)
                        {
                            players[i].DespawnCharacterIfExist();
                            NetworkServer.Destroy(players[i].gameObject);
                            break;
                        }
                    }
                }
                else 
                {
                    FillEmptySlotsWithBots();
                }
            }

            TargeRPC_ClientSetupGamemode(conn, RoomSetup.Properties, player);
        }
        public virtual void Relay_ClientDisconnected(NetworkConnection conn, NetworkIdentity player)
        {
            if (!this || !NetworkServer.active) return;
            if (RoomSetup.Properties.P_FillEmptySlotsWithBots)
            {
                List<PlayerInstance> players = new List<PlayerInstance>(GameManager.Players.Values);

                if (players.Count < RoomSetup.Properties.P_MaxPlayers)
                {
                    FillEmptySlotsWithBots();
                }
            }
        }

        protected virtual int BotFillLimit => int.MaxValue;

        void FillEmptySlotsWithBots() 
        {
            List<PlayerInstance> players = new List<PlayerInstance>(GameManager.Players.Values);
            int neededBots = RoomSetup.Properties.P_MaxPlayers - players.Count;
            if (BotFillLimit < int.MaxValue)
            {
                int existingBots = players.FindAll(player => player.BOT).Count;
                neededBots = Mathf.Min(neededBots, BotFillLimit - existingBots);
            }

            for (int i = 0; i < neededBots; i++)
            {
                SpawnBot();
            }
        }

        protected void SpawnBot() 
        {
            if (!this || !NetworkServer.active || !DNNetworkManager.Instance || !DNNetworkManager.Instance.isNetworkActive) return;

            GameObject gm = Instantiate(DNNetworkManager.Instance.playerPrefab, transform.position, transform.rotation);
            NetworkServer.Spawn(gm);

            PlayerInstance playerInstance = gm.GetComponent<PlayerInstance>();
            playerInstance.playerName = "机器人 " + Random.Range(0, 999).ToString();

            if (playerInstance)
            {
                playerInstance.SetAsBot();
                playerInstance.RegisterPlayerInstance();

                playerInstance.ProcessRequestToJoinTeam(0);
                //if bot after trying to join team 0 is still "teamless" then try to join team 1
                if (playerInstance.Team == -1)
                    playerInstance.ProcessRequestToJoinTeam(1);
            }
        }

        [TargetRpc]
        void TargeRPC_ClientSetupGamemode(NetworkConnection conn, RoomProperties roomProperties, NetworkIdentity player)
        {
            //backend part
            if (!isServer)
                SetupGamemode(roomProperties);

            //ui part
            ClientFrontend.ClientEvent_OnJoinedToGame.Invoke(this, player);
        }

        public virtual void SetupGamemode(RoomProperties roomProperties)
        {
            RoomSetup.Properties = roomProperties;

            GameDuration = roomProperties.P_GameDuration;
            GameManager.SetGamemode(this);
        }

        protected virtual void TimerEnded()
        {

        }

        #region listeners

        
        public virtual void Server_OnPlayerKilled(Health victim, Health killer)
        {
        }

        #endregion

        /// <summary>
        /// method assings given player to given team and checks what should be done about it
        /// </summary>
        protected void SetTeam(PlayerInstance player, int team) 
        {
            AssignPlayerToTeam(player, team);

            CheckTeamStates();
        }

        [ClientRpc]
        protected void GamemodeMessage(string msg, float liveTime)
        {
            ClientFrontend.GamemodeEvent_Message?.Invoke(msg, liveTime);
        }

        protected Transform GetBestSpawnPoint(Transform[] spawnPoints, int team)
        {    
            //searching for best spawn point
            Transform bestSpawnPoint = spawnPoints[0];

            float bestDistance = 0;
            foreach (Transform spawnPoint in spawnPoints)
            {
                float nearestEnemyDistance = float.MaxValue;

                foreach (Health character in CustomSceneManager.spawnedCharacters)
                {
                    if (character.Team != team || GameManager.Gamemode.FFA)
                    {
                        float _currentCalculatedDistance = Vector3.Distance(character.transform.position, spawnPoint.position);
                        if (_currentCalculatedDistance < nearestEnemyDistance)
                        {
                            nearestEnemyDistance = _currentCalculatedDistance;
                        }
                    }
                }

                if (nearestEnemyDistance > bestDistance)
                {
                    bestSpawnPoint = spawnPoint;
                    bestDistance = nearestEnemyDistance;
                }
            }
            return bestSpawnPoint;
        }

        protected void AssignPlayerToTeam(PlayerInstance player, int teamToAssingTo)
        {
            //if player was already in team then remove him
            if (player.Team != -1)
            {
                _teams[player.Team].PlayerInstances.Remove(player);
            }

            //assign to team
            _teams[teamToAssingTo].PlayerInstances.Add(player);

            player.ServerSetTeam(teamToAssingTo);
        }

        /// <summary>
        /// checks if game can start if someone joined some team,
        /// or if game can be still running if someone left some team or
        /// totally disconnected from the game
        /// </summary>
        protected virtual void CheckTeamStates()
        {

        }

        #region player managament

        protected void RespawnAllPlayers(SpawnpointsContainer spawnpoints)
        {
            foreach (PlayerInstance pi in GameManager.Players.Values)
            {
                pi.SpawnCharacter(spawnpoints.GetNextSpawnPoint());
            }
        }

        /// <summary>
        /// block movent for all players
        /// </summary>
        protected void BlockAllPlayers(bool block = true) 
        {
            foreach (PlayerInstance pi in GameManager.Players.Values)
            {
                if(pi.MyCharacter)
                    pi.MyCharacter.BlockCharacter(block);
            }
        }

        /// <summary>
        /// reset kills/deaths stats
        /// </summary>
        protected void ResetPlayersStats()
        {
            foreach (PlayerInstance pi in GameManager.Players.Values)
            {
                pi.Kills = 0;
                pi.Deaths = 0;
                pi.Server_UpdateStatsForAllClients();
            }
        }
        #endregion

        /// <summary>
        /// process player request to join team, because team may be full or something else might be going on so
        /// we don't want to let players always join team that they want
        /// </summary>
        public void PlayerRequestToJoinTeam(PlayerInstance player, int requestedTeam)
        {
            int permission = PlayerRequestToJoinTeamPermission(player, requestedTeam);

            if (permission == 0)
            {
                AssignPlayerToTeam(player, requestedTeam);

                CheckTeamStates();

                OnPlayerAddedToTeam(player, requestedTeam);

                Server_WriteToChat($"{player.playerName} <color=#{ColorUtility.ToHtmlStringRGBA(ClientInterfaceManager.Instance.UIColorSet.TeamColors[requestedTeam])}> 加入了{(requestedTeam == 0? "蓝队": "橙队")} </color>");
            }

            //notify player if his request was accepted, and if not, tell him why, maybe team was full or sth else
            player.RpcTeamJoiningResponse(requestedTeam, permission);
        }

        /// <summary>
        /// What happens if given player joined given team
        /// </summary>
        protected virtual void OnPlayerAddedToTeam(PlayerInstance player, int team) 
        {
            
        }

        /// <summary>
        /// here we exactly specify conditions that have to be met in order to let player join his requested team
        /// return 0 if player has permission to join team
        /// return -1 if team is already full
        /// </summary>
        /// <returns></returns>
        protected virtual int PlayerRequestToJoinTeamPermission(PlayerInstance player, int requestedTeam) 
        {
            return 0;
        }

        [ClientRpc]
        public void Server_WriteToChat(string msg) 
        {
            if(ChatUI._instance)
            ChatUI._instance.WriteMessageToChat(msg);
        }

        public enum GamemodeState
        {
            None,
            WaitingForPlayers,
            Warmup,
            Inprogress,
            Finish,
        }
        [System.Serializable]
        public class Team
        {
            public List<PlayerInstance> PlayerInstances = new List<PlayerInstance>();
        }
    }

    public enum Gamemodes : byte
    {
        None = 0,
        Deathmatch = 1,
        TeamDeathmatch = 2,
        Battlefield = 3,
    }
}