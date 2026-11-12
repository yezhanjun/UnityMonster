using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string FlamingDartsUnlock = "active_rogue_E";
        public const string FlamingDartsEvolution = "active_rogue_E_evolve";
        private static readonly float[] FlamingDartsCooldown = { 3f, 2.6f, 2.3f, 2f };
        private int flamingDartsRank;
        private float flamingDartsReady;

        private bool CanChooseFlamingDarts() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 && flamingDartsRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[flamingDartsRank] && progress.IsLearned(FlamingDartsUnlock) &&
            (flamingDartsRank < 3 || progress.IsLearned(FlamingDartsEvolution));

        internal bool TryChooseFlamingDarts()
        {
            if (!CanChooseFlamingDarts())
                return false;

            flamingDartsRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFlamingDarts(float now)
        {
            if (flamingDartsRank == 0 || now < flamingDartsReady || !progress.IsLearned(FlamingDartsUnlock))
                return;

            bool evolved = flamingDartsRank == 4 && progress.IsLearned(FlamingDartsEvolution);
            int rank = evolved ? 4 : Mathf.Min(flamingDartsRank, 3);
            int count = 1 + 2 * Mathf.Min(rank, 3);
            for (int i = 0; i < count; i++)
            {
                CollectTargets(caster.transform.position, MissileRange);
                if (targets.Count == 0)
                    return;

                flamingDartsReady = now + ScaleSkillCooldown(FlamingDartsCooldown[rank - 1]);
                Health target = targets[Random.Range(0, targets.Count)];
                Vector3 impact = target.transform.position;
                HitFlamingDart(target, impact, evolved);

                if (!evolved || Random.value >= 0.5f)
                    continue;

                CollectTargets(impact, 6f);
                targets.Remove(target);
                if (targets.Count > 0)
                {
                    Health bounced = targets[Random.Range(0, targets.Count)];
                    HitFlamingDart(bounced, bounced.transform.position, true);
                }
            }
        }

        private void HitFlamingDart(Health target, Vector3 impact, bool evolved)
        {
            int damage = evolved ? 20 : 14;
            Deal(target, ScaleClassSkillDamage(damage));
            if (evolved || Random.value < 0.2f)
                Strike(impact, 2.5f, ScaleClassSkillDamage(damage));
        }
    }
}
