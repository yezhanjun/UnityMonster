using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
namespace MTPSKIT.Gameplay
{
    // Server-owned laboratory inventory, scoped to the current character lifetime.
    public sealed partial class RangeResearchItems : MonoBehaviour
    {
        public int Steak, Glasses, Tooth, ArmorPlates, Medkits, ArmorPiercingRounds, Crowbars, Bears, Hooves, EnergyDrinks, ParentEggs, Watches, BrokenWatches, HealingRacks, Slugs, HealingPotions, EmptyBottles, Brooches, Aegises, Daggers;
        public float Barrier { get; private set; }
        float medkitReadyAt;
        float lastDamageAt = float.NegativeInfinity;
        float nextSlugHealAt;
        float elementalRingsReadyAt;
        float seedHealingRemainder;
        int knurlStacks;
        float knurlHealingRemainder;
        const int KnurlIndex = 41;
        const int CritDamageIndex = 42;
        int critDamageStacks;
        const int HealOnCritIndex = 43;
        const int RedWhipIndex = 44;
        const int PredatoryIndex = 45;
        const int ExtraLifeIndex = 46;
        const int ExtraLifeConsumedIndex = 47;
        const int VoidExtraLifeIndex = 48;
        const int VoidExtraLifeConsumedIndex = 49;
        const int SaferSpacesIndex = 50;
        const int WeepingFungusIndex = 51;
        const int BustlingFungusIndex = 52;
        const int NeedletickIndex = 53;
        const int PolyluteIndex = 54;
        const int VoidsentFlameIndex = 55;
        const int LostSeersIndex = 56;
        int lostSeersLenses;
        int voidsentFlames;
        int polylutes;
        int needleticks;
        int bustlingFungus;
        float nextFungusPulse;
        Vector3 fungusAnchor;
        int weepingFungus;
        float weepingHealingRemainder;
        bool weepingHealingActive, bustlingFungusActive;
        bool bucklerFeedbackActive;
        int saferSpaces;
        float saferSpacesReadyAt;
        int extraLives, voidExtraLives;
        internal int ConsumedExtraLives { get; private set; }
        internal int ConsumedVoidExtraLives { get; private set; }
        float extraLifeProtectedUntil;
        internal bool HasExtraLifeProtection => Time.time < extraLifeProtectedUntil;
        int predatoryStacks;
        List<float> predatoryExpirations;
        const float RedWhipCombatDuration = 5f;
        int healOnCritStacks;
        float critHealingRemainder;
        internal float CriticalChance => Time.time < guaranteedCritUntil ? 1f :
            Mathf.Min(1f, (Glasses * .1f + (healOnCritStacks > 0 ? .05f : 0f) + (predatoryStacks > 0 ? .05f : 0f) + (float)HikingCritBonus) * CollectorMultiplier + (owner is CharacterInstance hero ? hero.HeroBonuses.critical : 0f));
        double CriticalMultiplier => 2d + critDamageStacks + HikingCritBonus;
        double HikingCritBonus => Time.time < hikingBuffUntil ? .01d * hikingBoots * hikingHits : 0d;
        const int KnurlHealthPerStack = 40;
        internal const float KnurlRegenerationPerStack = 1.6f;
        internal enum Equipment : byte { None, Fruit, Elephant, OcularHud, Leech, TeamWarCry, Scanner, Capacitor, MissileLauncher, ExecutiveCard, BlastShower, Effigy, DroneBackup }
        const int FuelCellIndex = 57;
        const int WarHornIndex = 58;
        const int GestureIndex = 59;
        const int ShatteringJusticeIndex = 60;
        const int CloverIndex = 61;
        const int InfusionIndex = 62;
        const int HarpoonIndex = 63;
        const int ScorpionIndex = 64;
        const int DeathMarkIndex = 65;
        const int BerserkersIndex = 66;
        const int TentabaubleIndex = 67;
        const int ChargedPerforatorIndex = 68;
        const int ShatterspleenIndex = 69;
        const int GenesisLoopIndex = 70;
        const int NkuhanaIndex = 71;
        const int LittleDiscipleIndex = 72;
        const int MissileIndex = 73;
        const int PlasmaShrimpIndex = 74;
        const int PocketIcbmIndex = 75;
        const int BrittleCrownIndex = 76;
        const int GhorTomeIndex = 77;
        const int PearlIndex = 78;
        const int ShapedGlassIndex = 79;
        const int SoulboundCatalystIndex = 80;
        const int CeremonialDaggerIndex = 81;
        const int RazorwireIndex = 82;
        const int TeslaCoilIndex = 83;
        const int MiredUrnIndex = 84;
        const int FrostRelicIndex = 85;
        const int FireworkIndex = 86;
        const int MoltenPerforatorIndex = 87;
        const int DeskPlantIndex = 88;
        const int IgnitionTankIndex = 89;
        const int RollOfPenniesIndex = 90;
        const int MochaIndex = 91;
        const int StoneFluxIndex = 92;
        const int ChronicExpansionIndex = 93;
        const int HexOfFamineIndex = 94;
        const int HexOfPainIndex = 95;
        const int CookedSteakIndex = 96;
        const int UltimateMealIndex = 97;
        int ultimateMeals;
        const int StewIndex = 98;
        const int LeptonDaisyIndex = 99;
        int stews, leptonDaisies;
        const int HikingBootsIndex = 100;
        const int QuickFixIndex = 101;
        const int WhiteScrapIndex = 102;
        const int BolsteringLanternIndex = 103;
        const int PowerOrbIndex = 106;
        const int PowerCubeIndex = 107;
        const int AlienHeadIndex = 108;
        const int LightFluxIndex = 109;
        int powerOrbs, powerCubes, alienHeads, lightFlux;
        internal bool HasPowerCubeArmor => isActiveAndEnabled && powerCubes > 0;
        int bolsteringLanterns, lanternBodies;
        int quickFixes, whiteScrap;
        internal static double QuickFixBonus(int stacks) => stacks > 0 ? .5d + .15d * (stacks - 1) : 0d;
        int hikingBoots, hikingHits;
        float hikingBuffUntil;
        CharacterController hikingController;
        int hexOfFamine, hexOfPain, cookedSteaks;
        long famineHealingRemainder;
        int chronicExpansions, chronicKills;
        float chronicUntil;
        long ChronicCap => 5L + 5L * chronicExpansions;
        double ChronicDamageMultiplier => Time.time < chronicUntil && chronicExpansions > 0
            ? 1d + chronicKills * (.025d + .01d * chronicExpansions) : 1d;
        int ignitionTanks, rollsOfPennies;
        internal int ScaleBurnDamage(int damage) => (int)System.Math.Min(int.MaxValue, damage * (1d + 3d * ignitionTanks));
        int deskPlants;
        const float BlastShowerRadius = 5f;
        int moltenPerforators;
        RaycastHit[] moltenContacts;
        internal int FireworkStacks { get; private set; }
        uint nextItemProjectileId;
        int frostRelics;
        Queue<float> frostKillExpiries;
        float nextFrostPulse;
        int miredUrns;
        float nextUrnPulse;
        int teslaCoils;
        float teslaStartedAt, nextTeslaPulse;
        int soulboundCatalysts, ceremonialDaggers, razorwireStacks;
        int pearls, shapedGlass, appliedMaximumHealthAdjustment;
        int ghorTomes;
        int brittleCrowns;
        int pocketIcbms;
        int missiles;
        int littleDisciples;
        float discipleSprintCharge, nextDiscipleTargetCheck;
        CharacterMotor ownerMotor;
        int nkuhanaOpinions;
        double soulEnergy;
        float nextSoulTargetCheck;
        int genesisLoops;
        float genesisReadyAt;
        int shatterspleens;
        int chargedPerforators;
        int tentabaubles;
        int berserkers;
        float lastKillAt = float.NegativeInfinity;
        float secondLastKillAt = float.NegativeInfinity;
        float thirdLastKillAt = float.NegativeInfinity;
        float frenzyUntil;
        int deathMarks;
        float deathMarkUntil;
        int scorpions;
        float permanentArmorLoss;
        int harpoons;
        int infusions, infusionEarned;
        int InfusionBonus => Mathf.Min(infusionEarned, 100 * infusions);
        int clovers;
        int shatteringJustice, pulverizeHits;
        float pulverizedUntil;
        int gestures;
        internal bool EquipmentAutoActivates => gestures > 0;
        int warHorns;
        float warHornUntil;
        int fuelCells;
        float EquipmentRechargeScale => Mathf.Max(.001f, gestures > 0
            ? .5f * Mathf.Pow(.85f, fuelCells + gestures - 1f)
            : Mathf.Pow(.85f, fuelCells));
        EquipmentCharges fruitCharges, elephantCharges, ocularHudCharges, leechCharges;
        EquipmentCharges teamWarCryCharges, scannerCharges, capacitorCharges, missileLauncherCharges, blastShowerCharges, effigyCharges, droneBackupCharges;
        float elephantArmorUntil, guaranteedCritUntil, leechUntil;
        int leechDamageRemainder;
        internal Equipment Equipped { get; private set; }
        internal bool FruitEquipped => Equipped == Equipment.Fruit;
        internal float FruitCooldownRemaining => fruitCharges.Remaining(45f * EquipmentRechargeScale, fuelCells);
        internal float EquipmentCooldownRemaining => Equipped switch
        {
            Equipment.Fruit => FruitCooldownRemaining,
            Equipment.Elephant => elephantCharges.Remaining(45f * EquipmentRechargeScale, fuelCells),
            Equipment.OcularHud => ocularHudCharges.Remaining(60f * EquipmentRechargeScale, fuelCells),
            Equipment.Leech => leechCharges.Remaining(60f * EquipmentRechargeScale, fuelCells),
            Equipment.TeamWarCry => teamWarCryCharges.Remaining(45f * EquipmentRechargeScale, fuelCells),
            Equipment.Scanner => scannerCharges.Remaining(45f * EquipmentRechargeScale, fuelCells),
            Equipment.Capacitor => capacitorCharges.Remaining(20f * EquipmentRechargeScale, fuelCells),
            Equipment.MissileLauncher => missileLauncherCharges.Remaining(45f * EquipmentRechargeScale, fuelCells),
            Equipment.BlastShower => blastShowerCharges.Remaining(20f * EquipmentRechargeScale, fuelCells),
            Equipment.DroneBackup => droneBackupCharges.Remaining(100f * EquipmentRechargeScale, fuelCells),
            Equipment.Effigy => effigyCharges.Remaining(15f * EquipmentRechargeScale, fuelCells),
            _ => 0f
        };

        struct EquipmentCharges
        {
            int spent;
            float nextRecharge;
            internal bool HasDebt => spent > 0;

            internal float Remaining(float duration, int extraCharges)
            {
                while (spent > 0 && Time.time >= nextRecharge)
                {
                    spent--;
                    nextRecharge += duration;
                }
                return spent <= extraCharges ? 0f : nextRecharge - Time.time + (spent - extraCharges - 1) * duration;
            }

            internal void Spend(float duration)
            {
                if (spent++ == 0) nextRecharge = Time.time + duration;
            }

            internal void Advance(float duration, float seconds)
            {
                Remaining(duration, 0);
                if (spent == 0) return;
                nextRecharge -= seconds;
                Remaining(duration, 0);
            }

            internal void Rescale(float baseDuration, float previous, float next)
            {
                Remaining(baseDuration * previous, 0);
                if (spent > 0) nextRecharge = Time.time + (nextRecharge - Time.time) * next / previous;
            }
        }

        void SetFuelCells(int count)
        {
            float previous = EquipmentRechargeScale;
            fuelCells = count;
            RescaleEquipmentRecharge(previous);
        }

        void SetGestures(int count)
        {
            float previous = EquipmentRechargeScale;
            gestures = count;
            RescaleEquipmentRecharge(previous);
        }

        void RescaleEquipmentRecharge(float previous)
        {
            float next = EquipmentRechargeScale;
            fruitCharges.Rescale(45f, previous, next);
            elephantCharges.Rescale(45f, previous, next);
            ocularHudCharges.Rescale(60f, previous, next);
            leechCharges.Rescale(60f, previous, next);
            teamWarCryCharges.Rescale(45f, previous, next);
            scannerCharges.Rescale(45f, previous, next);
            capacitorCharges.Rescale(20f, previous, next);
            missileLauncherCharges.Rescale(45f, previous, next);
            blastShowerCharges.Rescale(20f, previous, next);
            droneBackupCharges.Rescale(100f, previous, next);
            effigyCharges.Rescale(15f, previous, next);
        }

        internal static Equipment EquipmentForCatalog(string id) => id switch
        {
            "Fruit" => Equipment.Fruit,
            "GainArmor" => Equipment.Elephant,
            "CritOnUse" => Equipment.OcularHud,
            "LifestealOnHit" => Equipment.Leech,
            "TeamWarCry" => Equipment.TeamWarCry,
            "Scanner" => Equipment.Scanner,
            "Lightning" => Equipment.Capacitor,
            "CommandMissile" => Equipment.MissileLauncher,
            "Cleanse" => Equipment.BlastShower,
            "CrippleWard" => Equipment.Effigy,
            "DroneBackup" => Equipment.DroneBackup,
            "MultiShopCard" => Equipment.ExecutiveCard,
            _ => Equipment.None
        };

