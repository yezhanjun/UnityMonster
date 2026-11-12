using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.AI;
namespace MTPSKIT.Gameplay
{
    // Server-owned attack timing. Titans alternate ground strikes and sustained beams.
    public sealed partial class RangeResearchEnemy : MonoBehaviour
    {
        const float RainOriginalGravityY = -30f;

        CharacterInstance body,target,playerTarget;
        float nextDroneTargetScan;
        BotAI ai;
        bool aiWasEnabled;
        CharacterMotor configuredMotor;
        float previousWalkSpeed, previousRunSpeed;
        int kind,stage,originalMax;
        float until;
        Vector3 lockedPoint;
        GameObject warning;
        Material material;
        LineRenderer laser;
        GameObject titanFist;
        Vector3 fistPoint;
        float fistStartedAt;
        const float FistVisibleDuration = .6f;
        bool nextTitanBeam;
        bool titanBeam;
        float nextBeamTick;
        Vector3 beamDirection;
        Vector3 headbuttForward;
        bool headbuttHit;
        float headbuttOpensAt;
        const float HeadbuttReach = 3.2f;
        const float HeadbuttHalfWidth = .9f;
        const float TitanBeamCharge = 3f;
        const float TitanBeamDuration = 2f;
        const float TitanBeamInterval = .25f;
        RainActorVisual visual;
        bool combatEnabled;
        float stunnedUntil;
        private bool timeTrapped;
        internal bool IsStunned => Time.time < stunnedUntil;
        Camera healthBarCamera;
        Image healthBarBackground, healthBarFill;
        internal const int QueenKind = 8;
        internal const int GupKind = 18;
        internal const int GeepKind = 19;
        internal const int GipKind = 20;
        internal const float GupAttackDuration = 2f;
        const float GupSpikesRelease = .58f;
        internal static bool IsGupFamilyKind(int archetype) => archetype is GupKind or GeepKind or GipKind;
        bool IsGupFamily => IsGupFamilyKind(kind);
        bool gupSplitCombat;
        float GupSpikesRadius => kind == GeepKind ? 3.3f : kind == GipKind ? 2.4f : 4.5f;
        Vector3 GupWarningScale => new Vector3(GupSpikesRadius * 2f, .08f, GupSpikesRadius * 2f);
        float nextGupAttack;
        internal const int QueenHealth = 2100;
        internal const float QueenSpawnDuration = 5f;
        internal const float QueenSpitDuration = 2f;
        internal const float QueenWardCastDuration = 6f;
        const float QueenAcidRadius = 6f;
        float queenNextSpit, queenNextSummon, queenNextEgg, queenNextWardCast, queenWardRelease;
        Transform queenWardMuzzleParent;
        int queenEggsAttempted;
        List<RangeResearchEnemy> queenGuards;
        const int QueenSpitCount = 6;
        const float QueenSpitLifetime = 5f;
        static GameObject queenSpitPrefab;
        QueenAcidShot[] queenAcidShots;
        GameObject[] queenSpitVisuals;
        const int QueenAcidPoolCapacity = 12;
        static GameObject queenAcidPoolPrefab;
        QueenAcidPool[] queenAcidPools;
        GameObject[] queenAcidPoolVisuals;
        static readonly Vector3 QueenPoolHalfSize = new Vector3(1.4749095f, .49414873f, 1.4749099f);

        sealed class QueenAcidPool
        {
            internal Vector3 Position;
            internal Quaternion Rotation;
            internal float Expires, NextScan, NextReset;
            internal readonly HashSet<Health> Hits = new HashSet<Health>(8);
        }

        struct QueenAcidShot
        {
            internal Vector3 Position, Velocity;
            internal float Expires;
        }

        internal const int BruiserKind = 7;
        internal const int BruiserHealth = 900;
        internal const float BruiserSpawnDuration = 1.9f;
        internal const float BruiserBreathEntry = .3f;
        internal const float BruiserBreathDuration = 1.3f;
        const float BruiserBreathRange = 20f;
        internal const float BruiserFireCharge = 1f;
        internal const float BruiserFireDuration = 2f;
        const float BruiserFireInterval = .35f / 5f;
        const float BruiserFireSpeed = 45f;
        const float BruiserFireLifetime = 6f;
        const float BruiserBlastRadius = 6f;
        float bruiserNextBreath, bruiserNextTick, bruiserNextFire, bruiserFireStarted;
        int bruiserShots;
        const int BruiserFireballCapacity = 10;
        static GameObject bruiserFireballPrefab, bruiserFireballImpactPrefab, bruiserBreathPrefab;
        static readonly Vector3 BruiserMouthOffset = new Vector3(0f, 1.45f, -1.13f);
        Transform bruiserHead;
        GameObject bruiserBreathEffect;
        BruiserFireball[] bruiserFireballs;
        GameObject[] bruiserFireballVisuals;

        struct BruiserFireball
        {
            internal Vector3 Position, Direction;
            internal float Expires;
        }
        internal const int JellyfishKind = 6;
        internal const int JellyfishHealth = 60;
        internal const float JellyfishSpawnDuration = 1.5f;
        const float JellyfishNovaRadius = 10f;
        static GameObject jellyfishNovaEffect, jellyfishChargePrefab;
        GameObject jellyfishCharge;
        internal const int WispKind = 5;
        internal const int WispHealth = 35;
        internal const float WispHoverHeight = 4f;
        internal const float WispChargeDuration = 1.6f;
        internal const float WispFireDuration = 2f;
        const float WispBulletRange = 200f;
        const float WispWarningLength = 50f;
        const float WispEmberDamage = 3.5f * 1.5f;
        CharacterController flyingController;
        CharacterMotor flyingMotor;
        bool flyingMotorEnabled;
        float wispCooldown;
        float wispStrafeDirection = -1f;
        static GameObject wispDeathEffect;
        LineRenderer[] wispTraces;
        Vector3[] wispEmberEnds;
        float wispTracesUntil;
        internal const int LemurianKind = 4;
        internal const int LemurianHealth = 80;
        internal const float LemurianChargeDuration = .6f;
        internal const float LemurianFireDuration = .5f;
        internal const float LemurianSpawnDuration = 1.2f;
        const float LemurianBiteOpens = .7709457f / 1.3333334f;
        const float LemurianBiteCloses = .9029907f / 1.3333334f;
        const float LemurianBiteReach = 3f;
        static readonly Vector3 LemurianBiteOffset = new Vector3(0f, 3.09f, .58f);
        static readonly Vector3 LemurianBiteHalfSize = new Vector3(2.0060413f, 5.7609895f, 3.6646993f);
        Transform lemurianHead;
        NavMeshPath chasePath;
        Vector3[] chaseCorners;
        int chaseCorner, chaseCornerCount;
        float nextChasePath;
        RaycastHit[] fireballContacts;
        Vector3 fireballPosition, fireballDirection;
        float fireballExpires;
        internal const int BeetleGuardKind = 3;
        internal const int BeetleGuardHealth = 480;
        internal const float GuardSlamDuration = 2.7f;
        internal const float GuardSunderDuration = 2.05f;
        internal const float GuardSpawnDuration = 1.5f;
        const float GuardDeathDuration = 2.3333335f;
        const byte SpawnAnimation = 7;
        const byte DeathAnimation = 8;
        float spawnUntil, despawnAt;
        float DeathAnimationDuration => kind switch
        {
            BeetleGuardKind => GuardDeathDuration,
            LemurianKind => 1.1666667f,
            BruiserKind => 1.6666666f,
            QueenKind => 3.7500002f,
            GreaterWispKind => 2.0333335f,
            ImpKind => 1.3333334f,
            BisonKind => 1.4166667f,
            VerminKind => 2.0333335f,
            FlyingVerminKind => FlyingVerminDeathDuration,
            HermitCrabKind => 1.2f,
            AcidLarvaKind => 1.3333333f,
            LunarExploderKind => LunarExploderDeathDuration,
            GupKind or GeepKind or GipKind => .5f,
            ParentKind => 2.0333335f,
            _ => 0f
        };
        const byte GuardSlamAnimation = 5;
        const byte GuardSunderAnimation = 6;
        const float GuardSlamDiameter = 16.60836f;
        const float SunderSpeed = 40f;
        const float SunderLifetime = 1.5f;
        static readonly Vector3 SunderHitboxHalfSize = new Vector3(1.1861334f, 2.9797452f, 1.1861336f);
        static readonly Vector3 SunderHitboxOffset = new Vector3(0f, .6f, 0f);
        HashSet<Health> attackHits;
        Collider[] attackContacts;
        float nextGuardSlam, nextGuardSunder, guardRelease, sunderStarted;
        bool guardRanged;
        Vector3 sunderOrigin, sunderDirection, sunderPosition;
        Quaternion sunderRotation;
        public int Archetype=>kind;
        internal bool IsBoss => kind is 2 or QueenKind;
        public const string GoldTitanVariant = "TitanGoldMesh";
        public const string SandyGolemVariant = "SandyGolem";
        public string ModelVariant { get; private set; }
        public bool OwnsCharacter { get; set; }
        internal bool IsLabSummon { get; set; }
        internal bool HasVisibleAttackWarning => warning && warning.activeInHierarchy &&
            ((laser && laser.enabled) || warning.GetComponent<MeshRenderer>().enabled);
        internal CharacterInstance CurrentTarget => target;
        internal bool UsesGroundNavigation =>
            kind is BeetleGuardKind or LemurianKind or BruiserKind or QueenKind or ImpKind or BisonKind or
                VerminKind or HermitCrabKind or AcidLarvaKind or LunarExploderKind or ParentKind or MiniMushroomKind or ClayBruiserKind or ClayGrenadierKind or ChildKind || IsGupFamily;
        internal void SetTarget(CharacterInstance victim)
        {
            playerTarget = victim;
            bool hadLivingTarget = target && target.CurrentHealth > 0;
            if (hadLivingTarget && stage != 0) return;
            if (target == victim && hadLivingTarget) return;
            nextChasePath = 0f;
            target = victim;
            if (!hadLivingTarget) SetCombat(victim != null);
        }

