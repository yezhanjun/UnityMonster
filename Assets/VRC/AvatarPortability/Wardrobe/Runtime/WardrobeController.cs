using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvatarPortability.Wardrobe
{
    [DisallowMultipleComponent]
    public sealed class WardrobeController : MonoBehaviour
    {
        [Tooltip("Defaults to this GameObject. Do not put clothing below the avatar skeleton.")]
        public Transform avatarRoot;
        public WardrobeItem[] startupItems = Array.Empty<WardrobeItem>();
        private readonly Dictionary<string, GameObject> equipped = new Dictionary<string, GameObject>(StringComparer.Ordinal);
        private Transform staging;

        private void Awake()
        {
            // Only direct outfit children belong to this controller; nested avatars own theirs.
            for (int i = 0; i < transform.childCount; i++)
            {
                var child = transform.GetChild(i);
                var saved = child.GetComponent<BakedWardrobeSlot>();
                if (!saved || string.IsNullOrWhiteSpace(saved.slot)) continue;
                if (equipped.ContainsKey(saved.slot))
                {
                    Debug.LogWarning("Duplicate baked wardrobe slot: " + saved.slot, this);
                    continue;
                }
                equipped.Add(saved.slot, child.gameObject);
            }
        }

        private void Start()
        {
            if (startupItems == null) return;
            foreach (WardrobeItem item in startupItems)
            {
                if (item && GetEquipped(item.slot)) continue;
                if (!TryEquip(item, out string report)) Debug.LogWarning(report, this);
            }
        }

        public GameObject GetEquipped(string slot)
        {
            return slot != null && equipped.TryGetValue(slot, out GameObject instance) ? instance : null;
        }

        public bool TryEquip(WardrobeItem item, out string report)
        {
            if (!Application.isPlaying) { report = "Equip requires Play mode; use editor Validate for asset checks."; return false; }
            if (!item || !item.prefab || string.IsNullOrWhiteSpace(item.slot))
            { report = "Wardrobe item needs a prefab and slot."; return false; }
            if (!staging)
            {
                var stage = new GameObject("Wardrobe Staging");
                stage.transform.SetParent(transform, false);
                stage.SetActive(false);
                staging = stage.transform;
            }
            // An inactive parent prevents OnEnable/Start on an unbound candidate.
            GameObject candidate = Instantiate(item.prefab, staging, false);
            candidate.SetActive(false);
            Transform body = avatarRoot ? avatarRoot : transform;
            try
            {
                if (!WardrobeBinding.TryBind(item, candidate, body, out report))
                { Destroy(candidate); return false; }
            }
            catch (Exception exception)
            { Destroy(candidate); report = "Wardrobe binding failed: " + exception.Message; return false; }
            candidate.transform.SetParent(transform, false);
            WardrobePoseFollower follower = candidate.GetComponent<WardrobePoseFollower>();
            if (follower) follower.Synchronize();
            candidate.name = item.prefab.name + " [" + item.slot + "]";
            if (equipped.TryGetValue(item.slot, out GameObject previous) && previous)
            { previous.SetActive(false); Destroy(previous); }
            equipped[item.slot] = candidate;
            candidate.SetActive(true);
            return true;
        }

        public bool Unequip(string slot)
        {
            if (slot == null || !equipped.TryGetValue(slot, out GameObject instance)) return false;
            equipped.Remove(slot);
            if (instance) { instance.SetActive(false); Destroy(instance); }
            return true;
        }

        private void OnDestroy() { equipped.Clear(); }
    }
}
