using System.Collections.Generic;
using MTPSKIT.Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public sealed class AaaBattleHud : MonoBehaviour
    {
        Slider health, healthTrail;
        TMP_Text healthText, current, capacity, label;
        Transform ammoPanel, slotTemplate, separator;
        readonly List<Transform> slots = new List<Transform>();
        ReferenceBattleHud reference;

        public void Initialize()
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Application.platform == RuntimePlatform.WindowsPlayer || Application.platform == RuntimePlatform.WindowsEditor)
            {
                var font = transform.Find("Holder/Value/Text").GetComponent<TMP_Text>().font;
                foreach (Transform child in transform) child.gameObject.SetActive(false);
                reference = gameObject.AddComponent<ReferenceBattleHud>();
                reference.Initialize(font);
                return;
            }
#endif
            health = transform.Find("Holder/Health/Health Bar").GetComponent<Slider>();
            healthTrail = transform.Find("Holder/Health/Animation Bar").GetComponent<Slider>();
            healthText = transform.Find("Holder/Value/Text").GetComponent<TMP_Text>();
            ammoPanel = transform.Find("Weapon Ammo");
            Transform content = ammoPanel.Find("Content");
            current = content.Find("AmmoText").GetComponent<TMP_Text>();
            capacity = content.Find("ClipsText").GetComponent<TMP_Text>();
            label = content.Find("FireType").GetComponent<TMP_Text>();
            separator = content.Find("Separator");
            slotTemplate = transform.Find("Loadout/Content/Weapon (0)");
            slotTemplate.gameObject.SetActive(false);
        }

        public void SetHealth(int value, int maximum, bool reset = false)
        {
            if(reference){reference.SetHealth(value,maximum);return;}
            health.normalizedValue = maximum > 0 ? (float)value / maximum : 0;
            if (reset || healthTrail.value < health.value) healthTrail.value = health.value;
            healthText.SetText("{0} / {1}", value, maximum);
        }

        void Update()
        {
            if (healthTrail && healthTrail.value != health.value)
                healthTrail.value = Mathf.MoveTowards(healthTrail.value, health.value, Time.deltaTime);
        }

        public void SetAmmo(string value, string itemName)
        {
            if(reference){reference.SetAmmo(value,itemName);return;}
            ammoPanel.gameObject.SetActive(!string.IsNullOrEmpty(value));
            if (string.IsNullOrEmpty(value)) return;
            int split = value.IndexOf('/');
            bool magazine = split >= 0;
            current.text = magazine ? value.Substring(0, split) : value;
            capacity.text = magazine ? value.Substring(split + 1) : string.Empty;
            separator.gameObject.SetActive(magazine);
            label.text = magazine ? "弹匣" : itemName;
        }

        public void SetSlots(CharacterItemManager inventory)
        {
            if(reference){reference.SetSlots(inventory);return;}
            for (int i = 0; i < inventory.Slots.Count; i++)
            {
                if (i == slots.Count)
                    slots.Add(Instantiate(slotTemplate, slotTemplate.parent));
                var row = slots[i];
                Item item = inventory.Slots[i].Item;
                row.gameObject.SetActive(item);
                if (!item) continue;
                bool selected = i == inventory.currentlyUsedSlotID;
                row.Find("Bg").gameObject.SetActive(selected);
                var icon = row.Find("Icon").GetComponent<Image>();
                icon.sprite = item.ItemIcon;
                icon.enabled = item.ItemIcon;
                icon.color = selected ? Color.black : Color.white;
                var key = row.Find("Key/Text").GetComponent<TMP_Text>();
                key.SetText("{0}", i + 1);
                key.color = icon.color;
            }
            for (int i = inventory.Slots.Count; i < slots.Count; i++)
                slots[i].gameObject.SetActive(false);
        }
    }
}
