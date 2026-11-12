using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DNServerList;

namespace DNServerList.UI
{
    /// <summary>
    /// Component responsible for drawing server list in UI
    /// </summary>
    public class UIServerList : MonoBehaviour
    {
        [Header("Server list client")]
        [SerializeField] ServerListClient _serverListClient;

        [Header("")]
        [Header("UI elements")]
        [SerializeField] private GameObject _prefabLobbyTile;

        GameObject[] _spawnedTiles;
        [SerializeField] Transform _tilesParent;
        [SerializeField] GameObject _noRoomsMessage;

        private void Awake()
        {
            _noRoomsMessage.SetActive(false);
        }

        private void Start()
        {
            ClearSpawnedTiles();
        }

        public void RefreshServerList() 
        {
            _serverListClient.GetServerList();
        }

        public void DrawServerList(LobbyData[] lobbies, int firstIndex, int totalCount) 
        {
            ClearSpawnedTiles();

            _noRoomsMessage.gameObject.SetActive(lobbies.Length == 0);

            if (lobbies.Length <= 0)
            {
                _prefabLobbyTile.gameObject.SetActive(false);
                return;
            }

            _prefabLobbyTile.gameObject.SetActive(true);


            _spawnedTiles = new GameObject[lobbies.Length];

            

            for (int i = 0; i < lobbies.Length; i++)
            {
                _spawnedTiles.SetValue(Instantiate(_prefabLobbyTile.gameObject, _tilesParent.transform),i);
                _spawnedTiles[i].GetComponent<UIServerListLobbyRepresenter>().Setup(lobbies[i]);
            }

            _prefabLobbyTile.gameObject.SetActive(false);
        }

        void ClearSpawnedTiles()
        {
            _prefabLobbyTile.gameObject.SetActive(false);

            if (_spawnedTiles == null) return;

            for (int i = 0; i < _spawnedTiles.Length; i++)
            {
                Destroy(_spawnedTiles[i].gameObject);
            }
            _spawnedTiles = new GameObject[0];
        }
    }
}