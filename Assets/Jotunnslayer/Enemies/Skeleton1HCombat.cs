using System.Collections;
using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [RequireComponent(typeof(CharacterInstance), typeof(CharacterMotor))]
    public sealed class Skeleton1HCombat : MonoBehaviour
    {
        public const string ConfigurationId = "skeleton_1h_sur";
        public const int BaseHealth = 36;
        public const float FollowSpeed = 1.45f;
        public const float AttackInterval = .8f;

        // TPS encounter tuning until the original timing and difficulty adjustments are recovered.
        [SerializeField, Min(.1f)] float reach = 1.6f;
        [SerializeField, Min(1)] int damage = 5;
        [SerializeField, Min(.01f)] float windup = .6f;
        [SerializeField, Min(.01f)] float recovery = 1f;
        [SerializeField, Min(0f)] float spawnDelay = 1f;
        [SerializeField] Skeleton1HVisual visual;

        enum Phase { Spawn, Follow, Windup, Recovery, PhaseChange }

        CharacterInstance body;
        bool initialized;
        bool archer;
        bool hybCaster;
        bool hybSubBossNormal;
        bool hybSubBossBowVolleyReleased;
        bool hybBossAdvanced;
        bool hybBossHard;
        int hybHardFinalMiniBossCasts;
        int[] hybNormalSummonCasts;
        int HybCircleCount => hybBossAdvanced ? Mathf.Min(3 + HyboreaSkillSet, hybBossHard ? 6 : 5) :
            hybBoss || hybSubBossNormal ? 3 : 2;
        bool hybBoss;
        bool hybBossSuspended;
        Transform[] hybTeleportHelpers;
        float nextHybTeleport;
        float nextHybNearTeleport;
        float hybTeleportHiddenUntil;
        const float HybInitialBoltSpeed = .5f;
        internal int HyboreaSkillSet { get; private set; }
        bool circlesAttack;
        bool hybSummonAttack;
        Skeleton1HSpawn.Kind hybSummonKind = Skeleton1HSpawn.Kind.SphinxSkeleton;
        float nextHybBossSummon;
        float nextHybBossBolt;
        bool hybBossBoltAttack;
        static readonly WaitForSeconds HybBossBoltSpacing = new WaitForSeconds(.6f);
        bool hybBowSummoned;
        bool hybGuardianSummoned;
        HybSummonVolley hybVolley;
        bool circlesActive;
        LineRenderer[] hybCircleWarnings;
        Vector3[] hybCircleCenters;
        float[] hybCircleDetonations;
        bool spider;
        bool alfheimSpider;
        bool sentinel;
        bool elfMelee;
        bool elfMeleeInsane;
        bool swordCast;
        float nextSwordCast;
        const float ElfSwordSpeed = .8f;
        bool elfRanged;
        bool stoneJotunn;
        bool drider;
        bool driderVariant;
        bool driderArea;
        float nextDriderArea;
        LineRenderer[] driderAreaWarnings;
        const float DriderAreaRadius = 1.9f;
        static readonly WaitForSeconds DriderAreaFuse = new WaitForSeconds(1.5f);
        bool driderOrb;
        bool driderWeb;
        float nextDriderOrb;
        float nextDriderWeb;
        float nextDriderCheck;
        Vector3 driderWebCenter;
        const float DriderWebSpeed = .85f;
        const float DriderWebRadius = 5f;
        Vector3 stoneStompCenter;
        const float StoneStompRadius = 5f;
        const float StoneStompRelease = .39194635f + 1.35f;
        const float StoneStompDuration = 3.8333335f;
        bool guardian;
        bool sphinxGuardian;
        bool sphinxMiniBoss;
        bool milipede;
        bool milipedeSpit;
        float nextMilipedeSpit;
        bool spiderInsane, spiderSplashCast, spiderSplashInFlight;
        float nextSpiderSplashCheck;
        Vector3 spiderSplashPoint;
        SkeletonBowArrow spiderSplash;
        const float SpiderCastSpeed = .75f;
        static readonly WaitForSeconds MilipedeSpitDelay = new WaitForSeconds(.05f);
        const float SphinxMiniBlastRadius = 5f;
        const float SphinxMiniBlastOffset = 3.5f;
        const float SphinxMiniCastSpeed = .3f;
        const float SphinxMiniBlastDelay = 1.9f;
        Coroutine sphinxMiniBlast;
        Transform sphinxMiniWarningArt;
        Material sphinxMiniWarningMaterial;
        float secondStrikeAt;
        bool boss;
        internal bool ResistsSlay => constructBoss || boss || alfheimPhase != 0 || hybCaster || hybBoss || drider || sphinxMiniBoss;
        int alfheimPhase;
        bool roundSlashAttack;
        bool burrowAttack;
        bool splashAttack;
        bool spiderSummonAttack;
        bool veteranSummon;
        float nextSpiderSummon;
        float nextVeteranSummon;
        float nextSummonCheck;
        AlfheimSummons alfheimSummons;
        const float SpiderSummonSpeed = 2f;
        const float SpiderSummonRelease = .16598694f / SpiderSummonSpeed;
        const float SpiderSummonDuration = 1.5000001f / SpiderSummonSpeed;
        bool splashLaunching;
        int splashInFlight;
        float nextSplashCheck;
        const float SplashSpeed = 2f;
        const float SplashRelease = 1.2125546f / SplashSpeed;
        const float SplashDuration = 2.1333334f / SplashSpeed;
        const int SplashCount = 5;
        static readonly WaitForSeconds SplashSpacing = new WaitForSeconds(.05f);
        Vector3[] splashCenters;
        LineRenderer[] splashWarnings;
        GameObject[] splashProjectiles;
        Coroutine burrowRoutine;
        float nextBurrowCheck;
        const float BurrowRadius = 3.5f;
        const float BurrowRelease = .9470499f;
        const float BurrowDuration = 6.0833335f;
        bool waveAttack;
        bool birdBoss;
        bool elementalMage;
        bool elementalMelee;
        bool ghostKing;
        bool jotunnWarrior;
        AudioClip[] warriorStompVariants;
        AudioClip warriorSpikeCast;
        AudioClip warriorSpikeImpact;
        Transform warriorAudioEmitter;
        internal AudioSource WarriorSecondaryAudio { get; private set; }
        Vector3 warriorStompPoint;
        float warriorStompWarningAt;
        GameObject warriorStompWarningPrefab;
        internal GameObject WarriorStompWarningArt { get; private set; }
        GameObject warriorStompImpactPrefab;
        internal GameObject WarriorStompImpactArt { get; private set; }
        Material warriorCraterMaterial;
        Mesh warriorCraterMesh;
        MaterialPropertyBlock warriorCraterProperties;
        Coroutine warriorCraterFade;
        internal MeshRenderer WarriorStompCrater { get; private set; }
        static readonly int WarriorCraterColor = Shader.PropertyToID("_Color");
        internal int WarriorStompHits { get; private set; }
        const int WarriorSpikeSteps = 6;
        const float WarriorSpikeRelease = 2.0680044f;
        const float WarriorSpikeTurnEnd = 1.6239697f;
        const float WarriorSpikeRadius = 1.5f;
        const float WarriorBodyCenterHeight = 4.647f;
        bool warriorV2;
        bool warriorSpikeWindup;
        Vector3?[] warriorSpikeGround;
        float warriorSpikeReleaseAt;
        float warriorSpikeYaw;
        int warriorSpikeIndex;
        internal int WarriorSpikesResolved { get; private set; }
        internal int WarriorSpikeHits { get; private set; }
        internal AudioSource GhostKingImpactAudio { get; private set; }
        Vector3 ghostKingAttackOrigin;
        static readonly WaitForSeconds GhostKingStrikeSpacing = new WaitForSeconds(.12f);
        bool elfWoman;
        bool elfWomanV2;
        bool elfSummonWindup;
        bool elfLargeSummon;
        bool nextElfSummonLarge;
        float nextElfSummon;
        internal AlfheimSummons ElfSummons => alfheimSummons;
        internal bool SuppressSummonRewards { get; set; }
        AudioClip[] elfSpikeBlastClips;
        bool elementalChampion;
        bool championMissileWindup;
        float nextChampionMissile;
        AudioClip championMissileAudio;
        List<SkeletonBowArrow> championMissiles;
        static readonly WaitForSeconds ChampionMissileSpacing = new WaitForSeconds(.25f);
        internal int ChampionMissilesReleased { get; private set; }
        internal int ChampionMissileDamage { get; private set; }
        internal void RecordChampionMissileDamage(int amount) => ChampionMissileDamage += amount;
        LineRenderer[] elementalSpikes;
        AudioClip[] elementalSpikeAudio;
        static readonly WaitForSeconds ElementalSpikeFuse = new WaitForSeconds(1.5f);
        static readonly WaitForSeconds ElfSpikeVisibleTime = new WaitForSeconds(.4f);
        static MaterialPropertyBlock elfSpikeColor;
        static readonly WaitForSeconds ElementalSpikeSpacing = new WaitForSeconds(.02f);
        internal int ElementalSpikeWarnings { get; private set; }
        internal int ElementalSpikeHits { get; private set; }
        bool elementalMageV2;
        bool elementalPlague;
        bool elementalVenom;
        bool PoisonMage => elementalPlague || elementalVenom;
        bool poisonOrbWindup;
        bool plagueSplashWindup;
        float nextPlagueSplash;
        Vector3 plagueSplashPoint;
        GameObject plagueSplashProjectile;
        AudioClip[] plagueSplashClips;
        internal AudioSource PlagueSplashFlightAudio { get; private set; }
        LineRenderer plagueSplashWarning;
        internal int PlagueSplashLanded { get; private set; }
        AudioClip poisonOrbCastAudio;
        float nextPoisonOrb;
        List<SkeletonBowArrow> poisonOrbs;
        internal int PoisonOrbDamage { get; private set; }
        internal void RecordPoisonOrbDamage(int amount) => PoisonOrbDamage += amount;
        bool mageFireWindup;
        float nextMageFire;
        AudioClip mageFireAudio;
        SkeletonBowArrow[] mageFireShots;
        internal int MageFireReleased { get; private set; }
        bool UsesDive => birdBoss || elementalMage;
        float diveJumpAt;
        Vector3 diveLanding;
        AudioClip birdImpactAudio;
        internal int DiveHits { get; private set; }
        internal bool DiveMoved { get; private set; }
        bool constructBoss;
        ConstructSaw constructSaw;
        AudioClip constructSawAlternate;
        int constructSkillSet;
        int constructFlameSide;
        Coroutine constructFlameRoutine;
        bool constructLaserWindup;
        Coroutine constructLaserRoutine;
        AudioClip constructLaserAudio;
        AudioClip constructLaserAlternate;
        LineRenderer[] constructLaserBeams;
        internal int ConstructLaserHits { get; private set; }
        internal int ConstructLaserBeamCount { get; private set; }
        AudioClip constructFlameAudio;
        internal int ConstructFlameHits { get; private set; }
        bool constructHammerWindup;
        AudioClip constructHammerSmash;
        AudioClip constructHammerSmashAlternate;
        internal AudioSource ConstructHammerAudio { get; private set; }
        Vector3 constructHammerPoint;
        const float ConstructHammerRadius = 1f;

        internal void SetConstructSkillSet(int skillSet)
        {
            if (constructBoss) constructSkillSet = Mathf.Clamp(skillSet, 0, 3);
        }
        internal int ConstructSawHits { get; private set; }
        internal void RecordConstructSawHit() => ConstructSawHits++;
        bool muspelBoss;
        AlfheimSpiderWave muspelWave;
        internal int MuspelWaveDamage { get; private set; }
        internal void RecordMuspelWaveDamage(int amount) => MuspelWaveDamage += amount;
        bool spikesAttack;
        bool spikesActive;
        float nextSpikesCheck;
        const int SpikeCount = 7;
        const float SpikeRadius = 1.3f;
        const float SpikesRelease = .0428669f;
        const float SpikesDuration = 4f;
        static readonly WaitForSeconds SpikeSpacing = new WaitForSeconds(.33f);
        static readonly WaitForSeconds SpikeFuse = new WaitForSeconds(.75f);
        LineRenderer[] spikeWarnings;
        Vector3[] spikeCenters;
        float nextWaveCheck;
        const float WaveRelease = 1.136598f;
        const float WaveDuration = 2.45f;
        const float RoundSlashRadius = 7f;
        const float RoundSlashSpeed = 1.2f;
        const float RoundSlashRelease = .07416592f / RoundSlashSpeed + 1.25f;
        const float RoundSlashDuration = 3.1333334f / RoundSlashSpeed;
        bool secondPhase;
        bool hornsAttack;
        bool summonAttack;
        float nextSummon;
        float nextGhostSummon;
        bool ghostSummon;
        bool jumpAttack;
        bool niflheimBossNormal;
        bool niflheimBossEndless;
        AudioClip goatHornsCast;
        AudioClip goatHornsImpact;
        internal int EndlessHornsDamage { get; private set; }
        bool GoatHasJumpAudio => niflheimBossNormal || niflheimBossEndless;
        float nextEndlessSkillCheck;
        Vector3 jumpImpact;
        const float JumpSpeed = .7f;
        const float JumpImpactRadius = 3f;
        LineRenderer jumpWarning;
        Material jumpWarningMaterial;
        const float HornsRadius = 4.8f;
        const float HornsSpeed = .8f;
        const float HornsRelease = .28125286f;
        const float HornsDuration = 2.0666668f;
        int meleeAttackSlot;
        float attackRecovery;
        Vector3 shotDirection;
        const float BowRelease = .37789664f;
        const float BowAttackInterval = 4f;
        const float BowRange = 12f;
        CharacterInstance target;
        Phase phase;
        float phaseEnds;
        float nextAttack;
        float attackYaw;
        bool gmWaiting;
        bool gmHybTeleport;
        bool trainingSample;
        AudioClip originalDeath;
        AudioClip originalAttack;
        AudioClip goatJumpLaunch;
        AudioClip goatFreezeCast;
        bool goatFreezeAttack;
        bool ettinBoss;
        bool ettinEndless;
        bool ettinNova;
        bool ettinPoisonAttack;
        EttinPoisonVolley ettinPoison;
        AudioClip ettinPoisonFlight;
        AudioClip ettinNovaImpact;
        AudioSource ettinNovaLayer;
        internal int EttinNovaHits { get; private set; }
        bool ettinLightning;
        internal int EttinLightningDamage { get; private set; }
        float nextEttinLightning;
        bool goatRingSummon;
        bool goatRotatorAttack;
        float nextGoatRotatorCheck;
        AudioClip goatRotatorBlast;
        GoatRotatorVolley goatRotators;
        float nextGoatRingCheck;
        float nextGoatFreezeCheck;
        AudioSource attackAudio;
        bool playMeleeSwingAudio;
        AudioClip hybCircleBlast;
        AudioSource[] hybCircleAudio;
        internal AudioSource AttackAudio => attackAudio;

        internal void SetGmCombat(bool fighting)
        {
            if (!body || !body.isServer) return;
            gmWaiting = !fighting;
            gmHybTeleport = hybBoss;
            ClearCombatActions();
            phase = Phase.Follow;
            nextAttack = phaseEnds = Time.time + .25f;
        }

        internal bool Begin(CharacterInstance victim, Skeleton1HVisual presentation, Skeleton1HSpawn.Kind kind)
        {
            if (body || !isActiveAndEnabled) return false;
            var character = GetComponent<CharacterInstance>();
            if (!character.isServer || character.CurrentHealth <= 0 || !victim ||
                victim.CurrentHealth <= 0 || victim.Team == character.Team ||
                !presentation || !presentation.Play(Skeleton1HVisual.Motion.Spawn)) return false;

            hybSubBossNormal = kind == Skeleton1HSpawn.Kind.HybSubBossNormal;
            hybCaster = kind == Skeleton1HSpawn.Kind.HybSubBossEasy || hybSubBossNormal;
            hybBossHard = kind == Skeleton1HSpawn.Kind.HybBossHard;
            hybBossAdvanced = kind == Skeleton1HSpawn.Kind.HybBossNormal || hybBossHard;
            hybBoss = kind == Skeleton1HSpawn.Kind.HybBossEasy || hybBossAdvanced;
            elfRanged = kind == Skeleton1HSpawn.Kind.AlfheimElfRanged;
            stoneJotunn = kind is Skeleton1HSpawn.Kind.AlfheimStoneJotunn or Skeleton1HSpawn.Kind.AlfheimStoneJotunnInsane;
            driderVariant = kind == Skeleton1HSpawn.Kind.AlfheimDriderVariant;
            drider = driderVariant || kind == Skeleton1HSpawn.Kind.AlfheimDrider;
            bool sphinxBow = kind == Skeleton1HSpawn.Kind.SphinxBow;
            archer = kind == Skeleton1HSpawn.Kind.Bow || sphinxBow || hybCaster || hybBoss || elfRanged;
            elfMeleeInsane = kind == Skeleton1HSpawn.Kind.AlfheimElfMeleeInsane;
            elfMelee = elfMeleeInsane || kind == Skeleton1HSpawn.Kind.AlfheimElfMelee;
            bool sentinelVeteran = kind == Skeleton1HSpawn.Kind.AlfheimSentinelVeteran;
            sentinel = sentinelVeteran || kind == Skeleton1HSpawn.Kind.AlfheimSentinel;
            bool alfheimVeteran = kind == Skeleton1HSpawn.Kind.AlfheimSpiderVeteran;
            alfheimSpider = alfheimVeteran || kind == Skeleton1HSpawn.Kind.AlfheimSpider;
            spiderInsane = kind == Skeleton1HSpawn.Kind.SpiderInsane;
            spider = kind == Skeleton1HSpawn.Kind.Spider || spiderInsane || alfheimSpider;
            sphinxGuardian = kind == Skeleton1HSpawn.Kind.SphinxGuardian;
            sphinxMiniBoss = kind == Skeleton1HSpawn.Kind.SphinxMiniBoss;
            milipede = kind is Skeleton1HSpawn.Kind.Milipede or Skeleton1HSpawn.Kind.MilipedeInsane;
            trainingSample = milipede || kind == Skeleton1HSpawn.Kind.Melee;
            if (trainingSample)
                originalDeath = Resources.Load<AudioClip>(milipede
                    ? "Jotunnslayer/Milipede/OriginalDeath" : "Jotunnslayer/Skeleton1H/OriginalDeath");
            bool muspelHel = kind is Skeleton1HSpawn.Kind.MuspelEndless or Skeleton1HSpawn.Kind.MuspelHelForm;
            elementalChampion = kind == Skeleton1HSpawn.Kind.ElementalMeleeChampion;
            if (elementalChampion)
                championMissileAudio = Resources.Load<AudioClip>("Jotunnslayer/ElementalMelee/OriginalChampionMissiles");
            jotunnWarrior = kind is Skeleton1HSpawn.Kind.JotunnWarrior or Skeleton1HSpawn.Kind.JotunnWarriorV2;
            ghostKing = kind == Skeleton1HSpawn.Kind.GhostKing;
            elfWomanV2 = kind == Skeleton1HSpawn.Kind.ElfWomanV2;
            elfWoman = elfWomanV2 || kind == Skeleton1HSpawn.Kind.ElfWoman;
            elementalMelee = kind is Skeleton1HSpawn.Kind.ElementalMelee or Skeleton1HSpawn.Kind.ElementalMeleeChampion;
            elementalPlague = kind == Skeleton1HSpawn.Kind.ElementalPlague;
            elementalVenom = kind == Skeleton1HSpawn.Kind.ElementalVenom;
            elementalMage = PoisonMage || kind is Skeleton1HSpawn.Kind.ElementalMage or Skeleton1HSpawn.Kind.ElementalMageV2;
            elementalMageV2 = kind == Skeleton1HSpawn.Kind.ElementalMageV2;
            birdBoss = kind is Skeleton1HSpawn.Kind.BirdNoObjective or Skeleton1HSpawn.Kind.BirdObjective;
            constructBoss = kind == Skeleton1HSpawn.Kind.ConstructBoss;
            muspelBoss = kind == Skeleton1HSpawn.Kind.MuspelBoss || muspelHel;
            ettinBoss = kind == Skeleton1HSpawn.Kind.EttinBoss;
            ettinEndless = kind is Skeleton1HSpawn.Kind.EttinEndless or Skeleton1HSpawn.Kind.EttinHelForm;
            if (ettinEndless)
            {
                ettinNovaImpact = Resources.Load<AudioClip>("Jotunnslayer/NiflheimBossEasy/OriginalGoatHornsImpact");
                var novaClip = Resources.Load<AudioClip>("Jotunnslayer/EttinBoss/OriginalNovaLayer");
                if (novaClip)
                {
                    ettinNovaLayer = gameObject.AddComponent<AudioSource>();
                    ettinNovaLayer.playOnAwake = false;
                    ettinNovaLayer.spatialBlend = 1f;
                    ettinNovaLayer.maxDistance = 24f;
                    ettinNovaLayer.volume = .7f;
                    ettinNovaLayer.clip = novaClip;
                }
                ettinPoisonFlight = Resources.Load<AudioClip>("Jotunnslayer/EttinBoss/OriginalPoisonFlight");
            }
            niflheimBossNormal = kind == Skeleton1HSpawn.Kind.NiflheimBossNormal;
            niflheimBossEndless = kind is Skeleton1HSpawn.Kind.NiflheimBossEndless or Skeleton1HSpawn.Kind.NiflheimBossHelForm;
            if (niflheimBossEndless)
            {
                goatHornsCast = Resources.Load<AudioClip>("Jotunnslayer/NiflheimBossEasy/OriginalGoatHornsCast");
                goatHornsImpact = Resources.Load<AudioClip>("Jotunnslayer/NiflheimBossEasy/OriginalGoatHornsImpact");
            }
            if (GoatHasJumpAudio)
                goatJumpLaunch = Resources.Load<AudioClip>("Jotunnslayer/NiflheimBossEasy/OriginalGoatJumpLaunch");
            if (jotunnWarrior)
            {
                warriorAudioEmitter = new GameObject("Warrior skill audio").transform;
                warriorAudioEmitter.SetParent(transform, false);
            }
            bool basicSpider = kind is Skeleton1HSpawn.Kind.Spider or Skeleton1HSpawn.Kind.SpiderInsane;
            bool basicHeavyStrike = kind is Skeleton1HSpawn.Kind.Guardian or Skeleton1HSpawn.Kind.NiflheimBossEasy or
                Skeleton1HSpawn.Kind.AlfheimPhase1 or Skeleton1HSpawn.Kind.AlfheimPhase2 or Skeleton1HSpawn.Kind.SphinxGuardian;
            playMeleeSwingAudio = kind is Skeleton1HSpawn.Kind.Melee or Skeleton1HSpawn.Kind.Bow or Skeleton1HSpawn.Kind.SphinxSkeleton or
                Skeleton1HSpawn.Kind.SphinxBow or Skeleton1HSpawn.Kind.Beetle or Skeleton1HSpawn.Kind.BeetleBig or Skeleton1HSpawn.Kind.BeetleDesert or Skeleton1HSpawn.Kind.BeetleDesertBig or Skeleton1HSpawn.Kind.BeetlePortal || basicSpider;
            if (playMeleeSwingAudio || basicHeavyStrike || drider || stoneJotunn || elfRanged || elfMelee || sentinel || alfheimSpider ||
                milipede || sphinxMiniBoss || hybBoss || hybCaster || GoatHasJumpAudio || ettinBoss ||
                ettinEndless || muspelBoss || constructBoss || UsesDive || elementalMelee || elfWoman || ghostKing || jotunnWarrior)
            {
                originalAttack = Resources.Load<AudioClip>(basicSpider || drider || alfheimSpider
                    ? "Jotunnslayer/SpiderRegular/SpiderStrikePlaceholder" : stoneJotunn || sentinel || basicHeavyStrike
                    ? "Jotunnslayer/NiflheimBossEasy/OriginalGoatJumpImpact" : playMeleeSwingAudio || elfRanged || elfMelee
                    ? "Jotunnslayer/Skeleton1H/SwingPlaceholder" : jotunnWarrior
                    ? "Jotunnslayer/JotunnWarrior/OriginalWarriorStompBase" : ghostKing
                    ? "Jotunnslayer/GhostKing/OriginalGhostKingCast" : elfWoman
                    ? "Jotunnslayer/ElfWoman/OriginalElfSpikeCast" : elementalMelee
                    ? "Jotunnslayer/ElementalMelee/OriginalElementalSpikes1" : elementalMage
                    ? "Jotunnslayer/ElementalMage/OriginalMageJump" : birdBoss
                    ? "Jotunnslayer/BirdBoss/OriginalBirdJumpCast" : constructBoss
                    ? "Jotunnslayer/ConstructBoss/OriginalSawThrust01" : milipede
                    ? "Jotunnslayer/Milipede/OriginalSpit" : sphinxMiniBoss
                        ? "Jotunnslayer/SphinxSkeleton/OriginalMiniBossBlast" : GoatHasJumpAudio
                        ? "Jotunnslayer/NiflheimBossEasy/OriginalGoatJumpImpact" : ettinBoss
                            ? "Jotunnslayer/EttinBoss/OriginalLightningCast" : ettinEndless
                                ? "Jotunnslayer/NiflheimBossEasy/OriginalGoatHornsCast" : muspelBoss
                                    ? "Jotunnslayer/MuspelBoss/OriginalFireWave" : "Jotunnslayer/HybBossEasy/OriginalCircleCast");
                if (constructBoss)
                    constructSawAlternate = Resources.Load<AudioClip>("Jotunnslayer/ConstructBoss/OriginalSawThrust02");
                if (birdBoss)
                    birdImpactAudio = Resources.Load<AudioClip>("Jotunnslayer/BirdBoss/OriginalBirdJumpImpact");
                if (elfWoman)
                    elfSpikeBlastClips = new[]
                    {
                        Resources.Load<AudioClip>("Jotunnslayer/ElfWoman/OriginalElfSpikeBlast01"),
                        Resources.Load<AudioClip>("Jotunnslayer/ElfWoman/OriginalElfSpikeBlast02"),
                        Resources.Load<AudioClip>("Jotunnslayer/ElfWoman/OriginalElfSpikeBlast03")
                    };
                if (elementalMelee)
                    elementalSpikeAudio = new[]
                    {
                        originalAttack,
                        Resources.Load<AudioClip>("Jotunnslayer/ElementalMelee/OriginalElementalSpikes2"),
                        Resources.Load<AudioClip>("Jotunnslayer/ElementalMelee/OriginalElementalSpikes3")
                    };
                if (originalAttack || goatJumpLaunch || birdImpactAudio)
                {
                    attackAudio = (warriorAudioEmitter ? warriorAudioEmitter.gameObject : gameObject).AddComponent<AudioSource>();
                    attackAudio.playOnAwake = false;
                    attackAudio.spatialBlend = 1f;
                    attackAudio.maxDistance = 24f;
                    attackAudio.volume = .7f;
                    attackAudio.clip = originalAttack;
                }
            }
            if (jotunnWarrior)
            {
                warriorV2 = kind == Skeleton1HSpawn.Kind.JotunnWarriorV2;
                warriorStompWarningPrefab = Resources.Load<GameObject>(
                    "Jotunnslayer/JotunnWarrior/StompWarning/WarriorStompStartNative");
                warriorStompImpactPrefab = Resources.Load<GameObject>(
                    "Jotunnslayer/JotunnWarrior/StompImpact/WarriorStompImpactNative");
                warriorCraterMaterial = Resources.Load<Material>(
                    "Jotunnslayer/JotunnWarrior/StompImpact/WarriorImpactCrater");
                warriorSpikeGround = new Vector3?[warriorV2 ? WarriorSpikeSteps * 3 : WarriorSpikeSteps];
                elementalSpikes = new LineRenderer[warriorSpikeGround.Length];
                warriorSpikeCast = Resources.Load<AudioClip>("Jotunnslayer/GhostKing/OriginalGhostKingCast");
                warriorSpikeImpact = Resources.Load<AudioClip>(warriorV2
                    ? "Jotunnslayer/JotunnWarrior/OriginalWarriorSpikeCone"
                    : "Jotunnslayer/GhostKing/OriginalGhostKingImpact");
                warriorStompVariants = new[]
                {
                    Resources.Load<AudioClip>("Jotunnslayer/JotunnWarrior/OriginalWarriorStomp1"),
                    Resources.Load<AudioClip>("Jotunnslayer/JotunnWarrior/OriginalWarriorStomp2"),
                    Resources.Load<AudioClip>("Jotunnslayer/JotunnWarrior/OriginalWarriorStomp3")
                };
                WarriorSecondaryAudio = warriorAudioEmitter.gameObject.AddComponent<AudioSource>();
                WarriorSecondaryAudio.playOnAwake = false;
                WarriorSecondaryAudio.spatialBlend = 1f;
                WarriorSecondaryAudio.maxDistance = 24f;
                WarriorSecondaryAudio.volume = .7f;
            }
            if (ghostKing)
            {
                var clip = Resources.Load<AudioClip>("Jotunnslayer/GhostKing/OriginalGhostKingImpact");
                if (clip)
                {
                    var emitter = new GameObject("Ghost king impact audio");
                    emitter.transform.SetParent(transform, false);
                    GhostKingImpactAudio = emitter.AddComponent<AudioSource>();
                    GhostKingImpactAudio.playOnAwake = false;
                    GhostKingImpactAudio.spatialBlend = 1f;
                    GhostKingImpactAudio.maxDistance = 20f;
                    GhostKingImpactAudio.volume = .7f;
                    GhostKingImpactAudio.clip = clip;
                }
            }
            if (elementalPlague)
                plagueSplashClips = new[]
                {
                    Resources.Load<AudioClip>("Jotunnslayer/ElementalMage/OriginalPlagueFlight01"),
                    Resources.Load<AudioClip>("Jotunnslayer/ElementalMage/OriginalPlagueFlight02"),
                    Resources.Load<AudioClip>("Jotunnslayer/ElementalMage/OriginalPlagueFlight03")
                };
            if (PoisonMage)
                poisonOrbCastAudio = Resources.Load<AudioClip>("Jotunnslayer/ElementalMage/OriginalPoisonOrbCast");
            if (elementalMageV2)
                mageFireAudio = Resources.Load<AudioClip>("Jotunnslayer/ElementalMage/OriginalMageFireRing");
            if (constructBoss)
            {
                constructLaserAudio = Resources.Load<AudioClip>("Jotunnslayer/ConstructBoss/OriginalLaser01");
                constructLaserAlternate = Resources.Load<AudioClip>("Jotunnslayer/ConstructBoss/OriginalLaser02");
                constructFlameAudio = Resources.Load<AudioClip>("Jotunnslayer/ConstructBoss/OriginalFlameChargeAndBurn");
                constructHammerSmash = Resources.Load<AudioClip>("Jotunnslayer/ConstructBoss/OriginalHammerSmash01");
                constructHammerSmashAlternate = Resources.Load<AudioClip>("Jotunnslayer/ConstructBoss/OriginalHammerSmash02");
                if (constructHammerSmash)
                {
                    var impact = new GameObject("Construct hammer impact audio");
                    impact.transform.SetParent(transform, false);
                    ConstructHammerAudio = impact.AddComponent<AudioSource>();
                    ConstructHammerAudio.playOnAwake = false;
                    ConstructHammerAudio.spatialBlend = 1f;
                    ConstructHammerAudio.maxDistance = 20f;
                    ConstructHammerAudio.volume = .7f;
                }
            }
            if (hybBoss || hybCaster || niflheimBossNormal || ettinBoss)
            {
                if (niflheimBossNormal)
                {
                    goatFreezeCast = Resources.Load<AudioClip>("Jotunnslayer/NiflheimBossEasy/OriginalGoatFreezeCast");
                    goatRotatorBlast = Resources.Load<AudioClip>("Jotunnslayer/NiflheimBossEasy/OriginalGoatRotatorBlast");
                }
                hybCircleBlast = Resources.Load<AudioClip>(niflheimBossNormal
                    ? "Jotunnslayer/NiflheimBossEasy/OriginalGoatFreezeBlast" : ettinBoss
                        ? "Jotunnslayer/EttinBoss/OriginalLightningImpact" : "Jotunnslayer/HybBossEasy/OriginalCircleBlast");
                int maxCircles = ettinBoss ? 15 : niflheimBossNormal ? 5 : hybBossAdvanced ? (hybBossHard ? 6 : 5) : HybCircleCount;
                hybCircleWarnings = new LineRenderer[maxCircles];
                hybCircleCenters = new Vector3[maxCircles];
                hybCircleDetonations = new float[maxCircles];
                if (hybCircleBlast) hybCircleAudio = new AudioSource[maxCircles];
                if (hybBossAdvanced) hybNormalSummonCasts = new int[4];
            }
            guardian = sphinxGuardian || kind == Skeleton1HSpawn.Kind.Guardian;
            boss = kind == Skeleton1HSpawn.Kind.NiflheimBossEasy || GoatHasJumpAudio;
            alfheimPhase = kind == Skeleton1HSpawn.Kind.AlfheimPhase1 ? 1 :
                kind == Skeleton1HSpawn.Kind.AlfheimPhase2 ? 2 : 0;
            visual = presentation;
            body = character;
            target = victim;
            if (TryGetComponent(out BotAI bot))
            {
                bot.enabled = false;
                bot.StopAllCoroutines();
            }

            var motor = GetComponent<CharacterMotor>();
            motor.WalkSpeed = constructBoss ? 1.4f : ghostKing ? 1.7f : UsesDive || elementalMelee || elfWoman || jotunnWarrior ? 2.6f : muspelBoss ? 3f : ettinBoss || ettinEndless ? 4.5f : sphinxMiniBoss ? 2.6f : sphinxBow || hybBoss ? 4f : sphinxGuardian ? 2f : hybCaster ? 1.8f : alfheimPhase == 1 ? .8f : alfheimPhase == 2 ? 4.5f : boss ? (GoatHasJumpAudio ? 3.7f : 2.7f) : drider ? 2f : stoneJotunn ? 1.85f : alfheimVeteran ? 1.5f : (spider || sentinel) ? 2.73f : FollowSpeed;
            motor.RunSpeed = motor.WalkSpeed;
            if (!initialized)
            {
                body.MaxHealth = (hybCaster || drider || sphinxMiniBoss || niflheimBossEndless || ettinEndless || muspelHel || UsesDive || elementalMelee || elfWoman || ghostKing || jotunnWarrior) ? 1100 : constructBoss || muspelBoss || ettinBoss || boss || hybBoss || alfheimPhase != 0 ? 4000 : (guardian || alfheimVeteran || sentinelVeteran || stoneJotunn || milipede) ? 140 : spider && !spiderInsane ? 14 : BaseHealth;
                body.CurrentHealth = body.MaxHealth;
                initialized = true;
            }
            body.SetMovementInput(Vector2.zero);
            phase = Phase.Spawn;
            phaseEnds = Time.time + (sphinxBow || sphinxGuardian || sphinxMiniBoss || milipede || alfheimPhase != 0 || alfheimSpider || sentinel || elfMelee || elfRanged || stoneJotunn || drider ? visual.SpawnDuration : spider ? 1.3333334f : spawnDelay);
            return true;
        }

        internal void SetHyboreaSkillSet(int skillSet)
        {
            if (hybCaster && phase == Phase.Spawn)
                HyboreaSkillSet = skillSet;
        }

        internal void SetHyboreaBossSkillSet(int skillSet)
        {
            if (hybBoss) HyboreaSkillSet = Mathf.Clamp(skillSet, 0, hybBossAdvanced ? 3 : 2);
        }

        internal void SetHyboreaBossSuspended(bool suspended)
        {
            if (!hybBoss || !body || hybBossSuspended == suspended) return;
            if (suspended) ClearCombatActions();
            hybBossSuspended = suspended;
            visual.SetModelHidden(suspended);
            if (!suspended)
            {
                phase = Phase.Recovery;
                phaseEnds = Time.time;
            }
        }

        void Update()
        {
            if (!body || !body.isServer || hybBossSuspended) return;

            if (trainingSample && body.CurrentHealth <= 0)
            {
                // These samples have no imported death animation yet. Remove their own body;
                // never fall back to the carrier's unrelated soldier ragdoll.
                if (originalDeath) AudioSource.PlayClipAtPoint(originalDeath, body.transform.position, .7f);
                Mirror.NetworkServer.Destroy(gameObject);
                return;
            }

            if (body.CurrentHealth <= 0 || !target || target.CurrentHealth <= 0 || body.Team == target.Team)
            {
                Stop();
                return;
            }

            if (gmWaiting)
            {
                body.SetMovementInput(Vector2.zero);
                return;
            }

            float now = Time.time;
            if (spiderSplashInFlight && !spiderSplash)
            {
                spiderSplashInFlight = false;
                HideJumpWarning();
            }
            if (hybTeleportHiddenUntil > 0f && now >= hybTeleportHiddenUntil)
                FinishHybTeleport();
            if (body.Block || SkillBattlePreview.IsStoppedByRaidControl(body, now))
            {
                if (warriorSpikeReleaseAt > 0f) CancelWarriorSpikes();
                secondStrikeAt = 0f;
                if (constructFlameRoutine != null) StopConstructFlame();
                if (constructLaserRoutine != null)
                {
                    StopCoroutine(constructLaserRoutine);
                    constructLaserRoutine = null;
                    HideConstructLasers();
                    if (attackAudio) attackAudio.Stop();
                }
                if (sphinxMiniBlast != null)
                {
                    StopCoroutine(sphinxMiniBlast);
                    sphinxMiniBlast = null;
                    if (attackAudio) attackAudio.Stop();
                    HideJumpWarning();
                }
                if (phase == Phase.Windup || burrowRoutine != null)
                {
                    if ((constructBoss || UsesDive || elementalMelee || ghostKing || jotunnWarrior || GoatHasJumpAudio && jumpAttack) && attackAudio) attackAudio.Stop();
                    CancelAlfheimBurrow();
                    if (WarriorSecondaryAudio) WarriorSecondaryAudio.Stop();
                    if (plagueSplashWindup && plagueSplashWarning) plagueSplashWarning.enabled = false;
                    if (ghostKing && elementalSpikes != null)
                        foreach (var warning in elementalSpikes)
                            if (warning) warning.enabled = false;
                    phase = Phase.Recovery;
                    phaseEnds = now + recovery;
                    HideJumpWarning();
                    visual.Play(Skeleton1HVisual.Motion.Hit, restart: true);
                }
                body.SetMovementInput(Vector2.zero);
                return;
            }

            if (boss && !niflheimBossEndless && !secondPhase && phase != Phase.Spawn && body.CurrentHealth * 2 <= body.MaxHealth)
            {
                secondPhase = true;
                if (niflheimBossNormal && attackAudio) attackAudio.Stop();
                StopAllCoroutines();
                HideJumpWarning();
                phase = Phase.PhaseChange;
                phaseEnds = now + 7.15f;
                body.SetMovementInput(Vector2.zero);
                visual.Play(Skeleton1HVisual.Motion.PhaseChange);
                return;
            }

            Vector3 delta = target.transform.position - body.transform.position;
            if (warriorSpikeReleaseAt > 0f)
            {
                if (now < warriorSpikeReleaseAt - (WarriorSpikeRelease - WarriorSpikeTurnEnd) &&
                    delta.x * delta.x + delta.z * delta.z > .0001f)
                {
                    float yawToTarget = Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg;
                    if (Mathf.Abs(Mathf.DeltaAngle(warriorSpikeYaw, yawToTarget)) >= .25f)
                    {
                        body.lookInput = new Vector2(0f, yawToTarget);
                        AimWarriorSpikes(yawToTarget);
                    }
                }
                UpdateWarriorSpikes(now);
            }
            if (phase == Phase.Recovery && secondStrikeAt > 0f && now >= secondStrikeAt)
            {
                secondStrikeAt = 0f;
                ResolveMeleeStrike(delta);
            }
            if (phase == Phase.Windup)
            {
                if (UsesDive && diveJumpAt > 0f && now >= diveJumpAt)
                    MoveToDivePoint();
                if (jotunnWarrior && warriorStompWarningAt > 0f && now >= warriorStompWarningAt)
                {
                    warriorStompWarningAt = 0f;
                    Vector3 ground = warriorStompPoint - Vector3.up;
                    ShowGroundWarning(ref jumpWarning, ground, 5f);
                    if (warriorStompWarningPrefab)
                    {
                        if (!WarriorStompWarningArt)
                            WarriorStompWarningArt = Instantiate(warriorStompWarningPrefab, ground, Quaternion.identity, transform);
                        else
                        {
                            WarriorStompWarningArt.transform.SetPositionAndRotation(ground, Quaternion.identity);
                            WarriorStompWarningArt.SetActive(true);
                        }
                    }
                }
                if (now < phaseEnds) return;

                phase = Phase.Recovery;
                phaseEnds = now + attackRecovery;
                if ((drider || stoneJotunn || elfRanged || elfMelee || sentinel || alfheimSpider ||
                    guardian || boss && !GoatHasJumpAudio || alfheimPhase != 0) && attackAudio)
                    attackAudio.Play();
                if (jotunnWarrior)
                {
                    if (warriorSpikeWindup) return;
                    HideJumpWarning();
                    if (warriorStompImpactPrefab)
                    {
                        Vector3 ground = warriorStompPoint - Vector3.up;
                        if (!WarriorStompImpactArt)
                            WarriorStompImpactArt = Instantiate(warriorStompImpactPrefab, ground, Quaternion.identity);
                        else
                        {
                            WarriorStompImpactArt.SetActive(false);
                            WarriorStompImpactArt.transform.SetPositionAndRotation(ground, Quaternion.identity);
                            WarriorStompImpactArt.SetActive(true);
                        }
                        ShowWarriorStompCrater(ground);
                    }
                    if (CanDamage(body, target, warriorStompPoint, 5f))
                    {
                        int before = target.CurrentHealth;
                        target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, body, 0);
                        if (target && target.CurrentHealth < before) WarriorStompHits++;
                    }
                    return;
                }
                if (ghostKing)
                {
                    StartCoroutine(CastGhostKingStrike());
                    return;
                }
                if (elfWoman)
                {
                    if (elfSummonWindup)
                    {
                        if (!alfheimSummons) alfheimSummons = gameObject.AddComponent<AlfheimSummons>();
                        alfheimSummons.LaunchElfWoman(body, target, elfLargeSummon);
                        return;
                    }
                    if (attackAudio) attackAudio.Play();
                    StartCoroutine(CastElfWomanSpikes());
                    return;
                }
                if (elementalMelee)
                {
                    if (championMissileWindup)
                    {
                        StartCoroutine(LaunchChampionMissiles());
                        return;
                    }
                    if (attackAudio && elementalSpikeAudio != null)
                    {
                        attackAudio.clip = elementalSpikeAudio[Random.Range(0, elementalSpikeAudio.Length)] ?? originalAttack;
                        attackAudio.Play();
                    }
                    StartCoroutine(CastElementalSpikes());
                    return;
                }
                if (UsesDive)
                {
                    HideJumpWarning();
                    if (plagueSplashWindup)
                    {
                        StartCoroutine(LaunchPlagueSplash());
                        return;
                    }
                    if (poisonOrbWindup)
                    {
                        poisonOrbs ??= new List<SkeletonBowArrow>(6);
                        poisonOrbs.RemoveAll(orb => !orb);
                        poisonOrbs.Add(SkeletonBowArrow.LaunchElementalPoisonOrb(body, target, Random.Range(90, 111)));
                        return;
                    }
                    if (mageFireWindup)
                    {
                        mageFireShots ??= new SkeletonBowArrow[7];
                        for (int i = 0; i < mageFireShots.Length; i++)
                        {
                            Vector3 direction = Quaternion.Euler(0f, attackYaw - 67.5f + 22.5f * i, 0f) * Vector3.forward;
                            mageFireShots[i] = SkeletonBowArrow.LaunchMageFire(body, target, direction, Random.Range(90, 111));
                        }
                        MageFireReleased += mageFireShots.Length;
                        return;
                    }
                    if (attackAudio && birdImpactAudio)
                    {
                        attackAudio.clip = birdImpactAudio;
                        attackAudio.Play();
                    }
                    if (CanDamage(body, target, diveLanding + Vector3.up, JumpImpactRadius))
                    {
                        int before = target.CurrentHealth;
                        target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, body, 0);
                        if (target && target.CurrentHealth < before) DiveHits++;
                    }
                    return;
                }

                if (constructBoss)
                {
                    HideJumpWarning();
                    if (constructLaserWindup)
                        constructLaserRoutine = StartCoroutine(ReleaseConstructLasers());
                    else if (constructFlameSide != 0)
                        constructFlameRoutine = StartCoroutine(SweepConstructFlame());
                    else if (constructHammerWindup)
                    {
                        if (ConstructHammerAudio)
                        {
                            ConstructHammerAudio.transform.position = constructHammerPoint;
                            ConstructHammerAudio.clip = constructHammerSmashAlternate && Random.value < .5f
                                ? constructHammerSmashAlternate : constructHammerSmash;
                            ConstructHammerAudio.Play();
                        }
                        if (CanDamage(body, target, constructHammerPoint, ConstructHammerRadius))
                            target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.explosion, body, 0);
                    }
                    else constructSaw = ConstructSaw.Launch(body, target, this, attackYaw);
                    return;
                }

                if (spiderSummonAttack)
                {
                    if (!alfheimSummons) alfheimSummons = gameObject.AddComponent<AlfheimSummons>();
                    alfheimSummons.Launch(body, target, veteranSummon);
                    return;
                }

                if (splashAttack)
                {
                    StartCoroutine(LaunchAlfheimSplash());
                    return;
                }

                if (burrowAttack)
                {
                    burrowRoutine = StartCoroutine(ResolveAlfheimBurrow(target.transform.position));
                    return;
                }

                if (spikesAttack)
                {
                    StartCoroutine(SpawnAlfheimSpikes());
                    return;
                }

                if (waveAttack)
                {
                    Quaternion facing = Quaternion.Euler(0f, attackYaw, 0f);
                    if (muspelBoss)
                    {
                        HideJumpWarning();
                        muspelWave = AlfheimSpiderWave.Launch(body, target, facing, Random.Range(90, 111), muspel: true);
                    }
                    else AlfheimSpiderWave.Launch(body, target, facing, damage);
                    return;
                }

                if (swordCast)
                {
                    Vector3 facing = Quaternion.Euler(0f, attackYaw, 0f) * Vector3.forward;
                    SkeletonBowArrow.LaunchElfSword(body, target, facing, damage);
                    return;
                }

                if (driderArea)
                {
                    StartCoroutine(SpawnDriderAreas());
                    return;
                }

                if (driderOrb)
                {
                    Vector3 facing = Quaternion.Euler(0f, attackYaw, 0f) * Vector3.forward;
                    SkeletonBowArrow.LaunchDriderOrb(body, target, facing, damage);
                    return;
                }

                if (driderWeb)
                {
                    HideJumpWarning();
                    if (CanDamage(body, target, driderWebCenter, DriderWebRadius))
                        target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
                    return;
                }

                if (stoneJotunn)
                {
                    HideJumpWarning();
                    if (CanDamage(body, target, stoneStompCenter, StoneStompRadius))
                        target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
                    return;
                }

                if (sphinxMiniBoss)
                {
                    Vector3 forward = Quaternion.Euler(0f, attackYaw, 0f) * Vector3.forward;
                    Vector3 center = body.transform.position + forward * SphinxMiniBlastOffset;
                    ShowGroundWarning(ref jumpWarning, center, SphinxMiniBlastRadius);
                    ShowSphinxMiniWarningArt(center);
                    if (attackAudio) attackAudio.Play();
                    sphinxMiniBlast = StartCoroutine(ResolveSphinxMiniBlast(center));
                    return;
                }

                if (spiderSplashCast)
                {
                    if ((playMeleeSwingAudio || hybCaster) && attackAudio) attackAudio.Play();
                    spiderSplash = SkeletonBowArrow.LaunchSpiderSplash(body, target, spiderSplashPoint, damage);
                    spiderSplashInFlight = true;
                    nextAttack = Mathf.Max(nextAttack, now + SkeletonBowArrow.SpiderSplashFlightTime);
                    return;
                }

                if (milipedeSpit)
                {
                    StartCoroutine(ReleaseMilipedeSpit());
                    return;
                }

                if (roundSlashAttack)
                {
                    HideJumpWarning();
                    Vector3 center = body.CombatAimPoint;
                    if (CanDamage(body, target, center, RoundSlashRadius))
                        target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.melee, body, 0);
                    return;
                }

                if (hybSummonAttack)
                {
                    if (hybSubBossNormal && hybSummonKind == Skeleton1HSpawn.Kind.SphinxBow)
                        hybSubBossBowVolleyReleased = true;
                    if (hybBossHard && HyboreaSkillSet == 3 && hybSummonKind == Skeleton1HSpawn.Kind.SphinxMiniBoss)
                        hybHardFinalMiniBossCasts++;
                    else if (hybBossAdvanced && hybSummonKind is Skeleton1HSpawn.Kind.SphinxBow or Skeleton1HSpawn.Kind.SphinxMiniBoss)
                        hybNormalSummonCasts[HyboreaSkillSet]++;
                    hybVolley = gameObject.AddComponent<HybSummonVolley>();
                    int count = hybSummonKind switch
                    {
                        Skeleton1HSpawn.Kind.SphinxSkeleton => 7,
                        Skeleton1HSpawn.Kind.SphinxMiniBoss => 1,
                        _ => 4
                    };
                    hybVolley.Begin(body, target, hybSummonKind, count);
                    return;
                }

                if (goatRotatorAttack)
                {
                    if (goatRotators) Destroy(goatRotators);
                    goatRotators = gameObject.AddComponent<GoatRotatorVolley>();
                    goatRotators.Begin(body, target, attackYaw, goatRotatorBlast);
                    return;
                }

                if (goatRingSummon)
                {
                    hybVolley = gameObject.AddComponent<HybSummonVolley>();
                    hybVolley.Begin(body, target, Skeleton1HSpawn.Kind.Guardian, 10, goatRing: true);
                    return;
                }

                if (ettinPoisonAttack)
                {
                    HideJumpWarning();
                    ettinPoison = gameObject.AddComponent<EttinPoisonVolley>();
                    ettinPoison.Begin(body, target, ettinPoisonFlight);
                    return;
                }

                if (ettinNova)
                {
                    StartCoroutine(ResolveEttinNova(body, target, jumpImpact));
                    return;
                }

                if (circlesAttack || goatFreezeAttack || ettinLightning)
                {
                    StartCoroutine(ResolveHybCircles(body, target, target.CombatAimPoint, goatFreezeAttack));
                    return;
                }

                if (hybBossBoltAttack)
                {
                    Vector3 forward = Quaternion.Euler(0f, attackYaw, 0f) * Vector3.forward;
                    StartCoroutine(ResolveHybBossBolts(body, target, forward));
                    return;
                }

                if (elfRanged)
                {
                    Vector3 facing = Quaternion.Euler(0f, attackYaw, 0f) * Vector3.forward;
                    SkeletonBowArrow.LaunchElfChain(body, target, facing, damage);
                    return;
                }

                if (archer)
                {
                    if ((playMeleeSwingAudio || hybCaster) && attackAudio) attackAudio.Play();
                    SkeletonBowArrow.Launch(body, target, shotDirection, damage, spell: hybCaster);
                    return;
                }

                if (jumpAttack)
                {
                    HideJumpWarning();
                    if (GoatHasJumpAudio && attackAudio && originalAttack)
                    {
                        attackAudio.clip = originalAttack;
                        attackAudio.Play();
                    }
                    StartCoroutine(ResolveJumpImpact(body, target, jumpImpact));
                    return;
                }

                if (summonAttack)
                {
                    NiflheimEasySkeletonWave.StartBossSummon(body, target, ghostSummon);
                    return;
                }

                if (hornsAttack)
                {
                    if (niflheimBossEndless)
                    {
                        StartCoroutine(ResolveEndlessHorns(body, target, jumpImpact));
                        return;
                    }
                    if (delta.sqrMagnitude <= HornsRadius * HornsRadius && HasClearStrike())
                        target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
                    return;
                }

                HideJumpWarning();
                ResolveMeleeStrike(delta);
                return;
            }

            if (burrowRoutine != null || (phase != Phase.Follow && now < phaseEnds)) return;
            phase = Phase.Follow;
            if ((hybTeleportHelpers != null || gmHybTeleport) && TryHybTeleport(now, delta)) return;
            float yaw = delta.x * delta.x + delta.z * delta.z > .0001f
                ? Mathf.Atan2(delta.x, delta.z) * Mathf.Rad2Deg
                : body.lookInput.y;
            body.lookInput = new Vector2(0, yaw);

            if (constructBoss)
            {
                // Playability adaptation: the original stationary machine slowly closes gaps.
                bool approach = delta.sqrMagnitude > 9f || !HasClearStrike();
                body.SetMovementInput(approach ? Vector2.up : Vector2.zero);
                visual.Play(Skeleton1HVisual.Motion.Idle);
                if (approach || now < nextAttack || delta.sqrMagnitude > 225f || !HasClearStrike()) return;
                body.SetMovementInput(Vector2.zero);
                constructLaserWindup = false;
                if (constructSkillSet == 3 && Random.value < .7f)
                {
                    constructLaserWindup = true;
                    attackYaw = yaw;
                    phase = Phase.Windup;
                    phaseEnds = now + .07001f;
                    attackRecovery = 3.75598f - .07001f;
                    nextAttack = now + 6f;
                    visual.Play(Skeleton1HVisual.Motion.ConstructLaser, restart: true);
                    return;
                }
                if (constructSkillSet >= 2 && delta.sqrMagnitude <= 100f)
                {
                    float choice = Random.value;
                    if (choice < (constructSkillSet == 2 ? .75f : .51f))
                    {
                        StartConstructFlame(now, yaw, choice < (constructSkillSet == 2 ? .375f : .255f) ? -1 : 1);
                        return;
                    }
                }
                if (constructSkillSet == 1)
                {
                    TryStartConstructHammer(now, delta.magnitude, yaw);
                    return;
                }

                constructFlameSide = 0;
                constructHammerWindup = false;
                attackYaw = yaw;
                phase = Phase.Windup;
                phaseEnds = now + 2.00494f;
                attackRecovery = 5.06667f - 2.00494f;
                nextAttack = now + 3f;
                Vector3 endpoint = body.transform.position +
                    Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * ConstructSaw.OutboundDistance;
                ShowGroundWarning(ref jumpWarning, endpoint, 1f);
                if (attackAudio)
                {
                    attackAudio.clip = constructSawAlternate && Random.value < .5f ? constructSawAlternate : originalAttack;
                    attackAudio.Play();
                }
                visual.Play(Skeleton1HVisual.Motion.Attack, restart: true);
                return;
            }

            if (UsesDive)
            {
                bool inRange = delta.sqrMagnitude <= 100f && HasClearStrike();
                body.SetMovementInput(inRange ? Vector2.zero : Vector2.up);
                visual.Play(inRange ? Skeleton1HVisual.Motion.Idle : Skeleton1HVisual.Motion.Walk);
                if (now < nextAttack) return;
                if (elementalPlague && inRange && !plagueSplashProjectile && now >= nextPlagueSplash)
                {
                    nextPlagueSplash = now + .5f;
                    if (Random.value < .5f)
                    {
                        Vector3 desired = Random.value < .4f ? target.transform.position :
                            body.transform.position + Quaternion.Euler(0f, yaw + Random.Range(-60f, 60f), 0f) *
                            Vector3.forward * Random.Range(0f, 6f);
                        if (TryGetAlfheimGround(desired, out plagueSplashPoint))
                        {
                            plagueSplashWindup = true;
                            poisonOrbWindup = false;
                            diveJumpAt = 0f;
                            phase = Phase.Windup;
                            phaseEnds = now + 1.4914f;
                            attackRecovery = 1.8872f - 1.4914f;
                            nextAttack = now + 2f;
                            body.SetMovementInput(Vector2.zero);
                            ShowGroundWarning(ref plagueSplashWarning, plagueSplashPoint, 4f);
                            visual.Play(Skeleton1HVisual.Motion.AlfheimSplash, restart: true);
                            return;
                        }
                    }
                }
                plagueSplashWindup = false;
                if (PoisonMage && now >= nextPoisonOrb && delta.sqrMagnitude <= 10000f && HasClearStrike())
                {
                    nextPoisonOrb = now + .5f;
                    if (Random.value < (elementalPlague ? .5f : .6f))
                    {
                        poisonOrbWindup = true;
                        diveJumpAt = 0f;
                        phase = Phase.Windup;
                        phaseEnds = now + 1.4819f;
                        attackRecovery = 1.9508f - 1.4819f;
                        nextPoisonOrb = now + 1f;
                        nextAttack = now + (elementalPlague ? 2f : 1f);
                        body.SetMovementInput(Vector2.zero);
                        ShowJumpWarning(target.transform.position);
                        if (attackAudio && poisonOrbCastAudio)
                        {
                            attackAudio.clip = poisonOrbCastAudio;
                            attackAudio.Play();
                        }
                        visual.Play(Skeleton1HVisual.Motion.AlfheimWave, restart: true);
                        return;
                    }
                }
                if (elementalMageV2 && now >= nextMageFire && delta.sqrMagnitude <= 10000f && HasClearStrike())
                {
                    nextMageFire = now + .5f;
                    if (Random.value < .5f)
                    {
                        mageFireWindup = true;
                        diveJumpAt = 0f;
                        attackYaw = yaw;
                        phase = Phase.Windup;
                        phaseEnds = now + 1.4914f;
                        attackRecovery = 1.8872f - 1.4914f;
                        nextMageFire = now + 5f;
                        nextAttack = now + 1f;
                        body.SetMovementInput(Vector2.zero);
                        ShowJumpWarning(target.transform.position);
                        if (attackAudio && mageFireAudio)
                        {
                            attackAudio.clip = mageFireAudio;
                            attackAudio.Play();
                        }
                        visual.Play(Skeleton1HVisual.Motion.AlfheimWave, restart: true);
                        return;
                    }
                }
                if (!inRange) return;
                poisonOrbWindup = false;
                mageFireWindup = false;
                nextAttack = now + .5f;
                float chance = elementalMageV2 || elementalPlague ? .5f : elementalMage ? .6f : .7f;
                if (Random.value >= chance || !TryGetAlfheimGround(target.transform.position, out diveLanding)) return;
                diveJumpAt = now + (elementalMage ? .5887f : 1.1787f);
                phase = Phase.Windup;
                phaseEnds = now + (elementalMage ? 1.4886f : 2.3051f);
                attackRecovery = elementalMage ? 1.9324f - 1.4886f : 3.2614f - 2.3051f;
                nextAttack = now + 4f;
                ShowJumpWarning(diveLanding);
                if (attackAudio && originalAttack)
                {
                    attackAudio.clip = originalAttack;
                    attackAudio.Play();
                }
                visual.Play(Skeleton1HVisual.Motion.Attack, restart: true);
                return;
            }

            if (jotunnWarrior)
            {
                float distanceSquared = Vector3.ProjectOnPlane(delta, Vector3.up).sqrMagnitude;
                bool inRange = distanceSquared <= 81f && HasClearStrike();
                body.SetMovementInput(inRange ? Vector2.zero : Vector2.up);
                visual.Play(inRange ? Skeleton1HVisual.Motion.Idle : Skeleton1HVisual.Motion.Walk);
                if (!inRange || now < nextAttack) return;
                if (distanceSquared > 25f)
                {
                    BeginWarriorSpikes(now, yaw);
                    return;
                }
                warriorSpikeWindup = false;
                Vector3 point = body.transform.position + Quaternion.Euler(0f, yaw, 0f) * Vector3.forward * 1.75f;
                if (!TryGetAlfheimGround(point, out var ground)) return;
                warriorStompPoint = ground + Vector3.up;
                const float stompRelease = .19421549f;
                const float impact = stompRelease + 1.35f;
                warriorStompWarningAt = now + stompRelease;
                phase = Phase.Windup;
                phaseEnds = now + impact;
                attackRecovery = 3.3312f - impact;
                nextAttack = now + 3f;
                warriorAudioEmitter.localPosition = Vector3.zero;
                if (attackAudio && originalAttack)
                {
                    attackAudio.clip = originalAttack;
                    attackAudio.Play();
                }
                if (WarriorSecondaryAudio)
                {
                    WarriorSecondaryAudio.clip = warriorStompVariants[Random.Range(0, warriorStompVariants.Length)];
                    if (WarriorSecondaryAudio.clip) WarriorSecondaryAudio.Play();
                }
                visual.Play(Skeleton1HVisual.Motion.Attack, restart: true);
                return;
            }
            if (ghostKing)
            {
                bool inRange = delta.sqrMagnitude <= 64f && HasClearStrike();
                body.SetMovementInput(inRange ? Vector2.zero : Vector2.up);
                visual.Play(inRange ? Skeleton1HVisual.Motion.Idle : Skeleton1HVisual.Motion.Walk);
                if (!inRange || now < nextAttack) return;
                nextAttack = now + .5f;
                if (Random.value >= .5f) return;
                attackYaw = yaw;
                ghostKingAttackOrigin = body.transform.position;
                elementalSpikes ??= new LineRenderer[4];
                for (int i = 0; i < elementalSpikes.Length; i++)
                    ShowGroundWarning(ref elementalSpikes[i], GhostKingStrikePoint(i), 1.5f);
                ElementalSpikeWarnings += elementalSpikes.Length;
                phase = Phase.Windup;
                phaseEnds = now + 1.1178f;
                attackRecovery = 1.7739f - 1.1178f;
                nextAttack = now + 3.2f;
                if (attackAudio) attackAudio.Play();
                visual.Play(Skeleton1HVisual.Motion.Attack, restart: true);
                return;
            }
            if (elfWoman)
            {
                bool clear = HasClearStrike();
                bool inRange = delta.sqrMagnitude <= 64f && clear;
                if (now >= nextAttack && now >= nextElfSummon && clear && delta.sqrMagnitude <= 100f &&
                    (ElementalSpikeWarnings > 0 || !inRange))
                {
                    elfSummonWindup = true;
                    elfLargeSummon = elfWomanV2 && nextElfSummonLarge;
                    nextElfSummonLarge = !nextElfSummonLarge;
                    nextElfSummon = now + 5f;
                    nextAttack = now + 1f;
                    phase = Phase.Windup;
                    phaseEnds = now + 1.0564f;
                    attackRecovery = 1.5641f - 1.0564f;
                    body.SetMovementInput(Vector2.zero);
                    visual.Play(Skeleton1HVisual.Motion.AlfheimSummon, restart: true);
                    return;
                }
                body.SetMovementInput(inRange ? Vector2.zero : Vector2.up);
                visual.Play(inRange ? Skeleton1HVisual.Motion.Idle : Skeleton1HVisual.Motion.Walk);
                if (!inRange || now < nextAttack) return;
                elfSummonWindup = false;
                phase = Phase.Windup;
                phaseEnds = now + .0381f;
                attackRecovery = 1.4915f - .0381f;
                nextAttack = now + 3f;
                visual.Play(Skeleton1HVisual.Motion.Attack, restart: true);
                return;
            }
            if (elementalMelee)
            {
                bool inRange = delta.sqrMagnitude <= 225f && HasClearStrike();
                body.SetMovementInput(inRange ? Vector2.zero : Vector2.up);
                visual.Play(inRange ? Skeleton1HVisual.Motion.Idle : Skeleton1HVisual.Motion.Walk);
                if (now < nextAttack) return;
                championMissileWindup = elementalChampion && now >= nextChampionMissile &&
                    delta.sqrMagnitude <= 10000f && HasClearStrike() && (!inRange || ElementalSpikeWarnings > 0);
                if (championMissileWindup)
                {
                    body.SetMovementInput(Vector2.zero);
                    phase = Phase.Windup;
                    phaseEnds = now + 2.1869f;
                    attackRecovery = 2.7557f - 2.1869f;
                    nextChampionMissile = now + 3.5f;
                    nextAttack = now + 3f;
                    ShowJumpWarning(target.transform.position);
                    if (attackAudio && championMissileAudio)
                    {
                        attackAudio.clip = championMissileAudio;
                        attackAudio.Play();
                    }
                    visual.Play(Skeleton1HVisual.Motion.AlfheimWave, restart: true);
                    return;
                }
                if (!inRange) return;
                phase = Phase.Windup;
                phaseEnds = now + .1242f;
                attackRecovery = 2.4468f - .1242f;
                nextAttack = now + 3f;
                visual.Play(Skeleton1HVisual.Motion.Attack, restart: true);
                return;
            }

            // Approximate caster spacing: retreat inside 6 m and pursue beyond 12 m.
            if (hybCaster)
            {
                bool retreat = delta.sqrMagnitude < 36f;
                bool approach = delta.sqrMagnitude > 144f || !HasClearStrike();
                if (retreat || approach)
                {
                    body.SetMovementInput(retreat ? Vector2.down : Vector2.up);
                    visual.Play(Skeleton1HVisual.Motion.Walk);
                    return;
                }
            }
            bool chase = !IsInReach(delta) || (archer && !elfRanged && !HasClearStrike());
            body.SetMovementInput(chase && !hybCaster ? Vector2.up : Vector2.zero);
            visual.Play(chase && !hybCaster ? Skeleton1HVisual.Motion.Walk : Skeleton1HVisual.Motion.Idle);
            if (now < nextAttack) return;
            waveAttack = false;
            if ((alfheimPhase == 2 || muspelBoss && !muspelWave) && now >= nextWaveCheck && delta.sqrMagnitude <= 100f * 100f)
            {
                waveAttack = Random.value < .3f;
                nextWaveCheck = now + 1f;
            }

            spikesAttack = false;
            if (!waveAttack && !spikesActive && alfheimPhase == 2 && now >= nextSpikesCheck &&
                delta.sqrMagnitude <= 15f * 15f)
            {
                spikesAttack = Random.value < .3f;
                nextSpikesCheck = now + 1f;
            }

            burrowAttack = false;
            if (!waveAttack && !spikesAttack && alfheimPhase == 2 && now >= nextBurrowCheck &&
                delta.sqrMagnitude >= 2f * 2f && delta.sqrMagnitude <= 20f * 20f)
            {
                burrowAttack = Random.value < .3f;
                nextBurrowCheck = now + 1f;
            }

            spiderSummonAttack = false;
            if (alfheimPhase == 1 && now >= nextSummonCheck && delta.sqrMagnitude <= 10f * 10f)
            {
                veteranSummon = now >= nextVeteranSummon && Random.value < .5f;
                spiderSummonAttack = veteranSummon || (now >= nextSpiderSummon && Random.value < .5f);
                nextSummonCheck = now + 1f;
            }

            splashAttack = false;
            if (!spiderSummonAttack && alfheimPhase == 1 && !splashLaunching && splashInFlight == 0 &&
                now >= nextSplashCheck && delta.sqrMagnitude <= 10f * 10f)
            {
                splashAttack = Random.value < .5f;
                nextSplashCheck = now + 1f;
            }

            driderArea = driderOrb = driderWeb = false;
            if (drider && now >= nextDriderCheck)
            {
                float distance = delta.sqrMagnitude;
                driderArea = driderVariant && now >= nextDriderArea && distance <= 15f * 15f;
                driderWeb = !driderArea && now >= nextDriderWeb && distance <= 5f * 5f &&
                    Random.value < (driderVariant ? .4f : .7f);
                driderOrb = !driderArea && !driderWeb && now >= nextDriderOrb && distance <= 100f * 100f &&
                    Random.value < (driderVariant ? .4f : .5f);
                nextDriderCheck = now + 1f;
            }

            swordCast = false;
            if (elfMeleeInsane && now >= nextSwordCast && delta.sqrMagnitude >= 25f && delta.sqrMagnitude <= 400f)
            {
                swordCast = Random.value < .4f;
                nextSwordCast = now + (swordCast ? 3f : 1f);
            }

            hybBossBoltAttack = false;
            if (hybBoss && now >= nextHybBossBolt && delta.sqrMagnitude <= 100f * 100f)
            {
                hybBossBoltAttack = Random.value < .3f;
                if (!hybBossBoltAttack) nextHybBossBolt = now + 1f;
            }
            milipedeSpit = false;
            if (milipede && now >= nextMilipedeSpit && delta.sqrMagnitude <= 13f * 13f)
            {
                milipedeSpit = Random.value < .8f;
                nextMilipedeSpit = now + (milipedeSpit ? 4f : .5f);
            }
            spiderSplashCast = false;
            if (spiderInsane && !spiderSplashInFlight && now >= nextSpiderSplashCheck &&
                delta.sqrMagnitude <= 13f * 13f && HasClearStrike())
            {
                spiderSplashCast = Random.value < .1f;
                nextSpiderSplashCheck = now + .5f;
            }
            goatFreezeAttack = false;
            if (niflheimBossNormal && secondPhase && !circlesActive && delta.sqrMagnitude <= 225f &&
                now >= nextGoatFreezeCheck)
            {
                goatFreezeAttack = Random.value < .6f;
                nextGoatFreezeCheck = now + 3f;
            }
            goatRingSummon = false;
            if (niflheimBossNormal && secondPhase && !goatFreezeAttack && !hybVolley &&
                delta.sqrMagnitude <= 100f && now >= nextGoatRingCheck)
            {
                goatRingSummon = Random.value < .3f;
                nextGoatRingCheck = now + 3f;
            }
            goatRotatorAttack = false;
            if (niflheimBossNormal && secondPhase && !goatFreezeAttack && !goatRingSummon && !goatRotators &&
                delta.sqrMagnitude <= 400f && now >= nextGoatRotatorCheck)
            {
                goatRotatorAttack = Random.value < .4f;
                nextGoatRotatorCheck = now + 3f;
            }
            bool endlessJump = false;
            bool endlessHorns = false;
            if (niflheimBossEndless && delta.sqrMagnitude <= 100f && now >= nextEndlessSkillCheck)
            {
                nextEndlessSkillCheck = now + 3f;
                endlessJump = Random.value < .3f;
                endlessHorns = !endlessJump && delta.sqrMagnitude <= 25f && Random.value < .7f;
                goatRingSummon = !endlessJump && !endlessHorns && !hybVolley && Random.value < .3f;
            }
            ettinLightning = false;
            if (ettinBoss && !circlesActive && delta.sqrMagnitude <= 225f && now >= nextEttinLightning)
            {
                ettinLightning = Random.value < .55f;
                nextEttinLightning = now + 1.5f;
            }
            ettinNova = false;
            ettinPoisonAttack = false;
            if (ettinEndless && delta.sqrMagnitude <= 400f && now >= nextEttinLightning)
            {
                ettinNova = delta.sqrMagnitude <= 56.25f && Random.value < .35f;
                ettinPoisonAttack = !ettinNova && !ettinPoison && Random.value < .5f;
                nextEttinLightning = now + 1.5f;
            }
            bool rangedCast = ettinPoisonAttack || ettinNova || ettinLightning || endlessJump || endlessHorns || goatFreezeAttack || goatRingSummon || goatRotatorAttack || spiderSplashCast || waveAttack || spikesAttack || burrowAttack || splashAttack || spiderSummonAttack ||
                driderArea || driderOrb || driderWeb || swordCast || hybBossBoltAttack || milipedeSpit;
            if (chase && !rangedCast) return;
            if (rangedCast) body.SetMovementInput(Vector2.zero);

            attackYaw = yaw;
            if (archer)
            {
                shotDirection = (target.CombatAimPoint - body.CombatAimPoint).normalized;
                if (shotDirection.sqrMagnitude < .01f) return;
            }

            float release = archer ? BowRelease : spider ? .28f : windup;
            float interval = archer ? BowAttackInterval : spiderInsane ? 2f : spider ? .6f : AttackInterval;
            attackRecovery = archer ? .8f - BowRelease : spider ? .3f : recovery;
            if (elfRanged)
            {
                release = .44673544f;
                attackRecovery = 2f - release;
            }
            if (alfheimSpider || elfMelee)
            {
                float duration = visual.AttackDuration;
                release = duration * .5f;
                attackRecovery = duration - release;
                interval = elfMelee ? duration : .7f;
            }
            if (stoneJotunn)
            {
                release = StoneStompRelease;
                attackRecovery = StoneStompDuration - release;
                interval = 4f;
                Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                stoneStompCenter = body.transform.position + forward * 1.75f + Vector3.up * .5f;
                ShowGroundWarning(ref jumpWarning, stoneStompCenter, StoneStompRadius);
            }
            if (sphinxMiniBoss)
            {
                release = .82805f / SphinxMiniCastSpeed;
                attackRecovery = (2.88409f - .82805f) / SphinxMiniCastSpeed;
                interval = .1f;
            }

            var motion = Skeleton1HVisual.Motion.Attack;
            if (spiderSplashCast)
            {
                motion = Skeleton1HVisual.Motion.AttackAlternate;
                release = .605203f / SpiderCastSpeed;
                attackRecovery = (.7666667f - .605203f) / SpiderCastSpeed;
                spiderSplashPoint = target.transform.position + Vector3.up * .22f;
                if (Physics.Raycast(target.transform.position + Vector3.up, Vector3.down, out var ground, 3f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                    spiderSplashPoint = ground.point + Vector3.up * .22f;
                ShowGroundWarning(ref jumpWarning, spiderSplashPoint, SkeletonBowArrow.SpiderSplashRadius);
            }
            if (milipede)
            {
                motion = milipedeSpit ? Skeleton1HVisual.Motion.AttackAlternate : Skeleton1HVisual.Motion.Attack;
                release = milipedeSpit ? .61252f : .67085f;
                attackRecovery = (milipedeSpit ? .95488f : 1.0666667f) - release;
                interval = 1.0666667f;
            }
            float hybBoltSpeed = hybBoss || HyboreaSkillSet == 0 ? HybInitialBoltSpeed : 1f;
            if (swordCast)
            {
                motion = Skeleton1HVisual.Motion.AttackAlternate;
                release = .2694881f / ElfSwordSpeed;
                attackRecovery = (1.2f - .2694881f) / ElfSwordSpeed;
                interval = 1.2f / ElfSwordSpeed;
            }
            if (drider)
            {
                interval = 1f;
                if (driderArea)
                {
                    motion = Skeleton1HVisual.Motion.BossSummon;
                    release = 1.1169759f;
                    attackRecovery = 2.266667f - release;
                    nextDriderArea = now + 6f;
                }
                else if (driderWeb)
                {
                    motion = Skeleton1HVisual.Motion.BossHorns;
                    release = .3865369f / DriderWebSpeed + 1f;
                    attackRecovery = 2.6333334f / DriderWebSpeed - release;
                    nextDriderWeb = now + .1f;
                    Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
                    driderWebCenter = body.transform.position + forward * 1.75f + Vector3.up * .5f;
                    ShowGroundWarning(ref jumpWarning, driderWebCenter, DriderWebRadius);
                }
                else if (driderOrb)
                {
                    motion = Skeleton1HVisual.Motion.AttackAlternate;
                    release = .5628866f;
                    attackRecovery = 1f - release;
                    nextDriderOrb = now + 2.5f;
                }
                else
                {
                    release = attackRecovery = 1f;
                }
            }
            bool normalJump = niflheimBossNormal && !goatFreezeAttack && !goatRingSummon && !goatRotatorAttack && Random.value < (secondPhase ? .25f : .3f);
            ghostSummon = boss && !niflheimBossEndless && !goatFreezeAttack && !goatRingSummon && !goatRotatorAttack && !normalJump && now >= nextGhostSummon &&
                NiflheimEasySkeletonWave.CanSummon(body, ghosts: true) && Random.value < .5f;
            summonAttack = ghostSummon || (boss && !niflheimBossEndless && !goatFreezeAttack && !goatRingSummon && !goatRotatorAttack && !normalJump && now >= nextSummon &&
                NiflheimEasySkeletonWave.CanSummon(body, ghosts: false) && Random.value < .5f);
            hornsAttack = endlessHorns || (boss && !niflheimBossEndless && !goatFreezeAttack && !goatRingSummon && !goatRotatorAttack && !normalJump && !summonAttack && Random.value < .7f);
            jumpAttack = endlessJump || normalJump || (boss && !GoatHasJumpAudio && !hornsAttack && !summonAttack && Random.value < .5f);
            if (sentinel)
            {
                bool alternate = meleeAttackSlot == 2;
                meleeAttackSlot = (meleeAttackSlot + 1) % 3;
                release = alternate ? .61164343f : .46167248f;
                attackRecovery = (alternate ? .91666675f : 1f) - release;
                interval = .7f;
                if (alternate) motion = Skeleton1HVisual.Motion.AttackAlternate;
            }
            if (guardian || (boss && !hornsAttack && !summonAttack && !jumpAttack))
            {
                bool alternate = meleeAttackSlot == 1;
                meleeAttackSlot = (meleeAttackSlot + 1) % 3;
                release = boss ? (alternate ? 1.1898625f : 1.1158763f)
                    : (alternate ? .5619215f : .55418867f);
                interval = boss ? 3f : 1.6f;
                float duration = boss ? (alternate ? 2.0833335f : 1.6500001f) : 1.4666668f;
                if (sphinxGuardian)
                {
                    release = alternate ? .46422577f : .5059497f;
                    secondStrikeAt = now + (alternate ? .47719297f : .5286041f);
                    duration = alternate ? 1.1333334f : 1.1000001f;
                }
                attackRecovery = duration - release;
                if (alternate) motion = Skeleton1HVisual.Motion.AttackAlternate;
            }

            if (hornsAttack)
            {
                motion = Skeleton1HVisual.Motion.BossHorns;
                if (niflheimBossEndless)
                {
                    jumpImpact = body.transform.position;
                    ShowGroundWarning(ref jumpWarning, jumpImpact, HornsRadius);
                }
                release = HornsRelease / HornsSpeed;
                attackRecovery = (HornsDuration - HornsRelease) / HornsSpeed;
                interval = 3f;
            }

            if (summonAttack)
            {
                motion = Skeleton1HVisual.Motion.BossSummon;
                release = .72164947f;
                attackRecovery = 1.7500001f - release;
                interval = 3f;
                if (ghostSummon) nextGhostSummon = now + 15f;
                else nextSummon = now + 10f;
            }

            if (jumpAttack)
            {
                motion = Skeleton1HVisual.Motion.BossJump;
                jumpImpact = target.CombatAimPoint;
                ShowJumpWarning(jumpImpact);
                if (GoatHasJumpAudio && attackAudio && goatJumpLaunch)
                {
                    attackAudio.clip = goatJumpLaunch;
                    attackAudio.Play();
                }
                release = 1.3512014f / JumpSpeed;
                attackRecovery = (2.15f - 1.3512014f) / JumpSpeed;
                interval = 3f;
            }

            if (ettinEndless)
            {
                interval = 1.5f;
                if (ettinPoisonAttack)
                {
                    motion = Skeleton1HVisual.Motion.HybSummon;
                    ShowGroundWarning(ref jumpWarning, target.transform.position, 6f);
                    release = 1.0769343f;
                    attackRecovery = 2.1745481f - release;
                }
                else if (ettinNova)
                {
                    motion = Skeleton1HVisual.Motion.HybCircles;
                    jumpImpact = body.transform.position;
                    release = .09445178f;
                    attackRecovery = 2.9999056f - release;
                }
                else
                {
                    release = .4f;
                    attackRecovery = .4166667f;
                }
            }

            if (ettinLightning)
            {
                motion = Skeleton1HVisual.Motion.HybCircles;
                release = 1.1851906f;
                attackRecovery = 2.2881002f - release;
                interval = 1.5f;
            }

            if (goatRotatorAttack)
            {
                motion = Skeleton1HVisual.Motion.HybCircles;
                release = .2796656f / .5f;
                attackRecovery = (3.278725f - .2796656f) / .5f;
                interval = 3f;
            }

            if (goatRingSummon)
            {
                motion = Skeleton1HVisual.Motion.BossSummon;
                release = .72164947f / .8f;
                attackRecovery = (1.7500001f - .72164947f) / .8f;
                interval = 3f;
            }

            if (goatFreezeAttack)
            {
                motion = Skeleton1HVisual.Motion.HybCircles;
                release = .2796656f / .8f;
                attackRecovery = (3.278725f - .2796656f) / .8f;
                interval = 3f;
            }

            if (hybBoss)
            {
                hybSummonAttack = !hybBossBoltAttack && !hybVolley && TrySelectHybBossSummon(now);
                circlesAttack = !hybBossBoltAttack && !hybSummonAttack;
                if (circlesAttack && circlesActive) return;
                if (hybBossBoltAttack)
                {
                    release = .8330725f / hybBoltSpeed;
                    attackRecovery = (1.6500001f - .8330725f) / hybBoltSpeed;
                    interval = 3.5f;
                    nextHybBossBolt = now + interval;
                }
                else
                {
                    motion = hybSummonAttack ? Skeleton1HVisual.Motion.HybSummon : Skeleton1HVisual.Motion.HybCircles;
                    release = hybSummonAttack ? 1.8720143f : .67777777f;
                    attackRecovery = (hybSummonAttack ? 2.9500003f : 2.0333335f) - release;
                    interval = hybSummonAttack ? 1.6f : 1f;
                }
            }
            else if (hybCaster)
            {
                bool inSpellRange = delta.sqrMagnitude <= 30f * 30f;
                float summonChance = HyboreaSkillSet == 0 && !hybSubBossNormal ? .2f : .3f;
                bool canSummon = !hybVolley && inSpellRange;
                bool summonBows = canSummon && hybSubBossNormal && HyboreaSkillSet is 1 or 2 &&
                    !hybSubBossBowVolleyReleased && Random.value < .2f;
                hybSummonKind = summonBows ? Skeleton1HSpawn.Kind.SphinxBow : Skeleton1HSpawn.Kind.SphinxSkeleton;
                hybSummonAttack = summonBows || (canSummon && Random.value < summonChance);
                circlesAttack = !hybSummonAttack && !circlesActive && inSpellRange && Random.value < .3f;
                if (circlesAttack) motion = Skeleton1HVisual.Motion.HybCircles;
                release = circlesAttack ? .67777777f : .8330725f / hybBoltSpeed;
                attackRecovery = (circlesAttack ? 2.0333335f : 1.6500001f / hybBoltSpeed) - release;
                interval = 1.7f;
                if (hybSummonAttack)
                {
                    motion = Skeleton1HVisual.Motion.HybSummon;
                    release = 1.8720143f;
                    attackRecovery = 2.9500003f - release;
                }
            }

            roundSlashAttack = !rangedCast && alfheimPhase == 2 && Random.value < .3f;
            if (spiderSummonAttack)
            {
                motion = Skeleton1HVisual.Motion.AlfheimSummon;
                release = SpiderSummonRelease;
                attackRecovery = SpiderSummonDuration - release;
                interval = 1.4f;
                if (veteranSummon) nextVeteranSummon = now + 7f;
                else nextSpiderSummon = now + 1.6f;
            }
            else if (splashAttack)
            {
                motion = Skeleton1HVisual.Motion.AlfheimSplash;
                release = SplashRelease;
                attackRecovery = SplashDuration - release;
                interval = 1.4f;
                nextSplashCheck = now + 2.5f;
            }
            else if (burrowAttack)
            {
                motion = Skeleton1HVisual.Motion.AlfheimBurrow;
                release = BurrowRelease;
                attackRecovery = BurrowDuration - release;
                interval = BurrowDuration;
                nextBurrowCheck = now + BurrowDuration + 5f;
            }
            else if (spikesAttack)
            {
                motion = Skeleton1HVisual.Motion.AlfheimSpikes;
                release = SpikesRelease;
                attackRecovery = SpikesDuration - release;
                interval = SpikesDuration;
                nextSpikesCheck = now + 3f;
            }
            else if (waveAttack)
            {
                motion = Skeleton1HVisual.Motion.AlfheimWave;
                release = muspelBoss ? 2.0181398f : WaveRelease;
                float duration = muspelBoss ? 3.2568388f : WaveDuration;
                attackRecovery = duration - release;
                interval = muspelBoss ? 1.7f : duration;
                nextWaveCheck = now + duration + 1f;
                if (muspelBoss)
                {
                    ShowGroundWarning(ref jumpWarning, target.transform.position, 1f);
                    if (attackAudio) attackAudio.Play();
                }
            }
            else if (roundSlashAttack)
            {
                motion = Skeleton1HVisual.Motion.AttackAlternate;
                release = RoundSlashRelease;
                attackRecovery = RoundSlashDuration - release;
                interval = RoundSlashDuration + .1f;
                ShowGroundWarning(ref jumpWarning, body.transform.position, RoundSlashRadius);
            }
            else if (alfheimPhase != 0)
            {
                float duration = alfheimPhase == 1 ? .4666667f : .3f;
                release = attackRecovery = duration * .5f;
                interval = alfheimPhase == 1 ? 1.4f : 1.7f;
            }

            nextAttack = now + interval;
            phase = Phase.Windup;
            phaseEnds = now + release;
            if (!archer && !rangedCast && !stoneJotunn && !sphinxMiniBoss && !roundSlashAttack &&
                motion is Skeleton1HVisual.Motion.Attack or Skeleton1HVisual.Motion.AttackAlternate)
                ShowGroundWarning(ref jumpWarning, body.transform.position, MeleeReach);
            float animationSpeed = spiderSplashCast ? SpiderCastSpeed
                : (hybCaster || hybBoss) && motion == Skeleton1HVisual.Motion.Attack ? hybBoltSpeed
                : goatRotatorAttack ? .5f : goatFreezeAttack || goatRingSummon ? .8f : hornsAttack ? HornsSpeed : jumpAttack ? JumpSpeed
                : swordCast ? ElfSwordSpeed : driderWeb ? DriderWebSpeed : roundSlashAttack ? RoundSlashSpeed
                : splashAttack ? SplashSpeed : spiderSummonAttack ? SpiderSummonSpeed
                : sphinxMiniBoss ? SphinxMiniCastSpeed : 1f;
            visual.Play(motion, restart: true, speed: animationSpeed);
        }

        IEnumerator LaunchPlagueSplash()
        {
            const float duration = 1.3f;
            const float arcHeight = 3f;
            Vector3 origin = body.CombatAimPoint;
            Vector3 destination = plagueSplashPoint;
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            plagueSplashProjectile = projectile;
            projectile.name = "Elemental plague splash";
            AudioClip flightClip = plagueSplashClips[Random.Range(0, plagueSplashClips.Length)];
            if (flightClip)
            {
                var source = projectile.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.maxDistance = 20f;
                source.volume = .7f;
                source.clip = flightClip;
                PlagueSplashFlightAudio = source;
                source.Play();
            }
            projectile.GetComponent<Collider>().enabled = false;
            projectile.transform.position = origin;
            projectile.transform.localScale = Vector3.one * .35f;
            float started = Time.time;
            while (Time.time - started < duration)
            {
                float progress = Mathf.Clamp01((Time.time - started) / duration);
                Vector3 next = Vector3.Lerp(origin, destination, progress) +
                    Vector3.up * (4f * arcHeight * progress * (1f - progress));
                if (Physics.Linecast(projectile.transform.position, next, GameManager.environmentLayer,
                    QueryTriggerInteraction.Ignore)) break;
                projectile.transform.position = next;
                yield return null;
            }
            if (Time.time - started >= duration) PlagueSplashLanded++;
            ClearPlagueSplash();
        }

        void ClearPlagueSplash()
        {
            if (plagueSplashWarning) plagueSplashWarning.enabled = false;
            if (PlagueSplashFlightAudio) PlagueSplashFlightAudio.Stop();
            PlagueSplashFlightAudio = null;
            if (!plagueSplashProjectile) return;
            Destroy(plagueSplashProjectile);
            plagueSplashProjectile = null;
        }

        IEnumerator LaunchChampionMissiles()
        {
            HideJumpWarning();
            championMissiles ??= new List<SkeletonBowArrow>(6);
            championMissiles.RemoveAll(shot => !shot);
            for (int i = 0; i < 3; i++)
            {
                if (!body || body.CurrentHealth <= 0 || !target || target.CurrentHealth <= 0) yield break;
                championMissiles.Add(SkeletonBowArrow.LaunchChampionMissile(body, target, i, Random.Range(90, 111)));
                ChampionMissilesReleased++;
                if (i < 2) yield return ChampionMissileSpacing;
            }
        }

        Vector3 GhostKingStrikePoint(int index) => ghostKingAttackOrigin +
            Quaternion.Euler(0f, attackYaw, 0f) * Vector3.forward * (1.4f + 3f * index);

        void AimWarriorSpikes(float yaw)
        {
            warriorSpikeYaw = yaw;
            var facing = Quaternion.Euler(0f, yaw, 0f);
            var forward = facing * Vector3.forward;
            var right = facing * Vector3.right;
            var origin = body.transform.position + forward * (warriorV2 ? 5f : 1.4f);
            int lanes = warriorV2 ? 3 : 1;
            for (int step = 0; step < WarriorSpikeSteps; step++)
            {
                var center = origin + forward * (step * 3f);
                int index = step * lanes;
                PlaceWarriorSpike(index, center);
                if (warriorV2)
                {
                    var offset = right * ((step + 2) * 1.5f);
                    PlaceWarriorSpike(index + 1, center - offset);
                    PlaceWarriorSpike(index + 2, center + offset);
                }
            }
        }

        void BeginWarriorSpikes(float now, float yaw)
        {
            AimWarriorSpikes(yaw);
            warriorSpikeIndex = 0;
            warriorSpikeWindup = true;
            warriorSpikeReleaseAt = now + WarriorSpikeRelease;
            phase = Phase.Windup;
            phaseEnds = warriorSpikeReleaseAt;
            attackRecovery = 3.3701737f - WarriorSpikeRelease;
            nextAttack = now + 3f;
            visual.Play(Skeleton1HVisual.Motion.AttackAlternate, restart: true);
            warriorAudioEmitter.localPosition = Vector3.up * WarriorBodyCenterHeight;
            if (attackAudio && warriorSpikeCast)
            {
                attackAudio.clip = warriorSpikeCast;
                attackAudio.Play();
            }
        }

        void PlaceWarriorSpike(int index, Vector3 point)
        {
            warriorSpikeGround[index] = null;
            if (TryGetAlfheimGround(point, out var ground))
            {
                warriorSpikeGround[index] = ground;
                ShowGroundWarning(ref elementalSpikes[index], ground, WarriorSpikeRadius);
            }
            else if (elementalSpikes[index])
                elementalSpikes[index].enabled = false;
        }

        void UpdateWarriorSpikes(float now)
        {
            int lanes = warriorV2 ? 3 : 1;
            while (warriorSpikeIndex < warriorSpikeGround.Length)
            {
                int index = warriorSpikeIndex;
                float offset = index / lanes * .12f + (index % lanes == 0 ? 0f : .10f);
                if (now < warriorSpikeReleaseAt + offset) return;
                if (index == 0 && WarriorSecondaryAudio && warriorSpikeImpact)
                {
                    WarriorSecondaryAudio.clip = warriorSpikeImpact;
                    WarriorSecondaryAudio.Play();
                }
                warriorSpikeIndex++;
                WarriorSpikesResolved++;
                var warning = elementalSpikes[index];
                if (warning) warning.enabled = false;
                if (warriorSpikeGround[index] is not Vector3 ground ||
                    !CanDamage(body, target, ground + Vector3.up, WarriorSpikeRadius)) continue;
                int before = target.CurrentHealth;
                target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, body, 0);
                if (target && target.CurrentHealth < before) WarriorSpikeHits++;
            }
            warriorSpikeReleaseAt = 0f;
        }

        void CancelWarriorSpikes()
        {
            warriorSpikeReleaseAt = 0f;
            if (attackAudio) attackAudio.Stop();
            if (WarriorSecondaryAudio) WarriorSecondaryAudio.Stop();
            foreach (var warning in elementalSpikes)
                if (warning) warning.enabled = false;
        }

        IEnumerator CastGhostKingStrike()
        {
            if (GhostKingImpactAudio)
            {
                GhostKingImpactAudio.transform.position = GhostKingStrikePoint(0);
                GhostKingImpactAudio.Play();
            }
            for (int i = 0; i < elementalSpikes.Length; i++)
            {
                var warning = elementalSpikes[i];
                if (warning) warning.enabled = false;
                Vector3 point = GhostKingStrikePoint(i);
                if (TryGetAlfheimGround(point, out var ground) && CanDamage(body, target, ground + Vector3.up, 1.5f))
                {
                    int before = target.CurrentHealth;
                    target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, body, 0);
                    if (target && target.CurrentHealth < before) ElementalSpikeHits++;
                }
                if (i + 1 < elementalSpikes.Length) yield return GhostKingStrikeSpacing;
            }
        }

        IEnumerator CastElfWomanSpikes()
        {
            int count = elfWomanV2 ? 7 : 4;
            elementalSpikes ??= new LineRenderer[count];
            Vector3 origin = body.transform.position;
            float yaw = Random.Range(0f, 360f);
            int precision = 0;
            for (int i = 0; i < count; i++)
            {
                if (i == 5) precision = 0;
                Vector3 point = origin + Quaternion.Euler(0f, yaw + 60f * i, 0f) * Vector3.forward * Random.Range(0f, 7f);
                if (target && precision < 3 && Random.value < .45f)
                {
                    point = target.transform.position;
                    precision++;
                }
                if (TryGetAlfheimGround(point, out var ground))
                {
                    ShowGroundWarning(ref elementalSpikes[i], ground, 2.5f);
                    ElementalSpikeWarnings++;
                    StartCoroutine(ResolveElementalSpike(ground + Vector3.up, elementalSpikes[i]));
                }
                if (i + 1 < count) yield return new WaitForSeconds(Random.Range(.085f, .12f));
            }
        }

        IEnumerator CastElementalSpikes()
        {
            const int innerCount = 5;
            const int totalCount = 13;
            elementalSpikes ??= new LineRenderer[totalCount];
            Vector3 origin = body.transform.position;
            float innerYaw = Random.Range(0f, 360f);
            float outerYaw = Random.Range(0f, 360f);
            int precisionPoints = 0;
            for (int i = 0; i < totalCount; i++)
            {
                bool inner = i < innerCount;
                float angle = inner ? innerYaw + 72f * i : outerYaw + 45f * (i - innerCount);
                float distance = inner ? 3f : Random.Range(5f, 12f);
                Vector3 point = origin + Quaternion.Euler(0f, angle, 0f) * Vector3.forward * distance;
                if (!inner && target && precisionPoints < 3 && Random.value < .25f)
                {
                    point = target.transform.position;
                    precisionPoints++;
                }

                if (TryGetAlfheimGround(point, out var ground))
                {
                    ShowGroundWarning(ref elementalSpikes[i], ground, inner ? 1.5f : 2.5f);
                    ElementalSpikeWarnings++;
                    StartCoroutine(ResolveElementalSpike(ground + Vector3.up, elementalSpikes[i]));
                }
                yield return ElementalSpikeSpacing;
            }
        }

        IEnumerator ResolveElementalSpike(Vector3 center, LineRenderer warning)
        {
            yield return ElementalSpikeFuse;
            if (warning) warning.enabled = false;
            if (elfWoman && warning && elfSpikeBlastClips != null)
            {
                var clip = elfSpikeBlastClips[Random.Range(0, elfSpikeBlastClips.Length)];
                if (clip)
                {
                    if (!warning.TryGetComponent(out AudioSource source))
                    {
                        source = warning.gameObject.AddComponent<AudioSource>();
                        source.playOnAwake = false;
                        source.spatialBlend = 1f;
                        source.maxDistance = 20f;
                        source.volume = .7f;
                    }
                    warning.transform.position = center;
                    source.clip = clip;
                    source.Play();
                }
            }
            Transform impact = null;
            if (elfWoman && warning)
            {
                impact = warning.transform.Find("Elf spike impact");
                if (!impact)
                {
                    var spike = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    spike.name = "Elf spike impact";
                    spike.GetComponent<Collider>().enabled = false;
                    Destroy(spike.GetComponent<Collider>());
                    impact = spike.transform;
                    impact.SetParent(warning.transform, false);
                    impact.localScale = new Vector3(.45f, 1f, .45f);
                    elfSpikeColor ??= new MaterialPropertyBlock();
                    elfSpikeColor.SetColor(Shader.PropertyToID("_BaseColor"), new Color(.85f, .12f, .55f));
                    spike.GetComponent<MeshRenderer>().SetPropertyBlock(elfSpikeColor);
                }
                impact.position = center;
                impact.gameObject.SetActive(true);
            }
            if (CanDamage(body, target, center, 1.9f))
            {
                int before = target.CurrentHealth;
                target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, body, 0);
                if (target && target.CurrentHealth < before) ElementalSpikeHits++;
            }
            if (impact)
            {
                yield return ElfSpikeVisibleTime;
                if (impact) impact.gameObject.SetActive(false);
            }
        }

        void MoveToDivePoint()
        {
            diveJumpAt = 0f;
            Vector3 approach = diveLanding - body.transform.position;
            approach.y = 0f;
            Vector3 stop = diveLanding - approach.normalized * 1.25f;
            if (!TryGetAlfheimGround(stop, out var landing)) return;
            var controller = body.GetComponent<CharacterController>();
            if (!controller || !controller.enabled) return;
            Vector3 move = landing - body.transform.position;
            move.y = 0f;
            Vector3 before = body.transform.position;
            controller.Move(move);
            DiveMoved |= (body.transform.position - before).sqrMagnitude > .04f;
        }

        IEnumerator ReleaseConstructLasers()
        {
            const int beamCount = 7;
            const float halfWidth = .35f;
            const float length = 8f;
            Vector3 origin = body.transform.position + Vector3.up;
            constructLaserBeams ??= new LineRenderer[beamCount];
            for (int i = 0; i < beamCount; i++)
            {
                ShowGroundWarning(ref constructLaserBeams[i], origin, 1f);
                var beam = constructLaserBeams[i];
                Quaternion facing = Quaternion.Euler(0f, attackYaw - 90f + 30f * i, 0f);
                Vector3 forward = facing * Vector3.forward * length;
                Vector3 right = facing * Vector3.right * halfWidth;
                Vector3 ground = new Vector3(origin.x, beam.GetPosition(0).y, origin.z);
                beam.positionCount = 4;
                beam.SetPosition(0, ground - right);
                beam.SetPosition(1, ground + right);
                beam.SetPosition(2, ground + forward + right);
                beam.SetPosition(3, ground + forward - right);
                beam.startColor = beam.endColor = new Color(1f, .22f, .06f);
            }
            if (attackAudio && constructLaserAudio)
            {
                attackAudio.clip = constructLaserAlternate && Random.value < .5f ? constructLaserAlternate : constructLaserAudio;
                attackAudio.Play();
            }

            yield return new WaitForSeconds(1.5f);
            if (body && body.CurrentHealth > 0 && target && target.CurrentHealth > 0)
            {
                ConstructLaserBeamCount = beamCount;
                foreach (var beam in constructLaserBeams)
                    beam.startColor = beam.endColor = Color.cyan;
                int hitMask = 0;
                float endsAt = Time.time + .5f;
                while (Time.time < endsAt && target && target.CurrentHealth > 0 && target.Team != body.Team)
                {
                    Vector3 aim = target.CombatAimPoint;
                    Vector3 delta = aim - origin;
                    if (Mathf.Abs(delta.y) <= .5f &&
                        !Physics.Linecast(origin, aim, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                    {
                        for (int i = 0; i < beamCount && target && target.CurrentHealth > 0; i++)
                        {
                            if ((hitMask & (1 << i)) != 0) continue;
                            Vector3 local = Quaternion.Euler(0f, -(attackYaw - 90f + 30f * i), 0f) * delta;
                            if (Mathf.Abs(local.x) > halfWidth || local.z < 0f || local.z > length) continue;
                            hitMask |= 1 << i;
                            int healthBefore = target.CurrentHealth;
                            target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.hitscan, body, 0);
                            if (target && target.CurrentHealth < healthBefore) ConstructLaserHits++;
                        }
                    }
                    yield return null;
                }
            }
            HideConstructLasers();
            constructLaserRoutine = null;
        }

        void HideConstructLasers()
        {
            if (constructLaserBeams == null) return;
            foreach (var beam in constructLaserBeams)
                if (beam) beam.enabled = false;
        }

        void StartConstructFlame(float now, float yaw, int side)
        {
            const float charge = 1.85f;
            constructFlameSide = side;
            attackYaw = yaw;
            phase = Phase.Windup;
            phaseEnds = now + charge;
            attackRecovery = 5f;
            nextAttack = now + 10f;
            Vector3 origin = body.transform.position + Vector3.up * 1.09f;
            ShowGroundWarning(ref jumpWarning, origin, 1f);
            DrawConstructFlame(origin, ConstructFlameFacing(0f));
            if (attackAudio && constructFlameAudio)
            {
                attackAudio.clip = constructFlameAudio;
                attackAudio.Play();
            }
            visual.Play(side < 0 ? Skeleton1HVisual.Motion.ConstructLeftFlame : Skeleton1HVisual.Motion.ConstructRightFlame,
                restart: true, speed: .7f);
        }

        Quaternion ConstructFlameFacing(float elapsed) =>
            Quaternion.Euler(0f, attackYaw + constructFlameSide * (80f - elapsed * 36f), 0f);

        void DrawConstructFlame(Vector3 origin, Quaternion facing)
        {
            Vector3 forward = facing * Vector3.forward * 14f;
            Vector3 right = facing * Vector3.right;
            float height = jumpWarning.GetPosition(0).y;
            origin.y = height;
            jumpWarning.positionCount = 4;
            jumpWarning.SetPosition(0, origin - right);
            jumpWarning.SetPosition(1, origin + right);
            jumpWarning.SetPosition(2, origin + forward + right);
            jumpWarning.SetPosition(3, origin + forward - right);
        }

        IEnumerator SweepConstructFlame()
        {
            Vector3 origin = body.transform.position + Vector3.up * 1.09f;
            float started = Time.time;
            int lastHitSector = -1;
            int hits = 0;
            ShowGroundWarning(ref jumpWarning, origin, 1f);
            while (body && target && target.CurrentHealth > 0)
            {
                float elapsed = Time.time - started;
                if (elapsed >= 5f) break;
                Quaternion facing = ConstructFlameFacing(elapsed);
                DrawConstructFlame(origin, facing);
                int sector = Mathf.FloorToInt(elapsed * 36f / 15f);
                Vector3 aim = target.CombatAimPoint;
                Vector3 local = Quaternion.Inverse(facing) * (aim - origin);
                if (hits < 20 && sector != lastHitSector && target.Team != body.Team &&
                    Mathf.Abs(local.x) <= 1f && Mathf.Abs(local.y) <= .5f && local.z >= 0f && local.z <= 14f &&
                    !Physics.Linecast(origin, aim, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                {
                    lastHitSector = sector;
                    hits++;
                    int before = target.CurrentHealth;
                    target.Server_ChangeHealthStateRaw(Random.Range(95, 106), CharacterPart.body, AttackType.explosion, body, 0);
                    if (target && target.CurrentHealth < before) ConstructFlameHits++;
                }
                yield return null;
            }
            constructFlameRoutine = null;
            HideJumpWarning();
        }

        void StopConstructFlame()
        {
            StopCoroutine(constructFlameRoutine);
            constructFlameRoutine = null;
            if (attackAudio) attackAudio.Stop();
            HideJumpWarning();
        }

        void TryStartConstructHammer(float now, float distance, float yaw)
        {
            bool near = distance >= 3.3f && distance <= 8.4f;
            bool far = distance >= 8.6f && distance <= 13f;
            if (!near && !far) return;
            if (Random.value >= .75f)
            {
                nextAttack = now + 1.5f;
                return;
            }

            const float speed = 1.4f;
            float release = (near ? .86764f : .90915f) / speed;
            float end = (near ? 3.27695f : 3.22814f) / speed;
            Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            constructHammerPoint = body.transform.position + forward * (near ? 7.3f : 10.5f);
            constructHammerPoint.y = body.CombatAimPoint.y;
            constructFlameSide = 0;
            constructHammerWindup = true;
            phase = Phase.Windup;
            phaseEnds = now + release;
            attackRecovery = end - release;
            nextAttack = now + 6f;
            ShowGroundWarning(ref jumpWarning, constructHammerPoint, ConstructHammerRadius);
            visual.Play(near ? Skeleton1HVisual.Motion.ConstructNearHammer : Skeleton1HVisual.Motion.ConstructFarHammer,
                restart: true, speed: speed);
        }

        void ResolveMeleeStrike(Vector3 delta)
        {
            if (playMeleeSwingAudio && attackAudio)
                attackAudio.Play();
            Vector3 forward = Quaternion.Euler(0, attackYaw, 0) * Vector3.forward;
            Vector3 horizontal = new Vector3(delta.x, 0, delta.z);
            if (IsInReach(delta) && Vector3.Dot(forward, horizontal.normalized) >= .5f && HasClearStrike())
                target.Server_ChangeHealthStateRaw(guardian ? Mathf.RoundToInt(damage * 2.5f) : damage,
                    CharacterPart.body, AttackType.melee, body, 0);
        }

        void ShowJumpWarning(Vector3 center) => ShowGroundWarning(ref jumpWarning, center, JumpImpactRadius);

        void ShowGroundWarning(ref LineRenderer warning, Vector3 center, float radius)
        {
            if (!warning)
            {
                if (!jumpWarningMaterial)
                {
                    var shader = Shader.Find("Sprites/Default");
                    if (!shader) return;
                    jumpWarningMaterial = new Material(shader);
                }

                var indicator = new GameObject("Enemy ground warning");
                indicator.transform.SetParent(transform);
                warning = indicator.AddComponent<LineRenderer>();
                warning.useWorldSpace = true;
                warning.loop = true;
                warning.positionCount = 48;
                warning.widthMultiplier = .07f;
                warning.startColor = warning.endColor = new Color(1f, .22f, .06f);
                warning.sharedMaterial = jumpWarningMaterial;
            }

            Vector3 ground = center;
            if (Physics.Raycast(center + Vector3.up * .5f, Vector3.down, out var hit, 5f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                ground = hit.point;
            ground += Vector3.up * .035f;

            warning.positionCount = 48;
            for (int i = 0; i < warning.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / warning.positionCount);
                warning.SetPosition(i, ground + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
            warning.enabled = true;
        }

        void HideJumpWarning()
        {
            warriorStompWarningAt = 0f;
            if (WarriorStompWarningArt) WarriorStompWarningArt.SetActive(false);
            if (jumpWarning) jumpWarning.enabled = false;
            if (sphinxMiniWarningArt) sphinxMiniWarningArt.gameObject.SetActive(false);
        }

        void ShowWarriorStompCrater(Vector3 ground)
        {
            if (!warriorCraterMaterial) return;
            if (!WarriorStompCrater)
            {
                var crater = new GameObject("Warrior stomp crater");
                crater.transform.SetParent(WarriorStompImpactArt.transform, false);
                warriorCraterMesh = new Mesh { name = "Warrior stomp crater plane" };
                warriorCraterMesh.vertices = new[]
                {
                    new Vector3(-5f, 0f, -5f), new Vector3(-5f, 0f, 5f),
                    new Vector3(5f, 0f, 5f), new Vector3(5f, 0f, -5f)
                };
                warriorCraterMesh.uv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
                warriorCraterMesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
                crater.AddComponent<MeshFilter>().sharedMesh = warriorCraterMesh;
                WarriorStompCrater = crater.AddComponent<MeshRenderer>();
                WarriorStompCrater.sharedMaterial = warriorCraterMaterial;
                warriorCraterProperties = new MaterialPropertyBlock();
            }
            Vector3 normal = Vector3.up;
            if (Physics.Raycast(ground + Vector3.up * .5f, Vector3.down, out var hit, 1f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                normal = hit.normal;
            WarriorStompCrater.transform.SetPositionAndRotation(ground + normal * .025f,
                Quaternion.FromToRotation(Vector3.up, normal));
            if (warriorCraterFade != null) StopCoroutine(warriorCraterFade);
            WarriorStompCrater.enabled = true;
            warriorCraterFade = StartCoroutine(FadeWarriorStompCrater());
        }

        IEnumerator FadeWarriorStompCrater()
        {
            float start = Time.time;
            for (float elapsed = 0f; elapsed < 3f; elapsed = Time.time - start)
            {
                var color = Color.white;
                color.a = Mathf.Min(Mathf.Clamp01(elapsed / .3f), Mathf.Clamp01((3f - elapsed) / 2f));
                warriorCraterProperties.SetColor(WarriorCraterColor, color);
                WarriorStompCrater.SetPropertyBlock(warriorCraterProperties);
                yield return null;
            }
            WarriorStompCrater.enabled = false;
            warriorCraterFade = null;
        }

        void ShowSphinxMiniWarningArt(Vector3 center)
        {
            if (!jumpWarning) return;
            if (!sphinxMiniWarningArt)
            {
                var shader = Resources.Load<Shader>("Jotunnslayer/HybBossEasy/HybSplitAdditive");
                var mask = Resources.Load<Texture2D>("Jotunnslayer/SphinxSkeleton/ring_shield01");
                if (!shader || !mask) return;
                sphinxMiniWarningMaterial = new Material(shader)
                {
                    mainTexture = mask,
                    color = new Color(1f, .5f, .12f, .85f)
                };
                sphinxMiniWarningMaterial.SetFloat("_MaskOnly", 1f);
                sphinxMiniWarningMaterial.SetFloat("_UseVertexColor", 0f);
                var art = GameObject.CreatePrimitive(PrimitiveType.Quad);
                art.name = "Sphinx blast warning mask";
                var collider = art.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                art.GetComponent<MeshRenderer>().sharedMaterial = sphinxMiniWarningMaterial;
                sphinxMiniWarningArt = art.transform;
            }
            center.y = jumpWarning.GetPosition(0).y + .01f;
            sphinxMiniWarningArt.SetPositionAndRotation(center, Quaternion.Euler(90f, 0f, 0f));
            sphinxMiniWarningArt.localScale = Vector3.one * SphinxMiniBlastRadius;
            sphinxMiniWarningArt.gameObject.SetActive(true);
        }

        IEnumerator SpawnDriderAreas()
        {
            driderAreaWarnings ??= new LineRenderer[3];
            Vector3 origin = body.transform.position;
            float startAngle = Random.Range(0f, 360f);
            for (int i = 0; i < driderAreaWarnings.Length; i++)
            {
                yield return new WaitForSeconds(Random.Range(.02f, .05f));
                Vector3 offset = Quaternion.Euler(0f, startAngle + 120f * i, 0f) * Vector3.forward * 4f;
                if (!TryGetAlfheimGround(origin + offset, out Vector3 center)) continue;

                ShowGroundWarning(ref driderAreaWarnings[i], center, DriderAreaRadius);
                StartCoroutine(ResolveDriderArea(i, center + Vector3.up * .1f));
            }
        }

        IEnumerator ResolveDriderArea(int index, Vector3 center)
        {
            yield return DriderAreaFuse;
            ResolveWarnedArea(body, target, center, DriderAreaRadius, driderAreaWarnings[index]);
        }

        IEnumerator ResolveSphinxMiniBlast(Vector3 center)
        {
            float startedAt = Time.time;
            while (Time.time < startedAt + SphinxMiniBlastDelay)
            {
                float progress = (Time.time - startedAt) / SphinxMiniBlastDelay;
                if (sphinxMiniWarningArt)
                    sphinxMiniWarningArt.localScale = Vector3.one * (SphinxMiniBlastRadius * (1f + progress));
                yield return null;
            }
            sphinxMiniBlast = null;
            HideJumpWarning();
            ResolveWarnedArea(body, target, center, SphinxMiniBlastRadius, jumpWarning);
        }

        IEnumerator ReleaseMilipedeSpit()
        {
            yield return MilipedeSpitDelay;
            if (body && body.CurrentHealth > 0 && target && target.CurrentHealth > 0 &&
                !body.Block && !SkillBattlePreview.IsStoppedByRaidControl(body, Time.time))
            {
                if (attackAudio) attackAudio.Play();
                SkeletonBowArrow.LaunchMilipedeSpit(body, target, damage);
            }
        }

        IEnumerator ResolveEttinNova(CharacterInstance caster, CharacterInstance victim, Vector3 center)
        {
            const float radius = 8f;
            ShowGroundWarning(ref jumpWarning, center, radius);
            if (attackAudio && originalAttack)
            {
                attackAudio.clip = originalAttack;
                attackAudio.Play();
            }
            if (ettinNovaLayer) ettinNovaLayer.Play();
            yield return new WaitForSeconds(1.6f);
            if (ettinNovaLayer) ettinNovaLayer.Stop();
            HideJumpWarning();
            if (!caster || caster.CurrentHealth <= 0) yield break;
            if (attackAudio && ettinNovaImpact)
            {
                attackAudio.clip = ettinNovaImpact;
                attackAudio.Play();
            }
            if (!CanDamage(caster, victim, center, radius) || !victim.TryGetComponent(out CharacterMotor motor))
                yield break;
            Vector3 direction = Vector3.ProjectOnPlane(victim.transform.position - center, Vector3.up).normalized;
            if (direction.sqrMagnitude == 0f) direction = caster.transform.forward;
            motor.ApplyServerRaidKnockback(direction * 3f);
            EttinNovaHits++;
        }

        IEnumerator ResolveHybCircles(CharacterInstance caster, CharacterInstance victim, Vector3 center, bool freeze)
        {
            circlesActive = true;
            if (attackAudio && (!freeze || goatFreezeCast))
            {
                if (freeze) attackAudio.clip = goatFreezeCast;
                attackAudio.Play();
            }
            float radius = ettinBoss ? 1f : 4f;
            float fuse = ettinBoss ? 1f : 1.5f;
            int count = ettinBoss ? 15 : freeze ? 5 : HybCircleCount;
            int precisionHits = 0;
            int detonated = 0;
            for (int i = 0; i < count; i++)
            {
                float warningAt = Time.time + (ettinBoss ? Random.Range(.04f, .05f) : Random.Range(.2f, .3f));
                while (detonated < i && hybCircleDetonations[detonated] <= warningAt)
                {
                    float untilBlast = hybCircleDetonations[detonated] - Time.time;
                    if (untilBlast > 0f) yield return new WaitForSeconds(untilBlast);
                    ResolveHybCircle(caster, victim, hybCircleCenters[detonated], radius,
                        hybCircleWarnings[detonated], detonated, freeze);
                    detonated++;
                }
                float untilWarning = warningAt - Time.time;
                if (untilWarning > 0f) yield return new WaitForSeconds(untilWarning);
                Vector3 position;
                if (ettinBoss)
                {
                    if (i % 5 == 0) precisionHits = 0;
                    if (precisionHits < 3 && Random.value < .25f)
                    {
                        position = center;
                        precisionHits++;
                    }
                    else
                    {
                        Vector2 offset = Random.insideUnitCircle * 7f;
                        position = center + new Vector3(offset.x, 0f, offset.y);
                    }
                }
                else position = PickCircleCenter(center, count >= 5, freeze);
                hybCircleCenters[i] = position;
                hybCircleDetonations[i] = Time.time + fuse;
                ShowGroundWarning(ref hybCircleWarnings[i], position, radius);
            }
            for (int i = detonated; i < count; i++)
            {
                float remaining = hybCircleDetonations[i] - Time.time;
                if (remaining > 0f) yield return new WaitForSeconds(remaining);
                ResolveHybCircle(caster, victim, hybCircleCenters[i], radius, hybCircleWarnings[i], i, freeze);
            }
            circlesActive = false;
        }

        void ResolveHybCircle(CharacterInstance caster, CharacterInstance victim, Vector3 center,
            float radius, LineRenderer warning, int index, bool freeze)
        {
            if (warning && hybCircleAudio != null)
            {
                var source = hybCircleAudio[index];
                if (!source)
                {
                    source = warning.gameObject.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.spatialBlend = 1f;
                    source.maxDistance = 20f;
                    source.volume = .7f;
                    source.clip = hybCircleBlast;
                    hybCircleAudio[index] = source;
                }
                warning.transform.position = center;
                source.Play();
            }
            if (!freeze && !ettinBoss)
            {
                ResolveWarnedArea(caster, victim, center, radius, warning);
                return;
            }
            if (warning) warning.enabled = false;
            if (!CanDamage(caster, victim, center, radius)) return;
            int health = victim.CurrentHealth;
            victim.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, caster, 0);
            if (ettinBoss && victim) EttinLightningDamage += Mathf.Max(0, health - victim.CurrentHealth);
            if (freeze && victim.CurrentHealth > 0 && victim.CurrentHealth < health)
                victim.ResearchRootUntil = System.Math.Max(victim.ResearchRootUntil, Mirror.NetworkTime.time + 2d);
        }

        static Vector3 PickCircleCenter(Vector3 center, bool wide, bool freeze)
        {
            if (Random.value < .35f) return center;
            float angle = Random.Range(0f, 2f * Mathf.PI);
            return center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * (wide ? Random.Range(0f, freeze ? 9f : 8f) : Random.Range(2f, 7f));
        }

        void ResolveWarnedArea(CharacterInstance caster, CharacterInstance victim, Vector3 center, float radius, LineRenderer ring)
        {
            if (ring) ring.enabled = false;
            if (CanDamage(caster, victim, center, radius))
                victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, caster, 0);
        }

        IEnumerator ResolveEndlessHorns(CharacterInstance caster, CharacterInstance victim, Vector3 center)
        {
            if (attackAudio && goatHornsCast)
            {
                attackAudio.clip = goatHornsCast;
                attackAudio.Play();
            }
            yield return new WaitForSeconds(.85f);
            HideJumpWarning();
            if (!caster || caster.CurrentHealth <= 0) yield break;
            if (attackAudio && goatHornsImpact)
            {
                attackAudio.clip = goatHornsImpact;
                attackAudio.Play();
            }
            if (CanDamage(caster, victim, center, HornsRadius))
            {
                int health = victim.CurrentHealth;
                victim.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, caster, 0);
                if (victim) EndlessHornsDamage += Mathf.Max(0, health - victim.CurrentHealth);
            }
        }

        IEnumerator ResolveJumpImpact(CharacterInstance caster, CharacterInstance victim, Vector3 center)
        {
            if (CanDamage(caster, victim, center, JumpImpactRadius))
                victim.Server_ChangeHealthStateRaw(niflheimBossEndless ? Random.Range(90, 111) : damage,
                    CharacterPart.body, AttackType.explosion, caster, 0);
            if (niflheimBossEndless) yield break;

            yield return new WaitForSeconds(.15f);
            for (int i = 0; i < 15; i++)
            {
                if (!caster || !caster.isServer || caster.CurrentHealth <= 0 ||
                    !victim || victim.CurrentHealth <= 0 || victim.Team == caster.Team) yield break;

                Vector2 offset = Random.insideUnitCircle * 9f;
                Vector3 shard = center + new Vector3(offset.x, 0f, offset.y);
                if (CanDamage(caster, victim, shard, 1f))
                    victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, caster, 0);

                yield return new WaitForSeconds(Random.Range(.06f, .1f));
            }
        }

        IEnumerator LaunchAlfheimSplash()
        {
            splashLaunching = true;
            splashCenters ??= new Vector3[SplashCount];
            splashWarnings ??= new LineRenderer[SplashCount];
            splashProjectiles ??= new GameObject[SplashCount];
            int placed = 0;
            for (int shot = 0; shot < SplashCount; shot++)
            {
                yield return SplashSpacing;
                if (!target || target.CurrentHealth <= 0) break;
                if (!TryPickSplashCenter(target.transform.position,
                    placed, out Vector3 center)) continue;

                splashCenters[placed] = center;
                ShowGroundWarning(ref splashWarnings[placed], center, 1f);
                StartCoroutine(ResolveAlfheimSplash(placed, center));
                placed++;
            }
            splashLaunching = false;
        }

        IEnumerator ResolveAlfheimSplash(int index, Vector3 center)
        {
            const float flightTime = 1.3f;
            Vector3 origin = body.CombatAimPoint;
            var projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "Alfheim splash projectile";
            projectile.transform.SetParent(transform);
            projectile.transform.position = origin;
            projectile.transform.localScale = Vector3.one * .3f;
            var collider = projectile.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            splashProjectiles[index] = projectile;
            splashInFlight++;

            float launched = Time.time;
            float fraction;
            do
            {
                yield return null;
                fraction = Mathf.Clamp01((Time.time - launched) / flightTime);
                float arc = Mathf.SmoothStep(0f, 1f, 1f - Mathf.Abs(fraction * 2f - 1f));
                projectile.transform.position = Vector3.Lerp(origin, center, fraction) + Vector3.up * (arc * 5f);
            }
            while (fraction < 1f);

            Destroy(projectile);
            splashProjectiles[index] = null;
            splashInFlight--;
            if (splashWarnings[index]) splashWarnings[index].enabled = false;
            if (CanDamage(body, target, center + Vector3.up * .1f, 1f))
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
        }

        bool TryPickSplashCenter(Vector3 targetPosition, int count, out Vector3 center)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                Vector2 offset = Random.insideUnitCircle.normalized * Random.Range(0f, 6f);
                Vector3 point = targetPosition + new Vector3(offset.x, 0f, offset.y);
                if (!TryGetAlfheimGround(point, out center)) continue;
                int previous = 0;
                while (previous < count && (splashCenters[previous] - center).sqrMagnitude >= 4f * 4f)
                    previous++;
                if (previous == count) return true;
            }

            center = default;
            return false;
        }

        bool TrySelectHybBossSummon(float now)
        {
            if (hybBossAdvanced) return TrySelectHybAdvancedSummon(now);
            if (HyboreaSkillSet == 1)
            {
                if (!hybBowSummoned && Random.value < .7f)
                {
                    hybSummonKind = Skeleton1HSpawn.Kind.SphinxBow;
                    hybBowSummoned = true;
                    return true;
                }
                if (!hybGuardianSummoned && Random.value < .3f)
                {
                    hybSummonKind = Skeleton1HSpawn.Kind.SphinxGuardian;
                    hybGuardianSummoned = true;
                    return true;
                }
            }

            if (now < nextHybBossSummon || Random.value >= .3f) return false;
            hybSummonKind = Skeleton1HSpawn.Kind.SphinxSkeleton;
            nextHybBossSummon = now + 1.6f;
            return true;
        }

        bool TrySelectHybAdvancedSummon(float now)
        {
            int group = HyboreaSkillSet;
            if (hybBossHard && group == 3 && hybHardFinalMiniBossCasts < 2 && Random.value < .8f)
            {
                hybSummonKind = Skeleton1HSpawn.Kind.SphinxMiniBoss;
                return true;
            }

            bool skeletons = group == 0 && now >= nextHybBossSummon && Random.value < .3f;
            if (!skeletons && group != 2 && hybNormalSummonCasts[group] < (group == 3 ? 4 : 2) &&
                Random.value < (group == 3 ? .5f : .7f))
            {
                hybSummonKind = Skeleton1HSpawn.Kind.SphinxBow;
                return true;
            }
            if (group == 1 && Random.value < .3f)
            {
                hybSummonKind = Skeleton1HSpawn.Kind.SphinxGuardian;
                return true;
            }
            if (group == 2 && hybNormalSummonCasts[group] == 0 && Random.value < .8f)
            {
                hybSummonKind = Skeleton1HSpawn.Kind.SphinxMiniBoss;
                return true;
            }
            if (!skeletons && (group == 0 || now < nextHybBossSummon || Random.value >= .3f)) return false;
            hybSummonKind = Skeleton1HSpawn.Kind.SphinxSkeleton;
            nextHybBossSummon = now + 1.6f;
            return true;
        }

        IEnumerator ResolveHybBossBolts(CharacterInstance caster, CharacterInstance victim, Vector3 forward)
        {
            for (int i = 0; i < 5; i++)
            {
                if (i > 0) yield return HybBossBoltSpacing;
                if (!caster || caster.CurrentHealth <= 0 || !victim || victim.CurrentHealth <= 0 ||
                    caster.Team == victim.Team) yield break;
                float spread = i == 0 ? 0f : Random.Range(-50f, 50f);
                SkeletonBowArrow.LaunchHybBossMissile(caster, victim, forward, spread, damage);
            }
        }

        internal void BindHyboreaTeleportHelpers(Transform[] helpers)
        {
            if (hybBoss) hybTeleportHelpers = helpers;
        }

        bool TryHybTeleport(float now, Vector3 toTarget)
        {
            float distanceSquared = toTarget.x * toTarget.x + toTarget.z * toTarget.z;
            if (distanceSquared > 30f * 30f) return false;
            bool near = (!hybBossAdvanced || HyboreaSkillSet != 3) &&
                distanceSquared <= 4f * 4f && now >= nextHybNearTeleport;
            if (near)
            {
                nextHybNearTeleport = now + 3f;
                near = Random.value < (HyboreaSkillSet == 0 ? .5f : .35f);
            }
            if (!near && now < nextHybTeleport) return false;

            Vector3 targetPosition = target.transform.position;
            if (!TryGetHybTeleportLanding(targetPosition, out var landing))
            {
                if (gmHybTeleport && hybTeleportHelpers == null && !near)
                    nextHybTeleport = now + .5f;
                return false;
            }

            if (!near) nextHybTeleport = now + (hybBossAdvanced && HyboreaSkillSet == 3 ? 7f : 12f);
            body.SetMovementInput(Vector2.zero);
            visual.Play(Skeleton1HVisual.Motion.Idle);
            visual.SetModelHidden(true);
            hybTeleportHiddenUntil = now + .7f;
            phase = Phase.Recovery;
            phaseEnds = hybTeleportHiddenUntil;

            var controller = body.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false;
            body.transform.position = landing;
            if (controllerWasEnabled) controller.enabled = true;
            Vector3 facing = targetPosition - landing;
            body.lookInput = new Vector2(0f, Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg);
            return true;
        }

        bool TryGetHybTeleportLanding(Vector3 targetPosition, out Vector3 landing)
        {
            landing = default;
            int candidates = 0;
            if (hybTeleportHelpers != null)
            {
                foreach (var helper in hybTeleportHelpers)
                {
                    if (!helper) continue;
                    Vector3 separation = helper.position - targetPosition;
                    if (separation.x * separation.x + separation.z * separation.z < 5f * 5f) continue;
                    if (Random.Range(0, ++candidates) == 0) landing = helper.position;
                }
                return candidates > 0;
            }

            int mask = GameManager.environmentLayer;
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = targetPosition + Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward * 8f;
                if (!Physics.Raycast(candidate + Vector3.up * 4f, Vector3.down, out var ground, 8f,
                    mask, QueryTriggerInteraction.Ignore) || ground.normal.y < .65f ||
                    Mathf.Abs(ground.point.y - targetPosition.y) > 2f ||
                    (ground.point - body.transform.position).sqrMagnitude < 4f) continue;
                Vector3 point = ground.point + Vector3.up * .15f;
                if (Physics.CheckCapsule(point + Vector3.up * .5f, point + Vector3.up * 1.5f, .45f,
                    mask, QueryTriggerInteraction.Ignore)) continue;
                if (Random.Range(0, ++candidates) == 0) landing = point;
            }
            return candidates > 0;
        }

        void FinishHybTeleport()
        {
            if (hybTeleportHiddenUntil <= 0f) return;
            hybTeleportHiddenUntil = 0f;
            if (visual) visual.SetModelHidden(false);
        }

        IEnumerator ResolveAlfheimBurrow(Vector3 aimedPosition)
        {
            yield return new WaitForSeconds(1f);
            if (!TryGetAlfheimGround(aimedPosition, out Vector3 center))
            {
                burrowRoutine = null;
                yield break;
            }

            ShowGroundWarning(ref jumpWarning, center, BurrowRadius);
            visual.SetModelHidden(true);
            yield return new WaitForSeconds(1.7f);
            if (!body || !target || target.CurrentHealth <= 0 ||
                !TryGetBurrowLanding(center, out Vector3 landing))
            {
                FinishAlfheimBurrow();
                yield break;
            }

            var controller = body.GetComponent<CharacterController>();
            bool controllerWasEnabled = controller && controller.enabled;
            if (controllerWasEnabled) controller.enabled = false;
            body.transform.position = landing;
            if (controllerWasEnabled) controller.enabled = true;
            Vector3 toTarget = target.transform.position - landing;
            if (toTarget.x * toTarget.x + toTarget.z * toTarget.z > .0001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(1f);

            FinishAlfheimBurrow();
            if (CanDamage(body, target, center + Vector3.up * .1f, BurrowRadius))
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
        }

        bool TryGetBurrowLanding(Vector3 center, out Vector3 landing)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Quaternion facing = Quaternion.Euler(0f, attackYaw + attempt * 45f, 0f);
                Vector3 candidate = center - facing * Vector3.forward;
                if (!TryGetAlfheimGround(candidate, out Vector3 ground)) continue;
                Vector3 point = ground + Vector3.up * .15f;
                if (Physics.CheckCapsule(point + Vector3.up * .5f, point + Vector3.up * 1.5f, .45f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;

                landing = point;
                return true;
            }

            landing = default;
            return false;
        }

        static bool TryGetAlfheimGround(Vector3 position, out Vector3 ground)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var hit, 7f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore) && hit.normal.y >= .65f)
            {
                ground = hit.point;
                return true;
            }

            ground = default;
            return false;
        }

        void CancelAlfheimBurrow()
        {
            if (burrowRoutine == null) return;
            StopCoroutine(burrowRoutine);
            FinishAlfheimBurrow();
        }

        void FinishAlfheimBurrow()
        {
            burrowRoutine = null;
            if (visual) visual.SetModelHidden(false);
            HideJumpWarning();
        }

        IEnumerator SpawnAlfheimSpikes()
        {
            spikesActive = true;
            spikeWarnings ??= new LineRenderer[SpikeCount];
            spikeCenters ??= new Vector3[SpikeCount];
            int placed = 0;
            bool precisionUsed = false;
            for (int shard = 0; shard < SpikeCount; shard++)
            {
                yield return SpikeSpacing;
                if (!target || target.CurrentHealth <= 0)
                    break;
                if (shard == 4) precisionUsed = false;
                bool precise = !precisionUsed && Random.value < .3f;
                if (!TryPickSpikeCenter(target.transform.position, precise, placed, out Vector3 center))
                    continue;

                precisionUsed |= precise;
                spikeCenters[placed] = center;
                ShowGroundWarning(ref spikeWarnings[placed], center, SpikeRadius);
                StartCoroutine(ResolveAlfheimSpike(placed, center + Vector3.up * .1f));
                placed++;
            }
            yield return SpikeFuse;
            spikesActive = false;
        }

        bool TryPickSpikeCenter(Vector3 targetPosition, bool precise, int placed, out Vector3 center)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                Vector2 direction = Random.insideUnitCircle.normalized;
                float distance = precise && attempt == 0 ? 0f : Random.Range(.5f, 7f);
                Vector3 candidate = targetPosition + new Vector3(direction.x, 0f, direction.y) * distance;
                if (!Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out var hit, 7f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;

                bool overlaps = false;
                for (int i = 0; i < placed; i++)
                {
                    if ((spikeCenters[i] - hit.point).sqrMagnitude >= 3f * 3f) continue;
                    overlaps = true;
                    break;
                }
                if (overlaps) continue;

                center = hit.point;
                return true;
            }

            center = default;
            return false;
        }

        IEnumerator ResolveAlfheimSpike(int index, Vector3 center)
        {
            yield return SpikeFuse;
            if (spikeWarnings[index]) spikeWarnings[index].enabled = false;
            if (CanDamage(body, target, center, SpikeRadius))
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
        }

        static bool CanDamage(CharacterInstance caster, CharacterInstance victim, Vector3 point, float radius)
        {
            return caster && caster.isServer && caster.CurrentHealth > 0 &&
                victim && victim.CurrentHealth > 0 && victim.Team != caster.Team &&
                (victim.CombatAimPoint - point).sqrMagnitude <= radius * radius &&
                !Physics.Linecast(point, victim.CombatAimPoint, GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
        }

        bool HasClearStrike()
        {
            Vector3 origin = body.transform.position + Vector3.up;
            Vector3 end = target.transform.position + Vector3.up;
            return !Physics.Linecast(origin, end, GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
        }

        bool IsInReach(Vector3 delta)
        {
            if (archer)
            {
                float range = hybBoss ? 30f : hybCaster ? 100f : elfRanged ? 10f : BowRange;
                return delta.sqrMagnitude <= range * range;
            }
            if (stoneJotunn) return delta.sqrMagnitude <= StoneStompRadius * StoneStompRadius;
            if (sphinxMiniBoss) return delta.sqrMagnitude <= SphinxMiniBlastRadius * SphinxMiniBlastRadius;
            return Mathf.Abs(delta.y) <= 1f && delta.x * delta.x + delta.z * delta.z <= MeleeReach * MeleeReach;
        }

        float MeleeReach => boss || alfheimPhase != 0 ? reach + 1f : guardian ? reach + .5f : reach;

        void Stop()
        {
            ClearCombatActions();
            if (hybBossSuspended && visual) visual.SetModelHidden(false);
            hybBossSuspended = false;
            body = null;
            target = null;
        }

        void ClearCombatActions()
        {
            if (WarriorStompImpactArt) WarriorStompImpactArt.SetActive(false);
            warriorCraterFade = null;
            if (warriorSpikeReleaseAt > 0f) CancelWarriorSpikes();
            secondStrikeAt = 0f;
            diveJumpAt = 0f;
            elfSummonWindup = false;
            mageFireWindup = false;
            poisonOrbWindup = false;
            plagueSplashWindup = false;
            ClearPlagueSplash();
            if (poisonOrbs != null)
            {
                foreach (var orb in poisonOrbs)
                    if (orb) orb.CancelElementalPoisonOrb();
                poisonOrbs.Clear();
            }
            championMissileWindup = false;
            if (championMissiles != null)
            {
                foreach (var shot in championMissiles)
                    if (shot) shot.CancelChampionMissile();
                championMissiles.Clear();
            }
            if (mageFireShots != null)
                foreach (var shot in mageFireShots)
                    if (shot) shot.CancelMageFire();
            if (spiderSplash) Destroy(spiderSplash.gameObject);
            spiderSplashInFlight = spiderSplashCast = false;
            if (attackAudio) attackAudio.Stop();
            if (ettinNovaLayer) ettinNovaLayer.Stop();
            if (hybCircleAudio != null)
                foreach (var source in hybCircleAudio)
                    if (source) source.Stop();
            FinishHybTeleport();
            CancelAlfheimBurrow();
            HideJumpWarning();
            if (hybCircleWarnings != null)
                foreach (var warning in hybCircleWarnings)
                    if (warning) warning.enabled = false;
            if (elementalSpikes != null)
                foreach (var warning in elementalSpikes)
                    if (warning)
                    {
                        warning.enabled = false;
                        if (elfWoman && warning.TryGetComponent(out AudioSource source)) source.Stop();
                        var impact = warning.transform.Find("Elf spike impact");
                        if (impact) impact.gameObject.SetActive(false);
                    }
            if (spikeWarnings != null)
                foreach (var warning in spikeWarnings)
                    if (warning) warning.enabled = false;
            if (driderAreaWarnings != null)
                foreach (var warning in driderAreaWarnings)
                    if (warning) warning.enabled = false;
            if (splashWarnings != null)
                foreach (var warning in splashWarnings)
                    if (warning) warning.enabled = false;
            if (splashProjectiles != null)
                foreach (var projectile in splashProjectiles)
                    if (projectile) Destroy(projectile);
            circlesActive = false;
            spikesActive = false;
            splashLaunching = false;
            splashInFlight = 0;
            if (hybVolley) Destroy(hybVolley);
            if (goatRotators) Destroy(goatRotators);
            if (ettinPoison) Destroy(ettinPoison);
            if (muspelWave) Destroy(muspelWave.gameObject);
            if (WarriorSecondaryAudio) WarriorSecondaryAudio.Stop();
            if (GhostKingImpactAudio) GhostKingImpactAudio.Stop();
            if (constructSaw) constructSaw.Cancel();
            constructFlameRoutine = null;
            constructLaserRoutine = null;
            HideConstructLasers();
            if (ConstructHammerAudio) ConstructHammerAudio.Stop();
            if (alfheimSummons) alfheimSummons.enabled = false;
            StopAllCoroutines();
            sphinxMiniBlast = null;
            if (body && body.isServer) body.SetMovementInput(Vector2.zero);
            if (body && body.CurrentHealth > 0 && visual) visual.Play(Skeleton1HVisual.Motion.Idle);
        }

        void OnDestroy()
        {
            if (WarriorStompImpactArt) Destroy(WarriorStompImpactArt);
            if (warriorCraterMesh) Destroy(warriorCraterMesh);
            if (jumpWarningMaterial) Destroy(jumpWarningMaterial);
            if (sphinxMiniWarningArt) Destroy(sphinxMiniWarningArt.gameObject);
            if (sphinxMiniWarningMaterial) Destroy(sphinxMiniWarningMaterial);
        }

        void OnDisable() => Stop();
    }
}
