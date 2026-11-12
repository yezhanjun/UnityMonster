using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SkullEvolution = "active_ranger_B_evolve";
        private int skullRank;
        private float skullReady;

        private bool CanChooseSkull() => AllowsRaidClass(RaidClass.Ranger) && PendingRewards > 0 && skullRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[skullRank] && (skullRank < 3 || progress.IsLearned(SkullEvolution));

        internal bool TryChooseSkull()
        {
            if (!CanChooseSkull())
                return false;

            skullRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySkull(float now)
        {
            if (skullRank == 0 || now < skullReady)
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, MissileRange);
            Health primary = null;
            float closest = float.PositiveInfinity;
            foreach (Health candidate in targets)
            {
                float distance = (candidate.transform.position - origin).sqrMagnitude;
                if (distance >= closest)
                    continue;

                closest = distance;
                primary = candidate;
            }

            if (!primary)
                return;

            skullReady = now + ScaleSkillCooldown(2.5f);
            bool evolved = skullRank == 4 && progress.IsLearned(SkullEvolution);
            int rank = Mathf.Min(skullRank, 3);
            Vector3 impact = primary.transform.position;
            Vector3 forward = impact - origin;
            forward.y = 0;
            if (forward.sqrMagnitude < 0.0001f)
                forward = caster.transform.forward;

            HitSkullTarget(primary, 14 + rank * 6, evolved, now);

            CollectTargets(impact, 3.5f + rank * 0.5f);
            foreach (Health target in targets)
            {
                if (target == primary)
                    continue;

                Vector3 offset = target.transform.position - impact;
                offset.y = 0;
                if (!evolved && Vector3.Angle(forward, offset) > 45f)
                    continue;

                HitSkullTarget(target, evolved ? 26 : 18, evolved, now);
            }
        }

        private void HitSkullTarget(Health target, int damage, bool stun, float now)
        {
            int before = target.CurrentHealth;
            DealRangerSkillDamage(target, damage);
            if (!stun || !target || target.CurrentHealth <= 0 || target.CurrentHealth >= before)
                return;

            if (target.TryGetComponent<RangeResearchEnemy>(out var enemy) && enemy.Archetype == 2)
                return;

            StopSkillTarget(target, now + 2f);
        }
    }
}
