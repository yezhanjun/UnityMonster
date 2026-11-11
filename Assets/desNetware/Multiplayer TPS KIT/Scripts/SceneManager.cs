using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
namespace MTPSKIT.Gameplay
{
    public class CustomSceneManager : MonoBehaviour
    {
        public static CustomSceneManager singleton;

        public GameObject playerPrefab;
        public GameObject myPlayer;
        public Transform spawnPoint;

        public List<GameObject> itemPrefabs = new List<GameObject>();


        private void Awake()
        {
            singleton = this;
            //load all game items to list
       /*     List<GameObject> loadedItems = Resources.LoadAll("items", typeof(GameObject)).Cast<GameObject>().ToList();

            //cast all item id's in order
            List<GameItem> itemsID = Enum.GetValues(typeof(GameItem)).Cast<GameItem>().ToList();
            itemsID.RemoveAt(0); //removing id for "null" item
            itemPrefabs.Clear();
            itemPrefabs.Add(null); //adding "null" item

            for (int i = 0; i < itemsID.Count; i++)
            {
                GameObject item = loadedItems.Find(x => x.GetComponent<Item>().itemID == itemsID[i]); //find certain item with proper id in order of enum "items"
                if (item)
                {
                    itemPrefabs.Add(item); //add item to list in order of enum "items"
                    loadedItems.Remove(item); //remove from loaded items already added item
                }
            }*/
        }
      /*  public GameObject GetItemByEnum(GameItem _item)
        {
            int itemID = Mathf.Clamp((int)_item, 0, itemPrefabs.Count() - 1);

            if (itemID != (int)_item) Debug.Log($"Requested non existing item: {_item}");

            return Instantiate(itemPrefabs[itemID]);
        }*/



        public static List<Health> spawnedCharacters = new List<Health>();

        public static void RegisterCharacter(Health _char) 
        {
            spawnedCharacters.Add(_char);
        }
        public static void DeRegisterCharacter(Health _char)
        {
            spawnedCharacters.Remove(_char);
        }
    }
}
