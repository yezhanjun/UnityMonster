using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class HybSummonVolley : MonoBehaviour
    {
        const int ProjectileCount = 10;
        const int LandingAttempts = 8;
        const float Speed = 10f;
        const float ArcHeight = 3f;

        struct Shot
        {
            public Transform Visual;
            public Vector3 Landing;
            public float Duration;
            public float LaunchDelay;
            public float SummonExpires;
        }

        readonly Shot[] shots = new Shot[ProjectileCount];
        readonly CharacterInstance[] summons = new CharacterInstance[ProjectileCount];
        CharacterInstance caster;
        CharacterInstance target;
        Skeleton1HSpawn.Kind summonKind;
        Vector3 origin;
        float launchedAt;
        bool goatRing;

        internal void Begin(CharacterInstance source, CharacterInstance victim, Skeleton1HSpawn.Kind kind, int count, bool goatRing = false)
        {
            this.goatRing = goatRing;
            caster = source;
            target = victim;
            summonKind = kind;
            origin = caster.CombatAimPoint;
            launchedAt = Time.time;
            Vector3 center = goatRing ? caster.transform.position : target.transform.position;
            float startAngle = Random.Range(0f, 360f);
            float angleStep = goatRing ? 60f : count == 4 ? 90f : 51f;
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * angleStep;
                if (!TryFindLanding(center, angle, out Vector3 landing)) continue;

                var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = "Hyb summon projectile";
                projectile.transform.SetParent(transform, true);
                projectile.transform.position = origin;
                if (goatRing) projectile.SetActive(false);
                projectile.transform.localScale = Vector3.one * .25f;
                var collider = projectile.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                shots[i] = new Shot
                {
                    Visual = projectile.transform,
                    Landing = landing,
                    Duration = Mathf.Clamp(Vector3.Distance(origin, landing) / Speed, .1f, 3f),
                    LaunchDelay = goatRing ? (i + 1) * .1f : 0f
                };
            }
        }

        bool TryFindLanding(Vector3 center, float angle, out Vector3 landing)
        {
            for (int attempt = 0; attempt < LandingAttempts; attempt++)
            {
                float heading = attempt == 0 ? angle : Random.Range(0f, 360f);
                Vector3 offset = Quaternion.Euler(0f, heading, 0f) * Vector3.forward * (goatRing ? Random.Range(6f, 8f) : Random.Range(3f, 6f));
                Vector3 candidate = center + offset;
                if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var ground, 12f,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore) || ground.normal.y < .65f)
                    continue;

                landing = ground.point + Vector3.up * .15f;
                if (!Physics.CheckCapsule(landing + Vector3.up * .5f, landing + Vector3.up * 1.5f,
                    .45f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                    return true;
            }
            landing = default;
            return false;
        }

        void Update()
        {
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0 ||
                !target || target.CurrentHealth <= 0)
            {
                Destroy(this);
                return;
            }

            bool active = false;
            float elapsed = Time.time - launchedAt;
            for (int i = 0; i < shots.Length; i++)
            {
                ref Shot shot = ref shots[i];
                if (shot.Visual)
                {
                    if (elapsed < shot.LaunchDelay)
                    {
                        active = true;
                        continue;
                    }
                    if (!shot.Visual.gameObject.activeSelf) shot.Visual.gameObject.SetActive(true);
                    float t = Mathf.Clamp01((elapsed - shot.LaunchDelay) / shot.Duration);
                    Vector3 next = Vector3.Lerp(origin, shot.Landing, t) +
                        Vector3.up * (MonsterSummonArc.Evaluate(t) * ArcHeight);
                    bool blocked = Physics.Linecast(shot.Visual.position, next, GameManager.environmentLayer,
                        QueryTriggerInteraction.Ignore);
                    if (blocked || t >= 1f)
                    {
                        Destroy(shot.Visual.gameObject);
                        shot.Visual = null;
                        if (!blocked)
                        {
                            Vector3 facing = Vector3.ProjectOnPlane(target.transform.position - shot.Landing, Vector3.up);
                            Quaternion rotation = facing.sqrMagnitude > .001f
                                ? Quaternion.LookRotation(facing) : Quaternion.identity;
                            summons[i] = Skeleton1HSpawn.TrySpawn(target, shot.Landing, rotation, summonKind);
                            shot.SummonExpires = Time.time + 10f;
                        }
                    }
                    else
                    {
                        shot.Visual.position = next;
                        active = true;
                    }
                }

                if (goatRing && summons[i] && Time.time >= shot.SummonExpires)
                {
                    NetworkServer.Destroy(summons[i].gameObject);
                    summons[i] = null;
                }
                if (summons[i] && summons[i].CurrentHealth > 0) active = true;
            }

            if (!active) Destroy(this);
        }

        void OnDestroy()
        {
            for (int i = 0; i < shots.Length; i++)
            {
                if (shots[i].Visual) Destroy(shots[i].Visual.gameObject);
                if (NetworkServer.active && summons[i]) NetworkServer.Destroy(summons[i].gameObject);
            }
        }
    }
}
