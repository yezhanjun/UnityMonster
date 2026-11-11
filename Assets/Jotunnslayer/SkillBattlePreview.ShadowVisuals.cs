using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        private const string ShadowResourceFolder = "Jotunnslayer/ShadowImages/";
        private readonly List<ShadowImagePresentation> shadowPresentations = new List<ShadowImagePresentation>(14);
        private GameObject shadowModel;
        private AnimationClip shadowAttackClip, shadowIdleClip;
        private Material shadowMaterial;
        private bool shadowVisualAssetsAttempted;

        private sealed class ShadowImagePresentation
        {
            internal GameObject body;
            internal PlayableGraph graph;
            internal AnimationClipPlayable attack, idle;
            internal AnimationPlayableOutput output;
            internal float started;
            internal bool inUse, attacking;
        }

        private bool LoadShadowVisualAssets()
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null) return false;
            if (shadowVisualAssetsAttempted) return shadowModel && shadowMaterial && shadowAttackClip && shadowIdleClip;
            shadowVisualAssetsAttempted = true;
            shadowModel = Resources.Load<GameObject>(ShadowResourceFolder + "valkyrie_soulmate_skill_D");
            var attack = Resources.Load<AnimationClip>(ShadowResourceFolder + "valkyrie_ghost_attack");
            var idle = Resources.Load<AnimationClip>(ShadowResourceFolder + "valkyrie_ghost_idle");
            var shader = Resources.Load<Shader>(ShadowResourceFolder + "ShadowImage");
            if (!shadowModel || !attack || !idle || !shader || !shader.isSupported)
            {
                Debug.LogWarning("Shadow Images original presentation assets unavailable.", this);
                return false;
            }
            // Presentation never dispatches original cast events. The server runtime owns hits.
            shadowAttackClip = Instantiate(attack);
            shadowIdleClip = Instantiate(idle);
            shadowAttackClip.events = System.Array.Empty<AnimationEvent>();
            shadowIdleClip.events = System.Array.Empty<AnimationEvent>();
            shadowMaterial = new Material(shader);
            shadowMaterial.SetTexture("_AlbedoMap", Resources.Load<Texture2D>(ShadowResourceFolder + "valkyrie_shadowimage_dm"));
            shadowMaterial.SetTexture("_EmissionMap", Resources.Load<Texture2D>(ShadowResourceFolder + "valkyrie_shadowimage_em"));
            shadowMaterial.SetTexture("_OpacityMask", Resources.Load<Texture2D>(ShadowResourceFolder + "valkyrie_shadowimage_op"));
            return true;
        }

        private ShadowImagePresentation AcquireShadowPresentation(Vector3 position, Quaternion rotation, bool attack, float now)
        {
            if (!LoadShadowVisualAssets()) return null;
            ShadowImagePresentation visual = null;
            for (int i = 0; i < shadowPresentations.Count; i++)
                if (!shadowPresentations[i].inUse) { visual = shadowPresentations[i]; break; }
            if (visual == null)
            {
                var body = Instantiate(shadowModel, transform);
                body.name = "Shadow Image Original Model";
                foreach (var animator in body.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
                Transform modelRoot = null;
                foreach (var node in body.GetComponentsInChildren<Transform>(true))
                    if (node.name == "valkyrie_soul_mate") { modelRoot = node; break; }
                if (!modelRoot)
                {
                    Debug.LogWarning("Shadow Images original animation root is missing.", body);
                    foreach (var node in body.GetComponentsInChildren<Transform>(true))
                        Debug.Log("Shadow import node: " + node.name, body);
                    Destroy(body);
                    return null;
                }
                var targetAnimator = modelRoot.GetComponent<Animator>();
                if (!targetAnimator) targetAnimator = modelRoot.gameObject.AddComponent<Animator>();
                targetAnimator.enabled = true;
                targetAnimator.runtimeAnimatorController = null;
                targetAnimator.applyRootMotion = false;
                targetAnimator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                foreach (var renderer in body.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                {
                    renderer.sharedMaterial = shadowMaterial;
                    renderer.updateWhenOffscreen = true;
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                visual = new ShadowImagePresentation { body = body };
                visual.graph = PlayableGraph.Create("Shadow Image Presentation");
                visual.graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                visual.attack = AnimationClipPlayable.Create(visual.graph, shadowAttackClip);
                visual.idle = AnimationClipPlayable.Create(visual.graph, shadowIdleClip);
                visual.output = AnimationPlayableOutput.Create(visual.graph, "Original ghost", targetAnimator);
                visual.graph.Play();
                shadowPresentations.Add(visual);
            }
            visual.inUse = true;
            visual.attacking = attack;
            visual.started = now;
            visual.body.transform.SetPositionAndRotation(position, rotation);
            visual.body.SetActive(true);
            visual.output.SetSourcePlayable(attack ? visual.attack : visual.idle);
            UpdateShadowPresentation(visual, now);
            return visual;
        }

        private void UpdateShadowPresentation(ShadowImagePresentation visual, float now)
        {
            if (visual == null || !visual.inUse || !visual.body) return;
            float age = Mathf.Max(0f, now - visual.started);
            if (visual.attacking && age < 0.3f)
                visual.attack.SetTime(age * shadowAttackClip.length / 0.3f);
            else
            {
                visual.output.SetSourcePlayable(visual.idle);
                visual.idle.SetTime((visual.attacking ? age - 0.3f : age) % shadowIdleClip.length);
            }
            visual.graph.Evaluate(0f);
        }

        private static void ReleaseShadowPresentation(ShadowImagePresentation visual)
        {
            if (visual == null) return;
            visual.inUse = false;
            if (visual.body) visual.body.SetActive(false);
        }

        private void DisposeShadowPresentations()
        {
            foreach (var visual in shadowPresentations)
            {
                if (visual.graph.IsValid()) visual.graph.Destroy();
                if (visual.body) Destroy(visual.body);
            }
            shadowPresentations.Clear();
            if (shadowMaterial) Destroy(shadowMaterial);
            if (shadowAttackClip) Destroy(shadowAttackClip);
            if (shadowIdleClip) Destroy(shadowIdleClip);
        }
    }
}