        internal readonly struct PersistentItems
        {
            readonly int knurl, slugs, scope, scythe, redWhip, predatory, extraLives, consumedExtraLives;
            readonly int voidExtraLives, consumedVoidExtraLives;
            readonly int bears, saferSpaces, weepingFungus, bustlingFungus, daggers, needleticks, polylutes;
            readonly int voidsentFlames, lostSeersLenses;
            readonly int bolsteringLanterns, elusiveAntlers, collectors;
            readonly int powerOrbs, powerCubes, alienHeads, lightFlux;
            readonly float saferSpacesReadyAt;
            readonly Equipment equipped;
            readonly int fuelCells, warHorns, gestures, shatteringJustice, clovers;
            readonly int infusions, infusionEarned, harpoons, scorpions, deathMarks, berserkers, tentabaubles, chargedPerforators;
            readonly int shatterspleens, genesisLoops, nkuhanaOpinions, littleDisciples, missiles, plasmaShrimps, pocketIcbms;
            readonly int brittleCrowns, ghorTomes, pearls, shapedGlass, soulboundCatalysts, ceremonialDaggers, razorwireStacks, teslaCoils, miredUrns, frostRelics, fireworks, moltenPerforators, deskPlants, ignitionTanks, rollsOfPennies, mochas, stoneFlux, chronicExpansions, famine, pain, cookedSteaks, ultimateMeals, stews, leptonDaisies, hikingBoots, quickFixes, whiteScrap;
            readonly float teslaStartedAt, nextTeslaPulse;
            readonly float genesisReadyAt;
            readonly EquipmentCharges fruitCharges, elephantCharges, ocularHudCharges, leechCharges;
            readonly EquipmentCharges teamWarCryCharges, scannerCharges, capacitorCharges, missileLauncherCharges, blastShowerCharges, effigyCharges, droneBackupCharges;

            internal PersistentItems(RangeResearchItems items)
            {
                knurl = items.knurlStacks;
                slugs = items.Slugs;
                scope = items.critDamageStacks;
                scythe = items.healOnCritStacks;
                redWhip = items.owner is CharacterInstance character ? character.ResearchRedWhipStacks : 0;
                predatory = items.predatoryStacks;
                extraLives = items.extraLives;
                consumedExtraLives = items.ConsumedExtraLives;
                voidExtraLives = items.voidExtraLives;
                consumedVoidExtraLives = items.ConsumedVoidExtraLives;
                bears = items.Bears;
                saferSpaces = items.saferSpaces;
                weepingFungus = items.weepingFungus;
                bustlingFungus = items.bustlingFungus;
                daggers = items.Daggers;
                needleticks = items.needleticks;
                polylutes = items.polylutes;
                voidsentFlames = items.voidsentFlames;
                lostSeersLenses = items.lostSeersLenses;
                saferSpacesReadyAt = items.saferSpacesReadyAt;
                equipped = items.Equipped;
                fuelCells = items.fuelCells;
                warHorns = items.warHorns;
                gestures = items.gestures;
                shatteringJustice = items.shatteringJustice;
                clovers = items.clovers;
                infusions = items.infusions;
                infusionEarned = items.infusionEarned;
                harpoons = items.harpoons;
                scorpions = items.scorpions;
                deathMarks = items.deathMarks;
                berserkers = items.berserkers;
                tentabaubles = items.tentabaubles;
                chargedPerforators = items.chargedPerforators;
                shatterspleens = items.shatterspleens;
                genesisLoops = items.genesisLoops;
                nkuhanaOpinions = items.nkuhanaOpinions;
                littleDisciples = items.littleDisciples;
                missiles = items.missiles;
                pocketIcbms = items.pocketIcbms;
                brittleCrowns = items.brittleCrowns;
                ghorTomes = items.ghorTomes;
                pearls = items.pearls;
                shapedGlass = items.shapedGlass;
                soulboundCatalysts = items.soulboundCatalysts;
                ceremonialDaggers = items.ceremonialDaggers;
                razorwireStacks = items.razorwireStacks;
                teslaCoils = items.teslaCoils;
                miredUrns = items.miredUrns;
                frostRelics = items.frostRelics;
                fireworks = items.FireworkStacks;
                moltenPerforators = items.moltenPerforators;
                deskPlants = items.deskPlants;
                ignitionTanks = items.ignitionTanks;
                rollsOfPennies = items.rollsOfPennies;
                mochas = items.owner is CharacterInstance mochaOwner ? mochaOwner.ResearchMochas : 0;
                stoneFlux = items.owner is CharacterInstance stoneOwner ? stoneOwner.ResearchStoneFlux : 0;
                chronicExpansions = items.chronicExpansions;
                famine = items.hexOfFamine;
                pain = items.hexOfPain;
                cookedSteaks = items.cookedSteaks;
                ultimateMeals = items.ultimateMeals;
                stews = items.stews;
                leptonDaisies = items.leptonDaisies;
                hikingBoots = items.hikingBoots;
                quickFixes = items.quickFixes;
                whiteScrap = items.whiteScrap;
                bolsteringLanterns = items.bolsteringLanterns;
                elusiveAntlers = items.elusiveAntlers;
                collectors = items.collectors;
                powerOrbs = items.powerOrbs;
                powerCubes = items.powerCubes;
                alienHeads = items.alienHeads;
                lightFlux = items.lightFlux;
                teslaStartedAt = items.teslaStartedAt;
                nextTeslaPulse = items.nextTeslaPulse;
                plasmaShrimps = items.owner is CharacterInstance shrimpOwner ? shrimpOwner.ResearchPlasmaShrimps : 0;
                genesisReadyAt = items.genesisReadyAt;
                fruitCharges = items.fruitCharges;
                elephantCharges = items.elephantCharges;
                ocularHudCharges = items.ocularHudCharges;
                leechCharges = items.leechCharges;
                teamWarCryCharges = items.teamWarCryCharges;
                scannerCharges = items.scannerCharges;
                capacitorCharges = items.capacitorCharges;
                missileLauncherCharges = items.missileLauncherCharges;
                blastShowerCharges = items.blastShowerCharges;
                effigyCharges = items.effigyCharges;
                droneBackupCharges = items.droneBackupCharges;
            }

            internal int BonusHealth => knurl * KnurlHealthPerStack + Mathf.Min(infusionEarned, 100 * infusions);
            internal bool NeedsRestore => lightFlux > 0 || alienHeads > 0 || powerOrbs > 0 || powerCubes > 0 || collectors > 0 || elusiveAntlers > 0 || bolsteringLanterns > 0 || knurl > 0 || slugs > 0 || scope > 0 || scythe > 0 || redWhip > 0 || predatory > 0 ||
                extraLives > 0 || consumedExtraLives > 0 || voidExtraLives > 0 || consumedVoidExtraLives > 0 ||
                bears > 0 || saferSpaces > 0 || weepingFungus > 0 || bustlingFungus > 0 ||
                daggers > 0 || needleticks > 0 || polylutes > 0 || voidsentFlames > 0 || lostSeersLenses > 0 ||
                saferSpacesReadyAt > Time.time || equipped != Equipment.None ||
                fuelCells > 0 || warHorns > 0 || gestures > 0 || shatteringJustice > 0 || clovers > 0 ||
                infusions > 0 || infusionEarned > 0 || harpoons > 0 || scorpions > 0 || deathMarks > 0 || berserkers > 0 ||
                tentabaubles > 0 || chargedPerforators > 0 || shatterspleens > 0 || genesisLoops > 0 ||
                genesisReadyAt > Time.time || nkuhanaOpinions > 0 || littleDisciples > 0 || missiles > 0 ||
                plasmaShrimps > 0 || pocketIcbms > 0 || brittleCrowns > 0 || ghorTomes > 0 || pearls > 0 || shapedGlass > 0 || soulboundCatalysts > 0 || ceremonialDaggers > 0 || razorwireStacks > 0 || teslaCoils > 0 || miredUrns > 0 || frostRelics > 0 || fireworks > 0 || moltenPerforators > 0 || deskPlants > 0 || ignitionTanks > 0 || rollsOfPennies > 0 || mochas > 0 || stoneFlux > 0 || chronicExpansions > 0 || famine > 0 || pain > 0 || cookedSteaks > 0 || ultimateMeals > 0 || stews > 0 || leptonDaisies > 0 || hikingBoots > 0 || quickFixes > 0 || whiteScrap > 0 ||
                fruitCharges.HasDebt || elephantCharges.HasDebt ||
                ocularHudCharges.HasDebt || leechCharges.HasDebt || teamWarCryCharges.HasDebt ||
                scannerCharges.HasDebt || capacitorCharges.HasDebt || missileLauncherCharges.HasDebt || blastShowerCharges.HasDebt || effigyCharges.HasDebt || droneBackupCharges.HasDebt;

            internal void Restore(RangeResearchItems items)
            {
                items.knurlStacks = knurl;
                items.Slugs = slugs;
                items.critDamageStacks = scope;
                items.healOnCritStacks = scythe;
                if (items.owner is CharacterInstance character) character.ResearchRedWhipStacks = redWhip;
                items.predatoryStacks = predatory;
                items.extraLives = extraLives;
                items.ConsumedExtraLives = consumedExtraLives;
                items.voidExtraLives = voidExtraLives;
                items.ConsumedVoidExtraLives = consumedVoidExtraLives;
                items.Bears = bears;
                items.saferSpaces = saferSpaces;
                items.weepingFungus = weepingFungus;
                items.bustlingFungus = bustlingFungus;
                items.Daggers = daggers;
                items.needleticks = needleticks;
                items.polylutes = polylutes;
                items.voidsentFlames = voidsentFlames;
                items.lostSeersLenses = lostSeersLenses;
                items.saferSpacesReadyAt = saferSpacesReadyAt;
                items.Equipped = equipped;
                items.fuelCells = fuelCells;
                items.warHorns = warHorns;
                items.gestures = gestures;
                items.shatteringJustice = shatteringJustice;
                items.clovers = clovers;
                items.infusions = infusions;
                items.infusionEarned = infusionEarned;
                items.harpoons = harpoons;
                items.scorpions = scorpions;
                items.deathMarks = deathMarks;
                items.berserkers = berserkers;
                items.tentabaubles = tentabaubles;
                items.chargedPerforators = chargedPerforators;
                items.shatterspleens = shatterspleens;
                items.genesisLoops = genesisLoops;
                items.nkuhanaOpinions = nkuhanaOpinions;
                items.littleDisciples = littleDisciples;
                items.missiles = missiles;
                items.pocketIcbms = pocketIcbms;
                items.brittleCrowns = brittleCrowns;
                items.ghorTomes = ghorTomes;
                items.pearls = pearls;
                items.shapedGlass = shapedGlass;
                items.soulboundCatalysts = soulboundCatalysts;
                items.ceremonialDaggers = ceremonialDaggers;
                items.razorwireStacks = razorwireStacks;
                items.teslaCoils = teslaCoils;
                items.miredUrns = miredUrns;
                items.frostRelics = frostRelics;
                items.FireworkStacks = fireworks;
                items.moltenPerforators = moltenPerforators;
                items.deskPlants = deskPlants;
                items.ignitionTanks = ignitionTanks;
                items.rollsOfPennies = rollsOfPennies;
                items.chronicExpansions = chronicExpansions;
                items.hexOfFamine = famine;
                items.hexOfPain = pain;
                items.cookedSteaks = cookedSteaks;
                items.ultimateMeals = ultimateMeals;
                items.stews = stews;
                items.leptonDaisies = leptonDaisies;
                items.hikingBoots = hikingBoots;
                items.quickFixes = quickFixes;
                items.whiteScrap = whiteScrap;
                items.elusiveAntlers = elusiveAntlers;
                items.collectors = collectors;
                items.powerOrbs = powerOrbs;
                items.powerCubes = powerCubes;
                items.alienHeads = alienHeads;
                items.lightFlux = lightFlux;
                items.collectorBuffs = 0;
                items.collectorBuffUntil = 0f;
                items.antlerBuffs = 0;
                items.antlerBuffUntil = 0f;
                items.ResetAntlerSpawnTimer();
                items.bolsteringLanterns = bolsteringLanterns;
                items.lanternBodies = 0;
                items.SyncInventoryDisplayStacks();
                if (items.owner is CharacterInstance speedOwner)
                {
                    speedOwner.ResearchMochas = mochas;
                    speedOwner.ResearchStoneFlux = stoneFlux;
                }
                items.SyncPassiveItemStats();
                items.teslaStartedAt = teslaStartedAt;
                items.nextTeslaPulse = nextTeslaPulse;
                if (items.owner is CharacterInstance shrimpOwner) shrimpOwner.ResearchPlasmaShrimps = plasmaShrimps;
                items.genesisReadyAt = genesisReadyAt;
                items.fruitCharges = fruitCharges;
                items.elephantCharges = elephantCharges;
                items.ocularHudCharges = ocularHudCharges;
                items.leechCharges = leechCharges;
                items.teamWarCryCharges = teamWarCryCharges;
                items.scannerCharges = scannerCharges;
                items.capacitorCharges = capacitorCharges;
                items.missileLauncherCharges = missileLauncherCharges;
                items.blastShowerCharges = blastShowerCharges;
                items.effigyCharges = effigyCharges;
                items.droneBackupCharges = droneBackupCharges;
            }
        }

