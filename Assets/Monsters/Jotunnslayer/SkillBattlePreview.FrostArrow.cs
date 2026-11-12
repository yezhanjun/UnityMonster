using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string FrostArrowEvolution = "active_skadi_D_evolve";
        private int frostArrowRank;
        private float frostArrowReady;

        private static readonly List<SkillStop> SkillStops = new List<SkillStop>();

        private struct SkillStop
        {
            public SkillBattlePreview owner;
            public Health victim;
            public float expires, frozenUntil;
        }

        private bool CanChooseFrostArrow() => PendingRewards > 0 && frostArrowRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[frostArrowRank] && (frostArrowRank < 3 || progress.IsLearned(FrostArrowEvolution));

        internal bool TryChooseFrostArrow()
        {
            if (!CanChooseFrostArrow())
                return false;

            frostArrowRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFrostArrow(float now)
        {
            if (frostArrowRank == 0 || now < frostArrowReady)
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, MissileRange);
            if (targets.Count == 0)
                return;

            bool evolved = frostArrowRank == 4 && progress.IsLearned(FrostArrowEvolution);
            int rank = Mathf.Min(frostArrowRank, 3);
            frostArrowReady = now + ScaleSkillCooldown(4f - rank * 0.5f);
            CastFrostArrow(origin, now, rank, evolved);
            if (RollAnsuzRepeat(ref frostArrowReady))
            {
                CollectTargets(origin, MissileRange);
                if (targets.Count > 0)
                    CastFrostArrow(origin, now, rank, evolved);
            }
        }

        private void CastFrostArrow(Vector3 origin, float now, int rank, bool evolved)
        {
            Health primary = targets[0];
            foreach (var candidate in targets)
            {
                if ((candidate.transform.position - origin).sqrMagnitude < (primary.transform.position - origin).sqrMagnitude)
                    primary = candidate;
            }

            Vector3 impact = primary.transform.position;
            IndicatePath(origin, impact, .15f);
            Vector3 forward = Vector3.ProjectOnPlane(impact - origin, Vector3.up).normalized;
            if (forward == Vector3.zero)
                forward = caster.transform.forward;

            DealGodDamage(primary, evolved ? 50 : 25 + rank * 5);
            StopSkillTarget(primary, now + 3f, frozen: true);

            Vector3 side = Vector3.Cross(Vector3.up, forward);
            CollectTargets(impact, 5f);
            foreach (var target in targets)
            {
                if (target == primary)
                    continue;

                Vector3 offset = target.transform.position - impact;
                Vector3 lateral = offset - side * Vector3.Dot(offset, side);
                Vector3 longitudinal = offset - forward * Vector3.Dot(offset, forward);
                if (lateral.sqrMagnitude <= 0.36f || (evolved && longitudinal.sqrMagnitude <= 0.36f))
                    DealGodDamage(target, 8 + rank * 4);
            }
        }

        private void StopSkillTarget(Health victim, float expiry, bool frozen = false)
        {
            if (!victim || victim.CurrentHealth <= 0)
                return;

            for (int i = 0; i < SkillStops.Count; i++)
            {
                var freeze = SkillStops[i];
                if (freeze.owner != this || freeze.victim != victim)
                    continue;

                freeze.expires = Mathf.Max(freeze.expires, expiry);
                if (frozen)
                    freeze.frozenUntil = Mathf.Max(freeze.frozenUntil, expiry);
                SkillStops[i] = freeze;
                return;
            }

            SkillStops.Add(new SkillStop { owner = this, victim = victim, expires = expiry, frozenUntil = frozen ? expiry : 0f });
        }

        internal static bool IsFrozenByRaidSkill(Health victim, float now)
        {
            foreach (var stop in SkillStops)
                if (stop.victim == victim && now < stop.frozenUntil && stop.owner.HasLiveCaster)
                    return true;
            return false;
        }

        private void ExpireSkillStops(float now)
        {
            for (int i = SkillStops.Count - 1; i >= 0; i--)
            {
                var freeze = SkillStops[i];
                if (freeze.owner == this && (!freeze.victim || freeze.victim.CurrentHealth <= 0 || now >= freeze.expires))
                    SkillStops.RemoveAt(i);
            }
        }

        private void ClearSkillStops()
        {
            for (int i = SkillStops.Count - 1; i >= 0; i--)
            {
                if (SkillStops[i].owner == this)
                    SkillStops.RemoveAt(i);
            }
        }

        internal static bool IsStoppedBySkill(Health target, float now = float.NegativeInfinity)
        {
            foreach (var freeze in SkillStops)
            {
                if (freeze.victim == target && now < freeze.expires && freeze.owner.HasLiveCaster)
                    return true;
            }

            return false;
        }

        internal static bool IsStoppedByRaidControl(Health target, float now) =>
            IsStoppedBySkill(target, now) || IsStoppedByTimeTrap(target, now);
    }
}

