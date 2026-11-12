using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using MTPSKIT.Gameplay;

namespace MTPSKIT.BuildTools
{
    public static class HeroRootMotionBake
    {
        public static void BakeAndBuild() { Bake(); AcceptanceClientsBuild.Build(); }
        public static void Bake()
        {
            var clips = new HashSet<AnimationClip>();
            var runs = new HashSet<AnimationClip>();
            foreach (string key in new[] { "Rifle", "Pistol", "Shotgun", "Sniper" })
            {
                var p = Resources.Load<WendyRifleProfile>("DemonHunter3C/Wendy" + key);
                clips.Add(p.run); clips.Add(p.runStop); clips.Add(p.sprint);
                if (p.runStopLeft) clips.Add(p.runStopLeft);
                if (p.runStopRight) clips.Add(p.runStopRight);
                runs.Add(p.run);
                foreach (var clip in p.hipMove) clips.Add(clip);
                foreach (var clip in p.aimMove) clips.Add(clip);
            }
            var tracks = new List<HeroRootMotionLibrary.Track>();
            foreach (var clip in clips)
            {
                var instance = Object.Instantiate(Resources.Load<GameObject>(RangeCharacterSelection.ResourcePath(12)));
                var animator = instance.GetComponent<Animator>();
                var graph = PlayableGraph.Create("Bake source root motion");
                try
                {
                    animator.runtimeAnimatorController = null; animator.enabled = true; animator.applyRootMotion = true;
                    graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                    var playable = AnimationClipPlayable.Create(graph, clip);
                    playable.SetApplyFootIK(false); playable.SetApplyPlayableIK(false);
                    AnimationPlayableOutput.Create(graph, "Sample", animator).SetSourcePlayable(playable);
                    graph.Play(); graph.Evaluate(0);
                    int steps = Mathf.CeilToInt(clip.length * 60f);
                    float dt = clip.length / steps;
                    var xs = new Keyframe[steps + 1]; var zs = new Keyframe[steps + 1];
                    var left = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
                    var right = animator.GetBoneTransform(HumanBodyBones.RightFoot);
                    var feetL = new Vector3[steps + 1]; var feetR = new Vector3[steps + 1];
                    feetL[0] = instance.transform.InverseTransformPoint(left.position);
                    feetR[0] = instance.transform.InverseTransformPoint(right.position);
                    Vector3 position = Vector3.zero;
                    for (int i = 1; i <= steps; i++)
                    {
                        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                        graph.Evaluate(dt);
                        position += animator.deltaPosition;
                        xs[i] = new Keyframe(i * dt, position.x); zs[i] = new Keyframe(i * dt, position.z);
                        feetL[i] = instance.transform.InverseTransformPoint(left.position);
                        feetR[i] = instance.transform.InverseTransformPoint(right.position);
                    }
                    var x = new AnimationCurve(xs); var z = new AnimationCurve(zs);
                    for (int i = 0; i <= steps; i++)
                    {
                        AnimationUtility.SetKeyLeftTangentMode(x, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(x, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyLeftTangentMode(z, i, AnimationUtility.TangentMode.Linear);
                        AnimationUtility.SetKeyRightTangentMode(z, i, AnimationUtility.TangentMode.Linear);
                    }
                    float scale = 1f;
                    if (runs.Contains(clip))
                    {
                        // Retargeted leg reach need not match the source root stride.
                        // Fit a constant stride scale only from low, backwards-moving support feet.
                        float minL = float.MaxValue, minR = float.MaxValue;
                        for (int i = 0; i <= steps; i++) { minL = Mathf.Min(minL, feetL[i].y); minR = Mathf.Min(minR, feetR[i].y); }
                        float numerator = 0f, denominator = 0f;
                        for (int i = 1; i <= steps; i++)
                        {
                            float root = zs[i].value - zs[i-1].value;
                            float l = feetL[i].z - feetL[i-1].z, r = feetR[i].z - feetR[i-1].z;
                            if (root <= .0001f) continue;
                            float phase = (float)i / steps;
                            if (phase > .02f && phase < .18f && l < 0)
                            { numerator += -l * root; denominator += root * root; }
                            if (phase > .52f && phase < .68f && r < 0)
                            { numerator += -r * root; denominator += root * root; }
                        }
                        if (denominator > .00001f) scale = Mathf.Clamp(numerator / denominator, .5f, 1.5f);
                    }
                    tracks.Add(new HeroRootMotionLibrary.Track { clip = clip, x = x, z = z, plantedScale = scale });
                    Debug.Log("HERO_ROOT_BAKED " + clip.name + " delta=" + position + " plantedScale=" + scale);
                }
                finally { graph.Destroy(); Object.DestroyImmediate(instance); }
            }
            const string path = "Assets/Resources/DemonHunter3C/HeroRootMotion.asset";
            var library = AssetDatabase.LoadAssetAtPath<HeroRootMotionLibrary>(path);
            if (!library) { library = ScriptableObject.CreateInstance<HeroRootMotionLibrary>(); AssetDatabase.CreateAsset(library, path); }
            library.tracks = tracks.ToArray(); EditorUtility.SetDirty(library); AssetDatabase.SaveAssets();
        }
    }
}
