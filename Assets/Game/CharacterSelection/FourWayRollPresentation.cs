using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MTPSKIT.Gameplay
{
    // A full-body overlay after weapon presentation, before wardrobe/secondary motion.
    internal sealed class FourWayRollPresentation
    {
        readonly CharacterMotor motor;
        readonly Animator animator;
        PlayableGraph graph;
        readonly HeroAnimationGraph shared;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[4];
        internal bool Applied { get; private set; }
        internal FourWayRollPresentation(CharacterInstance owner, Animator target, HeroAnimationGraph sharedGraph)
        { motor = owner.GetComponent<CharacterMotor>(); animator = target; shared = sharedGraph; graph = shared.Graph; }
        internal void Tick()
        {
            Applied = false;
            if (!motor || !motor.IsFourWayRolling) { Deactivate(); return; }
            if (!mixer.IsValid()) Bind();
            if (!mixer.IsValid()) return;
            float time = motor.RollElapsed;
            for (int i = 0; i < 4; i++)
            {
                mixer.SetInputWeight(i, i == motor.RollDirection ? 1f : 0f);
                clips[i].SetTime(time);
            }
            float blend = Mathf.Min(Mathf.Clamp01(time / motor.RollProfile.blendDuration),
                Mathf.Clamp01((motor.RollProfile.Duration(motor.RollDirection) - time) / motor.RollProfile.blendDuration));
            blend *= 1f - motor.RollMoveWeight;
            shared.Weight(2, blend);
            Applied = true;
        }
        internal int BuildCount { get; private set; }
        void Bind()
        {
            var profile = motor.RollProfile;
            if (!profile || profile.directions == null || profile.directions.Length != 4) return;
            for (int i = 0; i < 4; i++) if (!profile.directions[i].clip) return;
            BuildCount++;
            mixer = AnimationMixerPlayable.Create(graph, 4);
            for (int i = 0; i < 4; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, profile.directions[i].clip);
                clips[i].SetSpeed(0f); clips[i].SetApplyFootIK(false); clips[i].SetApplyPlayableIK(false);
                graph.Connect(clips[i], 0, mixer, i);
            }
            shared.Connect(mixer, 2);
        }
        void Deactivate()
        {
            shared.Weight(2, 0f);
            Applied = false;
        }
        internal void Release()
        {
            if (mixer.IsValid()) shared.Remove(mixer, 2);
            Applied = false;
        }
    }
}
