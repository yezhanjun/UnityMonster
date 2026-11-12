using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class AlfheimSpiderWave : MonoBehaviour
    {
        const float Speed = 12f;
        const float Lifetime = 1.6f;
        const float InitialHalfWidth = 2f;
        const float HalfHeight = 2f;
        const float HalfDepth = .3f;
        const float HalfWidthGrowthPerSecond = 2f;
        const float MaxGroundCorrection = 1f;

        CharacterInstance source;
        CharacterInstance target;
        int damage;
        float elapsed;
        float heightOverGround;
        bool muspel;

        internal static AlfheimSpiderWave Launch(CharacterInstance caster, CharacterInstance victim, Quaternion facing, int damage, bool muspel = false)
        {
            var wave = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wave.name = muspel ? "Muspel fire wave" : "Alfheim spider wave";
            wave.transform.SetPositionAndRotation(caster.transform.position + Vector3.up * (muspel ? 1f : .4f), facing);
            wave.transform.localScale = new Vector3(InitialHalfWidth * 2f, .15f, HalfDepth * 2f);
            var collider = wave.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);

            var flight = wave.AddComponent<AlfheimSpiderWave>();
            flight.source = caster;
            flight.target = victim;
            flight.damage = damage;
            flight.muspel = muspel;
            if (TryGetGround(wave.transform.position, out var ground))
                flight.heightOverGround = ground.distance;
            return flight;
        }

        void FixedUpdate()
        {
            if (!source || !source.isServer || muspel && source.CurrentHealth <= 0)
            {
                Destroy(gameObject);
                return;
            }

            float step = Mathf.Min(Time.fixedDeltaTime, Lifetime - elapsed);
            elapsed += step;
            float travel = Speed * step;
            Vector3 start = transform.position;
            Vector3 forward = transform.forward;
            Vector3 end = start + forward * travel;
            if (TryGetGround(end, out var ground))
            {
                float adjustedHeight = ground.point.y + heightOverGround;
                if (Mathf.Abs(end.y - adjustedHeight) < MaxGroundCorrection)
                    end.y = adjustedHeight;
            }
            float halfWidth = muspel ? 1f : InitialHalfWidth + elapsed * HalfWidthGrowthPerSecond;
            float halfHeight = muspel ? 1f : HalfHeight;
            float halfDepth = muspel ? .15f : HalfDepth;

            if (target && target.CurrentHealth > 0 && target.Team != source.Team)
            {
                Vector3 aimPoint = target.CombatAimPoint;
                Vector3 delta = aimPoint - start;
                float depth = Vector3.Dot(delta, forward);
                float height = Mathf.Lerp(start.y, end.y, travel > 0f ? Mathf.Clamp01(depth / travel) : 0f);
                if (depth >= -halfDepth && depth <= travel + halfDepth &&
                    Mathf.Abs(Vector3.Dot(delta, transform.right)) <= halfWidth &&
                    Mathf.Abs(aimPoint.y - height) <= halfHeight)
                {
                    int health = target.CurrentHealth;
                    target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, source, 0);
                    if (muspel && target && source.TryGetComponent(out Skeleton1HCombat combat))
                        combat.RecordMuspelWaveDamage(Mathf.Max(0, health - target.CurrentHealth));
                    target = null;
                }
            }

            transform.position = end;
            transform.localScale = new Vector3(halfWidth * 2f, .15f, halfDepth * 2f);
            if (elapsed >= Lifetime) Destroy(gameObject);
        }

        static bool TryGetGround(Vector3 origin, out RaycastHit hit) =>
            Physics.Raycast(origin, Vector3.down, out hit, Mathf.Infinity,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
    }
}
