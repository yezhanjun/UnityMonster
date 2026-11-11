using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string IceUnlock = "active_skadi_E";
        public const string IceEvolution = "active_skadi_E_evolve";
        private static readonly List<SkillBattlePreview> IceOwners = new List<SkillBattlePreview>();
        private readonly HashSet<Health> iceContacts = new HashSet<Health>();
        private int iceRank, iceCastCount;
        private float iceReady, iceExpires, iceRadius;
        private bool iceEvolved;
        private Vector3 iceCenter;

        private bool CanChooseIceField() => PendingRewards > 0 && iceRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[iceRank] && progress.IsLearned(IceUnlock) &&
            (iceRank < 3 || progress.IsLearned(IceEvolution));

        internal bool TryChooseIceField()
        {
            if (!CanChooseIceField())
                return false;

            iceRank++;
            rewardsSpent++;
            return true;
        }

        private void TryIceField(float now)
        {
            if (iceRank == 0 || now < iceReady || !progress.IsLearned(IceUnlock) || IceOwners.Contains(this))
                return;

            int rank = Mathf.Min(iceRank, 3);
            iceEvolved = iceRank == 4 && progress.IsLearned(IceEvolution);
            iceCenter = caster.transform.position;
            iceReady = now + ScaleSkillCooldown(iceEvolved ? 6f : 12f);
            iceExpires = now + (iceEvolved ? 5f : 4f + rank);
            iceRadius = iceEvolved ? 7f : 4f + rank;
            iceCastCount = RollAnsuzRepeat(ref iceReady) ? 2 : 1;
            IceOwners.Add(this);
        }

        private void TickIceField(float now)
        {
            if (!IceOwners.Contains(this))
                return;

            if (now >= iceExpires)
            {
                ClearIceField();
                if (iceEvolved)
                    for (int i = 0; i < iceCastCount; i++)
                        StrikeGodDamage(iceCenter, iceRadius, 50);
                return;
            }

            CollectTargets(iceCenter, iceRadius);
            foreach (var target in targets)
            {
                if (!iceContacts.Add(target))
                    continue;

                for (int i = 0; i < iceCastCount; i++)
                    if (Random.value < 0.25f)
                        StopSkillTarget(target, now + 1.8f, frozen: true);
            }
        }

        private void ClearIceField()
        {
            IceOwners.Remove(this);
            iceContacts.Clear();
        }

        internal static float IceMoveMultiplier(Health actor)
        {
            if (!actor || actor.CurrentHealth <= 0)
                return 1f;

            foreach (var owner in IceOwners)
            {
                if (owner.caster && owner.caster.CurrentHealth > 0 && owner.caster.Team == actor.Team &&
                    (actor.transform.position - owner.iceCenter).sqrMagnitude <= owner.iceRadius * owner.iceRadius)
                    return 1.35f;
            }

            return 1f;
        }
    }
}
