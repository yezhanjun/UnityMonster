using UnityEngine;
using MTPSKIT;
using MTPSKIT.Gameplay;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string HealthRuneEvolution = "passive_broksindri_K_evolve";
        private static readonly int[] HealthRuneBonuses = { 0, 100, 160, 220, 350 };
        private static readonly int[] RuneRewardLevels = { 1, 8, 15, 22 };
        private int healthRuneRank, appliedMaximumHealthBonus, thorHealthBonus;
        private static readonly int[] OdinPassiveLevels = { 1, 8, 15 };
        private int olderThanTimeRank, sleipnirRank;
        private const string HagalazEvolution = "passive_broksindri_L_evolve";
        private int hagalazRank, hagalazHealingRemainder;
        private bool HagalazEvolved => hagalazRank == 4 && progress?.IsLearned(HagalazEvolution) == true;

        private bool CanChooseHagalaz() => HasLiveCaster && PendingRewards > 0 && hagalazRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[hagalazRank] && (hagalazRank < 3 || progress.IsLearned(HagalazEvolution));

        internal bool TryChooseHagalaz()
        {
            if (!CanChooseHagalaz())
                return false;

            hagalazRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static void ApplyHagalazLifesteal(Health attacker, int healthDamage)
        {
            if (!RaidStatOwners.TryGetValue(attacker, out var owner) || owner.hagalazRank == 0 ||
                owner.allInChosen || attacker.CurrentHealth <= 0 || attacker.CurrentHealth >= attacker.MaxHealth)
                return;

            int percent = owner.HagalazEvolved ? 5 : Mathf.Min(owner.hagalazRank, 3);
            long healing = (long)healthDamage * percent + owner.hagalazHealingRemainder;
            owner.hagalazHealingRemainder = (int)(healing % 100);
            if (healing >= 100)
                attacker.ServerHeal((int)(healing / 100), attacker.netId);
        }
        private static readonly int[] WardRewardLevels = { 1, 8, 15 };
        private static readonly float[] WardProtectionSeconds = { 1f, 1.5f, 1.85f, 2.2f };
        private const float WardRechargeSeconds = 35f;
        private int healingWardRank, wardCapacity, wardCharges;
        private float wardRechargeAt, wardProtectedUntil, wardRechargeInterval;
        private bool premonitionChosen;
        private bool boneChillChosen;
        private bool extractionChosen;
        private int wardEffectivenessRank;
        private const float TimeManipulationPerCharge = 0.07f;
        private bool timeManipulationChosen;
        private int TimeManipulationStacks => timeManipulationChosen ? wardCharges : 0;

        private void ResetEldritchWard(SkillProgress playerProgress)
        {
            healingWardRank = 0;
            premonitionChosen = false;
            boneChillChosen = false;
            extractionChosen = false;
            wardEffectivenessRank = 0;
            timeManipulationChosen = false;
            wardCapacity = LiveMatch && SelectedRaidClass == RaidClass.Seeress
                ? (playerProgress != null && playerProgress.IsLearned("virtue_seer_04_a") ? 5 : 3)
                : 0;
            wardCharges = wardCapacity;
            wardRechargeAt = 0f;
            int reduction = 0;
            if (wardCapacity > 0 && playerProgress != null)
            {
                if (playerProgress.IsLearned("virtue_seer_02_d")) reduction = 35;
                else if (playerProgress.IsLearned("virtue_seer_02_c")) reduction = 20;
                else if (playerProgress.IsLearned("virtue_seer_02_b")) reduction = 10;
                else if (playerProgress.IsLearned("virtue_seer_02_a")) reduction = 5;
            }
            wardRechargeInterval = WardRechargeSeconds * (100 - reduction) / 100f;
        }

        private bool CanChooseHealingWard() => LiveMatch && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && healingWardRank < WardRewardLevels.Length &&
            raidLevel >= WardRewardLevels[healingWardRank] &&
            (healingWardRank != 0 || progress.IsLearned("passive_seer_P"));

        internal bool TryChooseHealingWard()
        {
            if (!HasLiveCaster || !CanChooseHealingWard())
                return false;

            healingWardRank++;
            rewardsSpent++;
            RefreshHealthRune();
            return true;
        }

        private void RechargeEldritchWard(float now)
        {
            if (wardCharges >= wardCapacity || now < wardRechargeAt)
                return;

            while (wardCharges < wardCapacity && now >= wardRechargeAt)
            {
                wardCharges++;
                wardRechargeAt += wardRechargeInterval;
            }
            if (timeManipulationChosen)
                SyncTimeManipulation();
        }

        private bool CanChooseTimeManipulation() => HasLiveCaster && wardCapacity > 0 &&
            raidLevel >= 15 && PendingRewards > 0 && !timeManipulationChosen;

        internal bool TryChooseTimeManipulation()
        {
            if (!CanChooseTimeManipulation())
                return false;

            timeManipulationChosen = true;
            rewardsSpent++;
            SyncTimeManipulation();
            return true;
        }

        private void SyncTimeManipulation()
        {
            if (caster is CharacterInstance character)
                character.RaidTimeManipulationStacks = (byte)TimeManipulationStacks;
        }

        private bool CanChooseWardEffectiveness() => HasLiveCaster && wardCapacity > 0 &&
            PendingRewards > 0 && wardEffectivenessRank < WardRewardLevels.Length &&
            raidLevel >= WardRewardLevels[wardEffectivenessRank];

        internal bool TryChooseWardEffectiveness()
        {
            if (!CanChooseWardEffectiveness())
                return false;

            wardEffectivenessRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChoosePremonition() => HasLiveCaster && wardCapacity > 0 &&
            PendingRewards > 0 && !premonitionChosen;

        internal bool TryChoosePremonition()
        {
            if (!CanChoosePremonition())
                return false;

            premonitionChosen = true;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseBoneChill() => HasLiveCaster && wardCapacity > 0 &&
            raidLevel >= 8 && PendingRewards > 0 && !boneChillChosen;

        private bool CanChooseExtraction() => HasLiveCaster && UseExperienceCrystals && wardCapacity > 0 &&
            PendingRewards > 0 && !extractionChosen;

        internal bool TryChooseExtraction()
        {
            if (!CanChooseExtraction())
                return false;

            extractionChosen = true;
            rewardsSpent++;
            return true;
        }

        internal bool TryChooseBoneChill()
        {
            if (!CanChooseBoneChill())
                return false;

            boneChillChosen = true;
            rewardsSpent++;
            return true;
        }

        private void ApplyBoneChill(float now)
        {
            Vector3 origin = caster.transform.position;
            foreach (Health target in GameManager.HealthInstances.Values)
            {
                if (target && target.isServer && target.Team != caster.Team &&
                    (target.transform.position - origin).sqrMagnitude <= 25f)
                    StopSkillTarget(target, now + 3f, frozen: true);
            }
        }

        internal static bool IsProtectedByEldritchWard(Health victim, Health attacker, float now) =>
            victim.Team != attacker.Team && RaidStatOwners.TryGetValue(victim, out var owner) &&
            now < owner.wardProtectedUntil;

        internal static void TriggerEldritchWard(Health victim, Health attacker, float now)
        {
            if (victim.CurrentHealth <= 0 || victim.CurrentHealth >= victim.MaxHealth || victim.Team == attacker.Team ||
                !RaidStatOwners.TryGetValue(victim, out var owner) || owner.wardCapacity == 0)
                return;

            owner.RechargeEldritchWard(now);
            if (owner.wardCharges == 0)
                return;

            if (owner.wardCharges == owner.wardCapacity)
                owner.wardRechargeAt = now + owner.wardRechargeInterval;
            owner.wardCharges--;
            owner.wardProtectedUntil = now + WardProtectionSeconds[owner.wardEffectivenessRank];
            if (owner.premonitionChosen && Random.value < 0.3f)
                owner.wardCharges++;
            if (owner.timeManipulationChosen)
                owner.SyncTimeManipulation();
            if (owner.boneChillChosen)
                owner.ApplyBoneChill(now);
            if (owner.healingWardRank != 0)
                victim.ServerHeal(owner.healingWardRank * 15, victim.netId, allowOverhealBarrier: false);
            if (owner.extractionChosen && Random.value < 0.25f)
                owner.CollectExperienceCrystals(now, 30f);
        }

        private void DrawEldritchWardStatus(float now)
        {
            if (wardCapacity == 0 || Event.current.type != EventType.Repaint)
                return;

            string label = RewardText($"诡秘结界  {wardCharges}/{wardCapacity}",
                $"Eldritch Ward  {wardCharges}/{wardCapacity}");
            if (wardCharges < wardCapacity)
                label += $"  ·  {Mathf.CeilToInt(Mathf.Max(0f, wardRechargeAt - now))}s";
            GUI.Label(new Rect(16, 50, 260, 26), label, RewardLabelStyle);
        }

        private static int ReadThorHealthBonus(SkillProgress skillProgress)
        {
            if (skillProgress.IsLearned("virtue_thor_01_d")) return 150;
            if (skillProgress.IsLearned("virtue_thor_01_c")) return 80;
            if (skillProgress.IsLearned("virtue_thor_01_b")) return 40;
            return skillProgress.IsLearned("virtue_thor_01_a") ? 20 : 0;
        }

        private bool HealthRuneEvolved => healthRuneRank == 4 && progress?.IsLearned(HealthRuneEvolution) == true;

        private bool CanChooseHealthRune() => PendingRewards > 0 && healthRuneRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[healthRuneRank] &&
            (healthRuneRank < 3 || progress.IsLearned(HealthRuneEvolution));

        internal bool TryChooseHealthRune()
        {
            if (!caster || !caster.isServer || !CanChooseHealthRune())
                return false;

            healthRuneRank++;
            rewardsSpent++;
            RefreshHealthRune();
            RegisterRaidStats();
            return true;
        }

        private bool CanChooseOdinPassive(int rank, int maxRank, string id) =>
            HasLiveCaster && PendingRewards > 0 && rank < maxRank &&
            raidLevel >= OdinPassiveLevels[rank] && (rank != 0 || progress.IsLearned(id));

        private bool CanChooseOlderThanTime() => CanChooseOdinPassive(olderThanTimeRank, 2, "passive_odin_H");

        internal bool TryChooseOlderThanTime()
        {
            if (!CanChooseOlderThanTime())
                return false;

            olderThanTimeRank++;
            rewardsSpent++;
            RefreshHealthRune();
            return true;
        }

        private bool CanChooseSleipnir() => CanChooseOdinPassive(sleipnirRank, 3, "passive_odin_I");

        internal bool TryChooseSleipnir()
        {
            if (!CanChooseSleipnir())
                return false;

            sleipnirRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private void RefreshHealthRune(bool fillAddedHealth = false)
        {
            int rank = healthRuneRank == 4 && !HealthRuneEvolved ? 3 : healthRuneRank;
            SetMaximumHealthBonus(thorHealthBonus + HealthRuneBonuses[rank] + healingWardRank * 25 +
                olderThanTimeRank * 75, fillAddedHealth);
        }

        private void SetMaximumHealthBonus(int bonus, bool fillAddedHealth = false)
        {
            int change = bonus - appliedMaximumHealthBonus;
            if (change == 0)
                return;

            if (caster && caster.isServer)
            {
                bool wasFull = caster.CurrentHealth == caster.MaxHealth;
                caster.ServerSetMaximumHealth(Mathf.Max(1, caster.MaxHealth + change));
                if (fillAddedHealth && wasFull && change > 0)
                    caster.ServerHeal(change, caster.netId, allowOverhealBarrier: false);
            }

            appliedMaximumHealthBonus = bonus;
        }
    }
}
