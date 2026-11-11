using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerParticleImport
    {
        [Serializable] sealed class Document { public Node[] nodes; }
        [Serializable] sealed class Node
        {
            public string name, particleJson, rendererJson, lightJson;
            public int parent;
            public Vector3 position, scale;
            public Quaternion rotation;
        }
        [Serializable] sealed class SourceParticle
        {
            public float lengthInSec, simulationSpeed;
            public bool looping, prewarm, useUnscaledTime;
            public Initial InitialModule;
            public Emission EmissionModule;
            public Enabled VelocityModule, ColorModule;
        }
        [Serializable] sealed class Initial
        {
            public Curve startLifetime, startSize, startSpeed;
            public int maxNumParticles;
        }
        [Serializable] sealed class Emission { public Curve rateOverTime; }
        [Serializable] sealed class Curve { public int minMaxState; public float scalar, minScalar; }
        [Serializable] sealed class Enabled { public bool enabled; }

        // Import the native modules independently of original-game scripts or shaders.
        // This produces a parameter staging prefab; it is deliberately not wired into gameplay yet.
        public static void ImportAndValidate()
        {
            var source = JsonUtility.FromJson<Document>(File.ReadAllText("Docs/FrontEnd/antlers-world-particles-source.json"));
            if (source?.nodes == null || source.nodes.Length != 5) throw new InvalidOperationException("Unexpected world VFX hierarchy");
            var transforms = new Transform[source.nodes.Length];
            var native = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/RainImported/Antlers/ElusiveAntlersWorldParticles-Native.prefab");
            Require(native, "native prefab");
            GameObject root = UnityEngine.Object.Instantiate(native);
            // Unity names a prefab root after its asset filename on import.
            root.name = source.nodes[0].name;
            int particles = 0;
            try
            {
                for (int i = 0; i < source.nodes.Length; i++)
                {
                    var node = source.nodes[i];
                    var current = i == 0 ? root.transform : transforms[node.parent].Find(node.name);
                    Require(current && current.name == node.name, node.name + " hierarchy");
                    transforms[i] = current;
                    Require(Vector3.Distance(current.localPosition, node.position) < .00001f &&
                        Quaternion.Angle(current.localRotation, node.rotation) < .01f &&
                        Vector3.Distance(current.localScale, node.scale) < .00001f, node.name + " transform");
                    var instance = current.gameObject;
                    if (!string.IsNullOrEmpty(node.particleJson))
                    {
                        var system = instance.GetComponent<ParticleSystem>();
                        Require(system, node.name + " system");
                        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        var expected = JsonUtility.FromJson<SourceParticle>(node.particleJson);
                        var main = system.main;
                        Require(main.loop == expected.looping && main.prewarm == expected.prewarm &&
                            main.useUnscaledTime == expected.useUnscaledTime &&
                            Mathf.Approximately(main.duration, expected.lengthInSec) &&
                            Mathf.Approximately(main.simulationSpeed, expected.simulationSpeed) &&
                            main.maxParticles == expected.InitialModule.maxNumParticles, node.name + " main");
                        CheckCurve(main.startLifetime, expected.InitialModule.startLifetime, node.name + " lifetime");
                        CheckCurve(main.startSpeed, expected.InitialModule.startSpeed, node.name + " speed");
                        CheckCurve(main.startSize, expected.InitialModule.startSize, node.name + " size");
                        CheckCurve(system.emission.rateOverTime, expected.EmissionModule.rateOverTime, node.name + " emission");
                        Require(system.velocityOverLifetime.enabled == expected.VelocityModule.enabled &&
                            system.colorOverLifetime.enabled == expected.ColorModule.enabled, node.name + " modules");
                        system.Simulate(2f, false, true, true);
                        Require(system.particleCount > 0, node.name + " simulation");
                        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                        particles++;
                        Debug.Log($"RAIN_ANTLER_PARTICLE_NODE_CHECKED name={node.name} lifetime={main.startLifetime.constant} size={main.startSize.constant} max={main.maxParticles}");
                    }
                    if (!string.IsNullOrEmpty(node.lightJson)) Require(instance.GetComponent<Light>(), node.name + " light");
                }
                Require(particles == 3, "particle count");
                const string destination = "Assets/Resources/RainImported/Antlers/ElusiveAntlersWorldParticles-Parameters.prefab";
                PrefabUtility.SaveAsPrefabAsset(root, destination);
                AssetDatabase.SaveAssets();
                Require(AssetDatabase.LoadAssetAtPath<GameObject>(destination), "saved prefab");
                Debug.Log("RAIN_ANTLER_PARTICLE_PARAMETERS_PASSED materials_and_script_curves_pending=True");
            }
            finally { if (root) UnityEngine.Object.DestroyImmediate(root); }
        }

        static void CheckCurve(ParticleSystem.MinMaxCurve actual, Curve expected, string detail)
        {
            Require((int)actual.mode == expected.minMaxState, detail + " mode");
            if (expected.minMaxState == 0)
                Require(Mathf.Approximately(actual.constant, expected.scalar), detail);
            else if (expected.minMaxState == 3)
                Require(Mathf.Approximately(actual.constantMin, expected.minScalar) &&
                    Mathf.Approximately(actual.constantMax, expected.scalar), detail + " range");
            else Require(false, detail + " unsupported validation mode");
        }
        static void Require(bool value, string detail)
        {
            if (!value) throw new InvalidOperationException("RAIN_ANTLER_PARTICLE_PARAMETERS_FAILED " + detail);
        }
    }
}
