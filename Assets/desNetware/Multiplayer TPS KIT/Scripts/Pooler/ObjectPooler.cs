using Mirror;

using MTPSKIT.UI;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MTPSKIT
{
    public class ObjectPooler : MonoBehaviour
    {
        public static ObjectPooler Instance { private set; get; }

        //inspector setup
        public List<ObjectPool> PoolsDefinitions = new List<ObjectPool>();

        Dictionary<string, ObjectPool> _spawnedPools = new Dictionary<string, ObjectPool>();
        private Scene _poleersScene;

        void Awake()
        {
            if (!Instance)
            {
                Instance = this;
            }
            else
                return;

            for (int i = 0; i < PoolsDefinitions.Count; i++)
            {
                ObjectPool def = PoolsDefinitions[i];

                _spawnedPools.Add(def.PoolPrefab.name, def);
            }


            SceneManager.sceneLoaded += OnSceneLoaded;
        }
        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode loadSceneMode)
        {
            //spawn pools for maps
            if (SceneManager.GetActiveScene().buildIndex == 0) return;

            for (int i = 0; i < PoolsDefinitions.Count; i++)
            {
                SpawnPool(PoolsDefinitions[i], _poleersScene);
            }
        }

        void SpawnPool(ObjectPool def, Scene scene)
        {
            if (!def.PoolPrefab) return;

            PooledObject[] pooledObjects = new PooledObject[def.NumberOfObjects];

            for (int i = 0; i < def.NumberOfObjects; i++)
            {
                PooledObject newObj = Instantiate(def.PoolPrefab).GetComponent<PooledObject>();
                pooledObjects[i] = newObj;

                newObj.OnObjectInstantiated();
            }


            def.AssignObjects(pooledObjects);
        }

        public ObjectPool GetPoolByName(string name)
        {
            return _spawnedPools[name];
        }
    }
    [System.Serializable]
    public class ObjectPool
    {
        public GameObject PoolPrefab;
        public int NumberOfObjects = 10;

        int _lastUsedObjectID = -1;
        PooledObject[] _objectsInPool;

        public void AssignObjects(PooledObject[] pooledObjects) => _objectsInPool = pooledObjects;


        public PooledObject ReturnObject(Vector3 pos, Quaternion rot)
        {
            if (_objectsInPool.Length == 0)
                return null;

            _lastUsedObjectID++;

            if (_lastUsedObjectID >= NumberOfObjects)
                _lastUsedObjectID = 0;

            PooledObject selectedObj = _objectsInPool[_lastUsedObjectID];
            selectedObj.transform.position = pos;
            selectedObj.transform.rotation = rot;
            selectedObj.OnObjectReused();

            return selectedObj;
        }
    }
}