using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string HeartUnlock = "active_ranger_A";
        public const string HeartEvolution = "active_ranger_A_evolve";
        private const float HeartseekerCastRange = 10f;
        private const float RangerArrowSpeed = 12f;
        private int heartRank;
        private float heartReady;
        private readonly List<RangerArrow> rangerFlights = new List<RangerArrow>(6);

        private sealed class RangerArrow
        {
            internal readonly Health[] hit = new Health[9];
            internal int hitCount, limit, damage;
            internal Vector3 position, direction;
            internal float sampledAt, expires, radius, bleedChance, nextVisualSample;
            internal uint visualId;
            internal Health trackingTarget;
        }

        private bool CanChooseHeartseeker() => AllowsRaidClass(RaidClass.Ranger) && PendingRewards > 0 && heartRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[heartRank] && progress.IsLearned(HeartUnlock) &&
            (heartRank < 3 || progress.IsLearned(HeartEvolution));

        internal bool TryChooseHeartseeker()
        {
            if (!CanChooseHeartseeker())
                return false;

            heartRank++;
            rewardsSpent++;
            return true;
        }

        private void TryHeartseeker(float now)
        {
            if (heartRank == 0 || now < heartReady || !progress.IsLearned(HeartUnlock))
                return;

            CollectTargets(caster.transform.position, HeartseekerCastRange);
            if (targets.Count == 0)
                return;

            bool evolved = heartRank == 4 && progress.IsLearned(HeartEvolution);
            heartReady = now + ScaleSkillCooldown(4f);
            Vector3 origin = caster.transform.position + Vector3.up;
            int count = evolved ? 6 : 1;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = evolved ? Quaternion.AngleAxis(i * 60f, Vector3.up) * Vector3.forward :
                    (RaidSkillAimPoint(targets[targets.Count == 1 ? 0 : Random.Range(0, targets.Count)]) - origin).normalized;
                LaunchRangerArrow(now, origin, direction, evolved ? 3 : 3 * Mathf.Min(heartRank, 3),
                    evolved ? 20 : 15, evolved ? .5f : .3f, evolved ? .5f : .25f);
            }
        }

        private void LaunchRangerArrow(float now, Vector3 origin, Vector3 direction, int limit, int damage,
            float radius, float bleedChance = 0f, Health trackingTarget = null)
        {
            int slot = 0;
            while (slot < rangerFlights.Count && rangerFlights[slot].limit > 0)
                slot++;
            if (slot == rangerFlights.Count)
                rangerFlights.Add(new RangerArrow());
            var arrow = rangerFlights[slot];
            arrow.limit = limit;
            arrow.damage = damage;
            arrow.radius = radius;
            arrow.bleedChance = bleedChance;
            arrow.sampledAt = now;
            arrow.expires = now + 4f;
            arrow.position = origin;
            arrow.direction = direction;
            arrow.trackingTarget = trackingTarget;
            arrow.visualId = ++nextProjectileVisualId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            ValidationProjectileTrace?.Invoke($"arrow launch id={arrow.visualId} origin={origin} direction={direction} damage={damage} limit={limit}");
#endif
            SendRangerArrowVisual(arrow, false, now);
        }

        private void TickRangerArrows(float now)
        {
            foreach (var arrow in rangerFlights)
            {
                if (arrow.limit == 0)
                    continue;
                if (arrow.limit > 1 && !progress.IsLearned(HeartUnlock))
                {
                    ResetRangerArrow(arrow);
                    continue;
                }
                float sampleTime = Mathf.Min(now, arrow.expires);
                while (arrow.hitCount < arrow.limit && arrow.sampledAt < sampleTime && HasLiveCaster)
                {
                    Health tracking = arrow.trackingTarget;
                    if (tracking && tracking.CurrentHealth > 0 && tracking.Team != caster.Team)
                    {
                        Vector3 offset = RaidSkillAimPoint(tracking) - arrow.position;
                        if (offset.sqrMagnitude > .000001f)
                            arrow.direction = offset.normalized;
                    }
                    float distance = RangerArrowSpeed * (sampleTime - arrow.sampledAt);
                    float wall = float.PositiveInfinity;
                    if (arrow.direction != Vector3.zero && Physics.SphereCast(arrow.position, arrow.radius,
                        arrow.direction, out var terrain, distance, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                        wall = terrain.distance;

                    Health target = FindRangerArrowContact(arrow, distance, wall, out float contact);
                    arrow.position += arrow.direction * Mathf.Min(distance, contact);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    if (target || !float.IsPositiveInfinity(wall))
                        ValidationProjectileTrace?.Invoke($"arrow contact id={arrow.visualId} " +
                            $"hit={(target ? target.netId : 0)} terrain={!target && !float.IsPositiveInfinity(wall)} at={arrow.position}");
#endif
                    if (!target)
                    {
                        arrow.sampledAt = sampleTime;
                        if (!float.IsPositiveInfinity(wall))
                            arrow.limit = 0;
                        break;
                    }

                    arrow.sampledAt += contact / RangerArrowSpeed;
                    arrow.hit[arrow.hitCount++] = target;
                    int before = target.CurrentHealth;
                    DealRangerSkillDamage(target, arrow.damage);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    ValidationRangerHit?.Invoke(arrow.visualId, target.netId, before - target.CurrentHealth);
#endif
                    if (arrow.bleedChance > 0f && target && target.CurrentHealth > 0 && target.CurrentHealth < before &&
                        Random.value < arrow.bleedChance)
                        ApplyRaidBleed(target, arrow.sampledAt, ScaleClassSkillDamage(2), ticks: 4, requiresHeartUnlock: true);

                    if (arrow.hitCount >= arrow.limit || !HasLiveCaster)
                        break;
                    Health next = FindHeartBounceTarget(arrow);
                    if (next)
                    {
                        SendRangerArrowVisual(arrow, false, now);
                        arrow.direction = (RaidSkillAimPoint(next) - arrow.position).normalized;
                    }
                    else
                        arrow.limit = 0;
                }

                if (now >= arrow.expires || arrow.hitCount >= arrow.limit)
                    ResetRangerArrow(arrow);
                else if (now >= arrow.nextVisualSample)
                    SendRangerArrowVisual(arrow, false, now);
            }
        }

        private Health FindRangerArrowContact(RangerArrow arrow, float distance, float wall, out float contact)
        {
            Health nearest = null;
            contact = wall;
            float radiusSquared = arrow.radius * arrow.radius;
            foreach (Health candidate in GameManager.HealthInstances.Values)
            {
                if (!IsRangerArrowCandidate(arrow, candidate))
                    continue;
                Vector3 offset = RaidSkillAimPoint(candidate) - arrow.position;
                float along = Vector3.Dot(offset, arrow.direction);
                float lateralSquared = (offset - arrow.direction * along).sqrMagnitude;
                if (lateralSquared > radiusSquared)
                    continue;
                float halfChord = Mathf.Sqrt(radiusSquared - lateralSquared);
                float entry = Mathf.Max(0f, along - halfChord);
                if (along + halfChord < 0f || entry > distance || entry >= contact)
                    continue;
                nearest = candidate;
                contact = entry;
            }
            return nearest;
        }

        private Health FindHeartBounceTarget(RangerArrow arrow)
        {
            Health nearest = null;
            float rangeSquared = 15f * 15f;
            foreach (Health candidate in GameManager.HealthInstances.Values)
            {
                if (!IsRangerArrowCandidate(arrow, candidate))
                    continue;
                float distance = (RaidSkillAimPoint(candidate) - arrow.position).sqrMagnitude;
                if (distance > rangeSquared || (nearest && distance == rangeSquared && candidate.netId > nearest.netId))
                    continue;
                nearest = candidate;
                rangeSquared = distance;
            }
            return nearest;
        }

        private bool IsRangerArrowCandidate(RangerArrow arrow, Health target) =>
            target && target.isServer && target.CurrentHealth > 0 && target.Team != caster.Team &&
            System.Array.IndexOf(arrow.hit, target, 0, arrow.hitCount) < 0;

        private void ClearRangerArrows()
        {
            foreach (var arrow in rangerFlights)
                if (arrow.limit > 0)
                    ResetRangerArrow(arrow);
        }

        private void SendRangerArrowVisual(RangerArrow arrow, bool finished, float now)
        {
            SendProjectileTrail(arrow.visualId, arrow.position, finished, heartArrow: true);
            arrow.nextVisualSample = now + .1f;
        }

        private void ResetRangerArrow(RangerArrow arrow)
        {
            SendRangerArrowVisual(arrow, true, arrow.sampledAt);
            System.Array.Clear(arrow.hit, 0, arrow.hitCount);
            arrow.hitCount = arrow.limit = 0;
            arrow.visualId = 0;
            arrow.trackingTarget = null;
        }
    }
}
