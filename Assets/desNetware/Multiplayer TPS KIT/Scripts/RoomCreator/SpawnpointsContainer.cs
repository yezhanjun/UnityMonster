using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes {
    public class SpawnpointsContainer : MonoBehaviour
    {
        [HideInInspector] public int _lastUsedSpawnpointID;
        public List<Transform> Spawnpoints;

        public Transform GetNextSpawnPoint()
        {
            if (_lastUsedSpawnpointID >= Spawnpoints.Count)
                _lastUsedSpawnpointID = 0;

            Transform nextSpawnPoint = Spawnpoints[_lastUsedSpawnpointID];

            _lastUsedSpawnpointID++;

            return nextSpawnPoint;
        }
    }
    public enum SpawnpointsContainerType
    {
        Default,
        TeamA,
        TeamB,
    }
}