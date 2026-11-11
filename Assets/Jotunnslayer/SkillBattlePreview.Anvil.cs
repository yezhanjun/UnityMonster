using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string AnvilEvolution = "active_warden_D_evolve";
        private const float AnvilImpactRadius = 1.5f;
        private readonly Vector3[] anvilCenters = new Vector3[3];
        private int anvilRank, anvilCount, anvilSecondaryDamage;
        private float anvilReady, anvilPullAt;

        private bool CanChooseAnvil() => AllowsRaidClass(RaidClass.Warden) && PendingRewards > 0 && anvilRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[anvilRank] &&
            (anvilRank < 3 || progress.IsLearned(AnvilEvolution));

        internal bool TryChooseAnvil()
        {
            if (!CanChooseAnvil())
                return false;

            anvilRank++;
            rewardsSpent++;
            return true;
        }

        private void TryAnvil(float now)
        {
            if (anvilRank == 0 || anvilCount != 0 || now < anvilReady)
                return;

            Vector3 origin = caster.transform.position;
            bool evolved = anvilRank == 4 && progress.IsLearned(AnvilEvolution);
            anvilCount = SelectNearestImpactCenters(origin, 12f, anvilCenters, evolved ? 3 : 1);
            if (anvilCount == 0)
                return;

            int rank = Mathf.Min(anvilRank, 3);
            anvilReady = now + ScaleSkillCooldown(evolved ? 3f : 3.5f - 0.5f * rank);
            anvilPullAt = now + 1f;
            anvilSecondaryDamage = evolved ? 20 : 6 + 4 * rank;
            for (int i = 0; i < anvilCount; i++)
                Strike(anvilCenters[i], AnvilImpactRadius, ScaleClassSkillDamage(evolved ? 28 : 24));
        }

        private int SelectNearestImpactCenters(Vector3 origin, float range, Vector3[] centers, int limit)
        {
            CollectTargets(origin, range);
            int count = Mathf.Min(targets.Count, limit);
            for (int i = 0; i < count; i++)
            {
                int nearest = 0;
                float distance = (targets[0].transform.position - origin).sqrMagnitude;
                for (int j = 1; j < targets.Count; j++)
                {
                    float candidateDistance = (targets[j].transform.position - origin).sqrMagnitude;
                    if (candidateDistance < distance)
                    {
                        nearest = j;
                        distance = candidateDistance;
                    }
                }

                centers[i] = targets[nearest].transform.position;
                targets.RemoveAt(nearest);
            }

            return count;
        }

        private void TickAnvil(float now)
        {
            if (anvilCount == 0 || now < anvilPullAt)
                return;

            for (int i = 0; i < anvilCount; i++)
            {
                Vector3 center = anvilCenters[i];
                PullTargets(center, 3f, int.MaxValue);
                Strike(center, AnvilImpactRadius, ScaleClassSkillDamage(anvilSecondaryDamage));
            }

            anvilCount = 0;
        }
    }
}
