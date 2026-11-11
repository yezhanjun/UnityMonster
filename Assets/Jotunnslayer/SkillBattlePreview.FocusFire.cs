using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string FocusEvolution = "active_ranger_E_evolve";
        private const float FocusCastRange = 10f;
        private const float EvolvedFocusCooldown = 0.15f;
        private static readonly float[] FocusBurstIntervals = { 0.10f, 0.08f, 0.06f };
        private const int MaxFocusCatchUpShots = 4;
        private int focusRank, focusShots;
        private float focusReady, nextFocusShot, focusBurstInterval, nextEvolvedFocusShot;

        private bool CanChooseFocus() => AllowsRaidClass(RaidClass.Ranger) && PendingRewards > 0 && focusRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[focusRank] && (focusRank < 3 || progress.IsLearned(FocusEvolution));

        internal bool TryChooseFocus()
        {
            if (!CanChooseFocus())
                return false;

            focusRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFocus(float now, bool automaticCast = false)
        {
            if (focusRank == 0 || focusShots > 0 || now < focusReady)
                return;

            if (focusRank == 4 && progress.IsLearned(FocusEvolution))
            {
                TryEvolvedFocus(now, automaticCast);
                return;
            }

            CollectTargets(caster.transform.position, FocusCastRange);
            if (targets.Count == 0)
                return;

            focusReady = now + ScaleSkillCooldown(4f);
            nextFocusShot = now;
            int rank = Mathf.Min(focusRank, 3);
            focusShots = 4 + rank * 4;
            focusBurstInterval = FocusBurstIntervals[rank - 1];
        }

        private void TickFocus(float now)
        {
            while (focusShots > 0 && now >= nextFocusShot)
            {
                focusShots--;
                CollectTargets(caster.transform.position, FocusCastRange);
                if (targets.Count > 0)
                    LaunchFocusArrow(nextFocusShot, 8);
                nextFocusShot += focusBurstInterval;
            }
        }

        private void TryEvolvedFocus(float now, bool automaticCast)
        {
            if (now < nextEvolvedFocusShot)
                return;

            if (!automaticCast || nextEvolvedFocusShot == 0f)
                nextEvolvedFocusShot = now;

            for (int shot = 0; shot < MaxFocusCatchUpShots && now >= nextEvolvedFocusShot; shot++)
            {
                CollectTargets(caster.transform.position, FocusCastRange);
                if (targets.Count == 0)
                {
                    nextEvolvedFocusShot = 0f;
                    return;
                }

                LaunchFocusArrow(nextEvolvedFocusShot, 10);
                nextEvolvedFocusShot += EvolvedFocusCooldown;
            }

            if (now >= nextEvolvedFocusShot)
                nextEvolvedFocusShot = now + EvolvedFocusCooldown;
        }

        private void LaunchFocusArrow(float now, int damage)
        {
            var target = targets[targets.Count == 1 ? 0 : Random.Range(0, targets.Count)];
            Vector3 origin = caster.transform.position + Vector3.up;
            LaunchRangerArrow(now, origin, (RaidSkillAimPoint(target) - origin).normalized, 1, damage, .3f,
                trackingTarget: target);
        }
    }
}
