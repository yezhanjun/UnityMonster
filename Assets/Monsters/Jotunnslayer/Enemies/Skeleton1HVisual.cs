using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Jotunnslayer
{
    [RequireComponent(typeof(Animator))]
    public sealed class Skeleton1HVisual : MonoBehaviour
    {
        public enum Motion
        {
            Spawn,
            Idle,
            Walk,
            Attack,
            AttackAlternate,
            PhaseChange,
            BossHorns,
            BossSummon,
            BossJump,
            HybCircles,
            HybSummon,
            Hit,
            AlfheimWave,
            AlfheimSpikes,
            AlfheimBurrow,
            AlfheimSplash,
            AlfheimSummon,
            ConstructNearHammer,
            ConstructFarHammer,
            ConstructLeftFlame,
            ConstructRightFlame,
            ConstructLaser
        }

        [SerializeField] AnimationClip spawn;
        [SerializeField] AnimationClip idle;
        [SerializeField] AnimationClip walk;
        [SerializeField] AnimationClip attack;
        [SerializeField] AnimationClip hit;
        AnimationClip alternateAttack;
        AnimationClip phaseChange;
        AnimationClip bossHorns;
        AnimationClip bossSummon;
        AnimationClip bossJump;
        AnimationClip hybCircles;
        AnimationClip hybSummon;
        AnimationClip alfheimWave;
        AnimationClip alfheimSpikes;
        Renderer[] modelRenderers;
        AnimationClip alfheimBurrow;
        AnimationClip alfheimSplash;
        AnimationClip alfheimSummon;

        AnimationClip constructNearHammer;
        AnimationClip constructFarHammer;
        AnimationClip constructLeftFlame;
        AnimationClip constructRightFlame;
        AnimationClip constructLaser;

        internal void BindConstructSkills(AnimationClip near, AnimationClip far, AnimationClip left, AnimationClip right, AnimationClip laser)
        {
            constructNearHammer = near;
            constructFarHammer = far;
            constructLeftFlame = left;
            constructRightFlame = right;
            constructLaser = laser;
        }

        Animator animator;
        Material ownedMaterial;
        Material secondaryMaterial;
        Material returnFadeMaterial;
        Color returnFadeColor;
        PlayableGraph graph;
        AnimationPlayableOutput output;
        AnimationClipPlayable currentClip;
        Motion currentMotion;
        AnimationMixerPlayable transitionMixer;
        float transitionElapsed, transitionDuration, transitionStartedAt;

        internal Motion CurrentMotion => currentMotion;
        internal double CurrentClipTime => draugControllerLease != null ? (draugCastRequested ? draugCastSeconds : draugArrowSeconds) : currentClip.IsValid() ? currentClip.GetTime() : 0;
        DraugControllerResources.Lease draugControllerLease;
        int draugControllerIndex;
        bool draugArrowRequested, draugArrowStarted, draugArrowReturning, draugArrowFinished;
        float draugArrowSeconds;
        bool draugCastRequested, draugCastStarted, draugCastReturning, draugCastFinished;
        float draugCastSeconds;
        internal const float DraugCastReleaseSeconds = .49205490946769714f;
        internal const float DraugCastEndSeconds = 1.1088453531265259f;
        static readonly int DraugCastState = Animator.StringToHash("base.cast_tree");
        internal bool DraugCastStarted => draugCastStarted;
        internal bool DraugCastReturning => draugCastReturning;
        internal bool DraugCastFinished => draugCastFinished;
        internal bool DraugCastReleased => draugCastStarted && draugCastSeconds >= DraugCastReleaseSeconds;
        internal bool DraugCastEnded => draugCastStarted && (draugCastSeconds >= DraugCastEndSeconds || draugCastFinished);
        static readonly int DraugIdleState = Animator.StringToHash("base.idle");
        static readonly int DraugChaseState = Animator.StringToHash("base.chase");
        static readonly int[] DraugAttackStates = {
            Animator.StringToHash("base.attack.attack_01"), Animator.StringToHash("base.attack.attack_02"), Animator.StringToHash("base.attack.attack_03") };
        internal bool DraugControllerActive => draugControllerLease != null;
        internal bool DraugArrowStarted => draugArrowStarted;
        internal bool DraugArrowReturning => draugArrowReturning;
        internal bool DraugArrowFinished => draugArrowFinished;

        internal bool RequestDraugArrow(int index)
        {
            if (!isActiveAndEnabled || !IsConfigured || index < 0 || index > 2 || !DraugArrowClip(index)) return false;
            if (draugArrowRequested && !draugArrowFinished || draugCastRequested && !draugCastFinished) return false;
            EnsureDraugController();
            ResetDraugObservation();
            draugControllerIndex = index;
            draugArrowRequested = true;
            animator.SetInteger("attack_index", index);
            animator.SetTrigger("attack");
            return true;
        }

        void EnsureDraugController()
        {
            if (draugControllerLease == null)
            {
                bool chasing = currentMotion == Motion.Walk;
                float length = chasing ? walk.length : idle.length;
                float normalizedTime = currentClip.IsValid() ? (float)(currentClip.GetTime() / length) : 0f;
                if (graph.IsValid()) graph.Destroy();
                currentClip = default; transitionMixer = default;
                transitionElapsed = transitionDuration = 0f;
                draugControllerLease = DraugControllerResources.Acquire(animator);
                animator.applyRootMotion = false; animator.fireEvents = false;
                animator.Rebind(); animator.Update(0);
                animator.SetFloat("idle_anim_speed", 1f);
                animator.Play(chasing ? DraugChaseState : DraugIdleState, 0, normalizedTime);
                animator.Update(0);
            }
        }

        internal bool RequestDraugCast()
        {
            if (!isActiveAndEnabled || !IsConfigured || draugArrowRequested && !draugArrowFinished || draugCastRequested && !draugCastFinished) return false;
            EnsureDraugController();
            ResetDraugObservation();
            draugCastRequested = true;
            animator.SetFloat("cast_sequence", 1f);
            animator.SetTrigger("cast");
            return true;
        }

        internal void SampleDraugCast()
        {
            if (draugControllerLease == null || !draugCastRequested) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool transitioning = animator.IsInTransition(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            bool inCurrent = current.fullPathHash == DraugCastState;
            if (inCurrent || transitioning && next.fullPathHash == DraugCastState)
            {
                draugCastStarted = true;
                currentMotion = Motion.Attack;
                var state = inCurrent ? current : next;
                draugCastSeconds = Mathf.Max(draugCastSeconds, state.normalizedTime * attack.length);
            }
            if (draugCastStarted && inCurrent && transitioning && next.fullPathHash == DraugIdleState)
                draugCastReturning = true;
            if (draugCastStarted && !transitioning && current.fullPathHash == DraugIdleState)
            {
                // A final Animator evaluation may deliver CastEnd after the cast state disappears.
                draugCastReturning = draugCastFinished = true;
                draugCastSeconds = Mathf.Max(draugCastSeconds, attack.length);
                currentMotion = Motion.Idle;
            }
        }

        // Called after Animator evaluation; no clip-info arrays or per-frame allocation.
        internal void SampleDraugArrow()
        {
            if (draugControllerLease == null || !draugArrowRequested) return;
            var current = animator.GetCurrentAnimatorStateInfo(0);
            bool transitioning = animator.IsInTransition(0);
            var next = animator.GetNextAnimatorStateInfo(0);
            int attackState = DraugAttackStates[draugControllerIndex];
            bool inCurrent = current.fullPathHash == attackState;
            bool inNext = transitioning && next.fullPathHash == attackState;
            if (inCurrent || inNext)
            {
                draugArrowStarted = true;
                currentMotion = Motion.Attack;
                var state = inCurrent ? current : next;
                draugArrowSeconds = Mathf.Max(draugArrowSeconds, state.normalizedTime * DraugArrowClip(draugControllerIndex).length);
            }
            if (draugArrowStarted && inCurrent && transitioning && next.fullPathHash == DraugIdleState)
                draugArrowReturning = true;
            if (draugArrowStarted && !transitioning && current.fullPathHash == DraugIdleState)
            {
                draugArrowReturning = draugArrowFinished = true;
                draugArrowSeconds = Mathf.Max(draugArrowSeconds, DraugArrowClip(draugControllerIndex).length * DraugArrowTimeline.ExitNormalizedTime);
                currentMotion = Motion.Idle;
            }
        }

        void LateUpdate() { SampleDraugArrow(); SampleDraugCast(); }
        void ResetDraugObservation()
        {
            draugArrowRequested = draugArrowStarted = draugArrowReturning = draugArrowFinished = false;
            draugArrowSeconds = 0f;
            draugCastRequested = draugCastStarted = draugCastReturning = draugCastFinished = false;
            draugCastSeconds = 0f;
        }
        void ReleaseDraugController()
        {
            draugControllerLease?.Dispose();
            draugControllerLease = null;
            ResetDraugObservation();
        }

        internal AnimationClip PhaseChangeClip => phaseChange;
        AnimationClip[] draugArrows;
        internal AnimationClip DraugArrowClip(int index)
        {
            if(draugArrows==null)
            {
                draugArrows=new AnimationClip[3];
                for(int i=0;i<3;i++)draugArrows[i]=Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/attack_0"+(i+1));
            }
            return draugArrows[Mathf.Clamp(index,0,2)];
        }
        internal float IdleDuration => idle ? idle.length : 0f;
        internal float AttackDuration => attack ? attack.length : 0f;
        internal float SpawnDuration => spawn ? spawn.length : 0f;

        void Awake()
        {
            animator = GetComponent<Animator>();
        }

        internal void Bind(AnimationClip spawnClip, AnimationClip idleClip, AnimationClip walkClip,
            AnimationClip attackClip, AnimationClip hitClip, Material material, AnimationClip alternate = null, AnimationClip change = null, AnimationClip horns = null, AnimationClip summon = null, AnimationClip jump = null, Material secondary = null, AnimationClip circles = null, AnimationClip hybVolley = null, AnimationClip wave = null, AnimationClip spikes = null, AnimationClip burrow = null, AnimationClip splash = null, AnimationClip spiderSummon = null)
        {
            spawn = spawnClip;
            idle = idleClip;
            walk = walkClip;
            attack = attackClip;
            hit = hitClip;
            alternateAttack = alternate;
            phaseChange = change;
            bossHorns = horns;
            bossSummon = summon;
            bossJump = jump;
            hybCircles = circles;
            hybSummon = hybVolley;
            alfheimWave = wave;
            alfheimSpikes = spikes;
            alfheimBurrow = burrow;
            alfheimSplash = splash;
            alfheimSummon = spiderSummon;
            if (burrow) modelRenderers = GetComponentsInChildren<Renderer>(true);
            ownedMaterial = material;
            secondaryMaterial = secondary;
        }

        internal void ApplyAlfheimMaterials(GameObject model)
        {
            foreach (var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    slots[i] = secondaryMaterial && slots[i] && slots[i].name.Contains("jotunn_spider_bag")
                        ? secondaryMaterial : ownedMaterial;
                renderer.sharedMaterials = slots;
            }
        }

        internal void SetModelHidden(bool hidden)
        {
            modelRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in modelRenderers)
                if (renderer) renderer.forceRenderingOff = hidden;
        }

        internal void BeginHyboreaReturnFade()
        {
            if (!ownedMaterial || returnFadeMaterial) return;
            returnFadeMaterial = new Material(ownedMaterial);
            returnFadeColor = ownedMaterial.GetColor("_BaseColor");
            returnFadeMaterial.SetFloat("_Surface", 1f);
            returnFadeMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            returnFadeMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            returnFadeMaterial.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            returnFadeMaterial.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            returnFadeMaterial.SetFloat("_ZWrite", 0f);
            returnFadeMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            returnFadeMaterial.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            returnFadeMaterial.SetOverrideTag("RenderType", "Transparent");
            returnFadeMaterial.SetShaderPassEnabled("ShadowCaster", false);
            returnFadeMaterial.SetShaderPassEnabled("DepthOnly", false);
            returnFadeMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            SetHyboreaReturnFade(0f);
            SwapModelMaterial(ownedMaterial, returnFadeMaterial);
            SetModelHidden(false);
        }

        internal void SetHyboreaReturnFade(float fraction)
        {
            if (!returnFadeMaterial) return;
            Color color = returnFadeColor;
            color.a *= Mathf.Clamp01(fraction);
            returnFadeMaterial.SetColor("_BaseColor", color);
        }

        internal void EndHyboreaReturnFade()
        {
            if (!returnFadeMaterial) return;
            SwapModelMaterial(returnFadeMaterial, ownedMaterial);
            Destroy(returnFadeMaterial);
            returnFadeMaterial = null;
        }

        void SwapModelMaterial(Material from, Material to)
        {
            modelRenderers ??= GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in modelRenderers)
            {
                if (!renderer) continue;
                var slots = renderer.sharedMaterials;
                for (int i = 0; i < slots.Length; i++)
                    if (slots[i] == from) slots[i] = to;
                renderer.sharedMaterials = slots;
            }
        }

        void OnDestroy()
        {
            if (returnFadeMaterial) Destroy(returnFadeMaterial);
            if (ownedMaterial) Destroy(ownedMaterial);
            if (secondaryMaterial) Destroy(secondaryMaterial);
        }

        public bool IsConfigured => animator && animator.avatar && animator.avatar.isValid &&
            spawn && idle && walk && attack && hit && spawn.humanMotion == animator.avatar.isHuman &&
            idle.humanMotion == animator.avatar.isHuman && walk.humanMotion == animator.avatar.isHuman &&
            attack.humanMotion == animator.avatar.isHuman && hit.humanMotion == animator.avatar.isHuman;

        public bool Play(Motion motion, bool restart = false, float speed = 1f, AnimationClip overrideClip = null,
            float startSeconds = 0f, float blendSeconds = 0f, bool freezePrevious = false)
        {
            if (float.IsNaN(startSeconds) || float.IsInfinity(startSeconds) || startSeconds < 0f ||
                float.IsNaN(blendSeconds) || float.IsInfinity(blendSeconds) || blendSeconds < 0f)
                throw new System.ArgumentOutOfRangeException(nameof(startSeconds), "Animation timing must be finite and nonnegative");
            if (!isActiveAndEnabled || !IsConfigured) return false;

            if (draugControllerLease != null)
            {
                if (!overrideClip && speed == 1f && blendSeconds == 0f && !freezePrevious &&
                    (motion == Motion.Idle || motion == Motion.Walk))
                {
                    if (motion == currentMotion && !restart) return true;
                    ResetDraugObservation();
                    animator.ResetTrigger("attack"); animator.ResetTrigger("cast"); animator.ResetTrigger("idle"); animator.ResetTrigger("chase");
                    if (restart)
                    {
                        animator.Play(motion == Motion.Walk ? DraugChaseState : DraugIdleState, 0,
                            startSeconds / (motion == Motion.Walk ? walk.length : idle.length));
                        animator.Update(0);
                    }
                    else animator.SetTrigger(motion == Motion.Walk ? "chase" : "idle");
                    currentMotion = motion;
                    return true;
                }
                ReleaseDraugController();
            }
            AnimationClip clip = motion switch
            {
                Motion.Spawn => spawn,
                Motion.Idle => idle,
                Motion.Walk => walk,
                Motion.Attack => attack,
                Motion.ConstructNearHammer => constructNearHammer,
                Motion.ConstructFarHammer => constructFarHammer,
                Motion.ConstructLeftFlame => constructLeftFlame,
                Motion.ConstructRightFlame => constructRightFlame,
                Motion.ConstructLaser => constructLaser,
                Motion.AttackAlternate => alternateAttack,
                Motion.PhaseChange => phaseChange,
                Motion.BossHorns => bossHorns,
                Motion.BossSummon => bossSummon,
                Motion.BossJump => bossJump,
                Motion.HybCircles => hybCircles,
                Motion.HybSummon => hybSummon,
                Motion.AlfheimWave => alfheimWave,
                Motion.AlfheimSpikes => alfheimSpikes,
                Motion.AlfheimBurrow => alfheimBurrow,
                Motion.AlfheimSplash => alfheimSplash,
                Motion.AlfheimSummon => alfheimSummon,
                Motion.Hit => hit,
                _ => null
            };
            if(overrideClip)clip=overrideClip;
            if (!clip) return false;
            if (currentClip.IsValid() && motion == currentMotion && !restart) return true;

            if (!graph.IsValid())
            {
                animator.applyRootMotion = false;
                // The combat controller owns timing. Original animation events target game-specific scripts.
                animator.fireEvents = false;
                graph = PlayableGraph.Create(nameof(Skeleton1HVisual));
                graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
                output = AnimationPlayableOutput.Create(graph, "Skeleton", animator);
            }

            var previousRoot = output.GetSourcePlayable();
            currentClip = AnimationClipPlayable.Create(graph, clip);
            currentClip.SetTime(startSeconds);
            currentClip.SetSpeed(speed);
            transitionMixer = default;
            transitionElapsed = 0f;
            transitionDuration = blendSeconds;
            transitionStartedAt = Time.time;
            if (blendSeconds > 0f && previousRoot.IsValid())
            {
                // Preserve the currently blended pose if another blend interrupts it.
                // The retained subtree is destroyed when this transition completes.
                if (freezePrevious) previousRoot.SetSpeed(0);
                transitionMixer = AnimationMixerPlayable.Create(graph, 2);
                graph.Connect(previousRoot, 0, transitionMixer, 0);
                graph.Connect(currentClip, 0, transitionMixer, 1);
                transitionMixer.SetInputWeight(0, 1f);
                transitionMixer.SetInputWeight(1, 0f);
                output.SetSourcePlayable(transitionMixer);
            }
            else
            {
                output.SetSourcePlayable(currentClip);
                if (previousRoot.IsValid()) graph.DestroySubgraph(previousRoot);
            }
            currentMotion = motion;
            graph.Play();
            return true;
        }

        // Absolute scaled time avoids charging a full frame when Play is called earlier in this same frame.
        void Update() => AdvanceTransition(Time.time - transitionStartedAt - transitionElapsed);

        internal void AdvanceTransition(float deltaTime)
        {
            if (!transitionMixer.IsValid()) return;
            transitionElapsed += Mathf.Max(0f, deltaTime);
            float weight = Mathf.Clamp01(transitionElapsed / transitionDuration);
            transitionMixer.SetInputWeight(0, 1f - weight);
            transitionMixer.SetInputWeight(1, weight);
            if (weight < 1f) return;
            var previousRoot = transitionMixer.GetInput(0);
            // Detach the surviving clip before destroying the old tree.
            transitionMixer.DisconnectInput(1);
            transitionMixer.DisconnectInput(0);
            output.SetSourcePlayable(currentClip);
            graph.DestroySubgraph(previousRoot);
            graph.DestroyPlayable(transitionMixer);
            transitionMixer = default;
        }

        void OnDisable()
        {
            EndHyboreaReturnFade();
            ReleaseDraugController();
            if (graph.IsValid()) graph.Destroy();
            currentClip = default;
            transitionMixer = default;
            transitionElapsed = transitionDuration = 0f;
        }
    }
}