        internal void ConfigureReplica(int archetype)
        {
            body = GetComponent<CharacterInstance>();
            kind = archetype;
            if (kind == QueenKind)
            {
                queenSpitVisuals = new GameObject[QueenSpitCount];
                body.RainQueenSpit.OnRemove += RemoveReplicaQueenSpit;
                body.RainQueenSpit.OnClear += HideQueenSpitVisuals;
                queenAcidPoolVisuals = new GameObject[QueenAcidPoolCapacity];
                body.RainQueenAcidPools.OnAdd += ShowReplicaQueenAcidPool;
                body.RainQueenAcidPools.OnRemove += RemoveReplicaQueenAcidPool;
                body.RainQueenAcidPools.OnClear += HideQueenAcidPools;
                foreach (var entry in body.RainQueenAcidPools)
                    ShowReplicaQueenAcidPool(entry.Key);
            }
            if (kind == BruiserKind)
            {
                bruiserFireballs = new BruiserFireball[BruiserFireballCapacity];
                bruiserFireballVisuals = new GameObject[BruiserFireballCapacity];
                body.RainBruiserFireballs.OnAdd += StartReplicaBruiserFireball;
                body.RainBruiserFireballs.OnRemove += RemoveReplicaBruiserFireball;
                foreach (var entry in body.RainBruiserFireballs)
                    StartReplicaBruiserFireball(entry.Key);
            }
            if (kind == GreaterWispKind) ConfigureReplicaGreaterWispShots();
            if (kind == FlyingVerminKind) ConfigureReplicaFlyingVerminSpit();
            if (kind == LunarExploderKind) ConfigureLunarShardVisuals();
            if (kind == HermitCrabKind) ConfigureHermitMortarVisuals();
            OwnsCharacter = true;
            CreateVisual(null);
            CreateTelegraph();
            if (kind == 2) CreateTitanFist();
        }
        public void RemoveFromRange()
        {
            SetCombat(false);
            if (queenGuards != null)
            {
                foreach (var guard in queenGuards)
                    if (guard) guard.RemoveFromRange();
                queenGuards.Clear();
            }
            if(OwnsCharacter && Mirror.NetworkServer.active)Mirror.NetworkServer.Destroy(gameObject);
            else Destroy(this);
        }
        public void SetCombat(bool enabled)
        {
            if (IsGupFamily && despawnAt > 0f) gupSplitCombat = enabled;
            if (kind == ParentKind) CancelParentAction();
            if (kind == AcidLarvaKind) RestoreAcidLarvaMotor();
            if (kind == BisonKind) RestoreBisonChargeSpeed();
            if (kind == ImpKind)
            {
                SetImpBlinking(false);
                impBlinkExitUntil = 0f;
            }
            if (kind == ClayBruiserKind) CancelClayBurst();
            if (IsPendingRanged) CancelPendingRanged(stopAudio: true);
            combatEnabled=enabled;stage=0;until=0;
            nextTitanBeam=false;
            attackHits?.Clear();
            if (!enabled && queenAcidShots != null)
            {
                System.Array.Clear(queenAcidShots, 0, queenAcidShots.Length);
                HideQueenSpitVisuals();
                if (body && body.isServer && body.RainQueenSpit.Count > 0)
                    body.RainQueenSpit.Clear();
                foreach (var pool in queenAcidPools)
                {
                    pool.Expires = 0f;
                    pool.Hits.Clear();
                }
                HideQueenAcidPools();
                if (body && body.isServer && body.RainQueenAcidPools.Count > 0)
                    body.RainQueenAcidPools.Clear();
            }
            if (!enabled && kind == BruiserKind && bruiserFireballs != null)
                for (int i = 0; i < bruiserFireballs.Length; i++)
                    if (bruiserFireballs[i].Expires > 0f) FinishBruiserFireball(i);
            if (!enabled && greaterWispShots != null)
                ClearGreaterWispShots();
            if (!enabled && flyingVerminShots != null)
                ClearFlyingVerminShots();
            if (!enabled && hermitMortars != null)
            {
                hermitMortars.Clear();
                if (body.RainHermitMortars.Count != 0) body.RainHermitMortars.Clear();
            }
            if (!enabled && lunarShards != null)
            {
                lunarShards.Clear();
                if (body.RainLunarExploderShards.Count != 0) body.RainLunarExploderShards.Clear();
            }
            if(body)body.SetMovementInput(Vector2.zero);
            if(warning)warning.SetActive(false);
            if (titanFist) titanFist.SetActive(false);
            HideWispTraces();
            PlayVisual(Time.time < spawnUntil ? SpawnAnimation : (byte)0);
        }
        internal void ApplyStun()
        {
            if (!body || !body.isServer || body.CurrentHealth <= 0 || kind == 2) return;
            stunnedUntil = Time.time + 2f;
            SetCombat(combatEnabled);
            PublishTelegraph();
        }
        public void Configure(CharacterInstance victim,int archetype,string modelVariant=null)
        {
            body=GetComponent<CharacterInstance>();target=playerTarget=victim;kind=archetype;originalMax=body.MaxHealth;
            ModelVariant=modelVariant;
            ai=GetComponent<BotAI>();if(ai){aiWasEnabled=ai.enabled;ai.enabled=false;ai.StopAllCoroutines();}
            if(body.CharacterItemManager){body.CharacterItemManager.UsePrimaryInput=false;body.CharacterItemManager.UseSecondaryInput=false;}
            if(kind==2)body.MaxHealth=body.CurrentHealth=1500;
            if (UsesGroundNavigation)
            {
                configuredMotor = GetComponent<CharacterMotor>();
                if (configuredMotor)
                {
                    previousWalkSpeed = configuredMotor.WalkSpeed;
                    previousRunSpeed = configuredMotor.RunSpeed;
                    configuredMotor.WalkSpeed = kind switch
                    {
                        ChildKind => 3.5f,
                        ClayGrenadierKind => 2.5f,
                        ClayBruiserKind => 2.5f,
                        MiniMushroomKind => 2.5f,
                        BeetleGuardKind => 17f,
                        BruiserKind => 13f,
                        QueenKind => 6f,
                        ImpKind => 10f,
                        BisonKind => 3f,
                        VerminKind => 13f,
                        HermitCrabKind => 10f,
                        AcidLarvaKind => AcidLarvaSprintSpeed,
                        LunarExploderKind => LunarWalkSpeed,
                        ParentKind => 11f,
                        GupKind => 12f,
                        GeepKind => 8f,
                        GipKind => 5f,
                        _ => 7f
                    };
                    configuredMotor.RunSpeed = configuredMotor.WalkSpeed * 1.45f;
                }
                attackContacts = new Collider[128];
                attackHits = new HashSet<Health>(128);
                chasePath = new NavMeshPath();
                chaseCorners = new Vector3[32];
            }
            if (kind == ParentKind) spawnUntil = Time.time + ParentSpawnDuration;
            if (kind == BeetleGuardKind) spawnUntil = Time.time + GuardSpawnDuration;
            if (kind == ImpKind) spawnUntil = Time.time + ImpSpawnDuration;
            if (kind == BisonKind) spawnUntil = Time.time + BisonSpawnDuration;
            if (IsGupFamily) spawnUntil = Time.time + 1f;
            if (kind == VerminKind) spawnUntil = Time.time + VerminSpawnDuration;
            if (kind == LunarExploderKind)
            {
                spawnUntil = Time.time + LunarExploderSpawnDuration;
                lunarShards = new List<LunarShard>(9);
                fireballContacts = new RaycastHit[32];
                ConfigureLunarShardVisuals();
            }
            if (kind == AcidLarvaKind)
            {
                spawnUntil = Time.time + AcidLarvaSpawnDuration;
                acidLarvaController = GetComponent<CharacterController>();
            }
            if (kind == HermitCrabKind)
            {
                spawnUntil = Time.time + HermitCrabSpawnDuration;
                hermitMortars = new List<HermitMortar>(2);
                hermitBlastDistances = new Dictionary<Health, float>(16);
                ConfigureHermitMortarVisuals();
                fireballContacts = new RaycastHit[32];
            }
            if (kind == QueenKind)
            {
                queenGuards = new List<RangeResearchEnemy>(2);
                queenAcidShots = new QueenAcidShot[QueenSpitCount];
                queenSpitVisuals = new GameObject[QueenSpitCount];
                queenAcidPools = new QueenAcidPool[QueenAcidPoolCapacity];
                queenAcidPoolVisuals = new GameObject[QueenAcidPoolCapacity];
                for (int i = 0; i < queenAcidPools.Length; i++)
                    queenAcidPools[i] = new QueenAcidPool();
                fireballContacts = new RaycastHit[32];
                spawnUntil = Time.time + QueenSpawnDuration;
            }
            if (kind == BruiserKind)
            {
                fireballContacts = new RaycastHit[32];
                bruiserFireballs = new BruiserFireball[BruiserFireballCapacity];
                bruiserFireballVisuals = new GameObject[BruiserFireballCapacity];
                spawnUntil = Time.time + BruiserSpawnDuration;
            }
            if (kind == LemurianKind)
            {
                fireballContacts = new RaycastHit[32];
                spawnUntil = Time.time + LemurianSpawnDuration;
            }
            if (IsAirborneKind(kind))
            {
                flyingController = GetComponent<CharacterController>();
                flyingMotor = GetComponent<CharacterMotor>();
                if (flyingMotor)
                {
                    flyingMotorEnabled = flyingMotor.enabled;
                    flyingMotor.enabled = false;
                }
                spawnUntil = Time.time + (kind switch
                {
                    GreaterWispKind => GreaterWispSpawnDuration,
                    FlyingVerminKind => FlyingVerminSpawnDuration,
                    WispKind => 1f,
                    _ => JellyfishSpawnDuration
                });
                if (kind == WispKind)
                {
                    fireballContacts = new RaycastHit[32];
                    wispEmberEnds = new Vector3[3];
                }
                else
                {
                    attackContacts = new Collider[128];
                    attackHits = new HashSet<Health>(128);
                }
                if (kind == GreaterWispKind)
                {
                    greaterWispShots = new GreaterWispShot[GreaterWispShotCapacity];
                    greaterWispGhosts = new GameObject[greaterWispShots.Length];
                    fireballContacts = new RaycastHit[32];
                }
                if (kind == FlyingVerminKind)
                {
                    flyingVerminShots = new FlyingVerminShot[FlyingVerminShotCapacity];
                    flyingVerminGhosts = new GameObject[FlyingVerminShotCapacity];
                    fireballContacts = new RaycastHit[32];
                }
            }
            CreateVisual(modelVariant);
            CreateTelegraph();
            if (kind == 2) CreateTitanFist();
            SetCombat(false);
        }
        void CreateTelegraph()
        {
            warning=GameObject.CreatePrimitive(kind is 0 or BeetleGuardKind or LemurianKind or ImpKind or BisonKind or VerminKind ? PrimitiveType.Cube : PrimitiveType.Cylinder);warning.name="Attack telegraph";
            var warningCollider = warning.GetComponent<Collider>();
            warningCollider.enabled = false;
            Destroy(warningCollider);
            material=new Material(warning.GetComponent<Renderer>().sharedMaterial);material.color=Color.red;warning.GetComponent<Renderer>().sharedMaterial=material;
            if (kind != 0)
            {
                laser = warning.AddComponent<LineRenderer>();
                laser.sharedMaterial = material;
                laser.useWorldSpace = true;
                laser.positionCount = 2;
                warning.GetComponent<MeshRenderer>().enabled = kind == 2 || kind == JellyfishKind;
            }
            if (kind == ClayBruiserKind)
            {
                Destroy(material); material = new Material(Shader.Find("Sprites/Default"));
                warning.GetComponent<MeshRenderer>().sharedMaterial = material; laser.sharedMaterial = material;
            }
            if (kind == LunarExploderKind) ConfigureLunarDeathWarning();
            warning.SetActive(false);
            if (kind == JellyfishKind)
            {
                if (!jellyfishChargePrefab)
                    jellyfishChargePrefab = Resources.Load<GameObject>(
                        "RainActors/RoR2/Base/Characters/Jellyfish/JellyfishChargeNova");
                if (jellyfishChargePrefab)
                {
                    jellyfishCharge = Instantiate(jellyfishChargePrefab, transform, false);
                    jellyfishCharge.transform.localScale *= JellyfishNovaRadius;
                    jellyfishCharge.SetActive(false);
                }
            }
            if (kind == WispKind)
            {
                wispTraces = new LineRenderer[3];
                for (int i = 0; i < wispTraces.Length; i++)
                {
                    var trace = new GameObject("Wisp ember trace").AddComponent<LineRenderer>();
                    trace.transform.SetParent(transform, false);
                    trace.sharedMaterial = material;
                    trace.useWorldSpace = true;
                    trace.positionCount = 2;
                    trace.widthMultiplier = .045f;
                    trace.startColor = trace.endColor = new Color(1f, .6f, .15f);
                    trace.enabled = false;
                    wispTraces[i] = trace;
                }
            }
        }
        void CreateVisual(string modelVariant)
        {
            visual=gameObject.AddComponent<RainActorVisual>();
            if (kind == ClayBruiserKind) { visual.Setup("mdlClayBruiser", "ClayBruiserArmature_", 2.8f); return; }
            if (kind == ChildKind) { visual.Setup("mdlChild", "Child_", 2.4f); return; }
            if (kind == ClayGrenadierKind) { visual.Setup("mdlClayGrenadier", "PendingGrenadier_", 2.5f); return; }
            if (IsPendingRanged) { visual.Setup(kind == BellKind ? "mdlBell" : "mdlMiniMushroom", kind == BellKind ? "PendingBell_" : "PendingMini_", kind == BellKind ? 1.6f : 1.8f); return; }
            if (IsGupFamily)
            {
                string gupModel = kind == GeepKind ? "mdlGeep" : kind == GipKind ? "mdlGip" : "mdlGup";
                float gupHeight = kind == GeepKind ? 2f : kind == GipKind ? 1.35f : 2.7f;
                visual.Setup(gupModel, "GupArmature|", gupHeight);
                return;
            }
            if (kind == ParentKind)
            {
                visual.Setup("mdlParent", "ParentArmature_", 4.5f);
                parentSlamZone = visual.FindModelBone("SlamZone");
                ConfigureParentShape();
                return;
            }
            if (kind == LunarExploderKind)
            {
                visual.Setup("mdlLunarExploder", "LunarExploderArmature_", 2.66f);
                lunarMuzzle = visual.FindModelBone("MuzzleCore");
                return;
            }
            if (kind == AcidLarvaKind)
            {
                visual.Setup("mdlAcidLarva", "AcidLarva_", 1.35f);
                return;
            }
            if (kind == HermitCrabKind)
            {
                visual.Setup("mdlHermitCrab", "HermitCrabArmature_", 2f);
                return;
            }
            if (kind == FlyingVerminKind)
            {
                visual.Setup("mdlFlyingVermin", "FlyingVerminArmature_", 1.35f);
                flyingVerminMouth = visual.FindModelBone("Body");
                return;
            }
            if (kind == VerminKind)
            {
                visual.Setup("mdlVermin", "VerminArmature_", 1.35f);
                verminModel = visual.FindModelBone("mdlVermin");
                verminHead = visual.FindModelBone("Head");
                if (body.isServer) verminController = GetComponent<CharacterController>();
                return;
            }
            if (kind == BisonKind)
            {
                visual.Setup("mdlBison", "BisonArmature_", 2.8f);
                bisonHead = visual.FindModelBone("head");
                bisonModel = visual.FindModelBone("mdlBison");
                bisonRoot = visual.FindModelBone("ROOT");
                bisonChargeFeet = new[]
                {
                    visual.FindModelBone("toe.f.l"), visual.FindModelBone("toe.f.r"),
                    visual.FindModelBone("toe.b.l"), visual.FindModelBone("toe.b.r")
                };
                return;
            }
            if (kind == ImpKind)
            {
                visual.Setup("mdlImp", "ImpArmature_", 2f);
                impModel = visual.FindModelBone("mdlImp");
                impRightHand = visual.FindModelBone("hand.r");
                impLeftHand = visual.FindModelBone("hand.l");
                return;
            }
            if (kind == GreaterWispKind)
            {
                visual.Setup("mdlGreaterWisp", "GreaterWispArmature_", 2.8f);
                greaterWispLeft = visual.FindModelBone("Hand.l");
                greaterWispRight = visual.FindModelBone("Hand.r");
                return;
            }
            if (kind == QueenKind)
            {
                visual.Setup("mdlBeetleQueen", "BeetleQueenArmature_", 5f);
                queenWardMuzzleParent = visual.FindModelBone("Hips");
                return;
            }
            if (kind == BruiserKind)
            {
                visual.Setup("mdlLemurianBruiser", "LemurianBruiserArmature_", 3.5f);
                bruiserHead = visual.FindModelBone("head");
                return;
            }
            if (kind == JellyfishKind)
            {
                visual.Setup("mdlJellyfish", "JellyfishArmature_", 1.6f);
                return;
            }
            if (kind == WispKind)
            {
                visual.Setup("mdlWisp1Mouth", "WispArmature_", 1f);
                return;
            }
            if (kind == LemurianKind)
            {
                visual.Setup("mdlLemurian", "LemurianArm_", 2f);
                if (body.isServer) lemurianHead = visual.FindModelBone("head");
                return;
            }
            if (kind == BeetleGuardKind)
            {
                visual.Setup("mdlBeetleGuard", "BeetleGuard", 3.4f);
                return;
            }
            string model = kind == 2 ? "mdlTitan" : kind == 0 ? "mdlBeetle" :
                modelVariant == SandyGolemVariant ? "mdlGolem" : modelVariant ?? "mdlGolem";
            visual.Setup(model,kind==0?"Beetle":kind==1?"Golem":"Titan",kind==2?5:kind==1?2.8f:1.7f,modelVariant);
        }
        void Update()
        {
            if(!body)return;
            if (kind == ParentKind && body.CurrentHealth <= 0)
            {
                CancelParentAction();
                SetParentHurtboxesEnabled(false);
            }
            if(!body.isServer)
            {
                UpdateReplica();
                return;
            }
            if (lunarShards != null)
            {
                AdvanceLunarShards(Time.time);
                UpdateLunarShardVisuals();
            }
            if (body.CurrentHealth <= 0)
            {
                if (kind == LunarExploderKind)
                {
                    TickLunarExploderDeath(Time.time);
                    return;
                }
                if (kind == FlyingVerminKind)
                {
                    TickFlyingVerminDeath();
                    return;
                }
                if (kind == WispKind)
                {
                    PlayWispDeath(transform.position, transform.rotation);
                    if (body.RainEnemyKind > 0) body.RpcRainWispDeath(transform.position, transform.rotation);
                }
                float deathDuration = DeathAnimationDuration;
                if (deathDuration <= 0f || (despawnAt > 0f && Time.time >= despawnAt))
                {
                    if (kind is GupKind or GeepKind) SplitGup();
                    RemoveFromRange();
                }
                else if (despawnAt == 0f)
                {
                    if (IsGupFamily)
                    {
                        gupSplitCombat = combatEnabled;
                        body.RpcRainGupDeath();
                    }
                    SetCombat(false);
                    despawnAt = Time.time + deathDuration;
                    PlayVisual(DeathAnimation);
                }
                return;
            }
            if (spawnUntil > 0f)
            {
                if (Time.time < spawnUntil) return;
                spawnUntil = 0f;
                PlayVisual(0);
            }
            // A launched mortar keeps its own flight clock while the crab is controlled.
            if (kind == HermitCrabKind)
            {
                AdvanceHermitMortars(Time.time);
                if (!body || body.CurrentHealth <= 0) return;
            }
            bool stopForTrap = Jotunnslayer.SkillBattlePreview.IsStoppedByTimeTrap(body) || Jotunnslayer.SkillBattlePreview.IsStoppedBySkill(body);
            if (stopForTrap && !timeTrapped)
            {
                SetCombat(combatEnabled);
                PublishTelegraph();
            }

            timeTrapped = stopForTrap;
            if (timeTrapped || IsStunned) return;
            if (Jotunnslayer.SkillBattlePreview.TryGetTerrorDirection(body, out var flee))
            {
                if (stage != 0)
                {
                    SetCombat(combatEnabled);
                    PublishTelegraph();
                }

                body.lookInput = new Vector2(0, Mathf.Atan2(flee.x, flee.z) * Mathf.Rad2Deg);
                bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                    Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
                body.SetMovementInput(rooted ? Vector2.zero : Vector2.up);
                return;
            }
            if (combatEnabled && !(kind == QueenKind && stage == 4) && (!target || target.CurrentHealth <= 0 ||
                (stage == 0 && Time.time >= nextDroneTargetScan)))
            {
                nextDroneTargetScan = Time.time + .4f;
                if (playerTarget && playerTarget.CurrentHealth > 0)
                {
                    if (!target || target.CurrentHealth <= 0) SetCombat(true);
                    target = RangeDrone.PreferCloserAlly(transform.position, playerTarget);
                }
            }
            if (kind == BruiserKind) AdvanceBruiserFireballs(Time.time);
            if (kind == GreaterWispKind) AdvanceGreaterWispShots(Time.time);
            if (kind == FlyingVerminKind) AdvanceFlyingVerminShots(Time.time);
            if (kind == QueenKind)
            {
                AdvanceQueenAcid(Time.time);
                TickQueenAcidPools(Time.time);
            }
            if(body.Block){if(kind==ParentKind)CancelParentAction();if(kind==ImpKind)SetImpBlinking(false);if(kind==BisonKind)RestoreBisonChargeSpeed();if(kind==AcidLarvaKind)RestoreAcidLarvaMotor();body.SetMovementInput(Vector2.zero);stage=0;if(warning)warning.SetActive(false);if(titanFist)titanFist.SetActive(false);PlayVisual(0);return;}
            if (kind == QueenKind && stage == 4)
            {
                TickQueenWardCast();
                return;
            }
            if(!combatEnabled||!target||target.CurrentHealth<=0){SetCombat(false);return;}
            if (kind == ClayBruiserKind) { TickClayBurst(); return; }
            if (IsPendingRanged) { TickPendingRanged(); return; }
            if (IsGupFamily)
            {
                TickGup();
                return;
            }
            if (kind == ParentKind)
            {
                TickParent();
                return;
            }
            if (kind == LunarExploderKind)
            {
                TickLunarExploder();
                return;
            }
            if (kind == AcidLarvaKind)
            {
                TickAcidLarva();
                return;
            }
            if (kind == HermitCrabKind)
            {
                TickHermitCrab();
                return;
            }
            if (kind == FlyingVerminKind)
            {
                TickFlyingVermin();
                return;
            }
            if (kind == VerminKind)
            {
                TickVermin();
                return;
            }
            if (kind == BisonKind)
            {
                TickBison();
                return;
            }
            if (kind == ImpKind)
            {
                TickImp();
                return;
            }
            if (kind == QueenKind)
            {
                TickQueen();
                return;
            }
            if (kind == BruiserKind)
            {
                TickBruiser();
                return;
            }
            if (kind == JellyfishKind)
            {
                TickJellyfish();
                return;
            }
            if (kind == WispKind)
            {
                TickWisp();
                return;
            }
            if (kind == GreaterWispKind)
            {
                TickGreaterWisp();
                return;
            }
            if (kind == LemurianKind)
            {
                TickLemurian();
                return;
            }
            if (kind == BeetleGuardKind)
            {
                TickBeetleGuard();
                return;
            }
            UpdateTitanFist();
            Vector3 delta=target.transform.position-transform.position;delta.y=0;
            if (kind != 0 || stage == 0)
                body.lookInput=new Vector2(0,Mathf.Atan2(delta.x,delta.z)*Mathf.Rad2Deg);
            if(stage==0)
            {
                float range=kind==0?2.8f:20;
                body.SetMovementInput(delta.magnitude > range && !Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) && !Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body) ? Vector2.up : Vector2.zero);
                PlayVisual((byte)(delta.magnitude>range?1:0));
                if(delta.magnitude>range)return;
                titanBeam = kind == 2 && nextTitanBeam;
                if (kind == 2) nextTitanBeam = !nextTitanBeam;
                lockedPoint=target.transform.position;stage=1;until=Time.time+(kind==0?.8f:kind==1?3:titanBeam?TitanBeamCharge:1.6f);
                warning.SetActive(true);
                PlayVisual(2);
                material.color = Color.red;
                warning.transform.position=lockedPoint+Vector3.up*.08f;
                warning.transform.localScale=new Vector3(kind==2?8:2,.08f,kind==2?8:2);
                if (kind == 0)
                {
                    headbuttForward = delta.sqrMagnitude > .001f ? delta.normalized : transform.forward;
                    headbuttHit = false;
                    headbuttOpensAt = Time.time + .8f;
                    until = headbuttOpensAt + .18f;
                    warning.transform.SetPositionAndRotation(
                        transform.position + headbuttForward * (HeadbuttReach * .5f) + Vector3.up * .08f,
                        Quaternion.LookRotation(headbuttForward));
                    warning.transform.localScale = new Vector3(HeadbuttHalfWidth * 2f, .08f, HeadbuttReach);
                }
                if (laser)
                {
                    laser.enabled = kind == 1 || titanBeam;
                    warning.GetComponent<MeshRenderer>().enabled = !laser.enabled;
                    if (laser.enabled)
                    {
                        lockedPoint = target.CombatAimPoint - Vector3.up;
                        UpdateLaser(false, out _);
                    }
                }
            }
            else
            {
                body.SetMovementInput(Vector2.zero);
                if (kind == 0 && stage == 1)
                {
                    TickHeadbutt();
                    return;
                }
                if (stage == 3)
                {
                    TickTitanBeam();
                    return;
                }
                if (stage == 2 && kind == 1 && until - Time.time < 1.8f)
                    warning.SetActive(false);
                if (stage == 1 && laser && laser.enabled && Time.time < until)
                {
                    if (until - Time.time > .5f) lockedPoint = target.CombatAimPoint - Vector3.up;
                    UpdateLaser(false, out _);
                }
                if(Time.time<until)return;
                if(stage==1)
                {
                    if (titanBeam)
                    {
                        stage = 3;
                        nextBeamTick = Time.time;
                        until = Time.time + TitanBeamDuration;
                        beamDirection = (lockedPoint + Vector3.up - LaserOrigin).normalized;
                        PlayVisual(3);
                        TickTitanBeam();
                        return;
                    }
                    bool hit=false;
                    if(kind==2)
                    {
                        hit=Vector3.Distance(target.transform.position,lockedPoint)<4;
                        PlayFistAt(lockedPoint);
                        if (body.RainEnemyKind == 3) body.RpcRainTitanFist(lockedPoint);
                    }
                    else UpdateLaser(true, out hit);
                    if(hit)target.Server_ChangeHealthStateRaw(kind==2?40:kind==1?25:12,CharacterPart.body,AttackType.melee,body,0);
                    if (kind != 1) warning.SetActive(false);
                    stage=2;until=Time.time+(kind==0?1.2f:2);
                }
                else stage=0;
            }
        }
        internal static RangeResearchEnemy SpawnGupFamily(CharacterInstance victim, int archetype, int team,
            Vector3 position, Quaternion rotation, bool labSummon)
        {
            var mode = GameManager.Gamemode;
            var child = Instantiate(mode.GetPlayerCharacterPrefab(), position, rotation).GetComponent<CharacterInstance>();
            foreach (var slot in child.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            mode.ConfigurePlayerCharacter(child);
            child.Team = team;
            child.RainEnemyKind = archetype + 1;
            child.MaxHealth = child.CurrentHealth = archetype == GupKind ? 1000 : archetype == GeepKind ? 500 : 250;
            Mirror.NetworkServer.Spawn(child.gameObject);
            child.SetAsBOT(true);
            var actor = child.gameObject.AddComponent<RangeResearchEnemy>();
            actor.OwnsCharacter = true;
            actor.IsLabSummon = labSummon;
            actor.Configure(victim, archetype);
            return actor;
        }

        void SplitGup()
        {
            if (!playerTarget || !Mirror.NetworkServer.active) return;
            const int mask = GameManager.environmentLayer | GameManager.characterLayer;
            Vector3 center = transform.position;
            Vector3 firstPosition = default;
            int spawned = 0;
            for (int attempt = 0; attempt < 8 && spawned < 2; attempt++)
            {
                Vector3 direction = Quaternion.Euler(0f, attempt * 135f, 0f) * transform.right;
                Vector3 candidate = center + direction * (attempt < 4 ? 1.65f : 2.5f);
                if (!Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out var floor, 5f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore) || floor.normal.y < .65f)
                    continue;
                Vector3 position = floor.point + Vector3.up * .15f;
                if ((spawned > 0 && (position - firstPosition).sqrMagnitude < 1.44f) ||
                    Physics.CheckCapsule(position + Vector3.up * .5f, position + Vector3.up * 1.5f, .45f,
                        mask, QueryTriggerInteraction.Ignore))
                    continue;
                var child = SpawnGupFamily(playerTarget, kind == GupKind ? GeepKind : GipKind, body.Team,
                    position, Quaternion.LookRotation(direction), IsLabSummon);
                child.SetCombat(gupSplitCombat && playerTarget.CurrentHealth > 0);
                firstPosition = position;
                spawned++;
            }
        }

        void TickGup()
        {
            float now = Time.time;
            if (stage != 0)
            {
                if (stage == 1 && now >= until - GupAttackDuration + GupSpikesRelease)
                {
                    stage = 2;
                    warning.SetActive(false);
                    body.RpcRainGupSpikes();
                    attackHits.Clear();
                    int count = Physics.OverlapSphereNonAlloc(transform.position + Vector3.up, GupSpikesRadius,
                        attackContacts, GameManager.characterLayer, QueryTriggerInteraction.Collide);
                    CollectAttackHits(count, kind == GeepKind ? 18 : kind == GipKind ? 9 : 36, AttackType.melee);
                }
                if (now < until) return;
                stage = 0;
            }

            Vector3 offset = target.transform.position - transform.position;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
            float attackRange = GupSpikesRadius - 1.25f;
            if (visible && offset.sqrMagnitude <= attackRange * attackRange && now >= nextGupAttack)
            {
                body.SetMovementInput(Vector2.zero);
                stage = 1;
                until = now + GupAttackDuration;
                nextGupAttack = until + 2.5f;
                warning.transform.position = transform.position + Vector3.up * .05f;
                warning.transform.localScale = GupWarningScale;
                warning.SetActive(true);
                PlayVisual(2);
                return;
            }
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
            bool moving = !rooted && offset.sqrMagnitude > attackRange * attackRange && SteerChase(visible, visible, now);
            body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
            PlayVisual(moving ? (byte)1 : (byte)0);
        }

        void TickQueen()
        {
            float now = Time.time;
            Vector3 origin = transform.position + Vector3.up * 3f;
            Vector3 aim = target.CombatAimPoint - origin;
            if (aim.x * aim.x + aim.z * aim.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg);
            if (stage == 0)
            {
                bool visible = !Physics.Linecast(origin, origin + aim, GameManager.environmentLayer,
                    QueryTriggerInteraction.Ignore);
                bool summon = aim.sqrMagnitude >= 100f && aim.sqrMagnitude <= 16900f && now >= queenNextSummon;
                if (summon)
                {
                    body.SetMovementInput(Vector2.zero);
                    stage = 3;
                    until = now + 9f;
                    queenNextSummon = now + 28f;
                    queenNextEgg = now + 2.447087f;
                    queenEggsAttempted = 0;
                    PlayVisual(22);
                    return;
                }
                if (aim.sqrMagnitude <= 6400f && visible && now >= queenNextWardCast &&
                    body.CurrentHealth <= body.MaxHealth * .75f)
                {
                    body.SetMovementInput(Vector2.zero);
                    stage = 4;
                    body.RainQueenWardCastSpeed = body.ResearchAttackSpeed;
                    float duration = QueenWardCastDuration / body.RainQueenWardCastSpeed;
                    until = now + duration;
                    queenWardRelease = now + duration * (1.0383338f / 4.366667f);
                    queenNextWardCast = now + 20f;
                    PlayVisual(23);
                    return;
                }
                if (aim.sqrMagnitude > 6400f || !visible || now < queenNextSpit)
                {
                    bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                        Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
                    bool moving = aim.sqrMagnitude >= 400f && !rooted && SteerChase(visible, false, now);
                    body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
                    PlayVisual(moving ? (byte)1 : (byte)0);
                    return;
                }
                body.SetMovementInput(Vector2.zero);
                nextChasePath = 0f;
                stage = 1;
                until = now + QueenSpitDuration;
                queenNextSpit = now + 10f;
                warning.SetActive(true);
                laser.enabled = true;
                laser.widthMultiplier = .045f;
                material.color = Color.red;
                PlayVisual(20);
            }

            if (stage == 1)
            {
                laser.SetPosition(0, origin);
                laser.SetPosition(1, origin + aim);
                if (now < until) return;
                Vector3 horizontal = new Vector3(aim.x, 0f, aim.z);
                float flightTime = Mathf.Max(.02f, horizontal.magnitude / 50f);
                Vector3 velocity = horizontal / flightTime +
                    Vector3.up * (aim.y / flightTime - .5f * Physics.gravity.y * flightTime);
                if (velocity.sqrMagnitude < .001f) velocity = transform.forward * 50f;
                for (int i = 0; i < queenAcidShots.Length; i++)
                {
                    Vector3 direction = Quaternion.AngleAxis((3 - i) * 6f, Vector3.up) *
                        ApplyEmberSpread(velocity.normalized, 1f, 1f);
                    Vector3 initialVelocity = direction * velocity.magnitude;
                    queenAcidShots[i] = new QueenAcidShot
                    {
                        Position = origin,
                        Velocity = initialVelocity,
                        Expires = now + QueenSpitLifetime
                    };
                    UpdateQueenSpitVisual(i, origin, initialVelocity);
                    if (body.RainEnemyKind > 0)
                        body.RainQueenSpit.Add(i, new CharacterInstance.QueenSpitState
                        {
                            Origin = origin,
                            Velocity = initialVelocity,
                            StartedAt = Mirror.NetworkTime.time
                        });
                }
                warning.SetActive(false);
                stage = 2;
                until = now + QueenSpitDuration;
                PlayVisual(21);
            }
            if (stage == 3 && now >= queenNextEgg && queenEggsAttempted < 2 && now < until)
            {
                queenEggsAttempted++;
                queenNextEgg += 1f;
                SummonQueenGuard(target);
            }
            if ((stage == 2 || stage == 3) && now >= until) stage = 0;
        }

        void TickQueenWardCast()
        {
            float now = Time.time;
            if (now >= queenWardRelease)
            {
                queenWardRelease = float.PositiveInfinity;
                Vector3 muzzle = queenWardMuzzleParent
                    ? queenWardMuzzleParent.TransformPoint(0f, 6.59f, 0f)
                    : transform.position + Vector3.up * 3f;
                RainQueenWardCast.Launch(body, muzzle);
            }
            if (now < until) return;
            stage = 0;
            PlayVisual(0);
        }

        void SummonQueenGuard(CharacterInstance victim)
        {
            const float radius = .7f;
            var mode = GameManager.Gamemode;
            Vector3 center = victim.transform.position;
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float angle = Random.Range(0f, 360f);
                Vector3 direction = Quaternion.Euler(0f, angle, 0f) * Vector3.forward;
                Vector3 candidate = center + direction * Random.Range(3f, 20f);
                if (!NavMesh.SamplePosition(candidate, out var spawn, 2f, NavMesh.AllAreas)) continue;
                float distance = (spawn.position - center).sqrMagnitude;
                if (distance < 9f || distance > 400f ||
                    Physics.CheckCapsule(spawn.position + Vector3.up * (radius + .1f),
                        spawn.position + Vector3.up * (2f - radius), radius, GameManager.characterLayer,
                        QueryTriggerInteraction.Ignore)) continue;

                var guard = Instantiate(mode.GetPlayerCharacterPrefab(), spawn.position + Vector3.up * .1f,
                    Quaternion.LookRotation(-direction)).GetComponent<CharacterInstance>();
                foreach (var slot in guard.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                mode.ConfigurePlayerCharacter(guard);
                guard.Team = body.Team;
                guard.RainEnemyKind = BeetleGuardKind + 1;
                guard.MaxHealth = guard.CurrentHealth = BeetleGuardHealth;
                Mirror.NetworkServer.Spawn(guard.gameObject);
                guard.SetAsBOT(true);
                var actor = guard.gameObject.AddComponent<RangeResearchEnemy>();
                actor.OwnsCharacter = true;
                actor.IsLabSummon = IsLabSummon;
                actor.Configure(victim, BeetleGuardKind);
                actor.SetCombat(true);
                queenGuards.RemoveAll(item => !item);
                queenGuards.Add(actor);
                return;
            }
        }

        void AdvanceQueenAcid(float now)
        {
            float step = Time.deltaTime;
            for (int i = 0; i < queenAcidShots.Length; i++)
            {
                ref QueenAcidShot shot = ref queenAcidShots[i];
                if (shot.Expires <= 0f) continue;
                Vector3 travel = shot.Velocity * step + Physics.gravity * (.5f * step * step);
                float length = travel.magnitude;
                Vector3 direction = length > .0001f ? travel / length : Vector3.down;
                int hits = Physics.SphereCastNonAlloc(shot.Position, .5f, direction, fireballContacts, length,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                float distance = length;
                for (int j = 0; j < hits; j++)
                {
                    var contact = fireballContacts[j];
                    if (contact.collider && contact.distance < distance &&
                        !contact.collider.GetComponentInParent<Health>())
                        distance = contact.distance;
                }
                shot.Position += direction * distance;
                shot.Velocity += Physics.gravity * step;
                if (distance >= length && now < shot.Expires)
                {
                    UpdateQueenSpitVisual(i, shot.Position, shot.Velocity);
                    continue;
                }

                attackHits.Clear();
                int count = Physics.OverlapSphereNonAlloc(shot.Position, QueenAcidRadius, attackContacts,
                    GameManager.characterLayer, QueryTriggerInteraction.Collide);
                for (int j = 0; j < count; j++)
                {
                    var collider = attackContacts[j];
                    attackContacts[j] = null;
                    if (!collider) continue;
                    var victim = collider.GetComponentInParent<Health>();
                    if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0) continue;
                    Vector3 point = victim is CharacterInstance character ? character.CombatAimPoint : victim.transform.position;
                    if (Physics.Linecast(shot.Position, point, GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                        !attackHits.Add(victim)) continue;
                    int damage = Mathf.RoundToInt(25f * 1.3f *
                        (1f - Mathf.Clamp01(Vector3.Distance(shot.Position, point) / QueenAcidRadius)));
                    if (damage > 0)
                        victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
                }
                SpawnQueenAcidPool(shot.Position, now);
                shot = default;
                HideQueenSpitVisual(i);
                if (body.RainEnemyKind > 0) body.RainQueenSpit.Remove(i);
            }
        }

        void UpdateQueenSpitVisual(int index, Vector3 position, Vector3 velocity)
        {
            if (!queenSpitPrefab)
                queenSpitPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleQueen/BeetleQueenSpitGhost");
            if (!queenSpitPrefab) return;
            Quaternion rotation = velocity.sqrMagnitude > .0001f ? Quaternion.LookRotation(velocity) : Quaternion.identity;
            var effect = queenSpitVisuals[index];
            if (!effect) queenSpitVisuals[index] = Instantiate(queenSpitPrefab, position, rotation);
            else
            {
                effect.transform.SetPositionAndRotation(position, rotation);
                if (!effect.activeSelf) effect.SetActive(true);
            }
        }

        void UpdateReplicaQueenSpit()
        {
            foreach (var entry in body.RainQueenSpit)
            {
                if ((uint)entry.Key >= (uint)queenSpitVisuals.Length) continue;
                var shot = entry.Value;
                float elapsed = Mathf.Max(0f, (float)(Mirror.NetworkTime.time - shot.StartedAt));
                if (elapsed >= QueenSpitLifetime)
                {
                    HideQueenSpitVisual(entry.Key);
                    continue;
                }
                Vector3 position = shot.Origin + shot.Velocity * elapsed + Physics.gravity * (.5f * elapsed * elapsed);
                UpdateQueenSpitVisual(entry.Key, position, shot.Velocity + Physics.gravity * elapsed);
            }
        }

        void RemoveReplicaQueenSpit(int index, CharacterInstance.QueenSpitState _) => HideQueenSpitVisual(index);

        void HideQueenSpitVisual(int index)
        {
            if ((uint)index < (uint)queenSpitVisuals.Length && queenSpitVisuals[index])
                queenSpitVisuals[index].SetActive(false);
        }

        void HideQueenSpitVisuals()
        {
            for (int i = 0; i < queenSpitVisuals.Length; i++) HideQueenSpitVisual(i);
        }

        void SpawnQueenAcidPool(Vector3 position, float now)
        {
            Vector3 normal = Vector3.up;
            if (Physics.Raycast(position + Vector3.up * 3f, Vector3.down, out var ground, 7f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                position = ground.point;
                normal = ground.normal;
            }

            for (int i = 0; i < queenAcidPools.Length; i++)
            {
                var pool = queenAcidPools[i];
                if (pool.Expires > now) continue;
                if (pool.Expires > 0f) RemoveQueenAcidPool(i);
                pool.Position = position + normal * .07f;
                pool.Rotation = Quaternion.FromToRotation(Vector3.up, normal);
                pool.Expires = now + 15f;
                pool.NextScan = now;
                pool.NextReset = now + .5f;
                pool.Hits.Clear();
                ShowQueenAcidPool(i, position, pool.Rotation);
                if (body.RainEnemyKind > 0)
                    body.RainQueenAcidPools.Add(i, new CharacterInstance.QueenAcidPoolState
                    {
                        Position = position,
                        Rotation = pool.Rotation
                    });
                return;
            }
        }

        void TickQueenAcidPools(float now)
        {
            for (int i = 0; i < queenAcidPools.Length; i++)
            {
                var pool = queenAcidPools[i];
                if (pool.Expires <= now)
                {
                    if (pool.Expires > 0f) RemoveQueenAcidPool(i);
                    continue;
                }
                if (now < pool.NextScan) continue;
                pool.NextScan = now + .1f;
                if (now >= pool.NextReset)
                {
                    pool.Hits.Clear();
                    pool.NextReset = now + .5f;
                }

                int count = Physics.OverlapBoxNonAlloc(pool.Position, QueenPoolHalfSize, attackContacts,
                    pool.Rotation, GameManager.characterLayer, QueryTriggerInteraction.Collide);
                for (int j = 0; j < count; j++)
                {
                    var collider = attackContacts[j];
                    attackContacts[j] = null;
                    if (!collider) continue;
                    var victim = collider.GetComponentInParent<Health>();
                    if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                        !pool.Hits.Add(victim)) continue;
                    int damage = Mathf.RoundToInt(25f * 1.3f * .2f);
                    victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0, .1f);
                }
            }
        }

        void RemoveQueenAcidPool(int index)
        {
            var pool = queenAcidPools[index];
            pool.Expires = 0f;
            pool.Hits.Clear();
            if (queenAcidPoolVisuals[index]) queenAcidPoolVisuals[index].SetActive(false);
            if (body.RainEnemyKind > 0) body.RainQueenAcidPools.Remove(index);
        }

        void ShowQueenAcidPool(int index, Vector3 position, Quaternion rotation)
        {
            if (!queenAcidPoolPrefab)
                queenAcidPoolPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleQueen/BeetleQueenAcidGhost");
            if (!queenAcidPoolPrefab) return;
            var effect = queenAcidPoolVisuals[index];
            if (!effect) queenAcidPoolVisuals[index] = Instantiate(queenAcidPoolPrefab, position, rotation);
            else
            {
                effect.transform.SetPositionAndRotation(position, rotation);
                effect.SetActive(true);
            }
        }

        void ShowReplicaQueenAcidPool(int index)
        {
            if ((uint)index < (uint)queenAcidPoolVisuals.Length &&
                body.RainQueenAcidPools.TryGetValue(index, out var state))
                ShowQueenAcidPool(index, state.Position, state.Rotation);
        }

        void RemoveReplicaQueenAcidPool(int index, CharacterInstance.QueenAcidPoolState _)
        {
            if ((uint)index < (uint)queenAcidPoolVisuals.Length && queenAcidPoolVisuals[index])
                queenAcidPoolVisuals[index].SetActive(false);
        }

        void HideQueenAcidPools()
        {
            foreach (var effect in queenAcidPoolVisuals)
                if (effect) effect.SetActive(false);
        }

        void TickBruiser()
        {
            float now = Time.time;
            Vector3 origin = body.CombatAimPoint;
            Vector3 aim = target.CombatAimPoint - origin;
            if (aim.x * aim.x + aim.z * aim.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg);

            if (stage == 0)
            {
                bool visible = !Physics.Linecast(origin, origin + aim, GameManager.environmentLayer,
                    QueryTriggerInteraction.Ignore);
                bool breath = aim.sqrMagnitude <= 225f && visible && now >= bruiserNextBreath;
                bool fireball = aim.sqrMagnitude <= 6400f && visible && now >= bruiserNextFire;
                if (!breath && !fireball)
                {
                    bool moving = !BruiserRooted() && SteerChase(visible, false, now);
                    body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
                    PlayVisual(moving ? (byte)1 : (byte)0);
                    return;
                }

                if (!breath)
                {
                    nextChasePath = 0f;
                    stage = 4;
                    until = now + BruiserFireCharge;
                    bruiserNextFire = now + 4f;
                    warning.SetActive(true);
                    laser.enabled = true;
                    laser.widthMultiplier = .045f;
                    material.color = Color.red;
                    PlayVisual(18);
                }
                else
                {
                body.SetMovementInput(Vector2.zero);
                nextChasePath = 0f;
                stage = 1;
                bruiserNextBreath = now + 1f;
                until = now + BruiserBreathEntry;
                warning.SetActive(true);
                laser.enabled = true;
                laser.widthMultiplier = .045f;
                material.color = Color.red;
                    PlayVisual(15);
                }
            }

            Vector3 direction = aim.sqrMagnitude > .0001f ? aim.normalized : transform.forward;
            if (stage == 4 || stage == 5)
            {
                TickBruiserFire(now, origin, direction, aim.sqrMagnitude);
                return;
            }
            if (stage == 1 && now >= until)
            {
                stage = 2;
                bruiserNextTick = until + .125f;
                until += BruiserBreathDuration;
                laser.widthMultiplier = .5f;
                material.color = new Color(1f, .6f, .15f);
                PlayVisual(16);
            }
            if (stage == 1 || stage == 2)
            {
                if (bruiserHead)
                {
                    origin = bruiserHead.TransformPoint(BruiserMouthOffset);
                    Vector3 mouthAim = target.CombatAimPoint - origin;
                    if (mouthAim.sqrMagnitude > .0001f) direction = mouthAim.normalized;
                }
                laser.SetPosition(0, origin);
                laser.SetPosition(1, origin + direction * BruiserBreathRange);
            }
            if (stage == 2)
            {
                if (now >= bruiserNextTick && bruiserNextTick < until)
                {
                    bruiserNextTick += .125f;
                    Vector3 flameDirection = ApplyEmberSpread(direction, 3f);
                    // The original smart collision uses a thin ray for world geometry
                    // and the wider sweep for character hurtboxes.
                    float reach = Physics.Raycast(origin, flameDirection, out var wall, BruiserBreathRange,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ? wall.distance : BruiserBreathRange;
                    int count = Physics.SphereCastNonAlloc(origin, 3f, flameDirection, fireballContacts,
                        reach, GameManager.characterLayer & ~GameManager.environmentLayer, QueryTriggerInteraction.Collide);
                    attackHits.Clear();
                    for (int i = 0; i < count; i++)
                    {
                        var victim = fireballContacts[i].collider.GetComponentInParent<Health>();
                        if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                            !attackHits.Add(victim)) continue;
                        const float baseDamage = 16f;
                        const float tickDamage = baseDamage * 5f / (BruiserBreathDuration * 8f);
                        int healthBefore = victim.CurrentHealth;
                        victim.Server_ChangeHealthStateRaw(Mathf.RoundToInt(tickDamage),
                            CharacterPart.body, AttackType.hitscan, body, 0, .4f);
                        if (victim.CurrentHealth > 0 && victim.CurrentHealth < healthBefore)
                        {
                            const float burnDamage = tickDamage * .5f;
                            int burnTicks = Mathf.CeilToInt(burnDamage / (baseDamage * .1f));
                            if (!victim.TryGetComponent<RangeBleeding>(out var dot))
                                dot = victim.gameObject.AddComponent<RangeBleeding>();
                            dot.AddBurn(body, Mathf.RoundToInt(burnDamage), burnTicks);
                        }
                    }
                }
                if (now < until) return;
                stage = 3;
                until = now + 1f;
                warning.SetActive(false);
                PlayVisual(17);
            }
            if (stage == 3 && now >= until) stage = 0;
        }

        void UpdateBruiserBreathEffect()
        {
            bool active = body && body.CurrentHealth > 0 && warning && warning.activeSelf &&
                (body.isServer ? stage == 2 : body.RainEnemyAnimation == 16 && body.RainTelegraph == 3);
            if (!active)
            {
                if (bruiserBreathEffect && bruiserBreathEffect.activeSelf) bruiserBreathEffect.SetActive(false);
                return;
            }

            if (!bruiserBreathEffect)
            {
                if (!bruiserBreathPrefab)
                    bruiserBreathPrefab = Resources.Load<GameObject>(
                        "RainActors/RoR2/Base/Characters/LemurianBruiser/FlamebreathEffect");
                if (!bruiserBreathPrefab) return;
                bruiserBreathEffect = Instantiate(bruiserBreathPrefab, transform, false);
            }

            Vector3 origin = bruiserHead ? bruiserHead.TransformPoint(BruiserMouthOffset) : laser.GetPosition(0);
            Vector3 direction = laser.GetPosition(1) - laser.GetPosition(0);
            bruiserBreathEffect.transform.SetPositionAndRotation(origin,
                direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : transform.rotation);
            if (!bruiserBreathEffect.activeSelf) bruiserBreathEffect.SetActive(true);
        }

        bool BruiserRooted() => Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
            Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);

        void TickBruiserFire(float now, Vector3 origin, Vector3 direction, float distanceSquared)
        {
            body.SetMovementInput(!BruiserRooted() && distanceSquared <= 2500f ? Vector2.up : Vector2.zero);
            if (stage == 4)
            {
                laser.SetPosition(0, origin);
                laser.SetPosition(1, origin + direction * 80f);
                if (now < until) return;
                stage = 5;
                bruiserFireStarted = until;
                until += BruiserFireDuration;
                bruiserShots = 0;
                warning.SetActive(false);
                PlayVisual(19);
            }

            while (bruiserShots < 5 && now >= bruiserFireStarted + bruiserShots * BruiserFireInterval)
            {
                Vector3 shot = Quaternion.AngleAxis((bruiserShots - 2) * 11.25f, Vector3.up) * direction;
                bruiserShots++;
                for (int i = 0; i < bruiserFireballs.Length; i++)
                {
                    if (bruiserFireballs[i].Expires > 0f) continue;
                    bruiserFireballs[i] = new BruiserFireball
                    {
                        Position = origin,
                        Direction = shot,
                        Expires = now + BruiserFireLifetime
                    };
                    ShowBruiserFireball(i, origin, shot);
                    if (body.RainEnemyKind > 0)
                        body.RainBruiserFireballs.Add(i, new CharacterInstance.RainLinearShotState
                        {
                            Origin = origin,
                            Direction = shot,
                            StartedAt = Mirror.NetworkTime.time
                        });
                    break;
                }
            }
            if (now >= until) stage = 0;
        }

        void AdvanceBruiserFireballs(float now)
        {
            for (int i = 0; i < bruiserFireballs.Length; i++)
            {
                ref BruiserFireball shot = ref bruiserFireballs[i];
                if (shot.Expires <= 0f) continue;
                float travel = BruiserFireSpeed * Time.deltaTime;
                var victim = TraceFirstHostile(shot.Position, shot.Direction, 1f, travel, out float hitDistance);
                shot.Position += shot.Direction * hitDistance;
                if (bruiserFireballVisuals[i]) bruiserFireballVisuals[i].transform.position = shot.Position;
                if (!victim && hitDistance >= travel && now < shot.Expires) continue;

                attackHits.Clear();
                int count = Physics.OverlapSphereNonAlloc(shot.Position, BruiserBlastRadius, attackContacts,
                    GameManager.characterLayer, QueryTriggerInteraction.Collide);
                for (int j = 0; j < count; j++)
                {
                    var collider = attackContacts[j];
                    attackContacts[j] = null;
                    if (!collider) continue;
                    var hit = collider.GetComponentInParent<Health>();
                    if (!hit || hit == body || hit.Team == body.Team || hit.CurrentHealth <= 0) continue;
                    Vector3 point = hit is CharacterInstance character ? character.CombatAimPoint : hit.transform.position;
                    float distanceSquared = (point - shot.Position).sqrMagnitude;
                    if (distanceSquared > BruiserBlastRadius * BruiserBlastRadius ||
                        Physics.Linecast(shot.Position, point, GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                        !attackHits.Add(hit)) continue;
                    int damage = distanceSquared <= 9f ? 32 : 8;
                    hit.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
                }
                FinishBruiserFireball(i, exploded: true);
            }
        }

        void FinishBruiserFireball(int index, bool exploded = false)
        {
            Vector3 point = bruiserFireballs[index].Position;
            HideBruiserFireball(index);
            if (body && body.isServer && body.RainEnemyKind > 0)
            {
                body.RainBruiserFireballs.Remove(index);
                if (exploded) body.RpcRainBruiserFireballImpact(point);
            }
            if (exploded) PlayBruiserFireballImpact(point);
        }

        internal static void PlayBruiserFireballImpact(Vector3 point)
        {
            if (!bruiserFireballImpactPrefab)
                bruiserFireballImpactPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/LemurianBruiser/OmniExplosionVFXLemurianBruiserFireballImpact");
            if (!bruiserFireballImpactPrefab) return;
            var effect = Instantiate(bruiserFireballImpactPrefab, point, Quaternion.identity);
            effect.transform.localScale *= BruiserBlastRadius;
            Destroy(effect, 3f);
        }

        void StartReplicaBruiserFireball(int index)
        {
            if ((uint)index >= (uint)bruiserFireballs.Length ||
                !body.RainBruiserFireballs.TryGetValue(index, out var state)) return;
            float elapsed = Mathf.Max(0f, (float)(Mirror.NetworkTime.time - state.StartedAt));
            if (elapsed >= BruiserFireLifetime) return;
            bruiserFireballs[index] = new BruiserFireball
            {
                Position = state.Origin + state.Direction * (BruiserFireSpeed * elapsed),
                Direction = state.Direction,
                Expires = Time.time + BruiserFireLifetime - elapsed
            };
            ShowBruiserFireball(index, bruiserFireballs[index].Position, state.Direction);
        }

        void RemoveReplicaBruiserFireball(int index, CharacterInstance.RainLinearShotState _) =>
            HideBruiserFireball(index);

        void ShowBruiserFireball(int index, Vector3 position, Vector3 direction)
        {
            if (!bruiserFireballPrefab)
                bruiserFireballPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/LemurianBruiser/MegaFireballGhost");
            if (!bruiserFireballPrefab) return;
            var ghost = bruiserFireballVisuals[index];
            if (!ghost)
                bruiserFireballVisuals[index] = Instantiate(bruiserFireballPrefab, position, Quaternion.LookRotation(direction));
            else
            {
                ghost.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
                ghost.SetActive(true);
            }
        }

        void HideBruiserFireball(int index)
        {
            if (bruiserFireballs == null || (uint)index >= (uint)bruiserFireballs.Length) return;
            bruiserFireballs[index] = default;
            if (bruiserFireballVisuals[index]) bruiserFireballVisuals[index].SetActive(false);
        }

        void AdvanceReplicaBruiserFireballs()
        {
            for (int i = 0; i < bruiserFireballs.Length; i++)
            {
                ref BruiserFireball shot = ref bruiserFireballs[i];
                if (shot.Expires <= 0f) continue;
                if (Time.time >= shot.Expires)
                {
                    HideBruiserFireball(i);
                    continue;
                }
                shot.Position += shot.Direction * (BruiserFireSpeed * Time.deltaTime);
                if (bruiserFireballVisuals[i]) bruiserFireballVisuals[i].transform.position = shot.Position;
            }
        }

        void TickJellyfish()
        {
            Vector3 offset = target.CombatAimPoint - transform.position;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
            {
                float yaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                body.lookInput = new Vector2(0f, yaw);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            if (stage == 0 && offset.sqrMagnitude <= 64f)
            {
                stage = 1;
                until = Time.time + 1f;
                laser.enabled = false;
                warning.SetActive(true);
                warning.transform.localScale = new Vector3(JellyfishNovaRadius * 2f, .08f, JellyfishNovaRadius * 2f);
                PlayVisual(14);
            }

            if (flyingController && flyingController.enabled &&
                !Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) &&
                !Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body))
                flyingController.Move(Vector3.ClampMagnitude(offset, 1f) * (10f * Time.deltaTime));

            if (stage == 0)
            {
                PlayVisual(1);
                return;
            }

            Vector3 center = transform.position;
            warning.transform.position = center;
            if (Time.time < until) return;

            warning.SetActive(false);
            PlayJellyfishNova(center);
            if (body.RainEnemyKind > 0) body.RpcRainJellyfishNova(center);
            int count = Physics.OverlapSphereNonAlloc(center, JellyfishNovaRadius, attackContacts,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var collider = attackContacts[i];
                attackContacts[i] = null;
                if (!collider) continue;
                var victim = collider.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0) continue;
                Vector3 aim = victim is CharacterInstance character ? character.CombatAimPoint : victim.transform.position;
                if (Physics.Linecast(center, aim, GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                    !attackHits.Add(victim)) continue;
                int damage = Mathf.RoundToInt(60f * (1f - Mathf.Clamp01(Vector3.Distance(center, aim) / JellyfishNovaRadius)));
                if (damage > 0)
                    victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0, 2f);
            }

            body.Server_ChangeHealthStateRaw(body.CurrentHealth, CharacterPart.body, AttackType.explosion, body, 0, 0f);
            RemoveFromRange();
        }

        internal static void PlayWispDeath(Vector3 center, Quaternion rotation)
        {
            if (!wispDeathEffect)
                wispDeathEffect = Resources.Load<GameObject>("RainActors/RoR2/Base/Characters/Wisp/WispDeath");
            if (wispDeathEffect) Destroy(Instantiate(wispDeathEffect, center, rotation), 4f);
        }

        internal static void PlayJellyfishNova(Vector3 center)
        {
            if (!jellyfishNovaEffect)
                jellyfishNovaEffect = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/Jellyfish/JellyfishNova");
            if (!jellyfishNovaEffect) return;
            var effect = Instantiate(jellyfishNovaEffect, center, Quaternion.identity);
            effect.transform.localScale *= JellyfishNovaRadius;
            Destroy(effect, 3f);
        }

        void TickWisp()
        {
            float now = Time.time;
            Vector3 origin = body.CombatAimPoint;
            Vector3 aim = target.CombatAimPoint - origin;
            float distance = aim.magnitude;
            if (aim.x * aim.x + aim.z * aim.z > .001f)
            {
                float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                body.lookInput = new Vector2(0f, yaw);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            bool visible = !Physics.Linecast(origin, origin + aim, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore);
            MoveWisp(distance, visible);
            if (stage == 0)
            {
                if (now >= wispCooldown && distance <= 30f && visible && distance > .01f)
                {
                    stage = 1;
                    until = now + WispChargeDuration;
                    wispCooldown = now + 4f;
                    warning.SetActive(true);
                    laser.enabled = true;
                    material.color = Color.red;
                    laser.widthMultiplier = .045f;
                    PlayVisual(12);
                }
                else
                {
                    PlayVisual(0);
                    return;
                }
            }
            if (stage == 1)
            {
                origin = body.CombatAimPoint;
                aim = target.CombatAimPoint - origin;
                Vector3 direction = aim.sqrMagnitude > .0001f ? aim.normalized : transform.forward;
                laser.SetPosition(0, origin);
                laser.SetPosition(1, origin + direction * WispWarningLength);
                if (now < until) return;
                for (int shot = 0; shot < 3; shot++)
                    wispEmberEnds[shot] = FireWispEmber(origin, ApplyEmberSpread(direction, 4f));
                stage = 2;
                until = now + WispFireDuration;
                material.color = new Color(1f, .6f, .15f);
                laser.widthMultiplier = .12f;
                PlayWispTraces(origin, wispEmberEnds[0], wispEmberEnds[1], wispEmberEnds[2]);
                if (body.RainEnemyKind > 0)
                    body.RpcRainWispEmbers(origin, wispEmberEnds[0], wispEmberEnds[1], wispEmberEnds[2]);
                PlayVisual(13);
                return;
            }
            if (until - now <= WispFireDuration - .15f) warning.SetActive(false);
            if (now >= until) stage = 0;
        }

        void MoveWisp(float distance, bool visible)
        {
            if (!flyingController || !flyingController.enabled ||
                Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body)) return;

            Vector3 offset = target.transform.position + Vector3.up * WispHoverHeight - transform.position;
            Vector3 horizontal = new Vector3(offset.x, 0f, offset.z);
            bool strafe = distance > 20f && distance <= 30f && visible;
            if (distance <= 20f)
                horizontal = -horizontal.normalized;
            else if (strafe)
                horizontal = transform.right * wispStrafeDirection;
            else
                horizontal = Vector3.ClampMagnitude(horizontal, 1f);

            Vector3 direction = Vector3.ClampMagnitude(horizontal + Vector3.up * Mathf.Clamp(offset.y, -1f, 1f), 1f);
            CollisionFlags contact = flyingController.Move(direction * (6f * Time.deltaTime));
            if (strafe && (contact & CollisionFlags.Sides) != 0)
                wispStrafeDirection = -wispStrafeDirection;
        }

        static Vector3 ApplyEmberSpread(Vector3 direction, float maximumAngle, float minimumAngle = 0f)
        {
            Vector3 pitchAxis = Vector3.Cross(Vector3.up, direction);
            float angle = Random.Range(minimumAngle, maximumAngle);
            float roll = Random.Range(0f, 360f);
            Vector3 spread = Quaternion.Euler(0f, 0f, roll) *
                (Quaternion.Euler(angle, 0f, 0f) * Vector3.forward);
            float yaw = Mathf.Atan2(spread.z, spread.x) * Mathf.Rad2Deg - 90f;
            float pitch = Mathf.Atan2(spread.y, new Vector2(spread.x, spread.z).magnitude) * Mathf.Rad2Deg;
            return Quaternion.AngleAxis(yaw, Vector3.up) *
                (Quaternion.AngleAxis(pitch, pitchAxis) * direction);
        }

        Vector3 FireWispEmber(Vector3 origin, Vector3 direction)
        {
            var victim = TraceFirstHostile(origin, direction, .5f, WispBulletRange, out float nearest);
            if (victim)
            {
                float falloff = .5f + .5f * Mathf.InverseLerp(60f, 25f, nearest);
                int damage = Mathf.RoundToInt(WispEmberDamage * falloff);
                victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, body, 0, 1f / 3f);
            }
            return origin + direction * nearest;
        }

        Health TraceFirstHostile(Vector3 origin, Vector3 direction, float radius, float range, out float nearest)
            => TraceFirstHostile(origin, direction, radius, range, out nearest, out _);

        Health TraceFirstHostile(Vector3 origin, Vector3 direction, float radius, float range, out float nearest,
            out RaycastHit impact)
        {
            int count = Physics.SphereCastNonAlloc(origin, radius, direction, fireballContacts, range,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            Health victim = null;
            nearest = range;
            impact = default;
            for (int i = 0; i < count; i++)
            {
                var contact = fireballContacts[i];
                if (!contact.collider || contact.distance >= nearest) continue;
                var health = contact.collider.GetComponentInParent<Health>();
                if (health && (health == body || health.Team == body.Team || health.CurrentHealth <= 0)) continue;
                if (!health && (contact.collider.isTrigger || contact.collider.gameObject.layer != 0)) continue;
                nearest = contact.distance;
                impact = contact;
                victim = health;
            }
            return victim;
        }

        internal void PlayWispTraces(Vector3 origin, Vector3 first, Vector3 second, Vector3 third)
        {
            if (wispTraces == null) return;
            wispTracesUntil = Time.time + .15f;
            for (int i = 0; i < wispTraces.Length; i++)
            {
                var trace = wispTraces[i];
                trace.SetPosition(0, origin);
                trace.SetPosition(1, i == 0 ? first : i == 1 ? second : third);
                trace.enabled = true;
            }
        }

        void HideWispTraces()
        {
            if (wispTracesUntil <= 0f) return;
            wispTracesUntil = 0f;
            foreach (var trace in wispTraces)
                if (trace) trace.enabled = false;
        }

        void TickLemurian()
        {
            float now = Time.time;
            Vector3 aim = target.CombatAimPoint;
            Vector3 offset = target.transform.position - transform.position;
            bool visible = !Physics.Linecast(body.CombatAimPoint, aim, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore);
            float distance = offset.magnitude;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
            if (stage == 0)
            {
                bool bite = visible && distance <= 6f;
                bool fire = visible && distance >= 15f && distance <= 35f;
                if (!bite && !fire)
                {
                    bool moving = !rooted && SteerChase(visible, distance <= 7f, now);
                    body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
                    PlayVisual(moving ? (byte)1 : (byte)0);
                    return;
                }

                nextChasePath = 0f;
                body.SetMovementInput(Vector2.zero);
                warning.SetActive(true);
                material.color = Color.red;
                laser.enabled = fire;
                warning.GetComponent<MeshRenderer>().enabled = bite;
                stage = fire ? 2 : 1;
                until = now + (fire ? LemurianChargeDuration : 1f);
                if (bite)
                {
                    attackHits.Clear();
                    warning.transform.localScale = new Vector3(1.8f, .08f, LemurianBiteReach);
                }
                PlayVisual(fire ? (byte)10 : (byte)9);
            }
            if (stage == 1)
            {
                float move = distance <= 3f ? .35f : 1f;
                body.SetMovementInput(rooted || !visible ? Vector2.zero : Vector2.up * move);
                headbuttForward = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.forward;
                warning.transform.SetPositionAndRotation(
                    transform.position + headbuttForward * (LemurianBiteReach * .5f) + Vector3.up * .08f,
                    Quaternion.LookRotation(headbuttForward));
                float age = 1f - (until - now);
                if (age >= LemurianBiteOpens && age <= LemurianBiteCloses && lemurianHead)
                {
                    Vector3 center = lemurianHead.TransformPoint(LemurianBiteOffset);
                    Vector3 halfSize = Vector3.Scale(LemurianBiteHalfSize, lemurianHead.lossyScale);
                    int count = Physics.OverlapBoxNonAlloc(center, halfSize, attackContacts, lemurianHead.rotation,
                        GameManager.characterLayer, QueryTriggerInteraction.Collide);
                    CollectAttackHits(count, 24, AttackType.melee);
                }
                if (age > LemurianBiteCloses) warning.SetActive(false);
                if (now >= until) stage = 0;
                return;
            }
            body.SetMovementInput(rooted ? Vector2.zero : GetGroundStrafe());
            if (stage == 2)
            {
                fireballPosition = body.CombatAimPoint;
                fireballDirection = (aim - fireballPosition).normalized;
                laser.SetPosition(0, fireballPosition);
                laser.SetPosition(1, fireballPosition + fireballDirection * 35f);
                laser.widthMultiplier = .045f;
                if (now < until) return;
                stage = 3;
                fireballExpires = now + 5f;
                until = now + LemurianFireDuration;
                PlayVisual(11);
                return;
            }
            if (stage == 3)
            {
                if (warning.activeSelf) AdvanceLemurianFireball(now);
                if (now >= until && !warning.activeSelf) stage = 0;
            }
        }

        bool SteerChase(bool visible, bool direct, float now)
        {
            if (visible && direct)
            {
                nextChasePath = 0f;
                return true;
            }
            Vector3 origin = transform.position;
            if (now >= nextChasePath)
            {
                nextChasePath = now + .4f;
                chaseCorner = 1;
                chaseCornerCount = 0;
                if (NavMesh.SamplePosition(origin, out var start, 2f, NavMesh.AllAreas) &&
                    NavMesh.SamplePosition(target.transform.position, out var end, 2f, NavMesh.AllAreas) &&
                    NavMesh.CalculatePath(start.position, end.position, NavMesh.AllAreas, chasePath) &&
                    chasePath.status == NavMeshPathStatus.PathComplete)
                    chaseCornerCount = chasePath.GetCornersNonAlloc(chaseCorners);
            }
            while (chaseCorner < chaseCornerCount)
            {
                Vector3 heading = chaseCorners[chaseCorner] - origin;
                heading.y = 0f;
                if (heading.sqrMagnitude > .36f)
                {
                    body.lookInput = new Vector2(0f, Mathf.Atan2(heading.x, heading.z) * Mathf.Rad2Deg);
                    return true;
                }
                chaseCorner++;
            }
            return visible;
        }

        Vector2 GetGroundStrafe()
        {
            Vector3 origin = transform.position;
            Vector3 right = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.right;
            if (CanStrafe(origin, -right)) return Vector2.left;
            return CanStrafe(origin, right) ? Vector2.right : Vector2.zero;
        }

        static bool CanStrafe(Vector3 origin, Vector3 direction)
        {
            const float probeDistance = 1f;
            Vector3 waist = origin + Vector3.up;
            return !Physics.Raycast(waist, direction, probeDistance, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore) &&
                Physics.Raycast(waist + direction * probeDistance, Vector3.down, 2f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
        }

        void AdvanceLemurianFireball(float now)
        {
            const float radius = .5f;
            float travel = Time.deltaTime * 60f;
            int count = Physics.SphereCastNonAlloc(fireballPosition, radius, fireballDirection, fireballContacts,
                travel, GameManager.characterLayer, QueryTriggerInteraction.Collide);
            Health victim = null;
            float distance = travel;
            bool impact = false;
            for (int i = 0; i < count; i++)
            {
                var contact = fireballContacts[i];
                if (!contact.collider || contact.distance > distance) continue;
                var health = contact.collider.GetComponentInParent<Health>();
                if (health && (health == body || health.Team == body.Team || health.CurrentHealth <= 0)) continue;
                if (!health && (contact.collider.isTrigger || contact.collider.gameObject.layer != 0)) continue;
                impact = true;
                distance = contact.distance;
                victim = health;
            }
            if (victim) victim.Server_ChangeHealthStateRaw(12, CharacterPart.body, AttackType.hitscan, body, 0);
            fireballPosition += fireballDirection * distance;
            material.color = new Color(1f, .6f, .15f);
            laser.widthMultiplier = 1f;
            laser.SetPosition(0, fireballPosition);
            laser.SetPosition(1, fireballPosition + fireballDirection);
            if (impact || now >= fireballExpires) warning.SetActive(false);
        }

        void TickBeetleGuard()
        {
            float now = Time.time;
            if (stage == 0)
            {
                Vector3 offset = target.transform.position - transform.position;
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
                float distance = offset.magnitude;
                bool slam = distance <= 10f && now >= nextGuardSlam;
                bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                bool sunder = distance >= 10f && distance <= 40f && now >= nextGuardSunder && visible;
                if (slam || sunder)
                {
                    BeginGuardAttack(!slam, now);
                    return;
                }
                bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                    Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
                Vector2 move = Vector2.zero;
                if (!rooted)
                {
                    if (distance <= 10f && visible) move = GetGroundStrafe();
                    else if (SteerChase(visible, distance >= 10f && distance <= 25f, now)) move = Vector2.up;
                }
                body.SetMovementInput(move);
                PlayVisual(move == Vector2.zero ? (byte)0 : (byte)1);
                return;
            }

            if (stage == 1 && now >= guardRelease)
            {
                stage = 2;
                if (guardRanged)
                {
                    sunderStarted = guardRelease;
                    sunderPosition = sunderOrigin;
                }
                else
                {
                    CollectAttackHits(Physics.OverlapBoxNonAlloc(warning.transform.position + Vector3.up * 1.51f,
                        new Vector3(GuardSlamDiameter * .5f, 2.5f, GuardSlamDiameter * .5f),
                        attackContacts, warning.transform.rotation, GameManager.characterLayer, QueryTriggerInteraction.Collide),
                        Mathf.RoundToInt(12f * 4.4f), AttackType.melee);
                    warning.SetActive(false);
                }
            }

            if (stage == 2 && guardRanged) AdvanceSunder(now);
            if (now >= until && !warning.activeSelf)
            {
                stage = 0;
                PlayVisual(0);
            }
        }

        void BeginGuardAttack(bool ranged, float now)
        {
            nextChasePath = 0f;
            guardRanged = ranged;
            attackHits?.Clear();
            stage = 1;
            body.SetMovementInput(Vector2.zero);
            warning.SetActive(true);
            material.color = Color.red;
            laser.enabled = ranged;
            warning.GetComponent<MeshRenderer>().enabled = !ranged;
            if (ranged)
            {
                nextGuardSunder = now + 8f;
                until = now + GuardSunderDuration;
                guardRelease = now + GuardSunderDuration * (1.161012f / 2.766667f);
                sunderOrigin = body.CombatAimPoint;
                Vector3 aim = target.CombatAimPoint - sunderOrigin;
                sunderDirection = aim.sqrMagnitude > .001f ? aim.normalized : transform.forward;
                sunderRotation = Quaternion.LookRotation(sunderDirection);
                laser.SetPosition(0, sunderOrigin);
                laser.SetPosition(1, sunderOrigin + sunderDirection * (SunderSpeed * SunderLifetime));
                laser.widthMultiplier = .045f;
                PlayVisual(GuardSunderAnimation);
                return;
            }

            nextGuardSlam = now + 3f;
            until = now + GuardSlamDuration;
            guardRelease = now + GuardSlamDuration * (1.3213111f / 2.7833335f);
            var facing = Quaternion.Euler(0f, body.lookInput.y, 0f);
            var hitboxRotation = facing * Quaternion.Euler(0f, -13.3048935f, 0f);
            headbuttForward = hitboxRotation * Vector3.forward;
            warning.transform.SetPositionAndRotation(
                transform.position + facing * new Vector3(0f, .08f, 1.3299947f), hitboxRotation);
            warning.transform.localScale = new Vector3(GuardSlamDiameter, .08f, GuardSlamDiameter);
            PlayVisual(GuardSlamAnimation);
        }

        void AdvanceSunder(float now)
        {
            if (!warning.activeSelf) return;
            float age = Mathf.Min(now - sunderStarted, SunderLifetime);
            Vector3 destination = sunderOrigin + sunderDirection * (SunderSpeed * age);
            bool blocked = Physics.Linecast(sunderPosition, destination, out var wall, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore);
            if (blocked) destination = wall.point;
            // The original overlap attack uses the child HitBox transform, not the root sphere collider.
            Vector3 center = (sunderPosition + destination) * .5f + sunderRotation * SunderHitboxOffset;
            Vector3 halfSize = SunderHitboxHalfSize;
            halfSize.z += Vector3.Distance(sunderPosition, destination) * .5f;
            int count = Physics.OverlapBoxNonAlloc(center, halfSize, attackContacts, sunderRotation,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            CollectAttackHits(count, 36, AttackType.hitscan);
            sunderPosition = destination;
            material.color = new Color(1f, .6f, .15f);
            laser.widthMultiplier = .22f;
            laser.SetPosition(0, sunderPosition);
            laser.SetPosition(1, sunderPosition + sunderDirection);
            if (blocked || age >= SunderLifetime) warning.SetActive(false);
        }

        void CollectAttackHits(int count, int damage, AttackType attackType)
        {
            for (int i = 0; i < count; i++)
            {
                var victim = attackContacts[i].GetComponentInParent<Health>();
                attackContacts[i] = null;
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                    Physics.Linecast(body.CombatAimPoint, victim.transform.position + Vector3.up,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore) || !attackHits.Add(victim)) continue;
                victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, attackType, body, 0);
            }
        }

        void PlayVisual(byte state)
        {
            if (visual) visual.SetState(state, speed: state == 23 ? body.RainQueenWardCastSpeed : 1f);
            if (!body || body.RainEnemyKind <= 0 || body.RainEnemyAnimation == state) return;
            body.RainEnemyAnimationStartedAt = Mirror.NetworkTime.time;
            body.RainEnemyAnimation = state;
        }

        void UpdateReplica()
        {
            if (kind == QueenKind) UpdateReplicaQueenSpit();
            if (kind == GreaterWispKind) UpdateReplicaGreaterWispShots();
            if (kind == FlyingVerminKind) UpdateReplicaFlyingVerminSpit();
            if (kind == LunarExploderKind) UpdateLunarShardVisuals();
            if (kind == HermitCrabKind) UpdateHermitMortarVisuals();
            if (kind == BruiserKind) AdvanceReplicaBruiserFireballs();
            if (body.CurrentHealth > 0) UpdateTitanFist();
            else if (titanFist) titanFist.SetActive(false);
            if (visual)
            {
                int animation = body.CurrentHealth <= 0 && DeathAnimationDuration > 0f
                    ? DeathAnimation
                    : body.RainEnemyAnimation;
                if (kind == FlyingVerminKind && body.CurrentHealth <= 0 &&
                    body.RainEnemyAnimation == FlyingVerminDeathLoopAnimation)
                    animation = FlyingVerminDeathLoopAnimation;
                if (kind == ImpKind) SetImpBlinking(animation == ImpBlinkAnimation);
                if (kind == ParentKind) SetParentBlinking(body.CurrentHealth > 0 && body.RainParentBlinkUntil > Mirror.NetworkTime.time);
                double elapsed = animation >= 5 && animation == body.RainEnemyAnimation
                    ? System.Math.Max(0d, Mirror.NetworkTime.time - body.RainEnemyAnimationStartedAt)
                    : 0d;
                if (IsVerminBiteAnimation(animation) && body.RainVerminPauseUntil > Mirror.NetworkTime.time)
                    elapsed = System.Math.Max(0d, body.RainVerminPauseUntil - body.RainEnemyAnimationStartedAt);
                bool restartParent = kind == ParentKind && parentObservedAnimationStart != body.RainEnemyAnimationStartedAt;
                if (kind == ParentKind) parentObservedAnimationStart = body.RainEnemyAnimationStartedAt;
                visual.SetState(animation, elapsed, animation == 23 ? body.RainQueenWardCastSpeed : 1f, restartParent);
                if (kind == LunarExploderKind && animation == DeathAnimation)
                    visual.SetModelVisible(body.RainTelegraph == 1 ||
                        (body.RainTelegraph == 0 && elapsed < LunarExploderDeathDuration));
            }
            if (!warning) return;
            byte cue = body.CurrentHealth > 0 || kind == LunarExploderKind ? body.RainTelegraph : (byte)0;
            warning.SetActive(cue != 0);
            if (cue == 0) return;
            if (kind == LunarExploderKind)
            {
                ShowLunarDeathWarning(body.RainTelegraphStart, Quaternion.Euler(body.RainTelegraphEnd),
                    charging: cue != LunarZoneActiveCue);
                return;
            }
            bool beam = cue >= 2;
            if (laser) laser.enabled = beam;
            warning.GetComponent<MeshRenderer>().enabled = !beam;
            material.color = cue == 3 ? new Color(1f, .6f, .15f) : Color.red;
            if (beam && laser)
            {
                laser.SetPosition(0, body.RainTelegraphStart);
                laser.SetPosition(1, body.RainTelegraphEnd);
                laser.widthMultiplier = cue == 3 ? (kind == 2 ? .45f : kind == LemurianKind ? 1f : kind == BruiserKind ? .5f : .22f) : .045f;
            }
            else
            {
                warning.transform.position = body.RainTelegraphStart;
                warning.transform.localScale = kind == 0
                    ? new Vector3(HeadbuttHalfWidth * 2, .08f, HeadbuttReach)
                    : kind == BeetleGuardKind
                        ? new Vector3(GuardSlamDiameter, .08f, GuardSlamDiameter)
                        : kind == LemurianKind ? new Vector3(1.8f, .08f, LemurianBiteReach)
                        : kind == JellyfishKind ? new Vector3(JellyfishNovaRadius * 2f, .08f, JellyfishNovaRadius * 2f)
                        : kind == ImpKind ? ImpWarningScale
                        : kind == BisonKind ? BisonWarningScale
                        : kind == VerminKind ? VerminWarningScale
                        : kind == HermitCrabKind ? HermitWarningScale
                        : kind == ParentKind ? ParentWarningScale
                        : kind == AcidLarvaKind ? AcidLarvaWarningScale
                        : IsGupFamily ? GupWarningScale
                        : new Vector3(8, .08f, 8);
                if ((kind == 0 || kind == BeetleGuardKind || kind == LemurianKind) && body.RainTelegraphEnd.sqrMagnitude > .001f)
                    warning.transform.rotation = Quaternion.LookRotation(body.RainTelegraphEnd);
            }
        }

        void PublishTelegraph()
        {
            if (!body.isServer || body.RainEnemyKind == 0 || !warning) return;
            if (!warning.activeSelf)
            {
                body.RainTelegraph = 0;
                return;
            }
            if (kind == LunarExploderKind)
            {
                body.RainTelegraph = lunarZoneTick == 0 ? (byte)1 : LunarZoneActiveCue;
                body.RainTelegraphStart = warning.transform.position;
                body.RainTelegraphEnd = warning.transform.rotation.eulerAngles;
                return;
            }
            bool beam = laser && laser.enabled;
            bool charging = kind is LemurianKind or BisonKind ? stage == 2 : stage == 1 || kind == BruiserKind && stage == 4;
            body.RainTelegraph = beam ? (byte)(charging ? 2 : 3) : (byte)1;
            body.RainTelegraphStart = beam ? laser.GetPosition(0) : warning.transform.position;
            body.RainTelegraphEnd = beam ? laser.GetPosition(1) : headbuttForward;
        }
        void CreateTitanFist()
        {
            const string folder = "RainActors/RoR2/Base/Characters/Titan/";
            var prefab = Resources.Load<GameObject>(folder + "mdlTitanFist");
            if (!prefab) return;
            titanFist = Instantiate(prefab, transform);
            titanFist.name = "Titan ground fist";
            var titanMaterial = Resources.Load<Material>(folder + "matTitan");
            if (titanMaterial && titanFist.TryGetComponent<MeshRenderer>(out var renderer))
                renderer.sharedMaterial = titanMaterial;
            titanFist.SetActive(false);
        }

        internal void PlayFistAt(Vector3 point)
        {
            if (!titanFist) return;
            fistPoint = point;
            fistStartedAt = Time.time;
            titanFist.SetActive(true);
            UpdateTitanFist();
        }

        void UpdateTitanFist()
        {
            if (!titanFist || !titanFist.activeSelf) return;
            float age = Time.time - fistStartedAt;
            if (age >= FistVisibleDuration)
            {
                titanFist.SetActive(false);
                return;
            }
            float rise = age < .12f ? age / .12f : 1f - (age - .12f) / (FistVisibleDuration - .12f);
            titanFist.transform.position = fistPoint + Vector3.up * Mathf.Lerp(-1.4f, 1.1f, rise);
        }
        void TickHeadbutt()
        {
            if (Time.time < headbuttOpensAt) return;
            if (Time.time >= until)
            {
                warning.SetActive(false);
                stage = 2;
                until += .85f;
                return;
            }
            if (headbuttHit) return;
            var offset = target.transform.position - transform.position;
            float forward = Vector3.Dot(offset, headbuttForward);
            float lateral = Vector3.Dot(offset, Vector3.Cross(Vector3.up, headbuttForward));
            if (forward < 0f || forward > HeadbuttReach || Mathf.Abs(lateral) > HeadbuttHalfWidth ||
                Mathf.Abs(offset.y) > 1.5f ||
                Physics.Linecast(transform.position + Vector3.up, target.transform.position + Vector3.up,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) return;
            headbuttHit = true;
            target.Server_ChangeHealthStateRaw(12,CharacterPart.body,AttackType.melee,body,0);
        }
        Vector3 LaserOrigin => transform.position + Vector3.up * (kind == 2 ? 4f : 1.5f);

        void TickTitanBeam()
        {
            if (Time.time >= until)
            {
                warning.SetActive(false);
                PlayVisual(4);
                stage = 2;
                until = Time.time + 2f;
                return;
            }
            var aim = (target.CombatAimPoint - LaserOrigin).normalized;
            beamDirection = Vector3.RotateTowards(beamDirection, aim, 1.6f * Time.deltaTime, 0f);
            lockedPoint = LaserOrigin + beamDirection * 40f - Vector3.up;
            UpdateLaser(true, out bool hitTarget);
            if (Time.time < nextBeamTick) return;
            nextBeamTick = Time.time + TitanBeamInterval;
            if (hitTarget) target.Server_ChangeHealthStateRaw(5,CharacterPart.body,AttackType.melee,body,0);
        }

        void UpdateLaser(bool fired, out bool hitTarget)
        {
            var origin = LaserOrigin;
            var direction = (lockedPoint + Vector3.up - origin).normalized;
            var end = origin + direction * 40f;
            hitTarget = false;
            if (Physics.Raycast(origin, direction, out var contact, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                end = contact.point;
                hitTarget = contact.collider.GetComponentInParent<Health>() == target;
            }
            laser.SetPosition(0, origin);
            laser.SetPosition(1, end);
            laser.widthMultiplier = fired ? (titanBeam ? .45f : .22f) : .045f;
            material.color = fired ? new Color(1f, .6f, .15f) : Color.red;
        }
        void LateUpdate()
        {
            if (kind == VerminKind)
            {
                if (visual && body && body.CurrentHealth > 0 && IsVerminBiteAnimation(body.RainEnemyAnimation))
                    visual.SetVerminBitePaused(body.RainVerminPauseUntil > Mirror.NetworkTime.time);
                else ClearVerminLashEffect();
            }
            if (kind == BisonKind)
            {
                UpdateBisonChargeEffect();
                UpdateBisonChargeFootsteps();
            }
            if (kind == BruiserKind) UpdateBruiserBreathEffect();
            if (jellyfishCharge)
            {
                bool charging = body && body.CurrentHealth > 0 && warning && warning.activeSelf;
                if (jellyfishCharge.activeSelf != charging) jellyfishCharge.SetActive(charging);
            }
            if (wispTracesUntil > 0f && (!body || body.CurrentHealth <= 0 || Time.time >= wispTracesUntil))
                HideWispTraces();
            if (body) PublishTelegraph();
            if (!OwnsCharacter || !body || body.CurrentHealth <= 0)
            {
                if (healthBarBackground) healthBarBackground.gameObject.SetActive(false);
                return;
            }
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            if (!overlay) return;
            if (!healthBarBackground)
            {
                float width = kind == 2 ? 144 : 90;
                healthBarBackground = FrontEndUI.Box(overlay, "Summon health " + body.netId, 0, 0, width, 10,
                    new Color(.06f, .04f, .04f, .9f));
                healthBarBackground.transform.SetAsFirstSibling();
                healthBarBackground.raycastTarget = false;
                healthBarBackground.rectTransform.pivot = new Vector2(.5f, 0f);
                healthBarFill = FrontEndUI.Box(healthBarBackground.transform, "Health fill", 1, 1, width - 2, 8,
                    new Color(.94f, .24f, .17f));
                healthBarFill.raycastTarget = false;
            }
            var fill = healthBarFill.rectTransform;
            fill.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, (healthBarBackground.rectTransform.rect.width - 2) *
                Mathf.Clamp01((float)body.CurrentHealth / Mathf.Max(1, body.MaxHealth)));
            if (!healthBarCamera)
                healthBarCamera = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            bool visible = false;
            if (healthBarCamera && (healthBarCamera.transform.position - transform.position).sqrMagnitude <= 3600f)
            {
                var screen = healthBarCamera.WorldToScreenPoint(transform.position + Vector3.up *
                    (kind == 2 ? 5.4f : kind == 1 ? 3.2f : 2.1f));
                visible = screen.z > 0 && screen.x >= 0 && screen.x <= Screen.width &&
                    screen.y >= 0 && screen.y <= Screen.height;
                if (visible && RectTransformUtility.ScreenPointToLocalPointInRectangle(overlay, screen, null, out var local))
                    healthBarBackground.rectTransform.anchoredPosition = local - new Vector2(overlay.rect.xMin, overlay.rect.yMax);
            }
            healthBarBackground.gameObject.SetActive(visible);
        }
        void OnEnable()
        {
            if (kind == ParentKind && parentShapeConfigured && body && body.CurrentHealth > 0)
                SetParentHurtboxesEnabled(true);
        }

        void OnDisable()
        {
            if (kind == ParentKind)
            {
                CancelParentAction();
                SetParentHurtboxesEnabled(false);
            }
            if (healthBarBackground) healthBarBackground.gameObject.SetActive(false);
        }

        void OnDestroy()
        {
            if (kind == ParentKind) { CancelParentAction(); RestoreParentShape(); }
            if (kind == AcidLarvaKind) RestoreAcidLarvaMotor();
            if (kind == FlyingVerminKind) DestroyFlyingVerminSpit();
            if (kind == HermitCrabKind) DestroyHermitMortarVisuals();
            if (kind == LunarExploderKind) DestroyLunarShardVisuals();
            if (kind == ImpKind) SetImpBlinking(false);
            if (kind == GreaterWispKind) DestroyGreaterWispGhosts();
            if (kind == QueenKind && body && !body.isServer)
            {
                body.RainQueenSpit.OnRemove -= RemoveReplicaQueenSpit;
                body.RainQueenSpit.OnClear -= HideQueenSpitVisuals;
                body.RainQueenAcidPools.OnAdd -= ShowReplicaQueenAcidPool;
                body.RainQueenAcidPools.OnRemove -= RemoveReplicaQueenAcidPool;
                body.RainQueenAcidPools.OnClear -= HideQueenAcidPools;
            }
            if (queenSpitVisuals != null)
                foreach (var effect in queenSpitVisuals)
                    if (effect) Destroy(effect);
            if (queenAcidPoolVisuals != null)
                foreach (var effect in queenAcidPoolVisuals)
                    if (effect) Destroy(effect);
            if (kind == BruiserKind && body && !body.isServer)
            {
                body.RainBruiserFireballs.OnAdd -= StartReplicaBruiserFireball;
                body.RainBruiserFireballs.OnRemove -= RemoveReplicaBruiserFireball;
            }
            if (bruiserBreathEffect) Destroy(bruiserBreathEffect);
            if (bruiserFireballVisuals != null)
                foreach (var ghost in bruiserFireballVisuals)
                    if (ghost) Destroy(ghost);
            if (jellyfishCharge) Destroy(jellyfishCharge);
            if (wispTraces != null)
                foreach (var trace in wispTraces)
                    if (trace) Destroy(trace.gameObject);
            if (healthBarBackground) Destroy(healthBarBackground.gameObject);
            if (titanFist) Destroy(titanFist);
            if(body && body.isServer){body.SetMovementInput(Vector2.zero);if(kind==2 && !OwnsCharacter){body.MaxHealth=originalMax;body.CurrentHealth=Mathf.Min(body.CurrentHealth,originalMax);}}
            if (flyingMotor) flyingMotor.enabled = flyingMotorEnabled;
            if (configuredMotor)
            {
                configuredMotor.WalkSpeed = previousWalkSpeed;
                configuredMotor.RunSpeed = previousRunSpeed;
            }
            if(ai)ai.enabled=aiWasEnabled;if(warning)Destroy(warning);if(material)Destroy(material);
            if(visual)Destroy(visual);
        }
    }
}
