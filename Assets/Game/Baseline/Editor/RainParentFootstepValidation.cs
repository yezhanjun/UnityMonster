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

namespace MTPSKIT.BuildTools
{
    [InitializeOnLoad]
    public static class RainParentFootstepValidation
    {
        const string Active = "RainParentFootstep.Active";
        const string Deadline = "RainParentFootstep.Deadline";
        const string Folder = "Assets/RainSource/ParentEffects/";
        const string Parent = "Assets/Resources/RainActors/RoR2/Base/Characters/Parent/";
        const string Evidence = "Docs/玩法参考设计/雨中冒险2/原版依据/";
        static GameObject model, ground, water;
        static RainParentFootstepDust receiver;
        static RainParentFootstepEventProbe probe;
        static ParticleSystem dust;
        static Transform footL;
        static AnimationClip clip, idle;
        static PlayableGraph graph;
        static int phase, caseIndex, leaves;
        static float started, pausedAt;
        static bool originalQueriesHitTriggers;

        static RainParentFootstepValidation()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
            try
            {
                Prepare();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, Folder + "ParentFootstepValidation.unity");
                EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(Folder + "ParentFootstepValidation.unity");
                SessionState.SetBool(Active, true);
                SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 100f);
                EditorApplication.update -= Tick;
                EditorApplication.update += Tick;
                EditorApplication.EnterPlaymode();
            }
            catch (Exception error) { Fail(error); }
        }

        static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var native = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "GenericHugeFootstepDust-Native.prefab");
            Require(native, "Original footstep dust failed to import.");
            ValidateNative(native);
            var bindings = JObject.Parse(File.ReadAllText(Evidence + "Parent动画接线依据.json"));
            var root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Parent + "mdlParent.prefab"));
            root.SetActive(false);
            try
            {
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.applyRootMotion = false;
                var handler = root.AddComponent<RainParentFootstepDust>();
                handler.bodyTransform = root.transform;
                foreach (var name in new[] { "FootL", "FootR" })
                {
                    string path = (string)bindings["childLocatorPaths"].Single(c => (string)c["name"] == name)["path"];
                    var foot = root.transform.Find(path.Substring(path.IndexOf("mdlParent/", StringComparison.Ordinal) + "mdlParent/".Length));
                    Require(foot, "Original foot locator missing: " + name);
                    if (name == "FootL") handler.footL = foot; else handler.footR = foot;
                }
                handler.dustPrefab = native.GetComponent<ParticleSystem>();
                PrefabUtility.SaveAsPrefabAsset(root, Folder + "Parent-FootstepPartial.prefab");
                AssetDatabase.LoadAssetAtPath<AnimationClip>(Parent + "ParentArmature_WalkForward.anim").SampleAnimation(root, .791476667f);
                Debug.Log("RAIN_PARENT_FOOTSTEP_SOURCE_POSE left=" + handler.footL.position + " right=" + handler.footR.position);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }

            var eventSource = JObject.Parse(File.ReadAllText(Evidence + "Parent动画事件.json"));
            int total = 0, clips = 0;
            foreach (var source in eventSource["clips"])
            {
                string name = ((string)source["clip"]).Replace('|', '_');
                var events = source["events"].Where(e => (string)e["functionName"] == "Footstep").Select(e =>
                {
                    Require((long)e["objectReferenceParameter"]["m_PathID"] == 0, "Unexpected explicit footstep effect dependency.");
                    return new AnimationEvent { functionName = "Footstep", time = (float)e["time"], stringParameter = (string)e["data"] };
                }).ToArray();
                if (events.Length == 0) continue;
                var copy = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<AnimationClip>(Parent + name + ".anim"));
                string path = Folder + name + "-FootstepsOnly.anim";
                AssetDatabase.CreateAsset(copy, path);
                AnimationUtility.SetAnimationEvents(copy, events);
                EditorUtility.SetDirty(copy);
                AssetDatabase.SaveAssets();
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
                Require(AssetDatabase.LoadAssetAtPath<AnimationClip>(path).events.Length == events.Length, "Footstep events did not survive import.");
                total += events.Length;
                clips++;
            }
            Require(total == 12 && clips == 6, "Original footstep event set incomplete.");
            Debug.Log("RAIN_PARENT_FOOTSTEP_PREPARED clips=6 events=12 root_inactive=True material_audio_shake_surface_pending=True");
        }

        static void ValidateNative(GameObject native)
        {
            var source = JObject.Parse(File.ReadAllText(Evidence + "Parent脚步尘土原生源.json"));
            var instance = UnityEngine.Object.Instantiate(native);
            try
            {
                Require(instance.GetComponentsInChildren<Transform>(true).Length == 1, "Dust must be a single root.");
                leaves = 0;
                foreach (var record in source["nativeComponents"])
                {
                    string type = (string)record["type"];
                    if (type != "ParticleSystem" && type != "ParticleSystemRenderer") continue;
                    Component component = type == "ParticleSystem" ? (Component)instance.GetComponent<ParticleSystem>() : instance.GetComponent<ParticleSystemRenderer>();
                    var data = JObject.Parse(EditorJsonUtility.ToJson(component));
                    var actual = data[type] as JObject ?? data;
                    if (component is ParticleSystemRenderer renderer)
                    {
                        actual["m_LightmapIndex"] = renderer.lightmapIndex & 0xffff;
                        actual["m_LightmapIndexDynamic"] = renderer.realtimeLightmapIndex & 0xffff;
                        actual["m_LightmapTilingOffset"] = Vector(renderer.lightmapScaleOffset);
                        actual["m_LightmapTilingOffsetDynamic"] = Vector(renderer.realtimeLightmapScaleOffset);
                        var meshes = new Mesh[4];
                        renderer.GetMeshes(meshes);
                        var reference = record["data"]["m_Mesh"];
                        Require(meshes[0] && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(meshes[0], out string guid, out long id)
                            && guid == (string)reference["guid"] && id == (long)reference["fileID"], "Original inactive mesh field differs.");
                        Require(renderer.sharedMaterials.All(m => !m), "Unrestored opaque material must remain unbound.");
                    }
                    Compare(record["data"], actual, type);
                }
                var ps = instance.GetComponent<ParticleSystem>();
                Require(!ps.main.playOnAwake && !ps.main.loop && ps.emission.GetBurst(0).count.constant == 3,
                    "Original one-shot three-particle burst differs.");
                Debug.Log("RAIN_PARENT_FOOTSTEP_NATIVE_CHECKED particles=1 comparedLeaves=" + leaves + " original_mesh_bound=True opaque_material_unbound=True");
            }
            finally { UnityEngine.Object.DestroyImmediate(instance); }
        }

        static void StartClip(float weight, float speed)
        {
            if (graph.IsValid()) graph.Destroy();
            probe.count = 0;
            graph = PlayableGraph.Create("Original Parent weighted footsteps");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            var mixer = AnimationMixerPlayable.Create(graph, 2);
            var walk = AnimationClipPlayable.Create(graph, clip);
            var rest = AnimationClipPlayable.Create(graph, idle);
            walk.SetSpeed(speed);
            graph.Connect(walk, 0, mixer, 0);
            graph.Connect(rest, 0, mixer, 1);
            mixer.SetInputWeight(0, weight);
            mixer.SetInputWeight(1, 1 - weight);
            AnimationPlayableOutput.Create(graph, "Parent", model.GetComponent<Animator>()).SetSourcePlayable(mixer);
            graph.Play();
            started = Time.time;
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0)) throw new TimeoutException("Footstep play validation timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (phase == 0)
                {
                    originalQueriesHitTriggers = Physics.queriesHitTriggers;
                    ground = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    ground.transform.position = new Vector3(0, -.5f, 0);
                    ground.transform.localScale = new Vector3(100, 1, 100);
                    water = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    water.layer = 4;
                    water.transform.position = ground.transform.position;
                    water.transform.localScale = ground.transform.localScale;
                    water.GetComponent<Collider>().isTrigger = true;
                    water.SetActive(false);
                    model = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "Parent-FootstepPartial.prefab"));
                    model.SetActive(true);
                    receiver = model.GetComponent<RainParentFootstepDust>();
                    footL = receiver.footL;
                    probe = model.AddComponent<RainParentFootstepEventProbe>();
                    probe.left = receiver.footL; probe.right = receiver.footR;
                    clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "ParentArmature_WalkForward-FootstepsOnly.anim");
                    idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(Parent + "ParentArmature_Idle, Loop.anim");
                    Physics.SyncTransforms();
                    phase = 1;
                }
                else if (phase == 1)
                {
                    dust = model.GetComponentInChildren<ParticleSystem>();
                    Require(dust && !dust.isPlaying, "Persistent dust missing or played on awake.");
                    StartClip(.5f, 2);
                    phase = 2;
                }
                else if (phase == 2 && Time.time - started > .55f)
                {
                    Require(probe.count == 1 && Mathf.Abs(probe.lastWeight - .5f) < .0001f, "Mixer did not deliver an actual half-weight event.");
                    Require(dust.isStopped && dust.particleCount == 0, "Weight .5 must be rejected.");
                    Debug.Log("RAIN_PARENT_FOOTSTEP_WEIGHT_CHECKED event_weight=0.5 emitted=False");
                    StartClip(1, 1);
                    phase = 3;
                }
                else if (phase == 3 && Time.time - started > .2f)
                {
                    Time.timeScale = 0;
                    pausedAt = Time.realtimeSinceStartup;
                    phase = 4;
                }
                else if (phase == 4 && Time.realtimeSinceStartup - pausedAt > .3f)
                {
                    Require(probe.count == 0 && dust.isStopped, "Footstep advanced during GameTime pause.");
                    Time.timeScale = 1;
                    phase = 5;
                }
                else if (phase == 5 && Time.time - started > .95f)
                {
                    Require(probe.count == 1 && probe.lastWeight > .99f, "First full-weight original event missing.");
                    CheckDustAtLastFoot();
                    phase = 6;
                }
                else if (phase == 6 && Time.time - started > 2.6f)
                {
                    Require(probe.count == 2, "Second original footstep event missing.");
                    // The original right foot is above this synthetic floor's ray interval at its event.
                    // The original handler still plays sound, but its dust branch must skip a ray miss.
                    Require(!probe.lastRayHit && dust.isStopped && dust.particleCount == 0,
                        "Original second-foot ray miss must not replay dust.");
                    Require(model.GetComponentsInChildren<ParticleSystem>().Length == 1, "Footsteps allocated additional particle instances.");
                    Debug.Log("RAIN_PARENT_FOOTSTEP_WALK_CHECKED original_events=2 first_hit=True second_ray_miss=True single_particle_instance=True paused=True gray_start_color=True");
                    caseIndex = 0;
                    BeginCase();
                    phase = 7;
                }
                else if (phase == 7 && Time.time - started > .55f)
                {
                    Require(probe.count == 1, "Case did not receive original animation event: " + caseIndex);
                    if (caseIndex == 2)
                    {
                        CheckDustAtLastFoot();
                        Require(model.GetComponentsInChildren<ParticleSystem>().Length == 1
                            && model.GetComponentInChildren<ParticleSystem>() == dust, "Later valid contact did not reuse the owned particle instance.");
                    }
                    else Require(dust.isStopped && dust.particleCount == 0, "Invalid ground/body/child emitted dust: " + caseIndex);
                    Debug.Log("RAIN_PARENT_FOOTSTEP_BRANCH_CHECKED case=" + caseIndex + " emitted=" + (caseIndex == 2));
                    if (++caseIndex < 6) BeginCase();
                    else
                    {
                        graph.Destroy();
                        UnityEngine.Object.Destroy(receiver);
                        phase = 8;
                    }
                }
                else if (phase == 8)
                {
                    Require(model && !dust, "Removing dust owner leaked its persistent instance.");
                    UnityEngine.Object.Destroy(model);
                    Debug.Log("RAIN_PARENT_FOOTSTEP_PLAY_PASSED weight_gate=True water_trigger=True range_mask_body_child_guards=True owner_cleanup=True production_wiring=False");
                    Finish(0);
                }
            }
            catch (Exception error) { Fail(error); }
        }

        static void BeginCase()
        {
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            receiver.bodyTransform = model.transform;
            receiver.footL = footL;
            receiver.worldAndWaterMask = 1 | (1 << 4);
            ground.SetActive(true);
            ground.transform.position = new Vector3(0, -.5f, 0);
            water.SetActive(false);
            Physics.queriesHitTriggers = originalQueriesHitTriggers;
            float weight = 1;
            if (caseIndex == 0) weight = .49f;
            if (caseIndex == 1) receiver.worldAndWaterMask = 0;
            if (caseIndex == 2) { ground.SetActive(false); water.SetActive(true); Physics.queriesHitTriggers = false; }
            if (caseIndex == 3) receiver.bodyTransform = null;
            if (caseIndex == 4) receiver.footL = null;
            if (caseIndex == 5) ground.transform.position = new Vector3(0, -4.5f, 0);
            Physics.SyncTransforms();
            StartClip(weight, 2);
        }

        static void CheckDustAtLastFoot()
        {
            Require(dust.isPlaying && dust.particleCount > 0, "Original burst did not emit actual particles: playing=" + dust.isPlaying
                + " count=" + dust.particleCount + " foot=" + probe.lastFootPosition + " ray_hit=" + probe.lastRayHit
                + " hit_point=" + probe.lastHitPoint + " since_clip_start=" + (Time.time - started));
            var expected = probe.lastFootPosition;
            expected.y = 0;
            Require(Vector3.Distance(dust.transform.position, expected) < .002f, "Dust is not at actual animation-event terrain contact.");
            var color = dust.main.startColor.color;
            Require(Mathf.Abs(color.r - .5f) < .001f && Mathf.Abs(color.g - .5f) < .001f && Mathf.Abs(color.b - .5f) < .001f,
                "Original no-SurfaceDef gray color differs.");
        }

        static JObject Vector(Vector4 value) => new JObject { ["x"] = value.x, ["y"] = value.y, ["z"] = value.z, ["w"] = value.w };

        static void Compare(JToken expected, JToken actual, string path)
        {
            if (expected is JObject reference && reference["fileID"] != null) return;
            Require(actual != null, path + ": missing native field.");
            if (expected is JObject obj)
            {
                foreach (var property in obj.Properties())
                    if (property.Name != "serializedVersion") Compare(property.Value, actual[property.Name], path + "." + property.Name);
            }
            else if (expected is JArray array)
            {
                Require(actual is JArray other && array.Count == other.Count, path + ": array length differs.");
                for (int i = 0; i < array.Count; i++) Compare(array[i], actual[i], path + "[" + i + "]");
            }
            else
            {
                if (expected.Type == JTokenType.Float || expected.Type == JTokenType.Integer)
                {
                    double a = (double)expected, b = actual.Type == JTokenType.Boolean ? ((bool)actual ? 1 : 0) : (double)actual;
                    Require(Math.Abs(a - b) <= .000002 * Math.Max(1, Math.Abs(a)), path + ": native value differs.");
                }
                else Require(JToken.DeepEquals(expected, actual), path + ": native value differs.");
                leaves++;
            }
        }

        static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        static void Fail(Exception error) { Debug.LogException(error); Debug.LogError("RAIN_PARENT_FOOTSTEP_FAILED"); Finish(1); }
        static void Finish(int code)
        {
            if (graph.IsValid()) graph.Destroy();
            Time.timeScale = 1;
            Physics.queriesHitTriggers = originalQueriesHitTriggers;
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }

    public sealed class RainParentFootstepEventProbe : MonoBehaviour
    {
        public int count;
        public float lastWeight;
        public Vector3 lastFootPosition;
        public Vector3 lastHitPoint;
        public bool lastRayHit;
        public Transform left, right;
        public void Footstep(AnimationEvent animationEvent)
        {
            count++;
            lastWeight = animationEvent.animatorClipInfo.weight;
            lastFootPosition = (animationEvent.stringParameter == "FootL" ? left : right).position;
            lastRayHit = Physics.Raycast(lastFootPosition + Vector3.up * 1.5f, Vector3.down, out var hit, 4,
                1 | (1 << 4), QueryTriggerInteraction.Collide);
            lastHitPoint = hit.point;
        }
    }
}
