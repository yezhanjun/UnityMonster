using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.UI.HUD;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT.UI;
using System;

namespace MTPSKIT.Gameplay {

    /// <summary>
    /// This component is responsible for managing and animating character
    /// </summary>
    [RequireComponent(typeof(CharacterAnimator))]
    public partial class CharacterInstance : Health
    {
        internal const float RainBaseAttackPower = 12f;
        [SyncVar] internal float RainAttackPower;
        [SyncVar] internal float RainHealthRegen;
        internal int ResearchSyringes
        {
            get => researchStatItemStacks.Syringes;
            set
            {
                if (researchStatItemStacks.Syringes == value) return;
                var next = researchStatItemStacks;
                next.Syringes = value;
                researchStatItemStacks = next;
            }
        }
        internal int ResearchMochas
        {
            get => researchStatItemStacks.Mochas;
            set
            {
                if (researchStatItemStacks.Mochas == value) return;
                var next = researchStatItemStacks;
                next.Mochas = value;
                researchStatItemStacks = next;
            }
        }
        internal int ResearchStoneFlux
        {
            get => researchStatItemStacks.StoneFlux;
            set
            {
                if (researchStatItemStacks.StoneFlux == value) return;
                var next = researchStatItemStacks;
                next.StoneFlux = value;
                researchStatItemStacks = next;
            }
        }
        [SyncVar] internal int ResearchHexOfPain;
        [SyncVar] internal int ResearchHexOfFamine;
        [SyncVar] internal int ResearchCookedSteaks;
        [SyncVar] internal int ResearchUltimateMeals;
        internal struct ResearchStatItemStacks
        {
            public HeroStatBonuses Hero;
            public float HeroArmor;
            public double HeroArmorUntil;
            public int Stews, Knurls, ActiveSlugs, HikingBoots, QuickFixes, WhiteScrap, Lanterns, LanternBodies, Antlers, AntlerBuffs, Syringes, Mochas, StoneFlux, Collectors, CollectorBuffs;
            public bool PowerOrb;
            public int AlienHeads, LightFlux;
        }

        [SyncVar] ResearchStatItemStacks researchStatItemStacks;
        internal int ResearchStews => researchStatItemStacks.Stews;
        internal int ResearchKnurls => researchStatItemStacks.Knurls;
        internal int ResearchActiveSlugs => researchStatItemStacks.ActiveSlugs;
        internal int ResearchHikingBoots => researchStatItemStacks.HikingBoots;
        internal int ResearchQuickFixes => researchStatItemStacks.QuickFixes;
        internal int ResearchWhiteScrap => researchStatItemStacks.WhiteScrap;
        internal int ResearchLanterns => researchStatItemStacks.Lanterns;
        internal int ResearchLanternBodies => researchStatItemStacks.LanternBodies;
        internal int ResearchAntlers => researchStatItemStacks.Antlers;
        internal int ResearchAntlerBuffs => researchStatItemStacks.AntlerBuffs;
        internal int ResearchCollectors => researchStatItemStacks.Collectors;
        internal int ResearchCollectorBuffs => researchStatItemStacks.CollectorBuffs;
        internal float ResearchCollectorMultiplier => 1f + .03f * ResearchCollectorBuffs;
        internal void SetResearchCollectorStacks(int items, int buffs)
        {
            if (!isServer || (ResearchCollectors == items && ResearchCollectorBuffs == buffs)) return;
            var value = researchStatItemStacks;
            value.Collectors = items;
            value.CollectorBuffs = buffs;
            researchStatItemStacks = value;
        }
        internal float ResearchAntlerMoveBonus => .12f * ResearchAntlerBuffs;
        internal void SetResearchAntlerStacks(int items, int buffs)
        {
            if (!isServer || (ResearchAntlers == items && ResearchAntlerBuffs == buffs)) return;
            var value = researchStatItemStacks;
            value.Antlers = items;
            value.AntlerBuffs = buffs;
            researchStatItemStacks = value;
        }

        internal void SetResearchStatItemStacks(int stews, int knurls, int activeSlugs, int hikingBoots, int quickFixes, int whiteScrap,
            int lanterns, int lanternBodies)
        {
            if (stews == ResearchStews && knurls == ResearchKnurls && activeSlugs == ResearchActiveSlugs &&
                hikingBoots == ResearchHikingBoots && quickFixes == ResearchQuickFixes && whiteScrap == ResearchWhiteScrap &&
                lanterns == ResearchLanterns && lanternBodies == ResearchLanternBodies) return;
            var next = researchStatItemStacks;
            next.Stews = stews;
            next.Knurls = knurls;
            next.ActiveSlugs = activeSlugs;
            next.HikingBoots = hikingBoots;
            next.QuickFixes = quickFixes;
            next.WhiteScrap = whiteScrap;
            next.Lanterns = lanterns;
            next.LanternBodies = lanternBodies;
            researchStatItemStacks = next;
        }
        [SyncVar] internal int ResearchLeptonDaisies;
        [SyncVar] internal int ResearchClovers;
        internal float ResearchMochaMoveBonus => .07f * ResearchMochas;
        [SyncVar] internal int ResearchPredatoryBuffs;
        [SyncVar] internal bool ResearchWarHornActive;
        [SyncVar] internal bool ResearchBerserkerFrenzy;
        [SyncVar] internal double ResearchHarpoonUntil;
        [SyncVar] internal float ResearchHarpoonDuration;
        [SyncVar] internal int ResearchRedWhipStacks;
        [SyncVar] internal double ResearchRedWhipReadyAt;
        internal float ResearchRedWhipMoveBonus => NetworkTime.time >= ResearchRedWhipReadyAt
            ? .30f * ResearchRedWhipStacks : 0f;
        internal float ResearchHarpoonMoveBonus => ResearchHarpoonDuration > 0f
            ? 1.25f * Mathf.Clamp01((float)(ResearchHarpoonUntil - NetworkTime.time) / ResearchHarpoonDuration)
            : 0f;
        [SyncVar] internal int ResearchStickyBombs;
        [SyncVar] internal int ResearchStunGrenades;
        [SyncVar] internal int ResearchBehemoths;
        [SyncVar] internal int ResearchFocusCrystals;
        [SyncVar] internal int ResearchWisps;
        [SyncVar] internal int ResearchWarbanners;
        [SyncVar] internal int ResearchUkuleles;
        [SyncVar] internal int ResearchGasoline;
        [SyncVar] internal int ResearchIceRings;
        [SyncVar] internal int ResearchFireRings;
        [SyncVar] internal int ResearchShieldGenerators;
        [SyncVar] internal int ResearchBucklers;
        [SyncVar] internal int ResearchOpals;
        [SyncVar] internal int ResearchBaubles;
        [SyncVar] internal int ResearchSeeds;
        [SyncVar] internal double ResearchBaubleSlowUntil;
        [SyncVar] internal double ResearchTarUntil;
        [SyncVar(hook = nameof(OnFrostRelicRadiusChanged))] internal float ResearchFrostRelicRadius;
        LineRenderer frostRelicRing;
        [SyncVar] internal double ResearchRootUntil;
        [SyncVar] internal double RainScannerUntil;
        internal bool ResearchRooted => ResearchRootUntil > NetworkTime.time;
        [SyncVar] internal double ResearchOpalReadyAt;
        [SyncVar] internal int ResearchShield;
        int previousResearchShieldCapacity;
        bool researchShieldRechargeStarted;
        static AudioClip personalShieldRechargeAudio, atgFireAudio, atgImpactAudio;
        float researchShieldCharge, researchShieldRechargeAt;
        [SyncVar] internal int ResearchPlasmaShrimps;
        internal int ResearchShieldCapacity => Mathf.RoundToInt(MaxHealth *
            (.08f * ResearchShieldGenerators + (ResearchPlasmaShrimps > 0 ? .10f : 0f)));

        internal void RefreshResearchShield()
        {
            int capacity = ResearchShieldCapacity;
            researchShieldCharge = Mathf.Clamp(researchShieldCharge + capacity - previousResearchShieldCapacity, 0, capacity);
            previousResearchShieldCapacity = capacity;
            ResearchShield = Mathf.FloorToInt(researchShieldCharge);
        }

        internal int AbsorbResearchShield(int damage)
        {
            if (ResearchShieldGenerators <= 0 && ResearchPlasmaShrimps <= 0) return damage;
            RefreshResearchShield();
            researchShieldRechargeAt = Time.time + 7f;
            researchShieldRechargeStarted = false;
            int absorbed = Mathf.Min(damage, ResearchShield);
            researchShieldCharge -= absorbed;
            ResearchShield -= absorbed;
            return damage - absorbed;
        }
        [ClientRpc]
        void RpcRainPersonalShieldRecharge()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!personalShieldRechargeAudio)
                personalShieldRechargeAudio = Resources.Load<AudioClip>("RainImported/PersonalShieldRecharge");
            if (personalShieldRechargeAudio) AudioSource.PlayClipAtPoint(personalShieldRechargeAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain personal shield recharge", CombatAimPoint, .9f, new Color(.4f, .77f, 1f));
        }

