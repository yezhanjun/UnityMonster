using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Test values for the recovered RPGEffect duration and PlayerAttribute effects.
    public sealed class RangeTestBuffs : MonoBehaviour
    {
        public const string Lifesteal = "gm_lifesteal";
        public const string Regeneration = "gm_regeneration";
        public const string DamageReduction = "gm_damage_reduction";
        public const float Duration = 30f;
        Health owner;
        float lifestealUntil, regenerationUntil, reductionUntil, nextTick;

        void Awake() => owner = GetComponent<Health>();
        public static bool Supports(string id) => id == Lifesteal || id == Regeneration || id == DamageReduction;
        public float Remaining(string id)
        {
            float expires = id switch
            {
                Lifesteal => lifestealUntil,
                Regeneration => regenerationUntil,
                DamageReduction => reductionUntil,
                _ => 0f
            };
            return Mathf.Max(0f, expires - Time.time);
        }

        public int ReduceDamage(int damage) => Remaining(DamageReduction) > 0f
            ? Mathf.Max(1, Mathf.RoundToInt(damage * .75f))
            : damage;

        public void Apply(string id)
        {
            if (!owner.isServer || owner.CurrentHealth <= 0) return;
            if (id == Lifesteal) lifestealUntil = Time.time + Duration;
            if (id == DamageReduction) reductionUntil = Time.time + Duration;
            if (id == Regeneration)
            {
                if (Remaining(id) <= 0) nextTick = Time.time + 1f;
                regenerationUntil = Time.time + Duration;
            }
        }

        public void Remove(string id)
        {
            if (!owner.isServer) return;
            if (id == Lifesteal) lifestealUntil = 0;
            if (id == DamageReduction) reductionUntil = 0;
            if (id == Regeneration) regenerationUntil = 0;
        }

        public void OnDamageDealt(int damage)
        {
            if (owner.isServer && owner.CurrentHealth > 0 && Remaining(Lifesteal) > 0)
                owner.ServerHeal(Mathf.RoundToInt(damage * .2f), owner.netId);
        }

        void Update()
        {
            if (!owner.isServer) return;
            if (owner.CurrentHealth <= 0)
            {
                lifestealUntil = regenerationUntil = reductionUntil = 0;
                return;
            }
            if (nextTick <= regenerationUntil && regenerationUntil > 0 && Time.time >= nextTick)
            {
                nextTick += 1f;
                owner.ServerHeal(5, owner.netId, allowOverhealBarrier: false);
            }
        }
    }
}
