using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public class UIScoreBoardPlayerElement : MonoBehaviour
    {
        [SerializeField] Text _playerName;
        [SerializeField] Text _kills;
        [SerializeField] Text _deaths;
        [SerializeField] Text _assists;
        [SerializeField] Text _latency;
        [SerializeField] Image _background;
        [SerializeField] Color _localPlayerColor = Color.yellow;

        public void WriteData(PlayerInstance player)
        {
            _playerName.text = player.playerName;
            _kills.text = player.Kills.ToString();
            _deaths.text = player.Deaths.ToString();
            _assists.text = player.Assists.ToString();
            //assign appropriate color for player in scoreboard depending on team, if player is not in any team, give him white color
            Color teamColor = player.Team == -1 ? Color.white : ClientInterfaceManager.Instance.UIColorSet.TeamColors[player.Team];

            _playerName.color = teamColor;
            _kills.color = teamColor;
            _deaths.color = teamColor;
            _assists.color = teamColor;

            _latency.text = player.BOT ? "机器人" : player.ClientPing.ToString();

            if (player.Team != -1 && player == GameManager.myPlayerInstance)
            {
                Color color = ClientInterfaceManager.Instance.UIColorSet.TeamColors[player.Team];
                _background.color = new Color(color.r, color.g, color.b, 0.1f);
            }
        }
    }
}