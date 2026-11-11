using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed class RainQueenWardHealth : Health
    {
        internal const string ResourcePath =
            "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleWard/mdlBeetleWard";
        [SyncVar] internal double StartedAt;
        Transform indicator;
        float radiusVelocity;
        static GameObject deathEffect;

        protected override float BaseArmor => 30f;

        [ClientRpc]
        internal void RpcPlayDeathEffect(Vector3 position)
        {
            if (!deathEffect)
                deathEffect = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleWard/BeetleWardDeathEffect");
            if (!deathEffect) return;
            var effect = Instantiate(deathEffect, position, Quaternion.identity);
            if (effect.GetComponentInChildren<Light>() is Light flash && flash)
                Destroy(flash.gameObject, .08f);
            Destroy(effect, 3f);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            float age = Mathf.Max(0f, (float)(NetworkTime.time - StartedAt));
            var prefab = Resources.Load<GameObject>(
                "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleWard/BeetleWardIndicator");
            if (prefab)
            {
                indicator = Instantiate(prefab, transform, false).transform;
                indicator.localScale = Vector3.one * RainQueenWardCast.RadiusAt(age);
            }
            if (!TryGetComponent<Animator>(out var animator)) return;
            animator.Play("Base Layer.Spawn", 0, Mathf.Clamp01(age / 1.3833334f));
            animator.Play("Flap.Flap", 1, Mathf.Repeat(age / 1.3333334f, 1f));
        }

        void LateUpdate()
        {
            if (!indicator) return;
            float radius = RainQueenWardCast.RadiusAt((float)(NetworkTime.time - StartedAt));
            float scale = Mathf.SmoothDamp(indicator.localScale.x, radius, ref radiusVelocity, .2f);
            indicator.localScale = Vector3.one * scale;
        }
    }
}
