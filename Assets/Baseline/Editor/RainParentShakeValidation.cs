using System;
using System.IO;
using System.Linq;
using MTPSKIT.Gameplay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using Random = UnityEngine.Random;

namespace MTPSKIT.BuildTools
{
    [InitializeOnLoad]
    public static class RainParentShakeValidation
    {
        const string Folder = "Assets/RainSource/ParentEffects/";
        const string Evidence = "Docs/玩法参考设计/雨中冒险2/原版依据/";
        const string Active = "RainParentShake.Active", Deadline = "RainParentShake.Deadline";
        static RainParentShakeEmitter timed, walkShake;
        static GameObject model;
        static PlayableGraph graph;
        static int phase;
        static float started, pausedAt, pausedStopwatch;
        static Vector3 pausedOffset;

        static RainParentShakeValidation()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use isolated batch project.");
            try
            {
                PrepareAndCompare();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, Folder + "ParentShakeValidation.unity");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + "ParentShakeValidation.unity");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 90);
                EditorApplication.update -= Tick; EditorApplication.update += Tick;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception e) { Fail(e); }
        }

        static void Configure(RainParentShakeEmitter emitter, JToken data)
        {
            emitter.shakeOnStart = (int)data["shakeOnStart"] != 0;
            emitter.shakeOnEnable = (int)data["shakeOnEnable"] != 0;
            emitter.shakeOnDelay = (int)data["shakeOnDelay"] != 0;
            emitter.startDelay = (float)data["startDelay"];
            emitter.amplitude = (float)data["wave"]["amplitude"];
            emitter.frequency = (float)data["wave"]["frequency"];
            emitter.cycleOffset = (float)data["wave"]["cycleOffset"];
            emitter.duration = (float)data["duration"]; emitter.radius = (float)data["radius"];
            emitter.scaleShakeRadiusWithLocalScale = (int)data["scaleShakeRadiusWithLocalScale"] != 0;
            emitter.amplitudeTimeDecay = (int)data["amplitudeTimeDecay"] != 0;
        }

        static void PrepareAndCompare()
        {
            var effects = JObject.Parse(File.ReadAllText(Evidence + "Parent原生粒子校验源.json"))["effects"];
            int configurations = 0, samples = 0;
            foreach (var effect in effects)
            {
                var script = effect["omittedComponents"].FirstOrDefault(c => (string)c["scriptClass"]?["m_ClassName"] == "ShakeEmitter");
                if (script == null) continue;
                string name = (string)effect["name"];
                var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + "-DistortionPartial.prefab"));
                root.SetActive(false);
                try
                {
                    var emitter = root.AddComponent<RainParentShakeEmitter>();
                    Configure(emitter, script["data"]);
                    CompareOriginal(emitter, ref samples);
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + name + "-ShakePartial.prefab");
                    configurations++;
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            var dustSource = JObject.Parse(File.ReadAllText(Evidence + "Parent动画接线依据.json"))["footstepDust"]["hierarchy"]
                .Single(c => (string)c["script"]?["data"]?["m_ClassName"] == "ShakeEmitter")["data"];
            var dust = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "GenericHugeFootstepDust-Native.prefab"));
            dust.SetActive(false);
            try
            {
                var emitter = dust.AddComponent<RainParentShakeEmitter>(); Configure(emitter, dustSource);
                CompareOriginal(emitter, ref samples);
                // FootstepHandler instantiates this persistent prefab directly; original dust root is active.
                dust.SetActive(true);
                PrefabUtility.SaveAsPrefabAsset(dust, Folder + "GenericHugeFootstepDust-ShakePartial.prefab");
                configurations++;
            }
            finally { UnityEngine.Object.DestroyImmediate(dust); }
            var model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Parent-FootstepPartial.prefab"));
            model.SetActive(false);
            try
            {
                model.GetComponent<RainParentFootstepDust>().dustPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "GenericHugeFootstepDust-ShakePartial.prefab").GetComponent<ParticleSystem>();
                PrefabUtility.SaveAsPrefabAsset(model, Folder + "Parent-FootstepShakePartial.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(model); }
            AssetDatabase.SaveAssets();
            Require(configurations == 5, "Expected five original shake configurations.");
            Debug.Log("RAIN_PARENT_SHAKE_ORIGINAL_MATH_CHECKED configurations=5 samples=" + samples + " high_frequency_single_step=True random_state_restored=True");
        }

        static void CompareOriginal(RainParentShakeEmitter emitter, ref int samples)
        {
            var originalRandom = Random.state;
            try
            {
                foreach (bool fade in new[] { true, false })
                {
                    emitter.amplitudeTimeDecay = fade;
                    Random.InitState(6151);
                    Vector3 vector = Random.onUnitSphere;
                    var referenceRandom = Random.state;
                    float half = .5f / emitter.frequency, age = 0;
                    Random.InitState(6151); emitter.StartShake();
                    var actualRandom = Random.state;
                    foreach (float dt in new[] { .0007f, .003f, .016f, .04f, .06f, .2f })
                    {
                        age += dt; Vector3 expected = Vector3.zero;
                        if (age < emitter.duration)
                        {
                            half -= dt;
                            if (half < 0)
                            {
                                Random.state = referenceRandom;
                                vector = Vector3.Slerp(Random.onUnitSphere, -vector, .5f);
                                referenceRandom = Random.state;
                                half += .5f / emitter.frequency;
                            }
                            expected = vector * (Mathf.Sin(2 * Mathf.PI * (emitter.frequency * half + emitter.cycleOffset)) * emitter.amplitude)
                                * (fade ? 1 - age / emitter.duration : 1);
                        }
                        Random.state = actualRandom; emitter.ManualUpdate(dt); actualRandom = Random.state;
                        Require(Vector3.Distance(expected, emitter.CurrentOffset) < .00003f && Mathf.Abs(age - emitter.Stopwatch) < .000001f,
                            "Original random/wave/decay mismatch at frequency " + emitter.frequency);
                        samples++;
                    }
                }
                emitter.amplitudeTimeDecay = true;
                emitter.ResetShake();
            }
            finally { Random.state = originalRandom; }
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0)) throw new TimeoutException("Shake validation timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (phase == 0)
                {
                    CheckSpatialAndCamera();
                    var go = new GameObject("Scaled time shake"); go.SetActive(false);
                    timed = go.AddComponent<RainParentShakeEmitter>(); timed.shakeOnStart = false;
                    timed.duration = .6f; timed.frequency = 2; timed.cycleOffset = .25f;
                    go.SetActive(true); timed.StartShake(); started = Time.time; phase = 1;
                }
                else if (phase == 1 && Time.time - started > .1f)
                {
                    pausedStopwatch = timed.Stopwatch; pausedOffset = timed.CurrentOffset;
                    Time.timeScale = 0; pausedAt = Time.realtimeSinceStartup; phase = 2;
                }
                else if (phase == 2 && Time.realtimeSinceStartup - pausedAt > .25f)
                {
                    Require(timed.Stopwatch == pausedStopwatch && timed.CurrentOffset == pausedOffset, "Shake advanced while scaled time paused.");
                    Time.timeScale = 1; phase = 3;
                }
                else if (phase == 3 && Time.time - started > .7f)
                {
                    Require(timed.CurrentOffset == Vector3.zero, "Shake did not expire.");
                    timed.StartShake(); timed.ManualUpdate(.03f);
                    Require(timed.CurrentOffset.sqrMagnitude > .000001f, "Restart did not produce displacement.");
                    timed.enabled = false;
                    Require(timed.CurrentOffset == Vector3.zero && RainParentShakeEmitter.ComputeTotalShakeAtPoint(Vector3.zero) == Vector3.zero,
                        "Disabled shake retained global displacement.");
                    timed.enabled = true;
                    Require(RainParentShakeEmitter.ComputeTotalShakeAtPoint(Vector3.zero) == Vector3.zero, "Re-enable restored stale offset.");
                    UnityEngine.Object.DestroyImmediate(timed.gameObject);
                    Debug.Log("RAIN_PARENT_SHAKE_LIFECYCLE_CHECKED scaled_pause=True expiry=True restart=True disable_unregister=True");
                    BeginWalk(); phase = 4;
                }
                else if (phase == 4)
                {
                    if (!walkShake) walkShake = model.GetComponentInChildren<RainParentShakeEmitter>();
                    if (walkShake && walkShake.Stopwatch < .15f && walkShake.CurrentOffset.sqrMagnitude > .0000001f)
                    {
                        Require(model.GetComponentsInChildren<ParticleSystem>().Length == 1, "Footstep shake created extra particle instances.");
                        Vector3 sample = RainParentShakeEmitter.ComputeTotalShakeAtPoint(walkShake.transform.position);
                        Require(sample == walkShake.CurrentOffset, "Original Footstep event did not register owned dust shake.");
                        graph.Destroy(); UnityEngine.Object.Destroy(model); phase = 5;
                    }
                    else if (Time.time - started > 1.1f) throw new InvalidOperationException("Actual original walk event did not start dust shake.");
                }
                else if (phase == 5)
                {
                    Require(!model && !walkShake && RainParentShakeEmitter.ComputeTotalShakeAtPoint(Vector3.zero) == Vector3.zero,
                        "Destroyed model leaked registered shake.");
                    Debug.Log("RAIN_PARENT_SHAKE_PLAY_PASSED original_walk_event=True dust_owned_source=True destroy_unregister=True production_camera_pending=True");
                    Finish(0);
                }
            }
            catch (Exception e) { Fail(e); }
        }

        static void CheckSpatialAndCamera()
        {
            var first = new GameObject("Shake spatial A"); first.SetActive(false);
            var a = first.AddComponent<RainParentShakeEmitter>(); a.shakeOnStart = false;
            a.amplitude = 6; a.frequency = 2; a.cycleOffset = .25f; a.radius = 40;
            first.SetActive(true); a.StartShake(); a.ManualUpdate(.03f);
            Vector3 center = a.CurrentOffset;
            Require(center.sqrMagnitude > .01f, "Spatial reference offset empty.");
            Require(Vector3.Distance(RainParentShakeEmitter.ComputeTotalShakeAtPoint(Vector3.right * 20), center * .5f) < .000001f
                && RainParentShakeEmitter.ComputeTotalShakeAtPoint(Vector3.right * 40) == Vector3.zero, "Original radius falloff differs.");
            var second = new GameObject("Shake spatial B"); second.SetActive(false);
            var b = second.AddComponent<RainParentShakeEmitter>(); b.shakeOnStart = false;
            second.SetActive(true); b.StartShake(); b.ManualUpdate(.03f);
            Require(Vector3.Distance(RainParentShakeEmitter.ComputeTotalShakeAtPoint(Vector3.zero), center + b.CurrentOffset) < .000001f,
                "Multiple emitter sum differs.");
            second.SetActive(false);
            Require(RainParentShakeEmitter.ResolveCameraPosition(Vector3.zero, 0, .1f, 1) == Vector3.zero, "Disabled screen shake scale moves camera.");
            Require(Vector3.Distance(RainParentShakeEmitter.ResolveCameraPosition(Vector3.zero, 1, .1f, 0), center) < .000001f,
                "Unblocked camera displacement differs.");
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = center.normalized * (center.magnitude * .5f);
            wall.transform.rotation = Quaternion.LookRotation(center);
            wall.transform.localScale = new Vector3(20, 20, .1f);
            Physics.SyncTransforms();
            Vector3 constrained = RainParentShakeEmitter.ResolveCameraPosition(Vector3.zero, 1, .1f, 1);
            Require(constrained.magnitude < center.magnitude && constrained.magnitude > 0, "Camera near-plane sphere cast failed to constrain shake.");
            wall.GetComponent<Collider>().isTrigger = true; Physics.SyncTransforms();
            Require(Vector3.Distance(RainParentShakeEmitter.ResolveCameraPosition(Vector3.zero, 1, .1f, 1), center) < .000001f,
                "Trigger constrained original Ignore camera sweep.");
            UnityEngine.Object.DestroyImmediate(wall); UnityEngine.Object.DestroyImmediate(first); UnityEngine.Object.DestroyImmediate(second);
            Debug.Log("RAIN_PARENT_SHAKE_SPATIAL_CHECKED radius_edge=True half_distance=True sum=True camera_scale=True near_plane_sweep=True trigger_ignore=True");
        }

        static void BeginWalk()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ground.transform.position = new Vector3(0, -.5f, 0); ground.transform.localScale = new Vector3(100, 1, 100);
            model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Parent-FootstepShakePartial.prefab"));
            model.SetActive(true); Physics.SyncTransforms();
            graph = PlayableGraph.Create("Original Parent footstep shake event"); graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "ParentArmature_WalkForward-FootstepsOnly.anim");
            var playable = AnimationClipPlayable.Create(graph, clip);
            AnimationPlayableOutput.Create(graph, "Parent", model.GetComponent<Animator>()).SetSourcePlayable(playable);
            graph.Play(); started = Time.time;
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Fail(Exception error) { Debug.LogException(error); Debug.LogError("RAIN_PARENT_SHAKE_FAILED"); Finish(1); }
        static void Finish(int code)
        {
            if (graph.IsValid()) graph.Destroy(); Time.timeScale = 1;
            SessionState.SetBool(Active, false); EditorApplication.update -= Tick;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
