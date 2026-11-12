using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ThrowingEvolution = "active_berserker_D_evolve";
        private int throwingRank;
        private float throwingReady;

        private bool CanChooseThrowingAxes() => AllowsRaidClass(RaidClass.Berserker) && PendingRewards > 0 && throwingRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[throwingRank] && (throwingRank < 3 || progress.IsLearned(ThrowingEvolution));

        internal bool TryChooseThrowingAxes()
        {
            if (!CanChooseThrowingAxes())
                return false;

            throwingRank++;
            rewardsSpent++;
            return true;
        }

        private void TryThrowingAxes(float now)
        {
            if (throwingRank == 0 || now < throwingReady)
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, 10f);
            for (int i = targets.Count - 1; i >= 0; i--)
            {
                if ((targets[i].transform.position - origin).sqrMagnitude < 4f)
                    targets.RemoveAt(i);
            }

            if (targets.Count == 0)
                return;

            var primary = targets[Random.Range(0, targets.Count)];
            Vector3 impact = primary.transform.position;
            int rank = Mathf.Min(throwingRank, 3);
            bool evolved = throwingRank == 4 && progress.IsLearned(ThrowingEvolution);
            throwingReady = now + ScaleSkillCooldown(evolved ? 2.5f : 1.5f - 0.25f * rank);
            Strike(impact, evolved ? 2.5f : 1f, ScaleClassSkillDamage(evolved ? 40 : 15 + rank * 5));

            if (!evolved)
                return;

            // Split into four short lanes. A target can be hit by at most one fragment.
            CollectTargets(impact, 3f);
            foreach (var target in targets)
            {
                if (target == primary)
                    continue;

                Vector3 offset = target.transform.position - impact;
                Vector3 lane = Mathf.Abs(offset.x) > Mathf.Abs(offset.z) ? Vector3.right : Vector3.forward;
                Vector3 lateral = offset - lane * Vector3.Dot(offset, lane);
                if (lateral.sqrMagnitude <= 0.36f)
                    Deal(target, ScaleClassSkillDamage(20));
            }
        }
    }
}
