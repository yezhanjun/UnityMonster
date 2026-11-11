using System;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class SpiderRegularAssetValidation
    {
        const string Folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/SpiderRegular/";

        [MenuItem("Tools/Jotunnslayer/Validate Spider Regular Assets")]
        static void Validate()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "spider_sur_VarRegular.fbx");
            Require(model, "Spider model did not import.");
            var animator = model.GetComponent<Animator>();
            Require(animator && animator.avatar && animator.avatar.isValid && !animator.avatar.isHuman,
                "Spider requires a valid Generic Avatar on the root Animator.");
            var skin = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Require(skin && skin.sharedMesh && skin.bones.Length > 0 &&
                skin.bones.Length == skin.sharedMesh.bindposeCount, "Spider has invalid skin bindings.");

            Require(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "spiderling_dm.png"),
                "Spider albedo did not import.");
            var normal = AssetImporter.GetAtPath(Folder + "spiderling_nm.png") as TextureImporter;
            Require(normal && normal.textureType == TextureImporterType.NormalMap,
                "Spider normal texture must be imported as a normal map.");

            ValidateClip(model.transform, "spiderling_summon_ground");
            ValidateClip(model.transform, "spiderling_stand");
            ValidateClip(model.transform, "spiderling_walk");
            ValidateClip(model.transform, "spiderling_attack_03_nofire");
            ValidateClip(model.transform, "spiderling_hit_01");
            ValidateClip(model.transform, "spiderling_death_front");
            Debug.Log("Spider Regular asset validation passed: Generic avatar, skin and six bound motions.", model);
        }

        static void ValidateClip(Transform root, string name)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name + ".anim");
            Require(clip && !clip.legacy && !clip.humanMotion && clip.length > 0f,
                $"Spider motion {name} must be a nonempty Generic clip.");

            bool hasBoneBinding = false;
            foreach (var binding in AnimationUtility.GetCurveBindings(clip))
            {
                if (binding.type != typeof(Transform) || string.IsNullOrEmpty(binding.path)) continue;
                Require(root.Find(binding.path), $"Spider motion {name} cannot find {binding.path}.");
                hasBoneBinding = true;
            }
            Require(hasBoneBinding, $"Spider motion {name} is missing bone curves.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
