using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string BlessingUnlock = "active_freya_E";
        public const string BlessingEvolution = "active_freya_E_evolve";
        private int blessingRank, blessingTicks;
        private float blessingReady, blessingNextTick;
        private bool blessingEvolved;
        private Vector3 blessingCenter;
        private AudioClip blessingSound;
        private readonly List<Health> blessingAllies = new List<Health>();
        private int heroHymnRank;
        private static readonly int[] LastStandLevels = { 1, 8, 15 };
        private int lastStandRank, lastStandCriticalChance;
        private bool lastStandSampled;
        private Vector3 lastStandPosition;
        private float lastStandSince;

        private bool CanChooseLastStand() => HasLiveCaster && PendingRewards > 0 && lastStandRank < LastStandLevels.Length &&
            raidLevel >= LastStandLevels[lastStandRank] && (lastStandRank != 0 || progress.IsLearned("passive_freya_I"));

        internal bool TryChooseLastStand()
        {
            if (!CanChooseLastStand())
                return false;

            lastStandRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private void TickLastStand(float now)
        {
            if (lastStandRank == 0)
                return;

            Vector3 position = caster.transform.position;
            bool moved = !lastStandSampled || (position - lastStandPosition).sqrMagnitude > 0.000001f;
            lastStandSampled = true;
            lastStandPosition = position;
            if (caster is CharacterInstance character)
            {
                Vector3 velocity = formationController && formationController.enabled ? formationController.velocity : Vector3.zero;
                moved |= !IsFormationStationary(character.IsGrounded, character.movementInput, velocity);
            }

            if (moved)
                lastStandSince = now;

            float requiredSeconds = lastStandRank == 1 ? 0.4f : lastStandRank == 2 ? 0.3f : 0.2f;
            lastStandCriticalChance = now >= lastStandSince + requiredSeconds ? lastStandRank * 5 : 0;
        }

        private bool CanChooseHeroHymn() => HasLiveCaster && PendingRewards > 0 && heroHymnRank < 2 &&
            (heroHymnRank == 0 ? progress.IsLearned("passive_freya_H") : raidLevel >= 8);

        internal bool TryChooseHeroHymn()
        {
            if (!CanChooseHeroHymn())
                return false;

            heroHymnRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static int ScaleRaidHealing(Health target, int healing) =>
            healing > 0 && RaidStatOwners.TryGetValue(target, out var owner)
                ? ScaleHealingAmount(healing, owner.heroHymnRank * 20 + (owner.HagalazEvolved ? 30 : 0))
                : healing;

        private bool CanChooseBlessing() => PendingRewards > 0 && blessingRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[blessingRank] && progress.IsLearned(BlessingUnlock) &&
            (blessingRank < 3 || progress.IsLearned(BlessingEvolution));

        internal bool TryChooseBlessing()
        {
            if (!CanChooseBlessing())
                return false;

            blessingRank++;
            rewardsSpent++;
            return true;
        }

        private void TryBlessing(float now)
        {
            if (blessingRank == 0 || blessingTicks > 0 || now < blessingReady || !progress.IsLearned(BlessingUnlock))
                return;

            int rank = Mathf.Min(blessingRank, 3);
            blessingEvolved = blessingRank == 4 && progress.IsLearned(BlessingEvolution);
            blessingReady = now + ScaleSkillCooldown(blessingEvolved ? 20f : 33f - 3f * rank);
            blessingCenter = caster.transform.position;
            PlayGodCastAudio(ref blessingSound, "Jotunnslayer/FreyaBlessingOriginal", blessingCenter);
            blessingNextTick = now + 1f;
            blessingTicks = blessingEvolved ? 7 : 3 + rank;
            blessingCastCount = RollAnsuzRepeat(ref blessingReady) ? 2 : 1;
        }

        private void TickBlessing(float now)
        {
            if (blessingTicks == 0 || now < blessingNextTick)
                return;

            if (!progress.IsLearned(BlessingUnlock))
            {
                blessingTicks = 0;
                return;
            }

            blessingTicks--;
            blessingNextTick += 1f;
            for (int i = 0; i < blessingCastCount; i++)
                PulseBlessing();
        }

        private int blessingCastCount;

        private void PulseBlessing()
        {
            float radius = blessingEvolved ? 3f : 2.5f;
            blessingAllies.Clear();
            foreach (var ally in GameManager.HealthInstances.Values)
            {
                if (ally && ally.isServer && ally.Team == caster.Team && ally.CurrentHealth > 0 &&
                    ally.CurrentHealth < ally.MaxHealth && (ally.transform.position - blessingCenter).sqrMagnitude <= radius * radius)
                    blessingAllies.Add(ally);
            }

            foreach (var ally in blessingAllies)
                ally.ServerHeal(Mathf.Max(1, Mathf.RoundToInt(ally.MaxHealth * 0.05f)), caster.netId, false);

            if (blessingEvolved)
                StrikeGodDamage(blessingCenter, radius, 8);
        }
    }
}
