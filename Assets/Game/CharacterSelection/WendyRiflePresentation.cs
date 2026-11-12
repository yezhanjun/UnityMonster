using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MTPSKIT.Gameplay
{
    // Presentation owned by the portable display. No movement, ammo or damage authority.
    internal sealed class WendyRiflePresentation
    {
        readonly CharacterInstance owner;
        readonly Animator animator;
        WendyRifleProfile profile;
        readonly CharacterMotor motor;
        PlayableGraph graph;
        readonly HeroAnimationGraph shared;
        AnimationMixerPlayable locomotion;
        AnimationLayerMixerPlayable layers;
        readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[33];
        readonly AnimationClip[] sources = new AnimationClip[33];
        AnimationClipPlayable hipUpper, emptyReload;
        AnimationMixerPlayable reloadMixer;
        AvatarMask upperBody;
        Gun gun;
        Transform weapon, previousParent, mesh;
        Vector3 previousPosition, previousScale, meshPosition, meshScale;
        Quaternion previousRotation, meshRotation;
        Animator weaponAnimator;
        bool previousWeaponAnimator;
        float clock, aim, precisionAim, moving, fireTime = 100f, takeTime = 100f, reloadTime;
        bool reloading;
        bool wasMoving, wasSprinting, sprintStopActive;
        float sprintStopTime;
        int sprintStopIndex = 31;
        readonly float[] sprintStopSamples = new float[2];
        internal bool SprintStopActive => sprintStopActive;
        internal bool SprintStopRight => sprintStopIndex == 32;
        internal float SprintStopWeight => blendWeights[31] + blendWeights[32];
        internal float SprintStopSample => sprintStopTime;
        internal AnimationClip SprintStopClip => sources[sprintStopIndex];
        float stopTime = 100f;
        int stopIndex = 23;
        bool stopActive, queuedOppositeStop, queuedStopRight;
        readonly float[] stopSamples = new float[2];
        internal bool RunStopActive => stopActive;
        internal bool RunStopRight => stopIndex == 24;
        internal float RunStopWeight => blendWeights[23] + blendWeights[24];
        internal AnimationClip RunStopClip => sources[stopIndex];
        internal float StopSelectedPhase { get; private set; }
        readonly float[] targetWeights = new float[33];
        readonly float[] blendWeights = new float[33];
        float gaitPhase, reloadWeight, fireWeight;
        double gaitClock;
        int preparedFrame = -1;
        bool lockedMovement;
        readonly Transform leftLeg, leftKnee, leftFoot, rightLeg, rightKnee, rightFoot;
        bool leftPlanted, rightPlanted;
        Vector3 leftAnchor, rightAnchor;
        float plantFacing;
        bool hasPlantFacing;
        internal float MotionTimeScale { get; private set; } = 1f;
        internal void ApplyRunFootPlant()
        {
            if (!Active || !owner.IsGrounded || owner.IsCrouching || lockedMovement || ArcWeight > .01f || RunWeight < .95f ||
                owner.movementInput.sqrMagnitude < .1f || motor.IsFourWayRolling || motor.LocksAirFacing)
            { leftPlanted = rightPlanted = false; return; }
            float facing = owner.lookInput.y;
            float turnRate = hasPlantFacing ? Mathf.Abs(Mathf.DeltaAngle(plantFacing, facing)) / Mathf.Max(.0001f, Time.deltaTime) : 0f;
            plantFacing = facing; hasPlantFacing = true;
            // A world-space anchor is only safe during straight running, not a curved stride.
            if (turnRate > 15f || Mathf.Abs(Mathf.Atan2(owner.movementInput.x, owner.movementInput.y) * Mathf.Rad2Deg) > 10f)
            { leftPlanted = rightPlanted = false; return; }
            Plant(leftLeg, leftKnee, leftFoot, Mathf.Repeat(gaitPhase + .08f, 1f), ref leftPlanted, ref leftAnchor);
            Plant(rightLeg, rightKnee, rightFoot, Mathf.Repeat(gaitPhase + .58f, 1f), ref rightPlanted, ref rightAnchor);
        }
        static void Plant(Transform upper, Transform knee, Transform foot, float phase, ref bool planted, ref Vector3 anchor)
        {
            if (!upper || !knee || !foot || phase >= .30f) { planted = false; return; }
            if (!planted) { anchor = foot.position; planted = true; }
            var offset = anchor - foot.position; offset.y = 0;
            if (offset.sqrMagnitude > .04f) { anchor = foot.position; return; }
            float weight = Mathf.Min(Mathf.Clamp01(phase / .04f), Mathf.Clamp01((.30f - phase) / .04f));
            var target = foot.position + offset * weight;
            var root = upper.position;
            float a = Vector3.Distance(root, knee.position), b = Vector3.Distance(knee.position, foot.position);
            Vector3 axis = target - root;
            float distance = Mathf.Clamp(axis.magnitude, Mathf.Abs(a-b) + .001f, a+b-.001f);
            if (a < .001f || b < .001f || axis.sqrMagnitude < .00001f) return;
            axis.Normalize();
            Vector3 bend = Vector3.ProjectOnPlane(knee.position-root, axis);
            if (bend.sqrMagnitude < .000001f) return;
            bend.Normalize();
            float along = (a*a + distance*distance - b*b) / (2f*distance);
            var desiredKnee = root + axis*along + bend*Mathf.Sqrt(Mathf.Max(0, a*a-along*along));
            var footRotation = foot.rotation;
            upper.rotation = Quaternion.FromToRotation(knee.position-root, desiredKnee-root) * upper.rotation;
            knee.rotation = Quaternion.FromToRotation(foot.position-knee.position, target-knee.position) * knee.rotation;
            foot.rotation = footRotation;
        }
        internal bool TryGetRootDisplacement(out Vector3 displacement)
        {
            return owner.GetComponent<RangeRobotVisual>().SampleHeroMotion(out displacement);
        }
        internal float LocomotionWeightSum { get; private set; }
        internal float MaximumWeightStep { get; private set; }
        internal float ReloadWeight => reloadWeight;
        internal float RunWeight => blendWeights[22] + blendWeights[25] + blendWeights[26];
        internal float Arc { get; private set; }
        internal float ArcWeight => blendWeights[25] + blendWeights[26] + blendWeights[27] + blendWeights[28];
        internal float TurnWeight => blendWeights[29] + blendWeights[30];
        internal float GaitPhase => gaitPhase;
        internal bool Active => layers.IsValid() && gun;
        internal string Pose { get; private set; }
        internal float AppliedAimPitch { get; private set; }
        internal string WeaponResource => profile ? profile.weaponResource : null;
        internal AnimationClip ReloadClip => profile ? gun && gun.PresentationReloadFromEmpty && profile.reloadEmpty ? profile.reloadEmpty : profile.reload : null;
        internal Transform Hand => animator ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;

        internal WendyRiflePresentation(CharacterInstance character, Animator display, HeroAnimationGraph sharedGraph)
        {
            owner = character; animator = display; shared = sharedGraph; graph = shared.Graph;
            leftLeg = display.GetBoneTransform(HumanBodyBones.LeftUpperLeg); leftKnee = display.GetBoneTransform(HumanBodyBones.LeftLowerLeg);
            leftFoot = display.GetBoneTransform(HumanBodyBones.LeftFoot);
            rightLeg = display.GetBoneTransform(HumanBodyBones.RightUpperLeg); rightKnee = display.GetBoneTransform(HumanBodyBones.RightLowerLeg);
            rightFoot = display.GetBoneTransform(HumanBodyBones.RightFoot);
            motor = character.GetComponent<CharacterMotor>();

        }

        internal void Tick(float delta)
        {
            if (preparedFrame == Time.frameCount) return;
            preparedFrame = Time.frameCount;
            delta = owner.CharacterAnimator.CombatDeltaTime(delta);
            var item = owner.CharacterItemManager ? owner.CharacterItemManager.CurrentlyUsedItem as Gun : null;
            var nextProfile = motor.SprintProfile;
            bool eligible = nextProfile && owner.CurrentHealth > 0 && item && item.AaaBinding;
            if (!eligible) { Release(); return; }
            if (gun != item) { Release(); profile = nextProfile; Bind(item); }
            if (!Active) return;
            clock += delta; fireTime += delta; takeTime += delta;
            bool aiming = owner.IsAiming && !owner.IsRunning;
            bool locked = motor && motor.GodWarControl || owner.ReadActionKeyCode(ActionCodes.LockedLocomotion) || aiming;
            lockedMovement = locked;
            // Committed shot callbacks work for both owner and remote players. Never infer firing
            // from local mouse input or change the precision-aim/camera/network action flags here.
            if (owner.IsReloading || owner.IsRunning) fireTime = 100f;
            bool hipFiring = fireTime < Mathf.Max(0f, profile.hipFirePoseHold);
            aim = Mathf.MoveTowards(aim, aiming || hipFiring ? 1f : 0f, delta * 8f);
            precisionAim = Mathf.MoveTowards(precisionAim, aiming ? 1f : 0f, delta * 8f);
            moving = Mathf.MoveTowards(moving, Mathf.Clamp01(owner.movementInput.magnitude), delta * 10f);
            float gaitLength = Mathf.Lerp(profile.hipMove[0].length, profile.aimMove[0].length, precisionAim);
            if (!locked) gaitLength = owner.IsRunning ? profile.sprint.length : profile.run.length;
            float previousPhase = gaitPhase;
            double previousGaitClock = gaitClock;
            float speedScale = motor ? motor.GetMoveSpeed(false) / Mathf.Max(.01f, motor.WalkSpeed) : 1f;
            float gunSpeed = 1f;
            if (gun.AaaBinding)
            {
                var settings = gun.AaaBinding.settings;
                if (owner.CharacterItemManager.UsePrimaryInput && !owner.IsReloading) gunSpeed = settings.fireWalkPlayerSpeed;
                else if (owner.IsAiming) gunSpeed = settings.aimWalkPlayerSpeed;
            }
            MotionTimeScale = Mathf.Max(0f, speedScale * gunSpeed);
            bool advancingGait = owner.movementInput.sqrMagnitude > .01f || moving > .001f;
            for (int i = 2; i <= 28 && !advancingGait; i++)
                if (i < 19 || i == 22 || i >= 25) advancingGait = blendWeights[i] > .0001f;
            float phaseStep = advancingGait ? delta * MotionTimeScale / Mathf.Max(.01f, gaitLength) : 0f;
            gaitClock += phaseStep;
            gaitPhase = (float)(gaitClock - System.Math.Floor(gaitClock));
            for (int i = 0; i < sources.Length; i++)
            {
                if (i >= 19 && i <= 21) continue;
                SetTargetWeight(i, 0f);
                if (i >= 29) continue;
                // One normalized gait phase keeps hip/aim/directional clips on the same footfall.
                bool gait = i >= 2 && i <= 18 || i == 22 || i >= 25 && i <= 28;
                // Advance locomotion inside Evaluate so Unity extracts the same frame's
                // motion from the actual retargeted animation stream, including wraparound.
                clips[i].SetTime(gait ? previousGaitClock * sources[i].length : clock % Mathf.Max(.01f, sources[i].length));
                clips[i].SetSpeed(gait && Time.deltaTime > 0f ? phaseStep * sources[i].length / Time.deltaTime : 0f);
            }
            bool sprint = owner.IsRunning && moving > .1f;
            SetTargetWeight(0, (1f - moving) * (1f - aim));
            SetTargetWeight(1, (1f - moving) * aim);
            if (sprint) SetTargetWeight(18, moving);
            else
            {
                float direction = Mathf.Repeat(Mathf.Atan2(-owner.movementInput.x, owner.movementInput.y) * Mathf.Rad2Deg, 360f) / 45f;
                int first = Mathf.FloorToInt(direction) % 8, second = (first + 1) % 8;
                float fraction = direction - Mathf.Floor(direction);
                SetTargetWeight(2 + first, moving * (1f - precisionAim) * (1f - fraction));
                SetTargetWeight(2 + second, moving * (1f - precisionAim) * fraction);
                SetTargetWeight(10 + first, moving * precisionAim * (1f - fraction));
                SetTargetWeight(10 + second, moving * precisionAim * fraction);
            }
            bool regular = motor && !motor.GodWarControl;
            bool moveInput = owner.movementInput.sqrMagnitude > .01f;
            bool combat = locked;
            stopTime += delta;
            stopSamples[0] += delta; stopSamples[1] += delta;
            bool canSprintStop = regular && !combat && owner.IsGrounded && !owner.IsCrouching &&
                !motor.HeroJumpSession && owner.CurrentHealth > 0 && (!motor.IsFourWayRolling || motor.RollMoveWeight > 0f);
            sprintStopSamples[0] += delta; sprintStopSamples[1] += delta;
            if (!canSprintStop || moveInput) sprintStopActive = false;
            if (canSprintStop && !sprintStopActive && (wasSprinting || (motor.HeroSprintStopping && motor.IsFourWayRolling && motor.RollFreeForward)) && !moveInput)
            {
                bool fromRoll = motor.IsFourWayRolling;
                float phase = fromRoll ? motor.RollElapsed / motor.RollProfile.directions[0].clip.length : previousPhase;
                sprintStopIndex = profile.SprintStopRightFootAt(phase, fromRoll) ? 32 : 31;
                sprintStopTime = sources[sprintStopIndex].length * profile.sprintStopStart;
                sprintStopSamples[sprintStopIndex - 31] = sprintStopTime;
                sprintStopActive = true;
                stopActive = queuedOppositeStop = false;
            }
            else if (sprintStopActive)
            {
                sprintStopTime += delta;
                if (sprintStopTime >= sources[sprintStopIndex].length * profile.sprintStopExit) sprintStopActive = false;
            }
            wasSprinting = canSprintStop && sprint && moveInput;
            bool canStop = regular && !combat && !sprint && !sprintStopActive && SprintStopWeight < .01f && owner.IsGrounded && !owner.IsCrouching &&
                !motor.IsFourWayRolling && !motor.LocksAirFacing && owner.CurrentHealth > 0;
            if (!canStop)
            { stopActive = queuedOppositeStop = false; stopTime = 100f; }
            else
            {
                if (stopActive && stopTime >= sources[stopIndex].length * profile.runStopExit)
                    stopActive = queuedOppositeStop = false;
                if (stopActive && moveInput)
                {
                    queuedOppositeStop = profile.oppositeFootReinput;
                    queuedStopRight = stopIndex == 23;
                    stopActive = false;
                }
                if (wasMoving && !moveInput)
                {
                    StopSelectedPhase = previousPhase;
                    bool right = queuedOppositeStop ? queuedStopRight : profile.StopRightFootAt(previousPhase);
                    queuedOppositeStop = false;
                    stopIndex = right ? 24 : 23;
                    stopTime = stopSamples[stopIndex - 23] = 0f;
                    stopActive = true;
                    leftPlanted = rightPlanted = false;
                }
            }
            wasMoving = canStop && moveInput;
            if (regular && !combat && !sprint)
            {
                for (int i = 0; i < 19; i++) SetTargetWeight(i, 0f);
                // Enter the selected stop directly; one mixer blend supplies the transition.
                // Do not attenuate it again by the smoothed movement envelope.
                SetTargetWeight(22, stopActive ? 0f : moving);
                SetTargetWeight(stopIndex, stopActive ? 1f : 0f);
                SetTargetWeight(0, stopActive ? 0f : 1f - moving);
            }
            bool turning = motor.SprintTurning;
            float arcTarget = !locked && moveInput && !turning
                ? Mathf.Clamp(Mathf.Atan2(owner.movementInput.x, owner.movementInput.y) / Mathf.PI * profile.arcSensitivity, -1f, 1f) : 0f;
            Arc = Mathf.Lerp(Arc, arcTarget, Mathf.Clamp01(delta * profile.arcLerpSpeed));
            if (!locked && !turning && !stopActive)
            {
                int center = sprint ? 18 : 22;
                float weight = targetWeights[center];
                SetTargetWeight(center, weight * (1f - Mathf.Abs(Arc)));
                SetTargetWeight(sprint ? 27 : 25, weight * Mathf.Max(0f, -Arc));
                SetTargetWeight(sprint ? 28 : 26, weight * Mathf.Max(0f, Arc));
            }
            if (turning)
            {
                for (int i = 0; i < targetWeights.Length; i++) SetTargetWeight(i, 0f);
                SetTargetWeight(motor.SprintTurnRight ? 30 : 29, 1f);
                Arc = 0f;
            }
            if (sprintStopActive)
            {
                for (int i = 0; i < targetWeights.Length; i++) SetTargetWeight(i, 0f);
                SetTargetWeight(sprintStopIndex, 1f);
                Arc = 0f;
            }
            for (int i = 31; i <= 32; i++)
            {
                clips[i].SetTime(Mathf.Min(sprintStopSamples[i - 31], sources[i].length));
                clips[i].SetSpeed(sprintStopSamples[i - 31] < sources[i].length && Time.deltaTime > 0f ? delta / Time.deltaTime : 0f);
            }
            // Hold the last sample during blend-out; never jump a fading turn back to frame zero.
            if (turning)
                clips[motor.SprintTurnRight ? 30 : 29].SetTime(motor.SprintTurnTime);
            clips[23].SetTime(Mathf.Min(stopSamples[0], sources[23].length));
            clips[24].SetTime(Mathf.Min(stopSamples[1], sources[24].length));
            ApplyLocomotionWeights(delta);
            if (owner.IsReloading && !reloading) reloadTime = 0f;
            reloading = owner.IsReloading;
            reloadTime += delta;
            clips[19].SetTime(Mathf.Min(takeTime, sources[19].length));
            clips[20].SetTime(Mathf.Min(fireTime, sources[20].length));
            if (reloading && gun.PresentationReloadStarted >= 0f)
                reloadTime = Mathf.Max(0f, Time.time - gun.PresentationReloadStarted);
            var reloadClip = ReloadClip;
            float reloadSample = gun.PresentationIncrementalReload ? reloadTime :
                reloadTime / Mathf.Max(.01f, gun.PresentationReloadDuration) * reloadClip.length;
            clips[21].SetTime(Mathf.Min(reloadSample, sources[21].length));
            emptyReload.SetTime(Mathf.Min(reloadSample, (profile.reloadEmpty ? profile.reloadEmpty : profile.reload).length));
            bool empty = gun.PresentationReloadFromEmpty;
            reloadMixer.SetInputWeight(0, empty ? 0f : 1f);
            reloadMixer.SetInputWeight(1, empty ? 1f : 0f);
            hipUpper.SetTime(clock % Mathf.Max(.01f, profile.aimIdle.length));
            layers.SetInputWeight(1, aim);
            layers.SetInputWeight(2, takeTime < sources[19].length ? Mathf.Clamp01((sources[19].length - takeTime) * 8f) : 0f);
            float fireTarget = !reloading && fireTime < sources[20].length ? Mathf.Clamp01((sources[20].length - fireTime) * 15f) : 0f;
            fireWeight = Mathf.MoveTowards(fireWeight, fireTarget, delta / Mathf.Max(.01f, profile.fireBlend));
            reloadWeight = Mathf.MoveTowards(reloadWeight, reloading ? 1f : 0f, delta / Mathf.Max(.01f, reloading ? profile.reloadBlend : profile.reloadBlendOut));
            layers.SetInputWeight(3, fireWeight);
            layers.SetInputWeight(4, reloadWeight);
            Pose = sprintStopActive ? (SprintStopRight ? "SprintStopR" : "SprintStopL") : turning ? "SprintTurn" : reloading ? "Reload" : sprint ? "Sprint" : aiming ? "Aim" : aim > .001f ? "HipFire" :
                regular && !locked && moveInput ? "Run" : locked && moveInput ? "LockedMove" :
                stopActive ? (RunStopRight ? "RunStopR" : "RunStopL") : "Stand";
            shared.Weight(0, owner.IsGrounded && !owner.IsCrouching ? 1f : 0f);
            if (!owner.IsGrounded || owner.IsCrouching) Pose = owner.IsCrouching ? "CrouchFallback" : "AirFallback";
        }

        internal void ApplyAimPitch()
        {
            AppliedAimPitch = 0f;
            if (!Active || !shared.BaseActive) return;
            var root = animator.transform;
            if (aim > .001f && reloadWeight < .001f && takeTime >= sources[19].length && !owner.IsRunning)
            {
                var chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
                if (!chest) chest = animator.GetBoneTransform(HumanBodyBones.Chest);
                if (chest)
                {
                    AppliedAimPitch = owner.lookInput.x * aim;
                    chest.rotation = Quaternion.AngleAxis(AppliedAimPitch, root.right) * chest.rotation;
                }
            }
        }

        void SetTargetWeight(int index, float weight) => targetWeights[index] = weight;

        void ApplyLocomotionWeights(float delta)
        {
            float targetSum = 0f;
            for (int i = 0; i < targetWeights.Length; i++) targetSum += targetWeights[i];
            if (targetSum <= .0001f) { targetWeights[0] = 1f; targetSum = 1f; }
            float duration = sprintStopActive ? profile.sprintStopBlend : SprintStopWeight > .001f
                ? (owner.movementInput.sqrMagnitude > .01f ? profile.runStopReinputBlend : profile.sprintStopIdleBlend) : motor.SprintTurning ? profile.turnEnterBlend : TurnWeight > .001f ? profile.turnExitBlend : stopActive ? profile.runStopBlend : RunStopWeight > .001f ? profile.runStopReinputBlend : profile.locomotionBlend;
            float blend = 1f - Mathf.Exp(-Mathf.Max(0f, delta) * 3f / Mathf.Max(.01f, duration));
            LocomotionWeightSum = MaximumWeightStep = 0f;
            for (int i = 0; i < blendWeights.Length; i++)
            {
                if (i >= 19 && i <= 21) continue;
                float previous = blendWeights[i];
                blendWeights[i] = Mathf.Lerp(previous, targetWeights[i] / targetSum, blend);
                MaximumWeightStep = Mathf.Max(MaximumWeightStep, Mathf.Abs(blendWeights[i] - previous));
                LocomotionWeightSum += blendWeights[i];
                locomotion.SetInputWeight(i, blendWeights[i]);
            }
        }

        void Bind(Gun next)
        {
            if (!Hand || !next.ItemModel || !profile.armsMask) return;
            Arc = 0f;
            gun = next; clock = 0; aim = precisionAim = moving = 0; takeTime = 0; fireTime = 100f; reloading = false;
            wasMoving = wasSprinting = sprintStopActive = stopActive = queuedOppositeStop = false;
            sprintStopSamples[0] = sprintStopSamples[1] = 100f; stopTime = 100f;
            stopSamples[0] = stopSamples[1] = 100f;
            gaitClock = 0;
            gaitPhase = reloadWeight = fireWeight = 0f;
            System.Array.Clear(targetWeights, 0, targetWeights.Length);
            System.Array.Clear(blendWeights, 0, blendWeights.Length);
            blendWeights[0] = 1f;
            // Preserve the source aim stance and its calibrated hand basis. Only the
            // action overlays (including reload) are masked to arms and fingers.
            sources[0] = profile.idle; sources[1] = profile.aimIdle;
            if (profile.hipMove == null || profile.hipMove.Length != 8 || profile.aimMove == null || profile.aimMove.Length != 8) { gun = null; return; }
            for (int i = 0; i < 8; i++) { sources[2+i] = profile.hipMove[i]; sources[10+i] = profile.aimMove[i]; }
            sources[18] = profile.sprint; sources[19] = profile.take; sources[20] = profile.fire; sources[21] = profile.reload;
            sources[22] = profile.run;
            sources[23] = profile.runStopLeft ? profile.runStopLeft : profile.runStop;
            sources[24] = profile.runStopRight ? profile.runStopRight : profile.runStop;
            sources[25] = profile.runArcLeft ? profile.runArcLeft : profile.run;
            sources[26] = profile.runArcRight ? profile.runArcRight : profile.run;
            sources[27] = profile.sprintArcLeft ? profile.sprintArcLeft : profile.sprint;
            sources[28] = profile.sprintArcRight ? profile.sprintArcRight : profile.sprint;
            sources[29] = profile.sprintTurnLeft ? profile.sprintTurnLeft : profile.sprint;
            sources[30] = profile.sprintTurnRight ? profile.sprintTurnRight : profile.sprint;
            sources[31] = profile.sprintStopLeft ? profile.sprintStopLeft : sources[23];
            sources[32] = profile.sprintStopRight ? profile.sprintStopRight : sources[24];
            for (int i = 0; i < sources.Length; i++) if (!sources[i]) { gun = null; return; }

            locomotion = AnimationMixerPlayable.Create(graph, sources.Length);
            layers = AnimationLayerMixerPlayable.Create(graph, 5);
            graph.Connect(locomotion, 0, layers, 0); layers.SetInputWeight(0, 1f);
            upperBody = profile.armsMask;
            reloadMixer = AnimationMixerPlayable.Create(graph, 2);
            graph.Connect(reloadMixer, 0, layers, 4); layers.SetLayerMaskFromAvatarMask(4, upperBody);
            for (int i = 0; i < sources.Length; i++)
            {
                clips[i] = AnimationClipPlayable.Create(graph, sources[i]);
                clips[i].SetApplyFootIK(false); clips[i].SetApplyPlayableIK(false);
                clips[i].SetSpeed(0);
                if (i < 19 || i >= 22) graph.Connect(clips[i], 0, locomotion, i);
                else if (i == 21) graph.Connect(clips[i], 0, reloadMixer, 0);
                else { graph.Connect(clips[i], 0, layers, i - 17); layers.SetLayerMaskFromAvatarMask((uint)(i - 17), upperBody); }
            }
            emptyReload = AnimationClipPlayable.Create(graph, profile.reloadEmpty ? profile.reloadEmpty : profile.reload);
            emptyReload.SetSpeed(0); emptyReload.SetApplyFootIK(false); emptyReload.SetApplyPlayableIK(false);
            graph.Connect(emptyReload, 0, reloadMixer, 1);
            hipUpper = AnimationClipPlayable.Create(graph, profile.aimIdle); hipUpper.SetSpeed(0);
            hipUpper.SetApplyFootIK(false); hipUpper.SetApplyPlayableIK(false);
            graph.Connect(hipUpper, 0, layers, 1); layers.SetLayerMaskFromAvatarMask(1, upperBody);
            shared.Connect(layers, 0);
            weapon = next.ItemModel.transform;
            previousParent = weapon.parent; previousPosition = weapon.localPosition;
            previousRotation = weapon.localRotation; previousScale = weapon.localScale;
            weaponAnimator = weapon.GetComponent<Animator>();
            if (weaponAnimator) { previousWeaponAnimator = weaponAnimator.enabled; weaponAnimator.enabled = false; }
            // Same AAA mesh, remove the TPS soldier's old grip correction before applying the source mount.
            mesh = weapon.Find(profile.modelChild);
            if (mesh)
            {
                meshPosition = mesh.localPosition; meshRotation = mesh.localRotation; meshScale = mesh.localScale;
                mesh.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity); mesh.localScale = Vector3.one;
            }
            weapon.SetParent(Hand, false);
            weapon.SetLocalPositionAndRotation(profile.handPosition, Quaternion.Euler(profile.handEuler));
            weapon.localScale = Vector3.one * profile.handScale;
        }

        internal void Shot(Item source) { if (source == gun) fireTime = 0f; }
        internal void Release(Item source) { if (source == gun) Release(); }
        internal void Release()
        {
            leftPlanted = rightPlanted = hasPlantFacing = false;
            if (layers.IsValid()) shared.Remove(layers, 0);
            upperBody = null; // Shared source asset; owned by Resources, never destroy it.
            // A dropped/reassigned gun already has a new owner: do not restore its old hierarchy.
            if (weapon && weapon.parent == Hand)
            {
                weapon.SetParent(previousParent ? previousParent : gun ? gun.transform : null, false);
                weapon.SetLocalPositionAndRotation(previousPosition, previousRotation); weapon.localScale = previousScale;
            }
            if (mesh) { mesh.SetLocalPositionAndRotation(meshPosition, meshRotation); mesh.localScale = meshScale; }
            if (weaponAnimator) weaponAnimator.enabled = previousWeaponAnimator;
            gun = null; weapon = mesh = null; weaponAnimator = null;
            AppliedAimPitch = 0f;
        }
    }
}
