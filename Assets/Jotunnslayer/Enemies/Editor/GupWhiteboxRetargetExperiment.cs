using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    public static class GupWhiteboxRetargetExperiment
    {
        public static void Run()
        {
            const string folder = "Assets/Resources/RainActors/RoR2/DLC1/Gup";
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".anim", StringComparison.Ordinal)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                var bindings = AnimationUtility.GetCurveBindings(clip)
                    .Where(binding => binding.type == typeof(Transform) &&
                        binding.path.StartsWith("mdlGupArmature", StringComparison.Ordinal) &&
                        binding.propertyName.StartsWith("m_LocalPosition.", StringComparison.Ordinal)).ToArray();
                var curves = new AnimationCurve[bindings.Length];
                for (int i = 0; i < bindings.Length; i++)
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, bindings[i]);
                    var keys = curve.keys;
                    for (int k = 0; k < keys.Length; k++)
                    {
                        keys[k].value *= .01f;
                        keys[k].inTangent *= .01f;
                        keys[k].outTangent *= .01f;
                    }
                    curve.keys = keys;
                    curves[i] = curve;
                }
                AnimationUtility.SetEditorCurves(clip, bindings, curves);
                EditorUtility.SetDirty(clip);
                Debug.Log($"GUP_UNIT_FIX {clip.name} positionChannels={bindings.Length}");
            }
            AssetDatabase.SaveAssets();
            Skeleton1HHostSmoke.RunAlfheimBatch();
        }
    }
}
