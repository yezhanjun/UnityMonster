using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string RaiseEvolution = "active_nidhogg_A_evolve";
        private static readonly List<SkillBattlePreview> RaiseZones = new List<SkillBattlePreview>();
        private int raiseRank, raiseDamage, raiseCastCount;
        private float raiseReady, raiseExpiry, raiseNext;
        private bool raiseEvolved;
        private Vector3 raiseOrigin, raiseRepeatOrigin, raiseForward, raiseRight;

        private bool CanChooseRaiseDead() => PendingRewards > 0 && raiseRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[raiseRank] && (raiseRank < 3 || progress.IsLearned(RaiseEvolution));

        internal bool TryChooseRaiseDead()
        {
            if (!CanChooseRaiseDead())
                return false;

            raiseRank++;
            rewardsSpent++;
            return true;
        }

        private void TryRaiseDead(float now)
        {
            if (raiseRank == 0 || now < raiseReady || RaiseZones.Contains(this))
                return;

            int rank = Mathf.Min(raiseRank, 3);
            raiseEvolved = raiseRank == 4 && progress.IsLearned(RaiseEvolution);
            var pose = caster.transform;
            raiseOrigin = pose.position;
            raiseForward = Quaternion.Euler(0, pose.eulerAngles.y, 0) * Vector3.forward;
            raiseRight = Vector3.Cross(Vector3.up, raiseForward);
            if (!raiseEvolved)
            {
                CollectTargets(raiseOrigin, MissileRange);
                if (targets.Count == 0)
                    return;
                raiseOrigin = targets[Random.Range(0, targets.Count)].transform.position;
            }

            raiseCastCount = RollAnsuzRepeat(ref raiseReady) ? 2 : 1;
            if (raiseCastCount == 2)
                raiseRepeatOrigin = raiseEvolved ? raiseOrigin : targets[Random.Range(0, targets.Count)].transform.position;
            raiseDamage = raiseEvolved ? 36 : 16 + rank * 4;
            raiseReady = now + ScaleSkillCooldown(6f);
            raiseExpiry = now + (raiseEvolved ? 5f : 2f + rank);
            raiseNext = now + 1f;
            RaiseZones.Add(this);
        }

        private bool InRaiseZone(Vector3 position, Vector3 origin)
        {
            Vector3 offset = position - origin;
            if (!raiseEvolved)
                return offset.sqrMagnitude <= 6.25f;

            float along = Vector3.Dot(offset, raiseForward);
            float side = Mathf.Abs(Vector3.Dot(offset, raiseRight));
            return along >= 0f && along <= 10f && Mathf.Abs(side - 1.5f) <= 0.75f && Mathf.Abs(offset.y) <= 1f;
        }

        private void TickRaiseDead(float now)
        {
            if (!RaiseZones.Contains(this))
                return;

            while (raiseNext <= raiseExpiry && now >= raiseNext)
            {
                raiseNext += 1f;
                StrikeRaiseZone(raiseOrigin);
                if (raiseCastCount == 2)
                    StrikeRaiseZone(raiseRepeatOrigin);
            }

            if (now >= raiseExpiry)
                ClearRaiseDead();
        }

        private void StrikeRaiseZone(Vector3 origin)
        {
            CollectTargets(origin, raiseEvolved ? 11f : 2.5f);
            foreach (var target in targets)
                if (InRaiseZone(target.transform.position, origin))
                    DealGodDamage(target, raiseDamage);
        }

        private void ClearRaiseDead() => RaiseZones.Remove(this);

        internal static bool IsSlowedByRaiseDead(Health target)
        {
            if (!target)
                return false;

            foreach (var zone in RaiseZones)
            {
                if (zone.caster && zone.caster.CurrentHealth > 0 && zone.caster.Team != target.Team &&
                    (zone.InRaiseZone(target.transform.position, zone.raiseOrigin) ||
                        (zone.raiseCastCount == 2 && zone.InRaiseZone(target.transform.position, zone.raiseRepeatOrigin))))
                    return true;
            }

            return false;
        }
    }
}
