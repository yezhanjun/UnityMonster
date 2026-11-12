using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SisterhoodEvolution = "active_rogue_D_evolve";
        private readonly List<SisterDecoy> sisterDecoys = new List<SisterDecoy>(10);
        private int sisterhoodRank;
        private float sisterhoodReady;

        private struct SisterDecoy
        {
            public Vector3 position;
            public float nextAttack, expires;
        }

        private bool CanChooseSisterhood() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 && sisterhoodRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[sisterhoodRank] &&
            (sisterhoodRank < 3 || progress.IsLearned(SisterhoodEvolution));

        internal bool TryChooseSisterhood()
        {
            if (!CanChooseSisterhood())
                return false;

            sisterhoodRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySisterhood(float now)
        {
            if (sisterhoodRank == 0 || now < sisterhoodReady)
                return;

            bool evolved = sisterhoodRank == 4 && progress.IsLearned(SisterhoodEvolution);
            int count = evolved ? 5 : 1;
            float duration = evolved ? 3f : 4f + 2f * Mathf.Min(sisterhoodRank, 3);
            sisterhoodReady = now + ScaleSkillCooldown(4.5f);
            var pose = caster.transform;
            for (int i = 0; i < count; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, pose.eulerAngles.y + i * 360f / count, 0f) * Vector3.forward;
                sisterDecoys.Add(new SisterDecoy
                {
                    position = pose.position + direction * 2f,
                    nextAttack = now,
                    expires = now + duration
                });
            }
        }

        private void TickSisterhood(float now)
        {
            for (int i = sisterDecoys.Count - 1; i >= 0; i--)
            {
                var decoy = sisterDecoys[i];
                while (now >= decoy.nextAttack && decoy.nextAttack < decoy.expires)
                {
                    CollectTargets(decoy.position, 2.5f);
                    if (targets.Count > 0)
                        Deal(targets[0], ScaleClassSkillDamage(10));
                    decoy.nextAttack += 1f;
                }

                if (now >= decoy.expires)
                    sisterDecoys.RemoveAt(i);
                else
                    sisterDecoys[i] = decoy;
            }
        }
    }
}