        [SyncVar] internal double ResearchIceSlowUntil;
        [SyncVar] internal bool ResearchBannerBuff;
        [SyncVar] BeetleJuiceStatus beetleJuice;

        internal struct BeetleJuiceStatus
        {
            public byte Stacks;
            public double ExpiresAt;
        }

        internal void ClearRainDebuffs()
        {
            ResearchBaubleSlowUntil = ResearchTarUntil = ResearchRootUntil = ResearchIceSlowUntil = 0d;
            beetleJuice = default;
        }

        internal float BeetleJuicePenalty => beetleJuice.ExpiresAt > NetworkTime.time
            ? .05f * beetleJuice.Stacks : 0f;

        internal void ApplyBeetleJuice()
        {
            if (!isServer || CurrentHealth <= 0) return;
            double now = NetworkTime.time;
            int stacks = beetleJuice.ExpiresAt > now ? beetleJuice.Stacks : 0;
            beetleJuice = new BeetleJuiceStatus
            {
                Stacks = (byte)Mathf.Min(stacks + 1, 10),
                ExpiresAt = now + 2d
            };
        }

        internal int ResearchAlienHeads => researchStatItemStacks.AlienHeads;
        internal int ResearchLightFlux => researchStatItemStacks.LightFlux;
        internal void SetResearchPassiveItemStats(bool powerOrb, int alienHeads, int lightFlux)
        {
            if (!isServer || (researchStatItemStacks.PowerOrb == powerOrb &&
                ResearchAlienHeads == alienHeads && ResearchLightFlux == lightFlux)) return;
            var next = researchStatItemStacks;
            next.PowerOrb = powerOrb;
            next.AlienHeads = alienHeads;
            next.LightFlux = lightFlux;
            researchStatItemStacks = next;
        }
        internal float ResearchAttackSpeed => (1f + RangeResearchItems.LanternAttackSpeedBonus(ResearchLanterns, ResearchLanternBodies) +
            .15f * ResearchSyringes + .075f * ResearchMochas + .12f * ResearchPredatoryBuffs +
            (ResearchBannerBuff ? .30f : 0f) + (ResearchWarHornActive ? .70f : 0f) +
            (ResearchBerserkerFrenzy ? 1f : 0f) + (researchStatItemStacks.PowerOrb ? .15f : 0f)) *
            (1f - BeetleJuicePenalty) * ResearchCollectorMultiplier / (1f + ResearchLightFlux);
        float rainRegenRemainder;

        [SyncVar] internal bool RaidDarkTidings;
        [SyncVar] internal int RaidSpitefulPaceBonus;
        [SyncVar] internal byte RaidTimeManipulationStacks;
        [SyncVar] internal double RaidMindSlowUntil;

        internal float RainRegenBeforeCollector => RainHealthRegen +
            (2.5f * ResearchStews + RangeResearchItems.KnurlRegenerationPerStack * ResearchKnurls + 3f * ResearchActiveSlugs) *
            (RainAttackPower > 0f ? RainAttackPower / RainBaseAttackPower : 1f) *
            (1f + (float)RangeResearchItems.QuickFixBonus(ResearchQuickFixes));

        internal float EffectiveRainHealthRegen => RainRegenBeforeCollector * ResearchCollectorMultiplier + HeroBonuses.regeneration;

        internal float EffectiveRainAttackPower => ((RainAttackPower > 0f ? RainAttackPower : RainBaseAttackPower) +
            (ResearchStews > 0 && CurrentHealth > 0 && CurrentHealth >= MaxHealth ? RainRegenBeforeCollector : 0f)) * ResearchCollectorMultiplier * (1f + HeroBonuses.damage);

        internal int ScaleRainWeaponDamage(int levelOneDamage)
        {
            levelOneDamage = Jotunnslayer.SkillBattlePreview.ScaleRaidWeaponDamage(this, levelOneDamage);
            if ((RainAttackPower <= 0 && ResearchStews == 0 && ResearchCollectorBuffs == 0 && HeroBonuses.damage == 0f) || levelOneDamage <= 0) return levelOneDamage;
            float coefficient = levelOneDamage / RainBaseAttackPower;
            return Mathf.Max(1, Mathf.RoundToInt(EffectiveRainAttackPower * coefficient));
        }

        [SyncVar] internal int RainEnemyKind;
        [SyncVar] internal uint RainDroneOwner;
        [SyncVar] internal string RainDroneId;
        Collider rainDroneHitbox;
        internal Collider RainMonsterAimCollider { get; set; }
        internal Vector3 CombatAimPoint => RainMonsterAimCollider is SphereCollider aimSphere && aimSphere
            ? aimSphere.transform.TransformPoint(aimSphere.center) : RainMonsterAimCollider ? RainMonsterAimCollider.bounds.center :
            rainDroneHitbox ? rainDroneHitbox.bounds.center : transform.position + Vector3.up;
        [SyncVar] internal double RainParentBlinkUntil;
        [SyncVar] internal byte RainEnemyAnimation;
        [SyncVar] internal double RainEnemyAnimationStartedAt;
        [SyncVar] internal double RainVerminPauseUntil;
        [SyncVar] internal float RainQueenWardCastSpeed = 1f;
        [SyncVar] internal byte RainTelegraph;
        [SyncVar] internal Vector3 RainTelegraphStart, RainTelegraphEnd;

        [ClientRpc]
        internal void RpcRainTitanFist(Vector3 point)
        {
            if (!isServer && CurrentHealth > 0 && TryGetComponent<RangeResearchEnemy>(out var enemy))
                enemy.PlayFistAt(point);
        }

        [ClientRpc]
        internal void RpcRainWispEmbers(Vector3 origin, Vector3 first, Vector3 second, Vector3 third)
        {
            if (!isServer && CurrentHealth > 0 && TryGetComponent<RangeResearchEnemy>(out var enemy))
                enemy.PlayWispTraces(origin, first, second, third);
        }

        static AudioClip royalCapacitorAudio;
        static AudioClip gupSpikesAudio;
        static AudioClip gupDeathAudio;

