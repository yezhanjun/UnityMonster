using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace MTPSKIT.UI
{
    public class PlayerLodout : MonoBehaviour
    {
        [SerializeField] GameObject _playerPrefab;
        [SerializeField] GameObject _playerSlotPrefab;
        [SerializeField] Transform _gridParent;
        PlayerLodoutSingleSlot[] _playerSlots; 

        void Start()
        {
            if (!_playerPrefab) return;

            CharacterItemManager characterItemManager = _playerPrefab.GetComponent<CharacterItemManager>();

            if (UserSettings.PlayerLodout == null || UserSettings.PlayerLodout.Length == 0)
            {
                UserSettings.PlayerLodout = new int[characterItemManager.Slots.Count];

                for (int i = 0; i < UserSettings.PlayerLodout.Length; i++)
                {
                    UserSettings.PlayerLodout[i] = -1;
                }
            }
            int slotCount = characterItemManager.Slots.Count;

            _playerSlots = new PlayerLodoutSingleSlot[slotCount];

            for (int i = 0; i < ItemManager.Instance.SlotsLodout.Length; i++)
            {
                //if (characterItemManager.Slots[i].SpecificItemOnly != string.Empty) continue;

                //if (i >= ItemManager.Instance.SlotsLodout.Length) return;

                PlayerLodoutSingleSlot plss = Instantiate(_playerSlotPrefab, _gridParent).GetComponent<PlayerLodoutSingleSlot>();
                plss.Draw(characterItemManager.Slots[i], i);
                plss.InitializeSelection(UserSettings.PlayerLodout[i]);

                _playerSlots.SetValue(plss, i);
            }

            _playerSlotPrefab.SetActive(false);
            if (GameManager.myPlayerInstance) GameManager.myPlayerInstance.CmdSendNewLodoutInfo(UserSettings.PlayerLodout, false);
        }
    }
}
