using Mirror;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT.UI
{
    public class UITeamDeathmatch : UIGamemode
    {
        [SerializeField] Text _orangeScoreText;
        [SerializeField] Text _blueScoreText;
        [SerializeField] Text _teamSelectMessage;
        [SerializeField] GameObject _teamSelectPanel;

        public override void SetupUI(Gamemode gamemode, NetworkIdentity player)
        {
            base.SetupUI(gamemode, player);


            _orangeScoreText.color = ClientInterfaceManager.Instance.UIColorSet.TeamColors[1];
            _blueScoreText.color = ClientInterfaceManager.Instance.UIColorSet.TeamColors[0];

            TeamDeathmatch dm = gamemode.GetComponent<TeamDeathmatch>();
            dm.GamemodeEvent_TeamDeathmatch_PlayerKilled += OnPlayerKilled;

            gamemode.GamemodeEvent_Timer += _timer.UpdateTimer;

            //show cursor so client will be able to select team
            ClientFrontend.ShowCursor(true);
        }

        public void OnPlayerKilled(int blueScore, int orangeScore)
        {
            _blueScoreText.text = blueScore.ToString();
            _orangeScoreText.text = orangeScore.ToString();
        }


        public override void Btn_ShowTeamSelector()
        {
            base.Btn_ShowTeamSelector();
            ShowPanel(!_teamSelectPanel.activeSelf);
        }
        protected override void OnReceivedTeamResponse(int team, int permissionCode)
        {
            base.OnReceivedTeamResponse(team, permissionCode);
            if (permissionCode == 0)
            {
                ShowPanel(false);
            }
            else if (permissionCode == -1)
                _teamSelectMessage.text = "该队伍人数已满";
            else if (permissionCode == -2)
                _teamSelectMessage.text = "对局进行中无法更换队伍";
        }

        void ShowPanel(bool show)
        {
            ClientFrontend.ShowCursor(show);
            _teamSelectPanel.SetActive(show);

            if (!show)
                _teamSelectMessage.text = string.Empty;
        }
    }
}