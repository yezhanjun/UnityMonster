using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string FlamingRayEvolution = "active_rogue_C_evolve";
        private const float FlamingRayInterval = 0.25f;
        private const int FlamingRayPulses = 12;
        private int flamingRayRank, flamingRayPulse, flamingRayCount, flamingRayDamage;
        private float flamingRayReady, flamingRayStarted, flamingRayYaw, flamingRayRange, flamingRayBurnChance;

        private bool CanChooseFlamingRay() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 && flamingRayRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[flamingRayRank] &&
            (flamingRayRank < 3 || progress.IsLearned(FlamingRayEvolution));

        internal bool TryChooseFlamingRay()
        {
            if (!CanChooseFlamingRay())
                return false;

            flamingRayRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFlamingRay(float now)
        {
            if (flamingRayRank == 0 || flamingRayCount != 0 || now < flamingRayReady)
                return;

            int rank = Mathf.Min(flamingRayRank, 3);
            bool evolved = flamingRayRank == 4 && progress.IsLearned(FlamingRayEvolution);
            flamingRayCount = evolved ? 3 : 1;
            flamingRayDamage = evolved ? 20 : 6 + 4 * rank;
            flamingRayRange = evolved ? 5f : 1.5f + rank;
            flamingRayBurnChance = evolved ? 0.1f : 0.2f;
            flamingRayReady = now + ScaleSkillCooldown(7f - rank);
            flamingRayStarted = now;
            flamingRayYaw = caster.transform.eulerAngles.y;
            flamingRayPulse = 0;
        }

        private void TickFlamingRay(float now)
        {
            while (flamingRayCount != 0 && now >= flamingRayStarted + flamingRayPulse * FlamingRayInterval)
            {
                Vector3 origin = caster.transform.position;
                float yaw = flamingRayYaw + flamingRayPulse * 30f;
                for (int ray = 0; ray < flamingRayCount; ray++)
                {
                    Vector3 direction = Quaternion.Euler(0f, yaw + ray * 120f, 0f) * Vector3.forward;
                    SweepDamageLine(origin, origin + direction * flamingRayRange, ScaleClassSkillDamage(flamingRayDamage),
                        0.35f, flamingRayBurnChance, flamingRayStarted + flamingRayPulse * FlamingRayInterval);
                }

                if (++flamingRayPulse == FlamingRayPulses)
                    flamingRayCount = 0;
            }
        }
    }
}
