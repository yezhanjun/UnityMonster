using Mirror;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT.UI
{
    public class UIDeathmatch : UIGamemode
    {
        public override void SetupUI(Gamemode gamemode, NetworkIdentity player)
        {
            base.SetupUI(gamemode, player);

            Deathmatch dm = gamemode.GetComponent<Deathmatch>();
        }

        public void OnPlayerKilled(int blueScore, int orangeScore)
        {
            
        }
    }
}