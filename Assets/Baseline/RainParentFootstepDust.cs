using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // The dust/shake branches of the original FootstepHandler; sound and surface branches remain separate.
    // Keep on preparation assets until those branches and the original opaque material are available.
    public sealed class RainParentFootstepDust : MonoBehaviour
    {
        public Transform bodyTransform;
        public Transform footL, footR;
        public ParticleSystem dustPrefab;
        public bool enableFootstepDust = true;
        public LayerMask worldAndWaterMask = 1 | (1 << 4);
        ParticleSystem dust;
        RainParentShakeEmitter shake;

        void Start()
        {
            if (enableFootstepDust && dustPrefab)
            {
                dust = Instantiate(dustPrefab, transform);
                shake = dust.GetComponent<RainParentShakeEmitter>();
            }
        }

        public void Footstep(AnimationEvent animationEvent)
        {
            if (animationEvent.animatorClipInfo.weight > .5f) PlayDust(animationEvent.stringParameter);
        }

        void PlayDust(string childName)
        {
            if (!bodyTransform) return;
            var foot = childName == "FootL" ? footL : childName == "FootR" ? footR : null;
            if (!foot || !dust) return;
            if (!Physics.Raycast(foot.position + Vector3.up * 1.5f, Vector3.down, out var hit,
                4f, worldAndWaterMask, QueryTriggerInteraction.Collide)) return;
            dust.transform.position = hit.point;
            var main = dust.main;
            main.startColor = Color.gray;
            dust.Play();
            if (shake) shake.StartShake();
        }

        void OnDestroy()
        {
            // Also release when this component alone is removed from a surviving model.
            if (dust) Destroy(dust.gameObject);
        }
    }
}
