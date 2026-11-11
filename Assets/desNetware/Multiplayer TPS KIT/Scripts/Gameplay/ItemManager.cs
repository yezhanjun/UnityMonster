using UnityEngine;


namespace MTPSKIT.Gameplay {
    public class ItemManager : MonoBehaviour
    {
        public static ItemManager Instance;

        public LodoutForSlot[] SlotsLodout;

        private void Awake()
        {
            if (Instance) return;

            Instance = this;
        }
    }

    [System.Serializable]
    public class LodoutForSlot 
    {
        public string SlotName; //for inspector convienience only
        public GameObject[] availableItemsForSlot;
    }
}
