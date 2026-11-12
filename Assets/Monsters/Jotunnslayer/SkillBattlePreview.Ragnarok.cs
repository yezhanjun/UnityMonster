using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string RagnarokUnlock = "active_odin_C";
        public const string RagnarokEvolution = "active_odin_C_evolve";
        private int ragnarokRank;
        private float ragnarokReady;

        private bool CanChooseRagnarok() => PendingRewards > 0 && ragnarokRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[ragnarokRank] && progress.IsLearned(RagnarokUnlock) &&
            (ragnarokRank < 3 || progress.IsLearned(RagnarokEvolution));

        internal bool TryChooseRagnarok()
        {
            if (!CanChooseRagnarok())
                return false;

            ragnarokRank++;
            rewardsSpent++;
            return true;
        }

        private void TryRagnarok(float now)
        {
            if (ragnarokRank == 0 || now < ragnarokReady || !progress.IsLearned(RagnarokUnlock))
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            bool evolved = ragnarokRank == 4 && progress.IsLearned(RagnarokEvolution);
            ragnarokReady = now + ScaleSkillCooldown(evolved ? 30f : 50f - Mathf.Min(ragnarokRank, 3) * 5f);
            StrikeRagnarok(now, evolved);
            if (RollAnsuzRepeat(ref ragnarokReady))
            {
                CollectTargets(caster.transform.position, MissileRange);
                StrikeRagnarok(now, evolved);
            }
        }

        private void StrikeRagnarok(float now, bool evolved)
        {
            IndicateRing(caster.transform.position, MissileRange);
            foreach (var target in targets)
            {
                if (target.TryGetComponent<RangeResearchEnemy>(out var enemy) && enemy.Archetype != 0)
                {
                    if (evolved)
                        StopSkillTarget(target, now + 6f);
                }
                else
                {
                    DealGodDamage(target, evolved ? 700 : 500);
                }
            }
        }
    }
}
