using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SeismicEvolution = "active_warden_B_evolve";
        private static readonly List<RaidSkillSlow> RaidSkillSlows = new List<RaidSkillSlow>();
        private int seismicRank, seismicCastRank;
        private float seismicReady, seismicImpactAt;
        private Vector3 seismicOrigin, seismicFacing;

        private struct RaidSkillSlow
        {
            public SkillBattlePreview owner;
            public Health victim;
            public float expires, attackSlowUntil;
        }

        private bool CanChooseSeismic() => AllowsRaidClass(RaidClass.Warden) && PendingRewards > 0 && seismicRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[seismicRank] &&
            (seismicRank < 3 || progress.IsLearned(SeismicEvolution));

        internal bool TryChooseSeismic()
        {
            if (!CanChooseSeismic())
                return false;

            seismicRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySeismic(float now)
        {
            if (seismicRank == 0 || seismicCastRank != 0 || now < seismicReady)
                return;

            seismicCastRank = seismicRank == 4 && !progress.IsLearned(SeismicEvolution) ? 3 : seismicRank;
            seismicReady = now + ScaleSkillCooldown(4f);
            seismicImpactAt = now + 0.75f;
            seismicOrigin = caster.transform.position;
            seismicFacing = Quaternion.Euler(0f, caster.transform.eulerAngles.y, 0f) * Vector3.forward;
        }

        private void TickSeismic(float now)
        {
            if (seismicCastRank == 0 || now < seismicImpactAt)
                return;

            bool evolved = seismicCastRank == 4;
            int rank = Mathf.Min(seismicCastRank, 3);
            seismicCastRank = 0;
            StrikeSeismic(seismicFacing, rank, now);
            if (evolved)
            {
                StrikeSeismic(Quaternion.AngleAxis(-45f, Vector3.up) * seismicFacing, rank, now);
                StrikeSeismic(Quaternion.AngleAxis(45f, Vector3.up) * seismicFacing, rank, now);
            }
        }

        private void StrikeSeismic(Vector3 direction, int rank, float now)
        {
            Vector3 center = seismicOrigin + direction * 4f;
            PullTargets(center, 2f, int.MaxValue);
            CollectTargets(center, 1.5f);
            foreach (var target in targets)
            {
                int before = target.CurrentHealth;
                Deal(target, ScaleClassSkillDamage(14 + 6 * rank));
                if (target && target.CurrentHealth > 0 && target.CurrentHealth < before &&
                    Random.value < 0.1f + rank * 0.1f)
                    ApplyRaidSkillSlow(target, now + 4f);
            }
        }

        private void ApplyRaidSkillSlow(Health target, float expires, float attackSlowUntil = 0f)
        {
            for (int i = 0; i < RaidSkillSlows.Count; i++)
            {
                var slow = RaidSkillSlows[i];
                if (slow.owner != this || slow.victim != target)
                    continue;

                slow.expires = Mathf.Max(slow.expires, expires);
                slow.attackSlowUntil = Mathf.Max(slow.attackSlowUntil, attackSlowUntil);
                RaidSkillSlows[i] = slow;
                return;
            }

            RaidSkillSlows.Add(new RaidSkillSlow { owner = this, victim = target, expires = expires,
                attackSlowUntil = attackSlowUntil });
        }

        internal static bool IsSlowedByRaidSkill(Health target, float now)
        {
            if (target is CharacterInstance character && character && !character.isServer)
                return NetworkTime.time < character.RaidMindSlowUntil;

            foreach (var slow in RaidSkillSlows)
            {
                if (slow.victim == target && now < slow.expires && slow.owner.HasLiveCaster)
                    return true;
            }

            return false;
        }

        private static float GetRaidAttackSlowDeadline(Health target, float now)
        {
            float deadline = now;
            foreach (var slow in RaidSkillSlows)
                if (slow.victim == target && slow.attackSlowUntil > deadline && slow.owner.HasLiveCaster)
                    deadline = slow.attackSlowUntil;
            return deadline;
        }

        internal static float RaidSkillAttackSpeedFactor(Health target, float now) =>
            (target is CharacterInstance character && character && !character.isServer
                ? NetworkTime.time < character.RaidMindSlowUntil
                : GetRaidAttackSlowDeadline(target, now) > now) ? 0.75f : 1f;

        private static void SyncRaidAttackSlow(Health target, float now)
        {
            if (target is CharacterInstance character && character && character.isServer)
            {
                float deadline = GetRaidAttackSlowDeadline(target, now);
                character.RaidMindSlowUntil = deadline > now ? NetworkTime.time + deadline - now : 0d;
            }
        }

        private void ClearSeismic()
        {
            seismicCastRank = 0;
            ClearRaidSkillSlows(float.PositiveInfinity);
        }

        private void ClearRaidSkillSlows(float now)
        {
            for (int i = RaidSkillSlows.Count - 1; i >= 0; i--)
            {
                var slow = RaidSkillSlows[i];
                if (slow.owner == this && (now >= slow.expires || !slow.victim || slow.victim.CurrentHealth <= 0))
                {
                    RaidSkillSlows.RemoveAt(i);
                    if (slow.attackSlowUntil > 0f && float.IsPositiveInfinity(now))
                        SyncRaidAttackSlow(slow.victim, Time.time);
                }
            }
        }
    }
}
