using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MTPSKIT.Gameplay
{
    internal sealed class FourWayJumpPresentation
    {
        readonly CharacterMotor motor;
        readonly Animator animator;
        PlayableGraph graph;
        readonly HeroAnimationGraph shared;
        AnimationMixerPlayable mixer;
        readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[22];
        double sampledJumpStart = -1;
        readonly CharacterInstance owner;
        Item sessionItem;
        float layerWeight, releaseTime = -1f;
        float landingExitBlend;
        internal float LandingWeight => mixer.IsValid() ? (mixer.GetInputWeight(6) + mixer.GetInputWeight(21)) * layerWeight : 0f;
        internal bool LandingBlendOut => releaseTime >= 0f;
        internal bool Applied { get; private set; }
        internal string Phase { get; private set; }
        internal AnimationClip CurrentLoop { get; private set; }
        internal AnimationClip CurrentStart { get; private set; }
        internal FourWayJumpPresentation(CharacterInstance owner, Animator target, HeroAnimationGraph sharedGraph)
        { this.owner = owner; motor = owner.GetComponent<CharacterMotor>(); animator = target; shared = sharedGraph; graph = shared.Graph; }
        internal void Tick()
        {
            Applied = false;
            if (!motor || !motor.enabled || owner.CurrentHealth <= 0 || motor.IsFourWayRolling || !motor.JumpProfile)
            { Deactivate(); return; }
            if (motor.FourJumpStarted < 0)
            {
                if (mixer.IsValid() && Phase == "Landing" && owner.CharacterItemManager.CurrentlyUsedItem == sessionItem && owner.IsGrounded)
                {
                    float delta = owner.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
                    if (releaseTime < 0f) releaseTime = 0f;
                    releaseTime += delta;
                    layerWeight = Mathf.Max(0f, 1f - releaseTime / Mathf.Max(.01f, landingExitBlend));
                    // Retain the interrupted landing mixture, as Animator crossfade does.
                    SampleLanding(6, (float)clips[6].GetTime() + delta, delta);
                    SampleLanding(21, (float)clips[21].GetTime() + delta, delta);
                    shared.Weight(1, layerWeight); Applied = layerWeight > 0f;
                    if (!Applied) Deactivate();
                    return;
                }
                Deactivate(); return;
            }
            releaseTime = -1f;
            if (!mixer.IsValid()) Bind();
            if (!mixer.IsValid()) return;
            // A new jump may interrupt the landing recovery before the graph is released.
            if (sampledJumpStart != motor.FourJumpStarted)
            {
                sampledJumpStart = motor.FourJumpStarted;
                sessionItem = owner.CharacterItemManager.CurrentlyUsedItem;
            }
            var profile = motor.JumpProfile;
            float elapsed = motor.JumpElapsed;
            float landingTime = motor.JumpLandingElapsed;
            int direction = motor.FourJumpDirection;
            int start = motor.FourJumpDirectional ? direction : direction == 0 ? 7 : motor.FourJumpRightFoot ? 9 : 8;
            var startClip = motor.FourJumpDirectional ? profile.starts[direction] : direction == 0 ? profile.regularIdle : motor.FourJumpRightFoot ? profile.regularMoveRight : profile.regularMove;
            int loopIndex = motor.FourJumpDirectional ? 5 : direction == 0 ? 10 : motor.FourJumpRightFoot ? 12 : 11;
            AnimationClip loopClip = profile.TailClip(motor.FourJumpDirectional, motor.FourJumpDirection, motor.FourJumpRightFoot);
            int previousLoop = loopIndex;
            AnimationClip previousLoopClip = loopClip;
            int previousStart = start;
            AnimationClip previousClip = startClip;
            bool doubled = motor.JumpDoubleAt >= 0f;
            if (doubled) { start = 13; startClip = profile.doubleJump; loopIndex = 14; loopClip = profile.doubleTail ? profile.doubleTail : profile.loop; }
            if (doubled && motor.DoubleJumpDirectional)
            {
                // Separate playables let the same lock clip crossfade into itself at time zero.
                start = 15 + motor.DoubleJumpDirection;
                startClip = profile.starts[motor.DoubleJumpDirection];
                loopIndex = 20; loopClip = profile.loop;
            }
            CurrentStart = startClip;
            CurrentLoop = loopClip;
            float startLength = startClip.length * (doubled && !motor.DoubleJumpDirectional ? profile.doubleToLoop : profile.startToLoop);
            float phaseBlend = doubled ? Mathf.Max(.01f, profile.doubleLoopBlend) : profile.blend;
            for (int i = 0; i < 22; i++) mixer.SetInputWeight(i, 0f);
            float airTime = Mathf.Max(0f, elapsed - (landingTime >= 0 ? landingTime : 0) - (doubled ? motor.JumpDoubleAt : 0));
            float loopWeight = Mathf.Clamp01((airTime - startLength) / phaseBlend);
            clips[start].SetTime(Mathf.Min(airTime, startClip.length));
            clips[loopIndex].SetTime(Mathf.Max(0, airTime - startLength) % Mathf.Max(.01f, loopClip.length));
            float landWeight = landingTime < 0 ? 0f : Mathf.SmoothStep(0, 1, landingTime / profile.blend);
            mixer.SetInputWeight(start, (1f - loopWeight) * (1f - landWeight));
            mixer.SetInputWeight(loopIndex, loopWeight * (1f - landWeight));
            float runWeight = motor.JumpLandingRun ? Mathf.Clamp01(motor.LandingRunElapsed / profile.landingRunBlend) : 0f;
            if (motor.JumpLandingRun && motor.LandingRunElapsed >= landingTime - .001f) runWeight = 1f;
            float landingDelta = owner.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
            SampleLanding(6, Mathf.Max(0, landingTime), landingDelta); mixer.SetInputWeight(6, landWeight * (1f - runWeight));
            SampleLanding(21, motor.LandingRunElapsed, landingDelta); mixer.SetInputWeight(21, landWeight * runWeight);
            landingExitBlend = motor.JumpLandingRun ? profile.landingRunBlend : profile.blend;
            if (doubled && landingTime < 0f)
            {
                float doubleBlend = Mathf.Clamp01((elapsed - motor.JumpDoubleAt) / Mathf.Max(.01f, profile.doubleEnterBlend));
                for (int i = 0; i < 22; i++) mixer.SetInputWeight(i, mixer.GetInputWeight(i) * doubleBlend);
                clips[previousStart].SetTime(Mathf.Min(elapsed, previousClip.length));
                float previousLoopWeight = Mathf.Clamp01((elapsed - previousClip.length * profile.startToLoop) / profile.blend);
                clips[previousLoop].SetTime(Mathf.Max(0f, elapsed - previousClip.length * profile.startToLoop) % Mathf.Max(.01f, previousLoopClip.length));
                mixer.SetInputWeight(previousStart, mixer.GetInputWeight(previousStart) + (1f - doubleBlend) * (1f - previousLoopWeight));
                mixer.SetInputWeight(previousLoop, mixer.GetInputWeight(previousLoop) + (1f - doubleBlend) * previousLoopWeight);
            }
            Phase = landingTime >= 0 ? "Landing" : loopWeight > 0 ? "Loop" : doubled ? "Double" : "Start";
            float blend = Mathf.Clamp01(elapsed / profile.blend);
            // Landing entry crossfades inside this layer. Do not fade the whole layer out
            // before its 0.25-second entry transition has even had a chance to play.
            layerWeight = blend;
            shared.Weight(1, layerWeight);
            Applied = true;
        }
        internal int BuildCount { get; private set; }
        void SampleLanding(int index, float time, float delta)
        {
            // Evaluate a real interval so the shared graph extracts landing XZ root motion.
            float end = Mathf.Clamp(time, 0f, clips[index].GetAnimationClip().length);
            float start = Mathf.Clamp(time - delta, 0f, end);
            clips[index].SetTime(start);
            clips[index].SetSpeed(Time.deltaTime > 0f ? (end - start) / Time.deltaTime : 0f);
        }
        void Bind()
        {
            var profile = motor.JumpProfile;
            if (!profile || profile.starts == null || profile.starts.Length != 5 || !profile.loop || !profile.landing || !profile.regularIdle || !profile.regularMove || !profile.regularMoveRight || !profile.idleTail || !profile.moveTail || !profile.rightTail || !profile.doubleJump) return;
            for (int i = 0; i < 5; i++) if (!profile.starts[i]) return;
            BuildCount++;
            mixer = AnimationMixerPlayable.Create(graph, 22);
            for (int i = 0; i < 22; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, i == 21 ? (profile.landingRun ? profile.landingRun : profile.landing) : i == 20 ? profile.loop : i >= 15 ? profile.starts[i - 15] : i < 5 ? profile.starts[i] : i == 5 ? profile.loop : i == 6 ? profile.landing : i == 7 ? profile.regularIdle : i == 8 ? profile.regularMove : i == 9 ? profile.regularMoveRight : i == 10 ? profile.idleTail : i == 11 ? profile.moveTail : i == 12 ? profile.rightTail : i == 13 ? profile.doubleJump : profile.doubleTail ? profile.doubleTail : profile.loop);
                clips[i].SetSpeed(0); clips[i].SetApplyFootIK(false); clips[i].SetApplyPlayableIK(false);
                graph.Connect(clips[i], 0, mixer, i);
            }
            shared.Connect(mixer, 1);
        }
        void Deactivate()
        {
            shared.Weight(1, 0f);
            Applied = false;
            Phase = null; CurrentLoop = null; releaseTime = -1f; layerWeight = 0f;
        }
        internal void Release()
        {
            if (mixer.IsValid()) shared.Remove(mixer, 1);
            Applied = false; Phase = null;
        }
    }
}
