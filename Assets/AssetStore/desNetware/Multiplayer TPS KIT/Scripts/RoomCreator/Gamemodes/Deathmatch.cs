using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public class Deathmatch : Gamemode
    {
        public int KillsToWin = 20;

        [SerializeField] SpawnpointsContainer _spawnpoints;

        public Deathmatch()
        {
            Indicator = Gamemodes.Deathmatch;
            LetPlayersSpawnOnTheirOwn = true;
            FFA = true;
            FriendyFire = true; //friendly fire must be true because in free for all Deathmatch everyone are in the same team, 
            //so i they want to fight each other, friendy fire must be true
        }


        public override void PlayerSpawnCharacterRequest(PlayerInstance playerInstance)
        {
            base.PlayerSpawnCharacterRequest(playerInstance);

            playerInstance.SpawnCharacter(GetBestSpawnPoint(_spawnpoints.Spawnpoints.ToArray(), playerInstance.Team));
        }

        public override void Server_OnPlayerInstanceAdded(PlayerInstance player)
        {
            base.Server_OnPlayerInstanceAdded(player);
            if (!isServer) return;
            //in FFA we want all players to be in the same team, so we dont let team choose and we choose default team for them instead

            SetTeam(player, 0);

            player.Server_SpawnRequest();
        }

        public override void Server_OnPlayerKilled(Health victim, Health killer)
        {
            for (int i = 0; i < GameManager.Players.Count; i++)
            {
                var item = GameManager.Players.ElementAt(i);
                if (item.Value.Kills >= KillsToWin)
                {
                    SwitchGamemodeState(GamemodeState.Finish);
                }

            }
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

        protected override void CheckTeamStates()
        {
            base.CheckTeamStates();

            if (!isServer) return;

            //start the game if there is more than 1 player
            if (_teams[0].PlayerInstances.Count > 1)
            {
                if (State == GamemodeState.WaitingForPlayers)
                    SwitchGamemodeState(GamemodeState.Warmup);
            }
            else
            {
                SwitchGamemodeState(GamemodeState.WaitingForPlayers);
            }
        }

        protected override void SwitchGamemodeState(GamemodeState state)
        {
            base.SwitchGamemodeState(state);

            switch (state)
            {
                //start the game
                case GamemodeState.Inprogress:
                    ResetPlayersStats();
                    RespawnAllPlayers(_spawnpoints);

                    GamemodeMessage("对局开始！", 3f);
                    CountTimer(GameDuration);

                    LetPlayersSpawnOnTheirOwn = true;
                    break;

                //finish the game
                case GamemodeState.Finish:
                    StopTimer();

                    BlockAllPlayers(true);

                    LetPlayersSpawnOnTheirOwn = false;

                    //find the winner
                    List<PlayerInstance> players = new List<PlayerInstance>(GameManager.Players.Values);

                    players = players.OrderByDescending(x => x.Kills).ToList();

                    //display message who won
                    GamemodeMessage(players[0].playerName + " 获胜！", 5f);

                    //set timer for next round
                    DelaySetGamemodeState(GamemodeState.Warmup, 5f);
                    break;
            }
        }

        protected override void OnPlayerAddedToTeam(PlayerInstance player, int team)
        {
            player.SpawnCharacter(GetBestSpawnPoint(_spawnpoints.Spawnpoints.ToArray(), team));

        }

        protected override int PlayerRequestToJoinTeamPermission(PlayerInstance player, int requestedTeam)
        {
            return -1;
        }
    }
}