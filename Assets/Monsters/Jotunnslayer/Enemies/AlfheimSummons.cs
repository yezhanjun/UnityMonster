using System.Collections.Generic;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class AlfheimSummons : MonoBehaviour
    {
        const float ProjectileSpeed = 10f;
        const float ArcHeight = 3f;

        struct Shot
        {
            public Transform Visual;
            public Vector3 Origin;
            public Vector3 Landing;
            public float Started;
            public float Duration;
            public Skeleton1HSpawn.Kind Kind;
            public float SummonLifetime;
        }

        readonly List<Shot> shots = new List<Shot>(10);
        readonly List<(CharacterInstance Spider, float ExpiresAt)> summons = new List<(CharacterInstance, float)>(10);
        CharacterInstance caster;
        CharacterInstance target;
        internal int ElfNormalSpawned { get; private set; }
        internal int ElfLargeSpawned { get; private set; }
        internal int ActiveSummons => summons.Count;

        internal void Launch(CharacterInstance source, CharacterInstance victim, bool veteran) =>
            Launch(source, victim, veteran, veteran ? 4 : 10, 0f);

        internal void LaunchElfWoman(CharacterInstance source, CharacterInstance victim, bool large) =>
            Launch(source, victim, large, large ? 4 : 6, 10f);

        void Launch(CharacterInstance source, CharacterInstance victim, bool veteran, int count, float summonLifetime)
        {
            caster = source;
            target = victim;
            enabled = true;
            Vector3 origin = source.CombatAimPoint;
            Vector3 center = source.transform.position;
            float startAngle = Random.Range(0f, 360f);
            float now = Time.time;
            var kind = veteran ? Skeleton1HSpawn.Kind.AlfheimSpiderVeteran : Skeleton1HSpawn.Kind.AlfheimSpider;
            for (int i = 0; i < count; i++)
            {
                float angle = startAngle + i * (360f / count);
                Vector3 radial = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidate = center + radial * Random.Range(3f, 6f);
                if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var hit, 12f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore) || hit.normal.y < .65f)
                    continue;

                Vector3 landing = hit.point + Vector3.up * .15f;
                if (Physics.CheckCapsule(landing + Vector3.up * .5f, landing + Vector3.up * 1.5f, .45f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                    continue;

                var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                projectile.name = "Alfheim summon egg";
                projectile.transform.SetParent(transform, true);
                projectile.transform.position = origin;
                projectile.transform.localScale = Vector3.one * .25f;
                var collider = projectile.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                shots.Add(new Shot
                {
                    Visual = projectile.transform,
                    Origin = origin,
                    Landing = landing,
                    Started = now,
                    Duration = Mathf.Clamp(Vector3.Distance(origin, landing) / ProjectileSpeed, .1f, 3f),
                    Kind = kind,
                    SummonLifetime = summonLifetime
                });
            }
        }

        void Update()
        {
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0 ||
                !target || target.CurrentHealth <= 0)
            {
                enabled = false;
                return;
            }

            float now = Time.time;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                Shot shot = shots[i];
                if (!shot.Visual)
                {
                    shots.RemoveAt(i);
                    continue;
                }

                float fraction = Mathf.Clamp01((now - shot.Started) / shot.Duration);
                Vector3 next = Vector3.Lerp(shot.Origin, shot.Landing, fraction) +
                    Vector3.up * (MonsterSummonArc.Evaluate(fraction) * ArcHeight);
                bool blocked = Physics.Linecast(shot.Visual.position, next, GameManager.environmentLayer,
                    QueryTriggerInteraction.Ignore);
                if (!blocked && fraction < 1f)
                {
                    shot.Visual.position = next;
                    continue;
                }

                Destroy(shot.Visual.gameObject);
                shots.RemoveAt(i);
                if (blocked) continue;

                Vector3 facing = Vector3.ProjectOnPlane(target.transform.position - shot.Landing, Vector3.up);
                Quaternion rotation = facing.sqrMagnitude > .001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
                var spider = Skeleton1HSpawn.TrySpawn(target, shot.Landing, rotation, shot.Kind);
                if (spider)
                {
                    if (shot.SummonLifetime > 0f)
                    {
                        spider.Team = caster.Team;
                        if (spider.TryGetComponent(out Skeleton1HCombat combat)) combat.SuppressSummonRewards = true;
                        if (shot.Kind == Skeleton1HSpawn.Kind.AlfheimSpiderVeteran) ElfLargeSpawned++;
                        else ElfNormalSpawned++;
                    }
                    float expiresAt = shot.SummonLifetime > 0f ? now + shot.SummonLifetime : float.PositiveInfinity;
                    summons.Add((spider, expiresAt));
                }
            }

            for (int i = summons.Count - 1; i >= 0; i--)
            {
                var summon = summons[i];
                if (summon.Spider && now < summon.ExpiresAt) continue;
                if (summon.Spider) NetworkServer.Destroy(summon.Spider.gameObject);
                summons.RemoveAt(i);
            }
        }

        void OnDisable()
        {
            foreach (var shot in shots)
                if (shot.Visual) Destroy(shot.Visual.gameObject);
            if (NetworkServer.active)
                foreach (var summon in summons)
                    if (summon.Spider) NetworkServer.Destroy(summon.Spider.gameObject);
            shots.Clear();
            summons.Clear();
        }
    }
}
