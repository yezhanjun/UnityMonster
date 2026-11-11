using System;
using System.Collections.Generic;
using System.IO;
using MTPSKIT.Gameplay;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    [InitializeOnLoad]
    public static class RainParentEffectLifecycleValidation
    {
        const string Active = "RainParentEffectLifecycle.Active";
        const string Deadline = "RainParentEffectLifecycle.Deadline";
        const string Source = "Docs/玩法参考设计/雨中冒险2/原版依据/Parent原生粒子校验源.json";
        const string Folder = "Assets/RainSource/ParentEffects/";
        static readonly List<GameObject> timers = new List<GameObject>();
        static readonly List<GameObject> particles = new List<GameObject>();
        static GameObject lightObject;
        static float started, pauseStarted;
        static int phase;

        static RainParentEffectLifecycleValidation()
        {
            if (SessionState.GetBool(Active, false)) EditorApplication.update += Tick;
        }

        public static void Run()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Use an isolated batch project.");
            Prepare();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            string scenePath = Folder + "ParentEffectLifecycle.unity";
            EditorSceneManager.SaveScene(scene, scenePath);
            EditorSceneManager.playModeStartScene = AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath);
            SessionState.SetBool(Active, true);
            SessionState.SetFloat(Deadline, (float)EditorApplication.timeSinceStartup + 90f);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            EditorApplication.EnterPlaymode();
        }

        static void Prepare()
        {
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            foreach (var effect in JObject.Parse(File.ReadAllText(Source))["effects"])
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>((string)effect["assetPath"]);
                if (!prefab) throw new InvalidOperationException("Native FX prefab missing.");
                var root = UnityEngine.Object.Instantiate(prefab);
                root.name = (string)effect["name"];
                root.SetActive(false);
                try
                {
                    foreach (var script in effect["omittedComponents"])
                    {
                        string name = (string)script["scriptClass"]?["m_ClassName"];
                        var data = script["data"];
                        string path = (string)script["path"];
                        int slash = path.IndexOf('/');
                        var node = slash < 0 ? root.transform : root.transform.Find(path.Substring(slash + 1));
                        if (name == "DestroyOnTimer" || name == "DestroyOnParticleEnd")
                        {
                            var lifetime = node.gameObject.AddComponent<RainParentEffectLifetime>();
                            lifetime.useTimer = name == "DestroyOnTimer";
                            if (lifetime.useTimer)
                            {
                                lifetime.duration = (float)data["duration"];
                                lifetime.resetAgeOnDisable = (int)data["resetAgeOnDisable"] != 0;
                            }
                            else lifetime.trackedParticleSystem = node.GetComponentInChildren<ParticleSystem>(true);
                        }
                        if (name != "LightIntensityCurve") continue;
                        var light = node.gameObject.AddComponent<RainParentEffectLight>();
                        var keys = new List<Keyframe>();
                        foreach (var key in data["curve"]["m_Curve"])
                            keys.Add(new Keyframe((float)key["time"], (float)key["value"],
                                (float)key["inSlope"], (float)key["outSlope"],
                                (float)key["inWeight"], (float)key["outWeight"])
                                { weightedMode = (WeightedMode)(int)key["weightedMode"] });
                        light.curve = new AnimationCurve(keys.ToArray());
                        light.timeMax = (float)data["timeMax"];
                        light.loop = (int)data["loop"] != 0;
                        light.randomStart = (int)data["randomStart"] != 0;
                        light.enableNegativeLights = (int)data["enableNegativeLights"] != 0;
                    }
                    // Prepared assets remain outside Resources while shader and event integration is unfinished.
                    PrefabUtility.SaveAsPrefabAsset(root, Folder + root.name + "-Lifecycle.prefab");
                }
                finally { UnityEngine.Object.DestroyImmediate(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("RAIN_PARENT_FX_LIFECYCLE_PREFABS_PREPARED materials_pending=True");
        }

        static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > SessionState.GetFloat(Deadline, 0f))
                    throw new TimeoutException("Parent FX lifecycle play validation timed out.");
                if (!EditorApplication.isPlaying || EditorApplication.isPaused) return;
                if (phase == 0)
                {
                    foreach (string name in new[] { "ParentBlinkEffect", "ParentDeathEffect" })
                    {
                        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + "-Lifecycle.prefab"));
                        instance.SetActive(true);
                        Require(instance.GetComponent<RainParentEffectLifetime>().duration == 3f, "Original timer duration differs.");
                        timers.Add(instance);
                    }
                    foreach (string name in new[] { "ParentStep", "ParentSlamEffect" })
                    {
                        var instance = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder + name + "-Lifecycle.prefab"));
                        instance.SetActive(true);
                        particles.Add(instance);
                    }
                    lightObject = new GameObject("Parent light restoration fixture");
                    lightObject.SetActive(false);
                    var target = lightObject.AddComponent<Light>();
                    target.intensity = 4f;
                    var controller = lightObject.AddComponent<RainParentEffectLight>();
                    controller.curve = AnimationCurve.Linear(0, 1, 1, 0);
                    controller.timeMax = .2f;
                    lightObject.SetActive(true);
                    started = Time.time;
                    phase = 1;
                }
                else if (phase == 1 && Time.time - started > .3f)
                {
                    Require(lightObject.GetComponent<Light>().intensity <= .001f, "Light curve did not finish.");
                    lightObject.SetActive(false);
                    Require(Mathf.Abs(lightObject.GetComponent<Light>().intensity - 4f) < .001f, "Light intensity not restored on disable.");
                    lightObject.SetActive(true);
                    Require(lightObject.GetComponent<Light>().intensity == 0f, "Light did not reset on enable.");
                    foreach (var instance in particles)
                        instance.GetComponent<RainParentEffectLifetime>().trackedParticleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    Time.timeScale = 0f;
                    pauseStarted = Time.realtimeSinceStartup;
                    phase = 2;
                }
                else if (phase == 2 && Time.realtimeSinceStartup - pauseStarted > .35f)
                {
                    Require(timers.TrueForAll(instance => instance), "Scaled timer expired while paused.");
                    Require(particles.TrueForAll(instance => !instance), "Particle-completion effect was not destroyed.");
                    Time.timeScale = 1f;
                    Debug.Log("RAIN_PARENT_FX_LIFECYCLE_PAUSE_PARTICLES_LIGHT_CHECKED");
                    phase = 3;
                }
                else if (phase == 3 && Time.time - started > 3.25f)
                {
                    Require(timers.TrueForAll(instance => !instance), "Original three-second timer did not destroy effect.");
                    UnityEngine.Object.DestroyImmediate(lightObject);
                    Debug.Log("RAIN_PARENT_FX_LIFECYCLE_PLAY_PASSED");
                    Finish(0);
                }
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                Debug.LogError("RAIN_PARENT_FX_LIFECYCLE_PLAY_FAILED");
                Finish(1);
            }
        }

        static void Require(bool value, string message)
        {
            if (!value) throw new InvalidOperationException(message);
        }

        static void Finish(int code)
        {
            Time.timeScale = 1f;
            SessionState.SetBool(Active, false);
            EditorApplication.update -= Tick;
            EditorApplication.delayCall += () => EditorApplication.Exit(code);
        }
    }
}
