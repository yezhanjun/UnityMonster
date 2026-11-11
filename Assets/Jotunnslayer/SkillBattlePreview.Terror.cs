using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string TerrorEvolution = "active_nidhogg_C_evolve";
        private static readonly List<TerrorMark> TerrorMarks = new List<TerrorMark>();
        private int terrorRank;
        private float terrorReady;

        private struct TerrorMark
        {
            public SkillBattlePreview source;
            public Health target;
            public Vector3 position;
            public float expires;
            public bool bomb;
        }

        private bool CanChooseTerror() => PendingRewards > 0 && terrorRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[terrorRank] && (terrorRank < 3 || progress.IsLearned(TerrorEvolution));

        internal bool TryChooseTerror()
        {
            if (!CanChooseTerror())
                return false;

            terrorRank++;
            rewardsSpent++;
            return true;
        }

        private void TryTerror(float now)
        {
            if (terrorRank == 0 || now < terrorReady)
                return;

            terrorReady = now + ScaleSkillCooldown(1f);
            bool evolved = terrorRank == 4 && progress.IsLearned(TerrorEvolution);
            int rank = evolved ? 4 : Mathf.Min(terrorRank, 3);
            CastTerror(now, rank, evolved);
            if (RollAnsuzRepeat(ref terrorReady))
                CastTerror(now, rank, evolved);
        }

        private void CastTerror(float now, int rank, bool evolved)
        {
            IndicateRing(caster.transform.position, 3.5f);
            CollectTargets(caster.transform.position, 3.5f);
            foreach (var target in targets)
            {
                DealGodDamage(target, rank * 3);
                if (!target || target.CurrentHealth <= 0)
                    continue;

                if (Random.value < (rank + 1) * 0.01f)
                    TerrorMarks.Add(new TerrorMark { source = this, target = target, expires = now + 1.5f });
                if (evolved && Random.value < 0.2f)
                    TerrorMarks.Add(new TerrorMark { source = this, target = target, position = target.transform.position,
                        expires = now + 1f, bomb = true });
            }
        }

        private void TickTerror(float now)
        {
            for (int i = TerrorMarks.Count - 1; i >= 0; i--)
            {
                var mark = TerrorMarks[i];
                if (mark.source != this)
                    continue;

                bool alive = mark.target && mark.target.CurrentHealth > 0;
                if (alive && now < mark.expires)
                {
                    if (mark.bomb)
                    {
                        mark.position = mark.target.transform.position;
                        TerrorMarks[i] = mark;
                    }
                    continue;
                }

                TerrorMarks.RemoveAt(i);
                if (mark.bomb)
                    StrikeGodDamage(mark.target ? mark.target.transform.position : mark.position, 2f, 30);
            }
        }

        private void ClearTerror()
        {
            for (int i = TerrorMarks.Count - 1; i >= 0; i--)
            {
                if (TerrorMarks[i].source == this)
                    TerrorMarks.RemoveAt(i);
            }
        }

        internal static bool TryGetTerrorDirection(Health actor, out Vector3 direction)
        {
            foreach (var mark in TerrorMarks)
            {
                if (mark.bomb || mark.target != actor || !mark.source.caster || mark.source.caster.CurrentHealth <= 0)
                    continue;
                direction = actor.transform.position - mark.source.caster.transform.position;
                direction.y = 0;
                if (direction.sqrMagnitude > 0.0001f)
                    return true;
            }

            direction = Vector3.zero;
            return false;
        }
    }
}
