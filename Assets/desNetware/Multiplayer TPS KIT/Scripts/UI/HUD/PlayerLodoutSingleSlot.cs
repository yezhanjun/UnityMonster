using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI
{
    public class PlayerLodoutSingleSlot : MonoBehaviour
    {
        private List<int> itemIDs = new List<int>();
        [SerializeField] Dropdown _dropdown;

        int _playerSlotID;
        bool drawing;

        private void Awake()
        {
            _dropdown.onValueChanged.AddListener(OnItemSelected);
        }
        public void Draw(Slot slot, int playerSlotID) 
        {


            _playerSlotID = playerSlotID;
            drawing = true;



            _dropdown.ClearOptions();
            itemIDs.Clear();
            List<Dropdown.OptionData> options = new List<Dropdown.OptionData>();

            for (int i = 0; i < ItemManager.Instance.SlotsLodout[playerSlotID].availableItemsForSlot.Length; i++)
            {
                Item item = ItemManager.Instance.SlotsLodout[playerSlotID].availableItemsForSlot[i].GetComponent<Item>();
                //if (item.SlotType != slot.Type) continue;
                options.Add(new Dropdown.OptionData { text = item.ItemName, image = item.ItemIcon });
                
                itemIDs.Add(i);
            }

            _dropdown.AddOptions(options);
            foreach (var label in GetComponentsInChildren<Text>(true))
                if (label.text.StartsWith("装备槽")) label.text = "装备槽 " + (playerSlotID + 1);
            drawing = false;
        }

        public void InitializeSelection(int selection)
        {
            if (itemIDs.Count == 0) return;
            selection = Mathf.Clamp(selection, 0, itemIDs.Count - 1);
            _dropdown.SetValueWithoutNotify(selection);
            UserSettings.PlayerLodout[_playerSlotID] = itemIDs[selection];
        }

        public void OnItemSelected(int optionID) 
        {
            if (drawing || optionID < 0 || optionID >= itemIDs.Count) return;

            UserSettings.PlayerLodout[_playerSlotID] = itemIDs[optionID];

            if (GameManager.myPlayerInstance)
                GameManager.myPlayerInstance.CmdSendNewLodoutInfo(UserSettings.PlayerLodout, true);
        }
    }
}
