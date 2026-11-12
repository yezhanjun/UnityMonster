using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview : MonoBehaviour
    {
        public const string MissileUnlock = "active_seer_E";
        public const string MissileEvolution = "active_seer_E_evolve";
        public const string StormUnlock = "active_thor_D";
        public const string StormEvolution = "active_thor_D_evolve";

        public const string OrbEvolution = "active_thor_E_evolve";
        public const string HammerEvolution = "active_thor_B_evolve";
        public const string LightningEvolution = "active_thor_A_evolve";
        public const string ThunderEvolution = "active_thor_C_evolve";

        // The TPS prototype needs an acquisition range for missile targeting.
        private const float MissileRange = 20f;
        private const int PreviewExperiencePerKill = 35;
        private const float MinionGoldChance = 0.01f;
        private const float GoldPickupRadius = 4f;
        private int wisdomPickupPercent;
        private int ancientKnowledgeRank;
        private readonly List<Vector3> goldDrops = new List<Vector3>();
        private readonly List<Vector3> experienceCrystals = new List<Vector3>();
        private readonly List<Health> crystalContacts = new List<Health>();
        private bool collectingExperienceCrystals;
        private bool soulReaperChosen;
        private float soulReaperReady;
        private readonly System.Random goldRandom = new System.Random();

        [System.Serializable]
        private sealed class ExperienceCurve
        {
            public int maxLevel;
            public long[] thresholds;
        }

        private ExperienceCurve curve;
        private long experience;
        private int experienceRemainder, odinExperiencePermille;
        private const string DivinationUnlock = "passive_seer_O";
        private int divinationRank;
        private int goldDropBonus;
        private bool essenceExpansionChosen;
        private int primevalCurrentsRank;
        private int PrimevalCurrentsBonus => primevalCurrentsRank * 20;
        private const string MannazEvolution = "passive_broksindri_E_evolve";
        private int mannazRank;
        private const string AnsuzEvolution = "passive_broksindri_F_evolve";
        private int ansuzRank;
        private bool AnsuzEvolved => ansuzRank == 4 && progress?.IsLearned(AnsuzEvolution) == true;
        private bool RollAnsuzRepeat(ref float cooldownUntil)
        {
            if (!AnsuzEvolved || Random.value >= 0.13f)
                return false;

            if (MannazEvolved && Random.value < 0.2f)
                cooldownUntil = 0f;

            return true;
        }
        private int GodSkillDamageBonus => godDamageBonus + (AnsuzEvolved ? 75 : Mathf.Min(ansuzRank, 3) * 15) +
            PrimevalCurrentsBonus;

        private bool CanChooseAnsuz() => HasLiveCaster && PendingRewards > 0 && ansuzRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[ansuzRank] && (ansuzRank < 3 || progress.IsLearned(AnsuzEvolution));

        internal bool TryChooseAnsuz()
        {
            if (!CanChooseAnsuz())
                return false;

            ansuzRank++;
            rewardsSpent++;
            return true;
        }
        private bool MannazEvolved => mannazRank == 4 && progress?.IsLearned(MannazEvolution) == true;

        private bool CanChooseMannaz() => HasLiveCaster && PendingRewards > 0 && mannazRank < RuneRewardLevels.Length &&
            raidLevel >= RuneRewardLevels[mannazRank] && (mannazRank < 3 || progress.IsLearned(MannazEvolution));

        internal bool TryChooseMannaz()
        {
            if (!CanChooseMannaz())
                return false;

            mannazRank++;
            rewardsSpent++;
            return true;
        }
        private float skillCooldownMultiplier = 1f;
        internal int RaidLevel => raidLevel;
        private readonly List<Health> targets = new List<Health>();
        private Health caster;
        private SkillTreePanel tree;
        private float strikeInterval;
        private Vector3 sentinelPosition;
        private SkillProgress progress;
        private int raidLevel = 1;
        private int missileRank, stormRank, rewardsSpent;
        private float nextMissile, nextStorm, stormEnd, nextStrike;
        private readonly List<MagicMissile> magicMissiles = new List<MagicMissile>(24);
        private const float MagicMissileRadius = 0.45f;
        private const float MagicMissileSpeed = 13f;
        private const float MagicMissileLifetime = 2f;
        private const float MagicMissileHomingDelay = 0.1f;
        private uint nextProjectileVisualId;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal System.Action<string> ValidationProjectileTrace { get; set; }
        internal System.Action<uint, uint, int> ValidationRangerHit { get; set; }
        internal System.Action<int, int> ValidationBleedTick { get; set; }
#endif

        private struct MagicMissile
        {
            public Health target;
            public Vector3 position, direction;
            public float lastTick, expires;
            public float nextVisualSample;
            public float launchElevation, launchYaw;
            public uint visualId;
            public int damage;
            public bool shatters, refreshCasterOrigin;
        }
        private bool automatic;
        internal bool ShowControls { get; set; } = true;
        internal bool SuppressPresentation { get; set; }
        internal bool LiveMatch { get; set; }
        internal bool UseExperienceCrystals { get; set; }
        internal RaidClass SelectedRaidClass { get; set; }
        internal bool HasLiveCaster => caster && caster.isServer && caster.CurrentHealth > 0;
        private Vector2 rewardScroll;
        private int thunderRank, thunderStrikes, lightningRank;
        private float lightningReady, hammerReady;
        private int hammerRank, orbRank;
        private float orbReady;
        private readonly List<OrbFlight> orbs = new List<OrbFlight>(3);

        private sealed class OrbFlight
        {
            public Vector3 origin, direction, previous;
            public float started, expires, nextPulse;
            public int damage;
            public bool evolved;
            public readonly HashSet<Health> hit = new HashSet<Health>();
        }
        private readonly List<HammerFlight> hammers = new List<HammerFlight>(2);

        private struct HammerFlight
        {
            public Vector3 origin, turnaround;
            public float turnAt, returnAt;
            public bool evolved, turned;
        }
        private float thunderReady, nextThunderStrike;

        public void Bind(Health character, SkillProgress playerProgress, SkillTreePanel skillTree)
        {
            tree = skillTree;
            progress = playerProgress;
            if (ReferenceEquals(caster, character))
                return;

            ClearRuneRevive();
            ClearRaidStats();
            SetMaximumHealthBonus(0);
            ClearCasterEffects();
            regenNext = 0;
            regenRemainder = 0;
            if (caster)
                caster.Server_KilledCharacter -= OnEnemyKilled;

            if (!LiveMatch || curve == null)
            {
                ResetRaid();
                ResetRaidDash(playerProgress);
                ResetBlazingSoul(playerProgress);
                rageDuration = LiveMatch && SelectedRaidClass == RaidClass.Berserker
                    ? (playerProgress != null && playerProgress.IsLearned("virtue_berserker_04_a") ? 8f : 4f) : 0f;
                ResetEldritchWard(playerProgress);
                ReadRaidCriticalStats(playerProgress);
                evasionChance = playerProgress == null ? 0f : playerProgress.IsLearned("virtue_loki_03_b") ? 0.20f :
                    playerProgress.IsLearned("virtue_loki_03_a") ? 0.10f : 0f;
                bool isLiveRogue = LiveMatch && SelectedRaidClass == RaidClass.Rogue;
                if (isLiveRogue)
                    evasionChance += 0.20f;
                if (LiveMatch && SelectedRaidClass == RaidClass.Valkyrie && playerProgress != null)
                    evasionChance += playerProgress.IsLearned("virtue_valkyrie_02_c") ? 0.15f :
                        playerProgress.IsLearned("virtue_valkyrie_02_b") ? 0.10f :
                        playerProgress.IsLearned("virtue_valkyrie_02_a") ? 0.05f : 0f;
                counterattackEnabled = isLiveRogue && playerProgress != null && playerProgress.IsLearned("virtue_rogue_03_a");
                curseStunPercent = LiveMatch && SelectedRaidClass == RaidClass.Valkyrie && playerProgress != null
                    ? playerProgress.IsLearned("virtue_valkyrie_03_c") ? 17 :
                        playerProgress.IsLearned("virtue_valkyrie_03_b") ? 10 :
                        playerProgress.IsLearned("virtue_valkyrie_03_a") ? 5 : 0
                    : 0;
                swindleEnabled = playerProgress != null && playerProgress.IsLearned("virtue_loki_04_a");
                projectileReflectChance = LiveMatch && playerProgress != null
                    ? playerProgress.IsLearned("virtue_skadi_03_b") ? 25 :
                        playerProgress.IsLearned("virtue_skadi_03_a") ? 12 : 0
                    : 0;
                massacreEnabled = LiveMatch && playerProgress != null && playerProgress.IsLearned("virtue_thor_04_a");
                freyaReviveAvailable = playerProgress != null && playerProgress.IsLearned("virtue_freya_04_a");
                divineRebirthEnabled = LiveMatch && SelectedRaidClass == RaidClass.Valkyrie &&
                    playerProgress != null && playerProgress.IsLearned("virtue_valkyrie_04_a");
                thorHealthBonus = playerProgress != null ? ReadThorHealthBonus(playerProgress) : 0;
                thorRegenHalfPoints = playerProgress != null ? ReadThorRegenHalfPoints(playerProgress) : 0;
                odinExperiencePermille = playerProgress != null ? ReadOdinExperiencePermille(playerProgress) : 0;
                goldDropBonus = playerProgress != null ? ReadGoldDropBonus(playerProgress, "virtue_loki_02_") : 0;
                wisdomPickupPercent = LiveMatch && playerProgress != null
                    ? ReadFourTierHalfPoints(playerProgress, "virtue_freya_02_") : 0;
                if (LiveMatch && SelectedRaidClass == RaidClass.Seeress && playerProgress != null)
                    goldDropBonus += ReadGoldDropBonus(playerProgress, "virtue_seer_01_");
                skillCooldownMultiplier = playerProgress != null
                    ? ReadSkillCooldownMultiplier(playerProgress) : 1f;
                ResetRewardChoices(playerProgress);
                permanentMoveSpeedBonus = playerProgress != null
                    ? ReadPermanentMoveSpeedBonus(playerProgress, LiveMatch && SelectedRaidClass == RaidClass.Valkyrie) : 0;
            }

            caster = character;
            formationController = null;
            if (!caster)
                return;

            if (AllowsRaidClass(RaidClass.Valkyrie))
                caster.TryGetComponent(out formationController);
            curve ??= JsonUtility.FromJson<ExperienceCurve>(Resources.Load<TextAsset>("Jotunnslayer/RaidExperience").text);
            RefreshHealthRune(fillAddedHealth: true);
            RegisterRuneRevive();
            RegisterRaidStats();
            caster.Server_KilledCharacter += OnEnemyKilled;
        }

        private void ResetRaid()
        {
            experience = 0;
            experienceRemainder = 0;
            divinationRank = 0;
            ancientKnowledgeRank = 0;
            maledictionChosen = false;
            illOmenChosen = false;
            darkTidingsChosen = false;
            spitefulPaceChosen = false;
            essenceExpansionChosen = false;
            primevalCurrentsRank = 0;
            winterRavenChosen = false;
            spearheadRank = 0;
            fleetFootedRank = 0;
            focusedShotRank = 0;
            bulwarkRank = 0;
            formationRank = 0;
            invisibleBladeRank = 0;
            openWoundsRank = 0;
            dischargeRank = 0;
            raidLevel = 1;
            missileRank = stormRank = thunderRank = rewardsSpent = 0;
            thunderReady = nextThunderStrike = lightningReady = 0;
            lightningRank = hammerRank = 0;
            hammerReady = orbReady = 0;
            orbRank = mindRank = 0;
            mindReady = 0;
            briarRank = trapRank = wispRank = 0;
            wispReady = cuttingReady = 0;
            cuttingRank = assaultRank = 0;
            assaultReady = swirlingReady = 0;
            swirlingRank = throwingRank = 0;
            throwingReady = totemReady = 0;
            totemRank = heartRank = 0;
            heartReady = rainReady = 0;
            rainRank = skullRank = 0;
            skullReady = focusReady = 0;
            focusRank = netRank = 0;
            netReady = auraReady = 0;
            auraRank = swordRank = 0;
            swordReady = blessingReady = 0;
            blessingRank = discRank = 0;
            heroHymnRank = lastStandRank = 0;
            discReady = valkyrieReady = 0;
            valkyrieRank = plagueRank = 0;
            plagueReady = barrierReady = 0;
            barrierRank = serpentRank = 0;
            serpentReady = firestarReady = 0;
            firestarRank = viperRank = 0;
            viperReady = cometReady = 0;
            cometRank = ringRank = 0;
            ringReady = frostArrowReady = 0;
            frostArrowRank = snowRank = 0;
            iceBlockRank = 0;
            snowFightRank = 0;
            snowFightReady = 0f;
            snowReady = iceReady = 0;
            iceRank = gungnirRank = 0;
            gungnirReady = ravenReady = 0;
            ravenRank = ragnarokRank = boulderRank = warriorRank = 0;
            ragnarokReady = boulderReady = warriorReady = vortexReady = 0;
            vortexRank = shipRank = 0;
            shipReady = krakenReady = 0;
            krakenRank = leviathanRank = 0;
            leviathanReady = bubbleReady = 0;
            bubbleRank = geboRank = bonusRewards = regenRank = healthRuneRank = 0;
            olderThanTimeRank = sleipnirRank = 0;
            mightyOarsRank = torrentsRank = 0;
            allInChosen = false;
            allInKills = wingsOfFuryRank = 0;
            thorPotentialRank = thunderDischargeRank = 0;
            shapeshifterRank = rippedLotteryRank = 0;
            raidoRank = 0;
            hagalazRank = 0;
            inguzRank = 0;
            uruzRank = 0;
            perthRank = 0;
            retributionRank = 0;
            laguzRank = 0;
            kenazRank = 0;
            jeraRank = 0;
            tiwazRank = 0;
            mannazRank = ansuzRank = 0;
            wingReady = 0;
            wingRank = breathRank = 0;
            breathReady = diseaseRayReady = 0;
            diseaseRayRank = raiseRank = 0;
            raiseReady = 0;
            terrorRank = 0;
            terrorReady = 0;
            daggerRank = potRank = 0;
            flamingRayRank = 0;
            flamingRayReady = 0;
            sisterhoodRank = 0;
            sisterhoodReady = 0;
            flamingDartsRank = 0;
            flamingDartsReady = 0;
            shadowImagesRank = 0;
            shadowImagesReady = 0;
            javelinRank = 0;
            javelinReady = 0;
            spearBarrierRank = 0;
            spearBarrierReady = 0;
            fallenValkyriesRank = 0;
            fallenValkyriesReady = 0;
            valkyrieChargeRank = 0;
            valkyrieChargeReady = 0;
            seismicRank = 0;
            seismicReady = 0;
            anvilRank = 0;
            anvilReady = 0;
            shieldwallRank = 0;
            shieldwallReady = 0;
            ironFistRank = 0;
            ironFistReady = 0;
            hammertimeRank = 0;
            hammertimeReady = 0;
            daggerReady = potReady = trapReady = briarReady = 0;
            nextMissile = nextStorm = 0;
            runeReviveSpent = false;
            goldDrops.Clear();
            experienceCrystals.Clear();
            soulReaperChosen = false;
            soulReaperReady = 0f;
        }

        private void OnEnable() => RegisterRaidStats();

        private void OnDisable()
        {
            ClearRaidStats();
            SetMaximumHealthBonus(0);
            ClearCasterEffects();
        }

        private void ClearCasterEffects()
        {
            ClearGmIndicators();
            ClearGmLightning();
            ClearGodCastAudio();
            ClearMagicMissiles();
            spitefulPaceTargets = 0;
            spitefulPaceNextScan = 0f;
            if (caster is CharacterInstance character && character && character.isServer &&
                RaidStatOwners.TryGetValue(caster, out var owner) && owner == this)
                character.RaidSpitefulPaceBonus = 0;
            ClearRaidDash();
            movementSampled = false;
            oarsDistance = jeraDistance = 0f;
            hagalazHealingRemainder = 0;
            inguzHealingReady = 0f;
            tiwazChainReady = 0f;
            lastStandSampled = false;
            lastStandCriticalChance = 0;
            ClearRage();
            wardProtectedUntil = 0f;
            iceBlockUntil = 0f;
            divineRebirthImpacts.Clear();
            potStage = 0;
            flamingRayCount = 0;
            ironFistStrikes = 0;
            ClearHammertime();
            ClearShieldwall();
            anvilCount = 0;
            ClearSeismic();
            ClearValkyrieCharge();
            ClearSpearBarrier();
            ClearJavelins();
            ClearShadowImages();
            sisterDecoys.Clear();
            previewPoisons.Clear();
            stormEnd = 0;
            thunderStrikes = 0;
            hammers.Clear();
            orbs.Clear();
            briars.Clear();
            evolvedBriars.Clear();
            ClearTimeTraps();
            ClearSkillStops();
            ClearIceField();
            ClearNets();
            ClearCalamity();
            ClearDiseases();
            ClearRaidCurses();
            ClearSoulmate();
            ClearRaiseDead();
            ClearTerror();
            valkyrieZones.Clear();
            infections.Clear();
            firestars.Clear();
            vipers.Clear();
            ClearBoulders();
            ClearWarrior();
            vortexActive = false;
            shipActive = false;
            shipHits.Clear();
            krakenStage = 0;
            ClearLeviathan();
            ClearTidalBarrier();
            pendingCometCount = 0;
            ringWaves = snowShards = snowSeconds = 0;
            firestarBurns.Clear();
            barrierTicks = 0;
            wisps = null;
            swirling = null;
            totems.Clear();
            ClearRangerArrows();
            rainWaves = focusShots = swordSwings = blessingTicks = discStage = ravenTicks = 0;
            winterRavenReady = null;
            winterRavenImpactAt = 0f;
            nextEvolvedFocusShot = 0f;
            cuttingStep = 0;
        }

        private void OnDestroy()
        {
            DisposeShadowPresentations();
            ClearSoulmate();
            ClearMagicMissiles();
            ClearRangerArrows();
            if (spearBarrierRing)
                Destroy(spearBarrierRing.gameObject);
            if (spearBarrierMaterial)
                Destroy(spearBarrierMaterial);
            ClearRuneRevive();
            if (caster)
                caster.Server_KilledCharacter -= OnEnemyKilled;
        }

        private void OnEnemyKilled(Health victim)
        {
            if (!isActiveAndEnabled || !caster || !caster.isServer || !victim || victim == caster || victim.Team == caster.Team)
                return;

            if (victim.TryGetComponent(out Skeleton1HCombat summon) && summon.SuppressSummonRewards) return;

            if (allInChosen && allInKills < int.MaxValue)
                allInKills++;

            Vector3 dropPoint = victim.transform.position;
            if (UseExperienceCrystals)
                experienceCrystals.Add(dropPoint);
            else
                GrantKillExperience(Time.time);
            RollMinionGold(dropPoint, (float)goldRandom.NextDouble());
        }

        private void GrantKillExperience(float now)
        {
            int scaledExperience = PreviewExperiencePerKill *
                (1000 + GeboExperienceBonus * 10 + odinExperiencePermille + divinationRank * 150)
                + experienceRemainder;
            experience += scaledExperience / 1000;
            experienceRemainder = scaledExperience % 1000;
            int cap = Mathf.Min(curve.maxLevel, curve.thresholds.Length);
            float fortuneChance = UseExperienceCrystals && LiveMatch && SelectedRaidClass == RaidClass.Seeress ? 0.3f : 0f;
            float doubleRewardChance = permanentDoubleRewardChance + (GeboEvolved ? 0.2f : 0f) + fortuneChance;
            while (raidLevel < cap && experience >= curve.thresholds[raidLevel])
            {
                raidLevel++;
                TriggerIceBlock(now);
                if (doubleRewardChance > 0f && Random.value < doubleRewardChance)
                    bonusRewards++;
            }
        }

        internal void RollMinionGold(Vector3 position, float roll)
        {
            float chance = Mathf.Min(1f, MinionGoldChance * (100 + goldDropBonus) / 100f);
            if (roll < chance)
                goldDrops.Add(position);
        }

        private float ScaleSkillCooldown(float seconds) =>
            seconds * (caster is MTPSKIT.Gameplay.CharacterInstance hero ? hero.HeroCooldownMultiplier : 1f) * skillCooldownMultiplier * TorrentsCooldownFactors[torrentsRank] * LaguzCooldownFactor *
            (1f - TimeManipulationStacks * TimeManipulationPerCharge);

        private static float ReadSkillCooldownMultiplier(SkillProgress progress)
        {
            int reduction = 0;
            if (progress.IsLearned("virtue_skadi_01_c")) reduction = 15;
            else if (progress.IsLearned("virtue_skadi_01_b")) reduction = 10;
            else if (progress.IsLearned("virtue_skadi_01_a")) reduction = 5;
            if (progress.IsLearned("virtue_skadi_01_d")) reduction += 5;
            if (progress.IsLearned("virtue_skadi_01_e")) reduction += 10;

            return (200 - reduction) / 200f;
        }

        private static int ReadGoldDropBonus(SkillProgress skillProgress, string nodePrefix)
        {
            if (skillProgress.IsLearned(nodePrefix + "d")) return 50;
            if (skillProgress.IsLearned(nodePrefix + "c")) return 28;
            if (skillProgress.IsLearned(nodePrefix + "b")) return 14;
            return skillProgress.IsLearned(nodePrefix + "a") ? 7 : 0;
        }

        private static int ReadOdinExperiencePermille(SkillProgress skillProgress)
        {
            if (skillProgress.IsLearned("virtue_odin_02_d")) return 250;
            if (skillProgress.IsLearned("virtue_odin_02_c")) return 140;
            if (skillProgress.IsLearned("virtue_odin_02_b")) return 70;
            return skillProgress.IsLearned("virtue_odin_02_a") ? 35 : 0;
        }

        private void CollectNearbyGold(bool collectAll = false)
        {
            if (goldDrops.Count == 0)
                return;

            Vector3 position = caster.transform.position;
            float radius = RaidPickupRadius();
            for (int i = goldDrops.Count - 1; i >= 0; i--)
            {
                if ((!collectAll && (position - goldDrops[i]).sqrMagnitude > radius * radius) ||
                    !progress.TryCollectGold())
                    continue;

                goldDrops.RemoveAt(i);
            }
        }

        private float RaidPickupRadius()
        {
            int jeraBonus = JeraEvolved ? 100 : Mathf.Min(jeraRank, 3) * 20;
            return GoldPickupRadius * (1f + (wisdomPickupPercent + jeraBonus + ancientKnowledgeRank * 30) / 100f);
        }

        private void CollectExperienceCrystals(float now, float? pickupRadius = null)
        {
            if (experienceCrystals.Count == 0 || collectingExperienceCrystals)
                return;

            collectingExperienceCrystals = true;
            try
            {
                Vector3 position = caster.transform.position;
                float radius = pickupRadius ?? RaidPickupRadius();
                for (int i = experienceCrystals.Count - 1; i >= 0 && HasLiveCaster; i--)
                {
                    Vector3 crystal = experienceCrystals[i];
                    if ((position - crystal).sqrMagnitude > radius * radius)
                        continue;

                    experienceCrystals.RemoveAt(i);
                    GrantKillExperience(now);
                    TryTriggerSoulReaper(now);
                    if (LiveMatch && SelectedRaidClass == RaidClass.Seeress)
                        ApplyMaliciousWisdom(crystal, position);
                }
            }
            finally
            {
                crystalContacts.Clear();
                collectingExperienceCrystals = false;
            }
        }

        private void ApplyMaliciousWisdom(Vector3 start, Vector3 end)
        {
            const float contactRadius = 0.6f;
            Vector3 path = end - start;
            float lengthSquared = path.sqrMagnitude;
            crystalContacts.Clear();
            foreach (Health target in GameManager.HealthInstances.Values)
            {
                if (!target || !target.isServer || target.CurrentHealth <= 0 || target.Team == caster.Team)
                    continue;

                Vector3 offset = target.transform.position - start;
                float fraction = lengthSquared > 0f ? Mathf.Clamp01(Vector3.Dot(offset, path) / lengthSquared) : 0f;
                if ((offset - path * fraction).sqrMagnitude <= contactRadius * contactRadius)
                    crystalContacts.Add(target);
            }
            foreach (Health target in crystalContacts)
            {
                if (!HasLiveCaster)
                    break;
                if (target && target.CurrentHealth > 0 && Random.value < 0.25f)
                    Deal(target, ScaleClassSkillDamage(5));
            }
        }

        private bool CanChooseSoulReaper() => HasLiveCaster && UseExperienceCrystals &&
            AllowsRaidClass(RaidClass.Seeress) && PendingRewards > 0 && !soulReaperChosen;

        internal bool TryChooseSoulReaper()
        {
            if (!CanChooseSoulReaper())
                return false;

            soulReaperChosen = true;
            rewardsSpent++;
            return true;
        }

        private void TryTriggerSoulReaper(float now)
        {
            if (!soulReaperChosen || now < soulReaperReady || Random.value >= 0.05f)
                return;

            System.Span<int> cooling = stackalloc int[40];
            int count = 0;
            for (int i = 0; i < cooling.Length; i++)
            {
                if (SoulReaperCooldown(i) > now)
                    cooling[count++] = i;
            }
            if (count == 0)
                return;

            SoulReaperCooldown(cooling[count == 1 ? 0 : Random.Range(0, count)]) = now;
            soulReaperReady = now + 0.5f;
        }

        private ref float SoulReaperCooldown(int index)
        {
            switch (index)
            {
                case 0: return ref nextMissile;
                case 1: return ref nextStorm;
                case 2: return ref thunderReady;
                case 3: return ref lightningReady;
                case 4: return ref hammerReady;
                case 5: return ref orbReady;
                case 6: return ref mindReady;
                case 7: return ref briarReady;
                case 8: return ref trapReady;
                case 9: return ref wispReady;
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

        private void DrawRaidDrops(List<Vector3> drops, string label)
        {
            if (drops.Count == 0)
                return;

            var camera = Camera.main;
            if (!camera)
                return;
            foreach (Vector3 drop in drops)
            {
                Vector3 point = camera.WorldToScreenPoint(drop + Vector3.up * 0.5f);
                if (point.z > 0)
                    GUI.Label(new Rect(point.x - 16, Screen.height - point.y, 48, 24), label);
            }
        }

        public static int MissileCount(int level) => 5 + 4 * (Mathf.Clamp(level, 1, 3) - 1);
        public static float StormInterval(int level) => (100 - 15 * Mathf.Clamp(level, 1, 3)) / 100f;

        private static readonly int[] RequiredLevels = { 1, 6, 12, 18 };
        internal int PendingRewards => Mathf.Max(0, raidLevel + bonusRewards - rewardsSpent);
        private bool AllowsRaidClass(RaidClass raidClass) => !LiveMatch || SelectedRaidClass == raidClass;

        private bool CanChooseAncientKnowledge() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && ancientKnowledgeRank < 2 && (ancientKnowledgeRank == 0 || raidLevel >= 8);

        internal bool TryChooseAncientKnowledge()
        {
            if (!CanChooseAncientKnowledge())
                return false;

            ancientKnowledgeRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseDivination() => AllowsRaidClass(RaidClass.Seeress) && PendingRewards > 0 &&
            divinationRank < 2 && (divinationRank == 0 ? progress.IsLearned(DivinationUnlock) : raidLevel >= 8);

        internal bool TryChooseDivination()
        {
            if (!CanChooseDivination())
                return false;

            divinationRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChoose(bool missile)
        {
            int rank = missile ? missileRank : stormRank;
            string unlock = missile ? MissileUnlock : StormUnlock;
            string evolution = missile ? MissileEvolution : StormEvolution;
            return (!missile || AllowsRaidClass(RaidClass.Seeress)) &&
                PendingRewards > 0 && rank < RequiredLevels.Length && raidLevel >= RequiredLevels[rank] &&
                progress.IsLearned(unlock) && (rank < 3 || progress.IsLearned(evolution));
        }

        internal bool TryChoose(bool missile)
        {
            if (!CanChoose(missile))
                return false;

            if (missile) missileRank++;
            else stormRank++;

            rewardsSpent++;
            return true;
        }

        private bool CanChooseThunder() => PendingRewards > 0 && thunderRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[thunderRank] && (thunderRank < 3 || progress.IsLearned(ThunderEvolution));

        internal bool TryChooseThunder()
        {
            if (!CanChooseThunder())
                return false;

            thunderRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseLightning() => PendingRewards > 0 && lightningRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[lightningRank] && (lightningRank < 3 || progress.IsLearned(LightningEvolution));

        internal bool TryChooseLightning()
        {
            if (!CanChooseLightning())
                return false;

            lightningRank++;
            rewardsSpent++;
            return true;
        }

        private bool CanChooseOrb() => PendingRewards > 0 && orbRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[orbRank] && (orbRank < 3 || progress.IsLearned(OrbEvolution));

        internal bool TryChooseOrb()
        {
            if (!CanChooseOrb())
                return false;

            orbRank++;
            rewardsSpent++;
            return true;
        }

        private void TryOrb(float now)
        {
            if (orbRank == 0 || now < orbReady)
                return;

            int rank = Mathf.Min(orbRank, 3);
            bool evolved = orbRank == 4 && progress.IsLearned(OrbEvolution);
            orbReady = now + ScaleSkillCooldown(evolved ? 1.5f : 2.5f - rank * 0.25f);
            LaunchOrb(now, rank, evolved);
            if (RollAnsuzRepeat(ref orbReady))
                LaunchOrb(now, rank, evolved);
        }

        private void LaunchOrb(float now, int rank, bool evolved)
        {
            float angle = Random.Range(0f, 360f);
            Vector3 origin = caster.transform.position;
            orbs.Add(new OrbFlight
            {
                origin = origin,
                previous = origin,
                direction = Quaternion.Euler(0, angle, 0) * Vector3.forward,
                started = now,
                expires = now + (evolved ? 3f : 2f),
                nextPulse = now,
                damage = evolved ? 25 : 11 + rank * 3,
                evolved = evolved
            });
        }

        private void TickOrbs(float now)
        {
            for (int i = orbs.Count - 1; i >= 0; i--)
            {
                var orb = orbs[i];
                Vector3 position = orb.origin + orb.direction * ((Mathf.Min(now, orb.expires) - orb.started) * 4f);
                if (orb.evolved)
                {
                    if (now <= orb.expires && now >= orb.nextPulse)
                    {
                        PulseOrb(position, orb.damage);
                        orb.nextPulse = now + 0.7f;
                    }
                }
                else
                {
                    Vector3 segment = position - orb.previous;
                    float length = segment.magnitude;
                    CollectTargets(orb.previous, length + 0.75f);
                    foreach (var target in targets)
                    {
                        Vector3 offset = target.transform.position - orb.previous;
                        float projected = Mathf.Clamp(Vector3.Dot(offset, orb.direction), 0f, length);
                        if ((offset - orb.direction * projected).sqrMagnitude <= 0.5625f && orb.hit.Add(target))
                            DealThorDamage(target, orb.damage);
                    }

                    orb.previous = position;
                }

                if (now >= orb.expires)
                    orbs.RemoveAt(i);
            }
        }

        private void PulseOrb(Vector3 position, int mainDamage)
        {
            CollectTargets(position, 1.25f);
            if (targets.Count == 0)
                return;

            Health target = targets[0];
            position = target.transform.position;
            DealThorDamage(target, mainDamage);
            CollectTargets(position, 4f);
            int remaining = 3;
            for (int i = 0; i < targets.Count && remaining > 0; i++)
            {
                if (targets[i] == target)
                    continue;

                DealThorDamage(targets[i], 10);
                remaining--;
            }
        }

        private bool CanChooseHammer() => PendingRewards > 0 && hammerRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[hammerRank] && (hammerRank < 3 || progress.IsLearned(HammerEvolution));

        internal bool TryChooseHammer()
        {
            if (!CanChooseHammer())
                return false;

            hammerRank++;
            rewardsSpent++;
            return true;
        }

        private void TryHammer(float now)
        {
            if (hammerRank == 0 || now < hammerReady)
                return;

            bool evolved = hammerRank == 4 && progress.IsLearned(HammerEvolution);
            float range = evolved ? 9f : 3f + 3f * Mathf.Min(hammerRank, 3);
            hammerReady = now + ScaleSkillCooldown(evolved ? 4f : 5f);
            LaunchHammer(now, range, evolved);
            if (RollAnsuzRepeat(ref hammerReady))
                LaunchHammer(now, range, evolved);
        }

        private void LaunchHammer(float now, float range, bool evolved)
        {
            var pose = caster.transform;
            hammers.Add(new HammerFlight
            {
                origin = pose.position,
                turnaround = pose.position + pose.forward * range,
                turnAt = now + 2.5f,
                returnAt = now + 5f,
                evolved = evolved
            });
        }

        private void TickHammers(float now)
        {
            for (int i = hammers.Count - 1; i >= 0; i--)
            {
                var hammer = hammers[i];
                int damage = hammer.evolved ? 40 : 30;
                if (!hammer.turned && now >= hammer.turnAt)
                {
                    SweepDamageLine(hammer.origin, hammer.turnaround, ScaleThorDamage(damage));
                    if (hammer.evolved) StrikeThorDamage(hammer.turnaround, 5f, damage);
                    hammer.turned = true;
                    hammers[i] = hammer;
                }

                if (now >= hammer.returnAt)
                {
                    Vector3 destination = caster.transform.position;
                    SweepDamageLine(hammer.turnaround, destination, ScaleThorDamage(damage));
                    if (hammer.evolved) StrikeThorDamage(destination, 5f, damage);
                    hammers.RemoveAt(i);
                }
            }
        }

        private void SweepDamageLine(Vector3 start, Vector3 end, int damage, float radius = 0.6f,
            float burnChance = 0f, float hitTime = 0f)
        {
            IndicatePath(start, end, radius);
            Vector3 path = end - start;
            float length = path.magnitude;
            CollectTargets(start, length + radius);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - start;
                float fraction = length > 0 ? Mathf.Clamp01(Vector3.Dot(offset, path) / (length * length)) : 0;
                if ((offset - fraction * path).sqrMagnitude <= radius * radius)
                {
                    Deal(target, damage);
                    if (burnChance > 0f && target && target.CurrentHealth > 0 && Random.value < burnChance)
                        ApplyPreviewBurn(target, hitTime, ScaleClassSkillDamage(4));
                }
            }
        }

        private void TryLightning(float now)
        {
            if (lightningRank == 0 || now < lightningReady)
                return;

            bool evolved = lightningRank == 4 && progress.IsLearned(LightningEvolution);
            int rank = Mathf.Min(lightningRank, 3);
            int damage = evolved ? 28 : 14 + rank * 3;
            float radius = evolved ? 1f : 0.3f;
            lightningReady = now + ScaleSkillCooldown(evolved ? 0.3f : 4.75f - rank * 0.5f);

            PlayLightningAudio(evolved);
            StrikeLightning(radius, damage);
            if (RollAnsuzRepeat(ref lightningReady))
                StrikeLightning(radius, damage);
        }

        private void StrikeLightning(float radius, int damage)
        {
            // TPS preview places the four strikes three metres from the caster.
            var pose = caster.transform;
            Vector3 origin = pose.position;
            StrikeThorDamage(origin + pose.forward * 3f, radius, damage);
            StrikeThorDamage(origin - pose.forward * 3f, radius, damage);
            StrikeThorDamage(origin + pose.right * 3f, radius, damage);
            StrikeThorDamage(origin - pose.right * 3f, radius, damage);
            ShowGmLightning(origin + pose.forward * 3f);
            ShowGmLightning(origin - pose.forward * 3f);
            ShowGmLightning(origin + pose.right * 3f);
            ShowGmLightning(origin - pose.right * 3f);
        }

        private static int ReadGodDamageBonus(SkillProgress progress)
        {
            if (progress.IsLearned("virtue_nidhogg_02_c")) return 22;
            if (progress.IsLearned("virtue_nidhogg_02_b")) return 10;
            return progress.IsLearned("virtue_nidhogg_02_a") ? 4 : 0;
        }

        private int ScaleGodDamage(int damage) => (int)(((long)damage * (100 + GodSkillDamageBonus) + 50) / 100);

        private void DealGodDamage(Health target, int damage) => Deal(target, ScaleGodDamage(damage));

        private int ScaleThorDamage(int damage) =>
            (int)(((long)damage * (100 + GodSkillDamageBonus) * (100 + thorPotentialRank * 20) + 5000) / 10000);

        private void DealThorDamage(Health target, int damage) => Deal(target, ScaleThorDamage(damage));

        private void StrikeThorDamage(Vector3 origin, float radius, int damage) =>
            Strike(origin, radius, ScaleThorDamage(damage));

        private static int ReadClassSkillDamageBonus(SkillProgress progress, string prefix)
        {
            if (progress.IsLearned(prefix + "d")) return 35;
            if (progress.IsLearned(prefix + "c")) return 20;
            if (progress.IsLearned(prefix + "b")) return 10;
            return progress.IsLearned(prefix + "a") ? 5 : 0;
        }

        private int ClassSkillDamagePercent => 100 + classSkillDamageBonus +
            (MannazEvolved ? 75 : Mathf.Min(mannazRank, 3) * 15) +
            (essenceExpansionChosen ? 15 : 0) + PrimevalCurrentsBonus;

        private int ScaleClassSkillDamage(int damage) =>
            (int)(((long)damage * ClassSkillDamagePercent + 50) / 100);

        private float ScaleClassSkillDamage(float damage) => damage * ClassSkillDamagePercent * 0.01f;

        private bool CanChoosePrimevalCurrents() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && primevalCurrentsRank < 2 && (primevalCurrentsRank == 0 || raidLevel >= 8);

        internal bool TryChoosePrimevalCurrents()
        {
            if (!CanChoosePrimevalCurrents())
                return false;

            primevalCurrentsRank++;
            rewardsSpent++;
            return true;
        }

        private float ScaleSeerSkillDuration(float duration) => essenceExpansionChosen ? duration * 1.5f : duration;

        private bool CanChooseEssenceExpansion() => HasLiveCaster && AllowsRaidClass(RaidClass.Seeress) &&
            PendingRewards > 0 && !essenceExpansionChosen;

        internal bool TryChooseEssenceExpansion()
        {
            if (!CanChooseEssenceExpansion())
                return false;

            essenceExpansionChosen = true;
            rewardsSpent++;
            return true;
        }

        private void DealRangerSkillDamage(Health target, int damage) => Deal(target, ScaleClassSkillDamage(damage));

        private void StrikeRangerSkillDamage(Vector3 position, float radius, int damage) =>
            Strike(position, radius, ScaleClassSkillDamage(damage));

        private void StrikeGodDamage(Vector3 position, float radius, int damage) =>
            Strike(position, radius, ScaleGodDamage(damage));

        private void SweepGodDamage(Vector3 start, Vector3 end, int damage, float radius = 0.6f) =>
            SweepDamageLine(start, end, ScaleGodDamage(damage), radius);

        private void Strike(Vector3 position, float radius, int damage)
        {
            IndicateRing(position, radius);
            CollectTargets(position, radius);
            foreach (var target in targets)
                Deal(target, damage);
        }

        private void TryThunder(float now)
        {
            if (thunderRank == 0 || thunderStrikes > 0 || now < thunderReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            bool evolved = thunderRank == 4 && progress.IsLearned(ThunderEvolution);
            thunderReady = now + ScaleSkillCooldown(evolved ? 2f : 6f);
            thunderStrikes = evolved ? 1 : 4 + 3 * (Mathf.Min(thunderRank, 3) - 1);
            thunderCastCount = RollAnsuzRepeat(ref thunderReady) ? 2 : 1;
            thunderDamage = evolved ? 45 : 20;
            thunderRadius = evolved ? 4f : 1f;
            nextThunderStrike = now;
        }

        private int thunderDamage, thunderCastCount;
        private float thunderRadius;

        private void TickThunder(float now)
        {
            if (thunderStrikes == 0 || now < nextThunderStrike)
                return;

            thunderStrikes--;
            nextThunderStrike = now + 0.2f;
            for (int i = 0; i < thunderCastCount; i++)
                StrikeThunder();
        }

        private void StrikeThunder()
        {
            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            Vector3 center = targets[0].transform.position;
            CollectTargets(center, thunderRadius);
            IndicateRing(center, thunderRadius);
            foreach (var target in targets)
                DealThorDamage(target, thunderDamage);
        }

        private void Update()
        {
            Tick(Time.time);
            TickGmIndicators(Time.time);
            UpdateLiveRewardNavigation();
        }

        internal void Tick(float now)
        {
            TickGmLightning(now);
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0)
            {
                ClearCasterEffects();
                return;
            }

            TickHammertime(now);
            TickIronFist(now);
            TickShieldwall(now);
            TickAnvil(now);
            TickSeismic(now);
            TickValkyrieCharge(now);
            TickSpearBarrier(now);
            TickJavelins(now);
            TickShadowImages(now);
            TickDivineRebirth();
            TickSoulShards(now);
            TickDistancePassives(now);
            RechargeRaidDash(now);
            TickLastStand(now);
            TryBlazingSoul(now);
            TickRage(now);
            RechargeEldritchWard(now);
            CollectNearbyGold();
            CollectExperienceCrystals(now);
            if ((automatic || LiveMatch) && (!tree || !tree.enabled))
            {
                TryMissiles(now);
                TryStorm(now);
                TryThunder(now);
                TryLightning(now);
                TryHammer(now);
                TryOrb(now);
                TryMindPulse(now);
                TryBriar(now);
                TryTimeTrap(now);
                TryWisps(now);
                TryCuttingEdge(now);
                TryAssault(now);
                TrySwirling(now);
                TryThrowingAxes(now);
                TryTotem(now);
                TryHeartseeker(now);
                TryArrowRain(now);
                TrySkull(now);
                TryFocus(now, automaticCast: true);
                TryNet(now);
                TryAura(now);
                TryMagicSword(now);
                TryBlessing(now);
                TryDiscs(now);
                TryValkyrie(now);
                TryPlague(now);
                TryFireBarrier(now);
                TrySerpent(now);
                TryFirestar(now);
                TryVipers(now);
                TryComet(now);
                TryFrostRing(now);
                TryFrostArrow(now);
                TrySnowflake(now);
                TrySnowFight(now);
                TryIceField(now);
                TryGungnir(now);
                TryRavens(now);
                TryWinterRaven(now);
                TryRagnarok(now);
                TryBoulder(now);
                TryWarrior(now);
                TryVortex(now);
                TryShip(now);
                TryKraken(now);
                TryLeviathan(now);
                TryTidalBarrier(now);
                TryWingStrike(now);
                TryCalamity(now);
                TryDiseaseRay(now);
                TryRaiseDead(now);
                TryTerror(now);
                TryDaggerDance(now);
                TryShrapnelPot(now);
                TryFlamingRay(now);
                TrySisterhood(now);
                TryFlamingDarts(now);
                TryHammertime(now);
                TryIronFist(now);
                TryShieldwall(now);
                TryAnvil(now);
                TrySeismic(now);
                TryValkyrieCharge(now);
                TryFallenValkyries(now);
                TrySpearBarrier(now);
                TryJavelins(now);
                TryShadowImages(now);
            }

            TickThunder(now);
            TickMagicMissiles(now);
            TickHammers(now);
            TickOrbs(now);
            TickBriars(now);
            TickTimeTraps(now);
            TickWisps(now);
            TickSwirling(now);
            TickTotems(now);
            TickArrowRain(now);
            TickFocus(now);
            TickRangerArrows(now);
            TickNets(now);
            TickMagicSword(now);
            TickBlessing(now);
            TickDiscs(now);
            TickValkyrie(now);
            TickPlague(now);
            TickFireBarrier(now);
            TickFlamingRay(now);
            TickSisterhood(now);
            TickFirestars(now);
            TickVipers(now);
            TickComet(now);
            TickFrostRing(now);
            ExpireSkillStops(now);
            ClearRaidSkillSlows(now);
            TickSnowflake(now);
            TickIceField(now);
            TickRavens(now);
            TickWinterRaven(now);
            TickBoulders(now);
            TickWarrior(now);
            TickShrapnelPot(now);
            TickPreviewPoisons(now);
            TickVortex(now);
            TickShip(now);
            TickKraken(now);
            TickLeviathan(now);
            TickTidalBarrier(now);
            TickCalamity(now);
            ClearDiseases(now);
            ClearRaidCurses(now);
            TickSoulmate(now);
            TickRaiseDead(now);
            TickTerror(now);
            TickSpitefulPace(now);
            TickRegenerationRune(now);
            RefreshHealthRune();

            if (now >= stormEnd || now < nextStrike || !progress.IsLearned(StormUnlock))
                return;

            nextStrike = now + strikeInterval;
            for (int i = 0; i < sentinelCastCount; i++)
                StrikeSentinel(sentinelPosition, 30);
        }

        internal void Cast(float now)
        {
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0)
                return;

            TickLastStand(now);
            TryMissiles(now);
            TryStorm(now);
            TryThunder(now);
            TryLightning(now);
            TryHammer(now);
            TryOrb(now);
            TryMindPulse(now);
            TryBriar(now);
            TryTimeTrap(now);
            TryWisps(now);
            TryCuttingEdge(now);
            TryAssault(now);
            TrySwirling(now);
            TryThrowingAxes(now);
            TryTotem(now);
            TryHeartseeker(now);
            TryArrowRain(now);
            TrySkull(now);
            TryFocus(now);
            TryNet(now);
            TryAura(now);
            TryMagicSword(now);
            TryBlessing(now);
            TryDiscs(now);
            TryValkyrie(now);
            TryPlague(now);
            TryFireBarrier(now);
            TrySerpent(now);
            TryFirestar(now);
            TryVipers(now);
            TryComet(now);
            TryFrostRing(now);
            TryFrostArrow(now);
            TrySnowflake(now);
            TrySnowFight(now);
            TryIceField(now);
            TryGungnir(now);
            TryRavens(now);
            TryWinterRaven(now);
            TryRagnarok(now);
            TryBoulder(now);
            TryWarrior(now);
            TryVortex(now);
            TryShip(now);
            TryKraken(now);
            TryLeviathan(now);
            TryTidalBarrier(now);
            TryWingStrike(now);
            TryCalamity(now);
            TryDiseaseRay(now);
            TryRaiseDead(now);
            TryTerror(now);
            TryDaggerDance(now);
            TryShrapnelPot(now);
            TryFlamingRay(now);
            TrySisterhood(now);
            TryFlamingDarts(now);
            TryHammertime(now);
            TryIronFist(now);
            TryShieldwall(now);
            TryAnvil(now);
            TrySeismic(now);
            TryValkyrieCharge(now);
            TryFallenValkyries(now);
            TrySpearBarrier(now);
            TryJavelins(now);
            TryShadowImages(now);
        }

        internal void SetRaidLevel(int level)
        {
            raidLevel = Mathf.Clamp(level, 1, Mathf.Min(curve.maxLevel, curve.thresholds.Length));
            experience = curve.thresholds[raidLevel - 1];
            experienceRemainder = 0;
        }

        private void TryMissiles(float now)
        {
            if (now < nextMissile || missileRank == 0 || !progress.IsLearned(MissileUnlock))
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, 10f);
            if (targets.Count == 0)
                return;

            nextMissile = now + ScaleSkillCooldown(4f);
            if (missileRank == 4 && progress.IsLearned(MissileEvolution))
            {
                LaunchMagicMissiles(origin + Vector3.up, now, 1, 30, shatters: true);
                return;
            }

            LaunchMagicMissiles(origin + Vector3.up, now, MissileCount(missileRank), 8, shotInterval: 0.08f,
                fromCaster: true);
        }

        private void LaunchMagicMissiles(Vector3 position, float now, int count, int damage,
            float shotInterval = 0f, bool shatters = false, bool fromCaster = false)
        {
            int available = targets.Count;
            Vector3 fallbackHeading = caster.transform.forward;
            for (int shot = 0; shot < count; shot++)
            {
                Health target = null;
                if (available > 0)
                {
                    int index = Random.Range(0, available);
                    target = targets[index];
                    targets[index] = targets[--available];
                }

                float elevation = Random.Range(50f, 80f);
                Vector3 heading = target ? RaidSkillAimPoint(target) - position : fallbackHeading;
                float yaw = shot * 60f;
                float launchAt = now + shot * shotInterval;
                var missile = new MagicMissile
                {
                    target = target,
                    position = position,
                    direction = MagicMissileLaunchDirection(heading, fallbackHeading, elevation, yaw),
                    launchElevation = elevation,
                    launchYaw = yaw,
                    lastTick = launchAt,
                    expires = launchAt + MagicMissileLifetime,
                    nextVisualSample = launchAt + 0.1f,
                    damage = damage,
                    shatters = shatters,
                    refreshCasterOrigin = fromCaster && target
                };
                if (shot == 0)
                    StartMagicMissileVisual(ref missile);
                magicMissiles.Add(missile);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                ValidationProjectileTrace?.Invoke($"queued target={(target ? target.netId : 0)} " +
                        $"origin={position} direction={missile.direction} launch={launchAt:F3} damage={damage} shatters={shatters}");
#endif
            }
        }

        private void TickMagicMissiles(float now)
        {
            if (magicMissiles.Count == 0)
                return;
            if (!progress.IsLearned(MissileUnlock))
            {
                ClearMagicMissiles();
                return;
            }

            for (int i = magicMissiles.Count - 1; i >= 0 && HasLiveCaster; i--)
            {
                var missile = magicMissiles[i];
                if (now <= missile.lastTick)
                    continue;

                if (missile.visualId == 0)
                {
                    if (missile.refreshCasterOrigin)
                        missile.position = caster.transform.position + Vector3.up;
                    Health launchTarget = missile.target;
                    if (launchTarget && launchTarget.CurrentHealth > 0 && launchTarget.Team != caster.Team)
                        missile.direction = MagicMissileLaunchDirection(RaidSkillAimPoint(launchTarget) - missile.position,
                            caster.transform.forward, missile.launchElevation, missile.launchYaw);
                    StartMagicMissileVisual(ref missile);
                }
                float sampleTime = Mathf.Min(now, missile.expires);
                float homingStart = missile.expires - MagicMissileLifetime + MagicMissileHomingDelay;
                while (missile.lastTick < sampleTime)
                {
                    float stepEnd = Mathf.Min(sampleTime, missile.lastTick + 0.02f);
                    if (missile.lastTick < homingStart)
                        stepEnd = Mathf.Min(stepEnd, homingStart);
                    float delta = stepEnd - missile.lastTick;
                    float distance = MagicMissileSpeed * delta;
                    Vector3 direction = missile.direction;
                    float terrainDistance = float.PositiveInfinity;
                    if (direction != Vector3.zero && Physics.SphereCast(missile.position, MagicMissileRadius,
                        direction, out var terrain, distance, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                        terrainDistance = terrain.distance;

                    Health hit = FindMagicMissileContact(missile, direction, distance, terrainDistance, out float contact);
                    float travel = Mathf.Min(distance, contact);
                    missile.position += direction * travel;
                    if (hit || !float.IsPositiveInfinity(terrainDistance) || stepEnd >= missile.expires)
                    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                        ValidationProjectileTrace?.Invoke($"finished hit={(hit ? hit.netId : 0)} " +
                            $"locked={(missile.target ? missile.target.netId : 0)} " +
                            $"terrain={!float.IsPositiveInfinity(terrainDistance)} at={missile.position}");
#endif
                        magicMissiles.RemoveAt(i);
                        SendMagicMissileVisual(missile, true);
                        if (hit)
                            Deal(hit, ScaleClassSkillDamage(missile.damage));
                        if (missile.shatters && HasLiveCaster)
                        {
                            CollectTargets(missile.position - Vector3.up, 3f);
                            float finishedAt = Mathf.Min(stepEnd, missile.lastTick + travel / MagicMissileSpeed);
                            LaunchMagicMissiles(missile.position, finishedAt, 17, 12, shotInterval: 0.03f);
                        }
                        break;
                    }

                    Health target = missile.target;
                    if (!target || target.CurrentHealth <= 0 || target.Team == caster.Team)
                        missile.target = null;
                    else if (missile.lastTick >= homingStart)
                    {
                        float homingAge = missile.lastTick - homingStart;
                        float turnRate = Mathf.Min(30f, 4f + 13f * homingAge);
                        Vector3 desired = (RaidSkillAimPoint(target) - missile.position).normalized;
                        if (desired != Vector3.zero)
                            missile.direction = Vector3.RotateTowards(direction, desired, turnRate * delta, 0f);
                    }

                    missile.lastTick = stepEnd;
                }
                if (missile.lastTick == sampleTime)
                {
                    if (sampleTime >= missile.nextVisualSample)
                    {
                        SendMagicMissileVisual(missile, false);
                        missile.nextVisualSample = sampleTime + 0.1f;
                    }
                    magicMissiles[i] = missile;
                }
            }
        }

        private void StartMagicMissileVisual(ref MagicMissile missile)
        {
            missile.visualId = ++nextProjectileVisualId;
            SendMagicMissileVisual(missile, false);
        }

        private void ClearMagicMissiles()
        {
            foreach (var missile in magicMissiles)
                SendMagicMissileVisual(missile, true);
            magicMissiles.Clear();
        }

        private void SendMagicMissileVisual(MagicMissile missile, bool finished) =>
            SendProjectileTrail(missile.visualId, missile.position, finished, heartArrow: false);

        private void SendProjectileTrail(uint id, Vector3 position, bool finished, bool heartArrow)
        {
            if (id != 0 && caster is CharacterInstance actor && actor && actor.isServer && actor.netId != 0)
                actor.RpcRaidProjectileTrail(id, position, finished, heartArrow);
        }

        internal static Vector3 RaidSkillAimPoint(Health target) =>
            target is CharacterInstance character ? character.CombatAimPoint : target.transform.position + Vector3.up;

        internal static Vector3 MagicMissileLaunchDirection(Vector3 heading, Vector3 fallback, float elevation, float yaw)
        {
            heading.y = 0f;
            if (heading.sqrMagnitude <= 0.0001f)
                heading = Vector3.ProjectOnPlane(fallback, Vector3.up);
            Vector3 elevated = Vector3.RotateTowards(heading.normalized, Vector3.up, elevation * Mathf.Deg2Rad, 0f);
            return Quaternion.AngleAxis(yaw, Vector3.up) * elevated;
        }

        private Health FindMagicMissileContact(MagicMissile missile, Vector3 direction, float distance,
            float terrainDistance, out float contact)
        {
            Health nearest = null;
            contact = terrainDistance;
            float radiusSquared = MagicMissileRadius * MagicMissileRadius;
            foreach (Health candidate in GameManager.HealthInstances.Values)
            {
                if (!candidate || !candidate.isServer || candidate.CurrentHealth <= 0 || candidate.Team == caster.Team)
                    continue;

                Vector3 offset = RaidSkillAimPoint(candidate) - missile.position;
                bool overlapsStart = offset.sqrMagnitude <= radiusSquared;
                if (overlapsStart && candidate != missile.target)
                    continue;

                float along = Vector3.Dot(offset, direction);
                float lateralSquared = (offset - direction * along).sqrMagnitude;
                if (lateralSquared > radiusSquared)
                    continue;

                float halfChord = Mathf.Sqrt(radiusSquared - lateralSquared);
                float entry = Mathf.Max(0f, along - halfChord);
                if (along + halfChord < 0f || entry > distance || entry >= contact)
                    continue;

                contact = entry;
                nearest = candidate;
            }
            return nearest;
        }

        private void TryStorm(float now)
        {
            if (now < nextStorm || stormRank == 0 || !progress.IsLearned(StormUnlock))
                return;

            CollectTargets(caster.transform.position, 5f);
            if (targets.Count == 0)
                return;

            if (stormRank == 4 && progress.IsLearned(StormEvolution))
            {
                stormEnd = 0;
                nextStorm = now + ScaleSkillCooldown(0.55f);
                DealThorDamage(targets[0], 35);
                if (RollAnsuzRepeat(ref nextStorm))
                    StrikeSentinel(caster.transform.position, 35);
                return;
            }

            sentinelPosition = caster.transform.position;
            nextStorm = now + ScaleSkillCooldown(9f);
            nextStrike = now;
            strikeInterval = StormInterval(stormRank);
            stormEnd = now + 3f;
            sentinelCastCount = RollAnsuzRepeat(ref nextStorm) ? 2 : 1;
        }

        private int sentinelCastCount;

        private void StrikeSentinel(Vector3 origin, int damage)
        {
            IndicateRing(origin, 5f);
            CollectTargets(origin, 5f);
            if (targets.Count != 0)
                DealThorDamage(targets[0], damage);
        }

        private void CollectTargets(Vector3 origin, float radius)
        {
            targets.Clear();
            float rangeSquared = radius * radius;
            foreach (Health candidate in GameManager.HealthInstances.Values)
            {
                if (candidate && candidate.isServer && candidate != caster && candidate.CurrentHealth > 0 &&
                    candidate.Team != caster.Team && (candidate.transform.position - origin).sqrMagnitude <= rangeSquared)
                    targets.Add(candidate);
            }
        }

        private void Deal(Health target, int damage, bool periodic = false)
        {
            if (caster is MTPSKIT.Gameplay.CharacterInstance hero && hero.HeroBonuses.damage != 0f)
                damage = Mathf.RoundToInt(damage * (1f + hero.HeroBonuses.damage));
            int before = target.CurrentHealth;
            Vector3 point = target.transform.position + Vector3.up;
            if (periodic)
                target.ServerApplyRangeDot(damage, AttackType.explosion, caster);
            else
                target.ServerApplyRaidSkillDamage(damage, caster);
            if (target && target.CurrentHealth < before)
            {
                RecordImpact(point);
                IndicateHit(target.transform.position);
            }
        }

        private void OnGUI()
        {
            if (SuppressPresentation) return;
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0 || (tree && tree.enabled))
                return;

            DrawRaidDrops(goldDrops, "+1 G");
            DrawRaidDrops(experienceCrystals, "XP");
            DrawImpacts();
            DrawEldritchWardStatus(Time.time);
            DrawRageStatus(Time.time);
            DrawRaidDashStatus(Time.time);
            if (!ShowControls)
                return;
            GUILayout.BeginArea(new Rect(16, Mathf.Max(8, Screen.height - 544), 360, Mathf.Min(532, Screen.height - 16)), GUI.skin.box);
            if (!LiveMatch)
                automatic = GUILayout.Toggle(automatic, "Automatic casts (Jotunnslayer preview)");
            GUILayout.Label(RewardText($"等级 {raidLevel}   |   经验 {experience}   |   可选 {PendingRewards}   |   金币 {progress.Gold}",
                $"Raid level {raidLevel}   |   XP {experience}   |   Choices {PendingRewards}   |   Gold {progress.Gold}"), RewardLabelStyle);
            if (!LiveMatch)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Level −")) SetRaidLevel(raidLevel - 1);
                if (GUILayout.Button("Level +")) SetRaidLevel(raidLevel + 1);
                GUILayout.EndHorizontal();
            }
            DrawRewards();
            if (!LiveMatch && GUILayout.Button("Cast learned skills"))
            {
                Cast(Time.time);
            }
            GUILayout.EndArea();
        }
    }
}






























































