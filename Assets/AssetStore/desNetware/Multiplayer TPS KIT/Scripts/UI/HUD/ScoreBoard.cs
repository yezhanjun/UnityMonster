using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using MTPSKIT;

namespace MTPSKIT.UI.HUD
{
    public class ScoreBoard : MonoBehaviour
    {
        [SerializeField] GameObject _scoreBoard;
        [SerializeField] Transform _grid;
        [SerializeField] GameObject _playerPresenter;

        private List<GameObject> _instantiatedPresenters = new List<GameObject>();

        Coroutine c_refresher;

        void Start()
        {
            _playerPresenter.SetActive(false);
        }

        IEnumerator RefreshScoreboard()
        {
            while (true)
            {
                foreach (GameObject gm in _instantiatedPresenters)
                {
                    Destroy(gm);
                }
                _instantiatedPresenters.Clear();

                List<PlayerInstance> players = new List<PlayerInstance>(GameManager.Players.Values);
                players = players.OrderByDescending(x => x.Kills).ToList();

                for (int i = 0; i < players.Count; i++)
                {
                    PlayerInstance player = players[i];

                    GameObject presenter = Instantiate(_playerPresenter, _grid.position, _grid.rotation);
                    presenter.SetActive(true);
                    presenter.transform.SetParent(_grid);
                    presenter.GetComponent<UIScoreBoardPlayerElement>().WriteData(player);

                    _instantiatedPresenters.Add(presenter);
                }
                yield return new WaitForSeconds(0.5f);
            }
        }

        void Update()
        {
            _scoreBoard.SetActive(!ReferenceBattleHud.Active && Input.GetKey(KeyCode.Tab));

            if (_scoreBoard.activeSelf && c_refresher == null)
            {
                c_refresher = StartCoroutine(RefreshScoreboard());
            }

            if (!_scoreBoard.activeSelf && c_refresher != null)
            {
                StopCoroutine(c_refresher);
                c_refresher = null;
            }

        }
    }
}
