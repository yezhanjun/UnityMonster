using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string LeviathanEvolution = "active_njord_A_evolve";
        private int leviathanRank, leviathanDamage;
        private float leviathanReady;
        private bool leviathanEvolved;
        private readonly LeviathanCast[] leviathanCasts = new LeviathanCast[2];
        private readonly List<LeviathanBleed> leviathanBleeds = new List<LeviathanBleed>();

        private struct LeviathanCast
        {
            public Vector3 point;
            public Health target;
            public float next;
            public int hits;
        }

        private struct LeviathanBleed
        {
            public Health target;
            public float next;
            public int ticks;
        }

        private bool CanChooseLeviathan() => PendingRewards > 0 && leviathanRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[leviathanRank] && (leviathanRank < 3 || progress.IsLearned(LeviathanEvolution));

        internal bool TryChooseLeviathan()
        {
            if (!CanChooseLeviathan())
                return false;

            leviathanRank++;
            rewardsSpent++;
            return true;
        }

        private void TryLeviathan(float now)
        {
            if (leviathanRank == 0 || leviathanCasts[0].hits > 0 || leviathanCasts[1].hits > 0 ||
                now < leviathanReady || !StartLeviathanCast(ref leviathanCasts[0], now))
                return;

            int rank = Mathf.Min(leviathanRank, 3);
            leviathanEvolved = leviathanRank == 4 && progress.IsLearned(LeviathanEvolution);
            leviathanDamage = leviathanEvolved ? 40 : 15 + rank * 5;
            leviathanReady = now + ScaleSkillCooldown(7f - rank);
            if (RollAnsuzRepeat(ref leviathanReady))
                StartLeviathanCast(ref leviathanCasts[1], now);
        }

        private bool StartLeviathanCast(ref LeviathanCast cast, float now)
        {
            if (!SelectLeviathanPoint(ref cast))
                return false;

            cast.next = now + 0.25f;
            cast.hits = 2;
            return true;
        }

        private bool SelectLeviathanPoint(ref LeviathanCast cast)
        {
            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count > 1)
                targets.Remove(cast.target);
            if (targets.Count == 0)
                return false;

            cast.target = targets[Random.Range(0, targets.Count)];
            cast.point = cast.target.transform.position;
            return true;
        }

        private void TickLeviathan(float now)
        {
            for (int i = 0; i < leviathanCasts.Length; i++)
            {
                ref LeviathanCast cast = ref leviathanCasts[i];
                if (cast.hits == 0 || now < cast.next)
                    continue;

                CollectTargets(cast.point, 2f);
                foreach (var target in targets)
                {
                    DealGodDamage(target, leviathanDamage);
                    if (leviathanEvolved && target && target.CurrentHealth > 0 && Random.value < 0.25f)
                        leviathanBleeds.Add(new LeviathanBleed { target = target, next = now + 1f, ticks = 4 });
                }

                cast.hits--;
                if (cast.hits == 0 && leviathanEvolved && now + 0.5f < leviathanReady && Random.value < 0.5f)
                    cast.hits = 1;

                if (cast.hits == 0 || !SelectLeviathanPoint(ref cast))
                    cast = default;
                else
                    cast.next = now + 0.5f;
            }

            for (int i = leviathanBleeds.Count - 1; i >= 0; i--)
            {
                var bleed = leviathanBleeds[i];
                while (bleed.target && bleed.target.CurrentHealth > 0 && bleed.ticks > 0 && now >= bleed.next)
                {
                    DealGodDamage(bleed.target, 8);
                    bleed.ticks--;
                    bleed.next += 1f;
                }

                if (!bleed.target || bleed.target.CurrentHealth <= 0 || bleed.ticks == 0)
                    leviathanBleeds.RemoveAt(i);
                else
                    leviathanBleeds[i] = bleed;
            }
        }

        private void ClearLeviathan()
        {
            System.Array.Clear(leviathanCasts, 0, leviathanCasts.Length);
            leviathanBleeds.Clear();
        }
    }
}
