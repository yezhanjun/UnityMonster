#if UNITY_EDITOR
using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Playables;

namespace Jotunnslayer
{
    internal static class DraugVisualBlendValidation
    {
        static readonly FieldInfo GraphField = typeof(Skeleton1HVisual).GetField("graph", BindingFlags.Instance | BindingFlags.NonPublic);
        static void Require(bool condition, string reason) { if (!condition) throw new Exception(reason); }
        static void Pose(Transform bone, float expected, string phase)
        {
            Require(Mathf.Abs(bone.localPosition.x - expected) < .0001f,
                $"{phase}: expected {expected}, actual {bone.localPosition.x}");
        }
        static AnimationClip Clip(string name, float from, float to)
        {
            var clip = new AnimationClip { name = name };
            clip.SetCurve("Bone", typeof(Transform), "m_LocalPosition.x", AnimationCurve.Linear(0, from, 1, to));
            return clip;
        }
        public static void Run()
        {
            int exit = 1;
            GameObject root = null;
            Avatar avatar = null;
            AnimationClip idle = null, attack = null, hit = null;
            try
            {
                root = new GameObject("BlendValidation");
                var bone = new GameObject("Bone").transform;
                bone.SetParent(root.transform, false);
                var animator = root.AddComponent<Animator>();
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                avatar = AvatarBuilder.BuildGenericAvatar(root, "");
                animator.avatar = avatar;
                var visual = root.AddComponent<Skeleton1HVisual>();
                // Awake is not automatic for a non-ExecuteAlways component in edit mode.
                typeof(Skeleton1HVisual).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                idle = Clip("idle", 0, 0); attack = Clip("attack", 10, 20); hit = Clip("hit", 30, 30);
                visual.Bind(idle, idle, idle, attack, hit, null);
                Require(visual.Play(Skeleton1HVisual.Motion.Idle), "Initial play rejected");
                var graph = (PlayableGraph)GraphField.GetValue(visual);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                graph.Evaluate(0); Pose(bone, 0, "idle");
                Require(visual.Play(Skeleton1HVisual.Motion.Attack, true, 0, null, .4f, .1f), "Blend rejected");
                typeof(Skeleton1HVisual).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                graph.Evaluate(0); Pose(bone, 0, "same frame blend start");
                visual.AdvanceTransition(.05f); graph.Evaluate(0); Pose(bone, 7, "offset half blend");
                Require(graph.GetPlayableCount() == 3, "Expected two clips and mixer");
                // A second transition starts from the actual partially mixed pose.
                visual.Play(Skeleton1HVisual.Motion.Hit, true, 0, null, 0, .2f);
                graph.Evaluate(0); Pose(bone, 7, "interruption continuity");
                visual.AdvanceTransition(.1f); graph.Evaluate(0); Pose(bone, 18.5f, "interruption half blend");
                visual.AdvanceTransition(.1f); graph.Evaluate(0); Pose(bone, 30, "interruption complete");
                Require(graph.GetPlayableCount() == 1, "Completed blend retained old subtree");
                visual.Play(Skeleton1HVisual.Motion.Attack, true, 0, null, .4f, .1f);
                visual.AdvanceTransition(.05f); graph.Evaluate(0); Pose(bone, 22, "second half blend");
                visual.Play(Skeleton1HVisual.Motion.Idle, true);
                graph.Evaluate(0); Pose(bone, 0, "immediate cancel");
                Require(graph.GetPlayableCount() == 1, "Immediate play retained blend subtree");
                visual.Play(Skeleton1HVisual.Motion.Attack, true, 1, null, .4f);
                graph.Evaluate(0); Pose(bone, 14, "offset first evaluation");
                graph.Evaluate(.1f); Pose(bone, 15, "offset clock advances");
                visual.Play(Skeleton1HVisual.Motion.Hit, true, 0, null, 0, .2f);
                // Exercise the component cleanup path directly in the editor.
                typeof(Skeleton1HVisual).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                Require(!graph.IsValid(), "Disable did not destroy graph");
                Require(visual.Play(Skeleton1HVisual.Motion.Idle, true), "Graph recreation rejected");
                graph = (PlayableGraph)GraphField.GetValue(visual);
                Require(graph.IsValid() && graph.GetPlayableCount() == 1, "Graph recreation invalid");
                typeof(Skeleton1HVisual).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(visual, null);
                Debug.Log("DRAUG_VISUAL_BLEND_PASS offsetPose=True blendPose=True interruption=True cleanup=True clock=True recreation=True combatIntegration=False");
                exit = 0;
            }
            catch (Exception error) { Debug.LogException(error); }
            finally
            {
                if (root) UnityEngine.Object.DestroyImmediate(root);
                if (avatar) UnityEngine.Object.DestroyImmediate(avatar);
                if (idle) UnityEngine.Object.DestroyImmediate(idle);
                if (attack) UnityEngine.Object.DestroyImmediate(attack);
                if (hit) UnityEngine.Object.DestroyImmediate(hit);
            }
            EditorApplication.Exit(exit);
        }
    }
}
#endif
