using System;
using System.Collections.Generic;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        private const int LiveOfferSize = 3;
        private const int FallbackHealing = 125;
        private readonly System.Random rewardRandom = new System.Random();
        private readonly List<RewardOption> eligibleRewards = new List<RewardOption>();
        private readonly List<RewardOption> liveOffer = new List<RewardOption>(LiveOfferSize);
        private readonly HashSet<RewardOption> banishedRewards = new HashSet<RewardOption>();
        private readonly List<RewardOption> sealedRewards = new List<RewardOption>(3);
        private RewardOption[] rewardOptions;
        private RewardOption healFallback, goldFallback;
        private int offerAtReward = -1;
        private int focusedLiveReward, liveRewardDirection;
        private bool scrollToLiveReward;
        private int liveOfferLimit = LiveOfferSize;
        private float rulebreakerChance;
        private int rewardRerolls, rewardBanishes;
        private int rewardSealCapacity;
        private const string KenazEvolution = "passive_broksindri_M_evolve";
        private int kenazRank;
        private static readonly int[] RippedLotteryLevels = { 1, 8, 15 };
        private int rippedLotteryRank;
        private bool godExclusionAvailable, pendingGodExclusion;
        private RaidGod excludedGod;
        private RewardOption forcedRerollOption;
        private RewardOption pendingBanish;
        private RewardOption pendingSeal;
        private RewardOption pendingUnseal;
        private SkillCatalog rewardCatalog;
        private readonly Dictionary<string, string[]> rewardEffects = new Dictionary<string, string[]>();
        private Font rewardFont;
        private bool chineseRewards;
        private GUIStyle rewardButtonStyle, rewardLabelStyle;

        internal void ConfigureRewardDisplay(SkillCatalog catalog, Font font, bool english)
        {
            rewardCatalog = catalog;
            rewardFont = font;
            chineseRewards = !english;
            rewardEffects.Clear();
            var data = Resources.Load<TextAsset>("Jotunnslayer/RaidRewardEffects");
            if (!data)
                return;

            var catalogEffects = JsonUtility.FromJson<RewardEffectCatalog>(data.text);
            if (catalogEffects?.entries == null)
                return;

            foreach (var entry in catalogEffects.entries)
            {
                var effects = entry.effects;
                if (!english && entry.effectsZh != null && effects != null && entry.effectsZh.Length == effects.Length)
                {
                    bool complete = true;
                    foreach (var effect in entry.effectsZh)
                        if (string.IsNullOrWhiteSpace(effect)) { complete = false; break; }
                    if (complete) effects = entry.effectsZh;
                }
                rewardEffects[entry.id] = effects;
            }
        }

        [Serializable]
        private sealed class RewardEffectCatalog
        {
            public RewardEffectEntry[] entries;
        }

        [Serializable]
        private sealed class RewardEffectEntry
        {
            public string id;
            public string[] effects;
            public string[] effectsZh;
        }

        private string RewardText(string chinese, string english) => chineseRewards ? chinese : english;

        private string RewardTitle(string english, string id)
        {
            if (!chineseRewards || rewardCatalog?.skills == null)
                return english;

            foreach (var node in rewardCatalog.skills)
            {
                if (node.id == id)
                    return string.IsNullOrEmpty(node.title) ? english : node.title;
            }
            return english;
        }

        private GUIStyle RewardButtonStyle =>
            rewardButtonStyle ??= new GUIStyle(GUI.skin.button) { font = rewardFont, wordWrap = true };

        private GUIStyle RewardLabelStyle =>
            rewardLabelStyle ??= new GUIStyle(GUI.skin.label) { font = rewardFont, wordWrap = true };

        private enum RaidGod
        {
            None, Thor, Freya, Loki, Skadi, Odin, Njord, BrokkAndSindri, Nidhogg
        }

        private sealed class RewardOption
        {
            private readonly string name;
            internal readonly Func<bool> eligible, choose;
            private readonly Func<int> rank;
            private readonly int maxRank;
            private readonly string[] effects;
            internal readonly RaidGod god;

            internal RewardOption(string name, Func<bool> eligible, Func<bool> choose, Func<int> rank = null,
                RaidGod god = RaidGod.None, int maxRank = 4, string[] effects = null)
            {
                this.name = name;
                this.eligible = eligible;
                this.choose = choose;
                this.rank = rank;
                this.god = god;
                this.maxRank = maxRank;
                this.effects = effects;
            }

            internal string Label => rank == null ? name : $"{name}   {rank()}/{maxRank}";
            internal bool IsSkill => rank != null;
            internal string NextEffect
            {
                get
                {
                    if (effects == null || rank == null)
                        return null;

                    int currentRank = rank();
                    return currentRank < effects.Length ? effects[currentRank] : null;
                }
            }
        }

        private RewardOption CreateRewardOption(string name, string id, Func<bool> eligible, Func<bool> choose,
            Func<int> rank, RaidGod god = RaidGod.None, int maxRank = 4)
        {
            rewardEffects.TryGetValue(id, out var effects);
            return new RewardOption(RewardTitle(name, id), eligible, choose, rank, god, maxRank, effects);
        }

        private RewardOption[] CreateRewardOptions() => new RewardOption[]
        {
            CreateRewardOption("Magic Missiles", "active_seer_E", () => CanChoose(true), () => TryChoose(true), () => missileRank),
            CreateRewardOption("Sentinel", "active_thor_D", () => CanChoose(false), () => TryChoose(false), () => stormRank, RaidGod.Thor),
            CreateRewardOption("Thunderstorm", "active_thor_C_evolve", CanChooseThunder, TryChooseThunder, () => thunderRank, RaidGod.Thor),
            CreateRewardOption("Lightning Strike", "active_thor_A_evolve", CanChooseLightning, TryChooseLightning, () => lightningRank, RaidGod.Thor),
            CreateRewardOption("Mjolnir", "active_thor_B_evolve", CanChooseHammer, TryChooseHammer, () => hammerRank, RaidGod.Thor),
            CreateRewardOption("Ball Lightning", "active_thor_E_evolve", CanChooseOrb, TryChooseOrb, () => orbRank, RaidGod.Thor),
            CreateRewardOption("Overwhelming Potential", "passive_thor_H", CanChooseThorPotential, TryChooseThorPotential, () => thorPotentialRank, RaidGod.Thor, maxRank: 2),
            CreateRewardOption("Discharge", "passive_thor_I", CanChooseThunderDischarge, TryChooseThunderDischarge, () => thunderDischargeRank, RaidGod.Thor, maxRank: 3),
            CreateRewardOption("Mind Blast", "active_seer_D_evolve", CanChooseMind, TryChooseMind, () => mindRank),
            CreateRewardOption("Briar Thorns", "active_seer_B_evolve", CanChooseBriar, TryChooseBriar, () => briarRank),
            CreateRewardOption("Time Trap", "active_seer_A_evolve", CanChooseTrap, TryChooseTrap, () => trapRank),
            CreateRewardOption("Dark Wisps", "active_seer_C_evolve", CanChooseWisps, TryChooseWisps, () => wispRank),
            CreateRewardOption("Divination", "passive_seer_O", CanChooseDivination, TryChooseDivination, () => divinationRank, maxRank: 2),
            new RewardOption(RewardText("古老知识", "Ancient Knowledge"), CanChooseAncientKnowledge, TryChooseAncientKnowledge,
                () => ancientKnowledgeRank, maxRank: 2,
                effects: new[] { RewardText("拾取范围 +30%", "+30% pickup range"),
                    RewardText("拾取范围 +60%", "+60% pickup range") }),
            new RewardOption(RewardText("原初涌流", "Primeval Currents"), CanChoosePrimevalCurrents, TryChoosePrimevalCurrents,
                () => primevalCurrentsRank, maxRank: 2,
                effects: new[] { RewardText("技能伤害 +20%", "+20% skill damage"),
                    RewardText("技能伤害 +40%", "+40% skill damage") }),
            new RewardOption(RewardText("恶咒", "Malediction"), CanChooseMalediction, TryChooseMalediction,
                () => maledictionChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("暴击率 +10%，暴击施加 4 秒诅咒", "+10% critical chance, critical hits curse for 4 s") }),
            new RewardOption(RewardText("不祥之兆", "Ill Omen"), CanChooseIllOmen, TryChooseIllOmen,
                () => illOmenChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("对受诅咒、减速、冻结或定身的敌人额外造成 10 点伤害",
                    "Deals 10 bonus damage to cursed, slowed, frozen or rooted enemies") }),
            new RewardOption(RewardText("凶兆", "Dark Tidings"), CanChooseDarkTidings, TryChooseDarkTidings,
                () => darkTidingsChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("武器伤害 +5%，射程 +35%", "+5% attack damage, +35% range") }),
            new RewardOption(RewardText("怨恨步伐", "Spiteful Pace"), CanChooseSpitefulPace, TryChooseSpitefulPace,
                () => spitefulPaceChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("移速 +5%，每名受非伤害减益的敌人再加 2%",
                    "+5% movement speed, +2% per enemy with a non-damaging debuff") }),
            new RewardOption(RewardText("精华扩张", "Essence Expansion"), CanChooseEssenceExpansion, TryChooseEssenceExpansion,
                () => essenceExpansionChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("职业技能伤害 +15%，持续时间 +50%", "+15% class skill damage, +50% duration") }),
            CreateRewardOption("Healing Ward", "passive_seer_P", CanChooseHealingWard, TryChooseHealingWard, () => healingWardRank, maxRank: 3),
            new RewardOption(RewardText("冬鸦", "Winter Raven"), CanChooseWinterRaven, TryChooseWinterRaven,
                () => winterRavenChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("每 7 秒发射冬鸦，爆炸伤害 16，冻结 2.5 秒",
                    "Launches a raven every 7 s, dealing 16 blast damage and freezing for 2.5 s") }),
            new RewardOption(RewardText("结界效能", "Ward Effectiveness"), CanChooseWardEffectiveness, TryChooseWardEffectiveness,
                () => wardEffectivenessRank, maxRank: 3,
                effects: new[] { RewardText("保护时长 +50%", "+50% protection duration"),
                    RewardText("保护时长 +85%", "+85% protection duration"),
                    RewardText("保护时长 +120%", "+120% protection duration") }),
            new RewardOption(RewardText("预感", "Premonition"), CanChoosePremonition, TryChoosePremonition,
                () => premonitionChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("结界触发时有 30% 概率返还一层", "30% chance to refund one charge when Eldritch Ward triggers") }),
            new RewardOption(RewardText("汲取", "Extraction"), CanChooseExtraction, TryChooseExtraction,
                () => extractionChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("结界触发时有 25% 概率吸收 30 米内经验水晶",
                    "25% chance to collect experience crystals within 30 m when Eldritch Ward triggers") }),
            new RewardOption(RewardText("灵魂收割者", "Soul Reaper"), CanChooseSoulReaper, TryChooseSoulReaper,
                () => soulReaperChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("拾取经验水晶时有 5% 概率重置一项技能冷却，间隔 0.5 秒",
                    "5% chance to reset one skill cooldown on crystal pickup, with a 0.5 s cooldown") }),
            new RewardOption(RewardText("时间操控", "Time Manipulation"), CanChooseTimeManipulation, TryChooseTimeManipulation,
                () => timeManipulationChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("每层剩余结界：攻速 +7%，技能冷却 -7%",
                    "+7% attack speed and -7% skill cooldown per remaining ward charge") }),
            new RewardOption(RewardText("彻骨寒意", "Bone Chill"), CanChooseBoneChill, TryChooseBoneChill,
                () => boneChillChosen ? 1 : 0, maxRank: 1,
                effects: new[] { RewardText("结界触发时冻结 5 米内敌人 3 秒", "Eldritch Ward freezes enemies within 5 m for 3 s") }),
            CreateRewardOption("Cutting Edge", "active_berserker_C_evolve", CanChooseCuttingEdge, TryChooseCuttingEdge, () => cuttingRank),
            CreateRewardOption("Assault", "active_berserker_B_evolve", CanChooseAssault, TryChooseAssault, () => assaultRank),
            CreateRewardOption("Swirling Axe", "active_berserker_A_evolve", CanChooseSwirling, TryChooseSwirling, () => swirlingRank),
            CreateRewardOption("Throwing Axes", "active_berserker_D_evolve", CanChooseThrowingAxes, TryChooseThrowingAxes, () => throwingRank),
            CreateRewardOption("Totem of Suffering", "active_berserker_E_evolve", CanChooseTotem, TryChooseTotem, () => totemRank),
            CreateRewardOption("Open Wounds", "passive_berserker_O", CanChooseOpenWounds, TryChooseOpenWounds, () => openWoundsRank, maxRank: 2),
            CreateRewardOption("Discharge", "passive_berserker_P", CanChooseDischarge, TryChooseDischarge, () => dischargeRank, maxRank: 3),
            CreateRewardOption("Heart Seeker", "active_ranger_A_evolve", CanChooseHeartseeker, TryChooseHeartseeker, () => heartRank),
            CreateRewardOption("Arrow Rain", "active_ranger_C_evolve", CanChooseArrowRain, TryChooseArrowRain, () => rainRank),
            CreateRewardOption("Exploding Skull", "active_ranger_B_evolve", CanChooseSkull, TryChooseSkull, () => skullRank),
            CreateRewardOption("Focus Fire", "active_ranger_E_evolve", CanChooseFocus, TryChooseFocus, () => focusRank),
            CreateRewardOption("Entangling Net", "active_ranger_D_evolve", CanChooseNet, TryChooseNet, () => netRank),
            CreateRewardOption("Focused Shot", "passive_ranger_O", CanChooseFocusedShot, TryChooseFocusedShot, () => focusedShotRank, maxRank: 2),
            CreateRewardOption("Soul Shards", "passive_ranger_P", CanChooseSoulShards, TryChooseSoulShards, () => soulShardsRank, maxRank: 3),
            CreateRewardOption("Divine Aura", "active_freya_C_evolve", CanChooseAura, TryChooseAura, () => auraRank, RaidGod.Freya),
            CreateRewardOption("Magic Sword", "active_freya_A_evolve", CanChooseMagicSword, TryChooseMagicSword, () => swordRank, RaidGod.Freya),
            CreateRewardOption("Seidr Blessing", "active_freya_E_evolve", CanChooseBlessing, TryChooseBlessing, () => blessingRank, RaidGod.Freya),
            CreateRewardOption("Divine Discs", "active_freya_D_evolve", CanChooseDiscs, TryChooseDiscs, () => discRank, RaidGod.Freya),
            CreateRewardOption("Valkyrie", "active_freya_B_evolve", CanChooseValkyrie, TryChooseValkyrie, () => valkyrieRank, RaidGod.Freya),
            CreateRewardOption("Hero's Hymn", "passive_freya_H", CanChooseHeroHymn, TryChooseHeroHymn, () => heroHymnRank, RaidGod.Freya, maxRank: 2),
            CreateRewardOption("Last Stand", "passive_freya_I", CanChooseLastStand, TryChooseLastStand, () => lastStandRank, RaidGod.Freya, maxRank: 3),
            CreateRewardOption("Parasitic Plague", "active_loki_E_evolve", CanChoosePlague, TryChoosePlague, () => plagueRank, RaidGod.Loki),
            CreateRewardOption("Fire Barrier", "active_loki_B_evolve", CanChooseFireBarrier, TryChooseFireBarrier, () => barrierRank, RaidGod.Loki),
            CreateRewardOption("Jormungandr", "active_loki_A_evolve", CanChooseSerpent, TryChooseSerpent, () => serpentRank, RaidGod.Loki),
            CreateRewardOption("Firestar", "active_loki_C_evolve", CanChooseFirestar, TryChooseFirestar, () => firestarRank, RaidGod.Loki),
            CreateRewardOption("Vipers", "active_loki_D_evolve", CanChooseVipers, TryChooseVipers, () => viperRank, RaidGod.Loki),
            CreateRewardOption("Shapeshifter", "passive_loki_H", CanChooseShapeshifter, TryChooseShapeshifter, () => shapeshifterRank, RaidGod.Loki, maxRank: 2),
            CreateRewardOption("Ripped Lottery", "passive_loki_I", CanChooseRippedLottery, TryChooseRippedLottery, () => rippedLotteryRank, RaidGod.Loki, maxRank: 3),
            CreateRewardOption("Frost Comet", "active_skadi_A_evolve", CanChooseComet, TryChooseComet, () => cometRank, RaidGod.Skadi),
            CreateRewardOption("Frost Ring", "active_skadi_B_evolve", CanChooseFrostRing, TryChooseFrostRing, () => ringRank, RaidGod.Skadi),
            CreateRewardOption("Frost Arrow", "active_skadi_D_evolve", CanChooseFrostArrow, TryChooseFrostArrow, () => frostArrowRank, RaidGod.Skadi),
            CreateRewardOption("Snowflake", "active_skadi_C_evolve", CanChooseSnowflake, TryChooseSnowflake, () => snowRank, RaidGod.Skadi),
            CreateRewardOption("Ice Field", "active_skadi_E_evolve", CanChooseIceField, TryChooseIceField, () => iceRank, RaidGod.Skadi),
            CreateRewardOption("Ice Block", "passive_skadi_H", CanChooseIceBlock, TryChooseIceBlock, () => iceBlockRank, RaidGod.Skadi, maxRank: 2),
            CreateRewardOption("Snow Fight", "passive_skadi_I", CanChooseSnowFight, TryChooseSnowFight, () => snowFightRank, RaidGod.Skadi, maxRank: 3),
            CreateRewardOption("Gungnir", "active_odin_A_evolve", CanChooseGungnir, TryChooseGungnir, () => gungnirRank, RaidGod.Odin),
            CreateRewardOption("Huginn and Muninn", "active_odin_B_evolve", CanChooseRavens, TryChooseRavens, () => ravenRank, RaidGod.Odin),
            CreateRewardOption("Local Ragnarok", "active_odin_C_evolve", CanChooseRagnarok, TryChooseRagnarok, () => ragnarokRank, RaidGod.Odin),
            CreateRewardOption("Rune Boulder", "active_odin_D_evolve", CanChooseBoulder, TryChooseBoulder, () => boulderRank, RaidGod.Odin),
            CreateRewardOption("Einherjar", "active_odin_E_evolve", CanChooseWarrior, TryChooseWarrior, () => warriorRank, RaidGod.Odin),
            CreateRewardOption("Older than Time", "passive_odin_H", CanChooseOlderThanTime, TryChooseOlderThanTime, () => olderThanTimeRank, RaidGod.Odin, maxRank: 2),
            CreateRewardOption("Sleipnir's Speed", "passive_odin_I", CanChooseSleipnir, TryChooseSleipnir, () => sleipnirRank, RaidGod.Odin, maxRank: 3),
            CreateRewardOption("Abyssal Vortex", "active_njord_C_evolve", CanChooseVortex, TryChooseVortex, () => vortexRank, RaidGod.Njord),
            CreateRewardOption("Ship Charge", "active_njord_D_evolve", CanChooseShip, TryChooseShip, () => shipRank, RaidGod.Njord),
            CreateRewardOption("Kraken Touch", "active_njord_B_evolve", CanChooseKraken, TryChooseKraken, () => krakenRank, RaidGod.Njord),
            CreateRewardOption("Leviathan", "active_njord_A_evolve", CanChooseLeviathan, TryChooseLeviathan, () => leviathanRank, RaidGod.Njord),
            CreateRewardOption("Tidal Barrier", "active_njord_E_evolve", CanChooseTidalBarrier, TryChooseTidalBarrier, () => bubbleRank, RaidGod.Njord),
            CreateRewardOption("Mighty Oars", "passive_njord_H", CanChooseMightyOars, TryChooseMightyOars, () => mightyOarsRank, RaidGod.Njord, maxRank: 2),
            CreateRewardOption("Torrents", "passive_njord_I", CanChooseTorrents, TryChooseTorrents, () => torrentsRank, RaidGod.Njord, maxRank: 3),
            CreateRewardOption("Gebo Rune", "passive_broksindri_N_evolve", CanChooseGebo, TryChooseGebo, () => geboRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Jera Rune", JeraEvolution, CanChooseJera, TryChooseJera, () => jeraRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Thurisaz Rune", "passive_broksindri_J_evolve", CanChooseRegenerationRune, TryChooseRegenerationRune, () => regenRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Berkana Rune", "passive_broksindri_K_evolve", CanChooseHealthRune, TryChooseHealthRune, () => healthRuneRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Raido Rune", RaidoEvolution, CanChooseRaido, TryChooseRaido, () => raidoRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Hagalaz Rune", HagalazEvolution, CanChooseHagalaz, TryChooseHagalaz, () => hagalazRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Inguz Rune", InguzEvolution, CanChooseInguz, TryChooseInguz, () => inguzRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Uruz Rune", UruzEvolution, CanChooseUruz, TryChooseUruz, () => uruzRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Laguz Rune", LaguzEvolution, CanChooseLaguz, TryChooseLaguz, () => laguzRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Kenaz Rune", KenazEvolution, CanChooseKenaz, TryChooseKenaz, () => kenazRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Tiwaz Rune", TiwazEvolution, CanChooseTiwaz, TryChooseTiwaz, () => tiwazRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Mannaz Rune", MannazEvolution, CanChooseMannaz, TryChooseMannaz, () => mannazRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Ansuz Rune", AnsuzEvolution, CanChooseAnsuz, TryChooseAnsuz, () => ansuzRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Perth Rune", PerthEvolution, CanChoosePerth, TryChoosePerth, () => perthRank, RaidGod.BrokkAndSindri),
            CreateRewardOption("Wing Strike", "active_nidhogg_D_evolve", CanChooseWingStrike, TryChooseWingStrike, () => wingRank, RaidGod.Nidhogg),
            CreateRewardOption("Calamity Breath", "active_nidhogg_E_evolve", CanChooseCalamity, TryChooseCalamity, () => breathRank, RaidGod.Nidhogg),
            CreateRewardOption("Disease Ray", "active_nidhogg_B_evolve", CanChooseDiseaseRay, TryChooseDiseaseRay, () => diseaseRayRank, RaidGod.Nidhogg),
            CreateRewardOption("Raise Dead", "active_nidhogg_A_evolve", CanChooseRaiseDead, TryChooseRaiseDead, () => raiseRank, RaidGod.Nidhogg),
            CreateRewardOption("Terror Crush", "active_nidhogg_C_evolve", CanChooseTerror, TryChooseTerror, () => terrorRank, RaidGod.Nidhogg),
            CreateRewardOption("All In", "passive_nidhogg_F", CanChooseAllIn, TryChooseAllIn, () => allInChosen ? 1 : 0, RaidGod.Nidhogg, maxRank: 1),
            CreateRewardOption("Wings of Fury", "passive_nidhogg_I", CanChooseWingsOfFury, TryChooseWingsOfFury, () => wingsOfFuryRank, RaidGod.Nidhogg, maxRank: 3),
            CreateRewardOption("Dagger Dance", "active_rogue_A_evolve", CanChooseDaggerDance, TryChooseDaggerDance, () => daggerRank),
            CreateRewardOption("Shrapnel Pot", "active_rogue_B_evolve", CanChooseShrapnelPot, TryChooseShrapnelPot, () => potRank),
            CreateRewardOption("Flaming Ray", "active_rogue_C_evolve", CanChooseFlamingRay, TryChooseFlamingRay, () => flamingRayRank),
            CreateRewardOption("Sisterhood Incarnates", "active_rogue_D_evolve", CanChooseSisterhood, TryChooseSisterhood, () => sisterhoodRank),
            CreateRewardOption("Flaming Darts", "active_rogue_E_evolve", CanChooseFlamingDarts, TryChooseFlamingDarts, () => flamingDartsRank),
            CreateRewardOption("Fleet Footed", "passive_rogue_O", CanChooseFleetFooted, TryChooseFleetFooted, () => fleetFootedRank, maxRank: 2),
            CreateRewardOption("Invisible Blade", "passive_rogue_P", CanChooseInvisibleBlade, TryChooseInvisibleBlade, () => invisibleBladeRank, maxRank: 3),
            CreateRewardOption("Hammertime", "active_warden_E_evolve", CanChooseHammertime, TryChooseHammertime, () => hammertimeRank),
            CreateRewardOption("Iron Fist", "active_warden_A_evolve", CanChooseIronFist, TryChooseIronFist, () => ironFistRank),
            CreateRewardOption("Shieldwall", "active_warden_C_evolve", CanChooseShieldwall, TryChooseShieldwall, () => shieldwallRank),
            CreateRewardOption("Magnetized Anvil", "active_warden_D_evolve", CanChooseAnvil, TryChooseAnvil, () => anvilRank),
            CreateRewardOption("Seismic Shock", "active_warden_B_evolve", CanChooseSeismic, TryChooseSeismic, () => seismicRank),
            CreateRewardOption("Bulwark", "passive_warden_P", CanChooseBulwark, TryChooseBulwark, () => bulwarkRank, maxRank: 3),
            CreateRewardOption("Retribution", "passive_warden_O", CanChooseRetribution, TryChooseRetribution, () => retributionRank, maxRank: 2),
            CreateRewardOption("Valkyrie Charge", "active_valkyrie_E_evolve", CanChooseValkyrieCharge, TryChooseValkyrieCharge, () => valkyrieChargeRank),
            CreateRewardOption("Fallen Valkyries", "active_valkyrie_C_evolve", CanChooseFallenValkyries, TryChooseFallenValkyries, () => fallenValkyriesRank),
            CreateRewardOption("Spear Barrier", "active_valkyrie_B_evolve", CanChooseSpearBarrier, TryChooseSpearBarrier, () => spearBarrierRank),
            CreateRewardOption("Javelins", "active_valkyrie_A_evolve", CanChooseJavelins, TryChooseJavelins, () => javelinRank),
            CreateRewardOption("Shadow Images", "active_valkyrie_D_evolve", CanChooseShadowImages, TryChooseShadowImages, () => shadowImagesRank),
            CreateRewardOption("Spearhead", "passive_valkyrie_O", CanChooseSpearhead, TryChooseSpearhead, () => spearheadRank, maxRank: 2),
            CreateRewardOption("Formation", "passive_valkyrie_P", CanChooseFormation, TryChooseFormation, () => formationRank, maxRank: 3),
        };

        internal JotunnSkillSnapshot BuildRemoteSnapshot()
        {
            RefreshLiveOffer();
            var state = new JotunnSkillSnapshot
            {
                character = caster ? caster.netId : 0,
                attackSpeed = RaidBaseAttackSpeedMultiplier(caster),
                alive = HasLiveCaster, level = raidLevel, experience = experience,
                pending = PendingRewards, gold = progress?.Gold ?? 0, rerolls = rewardRerolls,
                banishes = rewardBanishes, sealCapacity = rewardSealCapacity,
                labels = new string[liveOffer.Count], details = new string[liveOffer.Count], actions = new byte[liveOffer.Count],
                sealedLabels = new string[sealedRewards.Count]
            };
            for (int i = 0; i < liveOffer.Count; i++)
            {
                state.labels[i] = LiveRewardLabel(i);
                var option = liveOffer[i];
                state.details[i] = option.NextEffect ?? string.Empty;
                if (!CanChangeLiveOffer || !IsLiveRewardSelectable(i)) continue;
                state.actions[i] = 1;
                if (option.IsSkill && rewardBanishes > 0) state.actions[i] |= 2;
                if (option.IsSkill && sealedRewards.Count < rewardSealCapacity) state.actions[i] |= 4;
                if (godExclusionAvailable && CanExcludeGod(option)) state.actions[i] |= 8;
            }
            for (int i = 0; i < sealedRewards.Count; i++) state.sealedLabels[i] = FormatRewardLabel(sealedRewards[i]);
            return state;
        }

        internal bool TryRemoteRewardAction(JotunnSkillAction action, int index) => action switch
        {
            JotunnSkillAction.Accept => TryAcceptLiveReward(index),
            JotunnSkillAction.Reroll => TryRerollLiveOffer(),
            JotunnSkillAction.Banish => TryBanishLiveReward(index),
            JotunnSkillAction.Seal => TrySealLiveReward(index),
            JotunnSkillAction.Unseal => TryUnsealLiveReward(index),
            JotunnSkillAction.ExcludeGod => TryExcludeLiveGod(index),
            _ => false
        };

        internal int RefreshLiveOffer()
        {
            if (pendingUnseal != null)
            {
                sealedRewards.Remove(pendingUnseal);
                pendingUnseal = null;
            }

            if (!LiveMatch || !HasLiveCaster || PendingRewards == 0)
            {
                liveOffer.Clear();
                return 0;
            }

            if (offerAtReward == rewardsSpent && (pendingBanish != null || pendingSeal != null || pendingGodExclusion))
            {
                if (pendingSeal != null)
                    sealedRewards.Add(pendingSeal);
                for (int i = liveOffer.Count - 1; i >= 0; i--)
                {
                    if (IsExcluded(liveOffer[i]))
                        liveOffer.RemoveAt(i);
                }
                pendingBanish = null;
                pendingSeal = null;
                pendingGodExclusion = false;
                FillLiveOffer();
                FocusLiveReward(0);
                return liveOffer.Count;
            }

            if (offerAtReward == rewardsSpent && forcedRerollOption == null)
            {
                foreach (var option in liveOffer)
                {
                    if (option.eligible())
                        return liveOffer.Count;
                }
            }

            if (offerAtReward != rewardsSpent)
                liveOfferLimit = LiveOfferSize + (rulebreakerChance > 0f && UnityEngine.Random.value < rulebreakerChance ? 1 : 0);
            offerAtReward = rewardsSpent;
            liveOffer.Clear();
            FillLiveOffer();
            FocusLiveReward(0);
            return liveOffer.Count;
        }

        private void UpdateLiveRewardNavigation()
        {
            if (!LiveMatch || !ShowControls || (tree && tree.enabled) || !CanChangeLiveOffer || liveOffer.Count == 0)
            {
                liveRewardDirection = 0;
                return;
            }

            float vertical = Input.GetAxisRaw("Vertical");
            int direction = vertical > .5f ? -1 : vertical < -.5f ? 1 : 0;
            if (direction != 0 && direction != liveRewardDirection)
                MoveLiveRewardFocus(direction);
            else if (!IsLiveRewardSelectable(focusedLiveReward))
                MoveLiveRewardFocus(0);
            liveRewardDirection = direction;
            if (Input.GetButtonDown("Submit"))
                TryAcceptLiveReward(focusedLiveReward);
        }

        internal int FocusedLiveReward => focusedLiveReward;

        internal void MoveLiveRewardFocus(int direction)
        {
            if (!IsLiveRewardSelectable(focusedLiveReward))
            {
                for (int i = 0; i < liveOffer.Count; i++)
                {
                    if (!IsLiveRewardSelectable(i))
                        continue;
                    FocusLiveReward(i);
                    return;
                }
                return;
            }

            if (direction == 0)
                return;
            for (int i = focusedLiveReward + direction; i >= 0 && i < liveOffer.Count; i += direction)
            {
                if (!IsLiveRewardSelectable(i))
                    continue;
                FocusLiveReward(i);
                return;
            }
        }

        private bool IsLiveRewardSelectable(int index)
        {
            if (index < 0 || index >= liveOffer.Count)
                return false;
            var option = liveOffer[index];
            return !IsExcluded(option) && option.eligible();
        }

        private void FocusLiveReward(int index)
        {
            focusedLiveReward = Mathf.Clamp(index, 0, Mathf.Max(0, liveOffer.Count - 1));
            scrollToLiveReward = liveOffer.Count > 0;
        }

        private void FillLiveOffer()
        {
            eligibleRewards.Clear();
            rewardOptions ??= CreateRewardOptions();
            foreach (var option in rewardOptions)
            {
                if (!IsExcluded(option) && !liveOffer.Contains(option) && option.eligible())
                    eligibleRewards.Add(option);
            }

            if (forcedRerollOption != null && eligibleRewards.Remove(forcedRerollOption))
                liveOffer.Add(forcedRerollOption);
            forcedRerollOption = null;

            while (liveOffer.Count < liveOfferLimit && eligibleRewards.Count != 0)
            {
                int index = rewardRandom.Next(eligibleRewards.Count);
                liveOffer.Add(eligibleRewards[index]);
                eligibleRewards.RemoveAt(index);
            }

            if (liveOffer.Count < LiveOfferSize && !liveOffer.Contains(goldFallback) && CanTakeGoldFallback())
                liveOffer.Add(goldFallback ??= new RewardOption(RewardText("金币  +1", "Golden Coin   +1 Gold"),
                    CanTakeGoldFallback, TakeGoldFallback));
            if (liveOffer.Count < LiveOfferSize && !liveOffer.Contains(healFallback) && CanTakeHealFallback())
                liveOffer.Add(healFallback ??= new RewardOption(RewardText($"治疗  +{FallbackHealing} 生命", $"Cure   +{FallbackHealing} HP"),
                    CanTakeHealFallback, TakeHealFallback));

        }

        private bool CanTakeGoldFallback() => PendingRewards > 0 && progress.Gold < int.MaxValue;

        private bool IsExcluded(RewardOption option) => banishedRewards.Contains(option) || sealedRewards.Contains(option) ||
            (excludedGod != RaidGod.None && option.god == excludedGod);

        private bool CanChangeLiveOffer => LiveMatch && HasLiveCaster && PendingRewards > 0 &&
            forcedRerollOption == null && pendingBanish == null && pendingSeal == null &&
            pendingUnseal == null && !pendingGodExclusion &&
            offerAtReward == rewardsSpent;

        private bool CanTakeHealFallback() => PendingRewards > 0 && HasLiveCaster &&
            !allInChosen && caster.CurrentHealth < caster.MaxHealth;

        private bool TakeGoldFallback()
        {
            if (!progress.TryCollectGold())
                return false;

            rewardsSpent++;
            return true;
        }

        private bool TakeHealFallback()
        {
            if (caster.ServerHeal(FallbackHealing, caster.netId, allowOverhealBarrier: false) <= 0)
                return false;

            rewardsSpent++;
            return true;
        }

        internal bool TryAcceptLiveReward(int index)
        {
            if (!CanChangeLiveOffer || !IsLiveRewardSelectable(index))
                return false;

            return liveOffer[index].choose();
        }

        internal string LiveRewardLabel(int index)
        {
            return FormatRewardLabel(liveOffer[index]);
        }

        private string FormatRewardLabel(RewardOption option)
        {
            return option.god == RaidGod.None ? option.Label : $"{DisplayGod(option.god)} · {option.Label}";
        }

        private string DisplayGod(RaidGod god) => god switch
        {
            RaidGod.Thor => RewardText("索尔", "Thor"),
            RaidGod.Freya => RewardText("芙蕾雅", "Freya"),
            RaidGod.Loki => RewardText("洛基", "Loki"),
            RaidGod.Skadi => RewardText("斯卡蒂", "Skadi"),
            RaidGod.Odin => RewardText("奥丁", "Odin"),
            RaidGod.Njord => RewardText("尼约德", "Njord"),
            RaidGod.BrokkAndSindri => RewardText("布洛克与辛德里", "Brokk & Sindri"),
            RaidGod.Nidhogg => RewardText("尼德霍格", "Nidhogg"),
            _ => string.Empty
        };

        private float permanentDoubleRewardChance;

        private bool CanChooseRippedLottery() => HasLiveCaster && PendingRewards > 0 &&
            rippedLotteryRank < RippedLotteryLevels.Length && raidLevel >= RippedLotteryLevels[rippedLotteryRank] &&
            progress.Gold < int.MaxValue && (rippedLotteryRank != 0 || progress.IsLearned("passive_loki_I"));

        internal bool TryChooseRippedLottery()
        {
            if (!CanChooseRippedLottery())
                return false;

            rippedLotteryRank++;
            rewardsSpent++;
            if (UnityEngine.Random.value >= 0.05f)
                progress.TryCollectGold(rippedLotteryRank * 10);
            return true;
        }

        private bool CanChooseKenaz() => LiveMatch && HasLiveCaster && PendingRewards > 0 &&
            kenazRank < RuneRewardLevels.Length && raidLevel >= RuneRewardLevels[kenazRank] &&
            (kenazRank < 3 || progress.IsLearned(KenazEvolution));

        internal bool TryChooseKenaz()
        {
            if (!CanChooseKenaz())
                return false;

            kenazRank++;
            rewardsSpent++;
            switch (kenazRank)
            {
                case 1:
                    rewardBanishes += 3;
                    break;
                case 2:
                    rewardRerolls += 5;
                    break;
                case 3:
                    rewardSealCapacity += 3;
                    break;
                case 4:
                    rewardBanishes += 2;
                    rewardRerolls += 3;
                    rewardSealCapacity++;
                    break;
            }
            return true;
        }

        private void ResetRewardChoices(SkillProgress playerProgress)
        {
            permanentDoubleRewardChance = 0f;
            rulebreakerChance = 0f;
            liveOfferLimit = LiveOfferSize;
            rewardRerolls = 0;
            rewardBanishes = 0;
            rewardSealCapacity = 0;
            godExclusionAvailable = false;
            pendingGodExclusion = false;
            excludedGod = RaidGod.None;
            forcedRerollOption = null;
            pendingBanish = null;
            pendingSeal = null;
            pendingUnseal = null;
            banishedRewards.Clear();
            sealedRewards.Clear();
            liveOffer.Clear();
            offerAtReward = -1;
            if (!LiveMatch || playerProgress == null)
                return;

            permanentDoubleRewardChance = playerProgress.IsLearned("virtue_odin_03_b") ? 0.10f :
                playerProgress.IsLearned("virtue_odin_03_a") ? 0.05f : 0f;
            permanentDoubleRewardChance += playerProgress.IsLearned("virtue_broksindri_03_b") ? 0.08f :
                playerProgress.IsLearned("virtue_broksindri_03_a") ? 0.04f : 0f;
            rulebreakerChance = playerProgress.IsLearned("virtue_nidhogg_03_d") ? 0.40f :
                playerProgress.IsLearned("virtue_nidhogg_03_c") ? 0.30f :
                playerProgress.IsLearned("virtue_nidhogg_03_b") ? 0.20f :
                playerProgress.IsLearned("virtue_nidhogg_03_a") ? 0.10f : 0f;
            godExclusionAvailable = playerProgress.IsLearned("virtue_odin_04_a");
            if (playerProgress.IsLearned("virtue_skadi_04_a")) rewardSealCapacity++;
            if (playerProgress.IsLearned("virtue_skadi_04_b")) rewardSealCapacity += 2;
            if (playerProgress.IsLearned("virtue_skadi_02_a")) rewardRerolls += 2;
            if (playerProgress.IsLearned("virtue_skadi_02_b")) rewardRerolls += 2;
            if (playerProgress.IsLearned("virtue_skadi_02_c")) rewardRerolls += 3;
            if (SelectedRaidClass != RaidClass.Seeress)
                return;

            if (playerProgress.IsLearned("virtue_seer_03_a")) rewardBanishes++;
            if (playerProgress.IsLearned("virtue_seer_03_b")) rewardBanishes++;
            rewardRerolls += rewardBanishes * 3;
        }

        internal bool TryBanishLiveReward(int index)
        {
            if (!CanChangeLiveOffer || rewardBanishes == 0 || index < 0 || index >= liveOffer.Count)
                return false;

            var option = liveOffer[index];
            if (!option.IsSkill || !option.eligible() || !banishedRewards.Add(option))
                return false;

            pendingBanish = option;
            rewardBanishes--;
            return true;
        }

        internal bool TrySealLiveReward(int index)
        {
            if (!CanChangeLiveOffer || sealedRewards.Count >= rewardSealCapacity ||
                index < 0 || index >= liveOffer.Count)
                return false;

            var option = liveOffer[index];
            if (!option.IsSkill || IsExcluded(option) || !option.eligible())
                return false;

            pendingSeal = option;
            return true;
        }

        internal bool TryUnsealLiveReward(int index)
        {
            if (!LiveMatch || !HasLiveCaster || pendingSeal != null || pendingUnseal != null ||
                index < 0 || index >= sealedRewards.Count)
                return false;

            pendingUnseal = sealedRewards[index];
            return true;
        }

        private bool CanExcludeGod(RewardOption option) =>
            option.god != RaidGod.None && option.god != RaidGod.Odin && option.eligible();

        internal bool TryExcludeLiveGod(int index)
        {
            if (!CanChangeLiveOffer || !godExclusionAvailable || index < 0 || index >= liveOffer.Count ||
                !CanExcludeGod(liveOffer[index]))
                return false;

            excludedGod = liveOffer[index].god;
            godExclusionAvailable = false;
            pendingGodExclusion = true;
            return true;
        }

        internal bool TryRerollLiveOffer()
        {
            if (!CanChangeLiveOffer || rewardRerolls == 0 || liveOffer.Count == 0)
                return false;

            eligibleRewards.Clear();
            foreach (var option in rewardOptions)
            {
                if (!IsExcluded(option) && !liveOffer.Contains(option) && option.eligible())
                    eligibleRewards.Add(option);
            }
            if (eligibleRewards.Count == 0)
                return false;

            forcedRerollOption = eligibleRewards[rewardRandom.Next(eligibleRewards.Count)];
            rewardRerolls--;
            return true;
        }

        private void DrawRewards()
        {
            bool wasEnabled = GUI.enabled;
            if (LiveMatch)
            {
                if (Event.current.type == EventType.Layout)
                    RefreshLiveOffer();

                rewardScroll = GUILayout.BeginScrollView(rewardScroll);
                Color background = GUI.backgroundColor;
                Rect focusedRect = default;
                if (liveOffer.Count == 0)
                    GUILayout.Label(PendingRewards == 0
                        ? RewardText("暂无技能奖励", "No pending skill rewards")
                        : RewardText("暂无合格技能", "No eligible skills"), RewardLabelStyle);

                for (int i = 0; i < liveOffer.Count; i++)
                {
                    var option = liveOffer[i];
                    string nextEffect = option.NextEffect;
                    GUI.enabled = wasEnabled && CanChangeLiveOffer && option.eligible();
                    if (i == focusedLiveReward)
                        GUI.backgroundColor = new Color(.88f, .65f, .32f);
                    if (GUILayout.Button(LiveRewardLabel(i), RewardButtonStyle, GUILayout.MinHeight(30)))
                        TryAcceptLiveReward(i);
                    if (i == focusedLiveReward && Event.current.type == EventType.Repaint)
                        focusedRect = GUILayoutUtility.GetLastRect();
                    GUI.backgroundColor = background;
                    if (nextEffect != null)
                        GUILayout.Label(RewardText("下一阶原表  ", "Next rank (source)  ") + nextEffect, RewardLabelStyle);
                    GUILayout.BeginHorizontal();
                    if (rewardBanishes > 0 || banishedRewards.Count > 0)
                    {
                        GUI.enabled = GUI.enabled && rewardBanishes > 0 && option.IsSkill;
                        if (GUILayout.Button(RewardText($"放逐 ({rewardBanishes})", $"Banish ({rewardBanishes})"), RewardButtonStyle))
                            TryBanishLiveReward(i);
                    }
                    if (godExclusionAvailable || excludedGod != RaidGod.None)
                    {
                        GUI.enabled = wasEnabled && CanChangeLiveOffer && godExclusionAvailable && CanExcludeGod(option);
                        if (GUILayout.Button(RewardText("驱逐神祇", "Exclude god"), RewardButtonStyle))
                            TryExcludeLiveGod(i);
                    }
                    if (rewardSealCapacity > 0)
                    {
                        GUI.enabled = wasEnabled && CanChangeLiveOffer && option.IsSkill &&
                            option.eligible() && sealedRewards.Count < rewardSealCapacity;
                        if (GUILayout.Button(RewardText("封印", "Seal"), RewardButtonStyle))
                            TrySealLiveReward(i);
                    }
                    GUILayout.EndHorizontal();
                }
                GUI.enabled = wasEnabled && CanChangeLiveOffer && rewardRerolls > 0 && liveOffer.Count > 0;
                if (GUILayout.Button(RewardText($"刷新  {rewardRerolls}", $"Reroll   {rewardRerolls}"), RewardButtonStyle))
                    TryRerollLiveOffer();
                if (rewardSealCapacity > 0)
                {
                    GUI.enabled = wasEnabled;
                    GUILayout.Label(RewardText($"封印  {sealedRewards.Count}/{rewardSealCapacity}",
                        $"Seals   {sealedRewards.Count}/{rewardSealCapacity}"), RewardLabelStyle);
                    for (int i = 0; i < sealedRewards.Count; i++)
                    {
                        GUI.enabled = wasEnabled && pendingUnseal == null && pendingSeal == null;
                        if (GUILayout.Button(RewardText("解除封印  ", "Unseal ") + sealedRewards[i].Label, RewardButtonStyle))
                            TryUnsealLiveReward(i);
                    }
                }
                GUI.enabled = wasEnabled;
                GUILayout.EndScrollView();
                if (scrollToLiveReward && Event.current.type == EventType.Repaint && focusedRect.height > 0f)
                {
                    float viewport = GUILayoutUtility.GetLastRect().height;
                    rewardScroll.y = Mathf.Clamp(rewardScroll.y, Mathf.Max(0f, focusedRect.yMax - viewport), focusedRect.yMin);
                    scrollToLiveReward = false;
                }
                return;
            }

            rewardOptions ??= CreateRewardOptions();
            rewardScroll = GUILayout.BeginScrollView(rewardScroll);
            foreach (var option in rewardOptions)
            {
                GUI.enabled = wasEnabled && option.eligible();
                if (GUILayout.Button("Choose " + option.Label))
                    option.choose();
            }
            GUI.enabled = wasEnabled;
            GUILayout.EndScrollView();
        }
    }
}
