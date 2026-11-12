using System;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    public static class GoatWhiteboxMinimalRepair
    {
        public static void Run()
        {
            const string folder = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/NiflheimBossEasy";
            const string prefix = "mesh_base/goatlord_skeleton/GoatLord/";
            string source = Path.Combine("D:/Project/Unity/TPSBattle/TPSShoot", folder);
            foreach (string original in Directory.GetFiles(source, "*.anim"))
            {
                string raw = File.ReadAllText(original);
                var paths = Regex.Matches(raw, @"(?m)^    path: (Root(?:/[^\r\n]*)?)\r?$");
                var seen = new System.Collections.Generic.HashSet<string>();
                foreach (Match match in paths)
                {
                    string oldPath = match.Groups[1].Value;
                    if (!seen.Add(oldPath)) continue;
                    string newPath = prefix + oldPath;
                    uint oldHash = unchecked((uint)Animator.StringToHash(oldPath));
                    uint newHash = unchecked((uint)Animator.StringToHash(newPath));
                    raw = Regex.Replace(raw, @"(?m)^(\s+path: )" + oldHash + @"(?=\r?$)", "${1}" + newHash);
                }
                raw = Regex.Replace(raw, @"(?m)^(    path: )(Root(?:/[^\r\n]*)?)(?=\r?$)", "$1" + prefix + "$2");
                if (Regex.IsMatch(raw, @"(?m)^\s+value:.*(?:NaN|∞)")) throw new InvalidOperationException("Invalid value in " + original);
                raw = raw.Replace("∞", "0").Replace("NaN", "0");
                string destination = folder + "/" + Path.GetFileName(original);
                File.WriteAllText(destination, raw);
                AssetDatabase.ImportAsset(destination, ImportAssetOptions.ForceUpdate);
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(destination);
                var prefab = Resources.Load<GameObject>("Jotunnslayer/NiflheimBossEasy/skeleton_goat_sur_boss_VarEasy");
                var instance = UnityEngine.Object.Instantiate(prefab);
                int missing = 0;
                foreach (var binding in AnimationUtility.GetCurveBindings(clip))
                    if (binding.type == typeof(Transform) && !instance.transform.Find(binding.path)) missing++;
                int invalid = 0;
                foreach (float time in new[] { 0f, .2f, .4f, clip.length * .5f, Mathf.Max(0, clip.length - .001f) })
                {
                    clip.SampleAnimation(instance, time);
                    foreach (var bone in instance.GetComponentsInChildren<Transform>())
                        if (!Finite(bone.localPosition.x) || !Finite(bone.localPosition.y) || !Finite(bone.localPosition.z) ||
                            !Finite(bone.localRotation.x) || !Finite(bone.localRotation.y) || !Finite(bone.localRotation.z) || !Finite(bone.localRotation.w))
                            invalid++;
                }
                UnityEngine.Object.DestroyImmediate(instance);
                Debug.Log($"GOAT_MINIMAL_REPAIR {clip.name} missing={missing} invalidSamples={invalid}");
                if (missing != 0 || invalid != 0) throw new InvalidOperationException("Invalid repaired clip " + clip.name);
            }
            Skeleton1HHostSmoke.RunAlfheimBatch();
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
