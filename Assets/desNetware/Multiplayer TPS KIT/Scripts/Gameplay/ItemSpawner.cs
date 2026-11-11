using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    public class ItemSpawner : NetworkBehaviour
    {
        public GameObject ItemPrefab;
        private GameObject _spawnedItem;

        private void FixedUpdate()
        {
            if (!_spawnedItem)
                Spawn();
        }
        void Spawn() 
        {
            if (!isServer) return;

            if (_spawnedItem) NetworkServer.Destroy(_spawnedItem);

            _spawnedItem = Instantiate(ItemPrefab, transform.position, transform.rotation);
            NetworkServer.Spawn(_spawnedItem);

        }
    }
}