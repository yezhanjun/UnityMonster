using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MTPSKIT.Gameplay
{
    // One graph, one output, one evaluation per visible character. Action modules only prepare nodes.
    internal sealed class HeroAnimationGraph
    {
        internal readonly PlayableGraph Graph;
        readonly AnimationLayerMixerPlayable layers;
        readonly Animator animator;
        readonly HumanPoseHandler poseHandler;
        HumanPose baseline, sampled;
        readonly float[] weights = new float[3];
        int evaluatedFrame = -1;
        internal Vector3 RootDisplacement { get; private set; }
        internal bool BaseActive => weights[0] > 0f;
        internal HeroAnimationGraph(Animator target)
        {
            animator = target;
            Graph = PlayableGraph.Create("Shared hero locomotion and actions");
            Graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            layers = AnimationLayerMixerPlayable.Create(Graph, 3);
            AnimationPlayableOutput.Create(Graph, "Hero pose", target).SetSourcePlayable(layers);
            poseHandler = new HumanPoseHandler(target.avatar, target.transform);
            baseline.muscles = new float[HumanTrait.MuscleCount];
            sampled.muscles = new float[HumanTrait.MuscleCount];
            target.applyRootMotion = true;
            Graph.Play();
        }
        internal void Connect(Playable node, int slot)
        {
            Graph.Connect(node, 0, layers, slot);
        }
        internal void Weight(int slot, float weight) => weights[slot] = Mathf.Clamp01(weight);
        internal void Remove(Playable node, int slot)
        {
            weights[slot] = 0f;
            if (!Graph.IsValid()) return;
            Graph.Disconnect(layers, slot);
            if (node.IsValid()) Graph.DestroySubgraph(node);
        }
        internal void Evaluate(float facing)
        {
            if (evaluatedFrame == Time.frameCount) return;
            evaluatedFrame = Time.frameCount; RootDisplacement = Vector3.zero;
            float actionWeight = Mathf.Max(weights[1], weights[2]);
            if (!BaseActive && actionWeight <= 0f) return;
            var root = animator.transform;
            var position = root.position;
            var rotation = Quaternion.Euler(0, facing, 0);
            root.rotation = rotation;
            if (!BaseActive) poseHandler.GetHumanPose(ref baseline);
            layers.SetInputWeight(0, weights[0]);
            // Without a firearm, blend the action against the retained TPS pose below.
            layers.SetInputWeight(1, !BaseActive && weights[1] > 0 ? 1f : weights[1]);
            layers.SetInputWeight(2, !BaseActive && weights[2] > 0 ? 1f : weights[2]);
            animator.enabled = true;
            Graph.Evaluate(Time.deltaTime);
            RootDisplacement = animator.deltaPosition;
            RootDisplacement = new Vector3(RootDisplacement.x, 0f, RootDisplacement.z);
            root.SetPositionAndRotation(position, rotation);
            if (!BaseActive)
            {
                poseHandler.GetHumanPose(ref sampled);
                for (int i = 0; i < sampled.muscles.Length; i++)
                    sampled.muscles[i] = Mathf.Lerp(baseline.muscles[i], sampled.muscles[i], actionWeight);
                sampled.bodyPosition = Vector3.Lerp(baseline.bodyPosition, sampled.bodyPosition, actionWeight);
                sampled.bodyRotation = Quaternion.Slerp(baseline.bodyRotation, sampled.bodyRotation, actionWeight);
                poseHandler.SetHumanPose(ref sampled);
            }
        }
        internal void Dispose()
        {
            if (Graph.IsValid()) Graph.Destroy();
            poseHandler.Dispose();
            if (animator) animator.enabled = false;
        }
    }
}