        internal bool Equip(Equipment equipment)
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0 || equipment == Equipment.None) return false;
            Equipped = equipment;
            return true;
        }

        internal bool Remove(Equipment equipment)
        {
            if (!owner || !owner.isServer || equipment == Equipment.None || Equipped != equipment) return false;
            Equipped = Equipment.None;
            return true;
        }

        internal bool TryUseEquipment() => !EquipmentAutoActivates && ActivateEquipment();

        bool ActivateEquipment()
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0 || Equipped == Equipment.None || EquipmentCooldownRemaining > 0f)
                return false;

            switch (Equipped)
            {
                case Equipment.Fruit:
                    fruitCharges.Spend(45f * EquipmentRechargeScale);
                    owner.ServerHeal(Mathf.CeilToInt(owner.MaxHealth * .5f), owner.netId);
                    if (owner is CharacterInstance fruitOwner) fruitOwner.RpcRainForeignFruit();
                    break;
                case Equipment.Elephant:
                    elephantCharges.Spend(45f * EquipmentRechargeScale);
                    elephantArmorUntil = Time.time + 5f;
                    if (owner is CharacterInstance elephantOwner) elephantOwner.RpcRainJadeElephant();
                    break;
                case Equipment.OcularHud:
                    ocularHudCharges.Spend(60f * EquipmentRechargeScale);
                    guaranteedCritUntil = Time.time + 8f;
                    if (owner is CharacterInstance ocularOwner) ocularOwner.RpcRainOcularHud(true);
                    break;
                case Equipment.Leech:
                    leechCharges.Spend(60f * EquipmentRechargeScale);
                    leechUntil = Time.time + 8f;
                    leechDamageRemainder = 0;
                    break;
                case Equipment.TeamWarCry:
                    teamWarCryCharges.Spend(45f * EquipmentRechargeScale);
                    foreach (var ally in GameManager.HealthInstances.Values)
                    {
                        if (ally is not CharacterInstance member || !member || member.Team != owner.Team ||
                            member.CurrentHealth <= 0) continue;
                        if (!member.TryGetComponent<RangeResearchItems>(out var inventory))
                            inventory = member.gameObject.AddComponent<RangeResearchItems>();
                        inventory.ApplyFrenzy(member, 7f);
                    }
                    break;
                case Equipment.Scanner:
                    if (owner is not CharacterInstance scannerOwner ||
                        GameManager.Gamemode is not Gamemodes.RangeGamemode mode || !mode.IsRainCoop)
                        return false;
                    mode.ServerStartRainScanner(scannerOwner.CombatAimPoint);
                    scannerCharges.Spend(45f * EquipmentRechargeScale);
                    scannerOwner.RainScannerUntil = Mirror.NetworkTime.time + 10d;
                    break;
                case Equipment.Capacitor:
                    if (owner is not CharacterInstance capacitorOwner) return false;
                    var target = FindCapacitorTarget(capacitorOwner);
                    if (!target) return false;
                    capacitorCharges.Spend(20f * EquipmentRechargeScale);
                    StartCoroutine(StrikeCapacitor(capacitorOwner, target, capacitorCastGeneration));
                    break;
                case Equipment.DroneBackup:
                    if (owner is not CharacterInstance backupOwner || !RangeDrone.TryDeployBackup(backupOwner, FindRainInventoryOwner())) return false;
                    droneBackupCharges.Spend(100f * EquipmentRechargeScale);
                    break;
                case Equipment.Effigy:
                    if (owner is not CharacterInstance effigyOwner ||
                        GameManager.Gamemode is not Gamemodes.RangeGamemode effigyMode ||
                        !effigyMode.TryPlaceRainEffigy(effigyOwner, FindRainInventoryOwner())) return false;
                    effigyCharges.Spend(15f * EquipmentRechargeScale);
                    break;
                case Equipment.BlastShower:
                    blastShowerCharges.Spend(20f * EquipmentRechargeScale);
                    deathMarkUntil = 0f;
                    pulverizedUntil = 0f;
                    pulverizeHits = 0;
                    if (owner is CharacterInstance showerOwner) showerOwner.ClearRainDebuffs();
                    if (owner.TryGetComponent<RangeBleeding>(out var dots)) dots.ClearDebuffs();
                    Vector3 cleanseCenter = owner.transform.position + Vector3.up;
                    RangeResearchEnemy.ClearHostileProjectiles(owner, cleanseCenter, BlastShowerRadius);
                    if (owner is CharacterInstance showerVisualOwner)
                        showerVisualOwner.RpcRainBlastShower(cleanseCenter, BlastShowerRadius);
                    break;
                case Equipment.MissileLauncher:
                    if (owner is not CharacterInstance launcherOwner ||
                        !FindHomingTarget(launcherOwner.CombatAimPoint)) return false;
                    missileLauncherCharges.Spend(45f * EquipmentRechargeScale);
                    float attack = ItemBaseAttack;
                    StartCoroutine(LaunchMissileSalvo(launcherOwner, ScaleMissileDamage(3d * attack), MissileVolleySize));
                    break;
                default:
                    return false;
            }
            if (warHorns > 0 && owner is CharacterInstance character)
            {
                warHornUntil = Time.time + 4f * (1f + warHorns);
                character.ResearchWarHornActive = true;
                character.RpcRainWarHorn();
            }
            return true;
        }

        static CharacterInstance FindCapacitorTarget(CharacterInstance attacker)
        {
            Vector3 origin = attacker.CombatAimPoint;
            Vector3 forward = Quaternion.Euler(attacker.lookInput.x, attacker.lookInput.y, 0f) * Vector3.forward;
            CharacterInstance best = null;
            float bestAlignment = Mathf.Cos(8f * Mathf.Deg2Rad);
            foreach (var candidate in GameManager.HealthInstances.Values)
            {
                if (candidate is not CharacterInstance target || !target || target.Team == attacker.Team ||
                    target.Team < 0 || target.CurrentHealth <= 0) continue;
                Vector3 offset = target.CombatAimPoint - origin;
                float distance = offset.magnitude;
                if (distance <= .01f || distance > 60f) continue;
                float alignment = Vector3.Dot(forward, offset / distance);
                if (alignment <= bestAlignment || Physics.Linecast(origin, target.CombatAimPoint,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;
                best = target;
                bestAlignment = alignment;
            }
            return best;
        }

        int capacitorCastGeneration;

        IEnumerator StrikeCapacitor(CharacterInstance attacker, CharacterInstance target, int generation)
        {
            const float radius = 3f;
            Vector3 point = target.CombatAimPoint;
            int damage = Mathf.RoundToInt(30f * attacker.EffectiveRainAttackPower);
            int team = attacker.Team;
            float lockAt = Time.time + .2f;
            float impactAt = Time.time + .5f;
            while (Time.time < impactAt)
            {
                if (!isActiveAndEnabled || generation != capacitorCastGeneration ||
                    !attacker || !attacker.isServer || attacker.CurrentHealth <= 0 || attacker.Team != team) yield break;
                if (Time.time < lockAt && target && target.CurrentHealth > 0) point = target.CombatAimPoint;
                yield return null;
            }
            if (!isActiveAndEnabled || generation != capacitorCastGeneration ||
                !attacker || !attacker.isServer || attacker.CurrentHealth <= 0 || attacker.Team != team) yield break;

            attacker.RpcRainLightningStrike(point, royalCapacitor: true);
            var targets = new System.Collections.Generic.List<Health>();
            foreach (var candidate in GameManager.HealthInstances.Values)
            {
                if (!candidate || candidate.CurrentHealth <= 0 || candidate.Team < 0 || candidate.Team == team) continue;
                Vector3 aim = candidate is CharacterInstance character ? character.CombatAimPoint : candidate.transform.position;
                if ((aim - point).sqrMagnitude <= radius * radius) targets.Add(candidate);
            }
            foreach (var candidate in targets)
            {
                if (!candidate || candidate.CurrentHealth <= 0 || candidate.Team < 0 || candidate.Team == team) continue;
                candidate.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, attacker, 0);
                if (candidate && candidate.CurrentHealth > 0 &&
                    candidate.TryGetComponent<RangeResearchEnemy>(out var enemy)) enemy.ApplyStun();
            }
        }
        const float DangerDuration = 7f;

        internal void HealFromLeech(int damage)
        {
            if (Time.time >= leechUntil || owner.CurrentHealth <= 0) return;

            long totalDamage = (long)damage + leechDamageRemainder;
            int healing = (int)(totalDamage / 5);
            leechDamageRemainder = (int)(totalDamage % 5);
            if (healing > 0) owner.ServerHeal(healing, owner.netId);
        }

        public static int CatalogIndex(string id) => id switch
        {
            "FlatHealth" => 0, "CritGlasses" => 1, "Tooth" => 2,
            "ArmorPlate" => 8, "Medkit" => 9, "BossDamageBonus" => 10,
            "Crowbar" => 11, "Bear" => 12,
            "Hoof" => 13, "SprintBonus" => 14,
            "ParentEgg" => 15, "FragileDamageBonus" => 16, "FragileDamageBonusConsumed" => 17,
            "IncreaseHealing" => 18, "HealWhileSafe" => 19,
            "HealingPotion" => 20, "HealingPotionConsumed" => 21, "BarrierOnKill" => 22, "BarrierOnOverHeal" => 23, "BleedOnHit" => 24,
            "Syringe" => 25, "StickyBomb" => 26, "StunChanceOnHit" => 27, "Behemoth" => 28, "NearbyDamageBonus" => 29,
            "ExplodeOnDeath" => 30, "WardOnLevel" => 31, "ChainLightning" => 32, "IgniteOnKill" => 33,
            "IceRing" => 34, "FireRing" => 35, "PersonalShield" => 36, "SprintArmor" => 37, "OutOfCombatArmor" => 38,
            "SlowOnHit" => 39, "Seed" => 40, "Knurl" => KnurlIndex, "CritDamage" => CritDamageIndex,
            "HealOnCrit" => HealOnCritIndex,
            "SprintOutOfCombat" => RedWhipIndex,
            "AttackSpeedOnCrit" => PredatoryIndex,
            "ExtraLife" => ExtraLifeIndex,
            "ExtraLifeConsumed" => ExtraLifeConsumedIndex,
            "ExtraLifeVoid" => VoidExtraLifeIndex,
            "ExtraLifeVoidConsumed" => VoidExtraLifeConsumedIndex,
            "BearVoid" => SaferSpacesIndex,
            "MushroomVoid" => WeepingFungusIndex,
            "Mushroom" => BustlingFungusIndex,
            "BleedOnHitVoid" => NeedletickIndex,
            "ChainLightningVoid" => PolyluteIndex,
            "ExplodeOnDeathVoid" => VoidsentFlameIndex,
            "CritGlassesVoid" => LostSeersIndex,
            "EquipmentMagazine" => FuelCellIndex,
            "EnergizedOnEquipmentUse" => WarHornIndex,
            "AutoCastEquipment" => GestureIndex,
            "ArmorReductionOnHit" => ShatteringJusticeIndex,
            "Clover" => CloverIndex,
            "Infusion" => InfusionIndex,
            "MoveSpeedOnKill" => HarpoonIndex,
            "PermanentDebuffOnHit" => ScorpionIndex,
            "DeathMark" => DeathMarkIndex,
            "WarCryOnMultiKill" => BerserkersIndex,
            "SlowOnHitVoid" => TentabaubleIndex,
            "LightningStrikeOnHit" => ChargedPerforatorIndex,
            "BleedOnHitAndExplode" => ShatterspleenIndex,
            "NovaOnLowHealth" => GenesisLoopIndex,
            "NovaOnHeal" => NkuhanaIndex,
            "SprintWisp" => LittleDiscipleIndex,
            "Missile" => MissileIndex,
            "MissileVoid" => PlasmaShrimpIndex,
            "MoreMissile" => PocketIcbmIndex,
            "GoldOnHit" => BrittleCrownIndex,
            "BonusGoldPackOnKill" => GhorTomeIndex,
            "Pearl" => PearlIndex,
            "LunarDagger" => ShapedGlassIndex,
            "Talisman" => SoulboundCatalystIndex,
            "Dagger" => CeremonialDaggerIndex,
            "Thorns" => RazorwireIndex,
            "ShockNearby" => TeslaCoilIndex,
            "SiphonOnLowHealth" => MiredUrnIndex,
            "Icicle" => FrostRelicIndex,
            "Firework" => FireworkIndex,
            "FireballsOnHit" => MoltenPerforatorIndex,
            "Plant" => DeskPlantIndex,
            "StrengthenBurn" => IgnitionTankIndex,
            "GoldOnHurt" => RollOfPenniesIndex,
            "AttackSpeedAndMoveSpeed" => MochaIndex,
            "HalfSpeedDoubleHealth" => StoneFluxIndex,
            "IncreaseDamageOnMultiKill" => ChronicExpansionIndex,
            "LowerHealing" => HexOfFamineIndex,
            "HexCurse" => HexOfPainIndex,
            "CookedSteak" => CookedSteakIndex,
            "UltimateMeal" => UltimateMealIndex,
            "Stew" => StewIndex,
            "TPHealingNova" => LeptonDaisyIndex,
            "CritAtLowerElevation" => HikingBootsIndex,
            "BonusHealthBoost" => QuickFixIndex,
            "ScrapWhite" => WhiteScrapIndex,
            "AttackSpeedPerNearbyAllyOrEnemy" => BolsteringLanternIndex,
            "SpeedBoostPickup" => ElusiveAntlersIndex,
            "SpeedOnPickup" => CollectorIndex,
            "PowerOrbSphere" => PowerOrbIndex,
            "PowerCube" => PowerCubeIndex,
            "AlienHead" => AlienHeadIndex,
            "HalfAttackSpeedHalfCooldowns" => LightFluxIndex,
            _ => -1
        };

        internal bool TryConsumeExtraLife()
        {
            if (voidExtraLives > 0)
            {
                voidExtraLives--;
                ConsumedVoidExtraLives++;
                CorruptBears();
                CorruptFungus();
                CorruptDaggers();
                CorruptGlasses();
                if (owner is CharacterInstance character)
                {
                    CorruptUkuleles(character);
                    CorruptWisps(character);
                    CorruptBaubles(character);
                    CorruptMissiles(character);
                }
            }
            else
            {
                if (extraLives <= 0) return false;
                extraLives--;
                ConsumedExtraLives++;
            }
            extraLifeProtectedUntil = Time.time + 3f;
            RecordRainInventory();
            return true;
        }

        void RecordRainInventory()
        {
            var player = FindRainInventoryOwner();
            if (player) player.RecordRainChestItems(this);
        }

        PlayerInstance FindRainInventoryOwner()
        {
            if (GameManager.Gamemode is not Gamemodes.RangeGamemode mode || !mode.IsRainCoop) return null;
            var identity = owner.connectionToClient?.identity;
            return identity && identity.TryGetComponent<PlayerInstance>(out var player) && player.MyCharacter == owner
                ? player : null;
        }

        internal void TryEarnBrittleCrownGold(float procCoefficient)
        {
            if (brittleCrowns == 0) return;
            var player = FindRainInventoryOwner();
            if (player && RollWithLuck(.3f * procCoefficient))
                player.RainGold = (int)System.Math.Min(int.MaxValue, player.RainGold + 2L * brittleCrowns);
        }

        void CorruptBears()
        {
            saferSpaces += Bears;
            Bears = 0;
        }

        void CorruptFungus()
        {
            weepingFungus += bustlingFungus;
            bustlingFungus = 0;
            nextFungusPulse = 0f;
        }

        void CorruptDaggers()
        {
            needleticks += Daggers;
            Daggers = 0;
        }

        void CorruptGlasses()
        {
            lostSeersLenses += Glasses;
            Glasses = 0;
        }

        internal bool RollLostSeersExecution(Health victim, float procCoefficient)
        {
            if (lostSeersLenses <= 0 || procCoefficient <= 0f ||
                (victim.TryGetComponent<RangeResearchEnemy>(out var enemy) && enemy.IsBoss))
                return false;
            float chance = .005f * lostSeersLenses * procCoefficient;
            return chance >= 1f || RollWithLuck(chance);
        }

        void CorruptUkuleles(CharacterInstance character)
        {
            polylutes += character.ResearchUkuleles;
            character.ResearchUkuleles = 0;
        }

        void CorruptWisps(CharacterInstance character)
        {
            voidsentFlames += character.ResearchWisps;
            character.ResearchWisps = 0;
        }

        internal void QueueVoidsentFlame(Vector3 position)
        {
            if (voidsentFlames <= 0 || owner is not CharacterInstance) return;
            int extra = voidsentFlames - 1;
            float attack = ItemBaseAttack;
            StartCoroutine(ExplodeVoidsentFlame(position, 12f + 2.4f * extra,
                Mathf.RoundToInt(attack * (2.6f + 1.56f * extra))));
        }

        IEnumerator ExplodeVoidsentFlame(Vector3 position, float radius, int damage)
        {
            yield return null;
            if (owner is not CharacterInstance character || !character || !character.isServer) yield break;
            character.RpcRainVoidsentFlame(position, radius);
            ApplyItemBlast(position, radius, damage);
        }

        IEnumerator ExplodeNextFrame(Vector3 position, float radius, int damage, bool shatterspleen = false)
        {
            yield return null;
            if (!owner || !owner.isServer) yield break;
            if (shatterspleen && owner is CharacterInstance character)
                character.RpcRainShatterspleen(position, radius);
            ApplyItemBlast(position, radius, damage, sweetSpot: shatterspleen);
        }

        internal bool TryHikingBootsHit(Health victim, RangeProcSource ancestors)
        {
            if (hikingBoots <= 0 || owner is not CharacterInstance attacker || attacker.CurrentHealth <= 0) return false;
            if (Time.time >= hikingBuffUntil) hikingHits = 0;
            if (hikingHits > 0) hikingBuffUntil = Time.time + 10f;
            if ((ancestors & RangeProcSource.HikingBoots) != 0) return false;

            float attackerCenter = hikingController
                ? hikingController.transform.TransformPoint(hikingController.center).y : attacker.CombatAimPoint.y;
            float heightThreshold = hikingController
                ? hikingController.radius * Mathf.Max(Mathf.Abs(hikingController.transform.lossyScale.x),
                    Mathf.Abs(hikingController.transform.lossyScale.z)) : 1f;
            float targetCenter = victim is CharacterInstance character ? character.CombatAimPoint.y : victim.transform.position.y + 1f;
            if (victim.TryGetComponent<CharacterController>(out var targetController) && targetController.enabled)
                targetCenter = targetController.transform.TransformPoint(targetController.center).y;
            if (attackerCenter - targetCenter < heightThreshold) return false;
            hikingHits = Mathf.Min(hikingHits + 1, 10);
            hikingBuffUntil = Time.time + 10f;
            return true;
        }

        internal void OnCriticalHit(float procCoefficient)
        {
            if (predatoryStacks > 0 && procCoefficient > 0f && owner is CharacterInstance character && character.CurrentHealth > 0)
            {
                float now = Time.time;
                predatoryExpirations ??= new List<float>(5);
                RemoveExpiredPredatoryStacks(now);
                int activeStacks = predatoryExpirations.Count;
                float expiration = now + 3f * procCoefficient;
                if (activeStacks < 1 + 2 * predatoryStacks) predatoryExpirations.Add(expiration);
                else predatoryExpirations[FindEarliestPredatoryStack()] = expiration;
                character.ResearchPredatoryBuffs = predatoryExpirations.Count;
                character.RpcRainPredatoryProc(Mathf.Min(activeStacks, 2));
            }

            if (healOnCritStacks <= 0 || owner.CurrentHealth <= 0) return;
            if (owner.CurrentHealth >= owner.MaxHealth && Aegises == 0)
            {
                critHealingRemainder = 0f;
                return;
            }

            critHealingRemainder += (4f + 4f * healOnCritStacks) * procCoefficient;
            int healing = Mathf.FloorToInt(critHealingRemainder);
            if (healing <= 0) return;
            critHealingRemainder -= healing;
            if (owner.ServerHeal(healing, owner.netId) > 0 && owner is CharacterInstance healer)
                healer.RpcRainScytheHeal();
        }

        internal void TryHealFromSeed(float procCoefficient)
        {
            if (owner is not CharacterInstance character || character.ResearchSeeds <= 0 ||
                character.CurrentHealth <= 0) return;
            if (character.CurrentHealth >= character.MaxHealth && Aegises == 0)
            {
                seedHealingRemainder = 0;
                return;
            }

            seedHealingRemainder += character.ResearchSeeds * procCoefficient;
            int healing = Mathf.FloorToInt(seedHealingRemainder);
            if (healing <= 0) return;
            seedHealingRemainder -= healing;
            character.ServerHeal(healing, character.netId);
        }

        void CorruptBaubles(CharacterInstance character)
        {
            tentabaubles += character.ResearchBaubles;
            character.ResearchBaubles = 0;
        }

        internal void TryApplyBaubleSlow(Health victim, float procCoefficient)
        {
            if (owner is not CharacterInstance character ||
                victim is not CharacterInstance target || target.CurrentHealth <= 0) return;
            if (tentabaubles > 0)
            {
                float chance = .05f * tentabaubles * procCoefficient;
                if (chance >= 1f || RollWithLuck(chance))
                {
                    target.ResearchRootUntil = System.Math.Max(target.ResearchRootUntil, Mirror.NetworkTime.time + tentabaubles);
                    character.RpcRainTentabauble(target.transform.position);
                }
                return;
            }
            if (character.ResearchBaubles <= 0) return;
            double now = Mirror.NetworkTime.time;
            double expires = now + 2d * character.ResearchBaubles;
            if (target.ResearchBaubleSlowUntil <= now) character.RpcRainChronobauble(target.transform.position);
            target.ResearchBaubleSlowUntil = System.Math.Max(target.ResearchBaubleSlowUntil, expires);
        }

        internal void TryElementalRings(Health victim, Vector3 position, int hitDamage, RangeProcSource ancestors)
        {
            if (owner is not CharacterInstance character ||
                (character.ResearchIceRings <= 0 && character.ResearchFireRings <= 0) ||
                Time.time < elementalRingsReadyAt) return;
            float attack = ItemBaseAttack;
            if (hitDamage < 4f * attack) return;
            elementalRingsReadyAt = Time.time + 10f;
            int fireStacks = character.ResearchFireRings;
            if (fireStacks > 0)
            {
                Vector3 direction = position - character.transform.position;
                direction.y = 0f;
                direction.Normalize();
                int fireDamage = (int)System.Math.Min(int.MaxValue, hitDamage * 3d * fireStacks);
                character.RpcRainFireRing(position, direction);
                StartCoroutine(FireTornado(position, direction, fireDamage));
            }
            int iceStacks = character.ResearchIceRings;
            if (iceStacks <= 0 || !victim || victim.CurrentHealth <= 0) return;
            character.RpcRainIceRing(position);
            if (victim is CharacterInstance target)
                target.ResearchIceSlowUntil = System.Math.Max(target.ResearchIceSlowUntil, Mirror.NetworkTime.time + 3d * iceStacks);
            int iceDamage = (int)System.Math.Min(int.MaxValue, System.Math.Round(hitDamage * 2.5d * iceStacks));
            victim.ServerApplyRangeIceRing(iceDamage, owner, ancestors);
        }

        IEnumerator FireTornado(Vector3 position, Vector3 direction, int damage)
        {
            const int ticks = 6;
            var interval = new WaitForSeconds(.5f);
            for (int tick = 0; tick < ticks; tick++)
            {
                yield return interval;
                if (!owner || !owner.isServer) yield break;
                int tickDamage = damage / (ticks - tick);
                damage -= tickDamage;
                position += direction;
                ApplyItemBlast(position, 8f, tickDamage);
            }
        }

        internal void TryUkulele(Health victim, Vector3 position, int hitDamage, float procCoefficient, RangeProcSource ancestors)
        {
            if (owner is not CharacterInstance character ||
                (character.ResearchUkuleles <= 0 && polylutes <= 0)) return;
            float chance = .25f * procCoefficient;
            if (chance < 1f && !RollWithLuck(chance)) return;
            if (polylutes > 0)
            {
                int strikeDamage = Mathf.Max(1, (int)(((long)hitDamage * 3 + 2) / 5));
                StartCoroutine(StrikePolylute(victim, strikeDamage, 3L * polylutes, ancestors));
                return;
            }
            int stacks = character.ResearchUkuleles;
            int damage = Mathf.Max(1, (int)(((long)hitDamage * 4 + 2) / 5));
            StartCoroutine(JumpUkulele(victim.netId, position, damage, stacks, ancestors));
        }

        IEnumerator StrikePolylute(Health victim, int damage, long strikes, RangeProcSource ancestors)
        {
            var travel = new WaitForSeconds(.1f);
            for (long strike = 0; strike < strikes; strike++)
            {
                yield return travel;
                if (!owner || !owner.isServer || !victim || victim.CurrentHealth <= 0) yield break;
                if (owner is CharacterInstance character)
                    character.RpcRainPolyluteStrike(victim.transform.position + Vector3.up, strike == 0);
                victim.ServerApplyRangeChainLightning(damage, owner, ancestors);
            }
        }

        IEnumerator JumpUkulele(uint firstVictim, Vector3 position, int damage, int stacks, RangeProcSource ancestors)
        {
            var visited = new HashSet<uint> { firstVictim };
            float range = 20f + 2f * stacks;
            var travel = new WaitForSeconds(.1f);
            for (long jump = 0; jump < 1L + 2L * stacks; jump++)
            {
                if (!owner || !owner.isServer) yield break;
                Health nearest = null;
                float closest = range * range;
                foreach (var candidate in GameManager.HealthInstances.Values)
                {
                    if (!candidate || candidate.CurrentHealth <= 0 || candidate.Team < 0 ||
                        candidate.Team == owner.Team || visited.Contains(candidate.netId)) continue;
                    float distance = (candidate.transform.position - position).sqrMagnitude;
                    if (distance > closest) continue;
                    closest = distance;
                    nearest = candidate;
                }
                if (!nearest) yield break;
                visited.Add(nearest.netId);
                if (owner is CharacterInstance character)
                    character.RpcRainUkuleleArc(position + Vector3.up, nearest.transform.position + Vector3.up);
                position = nearest.transform.position;
                yield return travel;
                if (!owner || !owner.isServer) yield break;
                if (nearest)
                {
                    position = nearest.transform.position;
                    nearest.ServerApplyRangeChainLightning(damage, owner, ancestors);
                }
            }
        }

        internal int ApplyNearbyDamage(int damage, Vector3 impact)
        {
            if (owner is not CharacterInstance character || character.ResearchFocusCrystals <= 0 ||
                (character.CombatAimPoint - impact).sqrMagnitude > 169f) return damage;
            return Mathf.RoundToInt(Mathf.Min(int.MaxValue / 2, damage * (1f + .2f * character.ResearchFocusCrystals)));
        }

        internal void TryChargedPerforator(Health victim, int hitDamage, float procCoefficient)
        {
            if (chargedPerforators == 0 || victim.CurrentHealth <= 0 || owner is not CharacterInstance attacker)
                return;
            float chance = .1f * procCoefficient;
            if (chance < 1f && !RollWithLuck(chance)) return;
            int damage = (int)System.Math.Min(int.MaxValue, (long)hitDamage * System.Math.Min(5L * chargedPerforators, int.MaxValue));
            StartCoroutine(StrikeChargedPerforator(attacker, victim, damage));
        }

        internal void TryMoltenPerforator(Vector3 origin, int hitDamage, float procCoefficient, RangeProcSource ancestors)
        {
            if (moltenPerforators <= 0) return;
            float chance = .1f * procCoefficient;
            if (chance < 1f && !RollWithLuck(chance)) return;
            int damage = (int)System.Math.Min(int.MaxValue, hitDamage * (3d * moltenPerforators));
            float angle = Random.Range(0f, 360f);
            for (int shot = 0; shot < 3; shot++)
            {
                Vector3 lateral = Quaternion.Euler(0f, angle + shot * 120f, 0f) * Vector3.forward;
                StartCoroutine(ResolveMoltenBall(origin, lateral * 3f + Vector3.up * 5f, damage,
                    ancestors | RangeProcSource.MoltenPerforator));
            }
        }

        IEnumerator ResolveMoltenBall(Vector3 position, Vector3 velocity, int damage, RangeProcSource ancestors)
        {
            float expiresAt = Time.time + 1.4f;
            uint flightId = ++nextItemProjectileId;
            if (owner is CharacterInstance launcher)
                launcher.RpcRainItemProjectileFlight(flightId, position, velocity, null, true, molten: true);
            while (owner && owner.isServer && owner.CurrentHealth > 0 && Time.time < expiresAt)
            {
                yield return null;
                if (!owner || !owner.isServer || owner.CurrentHealth <= 0) yield break;
                float step = Time.deltaTime;
                velocity += Physics.gravity * step;
                Vector3 next = position + velocity * step;
                if (TryFindMoltenImpact(position, next, out var impact))
                {
                    position = impact;
                    break;
                }
                position = next;
            }
            if (owner && owner.isServer && owner.CurrentHealth > 0)
            {
                if (owner is CharacterInstance impactOwner)
                    impactOwner.RpcRainItemProjectileImpact(flightId, position);
                ApplyNearbyItemProc(position, 49f, long.MaxValue, damage, .7f, ancestors, tesla: false, moltenImpact: true);
            }
        }

        bool TryFindMoltenImpact(Vector3 origin, Vector3 next, out Vector3 impact)
        {
            impact = next;
            Vector3 travel = next - origin;
            float distance = travel.magnitude;
            if (distance <= .0001f) return false;

            const float ballRadius = .18f;
            Vector3 direction = travel / distance;
            moltenContacts ??= new RaycastHit[16];
            int count = Physics.SphereCastNonAlloc(origin, ballRadius, direction, moltenContacts, distance,
                GameManager.fireLayer, QueryTriggerInteraction.Collide);
            var contacts = moltenContacts;
            if (count == contacts.Length)
            {
                contacts = Physics.SphereCastAll(origin, ballRadius, direction, distance,
                    GameManager.fireLayer, QueryTriggerInteraction.Collide);
                count = contacts.Length;
            }

            bool collided = false;
            for (int i = 0; i < count; i++)
            {
                var contact = contacts[i];
                if (!contact.collider || contact.distance <= 0f || contact.distance > distance) continue;
                if (contact.collider.TryGetComponent<HitBox>(out var hitbox))
                {
                    var target = hitbox._health;
                    if (!target || target.CurrentHealth <= 0 || target.Team < 0 || target.Team == owner.Team) continue;
                }
                else if (contact.collider.isTrigger || contact.collider.gameObject.layer != 0) continue;

                collided = true;
                distance = contact.distance;
                impact = contact.point;
            }
            return collided;
        }

        void CorruptMissiles(CharacterInstance character)
        {
            character.ResearchPlasmaShrimps += missiles;
            missiles = 0;
            character.RefreshResearchShield();
        }

        internal void TryLaunchMissile(Health victim, int hitDamage, float procCoefficient, RangeProcSource ancestors)
        {
            if (victim.CurrentHealth <= 0 || owner is not CharacterInstance character) return;
            int shrimp = character.ResearchPlasmaShrimps;
            if (shrimp > 0)
            {
                if (character.ResearchShield <= 0) return;
            }
            else
            {
                if (missiles == 0) return;
                float chance = .1f * procCoefficient;
                if (chance < 1f && !RollWithLuck(chance)) return;
            }

            double multiplier = shrimp > 0 ? .4d + .5d * (shrimp - 1) : 3d * missiles;
            int damage = ScaleMissileDamage(hitDamage * multiplier);
            for (int shot = 0; shot < MissileVolleySize; shot++)
                StartCoroutine(ResolveHomingItemShot(character.CombatAimPoint, victim, damage,
                    ancestors | RangeProcSource.Missile, shrimp > 0 ? .2f : 1f, plasmaShrimp: shrimp > 0, atg: shrimp == 0));
        }

        int MissileVolleySize => pocketIcbms > 0 ? 3 : 1;

        int ScaleMissileDamage(double damage)
        {
            if (pocketIcbms > 0) damage *= 1d + .5d * (pocketIcbms - 1);
            return (int)System.Math.Min(int.MaxValue, System.Math.Round(damage));
        }

        IEnumerator LaunchMissileSalvo(CharacterInstance character, int damage, int volleySize)
        {
            var interval = new WaitForSeconds(.08f);
            for (int launch = 0; launch < 12; launch++)
            {
                if (!character || !character.isServer || character.CurrentHealth <= 0) yield break;
                Vector3 origin = character.CombatAimPoint;
                var target = FindHomingTarget(origin);
                if (target)
                    for (int shot = 0; shot < volleySize; shot++)
                        StartCoroutine(ResolveHomingItemShot(origin, target, damage, RangeProcSource.Missile, 1f));
                yield return interval;
            }
        }

        static IEnumerator StrikeChargedPerforator(CharacterInstance attacker, Health victim, int damage)
        {
            yield return null;
            if (!attacker || !attacker.isServer || !victim || victim.CurrentHealth <= 0) yield break;
            Vector3 point = victim is CharacterInstance target ? target.CombatAimPoint : victim.transform.position;
            attacker.RpcRainLightningStrike(point, royalCapacitor: false);
            victim.ServerApplyRangeItemBlast(damage, attacker);
        }

        internal void TryBehemothBlast(Vector3 position, int hitDamage, float procCoefficient)
        {
            if (owner is not CharacterInstance character || character.ResearchBehemoths <= 0) return;
            float radius = (1.5f + 2.5f * character.ResearchBehemoths) * procCoefficient;
            int damage = Mathf.Max(1, (int)(((long)hitDamage * 3 + 2) / 5));
            character.RpcRainBehemothBlast(position, radius);
            ApplyItemBlast(position, radius, damage);
        }

        internal void TryStunOnHit(Health victim, float procCoefficient)
        {
            if (owner is not CharacterInstance character || character.ResearchStunGrenades <= 0 ||
                !victim.TryGetComponent<RangeResearchEnemy>(out var enemy) || enemy.Archetype == 2) return;
            float chance = 1f - 1f / (1f + .05f * character.ResearchStunGrenades * procCoefficient);
            if (!RollWithLuck(chance) || victim.CurrentHealth <= 0) return;
            enemy.ApplyStun();
            character.RpcRainStunGrenade(victim.transform.position);
        }

        internal void TryAttachStickyBomb(Health victim, int hitDamage, float procCoefficient)
        {
            if (owner is not CharacterInstance character || character.ResearchStickyBombs <= 0) return;
            float chance = .05f * character.ResearchStickyBombs * procCoefficient;
            if (chance < 1f && !RollWithLuck(chance)) return;
            int damage = (int)System.Math.Min(int.MaxValue, ((long)hitDamage * 9 + 2) / 5);
            character.RpcRainStickyBomb(victim.transform.position, false);
            StartCoroutine(ExplodeStickyBomb(victim, damage));
        }

        IEnumerator ExplodeStickyBomb(Health victim, int damage)
        {
            Vector3 position = victim.transform.position;
            float detonateAt = Time.time + 1f;
            while (Time.time < detonateAt)
            {
                if (victim && victim.CurrentHealth > 0) position = victim.transform.position;
                yield return null;
            }
            if (!owner || !owner.isServer) yield break;
            if (victim && victim.CurrentHealth > 0) position = victim.transform.position;
            if (owner is CharacterInstance character) character.RpcRainStickyBomb(position, true);
            ApplyItemBlast(position, 4f, damage);
        }

        void ApplyItemBlast(Vector3 position, float radius, int damage, bool sweetSpot = false,
            int burnDamage = 0, int burnTicks = 0)
        {
            var targets = ListPool<Health>.Get();
            try
            {
                foreach (var target in GameManager.HealthInstances.Values)
                    if (target && target.CurrentHealth > 0 && target.Team != owner.Team &&
                        (target.transform.position - position).sqrMagnitude <= radius * radius)
                        targets.Add(target);
                foreach (var target in targets)
                    if (target)
                    {
                        if (burnDamage > 0)
                        {
                            if (!target.TryGetComponent<RangeBleeding>(out var dot))
                                dot = target.gameObject.AddComponent<RangeBleeding>();
                            dot.AddBurn(owner, burnDamage, burnTicks);
                        }
                        int applied = sweetSpot && (target.transform.position - position).sqrMagnitude > radius * radius * .25f
                            ? Mathf.Max(1, Mathf.RoundToInt(damage * .25f)) : damage;
                        target.ServerApplyRangeItemBlast(applied, owner);
                    }
            }
            finally { ListPool<Health>.Release(targets); }
        }

        internal void TryInflictBleed(Health victim, float procCoefficient, bool critical)
        {
            if (victim.CurrentHealth <= 0) return;
            int stacks = needleticks > 0 ? needleticks : Daggers;
            float chance = .1f * stacks * procCoefficient;
            bool daggerProc = stacks > 0 && (chance >= 1f || RollWithLuck(chance));
            bool spleenProc = shatterspleens > 0 && critical;
            if (!daggerProc && !spleenProc) return;
            if (!victim.TryGetComponent<RangeBleeding>(out var bleeding))
                bleeding = victim.gameObject.AddComponent<RangeBleeding>();
            float attack = ItemBaseAttack;
            if (daggerProc && needleticks > 0) bleeding.AddCollapse(owner, Mathf.RoundToInt(attack * 4f));
            if (spleenProc || (daggerProc && needleticks == 0))
                bleeding.Add(owner, attack, procCoefficient);
            if ((spleenProc || (daggerProc && needleticks == 0)) && owner is CharacterInstance character)
            {
                var center = victim is CharacterInstance target ? target.CombatAimPoint : victim.transform.position;
                if (spleenProc) character.RpcRainShatterspleen(center, 0f);
                else character.RpcRainDaggerBleed(center);
            }
        }

        internal void TryExplodeShatterspleen(Health victim, Vector3 position)
        {
            if (shatterspleens == 0 || owner.CurrentHealth <= 0 ||
                !victim.TryGetComponent<RangeBleeding>(out var bleeding) || !bleeding.HasBleed()) return;
            float attack = ItemBaseAttack;
            double damage = (4d * attack + .15d * victim.MaxHealth) * shatterspleens;
            if (CriticalChance > 0f && RollCritical()) damage *= CriticalMultiplier;
            StartCoroutine(ExplodeNextFrame(position, 16f, (int)System.Math.Min(int.MaxValue, System.Math.Round(damage)),
                shatterspleen: true));
        }

        public void ConvertOverheal(int excess)
        {
            if (Aegises > 0 && excess > 0) AddBarrier(excess * .5f * Aegises);
        }

        void AddBarrier(float amount)
        {
            if (owner.CurrentHealth > 0)
                Barrier = Mathf.Min(owner.MaxHealth, Barrier + amount);
        }

        public int AbsorbBarrier(int damage)
        {
            int absorbed = Mathf.Min(damage, Mathf.FloorToInt(Barrier));
            Barrier -= absorbed;
            return damage - absorbed;
        }

        public int ReduceIncomingDamage(int damage, float externalArmor = 0f)
        {
            if (Time.time < deathMarkUntil)
                damage = (int)System.Math.Min(int.MaxValue, ((long)damage * 3 + 1) / 2);
            float armor = externalArmor + (Time.time < elephantArmorUntil ? 500f : 0f);
            if (owner is CharacterInstance character)
            {
                if (character.IsRunning) armor += 30f * character.ResearchBucklers;
                if (character.ResearchOpals > 0)
                {
                    double now = Mirror.NetworkTime.time;
                    if (now >= character.ResearchOpalReadyAt)
                    {
                        armor += 100f * character.ResearchOpals;
                        character.RpcRainOpalImpact();
                    }
                    character.ResearchOpalReadyAt = now + DangerDuration;
                }
            }
            armor -= permanentArmorLoss;
            if (Time.time < pulverizedUntil) armor -= 60f;
            armor *= CollectorMultiplier;
            if (owner is CharacterInstance hero) armor *= hero.HeroArmorMultiplier;
            if (HasPowerCubeArmor) armor *= 1.5f;
            if (armor != 0f)
            {
                float scale = armor > 0f ? 100f / (100f + armor) : 2f - 100f / (100f - armor);
                damage = (int)System.Math.Min(int.MaxValue, System.Math.Round((double)damage * scale));
            }
            return Mathf.Max(1, damage - 5 * ArmorPlates);
        }

        internal void ApplyShatteringJustice(Health victim)
        {
            if (shatteringJustice == 0 || victim.CurrentHealth <= 0) return;
            if (!victim.TryGetComponent<RangeResearchItems>(out var items))
                items = victim.gameObject.AddComponent<RangeResearchItems>();
            if (Time.time < items.pulverizedUntil) return;
            if (++items.pulverizeHits < 5) return;
            items.pulverizeHits = 0;
            items.pulverizedUntil = Time.time + 8f * shatteringJustice;
        }

        internal void ApplySymbioticScorpion(Health victim, float procCoefficient)
        {
            if (scorpions == 0 || victim.CurrentHealth <= 0 ||
                (procCoefficient < 1f && !RollWithLuck(procCoefficient))) return;
            if (!victim.TryGetComponent<RangeResearchItems>(out var items))
                items = victim.gameObject.AddComponent<RangeResearchItems>();
            bool firstApplication = items.permanentArmorLoss == 0f;
            items.permanentArmorLoss += 2f * scorpions;
            if (firstApplication && owner is CharacterInstance scorpionOwner)
                scorpionOwner.RpcRainScorpionHit(victim.transform.position + Vector3.up);
        }

        internal void ApplyDeathMark(Health victim)
        {
            if (deathMarks == 0 || victim.CurrentHealth <= 0) return;
            victim.TryGetComponent<RangeResearchItems>(out var items);
            if (items && Time.time < items.deathMarkUntil) return;
            int count = victim.TryGetComponent<RangeBleeding>(out var dot) ? dot.ActiveDebuffCount() : 0;
            if (victim is CharacterInstance character)
            {
                double now = Mirror.NetworkTime.time;
                if (character.ResearchBaubleSlowUntil > now) count++;
                if (character.ResearchTarUntil > now) count++;
                if (character.ResearchIceSlowUntil > now) count++;
                if (character.ResearchRooted) count++;
            }
            if (items)
            {
                if (Time.time < items.pulverizedUntil) count++;
                if (items.permanentArmorLoss > 0f) count++;
            }
            if (count < 4) return;
            if (!items) items = victim.gameObject.AddComponent<RangeResearchItems>();
            items.deathMarkUntil = Time.time + 7f * deathMarks;
            if (owner is CharacterInstance source)
                source.RpcRainDeathMark(victim is CharacterInstance marked ? marked.CombatAimPoint : victim.transform.position);
        }

        public void BeforeHealthDamage()
        {
            if (ParentEggs > 0 && owner.ServerHeal(15 * ParentEggs, owner.netId) > 0 &&
                owner is CharacterInstance eggOwner)
                eggOwner.RpcRainParentEggHeal();
        }

        void SyncInventoryDisplayStacks()
        {
            if (owner is not CharacterInstance character) return;
            SyncAntlers();
            SyncCollector();
            character.ResearchHexOfPain = hexOfPain;
            character.ResearchHexOfFamine = hexOfFamine;
            character.ResearchCookedSteaks = cookedSteaks;
            character.ResearchUltimateMeals = ultimateMeals;
            SyncResearchStatItems(character);
            character.ResearchLeptonDaisies = leptonDaisies;
            character.ResearchClovers = clovers;
        }

        int ActiveSlugStacks => Time.time >= lastDamageAt + DangerDuration ? Slugs : 0;

        void SyncResearchStatItems(CharacterInstance character)
        {
            int active = ActiveSlugStacks;
            bool wasActive = character.ResearchActiveSlugs > 0;
            character.SetResearchStatItemStacks(stews, knurlStacks, active, hikingBoots, quickFixes, whiteScrap,
                bolsteringLanterns, lanternBodies);
            if (character.isServer && wasActive != (active > 0))
                character.RpcRainSlugState(active > 0);
        }

        // Match this source version's two additive CharacterBody attack-speed terms.
        internal static float LanternAttackSpeedBonus(int stacks, int nearby) => stacks > 0
            ? Mathf.Min(nearby, (int)System.Math.Min(int.MaxValue, 2L + stacks)) * (.165f + .035f * stacks) : 0f;

        internal void RefreshLanternBodies()
        {
            if (!owner || !owner.isServer) return;
            int count = 0;
            if (isActiveAndEnabled && bolsteringLanterns > 0 && owner.CurrentHealth > 0)
            {
                int cap = (int)System.Math.Min(int.MaxValue, 2L + bolsteringLanterns);
                Vector3 center = owner.transform.position + owner.centerPosition;
                foreach (var candidate in GameManager.HealthInstances.Values)
                {
                    if (candidate is not CharacterInstance body || !body || body == owner || body.CurrentHealth <= 0 ||
                        (body.transform.position - center).sqrMagnitude > 400f) continue;
                    if (++count >= cap) break;
                }
            }
            if (lanternBodies == count) return;
            lanternBodies = count;
            SyncInventoryDisplayStacks();
        }

        public int ScaleHealing(int amount)
        {
            if (amount <= 0) return amount;
            long healing = (long)amount * (1L + HealingRacks);
            if (hexOfFamine <= 0) return (int)System.Math.Min(int.MaxValue, healing);

            // Divide by 1 + 0.4 per stack and carry fractional healing across integer ticks.
            long divisor = 5L + 2L * hexOfFamine;
            long whole = healing / divisor;
            long fraction = 5L * (healing % divisor) + famineHealingRemainder;
            famineHealingRemainder = fraction % divisor;
            return (int)System.Math.Min(int.MaxValue, 5L * whole + fraction / divisor);
        }

        internal int RecalculateItemMaximum(int maximum)
        {
            int stoneFlux = owner is CharacterInstance character ? character.ResearchStoneFlux : 0;
            if (pearls == 0 && shapedGlass == 0 && stoneFlux == 0 && hexOfPain == 0 && cookedSteaks == 0 && quickFixes == 0 && collectorBuffs == 0 && appliedMaximumHealthAdjustment == 0) return maximum;
            long unscaled = System.Math.Max(1L, System.Math.Min(int.MaxValue, (long)maximum - appliedMaximumHealthAdjustment));
            double cookedHealth = 50d * cookedSteaks;
            double flatItemHealth = 25d * Steak + KnurlHealthPerStack * (double)knurlStacks + InfusionBonus + cookedHealth;
            double quickFixMultiplier = 1d + QuickFixBonus(quickFixes);
            double quickFixHealth = flatItemHealth * (quickFixMultiplier - 1d);
            double itemHealthMultiplier = 1d + (.1d * pearls + stoneFlux + .05d * cookedSteaks) * quickFixMultiplier;
            int scaled = (int)System.Math.Max(1d, System.Math.Min(int.MaxValue,
                System.Math.Round((unscaled + cookedHealth + quickFixHealth) * itemHealthMultiplier * CollectorMultiplier /
                    (System.Math.Pow(2d, shapedGlass) + .3d * hexOfPain))));
            appliedMaximumHealthAdjustment = (int)(scaled - unscaled);
            return scaled;
        }

        internal void StoreHealingSoul(int amount)
        {
            if (nkuhanaOpinions > 0 && amount > 0 && owner.CurrentHealth > 0)
                soulEnergy += (double)amount * nkuhanaOpinions;
        }

        void TryLaunchHealingSkull()
        {
            if (nkuhanaOpinions == 0 || soulEnergy < owner.MaxHealth * .1d || Time.time < nextSoulTargetCheck) return;
            nextSoulTargetCheck = Time.time + .15f;
            Vector3 origin = owner.transform.position + Vector3.up;
            var nearest = FindHomingTarget(origin);
            if (!nearest) return;
            int damage = (int)System.Math.Min(int.MaxValue, System.Math.Round(soulEnergy * 2.5d));
            soulEnergy = 0d;
            StartCoroutine(ResolveHomingItemShot(origin, nearest, damage));
        }

        void UpdateLittleDisciple()
        {
            if (littleDisciples == 0) return;
            if (owner is not CharacterInstance character || !character.IsRunning || !ownerMotor || ownerMotor.RunSpeed <= 0f)
            {
                discipleSprintCharge = 0f;
                return;
            }

            float speedRatio = ownerMotor.GetMoveSpeed(true) / ownerMotor.RunSpeed;
            if (speedRatio <= 0f) return;
            discipleSprintCharge = Mathf.Min(1.6f, discipleSprintCharge + speedRatio * Time.deltaTime);
            if (discipleSprintCharge < 1.6f || Time.time < nextDiscipleTargetCheck) return;
            nextDiscipleTargetCheck = Time.time + .15f;
            Vector3 origin = character.CombatAimPoint;
            var target = FindHomingTarget(origin);
            if (!target) return;
            float attack = ItemBaseAttack;
            discipleSprintCharge = 0f;
            StartCoroutine(ResolveHomingItemShot(origin, target, Mathf.RoundToInt(3f * attack * littleDisciples)));
        }

        internal void OnRainInteraction(int fireworkStacks)
        {
            if (fireworkStacks <= 0 || owner is not CharacterInstance character || !character.isServer ||
                character.CurrentHealth <= 0) return;
            bool critical = CriticalChance > 0f && RollCritical();
            double damage = 3d * ItemBaseAttack;
            if (critical) damage *= (float)CriticalMultiplier;
            StartCoroutine(LaunchFireworks(character, 4L + 4L * fireworkStacks,
                ScaleMissileDamage(damage), MissileVolleySize, critical));
        }

        IEnumerator LaunchFireworks(CharacterInstance character, long count, int damage, int volleySize, bool critical)
        {
            var interval = new WaitForSeconds(.1f);
            for (long i = 0; i < count && character && character.isServer && character.CurrentHealth > 0; i++)
            {
                Vector3 origin = character.CombatAimPoint;
                for (int shot = 0; shot < volleySize; shot++)
                    StartCoroutine(ResolveFirework(character, origin, damage, critical));
                yield return interval;
            }
        }

        IEnumerator ResolveFirework(CharacterInstance character, Vector3 position, int damage, bool critical)
        {
            float startedAt = Time.time;
            float nextTargetSearch = startedAt + .25f;
            Vector3 velocity = Vector3.up * 25f;
            uint flightId = ++nextItemProjectileId;
            character.RpcRainItemProjectileFlight(flightId, position, velocity, null, false, molten: false);
            Health target = null;
            bool falling = false;

            while (character && character.isServer && character.CurrentHealth > 0)
            {
                yield return null;
                if (!character || !character.isServer || character.CurrentHealth <= 0) yield break;
                float now = Time.time;
                float age = now - startedAt;
                if (age >= 16f) break;

                if (age >= 8f && !falling)
                {
                    falling = true;
                    target = null;
                    velocity.y = Mathf.Min(0f, velocity.y);
                    character.RpcRainItemProjectileFlight(flightId, position, velocity, null, true, molten: false);
                }
                if (!falling && now >= nextTargetSearch &&
                    (!target || target.CurrentHealth <= 0 || target.Team == character.Team))
                {
                    nextTargetSearch = now + .2f;
                    Health previousTarget = target;
                    target = FindHomingTarget(position);
                    if (!ReferenceEquals(previousTarget, target))
                        character.RpcRainItemProjectileFlight(flightId, position, velocity, target, false, molten: false);
                }

                float step = Time.deltaTime;
                Vector3 destination = position;
                bool homing = target && target.CurrentHealth > 0 && target.Team != character.Team;
                if (homing)
                {
                    destination = target.transform.position + Vector3.up;
                    velocity = (destination - position).normalized * 30f;
                }
                else if (falling)
                {
                    velocity += Physics.gravity * step;
                }
                Vector3 next = homing ? Vector3.MoveTowards(position, destination, 30f * step) : position + velocity * step;
                if (Physics.Linecast(position, next, out var hit, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                {
                    position = hit.point;
                    break;
                }
                position = next;
                if (homing && (next - destination).sqrMagnitude <= .01f) break;
            }

            if (character && character.isServer && character.CurrentHealth > 0)
            {
                character.RpcRainItemProjectileImpact(flightId, position);
                ApplyNearbyItemProc(position, 25f, long.MaxValue, damage, .2f, RangeProcSource.Firework, tesla: false, critical: critical);
            }
        }

        Health FindHomingTarget(Vector3 origin)
        {
            Health nearest = null;
            float nearestDistance = 40f * 40f;
            foreach (var target in GameManager.HealthInstances.Values)
            {
                if (!target || target.Team < 0 || target.Team == owner.Team || target.CurrentHealth <= 0) continue;
                Vector3 point = target.transform.position + Vector3.up;
                float distance = (point - origin).sqrMagnitude;
                if (distance >= nearestDistance ||
                    Physics.Linecast(origin, point, GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;
                nearest = target;
                nearestDistance = distance;
            }
            return nearest;
        }

        IEnumerator ResolveHomingItemShot(Vector3 position, Health target, int damage,
            RangeProcSource ancestors = RangeProcSource.None, float missileCoefficient = 0f, bool plasmaShrimp = false, bool atg = false)
        {
            RangeProcSource visual = (ancestors & RangeProcSource.Missile) != 0 ? RangeProcSource.Missile : ancestors;
            if (owner is CharacterInstance character)
            {
                character.RpcRainHomingItemShot(position, target, visual, plasmaShrimp);
                if (atg) character.RpcRainAtgAudio(position, false);
            }
            float expiresAt = Time.time + 4f;
            while (owner && owner.isServer && owner.CurrentHealth > 0 &&
                Time.time < expiresAt)
            {
                yield return null;
                if (!owner || owner.CurrentHealth <= 0 || Time.time >= expiresAt) yield break;
                if (!target || target.CurrentHealth <= 0)
                {
                    if (ancestors != RangeProcSource.Dagger) yield break;
                    target = FindHomingTarget(position);
                    if (!target) yield break;
                    if (owner is CharacterInstance daggerOwner)
                        daggerOwner.RpcRainHomingItemShot(position, target, visual, false);
                }

                Vector3 destination = target.transform.position + Vector3.up;
                Vector3 next = Vector3.MoveTowards(position, destination, 30f * Time.deltaTime);
                if (Physics.Linecast(position, next, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                    yield break;
                if ((next - destination).sqrMagnitude <= .01f)
                {
                    if ((ancestors & RangeProcSource.Missile) != 0)
                    {
                        if (atg && owner is CharacterInstance atgOwner) atgOwner.RpcRainAtgAudio(destination, true);
                        target.ServerApplyRangeMissile(damage, owner, ancestors, missileCoefficient);
                    }
                    else if (ancestors == RangeProcSource.Dagger)
                    {
                        if (owner is CharacterInstance daggerOwner)
                            daggerOwner.RpcPlayRainCeremonialDaggerEffect(destination, true);
                        target.ServerApplyRangeDagger(damage, owner);
                    }
                    else target.ServerApplyRangeItemBlast(damage, owner);
                    yield break;
                }
                position = next;
            }
        }

        internal void QueueRazorwire(Vector3 center, RangeProcSource ancestors)
        {
            if (razorwireStacks <= 0 || !owner || !owner.isServer || owner.CurrentHealth <= 0) return;
            float radius = 15f + 10f * razorwireStacks;
            StartCoroutine(ResolveRazorwire(center, radius * radius, 3L + 2L * razorwireStacks,
                Mathf.RoundToInt(1.6f * ItemBaseAttack), ancestors | RangeProcSource.Razorwire));
        }

        IEnumerator ResolveRazorwire(Vector3 center, float radiusSquared, long limit, int damage, RangeProcSource ancestors)
        {
            yield return null;
            if (owner && owner.isServer && owner.CurrentHealth > 0)
                ApplyNearbyItemProc(center, radiusSquared, limit, damage, .5f, ancestors, tesla: false);
        }

        void UpdateTeslaCoil()
        {
            float now = Time.time;
            if (teslaCoils <= 0 || now < nextTeslaPulse || (now - teslaStartedAt) % 20f >= 10f) return;
            nextTeslaPulse = now + .5f;
            ApplyNearbyItemProc(owner.transform.position, 35f * 35f, 1L + 2L * teslaCoils,
                Mathf.RoundToInt(2f * ItemBaseAttack), .3f, RangeProcSource.Tesla, tesla: true);
        }

        void UpdateFrostRelic()
        {
            if (frostRelics <= 0 || frostKillExpiries == null || Time.time < nextFrostPulse) return;
            while (frostKillExpiries.Count > 0 && frostKillExpiries.Peek() <= Time.time)
                frostKillExpiries.Dequeue();
            if (frostKillExpiries.Count == 0)
            {
                SetFrostRelicRadius(0f);
                return;
            }

            nextFrostPulse = Time.time + .25f;
            float radius = Mathf.Min(6f + 2f * frostKillExpiries.Count, 6f + 12f * frostRelics);
            SetFrostRelicRadius(radius);
            ApplyNearbyItemProc(owner.transform.position, radius * radius, long.MaxValue,
                Mathf.RoundToInt(3f * ItemBaseAttack), .2f, RangeProcSource.FrostRelic, tesla: false);
        }

        void SetFrostRelicRadius(float radius)
        {
            if (owner is CharacterInstance character && character.ResearchFrostRelicRadius != radius)
                character.ResearchFrostRelicRadius = radius;
        }

        void UpdateMiredUrn()
        {
            if (miredUrns <= 0 || Time.time < nextUrnPulse || owner is not CharacterInstance character ||
                Mirror.NetworkTime.time >= character.ResearchRedWhipReadyAt) return;

            nextUrnPulse = Time.time + 1f;
            float damage = ItemBaseAttack;
            if (CriticalChance > 0f && RollCritical()) damage *= (float)CriticalMultiplier;
            ApplyNearbyItemProc(owner.transform.position, 13f * 13f, miredUrns, Mathf.RoundToInt(damage),
                0f, RangeProcSource.Urn, tesla: false);
        }

        void ApplyNearbyItemProc(Vector3 center, float radiusSquared, long limit, int damage, float coefficient,
            RangeProcSource ancestors, bool tesla, bool critical = false, bool moltenImpact = false)
        {
            var targets = ListPool<(Health target, float distance)>.Get();
            bool urn = ancestors == RangeProcSource.Urn;
            var endpoints = urn || ancestors == RangeProcSource.FrostRelic ? null : ListPool<Vector3>.Get();
            var tetherTargets = urn ? ListPool<uint>.Get() : null;
            try
            {
                foreach (var target in GameManager.HealthInstances.Values)
                {
                    if (!target || target.Team < 0 || target.Team == owner.Team || target.CurrentHealth <= 0) continue;
                    float distance = (target.transform.position - center).sqrMagnitude;
                    if (distance <= radiusSquared) targets.Add((target, distance));
                }
                targets.Sort(static (a, b) => a.distance.CompareTo(b.distance));
                for (int i = 0; i < targets.Count && limit > 0 && owner && owner.CurrentHealth > 0; i++)
                {
                    var target = targets[i].target;
                    if (!target || target.CurrentHealth <= 0 || target.Team == owner.Team) continue;
                    // Bound the cosmetic payload independently from the gameplay target limit.
                    if (urn)
                    {
                        if (tetherTargets.Count < 32) tetherTargets.Add(target.netId);
                    }
                    else if (endpoints != null && endpoints.Count < 32) endpoints.Add(target.transform.position + Vector3.up);
                    target.ServerApplyRangeAreaProc(damage, owner, coefficient, ancestors, critical, moltenImpact);
                    limit--;
                }
                if (owner is CharacterInstance character && character)
                {
                    if (urn && tetherTargets.Count > 0) character.RpcRainUrnTethers(tetherTargets);
                    else if (endpoints != null && endpoints.Count > 0) character.RpcRainAreaProc(center + Vector3.up, endpoints, tesla);
                }
            }
            finally
            {
                if (urn) ListPool<uint>.Release(tetherTargets);
                else if (endpoints != null) ListPool<Vector3>.Release(endpoints);
                ListPool<(Health target, float distance)>.Release(targets);
            }
        }

        public void OnHealthDamaged(int healthDamage)
        {
            if (brittleCrowns > 0)
            {
                var player = FindRainInventoryOwner();
                if (player && player.RainGold > 0)
                {
                    double fraction = (double)healthDamage / owner.MaxHealth * brittleCrowns;
                    player.RainGold -= (int)(player.RainGold * System.Math.Min(1d, fraction));
                }
            }
            lastDamageAt = Time.time;
            nextSlugHealAt = lastDamageAt + DangerDuration + 1f;
            if (Slugs > 0 && owner is CharacterInstance character) SyncResearchStatItems(character);

            if (Medkits > 0 && owner.CurrentHealth > 0)
                medkitReadyAt = Time.time + 2f;
        }

        public void ResolveLowHealthItems()
        {
            if (owner.CurrentHealth > owner.MaxHealth * .25f) return;
            if (genesisLoops > 0 && owner.CurrentHealth > 0 && owner.CurrentHealth < owner.MaxHealth * .25f &&
                Time.time >= genesisReadyAt && owner is CharacterInstance)
            {
                genesisReadyAt = Time.time + Mathf.Max(.01f, 30f * Mathf.Pow(.5f, genesisLoops - 1));
                float attack = ItemBaseAttack;
                StartCoroutine(ExplodeNextFrame(owner.transform.position, 25f, Mathf.RoundToInt(60f * attack)));
                RecordRainInventory();
            }
            if (HealingPotions > 0 && owner.CurrentHealth > 0)
            {
                HealingPotions--;
                EmptyBottles++;
                owner.ServerHeal(Mathf.RoundToInt(owner.MaxHealth * .75f), owner.netId);
                if (owner is CharacterInstance potionOwner) potionOwner.RpcRainHealingPotion();
            }
            if (Watches > 0 && owner.CurrentHealth <= owner.MaxHealth * .25f)
            {
                BrokenWatches += Watches;
                Watches = 0;
                if (owner is CharacterInstance watchOwner) watchOwner.RpcRainWatchBreak();
            }
        }

        void Update()
        {
            if (!owner.isServer) return;
            UpdateCollectorBuff();
            RefreshLanternBodies();
            if (EquipmentAutoActivates && ActivateEquipment()) RecordRainInventory();
            if (owner is CharacterInstance hornOwner && hornOwner.ResearchWarHornActive &&
                (owner.CurrentHealth <= 0 || Time.time >= warHornUntil))
                hornOwner.ResearchWarHornActive = false;
            if (owner is CharacterInstance frenzyOwner && frenzyOwner.ResearchBerserkerFrenzy &&
                (owner.CurrentHealth <= 0 || Time.time >= frenzyUntil))
                frenzyOwner.ResearchBerserkerFrenzy = false;
            if (owner is CharacterInstance character && predatoryExpirations is { Count: > 0 })
            {
                if (owner.CurrentHealth <= 0) predatoryExpirations.Clear();
                else RemoveExpiredPredatoryStacks(Time.time);
                character.ResearchPredatoryBuffs = predatoryExpirations.Count;
            }
            if (owner.CurrentHealth <= 0)
            {
                famineHealingRemainder = 0;
                hikingHits = 0;
                chronicKills = 0;
                chronicUntil = 0f;
                soulEnergy = 0d;
                discipleSprintCharge = 0f;
                frostKillExpiries?.Clear();
                SetFrostRelicRadius(0f);
                pulverizeHits = 0;
                permanentArmorLoss = 0f;
                deathMarkUntil = 0f;
                if (owner is CharacterInstance deadCharacter) deadCharacter.ResearchHarpoonUntil = 0d;
                if (owner is CharacterInstance scanCharacter) scanCharacter.RainScannerUntil = 0d;
                frenzyUntil = 0f;
                lastKillAt = secondLastKillAt = thirdLastKillAt = float.NegativeInfinity;
                pulverizedUntil = 0f;
                medkitReadyAt = 0;
                Barrier = 0f;
                knurlHealingRemainder = 0f;
                weepingHealingRemainder = 0f;
                weepingHealingActive = false;
                bucklerFeedbackActive = false;
                nextFungusPulse = 0f;
                critHealingRemainder = 0f;
                elephantArmorUntil = 0f;
                guaranteedCritUntil = 0f;
                leechUntil = 0f;
                leechDamageRemainder = 0;
                return;
            }
            TryLaunchHealingSkull();
            UpdateLittleDisciple();
            UpdateTeslaCoil();
            UpdateMiredUrn();
            UpdateFrostRelic();
            if (Barrier > 0f)
            {
                float maxBarrier = owner.MaxHealth;
                float decay = maxBarrier / 30f * Mathf.Lerp(.5f, 3f, Barrier / maxBarrier);
                Barrier = Mathf.Max(0f, Mathf.Min(Barrier, maxBarrier) - decay * Time.deltaTime);
            }
            if (medkitReadyAt > 0 && Time.time >= medkitReadyAt)
            {
                medkitReadyAt = 0;
                if (Medkits > 0)
                {
                    owner.ServerHeal(20 + Mathf.RoundToInt(owner.MaxHealth * .05f * Medkits), owner.netId);
                    if (owner is CharacterInstance medkitOwner) medkitOwner.RpcRainMedkit();
                }
            }
            if (owner is CharacterInstance regenOwner)
            {
                if (Slugs > 0 && regenOwner.ResearchActiveSlugs != ActiveSlugStacks) SyncResearchStatItems(regenOwner);
            }
            else if (Slugs > 0 && Time.time >= nextSlugHealAt)
            {
                nextSlugHealAt = Time.time + 1f;
                owner.ServerHeal(3 * Slugs, owner.netId, allowOverhealBarrier: false);
            }
            if (guaranteedCritUntil > 0f && Time.time >= guaranteedCritUntil)
            {
                guaranteedCritUntil = 0f;
                if (owner is CharacterInstance ocularOwner) ocularOwner.RpcRainOcularHud(false);
            }
            UpdateBucklerFeedback();
            UpdateWeepingFungus();
            UpdateBustlingFungus();
            if (owner is CharacterInstance || knurlStacks == 0 || owner.CurrentHealth >= owner.MaxHealth)
            {
                knurlHealingRemainder = 0f;
                return;
            }

            knurlHealingRemainder += KnurlRegenerationPerStack * knurlStacks * Time.deltaTime;
            int healing = Mathf.FloorToInt(knurlHealingRemainder);
            if (healing <= 0) return;
            knurlHealingRemainder -= healing;
            owner.ServerHeal(healing, owner.netId, allowOverhealBarrier: false);
        }

        void UpdateBucklerFeedback()
        {
            if (owner is not CharacterInstance character) return;
            bool active = character.CurrentHealth > 0 && character.IsRunning && character.ResearchBucklers > 0;
            if (active && !bucklerFeedbackActive) character.RpcRainBucklerSprint();
            bucklerFeedbackActive = active;
        }

        void UpdateWeepingFungus()
        {
            if (weepingFungus == 0) return;
            if (owner is not CharacterInstance character || !character.IsRunning || owner.CurrentHealth >= owner.MaxHealth)
            {
                weepingHealingRemainder = 0f;
                weepingHealingActive = false;
                return;
            }

            weepingHealingRemainder += owner.MaxHealth * .02f * weepingFungus * Time.deltaTime;
            int healing = Mathf.FloorToInt(weepingHealingRemainder);
            if (healing <= 0) return;
            weepingHealingRemainder -= healing;
            if (!weepingHealingActive)
            {
                weepingHealingActive = true;
                character.RpcRainWeepingFungus();
            }
            owner.ServerHeal(healing, owner.netId, allowOverhealBarrier: false);
        }

        void UpdateBustlingFungus()
        {
            if (bustlingFungus == 0 || owner is not CharacterInstance character) return;

            Vector3 position = character.transform.position;
            if (nextFungusPulse == 0f || !character.IsGrounded || character.IsRunning ||
                character.movementInput.sqrMagnitude > .01f || (position - fungusAnchor).sqrMagnitude > .0025f)
            {
                bustlingFungusActive = false;
                fungusAnchor = position;
                nextFungusPulse = Time.time + 1f;
                return;
            }
            if (Time.time < nextFungusPulse) return;
            nextFungusPulse = Time.time + 1f;

            float radius = 1.5f * (1f + bustlingFungus);
            character.RpcRainBustlingFungus(position, radius, !bustlingFungusActive);
            bustlingFungusActive = true;
            int healing = Mathf.Max(1, Mathf.RoundToInt(owner.MaxHealth * .0225f * (1f + bustlingFungus)));
            foreach (var identity in Mirror.NetworkServer.spawned.Values)
            {
                if (identity && identity.TryGetComponent<Health>(out var ally) &&
                    ally.Team == owner.Team && ally.CurrentHealth > 0 &&
                    (ally.transform.position - position).sqrMagnitude <= radius * radius)
                    ally.ServerHeal(healing, owner.netId);
            }
        }

        public float MoveSpeed(float walkSpeed, float runSpeed, bool sprinting, float bannerBonus = 0f)
        {
            // The sprint bonus is an addition based on walking speed in the reference formula.
            float speed = sprinting ? runSpeed : walkSpeed;
            var character = owner as CharacterInstance;
            float redWhipBonus = character ? character.ResearchRedWhipMoveBonus : 0f;
            float harpoonBonus = character ? character.ResearchHarpoonMoveBonus : 0f;
            float frenzyBonus = Time.time < frenzyUntil ? .5f : 0f;
            float mochaBonus = character ? character.ResearchMochaMoveBonus : 0f;
            return speed * (1f + .14f * Hooves + bannerBonus + redWhipBonus + harpoonBonus + frenzyBonus + mochaBonus + (character ? character.ResearchAntlerMoveBonus : 0f)) +
                (sprinting ? walkSpeed * .25f * EnergyDrinks : 0f);
        }

        internal void RecordCombat()
        {
            if (owner is CharacterInstance character)
                character.ResearchRedWhipReadyAt = Mirror.NetworkTime.time + RedWhipCombatDuration;
        }

        public bool RollDamageBlock()
        {
            if (saferSpaces > 0)
            {
                if (Time.time < saferSpacesReadyAt) return false;
                saferSpacesReadyAt = Time.time + 15f * Mathf.Pow(.9f, saferSpaces - 1);
                if (owner is CharacterInstance defender) defender.RpcRainSaferSpacesBlock();
                RecordRainInventory();
                return true;
            }
            float amplification = .15f * Bears;
            if (Bears <= 0 || Random.value >= amplification / (1f + amplification)) return false;
            if (owner is CharacterInstance character) character.RpcRainBearBlock();
            return true;
        }

        public int ApplyCrowbarDamage(int damage, Health victim)
        {
            if (Crowbars == 0) return damage;
            double current = victim.CurrentHealth;
            double capacity = victim.MaxHealth;
            if (victim.TryGetComponent<RangeResearchItems>(out var defender)) current += defender.Barrier;
            var target = victim as CharacterInstance;
            if (target)
            {
                current += target.ResearchShield;
                capacity += target.ResearchShieldCapacity;
            }
            if (current < capacity * .9d) return damage;
            if (owner is CharacterInstance source)
                source.RpcRainCrowbarImpact(target ? target.CombatAimPoint : victim.transform.position);
            return Mathf.RoundToInt(Mathf.Min(int.MaxValue / 2, damage * (1f + .75f * Crowbars)));
        }

        public int ApplyBossDamage(int damage, Health victim)
        {
            if (ArmorPiercingRounds == 0 || !victim.TryGetComponent<RangeResearchEnemy>(out var enemy) || !enemy.IsBoss)
                return damage;
            return Mathf.RoundToInt(Mathf.Min(int.MaxValue / 2, damage * (1f + .2f * ArmorPiercingRounds)));
        }
        Health owner;
        void Awake()
        {
            owner = GetComponent<Health>();
            TryGetComponent(out hikingController);
            if (owner) owner.Server_OnDamaged += EarnGoldOnDamage;
            if (TryGetComponent(out ownerMotor)) ownerMotor.RangeItems = this;
        }
        // Disabling the behavior removes its nearby-body buff, but preserves owned item stacks.
        void OnEnable()
        {
            RefreshLanternBodies();
            ResetAntlerSpawnTimer();
            SyncPassiveItemStats();
        }
        void OnDisable()
        {
            capacitorCastGeneration++;
            if (owner is CharacterInstance character && owner.isServer)
            {
                predatoryExpirations?.Clear();
                character.ResearchPredatoryBuffs = 0;
            }
            RefreshLanternBodies();
            SyncPassiveItemStats();
            if (owner && owner.isServer) ClearAntlerBuff();
            if (owner && owner.isServer) ClearCollectorBuff();
        }

        void RemoveExpiredPredatoryStacks(float now)
        {
            for (int i = predatoryExpirations.Count - 1; i >= 0; i--)
                if (predatoryExpirations[i] <= now) predatoryExpirations.RemoveAt(i);
        }

        int FindEarliestPredatoryStack()
        {
            int earliest = 0;
            for (int i = 1; i < predatoryExpirations.Count; i++)
                if (predatoryExpirations[i] < predatoryExpirations[earliest]) earliest = i;
            return earliest;
        }

        void SyncPassiveItemStats()
        {
            if (owner is CharacterInstance character)
                character.SetResearchPassiveItemStats(isActiveAndEnabled && powerOrbs > 0,
                    isActiveAndEnabled ? alienHeads : 0, isActiveAndEnabled ? lightFlux : 0);
        }

        void OnDestroy()
        {
            ServerPickup = null;
            if (owner && owner.isServer)
            {
                ClearCollectorBuff();
                if (owner is CharacterInstance collectorOwner) collectorOwner.SetResearchCollectorStacks(0, 0);
            }
            if (owner && owner.isServer && owner is CharacterInstance antlerOwner)
                antlerOwner.SetResearchAntlerStacks(0, 0);
            if (owner && owner.isServer && owner is CharacterInstance character)
                character.SetResearchStatItemStacks(character.ResearchStews, character.ResearchKnurls,
                    character.ResearchActiveSlugs, character.ResearchHikingBoots, character.ResearchQuickFixes,
                    character.ResearchWhiteScrap, 0, 0);
            if (owner) owner.Server_OnDamaged -= EarnGoldOnDamage;
        }

        void EarnGoldOnDamage(int health, CharacterPart part, AttackType type, Health attacker, int force)
        {
            if (rollsOfPennies <= 0 || !owner.isServer || !attacker || attacker.Team == owner.Team ||
                type == AttackType.falldamage || GameManager.Gamemode is not Gamemodes.RangeGamemode mode) return;
            var player = FindRainInventoryOwner();
            if (!player) return;
            double reward = System.Math.Floor(3d * rollsOfPennies * mode.RainDifficultyCoefficient());
            player.RainGold = (int)System.Math.Min(int.MaxValue, player.RainGold + reward);
        }

        internal bool TryUseWhiteItemStation(string station)
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0 || owner is not CharacterInstance) return false;
            if (station == "ScrapBisonSteak")
            {
                return TryScrapWhiteItem(CatalogIndex("FlatHealth")) > 0;
            }
            if (station != "PrintSyringe") return false;
            if (whiteScrap > 0)
            {
                whiteScrap--;
                SyncInventoryDisplayStacks();
            }
            else
            {
                int payment = SelectWhitePrinterPayment(Random.value);
                if (payment < 0 || !Remove(payment)) return false;
            }
            Grant(CatalogIndex("Syringe"));
            return true;
        }

        internal int TryScrapWhiteItem(int item)
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0) return 0;
            // The validated count accepts only integrated, removable Tier1 items.
            // Every removal follows its existing stat, shield and callback cleanup path.
            int count = Mathf.Min(10, WhitePrinterPaymentCount(item));
            int removed = 0;
            while (removed < count && Remove(item))
            {
                removed++;
                Grant(WhiteScrapIndex);
            }
            return removed;
        }

        // Integrated Tier1 items only. Scrap has priority in the transaction above;
        // the printer's own output is used only when no other eligible payment exists.
        static readonly int[] WhitePrinterPayments =
        {
            0, 1, 2, 8, 9, 10, 11, 12, 13, 14, 16, 19, 20, 22, 24,
            26, 27, 29, 31, 33, 36, 38, BustlingFungusIndex, FireworkIndex,
            RollOfPenniesIndex, MochaIndex, ChronicExpansionIndex, HikingBootsIndex, BolsteringLanternIndex, ElusiveAntlersIndex
        };

        internal static int WhiteScrappableTypeCount => WhitePrinterPayments.Length + 1;
        internal static int WhiteScrappableTypeAt(int index) =>
            index == WhitePrinterPayments.Length ? 25 : WhitePrinterPayments[index];

        internal int WhitePrinterPaymentCount(int item)
        {
            if (owner is not CharacterInstance character) return 0;
            return item switch
            {
                0 => Steak, 1 => Glasses, 2 => Tooth, 8 => ArmorPlates, 9 => Medkits,
                10 => ArmorPiercingRounds, 11 => Crowbars, 12 => Bears, 13 => Hooves,
                14 => EnergyDrinks, 16 => Watches, 19 => Slugs, 20 => HealingPotions,
                22 => Brooches, 24 => Daggers, 25 => character.ResearchSyringes,
                26 => character.ResearchStickyBombs, 27 => character.ResearchStunGrenades,
                29 => character.ResearchFocusCrystals, 31 => character.ResearchWarbanners,
                33 => character.ResearchGasoline, 36 => character.ResearchShieldGenerators,
                38 => character.ResearchOpals, BustlingFungusIndex => bustlingFungus,
                FireworkIndex => FireworkStacks, RollOfPenniesIndex => rollsOfPennies,
                MochaIndex => character.ResearchMochas, ChronicExpansionIndex => chronicExpansions,
                HikingBootsIndex => hikingBoots,
                BolsteringLanternIndex => bolsteringLanterns, ElusiveAntlersIndex => elusiveAntlers,
                _ => 0
            };
        }

        internal int SelectWhitePrinterPayment(float sample)
        {
            long total = 0;
            foreach (int item in WhitePrinterPayments) total += WhitePrinterPaymentCount(item);
            if (total == 0) return WhitePrinterPaymentCount(25) > 0 ? 25 : -1;
            // A long accumulator keeps large legitimate stacks from overflowing the lottery.
            double remaining = Mathf.Clamp01(sample) * (double)total;
            int last = -1;
            foreach (int item in WhitePrinterPayments)
            {
                int count = WhitePrinterPaymentCount(item);
                if (count <= 0) continue;
                last = item;
                if (remaining < count) return item;
                remaining -= count;
            }
            return last; // Random.value can return exactly 1.
        }

        internal bool TryCookMeal(string recipe)
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0) return false;
            switch (recipe)
            {
                case "BonusHealthBoost" when whiteScrap > 0 && owner is CharacterInstance mochaOwner && mochaOwner.ResearchMochas > 0:
                    whiteScrap--;
                    mochaOwner.ResearchMochas--;
                    quickFixes++;
                    SyncInventoryDisplayStacks();
                    owner.ServerSetMaximumHealth(owner.MaxHealth);
                    return true;
                case "Stew" when leptonDaisies > 0 && owner is CharacterInstance seedOwner && seedOwner.ResearchSeeds > 0:
                    if (--seedOwner.ResearchSeeds == 0) seedHealingRemainder = 0f;
                    return CookStew(1);
                case "StewDoubleDaisy" when leptonDaisies >= 2:
                    return CookStew(2);
                case "CookedSteak" when Steak >= 2:
                    return CookSteak(2);
                case "CookedSteakMocha" when Steak > 0 && owner is CharacterInstance character && character.ResearchMochas > 0:
                    character.ResearchMochas--;
                    return CookSteak(1);
                case "UltimateMeal" when clovers >= 2:
                    clovers -= 2;
                    break;
                case "UltimateMealFrostRelic" when clovers > 0 && frostRelics > 0:
                    RemoveFrostRelicStack();
                    clovers--;
                    break;
                default:
                    return false;
            }
            ultimateMeals++;
            SyncInventoryDisplayStacks();
            return true;
        }

        bool CookStew(int daisies)
        {
            leptonDaisies -= daisies;
            stews++;
            SyncInventoryDisplayStacks();
            return true;
        }

        bool CookSteak(int rawSteaks)
        {
            Steak -= rawSteaks;
            cookedSteaks++;
            SyncInventoryDisplayStacks();
            owner.ServerSetMaximumHealth(owner.MaxHealth - 25 * rawSteaks);
            return true;
        }

        public void Grant(int item)
        {
            if(!owner || !owner.isServer || owner.CurrentHealth<=0)return;
            if ((item is AlienHeadIndex or LightFluxIndex) && owner is CharacterInstance)
            {
                if (item == AlienHeadIndex) alienHeads++;
                else lightFlux++;
                SyncPassiveItemStats();
                RecordRainInventory();
                return;
            }
            if (item == PowerCubeIndex)
            {
                powerCubes++;
                RecordRainInventory();
                return;
            }
            if (item == PowerOrbIndex && owner is CharacterInstance)
            {
                powerOrbs++;
                SyncPassiveItemStats();
                RecordRainInventory();
                return;
            }
            if (item == CollectorIndex)
            {
                collectors++;
                SyncCollector();
                RecordRainInventory();
                return;
            }
            if (item == ElusiveAntlersIndex)
            {
                if (elusiveAntlers++ == 0) ResetAntlerSpawnTimer();
                SyncAntlers();
                RecordRainInventory();
                return;
            }
            if (item == BolsteringLanternIndex)
            {
                bolsteringLanterns++;
                RefreshLanternBodies();
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return;
            }
            if(item==0){Steak++;owner.ServerSetMaximumHealth(owner.MaxHealth+25);owner.ServerHeal(25,owner.netId,allowOverhealBarrier:false);}
            if (item == 1)
            {
                if (lostSeersLenses > 0) lostSeersLenses++;
                else Glasses++;
            }
            if (item == LostSeersIndex)
            {
                lostSeersLenses++;
                CorruptGlasses();
            }
            if (item == FuelCellIndex) SetFuelCells(fuelCells + 1);
            if (item == WarHornIndex && owner is CharacterInstance) warHorns++;
            if (item == GestureIndex) SetGestures(gestures + 1);
            if (item == ShatteringJusticeIndex) shatteringJustice++;
            if (item == CloverIndex)
            {
                clovers++;
                SyncInventoryDisplayStacks();
            }
            if (item == HarpoonIndex) harpoons++;
            if (item == ScorpionIndex) scorpions++;
            if (item == DeathMarkIndex) deathMarks++;
            if (item == ChargedPerforatorIndex) chargedPerforators++;
            if (item == ShatterspleenIndex) shatterspleens++;
            if (item == GenesisLoopIndex && owner is CharacterInstance) genesisLoops++;
            if (item == NkuhanaIndex) nkuhanaOpinions++;
            if (item == LittleDiscipleIndex && owner is CharacterInstance) littleDisciples++;
            if (item == MissileIndex && owner is CharacterInstance missileOwner)
            {
                if (missileOwner.ResearchPlasmaShrimps > 0) missileOwner.ResearchPlasmaShrimps++;
                else missiles++;
            }
            if (item == PocketIcbmIndex) pocketIcbms++;
            if (item == BrittleCrownIndex) brittleCrowns++;
            if (item == GhorTomeIndex) ghorTomes++;
            if (item == SoulboundCatalystIndex) soulboundCatalysts++;
            if (item == CeremonialDaggerIndex) ceremonialDaggers++;
            if (item == RazorwireIndex) razorwireStacks++;
            if (item == MiredUrnIndex) miredUrns++;
            if (item == FrostRelicIndex) frostRelics++;
            if (item == FireworkIndex) FireworkStacks++;
            if (item == MoltenPerforatorIndex) moltenPerforators++;
            if (item == DeskPlantIndex) deskPlants++;
            if (item == IgnitionTankIndex) ignitionTanks++;
            if (item == RollOfPenniesIndex) rollsOfPennies++;
            if (item == ChronicExpansionIndex) chronicExpansions++;
            if (item == QuickFixIndex)
            {
                quickFixes++;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth);
            }
            if (item == WhiteScrapIndex)
            {
                whiteScrap++;
                SyncInventoryDisplayStacks();
            }
            if (item == HikingBootsIndex)
            {
                hikingBoots++;
                SyncInventoryDisplayStacks();
            }
            if (item == LeptonDaisyIndex)
            {
                leptonDaisies++;
                SyncInventoryDisplayStacks();
            }
            if (item == StewIndex)
            {
                stews++;
                SyncInventoryDisplayStacks();
            }
            if (item == UltimateMealIndex)
            {
                ultimateMeals++;
                SyncInventoryDisplayStacks();
            }
            if (item == CookedSteakIndex)
            {
                cookedSteaks++;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth);
            }
            if (item == HexOfPainIndex)
            {
                hexOfPain++;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                if (owner is CharacterInstance painOwner) painOwner.RefreshResearchShield();
            }
            if (item == HexOfFamineIndex)
            {
                hexOfFamine++;
                SyncInventoryDisplayStacks();
                famineHealingRemainder = 0;
            }
            if (item == MochaIndex && owner is CharacterInstance mochaOwner) mochaOwner.ResearchMochas++;
            if (item == StoneFluxIndex && owner is CharacterInstance stoneOwner)
            {
                stoneOwner.ResearchStoneFlux++;
                owner.ServerSetMaximumHealth(owner.MaxHealth);
            }
            if (item == TeslaCoilIndex && teslaCoils++ == 0)
            {
                teslaStartedAt = Time.time;
                nextTeslaPulse = teslaStartedAt + .5f;
            }
            if (item == PearlIndex)
            {
                pearls++;
                owner.ServerSetMaximumHealth(owner.MaxHealth);
            }
            if (item == ShapedGlassIndex)
            {
                shapedGlass++;
                owner.ServerSetMaximumHealth(owner.MaxHealth);
            }
            if (item == PlasmaShrimpIndex && owner is CharacterInstance shrimpOwner)
            {
                shrimpOwner.ResearchPlasmaShrimps++;
                CorruptMissiles(shrimpOwner);
            }
            if (item == BerserkersIndex && owner is CharacterInstance) berserkers++;
            if (item == InfusionIndex)
            {
                int before = InfusionBonus;
                infusions++;
                ApplyInfusionHealthChange(before);
            }
            if(item==2)Tooth++;
            if(item==8)ArmorPlates++;
            if(item==9)Medkits++;
            if(item==10)ArmorPiercingRounds++;
            if(item==11)Crowbars++;
            if(item==12)
            {
                if (saferSpaces > 0) saferSpaces++;
                else Bears++;
            }
            if (item == SaferSpacesIndex)
            {
                saferSpaces++;
                CorruptBears();
            }
            if(item==13)Hooves++;
            if(item==14)EnergyDrinks++;
            if(item==15)ParentEggs++;
            if(item==16)Watches++;
            if(item==17)BrokenWatches++;
            if(item==18)HealingRacks++;
            if(item==19)
            {
                if (Slugs == 0) nextSlugHealAt = Mathf.Max(Time.time, lastDamageAt + DangerDuration) + 1f;
                Slugs++;
                if (owner is CharacterInstance slugOwner) SyncResearchStatItems(slugOwner);
            }
            if(item==20)HealingPotions++;
            if(item==21)EmptyBottles++;
            if(item==22)Brooches++;
            if(item==23)Aegises++;
            if(item==24)
            {
                if (needleticks > 0) needleticks++;
                else Daggers++;
            }
            if (item == NeedletickIndex)
            {
                needleticks++;
                CorruptDaggers();
            }
            if(item==25 && owner is CharacterInstance character) character.ResearchSyringes++;
            if(item==26 && owner is CharacterInstance stickyOwner) stickyOwner.ResearchStickyBombs++;
            if(item==27 && owner is CharacterInstance stunOwner) stunOwner.ResearchStunGrenades++;
            if(item==28 && owner is CharacterInstance blastOwner) blastOwner.ResearchBehemoths++;
            if(item==29 && owner is CharacterInstance crystalOwner) crystalOwner.ResearchFocusCrystals++;
            if (item == 30 && owner is CharacterInstance wispOwner)
            {
                if (voidsentFlames > 0) voidsentFlames++;
                else wispOwner.ResearchWisps++;
            }
            if (item == VoidsentFlameIndex && owner is CharacterInstance flameOwner)
            {
                voidsentFlames++;
                CorruptWisps(flameOwner);
            }
            if(item==31 && owner is CharacterInstance bannerOwner) bannerOwner.ResearchWarbanners++;
            if(item==32 && owner is CharacterInstance ukuleleOwner)
            {
                if (polylutes > 0) polylutes++;
                else ukuleleOwner.ResearchUkuleles++;
            }
            if (item == PolyluteIndex && owner is CharacterInstance polyluteOwner)
            {
                polylutes++;
                CorruptUkuleles(polyluteOwner);
            }
            if(item==33 && owner is CharacterInstance fuelOwner) fuelOwner.ResearchGasoline++;
            if(item==34 && owner is CharacterInstance iceOwner) iceOwner.ResearchIceRings++;
            if(item==35 && owner is CharacterInstance fireOwner) fireOwner.ResearchFireRings++;
            if(item==36 && owner is CharacterInstance shieldOwner)
            {
                shieldOwner.ResearchShieldGenerators++;
                shieldOwner.RefreshResearchShield();
            }
            if(item==37 && owner is CharacterInstance bucklerOwner) bucklerOwner.ResearchBucklers++;
            if(item==38 && owner is CharacterInstance opalOwner) opalOwner.ResearchOpals++;
            if (item == 39 && owner is CharacterInstance baubleOwner)
            {
                if (tentabaubles > 0) tentabaubles++;
                else baubleOwner.ResearchBaubles++;
            }
            if (item == TentabaubleIndex && owner is CharacterInstance tentaOwner)
            {
                tentabaubles++;
                CorruptBaubles(tentaOwner);
            }
            if(item==40 && owner is CharacterInstance seedOwner) seedOwner.ResearchSeeds++;
            if (item == KnurlIndex)
            {
                knurlStacks++;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth + KnurlHealthPerStack);
            }
            if (item == CritDamageIndex) critDamageStacks++;
            if (item == HealOnCritIndex) healOnCritStacks++;
            if (item == RedWhipIndex && owner is CharacterInstance whipOwner) whipOwner.ResearchRedWhipStacks++;
            if (item == PredatoryIndex && owner is CharacterInstance) predatoryStacks++;
            if (item == WeepingFungusIndex && owner is CharacterInstance)
            {
                weepingFungus++;
                CorruptFungus();
            }
            if (item == BustlingFungusIndex && owner is CharacterInstance)
            {
                if (weepingFungus > 0) weepingFungus++;
                else bustlingFungus++;
            }
            if (item == ExtraLifeIndex)
            {
                if (voidExtraLives > 0 || ConsumedVoidExtraLives > 0) voidExtraLives++;
                else extraLives++;
            }
            if (item == VoidExtraLifeIndex)
            {
                voidExtraLives += 1 + extraLives;
                extraLives = 0;
            }
            if (item == VoidExtraLifeConsumedIndex) ConsumedVoidExtraLives++;
            if (item == ExtraLifeConsumedIndex) ConsumedExtraLives++;
        }

        internal void RestoreRainChestItems(int steak, int glasses, int tooth)
        {
            Steak = steak;
            Glasses = glasses;
            Tooth = tooth;
        }
        internal float ItemBaseAttack => (owner is CharacterInstance character
            ? character.EffectiveRainAttackPower : CharacterInstance.RainBaseAttackPower) * ShapedGlassAttackMultiplier * (float)ChronicDamageMultiplier;

        long ShapedGlassAttackMultiplier => 1L + shapedGlass;

        public int ModifyDamage(int value) => ModifyDamage(value, out _);

        internal int ModifyDamage(int value, out bool critical)
        {
            if (shapedGlass > 0 || (chronicExpansions > 0 && Time.time < chronicUntil))
                value = (int)System.Math.Min(int.MaxValue,
                    System.Math.Round(value * (double)ShapedGlassAttackMultiplier * ChronicDamageMultiplier));
            if (Watches > 0)
                value = Mathf.RoundToInt(Mathf.Min(int.MaxValue / 2, value * (1f + .2f * Watches)));
            critical = RollCritical();
            if (!critical) return value;
            return (int)System.Math.Min(int.MaxValue, System.Math.Round(value * CriticalMultiplier));
        }
        bool RollCritical()
        {
            float chance = CriticalChance;
            return RollWithLuck(chance);
        }

        internal bool RollWithLuck(float chance)
        {
            if (Random.value < chance || chance >= 1f) return true;
            if (chance <= 0f) return false;
            long rerolls = clovers;
            if (ultimateMeals > 0 && owner.CurrentHealth >= owner.MaxHealth) rerolls += 2L * ultimateMeals;
            for (long i = 0; i < rerolls; i++)
            {
                if (Random.value >= chance) continue;
                if (i < clovers && owner is CharacterInstance character) character.RpcRainCloverReroll();
                return true;
            }
            return false;
        }

        void RemoveFrostRelicStack()
        {
            if (--frostRelics != 0) return;
            frostKillExpiries?.Clear();
            SetFrostRelicRadius(0f);
        }

        public bool Remove(int item)
        {
            if(!owner||!owner.isServer)return false;
            if (item == AlienHeadIndex && alienHeads > 0 && owner is CharacterInstance)
            {
                alienHeads--;
                SyncPassiveItemStats();
                RecordRainInventory();
                return true;
            }
            if (item == LightFluxIndex && lightFlux > 0 && owner is CharacterInstance)
            {
                lightFlux--;
                SyncPassiveItemStats();
                RecordRainInventory();
                return true;
            }
            if (item == PowerCubeIndex && powerCubes > 0)
            {
                powerCubes--;
                RecordRainInventory();
                return true;
            }
            if (item == PowerOrbIndex && powerOrbs > 0 && owner is CharacterInstance)
            {
                powerOrbs--;
                SyncPassiveItemStats();
                RecordRainInventory();
                return true;
            }
            if (item == CollectorIndex && collectors > 0)
            {
                collectors--;
                // Existing timed buffs survive item removal; only future refreshes require the item.
                SyncCollector();
                RecordRainInventory();
                return true;
            }
            if (item == ElusiveAntlersIndex && elusiveAntlers > 0)
            {
                elusiveAntlers--;
                SyncAntlers();
                RecordRainInventory();
                return true;
            }
            if (item == BolsteringLanternIndex && bolsteringLanterns > 0)
            {
                bolsteringLanterns--;
                RefreshLanternBodies();
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == QuickFixIndex && quickFixes > 0)
            {
                quickFixes--;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                RecordRainInventory();
                return true;
            }
            if (item == WhiteScrapIndex && whiteScrap > 0)
            {
                whiteScrap--;
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == HikingBootsIndex && hikingBoots > 0)
            {
                if (--hikingBoots == 0) hikingHits = 0;
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == LeptonDaisyIndex && leptonDaisies > 0)
            {
                leptonDaisies--;
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == StewIndex && stews > 0)
            {
                stews--;
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == UltimateMealIndex && ultimateMeals > 0)
            {
                ultimateMeals--;
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == CookedSteakIndex && cookedSteaks > 0)
            {
                cookedSteaks--;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                RecordRainInventory();
                return true;
            }
            if (item == HexOfPainIndex && hexOfPain > 0)
            {
                hexOfPain--;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                if (owner is CharacterInstance painOwner) painOwner.RefreshResearchShield();
                RecordRainInventory();
                return true;
            }
            if (item == HexOfFamineIndex && hexOfFamine > 0)
            {
                hexOfFamine--;
                SyncInventoryDisplayStacks();
                famineHealingRemainder = 0;
                RecordRainInventory();
                return true;
            }
            if (item == ChronicExpansionIndex && chronicExpansions > 0)
            {
                chronicExpansions--;
                chronicKills = chronicExpansions > 0 ? (int)System.Math.Min(chronicKills, ChronicCap) : 0;
                if (chronicExpansions == 0) chronicUntil = 0f;
                RecordRainInventory();
                return true;
            }
            if (item == StoneFluxIndex && owner is CharacterInstance stoneOwner && stoneOwner.ResearchStoneFlux > 0)
            {
                stoneOwner.ResearchStoneFlux--;
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                RecordRainInventory();
                return true;
            }
            if (item == MochaIndex && owner is CharacterInstance mochaOwner && mochaOwner.ResearchMochas > 0)
            {
                mochaOwner.ResearchMochas--;
                RecordRainInventory();
                return true;
            }
            if (item == RollOfPenniesIndex && rollsOfPennies > 0)
            {
                rollsOfPennies--;
                RecordRainInventory();
                return true;
            }
            if (item == IgnitionTankIndex && ignitionTanks > 0)
            {
                ignitionTanks--;
                RecordRainInventory();
                return true;
            }
            if (item == DeskPlantIndex && deskPlants > 0)
            {
                deskPlants--;
                RecordRainInventory();
                return true;
            }
            if (item == MoltenPerforatorIndex && moltenPerforators > 0)
            {
                moltenPerforators--;
                RecordRainInventory();
                return true;
            }
            if (item == FireworkIndex && FireworkStacks > 0)
            {
                FireworkStacks--;
                RecordRainInventory();
                return true;
            }
            if (item == FrostRelicIndex && frostRelics > 0)
            {
                RemoveFrostRelicStack();
                RecordRainInventory();
                return true;
            }
            if (item == MiredUrnIndex && miredUrns > 0)
            {
                if (--miredUrns == 0) nextUrnPulse = 0f;
                RecordRainInventory();
                return true;
            }
            if (item == TeslaCoilIndex && teslaCoils > 0)
            {
                teslaCoils--;
                RecordRainInventory();
                return true;
            }
            if (item == RazorwireIndex && razorwireStacks > 0)
            {
                razorwireStacks--;
                RecordRainInventory();
                return true;
            }
            if (item == CeremonialDaggerIndex && ceremonialDaggers > 0)
            {
                ceremonialDaggers--;
                RecordRainInventory();
                return true;
            }
            if (item == SoulboundCatalystIndex && soulboundCatalysts > 0)
            {
                soulboundCatalysts--;
                RecordRainInventory();
                return true;
            }
            if (item == ShapedGlassIndex && shapedGlass > 0)
            {
                shapedGlass--;
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                return true;
            }
            if (item == PearlIndex && pearls > 0)
            {
                pearls--;
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                return true;
            }
            if (item == GhorTomeIndex && ghorTomes > 0)
            {
                ghorTomes--;
                return true;
            }
            if (item == BrittleCrownIndex && brittleCrowns > 0)
            {
                brittleCrowns--;
                return true;
            }
            if (item == PocketIcbmIndex && pocketIcbms > 0)
            {
                pocketIcbms--;
                return true;
            }
            if (item == PlasmaShrimpIndex && owner is CharacterInstance shrimpOwner && shrimpOwner.ResearchPlasmaShrimps > 0)
            {
                shrimpOwner.ResearchPlasmaShrimps--;
                shrimpOwner.RefreshResearchShield();
                return true;
            }
            if (item == MissileIndex && missiles > 0)
            {
                missiles--;
                return true;
            }
            if (item == LittleDiscipleIndex && littleDisciples > 0)
            {
                if (--littleDisciples == 0) discipleSprintCharge = 0f;
                return true;
            }
            if (item == NkuhanaIndex && nkuhanaOpinions > 0)
            {
                if (--nkuhanaOpinions == 0) soulEnergy = 0d;
                return true;
            }
            if (item == GenesisLoopIndex && genesisLoops > 0)
            {
                genesisLoops--;
                return true;
            }
            if (item == ShatterspleenIndex && shatterspleens > 0)
            {
                shatterspleens--;
                return true;
            }
            if (item == ChargedPerforatorIndex && chargedPerforators > 0)
            {
                chargedPerforators--;
                return true;
            }
            if (item == TentabaubleIndex && tentabaubles > 0)
            {
                tentabaubles--;
                return true;
            }
            if (item == BerserkersIndex && berserkers > 0)
            {
                if (--berserkers == 0) lastKillAt = secondLastKillAt = thirdLastKillAt = float.NegativeInfinity;
                return true;
            }
            if (item == DeathMarkIndex && deathMarks > 0)
            {
                deathMarks--;
                return true;
            }
            if (item == ScorpionIndex && scorpions > 0)
            {
                scorpions--;
                return true;
            }
            if (item == HarpoonIndex && harpoons > 0)
            {
                harpoons--;
                return true;
            }
            if (item == InfusionIndex && infusions > 0)
            {
                int before = InfusionBonus;
                infusions--;
                ApplyInfusionHealthChange(before);
                return true;
            }
            if (item == CloverIndex && clovers > 0)
            {
                clovers--;
                SyncInventoryDisplayStacks();
                RecordRainInventory();
                return true;
            }
            if (item == ShatteringJusticeIndex && shatteringJustice > 0)
            {
                shatteringJustice--;
                return true;
            }
            if (item == GestureIndex && gestures > 0)
            {
                SetGestures(gestures - 1);
                return true;
            }
            if (item == WarHornIndex && warHorns > 0)
            {
                warHorns--;
                return true;
            }
            if (item == FuelCellIndex && fuelCells > 0)
            {
                SetFuelCells(fuelCells - 1);
                return true;
            }
            if (item == LostSeersIndex && lostSeersLenses > 0)
            {
                lostSeersLenses--;
                return true;
            }
            if (item == VoidsentFlameIndex && voidsentFlames > 0)
            {
                voidsentFlames--;
                return true;
            }
            if (item == PolyluteIndex && polylutes > 0)
            {
                polylutes--;
                return true;
            }
            if (item == NeedletickIndex && needleticks > 0)
            {
                needleticks--;
                return true;
            }
            if (item == BustlingFungusIndex && bustlingFungus > 0)
            {
                if (--bustlingFungus == 0) nextFungusPulse = 0f;
                return true;
            }
            if (item == WeepingFungusIndex && weepingFungus > 0)
            {
                if (--weepingFungus == 0)
                {
                    weepingHealingRemainder = 0f;
                    weepingHealingActive = false;
                }
                return true;
            }
            if (item == SaferSpacesIndex && saferSpaces > 0)
            {
                saferSpaces--;
                return true;
            }
            if (item == VoidExtraLifeIndex && voidExtraLives > 0)
            {
                voidExtraLives--;
                return true;
            }
            if (item == VoidExtraLifeConsumedIndex && ConsumedVoidExtraLives > 0)
            {
                ConsumedVoidExtraLives--;
                return true;
            }
            if (item == ExtraLifeIndex && extraLives > 0)
            {
                extraLives--;
                return true;
            }
            if (item == ExtraLifeConsumedIndex && ConsumedExtraLives > 0)
            {
                ConsumedExtraLives--;
                return true;
            }
            if (item == PredatoryIndex && predatoryStacks > 0 && owner is CharacterInstance predatoryOwner)
            {
                predatoryStacks--;
                int cap = predatoryStacks > 0 ? 1 + 2 * predatoryStacks : 0;
                if (predatoryExpirations != null)
                {
                    RemoveExpiredPredatoryStacks(Time.time);
                    while (predatoryExpirations.Count > cap)
                        predatoryExpirations.RemoveAt(FindEarliestPredatoryStack());
                }
                predatoryOwner.ResearchPredatoryBuffs = predatoryExpirations?.Count ?? 0;
                return true;
            }
            if (item == RedWhipIndex && owner is CharacterInstance whipOwner && whipOwner.ResearchRedWhipStacks > 0)
            {
                whipOwner.ResearchRedWhipStacks--;
                return true;
            }
            if (item == HealOnCritIndex && healOnCritStacks > 0)
            {
                if (--healOnCritStacks == 0) critHealingRemainder = 0f;
                return true;
            }
            if (item == CritDamageIndex && critDamageStacks > 0)
            {
                critDamageStacks--;
                return true;
            }
            if (item == KnurlIndex && knurlStacks > 0)
            {
                if (--knurlStacks == 0) knurlHealingRemainder = 0f;
                SyncInventoryDisplayStacks();
                owner.ServerSetMaximumHealth(owner.MaxHealth - KnurlHealthPerStack);
                return true;
            }
            if(item==0&&Steak>0){Steak--;owner.ServerSetMaximumHealth(owner.MaxHealth-25);return true;}
            if(item==1&&Glasses>0){Glasses--;return true;}
            if(item==2&&Tooth>0){Tooth--;return true;}
            if(item==8&&ArmorPlates>0){ArmorPlates--;return true;}
            if(item==9&&Medkits>0){if(--Medkits==0)medkitReadyAt=0;return true;}
            if(item==10&&ArmorPiercingRounds>0){ArmorPiercingRounds--;return true;}
            if(item==11&&Crowbars>0){Crowbars--;return true;}
            if(item==12&&Bears>0){Bears--;return true;}
            if(item==13&&Hooves>0){Hooves--;return true;}
            if(item==14&&EnergyDrinks>0){EnergyDrinks--;return true;}
            if(item==15&&ParentEggs>0){ParentEggs--;return true;}
            if(item==16&&Watches>0){Watches--;return true;}
            if(item==17&&BrokenWatches>0){BrokenWatches--;return true;}
            if(item==18&&HealingRacks>0){HealingRacks--;return true;}
            if (item == 19 && Slugs > 0)
            {
                Slugs--;
                if (owner is CharacterInstance slugOwner) SyncResearchStatItems(slugOwner);
                RecordRainInventory();
                return true;
            }
            if(item==20&&HealingPotions>0){HealingPotions--;return true;}
            if(item==21&&EmptyBottles>0){EmptyBottles--;return true;}
            if(item==22&&Brooches>0){Brooches--;return true;}
            if(item==23&&Aegises>0){Aegises--;return true;}
            if(item==24&&Daggers>0){Daggers--;return true;}
            if(item==25 && owner is CharacterInstance character && character.ResearchSyringes>0)
            { character.ResearchSyringes--; return true; }
            if(item==26 && owner is CharacterInstance stickyOwner && stickyOwner.ResearchStickyBombs>0)
            { stickyOwner.ResearchStickyBombs--; return true; }
            if(item==27 && owner is CharacterInstance stunOwner && stunOwner.ResearchStunGrenades>0)
            { stunOwner.ResearchStunGrenades--; return true; }
            if(item==28 && owner is CharacterInstance blastOwner && blastOwner.ResearchBehemoths>0)
            { blastOwner.ResearchBehemoths--; return true; }
            if(item==29 && owner is CharacterInstance crystalOwner && crystalOwner.ResearchFocusCrystals>0)
            { crystalOwner.ResearchFocusCrystals--; return true; }
            if(item==30 && owner is CharacterInstance wispOwner && wispOwner.ResearchWisps>0)
            { wispOwner.ResearchWisps--; return true; }
            if(item==31 && owner is CharacterInstance bannerOwner && bannerOwner.ResearchWarbanners>0)
            { bannerOwner.ResearchWarbanners--; return true; }
            if(item==32 && owner is CharacterInstance ukuleleOwner && ukuleleOwner.ResearchUkuleles>0)
            { ukuleleOwner.ResearchUkuleles--; return true; }
            if(item==33 && owner is CharacterInstance fuelOwner && fuelOwner.ResearchGasoline>0)
            { fuelOwner.ResearchGasoline--; return true; }
            if(item==34 && owner is CharacterInstance iceOwner && iceOwner.ResearchIceRings>0)
            { iceOwner.ResearchIceRings--; return true; }
            if(item==35 && owner is CharacterInstance fireOwner && fireOwner.ResearchFireRings>0)
            { fireOwner.ResearchFireRings--; return true; }
            if(item==36 && owner is CharacterInstance shieldOwner && shieldOwner.ResearchShieldGenerators>0)
            {
                shieldOwner.ResearchShieldGenerators--;
                shieldOwner.RefreshResearchShield();
                return true;
            }
            if(item==37 && owner is CharacterInstance bucklerOwner && bucklerOwner.ResearchBucklers>0)
            { bucklerOwner.ResearchBucklers--; return true; }
            if(item==38 && owner is CharacterInstance opalOwner && opalOwner.ResearchOpals>0)
            { opalOwner.ResearchOpals--; return true; }
            if(item==39 && owner is CharacterInstance baubleOwner && baubleOwner.ResearchBaubles>0)
            { baubleOwner.ResearchBaubles--; return true; }
            if(item==40 && owner is CharacterInstance seedOwner && seedOwner.ResearchSeeds>0)
            {
                if (--seedOwner.ResearchSeeds == 0) seedHealingRemainder = 0;
                return true;
            }
            return false;
        }
        void ApplySoulboundCatalyst()
        {
            float previousCooldown = EquipmentCooldownRemaining;
            float scale = EquipmentRechargeScale;
            float seconds = 2f + 2f * soulboundCatalysts;
            switch (Equipped)
            {
                case Equipment.Fruit: fruitCharges.Advance(45f * scale, seconds); break;
                case Equipment.Elephant: elephantCharges.Advance(45f * scale, seconds); break;
                case Equipment.OcularHud: ocularHudCharges.Advance(60f * scale, seconds); break;
                case Equipment.Leech: leechCharges.Advance(60f * scale, seconds); break;
                case Equipment.TeamWarCry: teamWarCryCharges.Advance(45f * scale, seconds); break;
                case Equipment.Scanner: scannerCharges.Advance(45f * scale, seconds); break;
                case Equipment.Capacitor: capacitorCharges.Advance(20f * scale, seconds); break;
                case Equipment.MissileLauncher: missileLauncherCharges.Advance(45f * scale, seconds); break;
                case Equipment.BlastShower: blastShowerCharges.Advance(20f * scale, seconds); break;
                case Equipment.DroneBackup: droneBackupCharges.Advance(100f * scale, seconds); break;
                case Equipment.Effigy: effigyCharges.Advance(15f * scale, seconds); break;
                default: return;
            }
            if (EquipmentCooldownRemaining < previousCooldown && owner is CharacterInstance catalystOwner)
                catalystOwner.RpcRainSoulboundCatalyst();
            RecordRainInventory();
        }

        internal void RefreshChronicExpansion()
        {
            if (chronicExpansions > 0 && Time.time < chronicUntil) chronicUntil = Time.time + 7f;
        }

        public void OnKill(Vector3 position, bool enemyKill)
        {
            if (owner.CurrentHealth <= 0) return;
            if (enemyKill && owner.isServer && chronicExpansions > 0)
            {
                if (Time.time >= chronicUntil)
                {
                    chronicKills = 0;
                    if (owner is CharacterInstance expansionOwner) expansionOwner.RpcRainChronicExpansion();
                }
                chronicKills = (int)System.Math.Min(int.MaxValue, System.Math.Min(ChronicCap, chronicKills + 1L));
                chronicUntil = Time.time + 7f;
            }
            if (enemyKill && owner.isServer && soulboundCatalysts > 0) ApplySoulboundCatalyst();
            if (enemyKill && owner.isServer && deskPlants > 0 && GameManager.Gamemode is Gamemodes.RangeGamemode plantMode)
                plantMode.PlantRainHealingSeed(position, owner.Team, owner.netId, deskPlants);
            if (enemyKill && owner.isServer && frostRelics > 0)
            {
                frostKillExpiries ??= new Queue<float>();
                while (frostKillExpiries.Count > 0 && frostKillExpiries.Peek() <= Time.time)
                    frostKillExpiries.Dequeue();
                if (frostKillExpiries.Count == 0) nextFrostPulse = Time.time + .25f;
                frostKillExpiries.Enqueue(Time.time + 5f);
            }
            if (enemyKill && owner.isServer && ceremonialDaggers > 0)
            {
                Vector3 origin = position + Vector3.up;
                var target = FindHomingTarget(origin);
                if (target)
                {
                    if (owner is CharacterInstance daggerCaster)
                        daggerCaster.RpcPlayRainCeremonialDaggerEffect(origin, false);
                    int damage = Mathf.RoundToInt(1.5f * ItemBaseAttack * ceremonialDaggers);
                    Vector3 lateral = Vector3.Cross(Vector3.up, target.transform.position + Vector3.up - origin).normalized;
                    for (int shot = -1; shot <= 1; shot++)
                    {
                        Vector3 launchPoint = origin + lateral * (.22f * shot);
                        if (Physics.Linecast(origin, launchPoint, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                            launchPoint = origin;
                        StartCoroutine(ResolveHomingItemShot(launchPoint, target, damage, RangeProcSource.Dagger));
                    }
                }
            }
            if (enemyKill && ghorTomes > 0 && GameManager.Gamemode is Gamemodes.RangeGamemode rain &&
                RollWithLuck(.04f * ghorTomes))
                rain.DropGoldPack(position, owner.Team);
            if (enemyKill && berserkers > 0 && owner is CharacterInstance frenzyOwner)
            {
                float now = Time.time;
                if (now - thirdLastKillAt <= 1f)
                {
                    ApplyFrenzy(frenzyOwner, 6f + 4f * (berserkers - 1));
                }
                thirdLastKillAt = secondLastKillAt;
                secondLastKillAt = lastKillAt;
                lastKillAt = now;
            }
            int infusionBefore = InfusionBonus;
            if (enemyKill && harpoons > 0 && owner is CharacterInstance harpoonOwner)
            {
                harpoonOwner.ResearchHarpoonDuration = 1f + .5f * (harpoons - 1);
                harpoonOwner.ResearchHarpoonUntil = Mirror.NetworkTime.time + harpoonOwner.ResearchHarpoonDuration;
                harpoonOwner.RpcRainHarpoon();
            }
            if (enemyKill && infusionBefore < 100 * infusions)
            {
                infusionEarned = Mathf.Min(100 * infusions, infusionEarned + infusions);
                ApplyInfusionHealthChange(infusionBefore);
                if (owner is CharacterInstance infusionOwner) infusionOwner.RpcRainInfusion(position);
                RecordRainInventory();
            }
            if (Tooth > 0 && GameManager.Gamemode is Gamemodes.RangeGamemode range)
                range.DropToothPickup(position, owner.Team, Tooth);
            if (Brooches > 0) AddBarrier(15f * Brooches);
            if (owner is CharacterInstance fuelOwner && fuelOwner.ResearchGasoline > 0)
            {
                int stacks = fuelOwner.ResearchGasoline;
                float attack = ItemBaseAttack;
                float blastDamage = attack * 1.5f;
                if (CriticalChance > 0 && RollCritical()) blastDamage *= (float)CriticalMultiplier;
                StartCoroutine(IgniteGasoline(position, stacks, Mathf.RoundToInt(blastDamage),
                    Mathf.RoundToInt(attack * .75f * (1f + stacks))));
            }
            if (owner is CharacterInstance character && character.ResearchWisps > 0)
            {
                int extra = character.ResearchWisps - 1;
                float attack = ItemBaseAttack;
                float damage = attack * 3.5f * (1f + .8f * extra);
                if (CriticalChance > 0 && RollCritical()) damage *= (float)CriticalMultiplier;
                StartCoroutine(ExplodeWisp(position, 12f + 2.4f * extra, Mathf.RoundToInt(damage)));
            }
        }

        void ApplyFrenzy(CharacterInstance character, float duration)
        {
            frenzyUntil = Mathf.Max(frenzyUntil, Time.time + duration);
            character.ResearchBerserkerFrenzy = true;
        }

        void ApplyInfusionHealthChange(int before)
        {
            int change = InfusionBonus - before;
            if (change != 0) owner.ServerSetMaximumHealth(owner.MaxHealth + change);
        }

        IEnumerator IgniteGasoline(Vector3 position, int stacks, int blastDamage, int burnDamage)
        {
            // Let the triggering death finish before a splash kill can start its own explosion.
            yield return null;
            if (!owner || !owner.isServer) yield break;
            float radius = 8f + 4f * stacks;
            if (owner is CharacterInstance character) character.RpcRainGasolineBlast(position, radius);
            ApplyItemBlast(position, radius, blastDamage,
                burnDamage: burnDamage, burnTicks: Mathf.CeilToInt(7.5f * (1f + stacks)));
        }

        IEnumerator ExplodeWisp(Vector3 position, float radius, int damage)
        {
            if (owner is CharacterInstance character) character.RpcRainWispItemBlast(position, radius, false);
            yield return new WaitForSeconds(.5f);
            if (!owner || !owner.isServer) yield break;
            if (owner is CharacterInstance source) source.RpcRainWispItemBlast(position, radius, true);
            ApplyItemBlast(position, radius, damage, sweetSpot: true);
        }
        public void Clear()
        {
            capacitorCastGeneration++;
            if(!owner || !owner.isServer)return;
            collectors = powerOrbs = powerCubes = alienHeads = lightFlux = 0;
            ClearCollectorBuff();
            SyncCollector();
            pearls = 0;
            shapedGlass = 0;
            hexOfPain = 0;
            cookedSteaks = 0;
            ultimateMeals = 0;
            stews = 0;
            leptonDaisies = 0;
            hikingBoots = hikingHits = 0;
            quickFixes = whiteScrap = 0;
            bolsteringLanterns = lanternBodies = 0;
            elusiveAntlers = 0;
            ClearAntlerBuff();
            ResetAntlerSpawnTimer();
            hikingBuffUntil = 0f;
            if (owner is CharacterInstance stoneOwner) stoneOwner.ResearchStoneFlux = 0;
            soulboundCatalysts = 0;
            ceremonialDaggers = 0;
            razorwireStacks = 0;
            teslaCoils = 0;
            miredUrns = 0;
            frostRelics = 0;
            FireworkStacks = 0;
            moltenPerforators = 0;
            deskPlants = 0;
            ignitionTanks = 0;
            rollsOfPennies = 0;
            chronicExpansions = chronicKills = 0;
            hexOfFamine = 0;
            SyncInventoryDisplayStacks();
            famineHealingRemainder = 0;
            chronicUntil = 0f;
            frostKillExpiries?.Clear();
            SetFrostRelicRadius(0f);
            nextUrnPulse = 0f;
            owner.ServerSetMaximumHealth(owner.MaxHealth - 25 * Steak - KnurlHealthPerStack * knurlStacks - InfusionBonus);
            infusions = infusionEarned = 0;
            harpoons = 0;
            berserkers = 0;
            tentabaubles = 0;
            chargedPerforators = 0;
            shatterspleens = 0;
            genesisLoops = 0;
            nkuhanaOpinions = 0;
            littleDisciples = 0;
            missiles = 0;
            pocketIcbms = 0;
            brittleCrowns = 0;
            ghorTomes = 0;
            discipleSprintCharge = 0f;
            soulEnergy = 0d;
            frenzyUntil = 0f;
            lastKillAt = secondLastKillAt = thirdLastKillAt = float.NegativeInfinity;
            permanentArmorLoss = 0f;
            scorpions = 0;
            deathMarks = 0;
            deathMarkUntil = 0f;
            knurlStacks = 0;
            knurlHealingRemainder = 0f;
            Equipped = Equipment.None;
            elephantArmorUntil = 0f;
            guaranteedCritUntil = 0f;
            leechUntil = 0f;
            leechDamageRemainder = 0;
            critDamageStacks = 0;
            healOnCritStacks = 0;
            critHealingRemainder = 0f;
            predatoryStacks = 0;
            predatoryExpirations?.Clear();
            extraLives = ConsumedExtraLives = 0;
            voidExtraLives = ConsumedVoidExtraLives = 0;
            saferSpaces = 0;
            weepingFungus = 0;
            bustlingFungus = 0;
            needleticks = 0;
            polylutes = 0;
            voidsentFlames = 0;
            lostSeersLenses = 0;
            SetFuelCells(0);
            SetGestures(0);
            shatteringJustice = pulverizeHits = 0;
            clovers = 0;
            Slugs = 0;
            SyncInventoryDisplayStacks();
            pulverizedUntil = 0f;
            warHorns = 0;
            warHornUntil = 0f;
            nextFungusPulse = 0f;
            weepingHealingRemainder = 0f;
            weepingHealingActive = false;
            extraLifeProtectedUntil = 0f;
            Steak=Glasses=Tooth=ArmorPlates=Medkits=ArmorPiercingRounds=Crowbars=Bears=Hooves=EnergyDrinks=ParentEggs=Watches=BrokenWatches=HealingRacks=HealingPotions=EmptyBottles=Brooches=Aegises=Daggers=0;
            if(owner is CharacterInstance character)
            {
                character.ResearchWarHornActive = false;
                character.ResearchBerserkerFrenzy = false;
                character.ResearchHarpoonUntil = 0d;
                character.ResearchRedWhipStacks = 0;
                character.ResearchRootUntil = 0d;
                character.RainScannerUntil = 0d;
                character.ResearchPredatoryBuffs = 0;
                character.ResearchMochas = 0;
                character.SetResearchPassiveItemStats(false, 0, 0);
                character.ResearchSyringes = character.ResearchStickyBombs = character.ResearchStunGrenades =
                    character.ResearchBehemoths = character.ResearchFocusCrystals = character.ResearchWisps =
                    character.ResearchWarbanners = character.ResearchUkuleles = character.ResearchGasoline =
                    character.ResearchIceRings = character.ResearchFireRings = 0;
                character.ResearchShieldGenerators = 0;
                character.ResearchPlasmaShrimps = 0;
                character.ResearchBucklers = 0;
                character.ResearchOpals = 0;
                character.ResearchBaubles = character.ResearchSeeds = 0;
                seedHealingRemainder = 0;
                character.ResearchOpalReadyAt = 0;
                character.RefreshResearchShield();
            }
            elementalRingsReadyAt = 0;
            Barrier=0f;
            medkitReadyAt=0;
        }
    }
}
