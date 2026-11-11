#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using AvatarPortability;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Runs only with an explicit command-line flag, through the normal training host.
    public sealed class PortableCharacterSmokeTest : MonoBehaviour
    {
        bool failed;
        float deadline;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "--portable-character-smoke") < 0) return;
            var root = new GameObject("Portable character integration validation");
            DontDestroyOnLoad(root);
            root.AddComponent<PortableCharacterSmokeTest>();
        }

        void OnLog(string message, string stack, LogType type)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null &&
                message.Contains("Global Illumination requires a graphics device")) return;
            if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) failed = true;
        }

        void Update()
        {
            if (Time.realtimeSinceStartup > deadline) { Debug.LogError("PORTABLE_SMOKE_TIMEOUT"); Finish(); }
        }

        IEnumerator Start()
        {
            deadline = Time.realtimeSinceStartup + 240;
            Application.logMessageReceived += OnLog;
            yield return null;
            var creator = FindFirstObjectByType<RoomCreator>();
            int map = creator ? Array.FindIndex(creator.Maps, item => MTPSKIT.RoomCreator.IsTrainingScene(item.Scene)) : -1;
            if (!Check(map >= 0, "Missing training configuration")) { Finish(); yield break; }
            creator.LaunchFromFrontEnd(map, 12, false);
            float spawnDeadline = Time.realtimeSinceStartup + 40;
            while ((!NetworkClient.localPlayer || !NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter) && Time.realtimeSinceStartup < spawnDeadline)
                yield return null;
            var player = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerInstance>() : null;
            if (!Check(player && player.MyCharacter, "Training player did not spawn")) { Finish(); yield break; }
            var owner = player.MyCharacter;
            if (PlayerGameplayInput.Instance) PlayerGameplayInput.Instance.enabled = false;
            yield return new WaitForSeconds(1);
            Vector3 spawnPosition = owner.transform.position;
            for (int round = 0; round < 2; round++)
            {
                for (int selection = 12; selection <= 15; selection++)
                {
                    owner.SetMovementInput(Vector2.zero);
                    owner.transform.position = spawnPosition;
                    if (!Check(player.ServerSetRangeHero(selection), "Selection rejected " + selection + " health=" + owner.CurrentHealth)) continue;
                    yield return null;
                    yield return new WaitForSeconds(.4f);
                    var visual = owner.GetComponent<RangeRobotVisual>();
                    if (!Check(visual && visual.Selection == selection && visual.DisplayAnimator, "Missing selected visual " + selection)) continue;
                    var model = visual.DisplayAnimator.gameObject;
                    int skinCount = model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length;
                    var solvers = model.GetComponentsInChildren<PortableBonePhysics>(true);
                    Check(model.transform.localScale == Vector3.one, "Author scale changed " + selection);
                    Check(solvers.Length > 0, "No portable physics " + selection);
                    Check(owner.GetComponentsInChildren<RangeRobotVisual>(true).Length == 1, "Duplicate visual controller");
                    owner.SetMovementInput(Vector2.up);
                    for (int frame = 0; frame < 60; frame++) yield return null;
                    owner.SetMovementInput(Vector2.zero);
                    owner.transform.position += Vector3.right * 3;
                    yield return null;
                    for (int frame = 0; frame < 15; frame++) yield return null;
                    model.SetActive(false);
                    model.SetActive(true);
                    owner.Client_Resurrect?.Invoke(owner.CurrentHealth);
                    for (int frame = 0; frame < 15; frame++) yield return null;
                    Check(model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == skinCount, "Clothing count changed " + selection);
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        if (!skin.gameObject.activeInHierarchy || !skin.enabled) continue;
                        Check(skin.sharedMesh && skin.bounds.size.sqrMagnitude < 10000 && Finite(skin.bounds.center), "Invalid skin bounds " + skin.name);
                    }
                    foreach (var solver in solvers)
                        if (solver.isActiveAndEnabled) Check(Finite(solver.transform.position), "Invalid physics pose " + solver.name);
                    if (round == 0)
                    {
                        yield return Capture(visual.DisplayAnimator, selection, "front", Vector3.forward);
                        yield return Capture(visual.DisplayAnimator, selection, "side", Vector3.right);
                        yield return Capture(visual.DisplayAnimator, selection, "back", Vector3.back);
                        var ragdoll = HumanoidRagdollBuilder.Create(visual.DisplayAnimator, owner.transform);
                        Check(ragdoll.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length == skinCount, "Ragdoll lost clothing " + selection);
                        for (int frame = 0; frame < 15; frame++) yield return null;
                        Destroy(ragdoll.gameObject);
                    }
                    Debug.Log("PORTABLE_SMOKE_CHARACTER selection=" + selection + " round=" + round + " skins=" + skinCount + " solvers=" + solvers.Length);
                    owner.transform.position = spawnPosition;
                }
            }
            Check(player.ServerSetRangeHero(0), "Soldier restore rejected");
            yield return null;
            Check(!owner.GetComponent<RangeRobotVisual>(), "Portable visual survived soldier restore");
            Check(player.ServerSetRangeHero(1), "FAF restore rejected");
            yield return null;
            Check(owner.GetComponent<RangeRobotVisual>().DisplayAnimator, "FAF restore failed");
            Finish();
        }

        static bool Finite(Vector3 value) => float.IsFinite(value.x) && float.IsFinite(value.y) && float.IsFinite(value.z);

        bool Check(bool condition, string message)
        {
            if (!condition) Debug.LogError("PORTABLE_SMOKE " + message);
            return condition;
        }

        static IEnumerator Capture(Animator model, int selection, string label, Vector3 direction)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) yield break;
            string directory = Path.GetFullPath("Logs/PortableCharacters");
            Directory.CreateDirectory(directory);
            var cameraObject = new GameObject("Portable validation camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.depth = 1000;
            camera.fieldOfView = 35;
            camera.nearClipPlane = .03f;
            camera.farClipPlane = 100;
            camera.backgroundColor = new Color(.16f, .19f, .23f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            var feet = model.GetBoneTransform(HumanBodyBones.LeftFoot).position;
            var head = model.GetBoneTransform(HumanBodyBones.Head).position;
            float height = Mathf.Max(.6f, head.y - feet.y + .3f);
            Vector3 center = (head + feet) * .5f;
            camera.transform.position = center + model.transform.TransformDirection(direction) * height * 2.1f;
            camera.transform.LookAt(center);
            try
            {
                for (int frame = 0; frame < 5; frame++) yield return null;
                ScreenCapture.CaptureScreenshot(Path.Combine(directory, selection + "-" + label + ".png"));
                for (int frame = 0; frame < 5; frame++) yield return null;
            }
            finally
            {
                Destroy(cameraObject);
            }
        }

        void Finish()
        {
            Application.logMessageReceived -= OnLog;
            Debug.Log(failed ? "PORTABLE_SMOKE_FAILED" : "PORTABLE_SMOKE_PASSED");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.Exit(failed ? 1 : 0);
#else
            Application.Quit(failed ? 1 : 0);
#endif
        }
    }
}
#endif
