using System;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    public static class GoatWhiteboxBindingDiagnostic
    {
        public static void Run()
        {
            const string folder = "Assets/Jotunnslayer/Enemies/Resources/Jotunnslayer/NiflheimBossEasy";
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".anim", StringComparison.Ordinal)) continue;
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
                int repaired = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                {
                    var curve = AnimationUtility.GetEditorCurve(clip, binding);
                    var keys = curve.keys;
                    bool changed = false;
                    for (int i = 0; i < keys.Length; i++)
                    {
                        if (float.IsNaN(keys[i].value) || float.IsInfinity(keys[i].value))
                            throw new InvalidOperationException("Non-finite animation value in " + clip.name);
                        if (float.IsNaN(keys[i].inTangent) || float.IsInfinity(keys[i].inTangent))
                        { keys[i].inTangent = 0f; changed = true; repaired++; }
                        if (float.IsNaN(keys[i].outTangent) || float.IsInfinity(keys[i].outTangent))
                        { keys[i].outTangent = 0f; changed = true; repaired++; }
                    }
                    if (!changed) continue;
                    curve.keys = keys;
                    AnimationUtility.SetEditorCurve(clip, binding, curve);
                }
                EditorUtility.SetDirty(clip);
                Debug.Log($"GOAT_TANGENT_REPAIR {clip.name} repaired={repaired}");
            }
            AssetDatabase.SaveAssets();
            Skeleton1HHostSmoke.RunAlfheimBatch();
        }
    }
}
