using MTPSKIT;
using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public class HudInventory : CharacterHud
    {
        [SerializeField] HudInventoryElement _elementPrefab;
        [SerializeField] Transform _elementsParent;
        CharacterItemManager _itemManager;

        List<HudInventoryElement> _spawnedElements = new List<HudInventoryElement>();


        //Transform _fppCameraTarget;

        [Header("SwapUI")]
        [SerializeField] Image _currentItem;
        [SerializeField] Image _potentialItem;
        bool _lookingAtPotentialItem = false;
        [SerializeField] GameObject _swapPanel;

        Item _previousItem;
        protected override void Awake()
        {
            base.Awake();
            _swapPanel.SetActive(false);
            _elementPrefab.gameObject.SetActive(false);
        }

        private void FixedUpdate()
        {
            if (!_itemManager) return;

            //TODO: fix out of range
            //   if (_itemManager.currentlyUsedItem.ItemType == SlotType.BuiltIn) return;

            _lookingAtPotentialItem = false;
            if (_myObservedCharacter.CurrentHealth > 0) //dont display UI for dead players
            {
                RaycastHit hit;
                if (Physics.Raycast(GameplayCamera._instance.transform.position, GameplayCamera._instance.transform.forward, out hit, 4.5f, GameManager.interactLayerMask))
                {
                    if (!_lookingAtPotentialItem)
                    {
                        GameObject go = hit.collider.gameObject;
                        Item _item = go.GetComponent<Item>();

                        if (_item && !_itemManager.AlreadyAquired(_item))
                        {
                            _potentialItem.sprite = _item.ItemIcon;
                            _lookingAtPotentialItem = true;

                            if (_previousItem != _item)
                                _lookingAtPotentialItem = false;

                            _previousItem = _item;
                        }
                    }
                }
            }

            if (_lookingAtPotentialItem != _swapPanel.activeSelf)
            {
                _swapPanel.SetActive(_lookingAtPotentialItem);
            }
        }


        protected override void AssignCharacterForUI(CharacterInstance characterToObserve)
        {
            characterToObserve.CharacterItemManager.Client_EquipmentChanged += OnEquipmentStateChanged;
            characterToObserve.Client_OnPickedupObject += OnObservedCharacterPickedupObject;
            _itemManager = characterToObserve.CharacterItemManager;
            //_fppCameraTarget = characterToObserve.FPPCameraTarget;

            OnEquipmentStateChanged(characterToObserve.CharacterItemManager.currentlyUsedSlotID);

        }

        protected override void DeassignCurrentCharacterFromUI(CharacterInstance _characterToDeassign)
        {
            _characterToDeassign.CharacterItemManager.Client_EquipmentChanged -= OnEquipmentStateChanged;
            _characterToDeassign.Client_OnPickedupObject -= OnObservedCharacterPickedupObject;
        }

        void OnObservedCharacterPickedupObject(string msg)
        {
            OnEquipmentStateChanged(_myObservedCharacter.CharacterItemManager.currentlyUsedSlotID);
        }

        void OnEquipmentStateChanged(int currentSlotID)
        {
            foreach (HudInventoryElement element in _spawnedElements)
                if (element) Destroy(element.gameObject);

            _spawnedElements.Clear();

            for (int i = 0; i < _itemManager.Slots.Count; i++)
            {

                _spawnedElements.Add(Instantiate(_elementPrefab.gameObject, _elementsParent).GetComponent<HudInventoryElement>());
                _spawnedElements[i].gameObject.SetActive(true);
                _spawnedElements[i].Draw(_itemManager.Slots[i].Item, _itemManager.Slots[i].Type, currentSlotID == i, i + 1, _itemManager.Slots[i]);

                //make empty eq slots in ui smaller
                _spawnedElements[i].transform.localScale = _itemManager.Slots[i].Item ? new Vector3(0.75f, 0.75f, 0.75f) : new Vector3(0.4f, 0.4f, 0.4f);
            }

            if (_itemManager.CurrentlyUsedItem)
                _currentItem.sprite = _itemManager.CurrentlyUsedItem.ItemIcon;
            else
                _currentItem.sprite = null;

            for (int i = 0; i < _itemManager.Slots.Count; i++)
            {
                if (_itemManager.Slots[i].Type == SlotType.Normal && !_itemManager.Slots[i].Item)
                {
                    _currentItem.sprite = null;
                }
            }
        }

        
    }
}