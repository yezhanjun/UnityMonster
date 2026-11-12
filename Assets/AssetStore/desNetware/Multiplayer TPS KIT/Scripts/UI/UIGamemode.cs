using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Mirror;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;

namespace MTPSKIT.UI
{
    public class UIGamemode : MonoBehaviour
    {
        [SerializeField] protected UITimer _timer;


        Coroutine _messageLiveTimeCounter;

        public virtual void SetupUI(Gamemode gamemode, NetworkIdentity player)
        {
            ClientFrontend.GamemodeUI = this;

            gamemode.GamemodeEvent_Timer += _timer.UpdateTimer;

            GameManager.myPlayerInstance = player.GetComponent<PlayerInstance>();

            GameManager.myPlayerInstance.PlayerEvent_OnReceivedTeamResponse += OnReceivedTeamResponse;
    
        }

        
        public void SelectTeam(int team)
        {
            GameManager.myPlayerInstance.ClientRequestJoiningTeam(team);
        }
        protected virtual void OnReceivedTeamResponse(int team, int permissionCode)
        {
        }
        public virtual void Btn_ShowTeamSelector()
        {
        }

    }
}