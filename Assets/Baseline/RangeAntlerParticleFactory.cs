using System;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Native VFX assets remain independent of MonoScript importer identity. As with the
    // shard's server physics, the owner creates and configures runtime behaviors explicitly.
    public static class RangeAntlerParticleFactory
    {
        [Serializable] sealed class Document { public Source[] scripts; }
        [Serializable] sealed class Source { public string name, className; public Data data; }
        [Serializable] sealed class Data
        {
            public Curve alphaCurve, curve;
            public float timeMax;
            public bool pauseTime, resetOnEnable, destroyOnEnd, disableOnEnd, disableObjectOnEnd;
            public bool loop, randomStart, enableNegativeLights;
        }
        [Serializable] sealed class Curve { public Key[] m_Curve; public int m_PreInfinity, m_PostInfinity; }
        [Serializable] sealed class Key
        {
            public float time, value, inSlope, outSlope, inWeight, outWeight;
            public int weightedMode;
        }
        sealed class Recipe { public string path; public AnimationCurve curve; public float duration; public bool light; }
        // Immutable source curves live for the play session and are never mutated by instances.
        static Recipe[] recipes;

        public static GameObject Create(Vector3 position)
        {
            var prefab = Resources.Load<GameObject>("RainImported/Antlers/ElusiveAntlersWorldParticles-Materials");
            if (!prefab) throw new InvalidOperationException("Antler native particle prefab missing");
            if (recipes == null) LoadRecipes();
            var root = UnityEngine.Object.Instantiate(prefab, position, Quaternion.identity);
            root.SetActive(false);
            try
            {
                foreach (var recipe in recipes)
                {
                    var node = root.transform.Find(recipe.path);
                    if (!node) throw new InvalidOperationException("Antler particle node missing: " + recipe.path);
                    var fade = node.gameObject.AddComponent<RangeAntlerParticleFade>();
                    fade.ConfigureSource(recipe.curve, recipe.duration, recipe.light);
                }
                root.SetActive(true);
                return root;
            }
            catch
            {
                root.SetActive(false);
                if (Application.isPlaying) UnityEngine.Object.Destroy(root);
                else UnityEngine.Object.DestroyImmediate(root);
                throw;
            }
        }

        static void LoadRecipes()
        {
            var text = Resources.Load<TextAsset>("RainImported/Antlers/WorldScriptCurves");
            if (!text) throw new InvalidOperationException("Antler source curves missing");
            var source = JsonUtility.FromJson<Document>(text.text);
            if (source?.scripts == null || source.scripts.Length != 3)
                throw new InvalidOperationException("Antler source curve count");
            var loaded = new Recipe[3];
            for (int index = 0; index < loaded.Length; index++)
            {
                var entry = source.scripts[index];
                if (entry.data.pauseTime || entry.data.resetOnEnable || entry.data.destroyOnEnd ||
                    entry.data.disableOnEnd || entry.data.disableObjectOnEnd || entry.data.loop ||
                    entry.data.randomStart || entry.data.enableNegativeLights)
                    throw new InvalidOperationException("Unsupported antler source curve lifecycle");
                bool light = entry.className == "LightIntensityCurve";
                if (!light && entry.className != "AnimateShaderAlpha") throw new InvalidOperationException("Antler source script");
                string path = entry.name switch
                {
                    "WispBase" => "WispBase",
                    "WispFlame" => "WispBase/WispFlame",
                    "Point Light" => "WispBase/Point Light",
                    _ => throw new InvalidOperationException("Antler source curve target")
                };
                for (int previous = 0; previous < index; previous++)
                    if (loaded[previous].path == path) throw new InvalidOperationException("Duplicate antler source curve target");
                var raw = light ? entry.data.curve : entry.data.alphaCurve;
                if (raw.m_PreInfinity != 2 || raw.m_PostInfinity != 2 || entry.data.timeMax <= 0f)
                    throw new InvalidOperationException("Antler source curve extrapolation");
                var keys = new Keyframe[raw.m_Curve.Length];
                for (int i = 0; i < keys.Length; i++)
                {
                    var key = raw.m_Curve[i];
                    keys[i] = new Keyframe(key.time, key.value, key.inSlope, key.outSlope, key.inWeight, key.outWeight)
                        { weightedMode = (WeightedMode)key.weightedMode };
                }
                loaded[index] = new Recipe { path = path, light = light, duration = entry.data.timeMax,
                    curve = new AnimationCurve(keys) { preWrapMode = WrapMode.ClampForever, postWrapMode = WrapMode.ClampForever } };
            }
            recipes = loaded;
        }
    }
}
