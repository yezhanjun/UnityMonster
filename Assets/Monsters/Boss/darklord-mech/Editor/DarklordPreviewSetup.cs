using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DarklordPreview.Editor
{
    public static class DarklordPreviewSetup
    {
        public const string Root = "Assets/Monsters/Boss/darklord-mech";
        public const string Model = Root + "/source/Darklord eternal.fbx";
        public const string Scene = Root + "/SwordShieldPreview/Darklord_SwordShieldPreview.unity";
        public const string AnimationRoot = "Assets/AssetStore/Sword and Shield Animation Pack/Animation/Humanoid";
        public const string PaladinRoot = "Assets/AssetStore/Paladin_Anim_Set/Animations";
        static readonly string[] Featured = { "Idle_Combat", "Walk_Combat_Loop_F_0", "Run_Combat_Loop_F_0", "Combo_Attack_01_All", "Combo_Attack_02_All", "Combo_Attack_03_All", "Combo_Attack_04_All", "Block_Loop", "Block_Hit", "Dodge_Combat_F_0", "Roll_Combat_F_0", "Hit_Combat_F", "Turn_Combat_180_R", "Idle" };

        [MenuItem("Tools/Darklord/Build Sword and Shield Preview")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Directory.CreateDirectory(Root + "/SwordShieldPreview");
            AssetDatabase.Refresh();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Model));
            model.name = "Darklord Sword and Shield";
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.fieldOfView = 36;
            camera.nearClipPlane = .05f;
            camera.backgroundColor = new Color(.065f, .08f, .11f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            var preview = Configure(model.GetComponent<Animator>(), camera);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Preview Ground";
            floor.transform.localScale = Vector3.one * 3;
            string materialPath = Root + "/SwordShieldPreview/Ground.mat";
            var ground = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
            if (!ground) { ground = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(ground, materialPath); }
            ground.SetColor("_BaseColor", new Color(.13f, .16f, .2f));
            EditorUtility.SetDirty(ground);
            floor.GetComponent<Renderer>().sharedMaterial = ground;
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 2; light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(40, -35, 0);
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .7f; fill.transform.rotation = Quaternion.Euler(15, 150, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), Scene);
            AssetDatabase.SaveAssets();
            Debug.Log("DARKLORD_PREVIEW_READY clips=" + preview.clips.Length + " existingAvatar=" + preview.animator.avatar.name);
        }

        [MenuItem("Tools/Darklord/Add Preview to Current Scene")]
        public static void AddToCurrentScene()
        {
            var animator = UnityEngine.Object.FindObjectsByType<Animator>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault(a => PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(a.gameObject) == Model);
            if (!animator) throw new InvalidOperationException("No Darklord model instance found in the current scene.");
            if (UnityEngine.Object.FindFirstObjectByType<DarklordActionPreview>()) return;
            Configure(animator, Camera.main);
            EditorSceneManager.MarkSceneDirty(animator.gameObject.scene);
        }

        static DarklordActionPreview Configure(Animator animator, Camera camera)
        {
            if (!animator || !animator.avatar || !animator.avatar.isValid || !animator.avatar.isHuman)
                throw new InvalidOperationException("The existing Darklord Humanoid Avatar is invalid.");
            var preview = new GameObject("Darklord Animation Preview Controls").AddComponent<DarklordActionPreview>();
            PopulateClips(preview);
            preview.animator = animator;
            preview.previewCamera = camera;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var renderers = animator.GetComponentsInChildren<Renderer>(true);
            var body = renderers.Where(r => r.name == "38_armor003_1_0_0").FirstOrDefault();
            Bounds bounds = body ? body.bounds : renderers[0].bounds;
            foreach (var renderer in renderers) bounds.Encapsulate(renderer.bounds);
            preview.pivotHeight = bounds.center.y - animator.transform.position.y;
            if (camera)
            {
                float distance = Mathf.Max(4, bounds.size.y * 1.9f);
                Vector3 pivot = animator.transform.position + Vector3.up * preview.pivotHeight;
                camera.transform.position = pivot + new Vector3(distance * .45f, distance * .13f, distance * .9f);
                camera.transform.LookAt(pivot);
            }
            return preview;
        }

        public static void PopulateClips(DarklordActionPreview preview)
        {
            if (!preview) return;
            preview.RefreshEditorClips();
            if (preview.clips.Length == 0) throw new InvalidOperationException("No Humanoid preview animations found.");
            EditorUtility.SetDirty(preview);
        }
        [MenuItem("Tools/Darklord/Refresh Preview Animation Lists")]
        public static void RefreshPreviewScenes()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            foreach (string path in new[] { Scene, BossPreview.Editor.BossPreviewSetup.ScenePath, "Assets/Scenes/SampleScene.unity" })
            {
                if (!File.Exists(path)) continue;
                var scene = EditorSceneManager.OpenScene(path);
                foreach (var root in scene.GetRootGameObjects())
                    foreach (var preview in root.GetComponentsInChildren<DarklordActionPreview>(true)) PopulateClips(preview);
                EditorSceneManager.SaveScene(scene);
            }
        }

        public static void BatchBuild()
        {
            try
            {
                Build();
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
                AddToCurrentScene();
                EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                EditorApplication.Exit(0);
            }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        [MenuItem("Tools/Darklord/Fix Avatar Head Mapping")]
        public static void FixHeadMapping()
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(Model);
            var description = importer.humanDescription;
            var bones = description.human;
            var corrected = new System.Collections.Generic.Dictionary<string, string>
            {
                { "Neck", "head neck lower" }, { "Head", "head face" },
                { "LeftEye", "head eyeball left" }, { "RightEye", "head eyeball right" },
                { "Jaw", "head jaw" }
            };
            for (int i = 0; i < bones.Length; i++)
                if (corrected.TryGetValue(bones[i].humanName, out string name))
                    bones[i] = new HumanBone { humanName = bones[i].humanName, boneName = name, limit = new HumanLimit { useDefaultValues = true } };
            description.human = bones;
            importer.humanDescription = description;
            importer.SaveAndReimport();
            var avatar = AssetDatabase.LoadAssetAtPath<GameObject>(Model).GetComponent<Animator>().avatar;
            if (!avatar || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Corrected head mapping produced an invalid Avatar.");
            Debug.Log("DARKLORD_HEAD_MAPPING_FIXED Head=head face Neck=head neck lower Eyes=head eyeball left/right Jaw=head jaw");
        }

        public static void BatchFixHeadAndBuild()
        {
            try { FixHeadMapping(); BatchBuild(); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
