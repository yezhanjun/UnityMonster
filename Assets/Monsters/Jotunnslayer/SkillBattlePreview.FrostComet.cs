using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string CometEvolution = "active_skadi_A_evolve";
        private static readonly int[] CometDamage = { 20, 27, 35, 40 };
        private int cometRank, cometCastRank;
        private float cometReady, cometImpact;
        private int pendingCometCount;
        private readonly Vector3[] cometPoints = new Vector3[2];

        private bool CanChooseComet() => PendingRewards > 0 && cometRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[cometRank] && (cometRank < 3 || progress.IsLearned(CometEvolution));

        internal bool TryChooseComet()
        {
            if (!CanChooseComet())
                return false;

            cometRank++;
            rewardsSpent++;
            return true;
        }

        private void TryComet(float now)
        {
            if (cometRank == 0 || pendingCometCount != 0 || now < cometReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            cometCastRank = cometRank == 4 && !progress.IsLearned(CometEvolution) ? 3 : cometRank;
            cometPoints[0] = targets[Random.Range(0, targets.Count)].transform.position;
            cometReady = now + ScaleSkillCooldown(3.5f);
            cometImpact = now + (cometCastRank == 4 ? 1f : 0.5f);
            pendingCometCount = 1;
            if (RollAnsuzRepeat(ref cometReady))
                cometPoints[pendingCometCount++] = targets[Random.Range(0, targets.Count)].transform.position;
        }

        private void TickComet(float now)
        {
            if (pendingCometCount == 0 || now < cometImpact)
                return;

            int count = pendingCometCount;
            pendingCometCount = 0;
            for (int i = 0; i < count; i++)
                ImpactComet(cometPoints[i]);
        }

        private void ImpactComet(Vector3 cometPoint)
        {
            StrikeGodDamage(cometPoint, Mathf.Min(cometCastRank + 1, 4), CometDamage[cometCastRank - 1]);
            if (cometCastRank != 4)
                return;

            CollectTargets(cometPoint, 5f);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - cometPoint;
                float angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                Vector3 spike = Quaternion.Euler(0, Mathf.Round(angle / 45f) * 45f, 0) * Vector3.forward * 4f;
                if ((offset - spike).sqrMagnitude <= 1f)
                    DealGodDamage(target, 15);
            }
        }
    }
}
