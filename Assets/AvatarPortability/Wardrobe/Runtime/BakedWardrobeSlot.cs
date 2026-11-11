using UnityEngine;

namespace AvatarPortability.Wardrobe
{
    /// <summary>Identifies an outfit saved into the avatar prefab in Edit mode.</summary>
    [DisallowMultipleComponent]
    public sealed class BakedWardrobeSlot : MonoBehaviour
    {
        public WardrobeItem item;
        public string slot;
    }
}
