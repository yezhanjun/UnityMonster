using System;
using UnityEngine;

namespace AvatarPortability.Wardrobe
{
    [CreateAssetMenu(menuName = "Avatar Portability/Wardrobe Item")]
    public sealed class WardrobeItem : ScriptableObject
    {
        public string slot = "Outfit";
        public GameObject prefab;
        [Tooltip("For pre-fitted, avatar-specific meshes only. Bind skin bones directly to the avatar; no clothing pose follower or independent physics is allowed.")]
        public bool bindSkinDirectlyToAvatar;
        [Tooltip("For retained clothing skeletons: preserve each bone's initial world rotation relative to its target, so different authored bone axes are not snapped together. Does not affect direct skin binding.")]
        public bool preserveRestRotationOffsets;
        [Tooltip("Paths are relative to the prefab / avatar root. Empty means the root itself.")]
        public string sourceSkeletonPath = "Armature";
        public string targetSkeletonPath = "Armature";
        public bool useHumanoidMapping = true;
        [Tooltip("Only unique exact names are eligible. Ambiguous names fail validation.")]
        public bool allowUniqueNameFallback;
        [Tooltip("Required unmapped bones cause equip to fail, preserving the existing slot.")]
        public string[] requiredSourcePaths = { "Armature/Hips", "Armature/Hips/Spine" };
        [Tooltip("Do not drive these bones or their descendants. Use for clothing physics branches.")]
        public string[] preservedSourcePaths = Array.Empty<string>();
        public BoneOverride[] overrides = Array.Empty<BoneOverride>();

        [Tooltip("Optional reference-pose rotations for specifically fitted outfits. Prevents equip during an animation from being mistaken for rest pose. Empty preserves existing behaviour.")]
        public RestRotationOverride[] restRotationOverrides = Array.Empty<RestRotationOverride>();
        [Serializable] public struct RestRotationOverride { public string sourcePath, targetPath; public Quaternion rotation; }

        [Serializable]
        public struct BoneOverride
        {
            public string sourcePath;
            public string targetPath;
        }
    }

}
