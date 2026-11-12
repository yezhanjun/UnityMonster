using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
namespace Jotunnslayer
{
    public static class ClayRuntimeClipBake
    {
        static string Folder, Model, BundlePrefix;
        static string[] Names;
        static void Configure(bool grenadier) {
            Folder = "Assets/Resources/RainActors/PendingWhitebox/" + (grenadier ? "claygrenadier" : "claybruiser");
            Model = grenadier ? "mdlClayGrenadier" : "mdlClayBruiser";
            BundlePrefix = grenadier ? "ror2-dlc1-claygrenadier" : "ror2-base-claybruiser";
            Names = grenadier ? new[] { "Idle", "Walk", "Attack" } : new[] { "Idle__Additive_Loop", "WalkForward", "FireGatling__Additive" };
        }
        public static void Run() { foreach(bool g in new[] { false,true }) { Configure(g); Bake(); } EditorApplication.Exit(0); }
        static void Bake()
        {
            const string source = "E:/SteamLibrary/steamapps/common/Risk of Rain 2/Risk of Rain 2_Data/StreamingAssets/aa/StandaloneWindows64";
            var bundles = new System.Collections.Generic.List<AssetBundle>();
            int baked = 0;
            try
            {
                foreach (string pattern in new[] { BundlePrefix + "_static_assets_all_*.bundle", BundlePrefix + "_assets_all_*.bundle" })
                    foreach (string file in Directory.GetFiles(source, pattern)) bundles.Add(AssetBundle.LoadFromFile(file));
                foreach (var bundle in bundles)
                foreach (var original in bundle.LoadAllAssets<AnimationClip>())
                {
                    string normalized = original.name.Replace('|', '_').Replace(',', '_').Replace(' ', '_');
                    string key = Array.Find(Names, n => normalized == n || normalized.EndsWith("_" + n, StringComparison.Ordinal));
                    if (key == null) continue;
                    var model = Object.Instantiate(Resources.Load<GameObject>(Folder.Substring("Assets/Resources/".Length) + "/" + Model));
                    AnimationClip clip = null;
                    try
                    {
                        foreach (var animator in model.GetComponentsInChildren<Animator>()) animator.fireEvents = false;
                        var bones = model.GetComponentsInChildren<Transform>();
                        var curves = new AnimationCurve[bones.Length * 10];
                        for (int i = 0; i < curves.Length; i++) curves[i] = new AnimationCurve();
                        string[] props = { "m_LocalPosition.x", "m_LocalPosition.y", "m_LocalPosition.z", "m_LocalRotation.x", "m_LocalRotation.y", "m_LocalRotation.z", "m_LocalRotation.w", "m_LocalScale.x", "m_LocalScale.y", "m_LocalScale.z" };
                        var initial = new Quaternion[bones.Length];
                        int frames = Mathf.Clamp(Mathf.CeilToInt(original.length * 30), 2, 180);
                        float maxAngle = 0;
                        for (int frame = 0; frame <= frames; frame++)
                        {
                            float time = original.length * frame / frames;
                            original.SampleAnimation(model, time);
                            for (int i = 1; i < bones.Length; i++)
                            {
                                var t = bones[i]; var p = t.localPosition; var q = t.localRotation; var s = t.localScale;
                                if (frame == 0) initial[i] = q; else maxAngle = Mathf.Max(maxAngle, Quaternion.Angle(initial[i], q));
                                float[] values = { p.x, p.y, p.z, q.x, q.y, q.z, q.w, s.x, s.y, s.z };
                                for (int axis = 0; axis < 10; axis++)
                                {
                                    if (float.IsNaN(values[axis]) || float.IsInfinity(values[axis])) throw new Exception("Nonfinite Bell pose");
                                    curves[i * 10 + axis].AddKey(new Keyframe(time, values[axis], 0, 0));
                                }
                            }
                        }
                        
                        clip = new AnimationClip { frameRate = 30 };
                        for (int i = 1; i < bones.Length; i++)
                            for (int axis = 0; axis < 10; axis++)
                                AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(AnimationUtility.CalculateTransformPath(bones[i], model.transform), typeof(Transform), props[axis]), curves[i * 10 + axis]);
                        clip.EnsureQuaternionContinuity();
                        var destinations = Directory.GetFiles(Folder, "*.anim").Where(p => Path.GetFileName(p).StartsWith(normalized + "_", StringComparison.Ordinal)).ToArray();
                        if (destinations.Length != 1) throw new Exception("Ambiguous Bell destination: " + key);
                        var existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(destinations[0]);
                        clip.name = existing.name;
                        EditorUtility.CopySerialized(clip, existing);
                        var settings = AnimationUtility.GetAnimationClipSettings(existing);
                        settings.loopTime = key.StartsWith("Idle") || key.StartsWith("Walk");
                        AnimationUtility.SetAnimationClipSettings(existing, settings);
                        EditorUtility.SetDirty(existing);
                        Debug.Log($"CLAY_BAKED {Model} {key} curves={AnimationUtility.GetCurveBindings(existing).Length} duration={original.length} maxAngle={maxAngle}");
                        baked++;
                    }
                    finally { if (clip) Object.DestroyImmediate(clip); Object.DestroyImmediate(model); }
                }
                if (baked != 3) throw new Exception("Expected 3 Bell clips, got " + baked);
                AssetDatabase.SaveAssets();
            }
            finally { foreach (var bundle in bundles) if (bundle) bundle.Unload(true); }

        }
        public static void VerifySavedAndRun()
        {
            foreach (bool g in new[] { false,true }) { Configure(g);
            foreach (string key in Names)
            {
                var p = Directory.GetFiles(Folder, "*.anim").Single(f => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(f), @"(?:^|_)" + System.Text.RegularExpressions.Regex.Escape(key) + @"_\d+$"));
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(p);
                int curves = AnimationUtility.GetCurveBindings(clip).Length;
                if (curves < 10 || clip.length <= 0) throw new Exception("Bell saved curves missing: " + key);
                Debug.Log($"CLAY_SAVED {Model} {key} curves={curves} duration={clip.length} loop={AnimationUtility.GetAnimationClipSettings(clip).loopTime}");
            }
            }
            Skeleton1HHostSmoke.RunAlfheimBatch();
        }
    }
}
