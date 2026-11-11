using System;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class Skeleton1HAssetValidation
    {
        const string Folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/Skeleton1H/";
        const string Model = Folder + "skeleton_1h_sur.fbx";
        const string Normal = Folder + "skeleton_part1_nm.png";

        [MenuItem("Tools/Jotunnslayer/Validate Skeleton 1H Assets")]
        public static void Validate()
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(Model);
            Require(model, "Skeleton model did not import.");
            var animator = model.GetComponent<Animator>();
            Require(animator && animator.avatar && animator.avatar.isValid && animator.avatar.isHuman,
                "Skeleton requires a valid humanoid Avatar on the root Animator.");

            var renderer = model.GetComponentInChildren<SkinnedMeshRenderer>(true);
            Require(renderer && renderer.sharedMesh && renderer.sharedMesh.vertexCount > 0 &&
                renderer.bones.Length > 0 && renderer.bones.Length == renderer.sharedMesh.bindposeCount,
                "Skeleton requires a skinned mesh with a complete bone binding.");

            Require(AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "skeleton_part1_dm.png"),
                "Skeleton albedo did not import.");
            var normal = AssetImporter.GetAtPath(Normal) as TextureImporter;
            Require(normal && normal.textureType == TextureImporterType.NormalMap,
                "Skeleton normal texture must be imported as a normal map.");

            VerifyClip("skeleton_summon_ground", false);
            VerifyClip("1h_stand_combat", true);
            VerifyClip("1h_walk", true);
            VerifyClip("1h_attack_04_nofire", false);
            VerifyClip("1h_hit", false);
            Debug.Log("Skeleton 1H asset validation passed: humanoid avatar, skinned mesh, textures and five motions.", model);
        }

        static void VerifyClip(string name, bool loop)
        {
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + name + ".anim");
            Require(clip && !clip.legacy && clip.humanMotion && clip.length > 0f && clip.isLooping == loop,
                $"Skeleton motion {name} requires a nonempty humanoid clip with loop={loop}.");
        }

        static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
