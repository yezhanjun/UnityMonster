using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ViperEvolution = "active_loki_D_evolve";
        private int viperRank;
        private float viperReady;
        private readonly List<Viper> vipers = new List<Viper>(20);

        private struct Viper
        {
            public Vector3 start, end;
            public float impactAt;
            public bool splits;
        }

        private bool CanChooseVipers() => PendingRewards > 0 && viperRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[viperRank] && (viperRank < 3 || progress.IsLearned(ViperEvolution));

        internal bool TryChooseVipers()
        {
            if (!CanChooseVipers())
                return false;

            viperRank++;
            rewardsSpent++;
            return true;
        }

        private void TryVipers(float now)
        {
            if (viperRank == 0 || now < viperReady)
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, MissileRange);
            if (targets.Count == 0)
                return;

            bool evolved = viperRank == 4 && progress.IsLearned(ViperEvolution);
            viperReady = now + ScaleSkillCooldown(4f);
            int count = 1 + Mathf.Min(viperRank, 3);
            LaunchVipers(origin, now, count, evolved);
            if (RollAnsuzRepeat(ref viperReady))
                LaunchVipers(origin, now, count, evolved);
        }

        private void LaunchVipers(Vector3 origin, float now, int count, bool split)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 end = targets.Count == 0
                    ? origin + Quaternion.Euler(0, i * 360f / count, 0) * Vector3.forward * 3f
                    : targets[i % targets.Count].transform.position;
                vipers.Add(new Viper { start = origin, end = end, impactAt = now + 0.3f, splits = split });
            }
        }

        private void TickVipers(float now)
        {
            for (int i = vipers.Count - 1; i >= 0; i--)
            {
                var viper = vipers[i];
                if (now < viper.impactAt)
                    continue;

                vipers.RemoveAt(i);
                SweepGodDamage(viper.start, viper.end, 15);
                if (!viper.splits)
                    continue;

                CollectTargets(viper.end, 6f);
                LaunchVipers(viper.end, now, 4, false);
            }
        }
    }
}
