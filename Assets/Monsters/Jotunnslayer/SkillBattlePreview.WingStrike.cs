using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string WingEvolution = "active_nidhogg_D_evolve";
        private int wingRank;
        private float wingReady;
        private static readonly int[] WingsOfFuryLevels = { 1, 8, 15 };
        private bool allInChosen;
        private int allInKills, wingsOfFuryRank;

        private bool CanChooseAllIn() => HasLiveCaster && PendingRewards > 0 && !allInChosen &&
            raidLevel >= 8 && progress.IsLearned("passive_nidhogg_F");

        internal bool TryChooseAllIn()
        {
            if (!CanChooseAllIn())
                return false;

            allInChosen = true;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool CanChooseWingsOfFury() => HasLiveCaster && PendingRewards > 0 &&
            wingsOfFuryRank < WingsOfFuryLevels.Length && raidLevel >= WingsOfFuryLevels[wingsOfFuryRank] &&
            (wingsOfFuryRank != 0 || progress.IsLearned("passive_nidhogg_I"));

        internal bool TryChooseWingsOfFury()
        {
            if (!CanChooseWingsOfFury())
                return false;

            wingsOfFuryRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static bool BlocksAllInHealing(Health target) =>
            RaidStatOwners.TryGetValue(target, out var owner) && owner.allInChosen;

        private bool CanChooseWingStrike() => PendingRewards > 0 && wingRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[wingRank] && (wingRank < 3 || progress.IsLearned(WingEvolution));

        internal bool TryChooseWingStrike()
        {
            if (!CanChooseWingStrike())
                return false;

            wingRank++;
            rewardsSpent++;
            return true;
        }

        private void TryWingStrike(float now)
        {
            if (wingRank == 0 || now < wingReady)
                return;

            int rank = Mathf.Min(wingRank, 3);
            bool evolved = wingRank == 4 && progress.IsLearned(WingEvolution);
            wingReady = now + ScaleSkillCooldown(5.5f - rank * 0.5f);
            StrikeWings(now, rank, evolved);
            if (RollAnsuzRepeat(ref wingReady))
                StrikeWings(now, rank, evolved);
        }

        private void StrikeWings(float now, int rank, bool evolved)
        {
            var pose = caster.transform;
            Vector3 origin = pose.position;
            Vector3 facing = Quaternion.Euler(0f, pose.eulerAngles.y, 0f) * Vector3.forward;
            IndicateSector(origin, facing, 8f, evolved ? 60f : 45f);
            CollectTargets(origin, 8f);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - origin;
                offset.y = 0;
                if (Vector3.Angle(facing, offset) > (evolved ? 60f : 45f))
                    continue;

                DealGodDamage(target, evolved ? 35 : 10 + 5 * rank);
                if (evolved && target && target.CurrentHealth > 0 && Random.value < 0.25f)
                    StopSkillTarget(target, now + 2f);
            }
        }
    }
}
