using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string DiscEvolution = "active_freya_D_evolve";
        private int discRank, discStage, discCount;
        private bool discsEvolved;
        private float discReady, discNext, discYaw;
        private Vector3 discOrigin;

        private bool CanChooseDiscs() => PendingRewards > 0 && discRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[discRank] && (discRank < 3 || progress.IsLearned(DiscEvolution));

        internal bool TryChooseDiscs()
        {
            if (!CanChooseDiscs())
                return false;

            discRank++;
            rewardsSpent++;
            return true;
        }

        private void TryDiscs(float now)
        {
            if (discRank == 0 || discStage != 0 || now < discReady)
                return;

            discsEvolved = discRank == 4 && progress.IsLearned(DiscEvolution);
            discCount = Mathf.Min(discRank, 3);
            discReady = now + ScaleSkillCooldown(4f);
            discNext = now + 0.5f;
            discOrigin = caster.transform.position;
            discYaw = caster.transform.eulerAngles.y;
            discStage = 1;
            discCastCount = RollAnsuzRepeat(ref discReady) ? 2 : 1;
        }

        private void TickDiscs(float now)
        {
            if (discStage == 0 || now < discNext)
                return;

            bool returning = discStage == 2;
            Vector3 destination = returning ? caster.transform.position : discOrigin;
            for (int i = 0; i < discCount * discCastCount; i++)
            {
                float angle = discYaw + (i % discCount - (discCount - 1) * 0.5f) * 30f;
                Vector3 turnaround = discOrigin + Quaternion.Euler(0, angle, 0) * Vector3.forward * 8f;
                SweepGodDamage(destination, turnaround, returning ? 20 : 23, discsEvolved ? 1f : 0.5f);
            }

            discStage = !returning && discsEvolved ? 2 : 0;
            discNext += 0.5f;
        }

        private int discCastCount;
    }
}
