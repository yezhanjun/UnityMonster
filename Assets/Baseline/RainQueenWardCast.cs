using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // In-flight orbs and their wards outlive the queen's cast and the queen herself.
    internal sealed class RainQueenWardCast : MonoBehaviour
    {
        const float Range = 120f;
        const float Speed = 30f;
        const float WardLifetime = 20f;
        static readonly AnimationCurve RadiusCurve = new AnimationCurve(
            new Keyframe(0f, .5f, 8.588516f, 8.588516f),
            new Keyframe(.4996338f, 1.9907646f, .36513633f, .36513633f));

        internal static float RadiusAt(float age) => 5f * RadiusCurve.Evaluate(Mathf.Clamp01(age / WardLifetime));

        struct Orb
        {
            internal CharacterInstance Target;
            internal float ArrivesAt;
            internal uint VisualId;
        }

        struct Ward
        {
            internal RainQueenWardHealth Health;
            internal int Team;
            internal float StartedAt, NextScan;
        }

        readonly List<Orb> orbs = new List<Orb>();
        readonly List<Ward> wards = new List<Ward>();
        Gamemode mode;
        int sourceTeam;
        static RainQueenWardHealth wardPrefab;

        internal static void Launch(CharacterInstance source, Vector3 origin)
        {
            if (!NetworkServer.active || !GameManager.Gamemode) return;
            RainQueenWardCast cast = null;
            foreach (var identity in NetworkServer.spawned.Values)
            {
                if (!identity || !identity.TryGetComponent<CharacterInstance>(out var candidate) ||
                    candidate == source || candidate.Team < 0 || candidate.Team == source.Team ||
                    candidate.CurrentHealth <= 0) continue;

                float distanceSquared = (candidate.CombatAimPoint - origin).sqrMagnitude;
                if (distanceSquared > Range * Range) continue;
                if (!cast)
                {
                    cast = new GameObject("Rain Queen Ward Cast").AddComponent<RainQueenWardCast>();
                    cast.mode = GameManager.Gamemode;
                    cast.sourceTeam = source.Team;
                }
                float duration = Mathf.Sqrt(distanceSquared) / Speed;
                cast.orbs.Add(new Orb
                {
                    Target = candidate,
                    ArrivesAt = Time.time + duration,
                    VisualId = cast.mode is RangeGamemode range
                        ? range.PublishQueenWardOrb(origin, candidate, duration) : 0
                });
            }
        }

        void FixedUpdate()
        {
            if (!NetworkServer.active || !mode || mode != GameManager.Gamemode)
            {
                Destroy(gameObject);
                return;
            }

            float now = Time.time;
            var range = mode as RangeGamemode;
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                var orb = orbs[i];
                if (now < orb.ArrivesAt) continue;
                orbs.RemoveAt(i);
                if (orb.VisualId != 0) range.RemoveQueenWardOrb(orb.VisualId);
                if (!orb.Target) continue;
                if (!wardPrefab)
                {
                    var prefab = Resources.Load<GameObject>(RainQueenWardHealth.ResourcePath);
                    if (!prefab || !prefab.TryGetComponent(out wardPrefab)) continue;
                }
                var wardHealth = Instantiate(wardPrefab, orb.Target.CombatAimPoint, Quaternion.identity);
                wardHealth.Team = sourceTeam;
                wardHealth.StartedAt = NetworkTime.time;
                NetworkServer.Spawn(wardHealth.gameObject);
                wards.Add(new Ward
                {
                    Health = wardHealth,
                    Team = orb.Target.Team,
                    StartedAt = now,
                    NextScan = now
                });
            }

            for (int i = wards.Count - 1; i >= 0; i--)
            {
                var ward = wards[i];
                float age = now - ward.StartedAt;
                if (!ward.Health || ward.Health.CurrentHealth <= 0 || age >= WardLifetime)
                {
                    if (ward.Health)
                    {
                        if (ward.Health.CurrentHealth <= 0)
                            ward.Health.RpcPlayDeathEffect(ward.Health.transform.position);
                        NetworkServer.Destroy(ward.Health.gameObject);
                    }
                    wards.RemoveAt(i);
                    continue;
                }
                if (now < ward.NextScan) continue;

                ward.NextScan = now + 1f;
                wards[i] = ward;
                float radius = RadiusAt(age);
                Vector3 position = ward.Health.transform.position;
                foreach (var identity in NetworkServer.spawned.Values)
                {
                    if (identity && identity.TryGetComponent<CharacterInstance>(out var candidate) &&
                        candidate.Team == ward.Team &&
                        (candidate.transform.position - position).sqrMagnitude <= radius * radius)
                        candidate.ApplyBeetleJuice();
                }
            }

            if (orbs.Count == 0 && wards.Count == 0) Destroy(gameObject);
        }

        void OnDestroy()
        {
            if (!NetworkServer.active) return;
            if (mode is RangeGamemode range && range)
                foreach (var orb in orbs)
                    if (orb.VisualId != 0) range.RemoveQueenWardOrb(orb.VisualId);
            foreach (var ward in wards)
                if (ward.Health) NetworkServer.Destroy(ward.Health.gameObject);
        }
    }
}
