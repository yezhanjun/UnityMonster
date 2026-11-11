using System;
using System.IO;
using AvatarPortability;
using AvatarPortability.ExpressionClips;
using AvatarPortability.Wardrobe;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class PortableCharacterSetup
    {
        static readonly string[] Sources =
        {
            "Assets/Characters/Wendy/Character/Wendy Current Default.prefab",
            "Assets/Characters/Shinano/Character/Shinano Current Default.prefab",
            "Assets/Characters/Sapphy/Presets/RinN008/00.prefab",
            "Assets/Characters/Milltina/Character/Milltina FC Portable.prefab"
        };

        [MenuItem("TPSShoot/Characters/Prepare and Validate Portable Characters")]
        public static void Prepare()
        {
            Directory.CreateDirectory("Assets/Resources/PortableCharacters");
            AssetDatabase.Refresh();
            for (int i = 0; i < Sources.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Sources[i]);
                if (!prefab) throw new InvalidOperationException("Missing character: " + Sources[i]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                try
                {
                    instance.transform.localScale = Vector3.one;
                    var animator = instance.GetComponent<Animator>();
                    if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                        throw new InvalidOperationException("Invalid root Humanoid Avatar: " + Sources[i]);
                    animator.runtimeAnimatorController = null;
                    animator.applyRootMotion = false;
                    animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                    foreach (var ui in instance.GetComponentsInChildren<WendyExpressionClipUI>(true))
                    {
                        ui.buildOnEnable = false;
                        ui.enabled = false;
                    }
                    foreach (var skin in instance.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                        skin.updateWhenOffscreen = true;
                    string output = "Assets/Resources/" + RangeCharacterSelection.ResourcePath(RangeCharacterSelection.FirstPortable + i) + ".prefab";
                    PrefabUtility.SaveAsPrefabAsset(instance, output);
                    Validate(instance, Sources[i]);
                    Debug.Log("PORTABLE_CHARACTER_READY " + output + " skins=" + instance.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length +
                        " physics=" + instance.GetComponentsInChildren<PortableBonePhysics>(true).Length +
                        " colliders=" + instance.GetComponentsInChildren<PortableBoneCollider>(true).Length +
                        " followers=" + instance.GetComponentsInChildren<WardrobePoseFollower>(true).Length +
                        " dependencies=" + AssetDatabase.GetDependencies(Sources[i], true).Length);
                }
                finally { UnityEngine.Object.DestroyImmediate(instance); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("PORTABLE_CHARACTERS_PREPARE_SUCCEEDED");
        }

        static void Validate(GameObject root, string label)
        {
            foreach (var node in root.GetComponentsInChildren<Transform>(true))
                if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(node.gameObject) != 0)
                    throw new InvalidOperationException("Missing script: " + label + "/" + node.name);
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (!skin.sharedMesh) throw new InvalidOperationException("Missing mesh: " + skin.name);
                if (!skin.rootBone || !skin.rootBone.IsChildOf(root.transform))
                    throw new InvalidOperationException("Invalid root bone: " + skin.name);
                var bones = skin.bones;
                foreach (var weight in skin.sharedMesh.GetAllBoneWeights())
                    if (weight.weight > 0 && (weight.boneIndex >= bones.Length || !bones[weight.boneIndex] || !bones[weight.boneIndex].IsChildOf(root.transform)))
                        throw new InvalidOperationException("Invalid weighted bone: " + skin.name);
                foreach (var material in skin.sharedMaterials)
                    if (!material || !material.shader || material.shader.name == "Hidden/InternalErrorShader")
                        throw new InvalidOperationException("Invalid material/shader: " + skin.name);
            }
            foreach (var physics in root.GetComponentsInChildren<PortableBonePhysics>(true))
            {
                if (physics.rootTransform && !physics.rootTransform.IsChildOf(root.transform))
                    throw new InvalidOperationException("External physics root: " + physics.name);
                foreach (var collider in physics.colliders)
                    if (collider && !collider.transform.IsChildOf(root.transform))
                        throw new InvalidOperationException("Invalid physics collider: " + physics.name);
            }
        }

        public static void PrepareAndBuild()
        {
            Prepare();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Baseline/Scenes/Startup.unity");
            SteamTestBuild.Build();
        }

        public static void RunSmoke()
        {
            Prepare();
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Baseline/Scenes/Startup.unity");
            EditorApplication.EnterPlaymode();
        }
    }
}