        [ClientRpc]
        internal void RpcRainGupDeath()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!gupDeathAudio) gupDeathAudio = Resources.Load<AudioClip>(RainActorVisual.GupResources + "OriginalGupDeath");
            if (gupDeathAudio) AudioSource.PlayClipAtPoint(gupDeathAudio, CombatAimPoint, .7f);
        }

        [ClientRpc]
        internal void RpcRainGupSpikes()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!gupSpikesAudio) gupSpikesAudio = Resources.Load<AudioClip>(RainActorVisual.GupResources + "OriginalGupAttack");
            if (gupSpikesAudio) AudioSource.PlayClipAtPoint(gupSpikesAudio, CombatAimPoint, .7f);
        }
        static AudioClip bearBlockAudio, saferSpacesBlockAudio;
        static AudioClip weepingFungusAudio, bustlingFungusAudio, medkitAudio;
        static AudioClip monsterToothPickupAudio, harpoonAudio, infusionAudio;
        static AudioClip watchBreakAudio, healingPotionAudio, scytheHealAudio, foreignFruitAudio;
        static AudioClip ocularHudStartAudio, ocularHudEndAudio;

        [ClientRpc]
        internal void RpcRainOcularHud(bool active)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!ocularHudStartAudio) ocularHudStartAudio = Resources.Load<AudioClip>("RainImported/OcularHudStart");
            if (!ocularHudEndAudio) ocularHudEndAudio = Resources.Load<AudioClip>("RainImported/OcularHudEnd");
            var clip = active ? ocularHudStartAudio : ocularHudEndAudio;
            if (clip) AudioSource.PlayClipAtPoint(clip, CombatAimPoint, .65f);
            if (active) ShowRainBlastRing("Rain ocular hud active", CombatAimPoint, .75f, new Color(.95f, .33f, .28f));
        }

        [ClientRpc]
        internal void RpcRainForeignFruit()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!foreignFruitAudio) foreignFruitAudio = Resources.Load<AudioClip>("RainImported/ForeignFruitOriginal");
            if (foreignFruitAudio) AudioSource.PlayClipAtPoint(foreignFruitAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain foreign fruit heal", transform.position + Vector3.up * .08f, 1.2f,
                new Color(.57f, .94f, .32f));
        }
        static AudioClip lostSeersAudio, tentabaubleAudio, chronobaubleAudio, jadeElephantAudio;

        [ClientRpc]
        internal void RpcRainJadeElephant()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!jadeElephantAudio) jadeElephantAudio = Resources.Load<AudioClip>("RainImported/JadeElephantOriginal");
            if (jadeElephantAudio) AudioSource.PlayClipAtPoint(jadeElephantAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain jade elephant armor", CombatAimPoint, 1.15f, new Color(.3f, .93f, .67f));
        }

        [ClientRpc]
        internal void RpcRainChronobauble(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!chronobaubleAudio) chronobaubleAudio = Resources.Load<AudioClip>("RainImported/ChronobaubleOriginal");
            if (chronobaubleAudio) AudioSource.PlayClipAtPoint(chronobaubleAudio, center, .65f);
            ShowRainBlastRing("Rain chronobauble slow", center + Vector3.up * .08f, .95f, new Color(.49f, .82f, 1f));
        }

        [ClientRpc]
        internal void RpcRainTentabauble(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!tentabaubleAudio) tentabaubleAudio = Resources.Load<AudioClip>("RainImported/TentabaubleOriginal");
            if (tentabaubleAudio) AudioSource.PlayClipAtPoint(tentabaubleAudio, center, .65f);
            ShowRainBlastRing("Rain tentabauble root", center + Vector3.up * .08f, .95f, new Color(.81f, .29f, .94f));
        }

        [ClientRpc]
        internal void RpcRainLostSeersExecution(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!lostSeersAudio) lostSeersAudio = Resources.Load<AudioClip>("RainImported/LostSeersExecution");
            if (lostSeersAudio) AudioSource.PlayClipAtPoint(lostSeersAudio, center, .7f);
            ShowRainBlastRing("Rain lost seers execution", center, 1.25f, new Color(.91f, .24f, .79f));
        }

        [ClientRpc]
        internal void RpcRainScytheHeal()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!scytheHealAudio) scytheHealAudio = Resources.Load<AudioClip>("RainImported/ScytheHealOriginal");
            if (scytheHealAudio) AudioSource.PlayClipAtPoint(scytheHealAudio, CombatAimPoint, .6f);
            ShowRainBlastRing("Rain scythe heal", transform.position + Vector3.up * .08f, .8f,
                new Color(.53f, .94f, .57f));
        }
        static AudioClip slugEmergeAudio, slugHideAudio, bucklerAudio;

        [ClientRpc]
        internal void RpcRainBucklerSprint()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!bucklerAudio) bucklerAudio = Resources.Load<AudioClip>("RainImported/RoseBucklerOriginal");
            if (bucklerAudio) AudioSource.PlayClipAtPoint(bucklerAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain rose buckler active", CombatAimPoint, .9f, new Color(.97f, .48f, .62f));
        }

        [ClientRpc]
        internal void RpcRainSoulboundCatalyst()
        {
            ShowRainBlastRing("Rain soulbound catalyst", transform.position + Vector3.up * .08f, .9f,
                new Color(.56f, .88f, 1f));
        }

        [ClientRpc]
        internal void RpcRainScorpionHit(Vector3 center)
        {
            ShowRainBlastRing("Rain scorpion armor loss", center, .7f, new Color(.92f, .52f, .23f));
        }

        [ClientRpc]
        internal void RpcRainChronicExpansion()
        {
            ShowRainBlastRing("Rain chronic expansion", transform.position + Vector3.up * .08f, 1f,
                new Color(.95f, .59f, .32f));
        }

        [ClientRpc]
        internal void RpcRainOpalImpact()
        {
            ShowRainBlastRing("Rain opal guard", CombatAimPoint, 1f, new Color(.87f, .72f, 1f));
        }

        [ClientRpc]
        internal void RpcRainParentEggHeal()
        {
            ShowRainBlastRing("Rain parent egg heal", transform.position + Vector3.up * .08f, .9f,
                new Color(.72f, .95f, .43f));
        }

        [ClientRpc]
        internal void RpcRainSlugState(bool active)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!slugEmergeAudio) slugEmergeAudio = Resources.Load<AudioClip>("RainImported/SlugEmerge");
            if (!slugHideAudio) slugHideAudio = Resources.Load<AudioClip>("RainImported/SlugHide");
            var clip = active ? slugEmergeAudio : slugHideAudio;
            if (clip) AudioSource.PlayClipAtPoint(clip, CombatAimPoint, .65f);
            if (active) ShowRainBlastRing("Rain cautious slug active", transform.position + Vector3.up * .08f,
                1f, new Color(.54f, .89f, .44f));
        }

        [ClientRpc]
        internal void RpcRainHealingPotion()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!healingPotionAudio) healingPotionAudio = Resources.Load<AudioClip>("RainImported/HealingPotionOriginal");
            if (healingPotionAudio) AudioSource.PlayClipAtPoint(healingPotionAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain healing potion", transform.position + Vector3.up * .08f, 1.2f,
                new Color(.46f, 1f, .62f));
        }

        [ClientRpc]
        internal void RpcRainWatchBreak()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!watchBreakAudio) watchBreakAudio = Resources.Load<AudioClip>("RainImported/DelicateWatchBreak");
            if (watchBreakAudio) AudioSource.PlayClipAtPoint(watchBreakAudio, CombatAimPoint, .7f);
            ShowRainBlastRing("Rain watch break", CombatAimPoint, .7f, new Color(1f, .73f, .4f));
        }

        [ClientRpc]
        internal void RpcRainInfusion(Vector3 origin)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!infusionAudio) infusionAudio = Resources.Load<AudioClip>("RainImported/InfusionOriginal");
            if (infusionAudio) AudioSource.PlayClipAtPoint(infusionAudio, CombatAimPoint, .65f);
            ShowRainAreaProc(origin + Vector3.up * .4f, new[] { CombatAimPoint }, "Rain infusion link",
                "Infusion trace", new Color(.95f, .24f, .34f));
        }

        [ClientRpc]
        internal void RpcRainHarpoon()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!harpoonAudio) harpoonAudio = Resources.Load<AudioClip>("RainImported/HarpoonOriginal");
            if (harpoonAudio) AudioSource.PlayClipAtPoint(harpoonAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain harpoon speed cue", transform.position + Vector3.up * .08f, 1.1f,
                new Color(.58f, .88f, 1f));
        }

        [ClientRpc]
        internal void RpcRainMonsterToothPickup()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!monsterToothPickupAudio)
                monsterToothPickupAudio = Resources.Load<AudioClip>("RainImported/MonsterToothPickup");
            if (monsterToothPickupAudio) AudioSource.PlayClipAtPoint(monsterToothPickupAudio, CombatAimPoint, .65f);
        }

        [ClientRpc]
        internal void RpcRainMedkit()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!medkitAudio) medkitAudio = Resources.Load<AudioClip>("RainImported/MedkitOriginal");
            if (medkitAudio) AudioSource.PlayClipAtPoint(medkitAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain medkit heal", transform.position + Vector3.up * .08f, 1f,
                new Color(.48f, .98f, .68f));
        }

        [ClientRpc]
        internal void RpcRainBustlingFungus(Vector3 center, float radius, bool started)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (started)
            {
                if (!bustlingFungusAudio) bustlingFungusAudio = Resources.Load<AudioClip>("RainImported/BustlingFungusStart");
                if (bustlingFungusAudio) AudioSource.PlayClipAtPoint(bustlingFungusAudio, center, .65f);
            }
            ShowRainBlastRing("Rain bustling fungus heal", center + Vector3.up * .08f, radius, new Color(.44f, .91f, .51f));
        }

        [ClientRpc]
        internal void RpcRainWeepingFungus()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!weepingFungusAudio) weepingFungusAudio = Resources.Load<AudioClip>("RainImported/WeepingFungusStart");
            if (weepingFungusAudio) AudioSource.PlayClipAtPoint(weepingFungusAudio, transform.position, .65f);
            ShowRainBlastRing("Rain weeping fungus heal", transform.position + Vector3.up * .08f, 1.2f,
                new Color(.62f, .94f, .68f));
        }

        [ClientRpc]
        internal void RpcRainSaferSpacesBlock()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!saferSpacesBlockAudio)
                saferSpacesBlockAudio = Resources.Load<AudioClip>("RainImported/SaferSpacesBlock");
            if (saferSpacesBlockAudio) AudioSource.PlayClipAtPoint(saferSpacesBlockAudio, CombatAimPoint, .7f);
            ShowRainBlastRing("Rain safer spaces shield", CombatAimPoint, 1.1f, new Color(.81f, .35f, 1f));
        }
        float nextBearBlockFeedback;

        [ClientRpc]
        internal void RpcRainBearBlock()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                Time.unscaledTime < nextBearBlockFeedback) return;
            nextBearBlockFeedback = Time.unscaledTime + .15f;
            if (!bearBlockAudio) bearBlockAudio = Resources.Load<AudioClip>("RainImported/BearBlockOriginal");
            if (bearBlockAudio) AudioSource.PlayClipAtPoint(bearBlockAudio, CombatAimPoint, .7f);
        }
        static readonly AudioClip[] predatoryProcAudio = new AudioClip[3];

        [ClientRpc]
        internal void RpcRainPredatoryProc(int level)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var clip = predatoryProcAudio[level];
            if (!clip)
                clip = predatoryProcAudio[level] = Resources.Load<AudioClip>("RainImported/PredatoryProc" + (level + 1));
            if (clip) AudioSource.PlayClipAtPoint(clip, CombatAimPoint, .65f);
        }

        [ClientRpc]
        internal void RpcRainLightningStrike(Vector3 point, bool royalCapacitor)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (royalCapacitor)
            {
                if (!royalCapacitorAudio)
                    royalCapacitorAudio = Resources.Load<AudioClip>("RainImported/RoyalCapacitorOriginal");
                if (royalCapacitorAudio) AudioSource.PlayClipAtPoint(royalCapacitorAudio, point, .7f);
            }
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return;

            var bolt = new GameObject("Rain lightning strike").AddComponent<LineRenderer>();
            var material = new Material(shader) { color = new Color(.62f, .9f, 1f) };
            bolt.sharedMaterial = material;
            bolt.useWorldSpace = true;
            bolt.widthMultiplier = .2f;
            bolt.positionCount = 5;
            bolt.SetPosition(0, point + new Vector3(-.45f, 8f, .2f));
            bolt.SetPosition(1, point + new Vector3(.3f, 6f, -.2f));
            bolt.SetPosition(2, point + new Vector3(-.25f, 4f, .15f));
            bolt.SetPosition(3, point + new Vector3(.18f, 2f, .15f));
            bolt.SetPosition(4, point);
            Destroy(bolt.gameObject, .18f);
            Destroy(material, .18f);
        }

        [ClientRpc]
        internal void RpcRainUrnTethers(List<uint> targetIds)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return;

            var material = new Material(shader) { color = new Color(.58f, .42f, .16f) };
            foreach (uint targetId in targetIds)
            {
                if (!NetworkClient.spawned.TryGetValue(targetId, out var identity) ||
                    !identity || !identity.TryGetComponent<Health>(out var target) || target.CurrentHealth <= 0)
                    continue;
                var tether = new GameObject("Rain mired urn tether").AddComponent<LineRenderer>();
                tether.transform.SetParent(transform, false);
                tether.sharedMaterial = material;
                tether.useWorldSpace = true;
                tether.positionCount = 3;
                tether.startWidth = .1f;
                tether.endWidth = .045f;
                Destroy(tether.gameObject, 1f);
                StartCoroutine(AnimateRainUrnTether(tether, target));
            }
            Destroy(material, 1f);
        }

        System.Collections.IEnumerator AnimateRainUrnTether(LineRenderer tether, Health target)
        {
            while (tether && target && CurrentHealth > 0 && target.CurrentHealth > 0)
            {
                Vector3 start = transform.position + Vector3.up;
                Vector3 end = target.transform.position + Vector3.up;
                if ((start - end).sqrMagnitude > 13f * 13f) break;
                tether.SetPosition(0, start);
                tether.SetPosition(1, (start + end) * .5f - Vector3.up * .45f);
                tether.SetPosition(2, end);
                yield return null;
            }
            if (tether) Destroy(tether.gameObject);
        }

        [ClientRpc]
        internal void RpcRainBlastShower(Vector3 center, float radius)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            ShowRainBlastRing("Rain blast shower ring", center, radius, new Color(.55f, 1f, .84f));
        }

        static AudioClip behemothBlastAudio;
        static AudioClip daggerBleedAudio;
        static AudioClip deathMarkAudio;
        static AudioClip crowbarAudio, focusCrystalAudio;

        [ClientRpc]
        internal void RpcRainFocusCrystal(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!focusCrystalAudio) focusCrystalAudio = Resources.Load<AudioClip>("RainImported/FocusCrystalOriginal");
            if (focusCrystalAudio) AudioSource.PlayClipAtPoint(focusCrystalAudio, center, .55f);
            ShowRainBlastRing("Rain focus crystal hit", center, .5f, new Color(.96f, .25f, .35f));
        }
        static AudioClip cloverRerollAudio;
        static AudioClip warHornAudio;
        static AudioClip iceRingAudio;
        static AudioClip fireRingAudio;
        static AudioClip wispItemBlastAudio;
        static AudioClip gasolineAudio;
        static AudioClip voidsentFlameAudio;

        [ClientRpc]
        internal void RpcRainVoidsentFlame(Vector3 position, float radius)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!voidsentFlameAudio) voidsentFlameAudio = Resources.Load<AudioClip>("RainImported/VoidsentFlameOriginal");
            if (voidsentFlameAudio) AudioSource.PlayClipAtPoint(voidsentFlameAudio, position, .7f);
            ShowRainBlastRing("Rain voidsent flame blast", position + Vector3.up * .08f, radius, new Color(.93f, .22f, .65f));
        }

        [ClientRpc]
        internal void RpcRainGasolineBlast(Vector3 position, float radius)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!gasolineAudio) gasolineAudio = Resources.Load<AudioClip>("RainImported/GasolineOriginal");
            if (gasolineAudio) AudioSource.PlayClipAtPoint(gasolineAudio, position, .7f);
            ShowRainBlastRing("Rain gasoline blast", position + Vector3.up * .08f, radius, new Color(1f, .5f, .1f));
        }

        [ClientRpc]
        internal void RpcRainWispItemBlast(Vector3 position, float radius, bool exploded)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (exploded)
            {
                if (!wispItemBlastAudio) wispItemBlastAudio = Resources.Load<AudioClip>("RainImported/WispItemExplosion");
                if (wispItemBlastAudio) AudioSource.PlayClipAtPoint(wispItemBlastAudio, position, .7f);
            }
            ShowRainBlastRing(exploded ? "Rain wisp item blast" : "Rain wisp item warning", position + Vector3.up * .08f,
                radius, exploded ? new Color(1f, .42f, .12f) : new Color(.85f, .23f, .12f));
        }
        static AudioClip ceremonialDaggerSpawnAudio, ceremonialDaggerImpactAudio;

        [ClientRpc]
        internal void RpcPlayRainCeremonialDaggerEffect(Vector3 position, bool impact)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (impact)
            {
                if (!ceremonialDaggerImpactAudio)
                    ceremonialDaggerImpactAudio = Resources.Load<AudioClip>("RainImported/CeremonialDaggerImpact");
                if (ceremonialDaggerImpactAudio) AudioSource.PlayClipAtPoint(ceremonialDaggerImpactAudio, position, .48f);
            }
            else
            {
                if (!ceremonialDaggerSpawnAudio)
                    ceremonialDaggerSpawnAudio = Resources.Load<AudioClip>("RainImported/CeremonialDaggerSpawn");
                if (ceremonialDaggerSpawnAudio) AudioSource.PlayClipAtPoint(ceremonialDaggerSpawnAudio, position, .7f);
            }
        }

        [ClientRpc]
        internal void RpcRainFireRing(Vector3 center, Vector3 direction)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!fireRingAudio) fireRingAudio = Resources.Load<AudioClip>("RainImported/FireRingOriginal");
            if (fireRingAudio) AudioSource.PlayClipAtPoint(fireRingAudio, center, .7f);
            var ring = CreateRainBlastRing("Rain fire tornado cue", center + Vector3.up * .08f, 8f, new Color(1f, .27f, .07f));
            if (!ring) return;
            Destroy(ring.sharedMaterial, 3.15f);
            Destroy(ring.gameObject, 3.15f);
            StartCoroutine(AnimateRainFireRing(ring, direction));
        }

        System.Collections.IEnumerator AnimateRainFireRing(LineRenderer ring, Vector3 direction)
        {
            Vector3 origin = ring.transform.position;
            float started = Time.time;
            while (ring)
            {
                ring.transform.position = origin + direction * Mathf.Min(6f, Mathf.Floor((Time.time - started) * 2f));
                yield return null;
            }
        }

        [ClientRpc]
        internal void RpcRainIceRing(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!iceRingAudio) iceRingAudio = Resources.Load<AudioClip>("RainImported/IceRingOriginal");
            if (iceRingAudio) AudioSource.PlayClipAtPoint(iceRingAudio, center, .65f);
            ShowRainBlastRing("Rain ice ring cue", center + Vector3.up * .1f, 1.25f, new Color(.46f, .82f, 1f));
        }

        [ClientRpc]
        internal void RpcRainWarHorn()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!warHornAudio) warHornAudio = Resources.Load<AudioClip>("RainImported/WarHornOriginal");
            if (warHornAudio) AudioSource.PlayClipAtPoint(warHornAudio, CombatAimPoint, .7f);
            ShowRainBlastRing("Rain war horn cue", transform.position + Vector3.up * .08f, 1.5f, new Color(1f, .68f, .2f));
        }

        [ClientRpc]
        internal void RpcRainCloverReroll()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!cloverRerollAudio) cloverRerollAudio = Resources.Load<AudioClip>("RainImported/CloverRerollOriginal");
            if (cloverRerollAudio) AudioSource.PlayClipAtPoint(cloverRerollAudio, CombatAimPoint, .65f);
            ShowRainBlastRing("Rain clover reroll cue", transform.position + Vector3.up * .08f, .7f, new Color(.25f, 1f, .48f));
        }

        [ClientRpc]
        internal void RpcRainCrowbarImpact(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!crowbarAudio) crowbarAudio = Resources.Load<AudioClip>("RainImported/CrowbarOriginal");
            if (crowbarAudio) AudioSource.PlayClipAtPoint(crowbarAudio, center, .65f);
            ShowRainBlastRing("Rain crowbar impact", center, .35f, new Color(1f, .75f, .25f));
        }

        [ClientRpc]
        internal void RpcRainDeathMark(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!deathMarkAudio) deathMarkAudio = Resources.Load<AudioClip>("RainImported/DeathMarkOriginal");
            if (deathMarkAudio) AudioSource.PlayClipAtPoint(deathMarkAudio, center, .7f);
            ShowRainBlastRing("Rain death mark cue", center, .65f, new Color(.68f, .18f, .86f));
        }
        static AudioClip shatterspleenImpactAudio, shatterspleenBlastAudio;

        [ClientRpc]
        internal void RpcRainShatterspleen(Vector3 center, float radius)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (radius > 0f)
            {
                if (!shatterspleenBlastAudio) shatterspleenBlastAudio = Resources.Load<AudioClip>("RainImported/ShatterspleenBlast");
                if (shatterspleenBlastAudio) AudioSource.PlayClipAtPoint(shatterspleenBlastAudio, center, .7f);
                ShowRainBlastRing("Rain Shatterspleen blast", center + Vector3.up * .08f, radius, new Color(.8f, .08f, .12f));
            }
            else
            {
                if (!shatterspleenImpactAudio) shatterspleenImpactAudio = Resources.Load<AudioClip>("RainImported/ShatterspleenImpact");
                if (shatterspleenImpactAudio) AudioSource.PlayClipAtPoint(shatterspleenImpactAudio, center, .65f);
            }
        }

        [ClientRpc]
        internal void RpcRainDaggerBleed(Vector3 center)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!daggerBleedAudio) daggerBleedAudio = Resources.Load<AudioClip>("RainImported/DaggerBleedOriginal");
            if (daggerBleedAudio) AudioSource.PlayClipAtPoint(daggerBleedAudio, center, .65f);
            ShowRainBlastRing("Rain dagger bleed cue", center, .4f, new Color(.9f, .1f, .16f));
        }

        [ClientRpc]
        internal void RpcRainBehemothBlast(Vector3 center, float radius)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!behemothBlastAudio) behemothBlastAudio = Resources.Load<AudioClip>("RainImported/BehemothBlastOriginal");
            if (behemothBlastAudio) AudioSource.PlayClipAtPoint(behemothBlastAudio, center, .65f);
            ShowRainBlastRing("Rain Behemoth blast ring", center + Vector3.up * .08f, radius, new Color(1f, .48f, .13f));
        }

        void ShowRainBlastRing(string effectName, Vector3 center, float radius, Color color)
        {
            var ring = CreateRainBlastRing(effectName, center, radius, color);
            if (!ring) return;
            const float lifetime = .45f;
            Destroy(ring.sharedMaterial, lifetime);
            Destroy(ring.gameObject, lifetime);
            StartCoroutine(AnimateRainBlastShower(ring, lifetime));
        }

        static LineRenderer CreateRainBlastRing(string effectName, Vector3 center, float radius, Color color)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return null;

            var ring = new GameObject(effectName).AddComponent<LineRenderer>();
            ring.transform.position = center;
            ring.sharedMaterial = new Material(shader) { color = color };
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = .09f;
            ring.positionCount = 32;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ring.positionCount);
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            return ring;
        }

        System.Collections.IEnumerator AnimateRainBlastShower(LineRenderer ring, float lifetime)
        {
            float startedAt = Time.time;
            while (ring)
            {
                ring.transform.localScale = Vector3.one * Mathf.Lerp(.1f, 1f, (Time.time - startedAt) / lifetime);
                yield return null;
            }
        }

        [ClientRpc]
        internal void RpcRainAreaProc(Vector3 origin, List<Vector3> endpoints, bool tesla)
            => ShowRainAreaProc(origin, endpoints, tesla ? "Rain Tesla pulse" : "Rain razorwire burst",
                tesla ? "Tesla arc" : "Razorwire trace", tesla ? new Color(.46f, .84f, 1f) : new Color(1f, .74f, .52f));

        static AudioClip ukuleleArcAudio;
        static AudioClip polyluteAudio;
        static AudioClip stickyBombAttachAudio, stickyBombCountdownAudio;

        [ClientRpc]
        internal void RpcRainStunGrenade(Vector3 center)
        {
            ShowRainBlastRing("Rain stun grenade cue", center + Vector3.up * 1.2f, .65f,
                new Color(1f, .83f, .28f));
        }

        [ClientRpc]
        internal void RpcRainStickyBomb(Vector3 center, bool exploded)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!exploded)
            {
                if (!stickyBombAttachAudio) stickyBombAttachAudio = Resources.Load<AudioClip>("RainImported/StickyBombAttach");
                if (!stickyBombCountdownAudio)
                    stickyBombCountdownAudio = Resources.Load<AudioClip>("RainImported/StickyBombCountdown");
                if (stickyBombAttachAudio) AudioSource.PlayClipAtPoint(stickyBombAttachAudio, center, .7f);
                if (stickyBombCountdownAudio) AudioSource.PlayClipAtPoint(stickyBombCountdownAudio, center, .6f);
            }
            ShowRainBlastRing(exploded ? "Rain sticky bomb blast" : "Rain sticky bomb attached", center + Vector3.up * .08f,
                exploded ? 4f : .6f, exploded ? new Color(1f, .46f, .16f) : new Color(.98f, .74f, .23f));
        }
        static AudioClip needletickStartAudio, needletickBurstAudio;

        [ClientRpc]
        internal void RpcRainNeedletick(Vector3 center, bool burst)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!needletickStartAudio) needletickStartAudio = Resources.Load<AudioClip>("RainImported/NeedletickStart");
            if (!needletickBurstAudio) needletickBurstAudio = Resources.Load<AudioClip>("RainImported/NeedletickBurst");
            var clip = burst ? needletickBurstAudio : needletickStartAudio;
            if (clip) AudioSource.PlayClipAtPoint(clip, center, .65f);
            ShowRainBlastRing(burst ? "Rain needletick burst" : "Rain needletick mark", center + Vector3.up * .08f,
                burst ? 1.4f : .7f, burst ? new Color(.95f, .32f, .82f) : new Color(.68f, .28f, 1f));
        }

        [ClientRpc]
        internal void RpcRainPolyluteStrike(Vector3 endpoint, bool firstStrike)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (firstStrike)
            {
                if (!polyluteAudio) polyluteAudio = Resources.Load<AudioClip>("RainImported/PolyluteOriginal");
                if (polyluteAudio) AudioSource.PlayClipAtPoint(polyluteAudio, endpoint, .6f);
            }
            ShowRainAreaProc(endpoint + Vector3.up * 3f, new[] { endpoint }, "Rain Polylute strike",
                "Polylute trace", new Color(.91f, .27f, .82f));
        }

        [ClientRpc]
        internal void RpcRainUkuleleArc(Vector3 origin, Vector3 endpoint)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!ukuleleArcAudio) ukuleleArcAudio = Resources.Load<AudioClip>("RainImported/UkuleleArcOriginal");
            if (ukuleleArcAudio) AudioSource.PlayClipAtPoint(ukuleleArcAudio, endpoint, .6f);
            ShowRainAreaProc(origin, new[] { endpoint }, "Rain Ukulele arc", "Ukulele trace", new Color(.42f, .82f, 1f));
        }

        void ShowRainAreaProc(Vector3 origin, IList<Vector3> endpoints, string effectName, string traceName, Color color)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return;

            const float lifetime = .14f;
            var burst = new GameObject(effectName);
            var material = new Material(shader) { color = color };
            for (int i = 0; i < endpoints.Count; i++)
            {
                Vector3 endpoint = endpoints[i];
                var wire = new GameObject(traceName).AddComponent<LineRenderer>();
                wire.transform.SetParent(burst.transform, false);
                wire.sharedMaterial = material;
                wire.useWorldSpace = true;
                wire.startWidth = .055f;
                wire.endWidth = .02f;
                wire.positionCount = 4;
                wire.SetPosition(0, origin);
                wire.SetPosition(1, Vector3.Lerp(origin, endpoint, .33f) + Vector3.up * .24f);
                wire.SetPosition(2, Vector3.Lerp(origin, endpoint, .66f) - Vector3.up * .12f);
                wire.SetPosition(3, endpoint);
            }
            Destroy(burst, lifetime);
            Destroy(material, lifetime);
        }

        sealed class ItemProjectileTrail
        {
            internal TrailRenderer Renderer;
            internal Health Target;
            internal Vector3 Velocity;
            internal bool Falling, Finished;
        }

        readonly Dictionary<uint, ItemProjectileTrail> itemProjectileTrails = new Dictionary<uint, ItemProjectileTrail>();

        [SyncVar(hook = nameof(OnRaidSoulmateChanged))]
        internal bool RaidSoulmateActive;

        private void OnRaidSoulmateChanged(bool previous, bool active)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!TryGetComponent<Jotunnslayer.SoulmateVisual>(out var visual))
            {
                if (!active) return;
                visual = gameObject.AddComponent<Jotunnslayer.SoulmateVisual>();
            }
            visual.SetVisible(active);
        }

        [ClientRpc]
        internal void RpcRaidSoulmateAttack(Health target, Vector3 point, bool windingUp)
        {
            if (!RaidSoulmateActive) return;
            OnRaidSoulmateChanged(false, true);
            if (TryGetComponent<Jotunnslayer.SoulmateVisual>(out var visual))
            {
                if (windingUp) visual.WindUp();
                else visual.Strike(target, point);
            }
        }

        [ClientRpc]
        internal void RpcRaidProjectileTrail(uint id, Vector3 position, bool finished, bool heartArrow)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!TryGetComponent<Jotunnslayer.MagicMissileTrails>(out var trails))
            {
                if (finished) return;
                trails = gameObject.AddComponent<Jotunnslayer.MagicMissileTrails>();
            }
            trails.Receive(id, position, finished, heartArrow);
        }

        [ClientRpc]
        internal void RpcRainItemProjectileFlight(uint id, Vector3 position, Vector3 velocity, Health target, bool falling, bool molten)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (itemProjectileTrails.TryGetValue(id, out var flight))
            {
                if (!flight.Renderer || flight.Finished) return;
                flight.Renderer.transform.position = position;
                flight.Target = target;
                flight.Velocity = velocity;
                flight.Falling = falling;
                return;
            }

            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return;
            var trail = new GameObject(molten ? "Rain molten ball trail" : "Rain firework trail").AddComponent<TrailRenderer>();
            trail.transform.SetParent(transform, true);
            trail.transform.position = position;
            var material = new Material(shader) { color = molten ? new Color(1f, .42f, .12f) : new Color(1f, .34f, .62f) };
            trail.sharedMaterial = material;
            trail.time = .22f;
            trail.startWidth = .15f;
            trail.endWidth = .025f;

            flight = new ItemProjectileTrail { Renderer = trail, Target = target, Velocity = velocity, Falling = falling };
            itemProjectileTrails.Add(id, flight);
            float lifetime = molten ? 1.8f : 16.5f;
            Destroy(trail.gameObject, lifetime);
            Destroy(material, lifetime);
            StartCoroutine(AnimateRainItemProjectile(id, flight));
        }

        [ClientRpc]
        internal void RpcRainItemProjectileImpact(uint id, Vector3 point)
        {
            if (!itemProjectileTrails.TryGetValue(id, out var flight) || !flight.Renderer) return;
            flight.Renderer.transform.position = point;
            flight.Finished = true;
        }

        System.Collections.IEnumerator AnimateRainItemProjectile(uint id, ItemProjectileTrail flight)
        {
            var trail = flight.Renderer;
            while (trail && !flight.Finished && CurrentHealth > 0)
            {
                yield return null;
                if (!trail || flight.Finished || CurrentHealth <= 0) break;
                Vector3 position = trail.transform.position;
                float step = Time.deltaTime;
                if (flight.Target && flight.Target.CurrentHealth > 0 && flight.Target.Team != Team)
                {
                    Vector3 destination = flight.Target.transform.position + Vector3.up;
                    flight.Velocity = (destination - position).normalized * 30f;
                    position = Vector3.MoveTowards(position, destination, 30f * step);
                }
                else
                {
                    if (flight.Falling) flight.Velocity += Physics.gravity * step;
                    position += flight.Velocity * step;
                }
                trail.transform.position = position;
            }

            itemProjectileTrails.Remove(id);
            if (trail)
            {
                trail.emitting = false;
                Destroy(trail.sharedMaterial, trail.time);
                Destroy(trail.gameObject, trail.time);
            }
        }

        [ClientRpc]
        internal void RpcRainAtgAudio(Vector3 center, bool impact)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!atgFireAudio) atgFireAudio = Resources.Load<AudioClip>("RainImported/AtgFire");
            if (!atgImpactAudio) atgImpactAudio = Resources.Load<AudioClip>("RainImported/AtgImpact");
            var clip = impact ? atgImpactAudio : atgFireAudio;
            if (clip) AudioSource.PlayClipAtPoint(clip, center, .65f);
        }

        [ClientRpc]
        internal void RpcRainHomingItemShot(Vector3 origin, Health target, RangeProcSource source, bool plasmaShrimp)
        {
            if (!target || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return;

            var shot = new GameObject(plasmaShrimp ? "Rain plasma shrimp trail" : "Rain homing item trail").AddComponent<TrailRenderer>();
            shot.transform.SetParent(transform, true);
            shot.transform.position = origin;
            bool dagger = source == RangeProcSource.Dagger;
            Color color = source switch
            {
                RangeProcSource.Missile => plasmaShrimp ? new Color(.94f, .26f, .8f) : new Color(1f, .62f, .2f),
                RangeProcSource.Dagger => new Color(.78f, .44f, 1f),
                _ => new Color(.52f, 1f, .6f)
            };
            var material = new Material(shader) { color = color };
            LineRenderer blade = null;
            if (dagger)
            {
                blade = new GameObject("Rain ceremonial dagger blade").AddComponent<LineRenderer>();
                blade.transform.SetParent(shot.transform, false);
                blade.sharedMaterial = material;
                blade.useWorldSpace = false;
                blade.widthMultiplier = .045f;
                blade.positionCount = 7;
                blade.SetPosition(0, new Vector3(0f, 0f, -.22f));
                blade.SetPosition(1, new Vector3(0f, 0f, -.1f));
                blade.SetPosition(2, new Vector3(-.12f, 0f, -.1f));
                blade.SetPosition(3, new Vector3(0f, 0f, .32f));
                blade.SetPosition(4, new Vector3(.12f, 0f, -.1f));
                blade.SetPosition(5, new Vector3(0f, 0f, -.1f));
                blade.SetPosition(6, new Vector3(0f, 0f, -.22f));
            }
            shot.sharedMaterial = material;
            shot.time = .2f;
            shot.startWidth = .18f;
            shot.endWidth = .02f;
            Destroy(shot.gameObject, 4.2f);
            Destroy(material, 4.2f);
            StartCoroutine(AnimateRainHomingItemShot(shot, target, blade));
        }

        System.Collections.IEnumerator AnimateRainHomingItemShot(TrailRenderer shot, Health target, LineRenderer blade)
        {
            Vector3 position = shot.transform.position;
            while (shot && target && target.CurrentHealth > 0 && CurrentHealth > 0)
            {
                yield return null;
                if (!shot || !target || target.CurrentHealth <= 0 || CurrentHealth <= 0) break;
                Vector3 destination = target.transform.position + Vector3.up;
                Vector3 next = Vector3.MoveTowards(position, destination, 30f * Time.deltaTime);
                if (Physics.Linecast(position, next, GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) break;
                if (blade && (next - position).sqrMagnitude > .000001f)
                    shot.transform.forward = next - position;
                shot.transform.position = next;
                position = next;
                if ((next - destination).sqrMagnitude <= .01f) break;
            }
            if (shot)
            {
                if (blade) blade.enabled = false;
                shot.emitting = false;
                Destroy(shot.sharedMaterial, .2f);
                Destroy(shot.gameObject, .2f);
            }
        }

        [ClientRpc]
        internal void RpcRainJellyfishNova(Vector3 center)
        {
            if (!isServer) RangeResearchEnemy.PlayJellyfishNova(center);
        }

        [ClientRpc]
        internal void RpcRainWispDeath(Vector3 center, Quaternion rotation)
        {
            if (!isServer) RangeResearchEnemy.PlayWispDeath(center, rotation);
        }

        public struct QueenSpitState
        {
            public Vector3 Origin;
            public Vector3 Velocity;
            public double StartedAt;
        }

        internal readonly SyncDictionary<int, QueenSpitState> RainQueenSpit =
            new SyncDictionary<int, QueenSpitState>();

        public struct QueenAcidPoolState
        {
            public Vector3 Position;
            public Quaternion Rotation;
        }

        internal readonly SyncDictionary<int, QueenAcidPoolState> RainQueenAcidPools =
            new SyncDictionary<int, QueenAcidPoolState>();

        public struct RainLinearShotState
        {
            public Vector3 Origin;
            public Vector3 Direction;
            public double StartedAt;
        }

        internal readonly SyncDictionary<int, RainLinearShotState> RainBruiserFireballs =
            new SyncDictionary<int, RainLinearShotState>();

        internal readonly SyncDictionary<int, RainLinearShotState> RainGreaterWispShots =
            new SyncDictionary<int, RainLinearShotState>();

        internal readonly SyncDictionary<int, RainLinearShotState> RainFlyingVerminShots =
            new SyncDictionary<int, RainLinearShotState>();

        internal readonly SyncDictionary<int, RainLinearShotState> RainLunarExploderShards =
            new SyncDictionary<int, RainLinearShotState>();

        [ClientRpc]
        internal void RpcRainLunarExploderShardImpact(Vector3 point, Vector3 normal)
        {
            if (!isServer) RangeResearchEnemy.PlayLunarShardImpact(point, normal);
        }

        public struct RainHermitMortarState
        {
            public Vector3 Origin, Velocity;
            public double StartedAt;
        }

        internal readonly SyncDictionary<int, RainHermitMortarState> RainHermitMortars =
            new SyncDictionary<int, RainHermitMortarState>();

        [ClientRpc]
        internal void RpcRainHermitMortarImpact(Vector3 point, Vector3 normal)
        {
            if (!isServer) RangeResearchEnemy.PlayHermitMortarImpact(point, normal);
        }

        [ClientRpc]
        internal void RpcRainFlyingVerminImpact(Vector3 point, Vector3 normal)
        {
            if (!isServer) RangeResearchEnemy.PlayFlyingVerminImpact(point, normal);
        }

        [ClientRpc]
        internal void RpcRainFlyingVerminDeath(Vector3 point)
        {
            if (!isServer) RangeResearchEnemy.PlayFlyingVerminDeath(point);
        }

        [ClientRpc]
        internal void RpcRainGreaterWispImpact(Vector3 point)
        {
            if (!isServer) RangeResearchEnemy.PlayGreaterWispImpact(point);
        }

        [ClientRpc]
        internal void RpcRainImpBlinkEffect(Vector3 origin, Quaternion rotation)
        {
            if (!isServer) RangeResearchEnemy.PlayImpBlinkEffect(origin, rotation);
        }

        [ClientRpc]
        internal void RpcRainImpSwipe(bool right)
        {
            if (!isServer && TryGetComponent<RangeResearchEnemy>(out var actor)) actor.PlayImpSwipe(right);
        }

        [ClientRpc]
        internal void RpcRainImpSwipeImpact(Vector3 position, Quaternion rotation)
        {
            if (!isServer) RangeResearchEnemy.PlayImpSwipeImpact(position, rotation);
        }

        [ClientRpc]
        internal void RpcRainVerminLashEffect()
        {
            if (!isServer && TryGetComponent<RangeResearchEnemy>(out var actor)) actor.PlayVerminLashEffect();
        }

        [ClientRpc]
        internal void RpcRainBruiserFireballImpact(Vector3 point)
        {
            if (!isServer) RangeResearchEnemy.PlayBruiserFireballImpact(point);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            if (RaidSoulmateActive) OnRaidSoulmateChanged(false, true);
            if (ResearchFrostRelicRadius > 0f && !frostRelicRing) OnFrostRelicRadiusChanged(0f, ResearchFrostRelicRadius);
            if (!isServer && RainEnemyKind > 0)
                gameObject.AddComponent<RangeResearchEnemy>().ConfigureReplica(RainEnemyKind - 1);
            if (!isServer && RainDroneOwner != 0)
                RangeDrone.ConfigureReplica(this);
        }

        void OnFrostRelicRadiusChanged(float previous, float radius)
        {
            if (radius <= 0f)
            {
                ClearFrostRelicRing();
                return;
            }
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            if (!frostRelicRing)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (!shader) return;

                frostRelicRing = new GameObject("Rain frost relic ring").AddComponent<LineRenderer>();
                frostRelicRing.transform.SetParent(transform, false);
                frostRelicRing.transform.localPosition = Vector3.up * .15f;
                frostRelicRing.sharedMaterial = new Material(shader) { color = new Color(.53f, .87f, 1f) };
                frostRelicRing.useWorldSpace = false;
                frostRelicRing.loop = true;
                frostRelicRing.widthMultiplier = .13f;
                frostRelicRing.positionCount = 48;
            }
            for (int i = 0; i < frostRelicRing.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / frostRelicRing.positionCount);
                frostRelicRing.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }

        void ClearFrostRelicRing()
        {
            if (!frostRelicRing) return;
            Destroy(frostRelicRing.sharedMaterial);
            Destroy(frostRelicRing.gameObject);
            frostRelicRing = null;
        }

        internal void ConfigureDroneHitbox(Bounds bounds)
        {
            _hitboxes.DisableHitboxes();
            var hitbox = new GameObject("Drone hitbox");
            hitbox.layer = (int)GameLayers.hitbox;
            hitbox.transform.SetParent(transform, false);
            hitbox.transform.position = bounds.center;
            var collider = hitbox.AddComponent<SphereCollider>();
            collider.radius = Mathf.Max(.3f, bounds.extents.magnitude);
            var damageReceiver = hitbox.AddComponent<HitBox>();
            damageReceiver._health = this;
            damageReceiver.part = CharacterPart.body;
            rainDroneHitbox = collider;
            collider.enabled = CurrentHealth > 0;
        }
        private GameObject _myUIMarker;
        public Transform CharacterMarkerPosition;

        [HideInInspector] public Transform FPPCameraTarget; //Transform that player's camera will stick to

        /// <summary>
        /// hand to place item in
        /// </summary>
        public Transform ItemTarget;

        /// <summary>
        /// items can stick to character model in wrong direction, so adjusting this varable will correct that
        /// </summary>
        public Vector3 itemRotationCorrector;


        [SerializeField] GameObject CharacterModel;


        public delegate void CharacterEvent_SetAsBOT(bool _set);
        public CharacterEvent_SetAsBOT Server_SetAsBOT;

        public Transform characterMind; //this objects indicated direction character is looking at
        public Transform characterFirePoint; //this object is child of characterMind, will be used for recoil of weapons


        /// <summary>
        /// character controller
        /// </summary>
        private CharacterMotor _motor;
        internal bool RaidControlStopsActions => (_motor && _motor.IsFourWayRolling) || CrowdControlStopsActions;
        internal bool CrowdControlStopsActions => (isServer
            ? Jotunnslayer.SkillBattlePreview.IsStoppedByRaidControl(this, Time.time)
            : _motor && _motor.RaidControlStopsActions);

        /// <summary>
        /// hitbox prefab to assign to player model
        /// </summary>
        [SerializeField] GameObject _hitBoxContainerPrefab;
        HitboxSetup _hitboxes;

        [HideInInspector] public PlayerRecoil PlayerRecoil;

        public bool IsUsingItem = false;

        public bool IsRunning { get; set; }


        public bool Block { private set; get; } = false; //determines if character can move and shoot or not, block if it is end of round

        /// <summary>
        /// only true for character that is controlled by client, so only for player controller
        /// </summary>
        public bool IsObserved { set; get; }

        /// <summary>
        /// indicates if character is controlled by server or client
        /// </summary>
        public bool BOT = false;

        Health _killer;

        [HideInInspector]public CharacterItemManager CharacterItemManager;

        [Header("Player/Bot input")]
        #region input to synchronize
        public Vector2 lookInput;
        public Vector2 movementInput { get; private set; }
        byte _actionCode;

        #endregion
        //this flag is needed to determine if we need to animate upper part of character to show reload animation while running
        public bool IsReloading = false;
        public bool IsAiming;

        public delegate void CharacterEvent_OnPickedupObject(string message);
        public CharacterEvent_OnPickedupObject Client_OnPickedupObject { get; set; }


        public delegate void CharacterEvent(float time);
        public CharacterEvent CharacterEvent_OnItemUsed { get; set; }

        public CharacterAnimator CharacterAnimator { private set; get; }
        public bool IsCrouching { get; internal set; }
        public bool IsGrounded { get; set; }

        protected override void Awake()
        {
            base.Awake();
            CharacterAnimator = GetComponent<CharacterAnimator>();
            CharacterItemManager = GetComponent<CharacterItemManager>();
            PlayerRecoil = GetComponent<PlayerRecoil>();
            _motor = GetComponent<CharacterMotor>();

            CustomSceneManager.RegisterCharacter(this);//we have to register spawned characters in order to let bot "see" them, and select nearest enemies from that register

            Server_OnHealthDepleted += ServerDeath;
            Client_OnHealthStateChanged += ClientOnHealthStateChanged;

            _hitboxes = Instantiate(_hitBoxContainerPrefab, transform.position, transform.rotation).GetComponent<HitboxSetup>();
            _hitboxes.SetHiboxes(CharacterModel, this);
            _hitboxes.transform.SetParent(transform);
        }
        protected override void Start()
        {
            base.Start();

            RoomManager.RoomTick += CharacterInstance_Tick;

            lookInput.y = transform.eulerAngles.y; //assigning start look rotation to spawnpoint rotation

            ObserveCharacter(isOwned);

            if (isOwned)
            {
                ClientFrontend.SetObservedCharacter(this);
                IsObserved = true;
            }

            gameObject.layer = 6; //setting apppropriate layer for character collisions
            GameManager.SetLayerRecursively(CharacterModel, 8);

            //spawning teammate marker
            var battlefieldEntity = GetComponent<MTPSKIT.Battlefield.BattlefieldEntity>();
            bool hasMinionHealthBar = battlefieldEntity && battlefieldEntity.Kind == MTPSKIT.Battlefield.BattlefieldKind.Minion;
            if (!hasMinionHealthBar && !_myUIMarker && !isOwned && Team == ClientFrontend.ThisClientTeam && !GameManager.Gamemode.FFA)
            {
                _myUIMarker = Instantiate(UICharacter._instance.UIMarkerPrefab);
                _myUIMarker.transform.SetParent(UICharacter._instance.WorldIconBorders.transform);
                _myUIMarker.GetComponent<PlayerNametag>().SetupNameplate(this);
            }

        }

        void Update()
        {
            if (isServer && CurrentHealth <= 0 && beetleJuice.Stacks > 0)
                beetleJuice = default;
            if (isServer && (ResearchShieldGenerators > 0 || ResearchPlasmaShrimps > 0) && CurrentHealth > 0)
            {
                if (ResearchShieldCapacity != previousResearchShieldCapacity) RefreshResearchShield();
                if (Time.time >= researchShieldRechargeAt && researchShieldCharge < previousResearchShieldCapacity)
                {
                    if (!researchShieldRechargeStarted && ResearchShieldGenerators > 0)
                    {
                        researchShieldRechargeStarted = true;
                        RpcRainPersonalShieldRecharge();
                    }
                    researchShieldCharge = Mathf.Min(previousResearchShieldCapacity,
                        researchShieldCharge + previousResearchShieldCapacity * .5f * Time.deltaTime);
                    ResearchShield = Mathf.FloorToInt(researchShieldCharge);
                }
            }
            if (isServer)
            {
                float regen = EffectiveRainHealthRegen;
                if (regen <= 0f || CurrentHealth <= 0 || CurrentHealth >= MaxHealth ||
                    (TryGetComponent<RangeBleeding>(out var burn) && burn.HasBurn()))
                    rainRegenRemainder = 0;
                else
                {
                    rainRegenRemainder += regen * Time.deltaTime;
                    int heal = Mathf.FloorToInt(rainRegenRemainder);
                    if (heal > 0)
                    {
                        rainRegenRemainder -= heal;
                        ServerHeal(heal, netId, allowOverhealBarrier: false);
                    }
                }
            }
            lookInput.x = Mathf.Clamp(lookInput.x, -90f, 90f);

            if (Block)
                movementInput = Vector2.zero;

            if (_killer && isOwned)
                ClientInterfaceManager.Instance.TPPCameraAssignCharacterToLookAt(_killer);
        }

        #region input networking
        void CharacterInstance_Tick()
        {
            if (isOwned)
            {
                NetworkClient.Send(ClientPrepareInputMessage(), Channels.Unreliable);
            }
            else if (BOT)
            {
                RpcReceiveInputFromServer(movementInput, lookInput, _actionCode);
            }
        }

        /// <summary>
        /// send player input to every client except client who sent it, so we can rotate character correcly and 
        /// play appropriate animations
        /// </summary>
        [ClientRpc(includeOwner = false)]
        void RpcReceiveInputFromServer(Vector2 movement, Vector2 look, byte inputCode)
        {
            if (BOT && isServer) return;

            movementInput = movement;
            lookInput = look;
            _actionCode = inputCode;
        }
        #endregion


        private void ClientOnHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attacker)
        {
            if (currentHealth > 0) return;
            if (RainDroneOwner != 0)
            {
                if (rainDroneHitbox) rainDroneHitbox.enabled = false;
                return;
            }

            //Set camera to follow killer
            if (IsObserved && attacker) {
                ClientInterfaceManager.Instance.TPPCameraAssignCharacterToLookAt(attacker);
            }

            _hitboxes.DisableHitboxes();
            _motor.Die(damagedPart, attacker); //disable movement for dead character

            GetComponent<CharacterController>().enabled = false;

            GameManager.SetLayerRecursively(CharacterModel, 9); //set ragdoll layer

            DespawnCharacterMarker();
        }

        private void ServerDeath(CharacterPart characterPart, AttackType attackType, Health killer, int attackForce)
        {
            if (killer) {
                CharacterInstance killerChar = killer.GetComponent<CharacterInstance>();

                if (killerChar)
                    killerChar.Server_KilledCharacter?.Invoke(this);
            }

            GameManager.Gamemode.Server_OnPlayerKilled(this, killer);

            GetComponent<CharacterController>().enabled = false;
        }

        public void SetAsBOT(bool _set) 
        {
            BOT = _set;
            Server_SetAsBOT?.Invoke(_set);
        }

        public void ObserveCharacter(bool _observe)
        {
            if (_observe)
                ClientInterfaceManager.Instance.AssignTPPCamera(this);
        }

        private void FixedUpdate()
        {
            //character animations
            if (Block)
                movementInput = Vector2.zero;

            IsGrounded = Physics.CheckSphere(transform.position + new Vector3(0, 0.3f, 0), 0.5f, GameManager.environmentLayer);
        }

        public void SetMovementInput(Vector2 input) 
        {
            if (Block) return;

            movementInput = input;
        }
        public void SetMovementInput(float x, float y) 
        {
            if (Block) return;

            SetMovementInput(new Vector2(x, y));
        }

        void DespawnCharacterMarker()
        {
            if (_myUIMarker)
                Destroy(_myUIMarker);
        }

        public void BlockCharacter(bool block) 
        {
            Block = block;
            RpcBlockCharacter(block);
        }
        [ClientRpc]
        private void RpcBlockCharacter(bool block) 
        {
            Block = block;
        }

        #region input


        public ClientSendInputMessage ClientPrepareInputMessage()
        {
            return new ClientSendInputMessage {
                Movement = FitMovementInputToOneByte(movementInput),
                LookX = (sbyte)Mathf.FloorToInt(lookInput.x),
                LookY = (short)lookInput.y,
                ActionCodes = _actionCode
            };
        }
        //public CharacterInputMessage ServerPrepareInputMessage()
        //{
        //    InputMessage.Movement = FitMovementInputToOneByte(Input.Movement);
        //    InputMessage.LookX = (sbyte)Mathf.FloorToInt(Input.LookX);
        //    InputMessage.LookY = (short)Input.LookY;
        //    InputMessage.ActionCodes = Input.ActionCodes;

        //    return InputMessage;
        //}


        public byte FitMovementInputToOneByte(Vector2 movement)
        {
            // Two small signed numbers (values between -8 to 7)
            int mX = Mathf.FloorToInt(movement.x / 0.2f);
            int mY = Mathf.FloorToInt(movement.y / 0.2f);

            // Convert the numbers to 4-bit two's complement representation
            byte first4Bits = (byte)((mX < 0 ? 0x08 : 0x00) | (Math.Abs(mX) & 0x07)); // Check sign bit and keep last 3 bits
            byte second4Bits = (byte)((mY < 0 ? 0x08 : 0x00) | (Math.Abs(mY) & 0x07)); // Check sign bit and keep last 3 bits

            // Combine the two 4-bit representations into a single byte
            return (byte)((first4Bits << 4) | second4Bits);
        }


        public void ReadAndApplyInputFromClient(ClientSendInputMessage msg)
        {
            if (!BOT && !isOwned)
                ApplyInput(msg.Movement, msg.LookX, msg.LookY, msg.ActionCodes);

            RpcReceiveInputFromServer(movementInput, lookInput, _actionCode);
        }
        /*public void ReadAndApplyInputFromServer(CharacterInputMessage msg)
        {
            ApplyInput(msg.Movement, msg.LookX, msg.LookY, msg.ActionCodes);
        }*/

        void ApplyInput(byte packedMovementInput, float lookInputX, float lookInputY, byte actionCodes)
        {
            SetMovementInput(new Vector2(((packedMovementInput & 0x70) >> 4) * ((packedMovementInput & 0x80) == 0x80 ? -1 : 1) * 0.2f,
               (packedMovementInput & 0x07) * ((packedMovementInput & 0x08) == 0x08 ? -1 : 1) * 0.2f
            ));

            
            lookInput.x = lookInputX;
            lookInput.y = lookInputY;
            _actionCode = actionCodes;

            //_currentRotationTargetX = lookInputX;
            //_currentRotationTargetY = lookInputY;
        }


        public bool IsBladeWalking => ReadActionKeyCode(ActionCodes.BladeWalk) &&
            CharacterItemManager.CurrentlyUsedItem is MeleeWeapon blade && blade.FullBodyLocomotion;

        public bool ReadActionKeyCode(ActionCodes actionCode)
        {
            return (_actionCode & (1 << (int)actionCode)) != 0;
        }
        public void SetActionKeyCode(ActionCodes actionCode, bool _set)
        {
            int a = _actionCode;
            if (_set)
            {
                a |= 1 << ((byte)actionCode);
            }
            else
            {
                a &= ~(1 << (byte)actionCode);
            }
            _actionCode = (byte)a;
        }
        
        #endregion

        protected override void OnDestroy()
        {
            ClearFrostRelicRing();
            base.OnDestroy();
            RoomManager.RoomTick -= CharacterInstance_Tick;

            CustomSceneManager.DeRegisterCharacter(this);

            DespawnCharacterMarker();
        }
    }
    public enum ActionCodes
    {
        Trigger1,
        Trigger2,
        Sprint,
        Crouch,
        BladeWalk,
        LockedLocomotion,
    }
}
