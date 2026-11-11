using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        private static readonly Dictionary<Health, SkillBattlePreview> RaidStatOwners =
            new Dictionary<Health, SkillBattlePreview>();
        private int permanentMoveSpeedBonus;
        private SkillProgress combatStatsProfile;
        private uint combatStatsRevision;
        private RaidClass combatStatsClass;
        private bool combatStatsLiveMatch, combatStatsInitialized;
        private int cachedPermanentWeaponDamage, cachedPermanentReduction, cachedClassSkillDamage;
        private int cachedGodDamage, cachedAllDamageHalfPoints;
        private int cachedAttackSpeedHalfPoints;
        private int attackSpeedHalfPoints
        {
            get { RefreshPermanentCombatStats(); return cachedAttackSpeedHalfPoints; }
        }
        private int godDamageBonus
        {
            get { RefreshPermanentCombatStats(); return cachedGodDamage; }
        }
        private int allDamageHalfPoints
        {
            get { RefreshPermanentCombatStats(); return cachedAllDamageHalfPoints; }
        }
        private int classSkillDamageBonus
        {
            get { RefreshPermanentCombatStats(); return cachedClassSkillDamage; }
        }
        private int permanentWeaponDamageBonus
        {
            get { RefreshPermanentCombatStats(); return cachedPermanentWeaponDamage; }
        }
        private int damageReductionPercent
        {
            get { RefreshPermanentCombatStats(); return cachedPermanentReduction; }
        }

        private void RefreshPermanentCombatStats()
        {
            uint revision = progress?.LearnedRevision ?? 0;
            if (combatStatsInitialized && ReferenceEquals(combatStatsProfile, progress) &&
                combatStatsRevision == revision && combatStatsClass == SelectedRaidClass &&
                combatStatsLiveMatch == LiveMatch)
                return;

            combatStatsInitialized = true;
            combatStatsProfile = progress;
            combatStatsRevision = revision;
            combatStatsClass = SelectedRaidClass;
            combatStatsLiveMatch = LiveMatch;
            cachedPermanentWeaponDamage = progress != null ? ReadPermanentWeaponDamage(progress) : 0;
            cachedPermanentReduction = progress != null ? ReadPermanentDamageReduction(progress) : 0;
            cachedGodDamage = progress != null ? ReadGodDamageBonus(progress) : 0;
            cachedAllDamageHalfPoints = progress != null ? ReadFourTierHalfPoints(progress, "virtue_thor_02_") : 0;
            cachedAttackSpeedHalfPoints = progress != null ? ReadRaidAttackSpeed(progress) : 0;
            cachedClassSkillDamage = LiveMatch && progress != null ? SelectedRaidClass switch
            {
                RaidClass.Ranger => ReadClassSkillDamageBonus(progress, "virtue_ranger_02_"),
                RaidClass.Warden => ReadClassSkillDamageBonus(progress, "virtue_warden_02_"),
                _ => 0
            } : 0;
        }
        private int raidCriticalChance, permanentCriticalDamage, invisibleBladeRank;
        private int openWoundsRank;
        private int OpenWoundsBonus => openWoundsRank == 0 ? 0 : 5 + openWoundsRank * 10;
        private static readonly int[] InvisibleBladeLevels = { 1, 8, 15 };
        private float evasionChance;
        private bool swindleEnabled;
        private int focusedShotRank;
        private int bulwarkRank;
        private int formationRank;
        private CharacterController formationController;
        private static readonly int[] FormationRequiredLevels = { 1, 8, 15 };
        private static readonly int[] BulwarkRequiredLevels = { 1, 8, 15 };
        private static readonly float[] BulwarkBlockChances = { 0f, 0.06f, 0.13f, 0.20f };
        private int WeaponDamageBonus => permanentWeaponDamageBonus + focusedShotRank * 15 + formationRank * 10 +
            (TiwazEvolved ? 75 : Mathf.Min(tiwazRank, 3) * 15) + (darkTidingsChosen ? 5 : 0);
        private bool darkTidingsChosen;

        private bool CanChooseDarkTidings() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            raidLevel >= 8 && PendingRewards > 0 && !darkTidingsChosen;

        internal bool TryChooseDarkTidings()
        {
            if (!CanChooseDarkTidings())
                return false;

            darkTidingsChosen = true;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static float ScaleRaidWeaponRange(Health attacker, float range) =>
            attacker && (attacker.isServer
                ? RaidStatOwners.TryGetValue(attacker, out var owner) && owner.darkTidingsChosen
                : attacker is CharacterInstance character && character.RaidDarkTidings)
                ? range * 1.35f : range;

        private const string TiwazEvolution = "passive_broksindri_A_evolve";
        private int tiwazRank;
        private float tiwazChainReady;
        private bool TiwazEvolved => tiwazRank == 4 && progress?.IsLearned(TiwazEvolution) == true;

        private bool CanChooseTiwaz() => HasLiveCaster && PendingRewards > 0 && tiwazRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[tiwazRank] && (tiwazRank < 3 || progress.IsLearned(TiwazEvolution));

        internal bool TryChooseTiwaz()
        {
            if (!CanChooseTiwaz())
                return false;

            tiwazRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }
        private int fleetFootedRank;
        private int shapeshifterRank;
        private int projectileReflectChance, retributionRank;

        private bool CanChooseRetribution() => HasLiveCaster && AllowsRaidClass(RaidClass.Warden) &&
            PendingRewards > 0 && retributionRank < 2 &&
            (retributionRank == 0 ? progress.IsLearned("passive_warden_O") : raidLevel >= 8);

        internal bool TryChooseRetribution()
        {
            if (!CanChooseRetribution())
                return false;

            retributionRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static bool TryReflectRaidProjectile(Health defender, Health source)
        {
            if (!defender || !source || !defender.isServer || defender.CurrentHealth <= 0 ||
                defender.Team == source.Team || !RaidStatOwners.TryGetValue(defender, out var owner))
                return false;

            int chance = owner.projectileReflectChance + owner.retributionRank * 15;
            if (chance == 0 || Random.value >= chance * 0.01f)
                return false;

            if (owner.retributionRank != 0)
                defender.ServerHeal(Mathf.Max(1, Mathf.RoundToInt(defender.MaxHealth * owner.retributionRank * 0.03f)), defender.netId);
            return true;
        }
        private const string RaidoEvolution = "passive_broksindri_B_evolve";
        private int raidoRank;
        private bool RaidoEvolved => raidoRank == 4 && progress?.IsLearned(RaidoEvolution) == true;

        private bool CanChooseRaido() => HasLiveCaster && PendingRewards > 0 && raidoRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[raidoRank] && (raidoRank < 3 || progress.IsLearned(RaidoEvolution));

        internal bool TryChooseRaido()
        {
            if (!CanChooseRaido())
                return false;

            raidoRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }
        private int MoveSpeedBonus => permanentMoveSpeedBonus + (fleetFootedRank == 2 ? 18 : fleetFootedRank * 10) +
            wingsOfFuryRank * 8 + (IsRaging(Time.time) ? 10 : 0) +
            SpitefulPaceBonus;

        private bool spitefulPaceChosen;
        private int spitefulPaceTargets;
        private float spitefulPaceNextScan;
        private int SpitefulPaceBonus => spitefulPaceChosen ? 5 + spitefulPaceTargets * 2 : 0;

        private bool CanChooseSpitefulPace() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && !spitefulPaceChosen;

        internal bool TryChooseSpitefulPace()
        {
            if (!CanChooseSpitefulPace())
                return false;

            spitefulPaceChosen = true;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private void TickSpitefulPace(float now)
        {
            if (!spitefulPaceChosen || now < spitefulPaceNextScan)
                return;

            spitefulPaceNextScan = now + 0.2f;
            spitefulPaceTargets = 0;
            foreach (Health target in GameManager.HealthInstances.Values)
            {
                if (target && target.isServer && target.CurrentHealth > 0 && target.Team != caster.Team &&
                    (HasRaidCurse(target, now) || HasDisease(target) || IsStoppedByRaidControl(target, now) ||
                    IsSlowedByRaidSkill(target, now) || IsSlowedByRaiseDead(target) || IsAffectedByRaidNet(target, now) ||
                    IsRootedByRaidSkill(target, now) || TryGetTerrorDirection(target, out _)))
                    spitefulPaceTargets++;
            }
            if (caster is CharacterInstance character)
                character.RaidSpitefulPaceBonus = SpitefulPaceBonus;
        }

        private bool CanChooseFleetFooted() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 &&
            fleetFootedRank < 2 && (fleetFootedRank == 0 ? progress.IsLearned("passive_rogue_O") : raidLevel >= 8);

        internal bool TryChooseFleetFooted()
        {
            if (!CanChooseFleetFooted())
                return false;

            fleetFootedRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool CanChooseFocusedShot() => AllowsRaidClass(RaidClass.Ranger) && PendingRewards > 0 &&
            focusedShotRank < 2 && (focusedShotRank == 0 ? progress.IsLearned("passive_ranger_O") : raidLevel >= 8);

        internal bool TryChooseFocusedShot()
        {
            if (!CanChooseFocusedShot())
                return false;

            focusedShotRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool CanChooseBulwark() => AllowsRaidClass(RaidClass.Warden) && PendingRewards > 0 &&
            bulwarkRank < BulwarkRequiredLevels.Length && raidLevel >= BulwarkRequiredLevels[bulwarkRank] &&
            (bulwarkRank != 0 || progress.IsLearned("passive_warden_P"));

        internal bool TryChooseBulwark()
        {
            if (!CanChooseBulwark())
                return false;

            bulwarkRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool CanChooseFormation() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 &&
            formationRank < FormationRequiredLevels.Length && raidLevel >= FormationRequiredLevels[formationRank] &&
            (formationRank != 0 || progress.IsLearned("passive_valkyrie_P"));

        internal bool TryChooseFormation()
        {
            if (!CanChooseFormation())
                return false;

            formationRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool CanChooseInvisibleBlade() => AllowsRaidClass(RaidClass.Rogue) && PendingRewards > 0 &&
            invisibleBladeRank < InvisibleBladeLevels.Length && raidLevel >= InvisibleBladeLevels[invisibleBladeRank] &&
            (invisibleBladeRank != 0 || progress.IsLearned("passive_rogue_P"));

        internal bool TryChooseInvisibleBlade()
        {
            if (!CanChooseInvisibleBlade())
                return false;

            invisibleBladeRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseOpenWounds() => AllowsRaidClass(RaidClass.Berserker) && PendingRewards > 0 &&
            openWoundsRank < 2 && (openWoundsRank == 0 ? progress.IsLearned("passive_berserker_O") : raidLevel >= 8);

        internal bool TryChooseOpenWounds()
        {
            if (!CanChooseOpenWounds())
                return false;

            openWoundsRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static void OnRaidWeaponHit(Health attacker, Health target, float now)
        {
            if (attacker.Team == target.Team || !RaidStatOwners.TryGetValue(attacker, out var owner))
                return;

            if (target.CurrentHealth > 0 && owner.openWoundsRank > 0 && Random.value < owner.OpenWoundsBonus * 0.01f)
                owner.ApplyRaidBleed(target, now, 2);
            owner.TryStartRage(now);
            owner.TryTiwazChain(target, now);
        }

        private void TryTiwazChain(Health hit, float now)
        {
            if (!TiwazEvolved || now < tiwazChainReady)
                return;

            Vector3 origin = hit.transform.position;
            Health nearest = null;
            float nearestSquared = 36f;
            foreach (Health target in GameManager.HealthInstances.Values)
            {
                if (!target || target == hit || !target.isServer || target.Team == caster.Team || target.CurrentHealth <= 0)
                    continue;

                float distanceSquared = (target.transform.position - origin).sqrMagnitude;
                if (distanceSquared > nearestSquared)
                    continue;

                nearest = target;
                nearestSquared = distanceSquared;
            }

            if (!nearest)
                return;

            tiwazChainReady = now + ScaleSkillCooldown(1.5f);
            Deal(nearest, 10);
        }

        private static int ReadPermanentMoveSpeedBonus(SkillProgress skillProgress, bool valkyrie)
        {
            int bonus = 0;
            if (skillProgress.IsLearned("virtue_loki_01_d")) bonus = 15;
            else if (skillProgress.IsLearned("virtue_loki_01_c")) bonus = 8;
            else if (skillProgress.IsLearned("virtue_loki_01_b")) bonus = 4;
            else if (skillProgress.IsLearned("virtue_loki_01_a")) bonus = 2;

            if (!valkyrie) return bonus;
            if (skillProgress.IsLearned("virtue_valkyrie_01_d")) return bonus + 7;
            if (skillProgress.IsLearned("virtue_valkyrie_01_c")) return bonus + 4;
            if (skillProgress.IsLearned("virtue_valkyrie_01_b")) return bonus + 2;
            return skillProgress.IsLearned("virtue_valkyrie_01_a") ? bonus + 1 : bonus;
        }

        private int ReadFoodHealingBonus(SkillProgress profile)
        {
            if (!LiveMatch || SelectedRaidClass != RaidClass.Warden || profile == null)
                return 0;

            if (profile.IsLearned("virtue_warden_01_d")) return 56;
            if (profile.IsLearned("virtue_warden_01_c")) return 32;
            if (profile.IsLearned("virtue_warden_01_b")) return 16;
            return profile.IsLearned("virtue_warden_01_a") ? 8 : 0;
        }

        internal static int ScaleFoodHealing(Health target, int healing)
        {
            if (healing <= 0 || !target || !RaidStatOwners.TryGetValue(target, out var owner))
                return healing;

            return ScaleHealingAmount(healing, owner.ReadFoodHealingBonus(owner.progress) + (owner.HealthRuneEvolved ? 100 : 0));
        }

        private static int ScaleHealingAmount(int healing, int percent) =>
            (int)System.Math.Min(int.MaxValue, ((long)healing * (100 + percent) + 50) / 100);

        private static int ReadFourTierHalfPoints(SkillProgress progress, string prefix)
        {
            if (progress.IsLearned(prefix + "d")) return 50;
            if (progress.IsLearned(prefix + "c")) return 28;
            if (progress.IsLearned(prefix + "b")) return 14;
            return progress.IsLearned(prefix + "a") ? 7 : 0;
        }

        private int ReadRaidAttackSpeed(SkillProgress progress)
        {
            int bonus = ReadFourTierHalfPoints(progress, "virtue_freya_01_");
            if (LiveMatch && SelectedRaidClass == RaidClass.Ranger)
                bonus += ReadFourTierHalfPoints(progress, "virtue_ranger_01_");
            else if (LiveMatch && SelectedRaidClass == RaidClass.Warden)
            {
                if (progress.IsLearned("virtue_warden_03_c")) bonus += 64;
                else if (progress.IsLearned("virtue_warden_03_b")) bonus += 32;
                else if (progress.IsLearned("virtue_warden_03_a")) bonus += 16;
            }
            return bonus;
        }

        private void RegisterRaidStats()
        {
            if ((LiveMatch || MoveSpeedBonus != 0 || attackSpeedHalfPoints != 0 || sleipnirRank != 0 || WeaponDamageBonus != 0 ||
                damageReductionPercent != 0 || allDamageHalfPoints != 0 || evasionChance > 0f ||
                bulwarkRank != 0 || wardCapacity != 0 || iceBlockRank != 0 || raidCriticalChance != 0 ||
                openWoundsRank != 0 || allInChosen || maledictionChosen || illOmenChosen || heroHymnRank != 0 || raidoRank != 0 ||
                HealthRuneEvolved || hagalazRank != 0 || inguzRank != 0 || uruzRank != 0 ||
                perthRank != 0 || lastStandRank != 0 || retributionRank != 0 || rageDuration > 0f) &&
                isActiveAndEnabled && caster && caster.isServer)
            {
                RaidStatOwners[caster] = this;
                if (caster is CharacterInstance character)
                {
                    character.RaidDarkTidings = darkTidingsChosen;
                    character.RaidSpitefulPaceBonus = SpitefulPaceBonus;
                    character.RaidTimeManipulationStacks = (byte)TimeManipulationStacks;
                }
            }
        }

        private int ReadPermanentWeaponDamage(SkillProgress progress)
        {
            if (!LiveMatch)
                return 0;

            int bravery = progress.IsLearned("virtue_odin_01_d") ? 20 :
                progress.IsLearned("virtue_odin_01_c") ? 11 :
                progress.IsLearned("virtue_odin_01_b") ? 6 :
                progress.IsLearned("virtue_odin_01_a") ? 3 : 0;
            if (SelectedRaidClass == RaidClass.Ranger)
                return bravery + (progress.IsLearned("virtue_ranger_04_a") ? 10 : 0);
            if (SelectedRaidClass != RaidClass.Berserker)
                return bravery;

            return bravery + ReadClassSkillDamageBonus(progress, "virtue_berserker_02_");
        }

        private int ReadPermanentDamageReduction(SkillProgress progress)
        {
            int reduction = progress.IsLearned("virtue_freya_03_b") ? 20 :
                progress.IsLearned("virtue_freya_03_a") ? 10 : 0;
            if (!LiveMatch || SelectedRaidClass != RaidClass.Berserker)
                return reduction;

            if (progress.IsLearned("virtue_berserker_01_d")) return reduction + 15;
            if (progress.IsLearned("virtue_berserker_01_c")) return reduction + 8;
            if (progress.IsLearned("virtue_berserker_01_b")) return reduction + 4;
            return progress.IsLearned("virtue_berserker_01_a") ? reduction + 2 : reduction;
        }

        private void ClearRaidStats()
        {
            if (!ReferenceEquals(caster, null) && RaidStatOwners.TryGetValue(caster, out var owner) && owner == this)
            {
                RaidStatOwners.Remove(caster);
                if (caster && caster is CharacterInstance character && character.isServer)
                {
                    character.RaidDarkTidings = false;
                    character.RaidSpitefulPaceBonus = 0;
                    character.RaidTimeManipulationStacks = 0;
                }
            }
        }

        internal static float RaidMoveMultiplier(Health target) =>
            target && RaidStatOwners.TryGetValue(target, out var owner)
                ? 1f + owner.MoveSpeedBonus * 0.01f
                : target is CharacterInstance character && character && !character.isServer
                    ? 1f + character.RaidSpitefulPaceBonus * 0.01f : 1f;

        private bool CanChooseShapeshifter() => HasLiveCaster && PendingRewards > 0 && shapeshifterRank < 2 &&
            (shapeshifterRank == 0 ? progress.IsLearned("passive_loki_H") : raidLevel >= 8);

        internal bool TryChooseShapeshifter()
        {
            if (!CanChooseShapeshifter())
                return false;

            shapeshifterRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static float RaidBaseAttackSpeedMultiplier(Health target)
        {
            if (target is CharacterInstance remote && remote && !remote.isServer && remote.isOwned &&
                remote.TryGetComponent<RemoteRaidStats>(out var stats) && stats.HasState)
                return stats.AttackSpeed;
            return target && RaidStatOwners.TryGetValue(target, out var owner)
                ? 1f + owner.attackSpeedHalfPoints / 200f + owner.sleipnirRank * 0.1f +
                    owner.MoveSpeedBonus * owner.shapeshifterRank * 0.0025f +
                    (owner.RaidoEvolved ? 0.4f : Mathf.Min(owner.raidoRank, 3) * 0.08f) +
                    owner.TimeManipulationStacks * TimeManipulationPerCharge
                : target is CharacterInstance character && character && !character.isServer
                    ? 1f + character.RaidTimeManipulationStacks * TimeManipulationPerCharge : 1f;
        }

        internal static float RaidAttackSpeedMultiplier(Health target) =>
            RaidBaseAttackSpeedMultiplier(target) * RaidSkillAttackSpeedFactor(target, Time.time);

        internal static int ScaleRaidWeaponDamage(Health attacker, int damage)
        {
            if (damage <= 0 || !attacker || !RaidStatOwners.TryGetValue(attacker, out var owner))
                return damage;

            int bonus = owner.WeaponDamageBonus;
            if (owner.formationRank != 0 && attacker is CharacterInstance character &&
                owner.formationController && owner.formationController.enabled &&
                IsFormationStationary(character.IsGrounded, character.movementInput, owner.formationController.velocity))
                bonus += owner.formationRank * 10;

            return (int)System.Math.Min(int.MaxValue, ((long)damage * (100 + bonus) + 50) / 100);
        }

        internal static bool IsFormationStationary(bool grounded, Vector2 input, Vector3 velocity) =>
            grounded && input.sqrMagnitude <= 0.01f && velocity.x * velocity.x + velocity.z * velocity.z <= 0.01f;

        internal static bool TryAvoidRaidDamage(Health victim, Health attacker)
        {
            if (attacker.Team == victim.Team || !RaidStatOwners.TryGetValue(victim, out var owner))
                return false;

            if (owner.evasionChance > 0f && UnityEngine.Random.value < owner.evasionChance)
            {
                if (owner.swindleEnabled)
                    owner.StopSkillTarget(attacker, Time.time + 5f);
                owner.TryCounterattackReset(Time.time);
                return true;
            }

            return owner.bulwarkRank != 0 && UnityEngine.Random.value < BulwarkBlockChances[owner.bulwarkRank];
        }

        private bool counterattackEnabled;

        private void TryCounterattackReset(float now)
        {
            if (!counterattackEnabled || Random.value >= 0.2f)
                return;

            int candidates = 0;
            ref float selected = ref daggerReady;
            for (int index = 0; index < 40; index++)
            {
                ref float ready = ref CounterattackCooldown(index);
                if (ready > now && Random.Range(0, ++candidates) == 0)
                    selected = ref ready;
            }
            if (candidates != 0)
                selected = 0f;
        }

        private ref float CounterattackCooldown(int index)
        {
            switch (index)
            {
                case 0: return ref daggerReady;
                case 1: return ref potReady;
                case 2: return ref flamingRayReady;
                case 3: return ref sisterhoodReady;
                case 4: return ref flamingDartsReady;
                case 5: return ref nextStorm;
                case 6: return ref thunderReady;
                case 7: return ref lightningReady;
                case 8: return ref hammerReady;
                case 9: return ref orbReady;
                case 10: return ref auraReady;
                case 11: return ref swordReady;
                case 12: return ref blessingReady;
                case 13: return ref discReady;
                case 14: return ref valkyrieReady;
                case 15: return ref plagueReady;
                case 16: return ref barrierReady;
                case 17: return ref serpentReady;
                case 18: return ref firestarReady;
                case 19: return ref viperReady;
                case 20: return ref cometReady;
                case 21: return ref ringReady;
                case 22: return ref frostArrowReady;
                case 23: return ref snowReady;
                case 24: return ref iceReady;
                case 25: return ref gungnirReady;
                case 26: return ref ravenReady;
                case 27: return ref ragnarokReady;
                case 28: return ref boulderReady;
                case 29: return ref warriorReady;
                case 30: return ref vortexReady;
                case 31: return ref shipReady;
                case 32: return ref krakenReady;
                case 33: return ref leviathanReady;
                case 34: return ref bubbleReady;
                case 35: return ref wingReady;
                case 36: return ref breathReady;
                case 37: return ref diseaseRayReady;
                case 38: return ref raiseReady;
                case 39: return ref terrorReady;
                default: throw new System.ArgumentOutOfRangeException(nameof(index));
            }
        }

        private const string UruzEvolution = "passive_broksindri_G_evolve";
        private int uruzRank;
        private bool UruzEvolved => uruzRank == 4 && progress?.IsLearned(UruzEvolution) == true;

        private bool CanChooseUruz() => HasLiveCaster && PendingRewards > 0 && uruzRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[uruzRank] && (uruzRank < 3 || progress.IsLearned(UruzEvolution));

        internal bool TryChooseUruz()
        {
            if (!CanChooseUruz())
                return false;

            uruzRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static int ScaleRaidAllDamage(Health attacker, int damage)
        {
            if (damage <= 0 || !RaidStatOwners.TryGetValue(attacker, out var owner))
                return damage;

            int uruzBonus = owner.UruzEvolved ? 200 : Mathf.Min(owner.uruzRank, 3) * 40;
            int bonus = owner.allDamageHalfPoints + uruzBonus + (owner.IsRaging(Time.time) ? 40 : 0);
            long multiplier = 10000L + bonus * 50L + owner.allInKills * 13L;
            if (multiplier > int.MaxValue * 10000L / damage)
                return int.MaxValue;

            return (int)(((long)damage * multiplier + 5000) / 10000);
        }

        private void ReadRaidCriticalStats(SkillProgress progress)
        {
            raidCriticalChance = 0;
            permanentCriticalDamage = 0;
            if (progress == null)
                return;

            if (progress.IsLearned("virtue_njord_01_a")) raidCriticalChance += 2;
            if (progress.IsLearned("virtue_njord_01_b")) raidCriticalChance += 2;
            if (progress.IsLearned("virtue_njord_01_c")) raidCriticalChance += 2;
            if (progress.IsLearned("virtue_njord_01_d")) raidCriticalChance += 2;
            if (progress.IsLearned("virtue_njord_01_e")) raidCriticalChance += 2;

            if (LiveMatch && SelectedRaidClass == RaidClass.Rogue)
            {
                if (progress.IsLearned("virtue_rogue_01_d")) raidCriticalChance += 15;
                else if (progress.IsLearned("virtue_rogue_01_c")) raidCriticalChance += 8;
                else if (progress.IsLearned("virtue_rogue_01_b")) raidCriticalChance += 4;
                else if (progress.IsLearned("virtue_rogue_01_a")) raidCriticalChance += 2;
            }

            if (progress.IsLearned("virtue_nidhogg_01_d")) permanentCriticalDamage = 35;
            else if (progress.IsLearned("virtue_nidhogg_01_c")) permanentCriticalDamage = 20;
            else if (progress.IsLearned("virtue_nidhogg_01_b")) permanentCriticalDamage = 10;
            else if (progress.IsLearned("virtue_nidhogg_01_a")) permanentCriticalDamage = 5;
        }

        private const string InguzEvolution = "passive_broksindri_D_evolve";
        private int inguzRank;
        private float inguzHealingReady;
        private bool InguzEvolved => inguzRank == 4 && progress?.IsLearned(InguzEvolution) == true;

        private bool CanChooseInguz() => HasLiveCaster && PendingRewards > 0 && inguzRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[inguzRank] && (inguzRank < 3 || progress.IsLearned(InguzEvolution));

        internal bool TryChooseInguz()
        {
            if (!CanChooseInguz())
                return false;

            inguzRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool maledictionChosen;
        private bool illOmenChosen;

        private bool CanChooseIllOmen() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && !illOmenChosen;

        internal bool TryChooseIllOmen()
        {
            if (!CanChooseIllOmen())
                return false;

            illOmenChosen = true;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static int ModifyIllOmenDamage(Health attacker, Health victim, int damage, float now)
        {
            if (attacker.Team == victim.Team || !RaidStatOwners.TryGetValue(attacker, out var owner) ||
                !owner.illOmenChosen)
                return damage;

            return HasRaidCurse(victim, now) || IsFrozenByRaidSkill(victim, now) || HasDisease(victim) ||
                IsSlowedByRaidSkill(victim, now) || IsSlowedByRaiseDead(victim) || IsAffectedByRaidNet(victim, now) ||
                IsRootedByRaidSkill(victim, now)
                ? (int)System.Math.Min(int.MaxValue, (long)damage + 10) : damage;
        }

        private bool CanChooseMalediction() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && !maledictionChosen;

        internal bool TryChooseMalediction()
        {
            if (!CanChooseMalediction())
                return false;

            maledictionChosen = true;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        internal static void OnRaidCriticalHit(Health attacker, Health victim, float now)
        {
            if (!RaidStatOwners.TryGetValue(attacker, out var owner))
                return;

            if (owner.maledictionChosen)
                owner.TryApplyRaidCurse(victim, now, 1f);

            if (!owner.InguzEvolved || now < owner.inguzHealingReady || owner.allInChosen ||
                attacker.CurrentHealth <= 0 || attacker.CurrentHealth >= attacker.MaxHealth ||
                Random.value >= 0.3f)
                return;

            owner.inguzHealingReady = now + 2f;
            attacker.ServerHeal(12, attacker.netId);
        }

        private const string PerthEvolution = "passive_broksindri_C_evolve";
        private int perthRank;
        private bool massacreEnabled;
        private bool PerthEvolved => perthRank == 4 && progress?.IsLearned(PerthEvolution) == true;

        private bool CanChoosePerth() => HasLiveCaster && PendingRewards > 0 && perthRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[perthRank] && (perthRank < 3 || progress.IsLearned(PerthEvolution));

        internal bool TryChoosePerth()
        {
            if (!CanChoosePerth())
                return false;

            perthRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private bool RollSlay(Health target, float chance)
        {
            if (target is not CharacterInstance character || !character.BOT)
                return false;
            if (target.TryGetComponent<RangeResearchEnemy>(out var rangeEnemy) &&
                rangeEnemy.Archetype is 2 or RangeResearchEnemy.QueenKind)
                return false;
            if (target.TryGetComponent<Skeleton1HCombat>(out var jotunnEnemy) && jotunnEnemy.ResistsSlay)
                return false;

            return Random.value < chance + (massacreEnabled ? 0.15f : 0f);
        }

        internal static bool RollPerthSlay(Health attacker, Health target) =>
            RaidStatOwners.TryGetValue(attacker, out var owner) && owner.PerthEvolved && owner.RollSlay(target, 0.1f);

        internal static int ScaleRaidCriticalDamage(Health attacker, int damage, bool raidSkill, out bool critical)
        {
            critical = false;
            if (!RaidStatOwners.TryGetValue(attacker, out var owner))
                return damage;

            int chance = owner.raidCriticalChance + (owner.PerthEvolved ? 25 : Mathf.Min(owner.perthRank, 3) * 5);
            if (owner.maledictionChosen)
                chance += 10;
            if (raidSkill)
                chance += owner.lastStandCriticalChance;
            if (chance == 0 || Random.value >= chance * 0.01f)
                return damage;

            critical = true;
            int invisibleBladeBonus = owner.invisibleBladeRank == 0 ? 0 : 10 + owner.invisibleBladeRank * 10;
            int inguzBonus = owner.InguzEvolved ? 60 : Mathf.Min(owner.inguzRank, 3) * 12;
            return (int)System.Math.Min(int.MaxValue,
                ((long)damage * (200 + owner.permanentCriticalDamage + invisibleBladeBonus +
                    owner.OpenWoundsBonus + inguzBonus) + 50) / 100);
        }
    }
}
