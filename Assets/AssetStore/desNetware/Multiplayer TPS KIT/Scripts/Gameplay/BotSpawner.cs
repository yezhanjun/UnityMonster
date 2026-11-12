using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
namespace MTPSKIT.Gameplay
{
    public class BotSpawner : NetworkBehaviour
    {
        public GameObject _object;
        private GameObject _mySpawnedObject;

        public int Team = 0;

        private void Update()
        {
            if (!isServer) return;

            if (Input.GetKeyDown(KeyCode.M))
            {
                Spawn();
            }
        }
        void Spawn()
        {
            if (!isServer) return;

            if (_mySpawnedObject)
                return;

            GameObject gm = Instantiate(_object, transform.position, transform.rotation);
            NetworkServer.Spawn(gm);

            PlayerInstance playerInstance = gm.GetComponent<PlayerInstance>();
            playerInstance.playerName = "机器人 " + Random.Range(0, 999).ToString();

            if (playerInstance)
            {
                playerInstance.SetAsBot();
                playerInstance.ProcessRequestToJoinTeam(Team);
            }

            _mySpawnedObject = gm;
        }
    }
}