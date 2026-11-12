using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT.UI
{
    public class HudInventoryElement : MonoBehaviour
    {
        [SerializeField] Image _itemIcon;
        [SerializeField] Image _itemBackground;


        [SerializeField] Color _notInUsebackGroundColor;
        [SerializeField] Color _inUsebackGroundColor;

        [SerializeField] Text _itemIDtext;
        [SerializeField] Color _inUseColor;
        [SerializeField] Color _notInUseColor;

        public void Draw(Item item, SlotType slotType, bool inUse, int slotID, Slot input)
        {
            gameObject.SetActive(slotType == SlotType.Normal || slotType == SlotType.BuiltIn && item);

            _itemIDtext.color = inUse ? _inUseColor : _notInUseColor;
            _itemIcon.color = inUse ? _inUseColor : _notInUseColor;

            _itemBackground.color = inUse ? _inUsebackGroundColor : _notInUsebackGroundColor;
            _itemIDtext.text = input.Key;

            if (!item)
            {
                transform.GetComponent<RectTransform>().sizeDelta = new Vector2(600, 80);
                _itemIcon.sprite = null;
                return;
            }

            _itemIcon.sprite = item.ItemIcon;

        }
    }
}