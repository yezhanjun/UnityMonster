using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string RegenerationEvolution = "passive_broksindri_J_evolve";
        private static readonly Dictionary<Health, SkillBattlePreview> RuneRevives =
            new Dictionary<Health, SkillBattlePreview>();
        private int regenRank;
        private int thorRegenHalfPoints, regenRemainder;
        private float regenNext;
        private bool runeReviveSpent, freyaReviveAvailable;
        private bool divineRebirthEnabled;
        private readonly List<Vector3> divineRebirthImpacts = new List<Vector3>(2);
        private bool HasRuneRevive => regenRank == 4 && !runeReviveSpent &&
            progress != null && progress.IsLearned(RegenerationEvolution);

        private bool CanChooseRegenerationRune() => PendingRewards > 0 && regenRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[regenRank] && (regenRank < 3 || progress.IsLearned(RegenerationEvolution));

        internal bool TryChooseRegenerationRune()
        {
            if (!CanChooseRegenerationRune())
                return false;

            regenRank++;
            RegisterRuneRevive();
            rewardsSpent++;
            return true;
        }

        internal static bool TryConsumeRuneRevive(Health actor)
        {
            if (!RuneRevives.TryGetValue(actor, out var owner) || !owner || !owner.isActiveAndEnabled)
                return false;

            if (owner.freyaReviveAvailable)
                owner.freyaReviveAvailable = false;
            else if (owner.HasRuneRevive)
                owner.runeReviveSpent = true;
            else
                return false;

            if (!owner.HasRuneRevive)
                RuneRevives.Remove(actor);
            if (owner.divineRebirthEnabled)
                owner.divineRebirthImpacts.Add(actor.transform.position);
            return true;
        }

        private void TickDivineRebirth()
        {
            foreach (Vector3 position in divineRebirthImpacts)
                Strike(position, 8f, 120);
            divineRebirthImpacts.Clear();
        }

        private void RegisterRuneRevive()
        {
            if (caster && caster.isServer && (freyaReviveAvailable || HasRuneRevive))
                RuneRevives[caster] = this;
        }

        private void ClearRuneRevive()
        {
            if (!ReferenceEquals(caster, null) && RuneRevives.TryGetValue(caster, out var owner) && owner == this)
                RuneRevives.Remove(caster);
        }

        private void TickRegenerationRune(float now)
        {
            if (regenRank == 0 && thorRegenHalfPoints == 0)
                return;

            if (regenNext == 0)
                regenNext = now + 1f;

            if (now < regenNext)
                return;

            int ticks = 1 + Mathf.FloorToInt(now - regenNext);
            regenNext += ticks;
            if (caster.CurrentHealth >= caster.MaxHealth)
            {
                regenRemainder = 0;
                return;
            }

            int runeHealing = regenRank == 4 && progress.IsLearned(RegenerationEvolution) ? 6 :
                regenRank > 0 ? 1 + Mathf.Min(regenRank, 3) : 0;
            int healingHalfPoints = (runeHealing * 2 + thorRegenHalfPoints) * ticks + regenRemainder;
            regenRemainder = healingHalfPoints % 2;
            caster.ServerHeal(healingHalfPoints / 2, caster.netId, false);
        }

        private static int ReadThorRegenHalfPoints(SkillProgress skillProgress)
        {
            if (skillProgress.IsLearned("virtue_thor_03_b")) return 6;
            return skillProgress.IsLearned("virtue_thor_03_a") ? 3 : 0;
        }
    }
}

