using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string VortexEvolution = "active_njord_C_evolve";
        private int vortexRank, vortexCastRank, vortexTick;
        private float vortexReady, vortexStarted, vortexExpiry, vortexYaw;
        private Vector3 vortexOrigin;
        private bool vortexActive;

        private bool CanChooseVortex() => PendingRewards > 0 && vortexRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[vortexRank] && (vortexRank < 3 || progress.IsLearned(VortexEvolution));

        internal bool TryChooseVortex()
        {
            if (!CanChooseVortex())
                return false;

            vortexRank++;
            rewardsSpent++;
            return true;
        }

        private void TryVortex(float now)
        {
            if (vortexRank == 0 || vortexActive || now < vortexReady)
                return;

            vortexCastRank = vortexRank == 4 && !progress.IsLearned(VortexEvolution) ? 3 : vortexRank;
            vortexOrigin = caster.transform.position;
            vortexYaw = caster.transform.eulerAngles.y;
            vortexStarted = now;
            vortexExpiry = now + (vortexCastRank == 4 ? 4f : 1.5f + vortexCastRank);
            vortexReady = now + ScaleSkillCooldown(8f);
            vortexTick = 1;
            vortexActive = true;
            vortexCastCount = RollAnsuzRepeat(ref vortexReady) ? 2 : 1;
        }

        private void TickVortex(float now)
        {
            if (!vortexActive)
                return;

            if (vortexCastRank == 4)
            {
                while (vortexTick <= 4 && now >= vortexStarted + vortexTick)
                {
                    for (int i = 0; i < 3 * vortexCastCount; i++)
                    {
                        Vector3 direction = Quaternion.Euler(0f, vortexYaw + i % 3 * 120f, 0f) * Vector3.forward;
                        Vector3 center = vortexOrigin + direction * (vortexTick * 2f);
                        PullTargets(center, 4f, int.MaxValue);
                        StrikeGodDamage(center, 1.5f, 15);
                    }

                    vortexTick++;
                }
            }
            else
            {
                Vector3 center = vortexOrigin + Quaternion.Euler(0f, vortexYaw, 0f) * Vector3.forward * 3f;
                for (int i = 0; i < vortexCastCount; i++)
                {
                    PullTargets(center, 4f, int.MaxValue);
                    if (now >= vortexExpiry)
                        StrikeGodDamage(center, 1.5f, 5 + vortexCastRank * 10);
                }
            }

            if (now >= vortexExpiry)
                vortexActive = false;
        }

        private int vortexCastCount;
    }
}
