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

        internal Motion CurrentMotion => currentMotion;
        internal AnimationClip PhaseChangeClip => phaseChange;
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

        public bool Play(Motion motion, bool restart = false, float speed = 1f)
        {
            if (!isActiveAndEnabled || !IsConfigured) return false;

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

            var previousClip = currentClip;
            currentClip = AnimationClipPlayable.Create(graph, clip);
            currentClip.SetSpeed(speed);
            output.SetSourcePlayable(currentClip);
            if (previousClip.IsValid()) graph.DestroyPlayable(previousClip);
            currentMotion = motion;
            graph.Play();
            return true;
        }

        void OnDisable()
        {
            EndHyboreaReturnFade();
            if (graph.IsValid()) graph.Destroy();
            currentClip = default;
        }
    }
}
