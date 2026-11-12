using System;
using CyberSamurai;
using DarklordPreview;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace BossPreview.Editor
{
    public static class BossPreviewSetup
    {
        public const string ScenePath = "Assets/Monsters/Boss/BossSkillPreview.unity";
        const string KatanaScene = "Assets/Monsters/Boss/cyber-samurai/KatanaPreview/CyberSamurai_KatanaPreview.unity";
        const string SwordShieldScene = "Assets/Monsters/Boss/darklord-mech/SwordShieldPreview/Darklord_SwordShieldPreview.unity";

        [MenuItem("Tools/Boss/Build Unified Skill Preview")]
        public static void Build()
        {
            if (!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var destination = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var hub = new GameObject("Boss Preview Hub").AddComponent<BossPreviewHub>();
            var camera = new GameObject("Main Camera").AddComponent<Camera>();
            camera.tag = "MainCamera"; camera.fieldOfView = 36; camera.nearClipPlane = .05f;
            camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.065f, .08f, .11f);
            camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
            hub.previewCamera = camera;
            hub.characters = new[] { CopySwordShield(destination, camera), CopyKatana(destination) };
            foreach (var entry in hub.characters) entry.root.SetActive(false);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
            floor.name = "Preview Ground"; floor.transform.localScale = Vector3.one * 3;
            floor.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Monsters/Boss/darklord-mech/SwordShieldPreview/Ground.mat");
            var key = new GameObject("Key Light").AddComponent<Light>();
            key.type = LightType.Directional; key.intensity = 2; key.shadows = LightShadows.Soft; key.transform.rotation = Quaternion.Euler(40, -35, 0);
            var fill = new GameObject("Fill Light").AddComponent<Light>();
            fill.type = LightType.Directional; fill.intensity = .7f; fill.transform.rotation = Quaternion.Euler(15, 150, 0);
            RenderSettings.ambientMode = AmbientMode.Flat; RenderSettings.ambientLight = new Color(.45f, .45f, .45f);
            SceneManager.SetActiveScene(destination);
            camera.transform.SetPositionAndRotation(hub.characters[0].cameraPosition, hub.characters[0].cameraRotation);
            EditorSceneManager.SaveScene(destination, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log("BOSS_PREVIEW_READY characters=" + hub.characters.Length);
        }

        static BossPreviewHub.Character CopySwordShield(Scene destination, Camera camera)
        {
            var source = EditorSceneManager.OpenScene(SwordShieldScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(destination);
            var original = FindInScene<DarklordActionPreview>(source);
            var root = new GameObject("Darklord Preview"); root.SetActive(false);
            var model = UnityEngine.Object.Instantiate(original.animator.gameObject, root.transform);
            model.name = "Darklord";
            var controls = new GameObject("Sword and Shield Controls"); controls.transform.SetParent(root.transform, false);
            var preview = controls.AddComponent<DarklordActionPreview>(); EditorUtility.CopySerialized(original, preview);
            preview.animator = model.GetComponent<Animator>(); preview.previewCamera = camera;
            preview.paused = false; preview.autoCycle = false; preview.currentClip = 0;
            var sourceCamera = original.previewCamera;
            Vector3 pivot = preview.animator.transform.position + Vector3.up * preview.pivotHeight;
            var entry = new BossPreviewHub.Character { displayName = "Darklord", root = root, swordShield = preview,
                cameraPosition = pivot + (sourceCamera.transform.position - pivot) * 1.2f, cameraRotation = sourceCamera.transform.rotation };
            EditorSceneManager.CloseScene(source, true);
            return entry;
        }

        static BossPreviewHub.Character CopyKatana(Scene destination)
        {
            var source = EditorSceneManager.OpenScene(KatanaScene, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(destination);
            var original = FindInScene<SamuraiKatanaPreview>(source);
            var root = new GameObject("Cyber Samurai Preview"); root.SetActive(false);
            var model = UnityEngine.Object.Instantiate(original.animator.gameObject, root.transform); model.name = "Cyber Samurai";
            var controls = new GameObject("Katana Controls"); controls.transform.SetParent(root.transform, false);
            var preview = controls.AddComponent<SamuraiKatanaPreview>(); EditorUtility.CopySerialized(original, preview);
            preview.animator = model.GetComponent<Animator>(); preview.bootstrap = model.GetComponent<SamuraiClothBootstrap>(); preview.cloth = preview.bootstrap.cloth;
            preview.autoCycle = false; preview.currentState = 0;
            var sourceCamera = FindInScene<Camera>(source);
            var entry = new BossPreviewHub.Character { displayName = "Cyber Samurai", root = root, katana = preview,
                cameraPosition = sourceCamera.transform.position, cameraRotation = sourceCamera.transform.rotation };
            EditorSceneManager.CloseScene(source, true);
            return entry;
        }

        static T FindInScene<T>(Scene scene) where T : Component
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var component = root.GetComponentInChildren<T>(true);
                if (component) return component;
            }
            throw new InvalidOperationException("Missing " + typeof(T).Name + " in " + scene.path);
        }

        public static void BatchBuild()
        {
            try { Build(); EditorApplication.Exit(0); }
            catch (Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }
    }
}
