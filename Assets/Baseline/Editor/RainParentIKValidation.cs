using System;
using System.IO;
using System.Linq;
using System.Reflection;
using MTPSKIT.Gameplay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MTPSKIT.BuildTools
{
    [InitializeOnLoad]
    public static class RainParentIKValidation
    {
        const string Active = "RainParentIK.Active";
        const string Deadline = "RainParentIK.Deadline";
        const string Folder = "Assets/RainSource/ParentEffects/";
        const string Parent = "Assets/Resources/RainActors/RoR2/Base/Characters/Parent/";
        static GameObject model;
        static RainParentIKEventProbe probe;
        static RainParentIKTargetPassive slam;
        static AnimationClip clip;
        static PlayableGraph graph;
        static int phase;
        static float started, pausedAt, stableY;
        static readonly FieldInfo Offset = typeof(RainParentIKTargetPassive).GetField(
            "targetHeightOffset", BindingFlags.Instance | BindingFlags.NonPublic);

        static RainParentIKValidation()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
            Prepare();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            EditorSceneManager.SaveScene(scene, Folder + "ParentIKValidation.unity");
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + "ParentIKValidation.unity");
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 90f);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Prepare()
        {
            var source = JObject.Parse(File.ReadAllText("Docs/玩法参考设计/雨中冒险2/原版依据/Parent动画接线依据.json"));
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Parent + "mdlParent.prefab"));
            root.SetActive(false);
            try
            {
                int count = 0;
                foreach (var row in source["modelMonoBehaviours"])
                {
                    if ((string)row["script"]["data"]["m_ClassName"] != "IKTargetPassive") continue;
                    string path = (string)row["path"];
                    path = path.Substring(path.IndexOf("mdlParent/", StringComparison.Ordinal) + "mdlParent/".Length);
                    var target = root.transform.Find(path).gameObject.AddComponent<RainParentIKTargetPassive>();
                    var data = row["data"];
                    target.minHeight = (float)data["minHeight"];
                    target.maxHeight = (float)data["maxHeight"];
                    target.dampTime = (float)data["dampTime"];
                    target.baseOffset = (float)data["baseOffset"];
                    target.cacheFirstPosition = (int)data["cacheFirstPosition"] != 0;
                    target.selfPlant = (int)data["selfPlant"] != 0;
                    target.selfPlantFrequency = (float)data["selfPlantFrequency"];
                    target.animatorIKWeightFloat = (string)data["animatorIKWeightFloat"];
                    Require((long)data["animator"]["m_PathID"] == 0, "Unexpected source animator dependency.");
                    target.worldMask = 1;
                    count++;
                }
                Require(count == 3, "Expected three original passive IK targets.");
                var animator = root.AddComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                root.AddComponent<RainParentAnimationEvents>();
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "Parent-IKPartial.prefab");
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            string clipPath = Folder + "Parent-Slam-IKOnly.anim";
            var copied = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(Parent + "ParentArmature_Slam.anim"));
            copied.name = "Parent-Slam-IKOnly";
            var events = JObject.Parse(File.ReadAllText("Docs/玩法参考设计/雨中冒险2/原版依据/Parent动画事件.json"));
            var originalEvent = events["clips"].Single(c => (string)c["clip"] == "ParentArmature|Slam")["events"]
                .Single(e => (string)e["functionName"] == "RefreshAllIK");
            AssetDatabase.CreateAsset(copied, clipPath);
            AnimationUtility.SetAnimationEvents(copied, new[] {
                new AnimationEvent { functionName = "RefreshAllIK", time = (float)originalEvent["time"] }
            });
            EditorUtility.SetDirty(copied);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(clipPath, ImportAssetOptions.ForceSynchronousImport);
            copied = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            Require(AnimationUtility.GetAnimationEvents(copied).Length == 1 && copied.events.Length == 1,
                "Original event must survive serialization and reimport.");
            Debug.Log("RAIN_PARENT_IK_PREPARED targets=3 original_event_time=" + copied.events[0].time + " production_wiring=False");
        }

        static void StartClip()
        {
            if (graph.IsValid()) graph.Destroy();
            graph = PlayableGraph.Create("Original Parent Slam IK event");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var playable = AnimationClipPlayable.Create(graph, clip);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            AnimationPlayableOutput.Create(graph, "Parent", model.GetComponent<Animator>()).SetSourcePlayable(playable);
            graph.Play();
            started = Time.time;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0))
                    throw new TimeoutException("Parent IK validation timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (phase == 0)
                {
                    var ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ground.name = "IK ground Default layer";
                    ground.transform.position = new Vector3(0, -.1f, 0);
                    ground.transform.localScale = new Vector3(100, 1, 100); // top = .4
                    model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Parent-IKPartial.prefab"));
                    model.SetActive(true);
                    probe = model.AddComponent<RainParentIKEventProbe>();
                    slam = model.GetComponentsInChildren<RainParentIKTargetPassive>().Single(t => t.name == "SlamZone");
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "Parent-Slam-IKOnly.anim");
                    Physics.SyncTransforms();
                    CheckRayAndSmoothing();
                    StartClip();
                    phase = 1;
                }
                else if (phase == 1 && Time.time - started > .25f)
                {
                    Require(probe.count == 0, "Event ran before original trigger time.");
                    Time.timeScale = 0;
                    pausedAt = Time.realtimeSinceStartup;
                    phase = 2;
                }
                else if (phase == 2 && Time.realtimeSinceStartup - pausedAt > .3f)
                {
                    Require(probe.count == 0, "GameTime animation event advanced while paused.");
                    Time.timeScale = 1;
                    phase = 3;
                }
                else if (phase == 3 && Time.time - started > .95f)
                {
                    Require(probe.count == 1, "Original Slam event did not dispatch exactly once: count=" + probe.count);
                    Require(Mathf.Abs((float)Offset.GetValue(slam) - .4f) < .001f, "Event did not raycast cached SlamZone.");
                    Require(Mathf.Abs(slam.transform.position.y - .4f) < .002f, "Cached target did not settle on terrain.");
                    stableY = slam.transform.position.y;
                    phase = 4;
                }
                else if (phase == 4 && Time.time - started > 1.2f)
                {
                    Require(Mathf.Abs(slam.transform.position.y - stableY) < .002f, "Cached target accumulated offsets.");
                    Debug.Log("RAIN_PARENT_IK_ORIGINAL_SLAM_EVENT_CHECKED paused=True count=1 ground_y=0.4 no_cached_accumulation=True");
                    StartClip();
                    phase = 5;
                }
                else if (phase == 5 && Time.time - started > .95f)
                {
                    Require(probe.count == 2, "New playback did not refresh targets again.");
                    StartClip();
                    phase = 6;
                }
                else if (phase == 6 && Time.time - started > .2f)
                {
                    graph.Destroy();
                    started = Time.time;
                    phase = 7;
                }
                else if (phase == 7 && Time.time - started > .9f)
                {
                    Require(probe.count == 2, "Destroyed animation graph dispatched a stale callback.");
                    model.SetActive(false);
                    float before = slam.transform.position.y;
                    model.SetActive(true);
                    slam.UpdateIKTargetPosition();
                    slam.UpdateYOffset();
                    Require(Mathf.Abs(slam.transform.position.y - before) < .002f, "Re-enable lost the original cached position.");
                    Debug.Log("RAIN_PARENT_IK_PLAY_PASSED restart=True cancelled_graph=True reenable=True production_wiring=False");
                    Finish(0);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_IK_PLAY_FAILED");
                Finish(1);
            }
        }

        static void CheckRayAndSmoothing()
        {
            var root = new GameObject("Scaled cached IK math fixture");
            root.SetActive(false);
            root.transform.position = new Vector3(3, 2, 5);
            root.transform.localScale = new Vector3(2, 3, 4);
            var node = new GameObject("Target");
            node.transform.SetParent(root.transform, false);
            node.transform.localPosition = new Vector3(1, -.5f, 1);
            var target = node.AddComponent<RainParentIKTargetPassive>();
            target.cacheFirstPosition = true;
            root.SetActive(true);
            target.UpdateIKTargetPosition();
            Require(Mathf.Abs((float)Offset.GetValue(target) + .1f) < .001f, "Original world-space ray interval or terrain hit differs.");
            float velocity = 0;
            float expected = .5f + Mathf.SmoothDamp(0, -.1f, ref velocity, .01f, float.PositiveInfinity, Time.deltaTime);
            target.UpdateYOffset();
            Require(Mathf.Abs(node.transform.position.y - expected) < .001f, "Scaled-parent smoothing differs from original formula.");
            target.worldMask = 0;
            target.baseOffset = .25f;
            target.UpdateIKTargetPosition();
            Require(Mathf.Abs((float)Offset.GetValue(target) - .25f) < .0001f, "Miss must reset target offset before baseOffset.");
            UnityEngine.Object.DestroyImmediate(root);
            Debug.Log("RAIN_PARENT_IK_RAY_MATH_CHECKED hit=True mask_miss=True base_offset=True scaled_parent=True");
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        static void Finish(int code)
        {
            if (graph.IsValid()) graph.Destroy();
            Time.timeScale = 1;
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }

    public sealed class RainParentIKEventProbe : MonoBehaviour
    {
        public int count;
        public void RefreshAllIK() => count++;
    }
}
