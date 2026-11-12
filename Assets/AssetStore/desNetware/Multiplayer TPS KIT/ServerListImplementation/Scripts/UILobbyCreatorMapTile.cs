using MTPSKIT;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT
{
    public class UILobbyCreatorMapTile : MonoBehaviour
    {
        [SerializeField] Button _button;
        [SerializeField] Image _mapIcon;
        [SerializeField] Text _mapName;
        [SerializeField] Image _backGround;
        int _mapID = -1;

        UILobbyCreator _lobbyCreator;

        public void Setup(UILobbyCreator creator, int mapID, MapRepresenter map)
        {
            _mapName.text = map.Name;
            _mapIcon.sprite = map.Icon;

            _lobbyCreator = creator;

            _mapID = mapID;

            _lobbyCreator.Event_OnMapSelected += OnMapSelected;

            _button.onClick.AddListener(OnClicked);
        }

        void OnClicked()
        {
            _lobbyCreator.SelectMap(_mapID);
        }
        void OnMapSelected(int mapID)
        {
            _backGround.color = mapID == _mapID ? Color.cyan : Color.black;
        }
    }
}