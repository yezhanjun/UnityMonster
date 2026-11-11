using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string BriarEvolution = "active_seer_B_evolve";
        private static readonly float[] BriarCooldowns = { 5f, 4.2f, 3.5f };
        private static readonly float[] BriarRootChances = { 0.2f, 0.35f, 0.5f };
        private int briarRank;
        private float briarReady;
        private readonly List<BriarCast> briars = new List<BriarCast>(2);
        private readonly List<BriarEvolutionCast> evolvedBriars = new List<BriarEvolutionCast>(2);

        private sealed class BriarEvolutionCast
        {
            public readonly Vector3[] centers = new Vector3[8];
            public int attempted, spawned;
            public float next;
        }

        private sealed class BriarCast
        {
            public Vector3 origin;
            public float yaw, reach, radius, expires, rootChance;
            public readonly HashSet<Health> hit = new HashSet<Health>();
        }

        private bool CanChooseBriar() => AllowsRaidClass(RaidClass.Seeress) && PendingRewards > 0 && briarRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[briarRank] && (briarRank < 3 || progress.IsLearned(BriarEvolution));

        internal bool TryChooseBriar()
        {
            if (!CanChooseBriar())
                return false;

            briarRank++;
            rewardsSpent++;
            return true;
        }

        private void TryBriar(float now)
        {
            if (briarRank == 0 || now < briarReady)
                return;

            int rank = Mathf.Min(briarRank, 3);
            bool evolved = briarRank == 4 && progress.IsLearned(BriarEvolution);
            briarReady = now + ScaleSkillCooldown(BriarCooldowns[rank - 1]);
            if (evolved)
            {
                evolvedBriars.Add(new BriarEvolutionCast { next = now + Random.Range(0.15f, 0.25f) });
                return;
            }

            briars.Add(new BriarCast
            {
                origin = caster.transform.position,
                yaw = Random.Range(0f, 360f),
                reach = 5f + 3f * rank,
                radius = 0.75f,
                expires = now + ScaleSeerSkillDuration(4f),
                rootChance = BriarRootChances[rank - 1]
            });
        }

        private void TickBriars(float now)
        {
            TickEvolvedBriars(now);
            for (int castIndex = briars.Count - 1; castIndex >= 0; castIndex--)
            {
                var cast = briars[castIndex];
                if (now >= cast.expires)
                {
                    briars.RemoveAt(castIndex);
                    continue;
                }

                CollectTargets(cast.origin, cast.reach + cast.radius);
                foreach (var target in targets)
                {
                    Vector3 offset = target.transform.position - cast.origin;
                    float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg - cast.yaw;
                    const float step = 180f;
                    float branch = Mathf.Round(angle / step) * step;
                    Vector3 direction = Quaternion.Euler(0f, branch + cast.yaw, 0f) * Vector3.forward;
                    float distance = Mathf.Clamp(Vector3.Dot(offset, direction), 0f, cast.reach);
                    if ((offset - direction * distance).sqrMagnitude <= cast.radius * cast.radius && cast.hit.Add(target))
                    {
                        DealBriarHit(target, 24, cast.rootChance, now);
                    }
                }
            }
        }

        private void TickEvolvedBriars(float now)
        {
            for (int i = evolvedBriars.Count - 1; i >= 0; i--)
            {
                var cast = evolvedBriars[i];
                while (cast.attempted < cast.centers.Length && now >= cast.next)
                {
                    cast.attempted++;
                    bool foundCenter = TryPickBriarCenter(cast, caster.transform.position, out Vector3 center);
                    cast.next += Random.Range(0.15f, 0.25f);
                    if (!foundCenter)
                        continue;

                    cast.centers[cast.spawned++] = center;
                    RecordImpact(center + Vector3.up * 0.15f);
                    CollectTargets(center, 1.2f);
                    foreach (var target in targets)
                        DealBriarHit(target, 30, 0.5f, now);
                }
                if (cast.attempted == cast.centers.Length)
                    evolvedBriars.RemoveAt(i);
            }
        }

        private bool TryPickBriarCenter(BriarEvolutionCast cast, Vector3 origin, out Vector3 center)
        {
            if (Random.value < 0.35f)
            {
                CollectTargets(origin, 12f);
                for (int i = targets.Count - 1; i >= 0; i--)
                {
                    Vector3 point = targets[i].transform.position;
                    if ((point - origin).sqrMagnitude < 4f || !TryResolveBriarCenter(cast, point, out _))
                        targets.RemoveAt(i);
                }
                if (targets.Count > 0 &&
                    TryResolveBriarCenter(cast, targets[Random.Range(0, targets.Count)].transform.position, out center))
                    return true;
            }

            for (int attempt = 0; attempt < 16; attempt++)
            {
                Vector3 direction = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                Vector3 candidate = origin + direction * Random.Range(2f, 12f);
                if (TryResolveBriarCenter(cast, candidate, out center))
                    return true;
            }
            for (int i = 0; i < 16; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 22.5f, 0f) * Vector3.forward;
                if (TryResolveBriarCenter(cast, origin + direction * 12f, out center))
                    return true;
            }
            center = default;
            return false;
        }

        private bool TryResolveBriarCenter(BriarEvolutionCast cast, Vector3 candidate, out Vector3 center)
        {
            center = candidate;
            if (Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out var hit, 7f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y < 0.65f)
                    return false;
                center = hit.point;
            }
            else if (LiveMatch)
                return false;

            for (int i = 0; i < cast.spawned; i++)
                if ((cast.centers[i] - center).sqrMagnitude < 4f)
                    return false;
            return true;
        }

        private void DealBriarHit(Health target, int damage, float rootChance, float now)
        {
            int before = target.CurrentHealth;
            Deal(target, ScaleClassSkillDamage(damage));
            if (target && target.CurrentHealth > 0 && target.CurrentHealth < before && Random.value < rootChance)
                ApplyRaidSkillRoot(target, now + 4f);
        }
    }
}
