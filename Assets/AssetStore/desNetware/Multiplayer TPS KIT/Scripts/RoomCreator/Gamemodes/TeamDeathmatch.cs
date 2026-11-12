using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public class TeamDeathmatch : Gamemode
    {
        int _orangeScore;
        int _blueScore;
        public int ScoreToWin = 2000;

        public delegate void TeamDeathmatch_PlayerKilled(int blueScore, int orangeScore);
        public TeamDeathmatch_PlayerKilled GamemodeEvent_TeamDeathmatch_PlayerKilled;

        [SerializeField] SpawnpointsContainer _spawnpointsTeamA;
        [SerializeField] SpawnpointsContainer _spawnpointsTeamB;

        //set values inherited from Gamemodeclass appropriately for this gamemode
        public TeamDeathmatch()
        {
            Indicator = Gamemodes.TeamDeathmatch;
            LetPlayersSpawnOnTheirOwn = true; //it means that player will be spawned by cooldown counter from moment of his death
                                              //instead of game event
            FriendyFire = false;
        }

        public override void SetupGamemode(RoomProperties roomProperties)
        {
            base.SetupGamemode(roomProperties);
            MaxTeamSize = Mathf.FloorToInt(roomProperties.P_MaxPlayers / 2);
        }

        public override void Relay_NewClientJoined(NetworkConnection conn, NetworkIdentity player)
        {
            base.Relay_NewClientJoined(conn, player);

            TargetRPC_TDM_ClientSetupGamemode(conn, _blueScore, _orangeScore);
        }

        //for new clients
        [TargetRpc]
        void TargetRPC_TDM_ClientSetupGamemode(NetworkConnection conn, int blueScore, int orangeScore)
        {
            _orangeScore = orangeScore;
            _blueScore = blueScore;
            GamemodeEvent_TeamDeathmatch_PlayerKilled?.Invoke(_blueScore, _orangeScore);
        }

        public override void PlayerSpawnCharacterRequest(PlayerInstance playerInstance)
        {
            base.PlayerSpawnCharacterRequest(playerInstance);

            playerInstance.SpawnCharacter((playerInstance.Team == 0 ? _spawnpointsTeamA : _spawnpointsTeamB).GetNextSpawnPoint());
        }

        protected override void OnPlayerAddedToTeam(PlayerInstance player, int team)
        {
            base.OnPlayerAddedToTeam(player, team);
            player.SpawnCharacter((player.Team == 0 ? _spawnpointsTeamA : _spawnpointsTeamB).GetNextSpawnPoint());
        }

        public override void Server_OnPlayerKilled(Health victim, Health killer)
        {
            base.Server_OnPlayerKilled(victim, killer);

            //count score only when game runs, not for example during warmup
            if (State == GamemodeState.Inprogress)
            {

                if (victim.Team == 1)
                    _blueScore += 100;
                else
                    _orangeScore += 100;

                TDM_UpdateGamemodeState(_blueScore, _orangeScore);

                if (_blueScore >= ScoreToWin || _orangeScore >= ScoreToWin)
                    SwitchGamemodeState(GamemodeState.Finish);
            }
        }
        [ClientRpc]
        void TDM_UpdateGamemodeState(int blueScore, int orangeScore)
        {
            _orangeScore = orangeScore;
            _blueScore = blueScore;

            GamemodeEvent_TeamDeathmatch_PlayerKilled?.Invoke(_blueScore, _orangeScore);
        }

        protected override void TimerEnded()
        {
            base.TimerEnded();

            switch (State)
            {
                case GamemodeState.Warmup:
                    SwitchGamemodeState(GamemodeState.Inprogress);
                    break;

                case GamemodeState.Inprogress:
                    SwitchGamemodeState(GamemodeState.Finish);
                    break;
            }
        }

        protected override void SwitchGamemodeState(GamemodeState state)
        {
            base.SwitchGamemodeState(state);

            switch (state)
            {
                case GamemodeState.Inprogress:
                    foreach (PlayerInstance player in GameManager.Players.Values)
                    {
                        player.SpawnCharacter((player.Team == 0 ? _spawnpointsTeamA : _spawnpointsTeamB).GetNextSpawnPoint());
                    }

                    GamemodeMessage("对局开始！", 3f);
                    CountTimer(GameDuration);

                    ResetPlayersStats();


                    _blueScore = 0;
                    _orangeScore = 0;

                    TDM_UpdateGamemodeState(0, 0);


                    LetPlayersSpawnOnTheirOwn = true;
                    break;

                case GamemodeState.Finish:
                    StopTimer();

                    LetPlayersSpawnOnTheirOwn = false;

                    BlockAllPlayers(true);

                    if (_blueScore == _orangeScore)
                        GamemodeMessage("平局", 5f);
                    else
                        GamemodeMessage((_blueScore > _orangeScore ? "蓝队" : "橙队") + " 获胜！", 5f);

                    DelaySetGamemodeState(GamemodeState.Warmup, 5f);
                    break;
            }

        }

        protected override void CheckTeamStates()
        {
            if (!isServer) return;

            //start the game if there are players in both teams
            if (_teams[0].PlayerInstances.Count > 0 && _teams[1].PlayerInstances.Count > 0)
            {
                if (State == GamemodeState.WaitingForPlayers)
                    SwitchGamemodeState(GamemodeState.Warmup);
            }
            else
            {
                SwitchGamemodeState(GamemodeState.WaitingForPlayers);
            }
        }


        protected override int PlayerRequestToJoinTeamPermission(PlayerInstance player, int requestedTeam) 
        {
            if (_teams[requestedTeam].PlayerInstances.Count >= MaxTeamSize)
                return -1;

            if (State == GamemodeState.Inprogress && player.Team != -1) //dont let players change team during game, let only new players join team for the first time
                return -2;

            return 0;
        }
    }
}