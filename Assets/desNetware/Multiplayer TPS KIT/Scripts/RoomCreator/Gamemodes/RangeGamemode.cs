using MTPSKIT.Gameplay;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using UnityEngine.SceneManagement;
using Unity.AI.Navigation;
using UnityEngine.AI;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode : Gamemode
    {
        [SerializeField, Min(1)] int robotHealth = 300;
        public int RobotHealth => robotHealth;
        [SerializeField] SpawnpointsContainer _playersSpawnPoints;
        [SerializeField] SpawnpointsContainer _botsSpawnPoints;
        protected override int BotFillLimit => (gameObject.scene.path == RoomCreator.TrainingScenePath ||
            gameObject.scene.path == RoomCreator.ModularTrainingScenePath) && _botsSpawnPoints
            ? _botsSpawnPoints.Spawnpoints.Count : base.BotFillLimit;
        static readonly int[] RainNormalKinds =
        {
            0, 1, RangeResearchEnemy.BeetleGuardKind, RangeResearchEnemy.LemurianKind,
            RangeResearchEnemy.WispKind, RangeResearchEnemy.JellyfishKind, RangeResearchEnemy.BruiserKind,
            RangeResearchEnemy.GreaterWispKind, RangeResearchEnemy.ImpKind, RangeResearchEnemy.BisonKind,
            RangeResearchEnemy.VerminKind, RangeResearchEnemy.FlyingVerminKind, RangeResearchEnemy.HermitCrabKind,
            RangeResearchEnemy.AcidLarvaKind, RangeResearchEnemy.LunarExploderKind, RangeResearchEnemy.ParentKind
        };
        public static int RainKillsForBoss => RainNormalKinds.Length;
        public bool IsRainCoop => RoomCreator.IsRainScene(SceneManager.GetActiveScene().name);
        [SyncVar] public int RainKills;
        [SyncVar] public bool RainBossActive;
        [SyncVar] internal int RainBossKind = -1;
        [SyncVar] public bool RainVictory;
        readonly SyncHashSet<int> openedRainChests = new SyncHashSet<int>();
        [SyncVar] int activeRainBeacons;
        public const int RainBeaconCount = 3;
        public int RainBeaconsActivated => (activeRainBeacons & 1) +
            ((activeRainBeacons >> 1) & 1) + ((activeRainBeacons >> 2) & 1);
        public const int RainChestPrice = 20;
        internal const int FirstRainEquipmentChest = 19;
        internal const int FirstExtraRainItemChest = FirstRainEquipmentChest + 4;
        internal const int RainScannerChest = 39;
        internal const int RainCardChest = 75;
        internal const int FirstRainMultishopChest = RainCardChest + 1;
        internal const int RainMultishopSize = 3;
        static readonly string[] RainChestRewards =
        {
            "FlatHealth", "CritGlasses", "Tooth", "Syringe", "StickyBomb", "StunChanceOnHit", "Behemoth",
            "NearbyDamageBonus", "ExplodeOnDeath", "WardOnLevel", "ChainLightning", "IgniteOnKill", "IceRing",
            "FireRing", "PersonalShield", "SprintArmor", "OutOfCombatArmor", "SlowOnHit", "Seed",
            "Fruit", "GainArmor", "CritOnUse", "LifestealOnHit",
            "Knurl", "CritDamage", "HealOnCrit", "EquipmentMagazine", "EnergizedOnEquipmentUse", "Infusion", "ExtraLife",
            "Clover", "MoveSpeedOnKill", "SprintOutOfCombat", "DeathMark", "PermanentDebuffOnHit",
            "WarCryOnMultiKill", "BearVoid", "SlowOnHitVoid", "TeamWarCry", "Scanner", "Lightning",
            "ArmorPlate", "ArmorReductionOnHit", "AttackSpeedOnCrit", "AutoCastEquipment",
            "BarrierOnKill", "BarrierOnOverHeal", "Bear", "BleedOnHit", "BleedOnHitAndExplode",
            "BossDamageBonus", "Crowbar", "HealWhileSafe", "Hoof", "IncreaseHealing", "LightningStrikeOnHit",
            "Medkit", "Mushroom", "ParentEgg", "SprintBonus", "BleedOnHitVoid", "ChainLightningVoid",
            "CritGlassesVoid", "ExplodeOnDeathVoid", "ExtraLifeVoid", "FragileDamageBonus", "HealingPotion", "MushroomVoid",
            "NovaOnLowHealth", "NovaOnHeal", "SprintWisp", "Missile", "MissileVoid", "MoreMissile", "CommandMissile",
            "MultiShopCard", "Hoof", "Syringe", "CritGlasses", "GoldOnHit", "BonusGoldPackOnKill", "Pearl", "LunarDagger", "Talisman", "Dagger", "Thorns", "ShockNearby", "SiphonOnLowHealth", "Icicle", "Firework", "FireballsOnHit", "Cleanse", "Plant", "CrippleWard", "DroneBackup", "StrengthenBurn", "GoldOnHurt", "AttackSpeedAndMoveSpeed", "HalfSpeedDoubleHealth", "IncreaseDamageOnMultiKill", "FlatHealth", "CookedSteak", "Clover", "UltimateMeal", "UltimateMealFrostRelic", "CookedSteakMocha", "TPHealingNova", "Stew", "TPHealingNova", "StewDoubleDaisy", "CritAtLowerElevation", "FlatHealth", "FlatHealth", "BonusHealthBoost", "AttackSpeedAndMoveSpeed", "ScrapBisonSteak", "PrintSyringe", "AttackSpeedPerNearbyAllyOrEnemy", "SpeedBoostPickup", "SpeedOnPickup"
        };
        float rainStageStartedAt;

        internal double RainDifficultyCoefficient()
        {
            int players = 0;
            foreach (var player in GameManager.Players.Values)
                if (player && !player.BOT) players++;
            players = Mathf.Max(1, players);
            double minutes = System.Math.Max(0d, Time.time - rainStageStartedAt) / 60d;
            return 1d + .3d * (players - 1) + minutes * .1012d * System.Math.Pow(players, .2d);
        }

        public static int RainChestCount => RainChestRewards.Length;
        internal static string RainChestRewardId(int index) => RainChestRewards[index];
        internal static bool IsRainWhiteItemStation(int index) =>
            (uint)index < RainChestCount && RainChestRewardId(index) is "ScrapBisonSteak" or "PrintSyringe";

        internal static bool IsRainCookStation(int index) =>
            (uint)index < RainChestCount && RainChestRewardId(index) is "CookedSteak" or "CookedSteakMocha" or "UltimateMeal" or "UltimateMealFrostRelic" or "Stew" or "StewDoubleDaisy" or "BonusHealthBoost";
        internal static bool IsRainEquipmentChest(int index) =>
            (uint)index < RainChestCount &&
            RangeResearchItems.EquipmentForCatalog(RainChestRewardId(index)) != RangeResearchItems.Equipment.None;
        internal static bool IsRainMultishopChest(int index) =>
            index >= FirstRainMultishopChest && index < FirstRainMultishopChest + RainMultishopSize;
        readonly SyncList<Vector4> rainBanners = new SyncList<Vector4>();
        public struct ToothPickup
        {
            public Vector3 Position;
            public int Team, Stacks, Gold;
            public float ExpiresAt;
            public float CollectAfter;
            public Quaternion Rotation;
            public string CatalogId;
            public string MonsterLogId;
        }
        internal const string HermitCrabLogId = RainMonsterLogs.HermitCrabId;
        internal const string LunarExploderLogId = RainMonsterLogs.LunarExploderId;
        static readonly Vector3 MonsterLogPickupHalfSize = new Vector3(.855f, .625f, .82f);
        Collider[] monsterLogContacts = new Collider[8];
        sealed class MonsterLogMotion
        {
            internal Rigidbody Body;
            internal CharacterInstance Target;
        }
        readonly Dictionary<uint, MonsterLogMotion> monsterLogBodies = new Dictionary<uint, MonsterLogMotion>();
        float nextMonsterLogSnapshot;
        static readonly string[] QueenTierTwoRewards =
        {
            "ChainLightning", "IceRing", "FireRing", "AttackSpeedOnCrit", "EquipmentMagazine", "EnergizedOnEquipmentUse"
        };
        readonly SyncDictionary<uint, ToothPickup> toothPickups = new SyncDictionary<uint, ToothPickup>();
        uint nextQueenWard;
        internal struct QueenWardOrbVisual
        {
            public Vector3 Origin, InitialTarget, StartVelocity;
            public uint TargetId;
            public double StartedAt;
            public float Duration;
        }

        sealed class QueenWardOrbEffect
        {
            internal Transform Transform;
            internal Vector3 LastTarget;
        }

        readonly SyncDictionary<uint, QueenWardOrbVisual> queenWardOrbStates =
            new SyncDictionary<uint, QueenWardOrbVisual>();
        readonly Dictionary<uint, QueenWardOrbEffect> queenWardOrbEffects =
            new Dictionary<uint, QueenWardOrbEffect>();
        GameObject queenWardOrbPrefab;
        readonly Dictionary<uint, GameObject> toothVisuals = new Dictionary<uint, GameObject>();
        uint nextToothPickup;
        Material bannerMaterial;
        int renderedBanners;
        readonly List<GameObject> rainChests = new List<GameObject>(RainChestCount);
        readonly List<GameObject> rainBeacons = new List<GameObject>(RainBeaconCount);
        MaterialPropertyBlock beaconIdle, beaconActive;
        int renderedBeacons = -1;
        readonly Dictionary<Health, RangeResearchEnemy> rainEnemies = new Dictionary<Health, RangeResearchEnemy>();
        float nextRainSpawn, nextRainTargetRefresh;
        int rainSpawnCount;
        bool rainNavigationInitialized;
        NavMeshSurface rainNavigation;
        NavMeshPath rainTargetPath;
        bool HasRainNavigation => rainNavigation && rainNavigation.navMeshData;

        protected override void Awake()
        {
            base.Awake();
            PeacufulBots = true;
            if (IsRainCoop) FriendyFire = false;
            if (!IsRainCoop || Debug.isDebugBuild) gameObject.AddComponent<RangeResearchLab>();
        }

        protected override void Start()
        {
            base.Start();
            if (!IsRainCoop || _playersSpawnPoints.Spawnpoints.Count == 0) return;
            rainStageStartedAt = Time.time;
            var spawn = _playersSpawnPoints.Spawnpoints[0];
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", new Color(.95f, .7f, .16f));
            tint.SetColor("_Color", new Color(.95f, .7f, .16f));
            var cookingTint = NodeTint(new Color(.95f, .36f, .12f, 1f));
            for (int i = 0; i < RainChestCount; i++)
            {
                int row = i / 15;
                int column = i % 15;
                var point = spawn.position + spawn.forward * (3f + row * 5f) + spawn.right * ((column - 1) * 2.5f);
                if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var ground, 8f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) point = ground.point;
                var chest = GameObject.CreatePrimitive(PrimitiveType.Cube);
                chest.name = "Rain supply chest " + i;
                chest.transform.SetParent(transform, true);
                chest.transform.position = point + Vector3.up * .45f;
                chest.transform.localScale = new Vector3(1.2f, .9f, .8f);
                chest.GetComponent<Collider>().enabled = false;
                chest.GetComponent<Renderer>().SetPropertyBlock(IsRainCookStation(i) || IsRainWhiteItemStation(i) ? cookingTint : tint);
                rainChests.Add(chest);
            }
            beaconIdle = NodeTint(new Color(.15f, .75f, 1f));
            beaconActive = NodeTint(new Color(.3f, 1f, .35f));
            for (int i = 0; i < RainBeaconCount; i++)
            {
                var point = spawn.position + spawn.forward * (6f + i * 3f) + spawn.right * ((i - 1) * 4f);
                if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var ground, 8f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) point = ground.point;
                var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                beacon.name = "Rain task beacon " + i;
                beacon.transform.SetParent(transform, true);
                beacon.transform.position = point + Vector3.up;
                beacon.transform.localScale = new Vector3(.7f, 1f, .7f);
                beacon.GetComponent<Collider>().enabled = false;
                rainBeacons.Add(beacon);
            }
        }

        static MaterialPropertyBlock NodeTint(Color color)
        {
            var tint = new MaterialPropertyBlock();
            tint.SetColor("_BaseColor", color);
            tint.SetColor("_Color", color);
            return tint;
        }

        void LateUpdate()
        {
            if (isClient && queenWardOrbEffects.Count > 0) UpdateQueenWardOrbs();
            if (isClient && renderedBanners < rainBanners.Count) DrawRainBanners();
            for (int i = 0; i < rainChests.Count; i++)
                rainChests[i].SetActive(!openedRainChests.Contains(i));
            if (renderedBeacons == activeRainBeacons) return;
            renderedBeacons = activeRainBeacons;
            for (int i = 0; i < rainBeacons.Count; i++)
                rainBeacons[i].GetComponent<Renderer>().SetPropertyBlock(
                    (activeRainBeacons & (1 << i)) == 0 ? beaconIdle : beaconActive);
        }

        [Server]
        internal void PlaceRainBanner(Vector3 position, int stacks)
        {
            if (!IsRainCoop || stacks <= 0) return;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var ground, 8f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                position = ground.point;
            rainBanners.Add(new Vector4(position.x, position.y, position.z, 8f + 8f * stacks));
        }

        void DrawRainBanners()
        {
            if (!bannerMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader) bannerMaterial = new Material(shader) { color = new Color(1f, .72f, .12f) };
            }
            while (renderedBanners < rainBanners.Count)
            {
                var banner = rainBanners[renderedBanners++];
                var ring = new GameObject("Rain warbanner").AddComponent<LineRenderer>();
                ring.transform.SetParent(transform);
                ring.loop = true;
                ring.widthMultiplier = .12f;
                if (bannerMaterial) ring.sharedMaterial = bannerMaterial;
                ring.positionCount = 64;
                for (int i = 0; i < ring.positionCount; i++)
                {
                    float angle = 2f * Mathf.PI * i / ring.positionCount;
                    ring.SetPosition(i, new Vector3(banner.x + Mathf.Cos(angle) * banner.w,
                        banner.y + .12f, banner.z + Mathf.Sin(angle) * banner.w));
                }
            }
        }

        public override void OnStopClient()
        {
            StopRainAntlerVisuals();
            queenWardOrbStates.OnAdd -= DrawQueenWardOrb;
            queenWardOrbStates.OnRemove -= RemoveQueenWardOrbEffect;
            queenWardOrbStates.OnClear -= ClearQueenWardOrbEffects;
            ClearQueenWardOrbEffects();
            toothPickups.OnAdd -= DrawToothPickup;
            toothPickups.OnSet -= UpdateMonsterLogVisual;
            toothPickups.OnRemove -= RemoveToothVisual;
            foreach (var visual in toothVisuals.Values) if (visual) Destroy(visual);
            toothVisuals.Clear();
            if (bannerMaterial) Destroy(bannerMaterial);
            StopRainHealingPlantVisuals();
            StopRainEffigyVisuals();
            base.OnStopClient();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            StartRainAntlerVisuals();
            StartRainHealingPlantVisuals();
            StartRainEffigyVisuals();
            queenWardOrbStates.OnAdd += DrawQueenWardOrb;
            queenWardOrbStates.OnRemove += RemoveQueenWardOrbEffect;
            queenWardOrbStates.OnClear += ClearQueenWardOrbEffects;
            foreach (uint id in queenWardOrbStates.Keys) DrawQueenWardOrb(id);
            toothPickups.OnAdd += DrawToothPickup;
            toothPickups.OnSet += UpdateMonsterLogVisual;
            toothPickups.OnRemove += RemoveToothVisual;
            foreach (uint id in toothPickups.Keys) DrawToothPickup(id);
        }

        internal uint PublishQueenWardOrb(Vector3 origin, CharacterInstance target, float duration)
        {
            uint id = ++nextQueenWard;
            queenWardOrbStates.Add(id, new QueenWardOrbVisual
            {
                Origin = origin,
                InitialTarget = target.CombatAimPoint,
                StartVelocity = new Vector3(Random.Range(-2f, 2f), Random.Range(5f, 20f), 2f),
                TargetId = target.netId,
                StartedAt = NetworkTime.time,
                Duration = duration
            });
            return id;
        }

        internal void RemoveQueenWardOrb(uint id) => queenWardOrbStates.Remove(id);

        void DrawQueenWardOrb(uint id)
        {
            if (!queenWardOrbPrefab)
                queenWardOrbPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/BeetleGroup/BeetleWard/BeetleWardOrbEffect");
            if (!queenWardOrbPrefab) return;
            var state = queenWardOrbStates[id];
            var effect = new QueenWardOrbEffect { LastTarget = state.InitialTarget };
            Vector3 position = QueenWardOrbPosition(state, effect, NetworkTime.time, out var heading);
            Quaternion rotation = heading.sqrMagnitude > .0001f ? Quaternion.LookRotation(heading) : Quaternion.identity;
            effect.Transform = Instantiate(queenWardOrbPrefab, position, rotation).transform;
            queenWardOrbEffects.Add(id, effect);
        }

        static Vector3 QueenWardOrbPosition(QueenWardOrbVisual state, QueenWardOrbEffect effect,
            double now, out Vector3 heading)
        {
            if (NetworkClient.spawned.TryGetValue(state.TargetId, out var identity) && identity &&
                identity.TryGetComponent<CharacterInstance>(out var target))
                effect.LastTarget = target.CombatAimPoint;
            float progress = state.Duration > 0f
                ? Mathf.Clamp01((float)(now - state.StartedAt) / state.Duration) : 1f;
            heading = effect.LastTarget - state.Origin + state.StartVelocity * (1f - 2f * progress);
            return Vector3.Lerp(state.Origin + state.StartVelocity * progress, effect.LastTarget, progress);
        }

        void UpdateQueenWardOrbs()
        {
            double now = NetworkTime.time;
            foreach (var entry in queenWardOrbEffects)
            {
                var effect = entry.Value;
                Vector3 position = QueenWardOrbPosition(queenWardOrbStates[entry.Key], effect, now, out var heading);
                Vector3 movement = position - effect.Transform.position;
                if (movement.sqrMagnitude > .0001f) heading = movement;
                Quaternion rotation = heading.sqrMagnitude > .0001f
                    ? Quaternion.LookRotation(heading) : effect.Transform.rotation;
                effect.Transform.SetPositionAndRotation(position, rotation);
            }
        }

        void RemoveQueenWardOrbEffect(uint id, QueenWardOrbVisual _)
        {
            if (!queenWardOrbEffects.TryGetValue(id, out var effect)) return;
            Destroy(effect.Transform.gameObject);
            queenWardOrbEffects.Remove(id);
        }

        void ClearQueenWardOrbEffects()
        {
            foreach (var effect in queenWardOrbEffects.Values)
                if (effect.Transform) Destroy(effect.Transform.gameObject);
            queenWardOrbEffects.Clear();
        }

        [Server]
        internal void DropToothPickup(Vector3 position, int team, int stacks)
        {
            if (team < 0 || stacks <= 0) return;
            DropRewardPickup(position, team, stacks, 0);
        }

        [Server]
        internal void DropGoldPack(Vector3 position, int team)
        {
            if (team < 0) return;
            DropRewardPickup(position, team, 0, 25);
        }

        void DropRewardPickup(Vector3 position, int team, int stacks, int gold, string catalogId = null)
        {
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var ground, 8f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                position = ground.point;
            toothPickups.Add(++nextToothPickup, new ToothPickup
            {
                Position = position, Team = team, Stacks = stacks, Gold = gold, CatalogId = catalogId,
                ExpiresAt = string.IsNullOrEmpty(catalogId) ? Time.time + 20f : float.PositiveInfinity
            });
        }

        static AudioClip toothSpawnAudio;

        void DrawToothPickup(uint id)
        {
            var pickup = toothPickups[id];
            if (!string.IsNullOrEmpty(pickup.MonsterLogId))
            {
                var prefab = Resources.Load<GameObject>("RainActors/RoR2/Base/Common/LogPickup/mdlMonsterLogPickup");
                if (!prefab) return;
                var model = Instantiate(prefab, pickup.Position, pickup.Rotation, transform);
                model.name = "Rain monster log " + pickup.MonsterLogId;
                model.AddComponent<RainMonsterLogVisual>();
                toothVisuals.Add(id, model);
                return;
            }
            var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bool item = !string.IsNullOrEmpty(pickup.CatalogId);
            orb.name = item ? "Rain boss reward " + pickup.CatalogId :
                pickup.Gold > 0 ? "Ghor gold pickup" : "Monster tooth healing pickup";
            orb.GetComponent<Collider>().enabled = false;
            orb.transform.SetParent(transform);
            orb.transform.position = pickup.Position + Vector3.up * .4f;
            orb.transform.localScale = Vector3.one * (.45f * Mathf.Pow(Mathf.Max(1, pickup.Stacks), .25f));
            orb.GetComponent<Renderer>().SetPropertyBlock(NodeTint(item
                ? pickup.CatalogId == "BeetleGland" ? new Color(1f, .85f, .16f) : new Color(.28f, .85f, .30f)
                : pickup.Gold > 0 ? new Color(1f, .72f, .16f) : new Color(.26f, 1f, .38f)));
            toothVisuals.Add(id, orb);
            if (!item && pickup.Gold == 0)
            {
                if (!toothSpawnAudio) toothSpawnAudio = Resources.Load<AudioClip>("RainImported/MonsterToothSpawn");
                if (toothSpawnAudio) AudioSource.PlayClipAtPoint(toothSpawnAudio, pickup.Position, .65f);
            }
        }

        void UpdateMonsterLogVisual(uint id, ToothPickup previous)
        {
            if (string.IsNullOrEmpty(previous.MonsterLogId) || !toothVisuals.TryGetValue(id, out var model) || !model)
                return;
            var pickup = toothPickups[id];
            model.transform.SetPositionAndRotation(pickup.Position, pickup.Rotation);
        }

        void SpawnMonsterLog(Vector3 position, string logId)
        {
            const float coneRadius = 30f;
            Quaternion launch = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) *
                Quaternion.Euler(Random.Range(0f, coneRadius), 0f, 0f);
            var physicsObject = new GameObject("Rain monster log body");
            physicsObject.layer = (int)GameLayers.item;
            physicsObject.transform.SetParent(transform);
            physicsObject.transform.SetPositionAndRotation(position + Vector3.up * .4f, Random.rotation);
            var collider = physicsObject.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.16f, .43f, 1.39f);
            collider.excludeLayers = ~GameManager.environmentLayer;
            collider.layerOverridePriority = 1;
            var body = physicsObject.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.linearDamping = .1f;
            body.angularDamping = .05f;
            body.collisionDetectionMode = CollisionDetectionMode.Continuous;
            body.maxAngularVelocity = 4f * Mathf.PI;
            body.linearVelocity = launch * (Vector3.up * 10f);
            body.angularVelocity = Random.onUnitSphere * body.maxAngularVelocity;
            uint id = ++nextToothPickup;
            monsterLogBodies.Add(id, new MonsterLogMotion { Body = body });
            toothPickups.Add(id, new ToothPickup
            {
                Team = 0, MonsterLogId = logId, Position = body.position, Rotation = body.rotation,
                CollectAfter = Time.time + .25f, ExpiresAt = Time.time + 300f
            });
        }

        void FixedUpdate()
        {
            if (!isServer) return;
            UpdateRainScannerPulses();
            UpdateRainAntlers();
            foreach (var motion in monsterLogBodies.Values)
            {
                var body = motion.Body;
                if (!body) continue;
                if (!motion.Target || motion.Target.CurrentHealth <= 0)
                {
                    motion.Target = null;
                    float nearest = 16f;
                    foreach (var health in GameManager.HealthInstances.Values)
                    {
                        if (health is not CharacterInstance character || !character ||
                            character.Team != 0 || character.CurrentHealth <= 0) continue;
                        float distance = (character.CombatAimPoint - body.position).sqrMagnitude;
                        if (distance >= nearest) continue;
                        nearest = distance;
                        motion.Target = character;
                    }
                }
                if (motion.Target)
                {
                    Vector3 direction = (motion.Target.CombatAimPoint - body.position).normalized;
                    body.linearVelocity = Vector3.MoveTowards(body.linearVelocity, direction * 30f, 10f);
                }
                if (!body.IsSleeping()) body.AddForce(Vector3.down * 30f, ForceMode.Acceleration);
            }
        }

        void PublishMonsterLogMotion()
        {
            if (monsterLogBodies.Count == 0 || Time.time < nextMonsterLogSnapshot) return;
            nextMonsterLogSnapshot = Time.time + 1f / 15f;
            foreach (var entry in monsterLogBodies)
            {
                var body = entry.Value.Body;
                if (!body || !toothPickups.TryGetValue(entry.Key, out var pickup)) continue;
                if ((pickup.Position - body.position).sqrMagnitude < .0001f &&
                    Quaternion.Angle(pickup.Rotation, body.rotation) < .5f) continue;
                pickup.Position = body.position;
                pickup.Rotation = body.rotation;
                toothPickups[entry.Key] = pickup;
            }
        }

        void RemoveToothVisual(uint id, ToothPickup pickup)
        {
            if (toothVisuals.TryGetValue(id, out var orb) && orb) Destroy(orb);
            toothVisuals.Remove(id);
        }

        bool TryCollectMonsterLog(ToothPickup pickup, Rigidbody body)
        {
            int count;
            while ((count = Physics.OverlapBoxNonAlloc(body.position,
                       MonsterLogPickupHalfSize, monsterLogContacts, body.rotation,
                       GameManager.characterLayer & ~GameManager.environmentLayer,
                       QueryTriggerInteraction.Collide)) == monsterLogContacts.Length)
                System.Array.Resize(ref monsterLogContacts, monsterLogContacts.Length * 2);

            bool collected = false;
            for (int i = 0; i < count; i++)
            {
                var contact = monsterLogContacts[i];
                monsterLogContacts[i] = null;
                if (collected || !contact) continue;
                var character = contact.GetComponentInParent<CharacterInstance>();
                if (character && character.Team == pickup.Team && character.CurrentHealth > 0)
                    collected = TryGrantMonsterLog(character, pickup.MonsterLogId);
            }
            return collected;
        }

        bool TryGrantMonsterLog(CharacterInstance character, string logId)
        {
            // Original UnlockPickup accepts player-team bodies with an inventory, including allies.
            // CharacterInstance owns the inventory in this project; the activator need not own a player connection.
            if (!character || character.Team != 0 || character.CurrentHealth <= 0) return false;

            foreach (var player in GameManager.Players.Values)
                if (player && !player.BOT && player.Team == 0 && player.connectionToClient != null)
                    player.ServerGrantRainMonsterLog(logId);
            return true;
        }

        bool CanDropMonsterLog(string logId)
        {
            foreach (var player in GameManager.Players.Values)
                if (player && !player.BOT && player.Team == 0 && player.connectionToClient != null &&
                    !player.ServerHasRainMonsterLog(logId))
                    return true;
            return false;
        }

        void DropQueenRewards(Vector3 position)
        {
            int count = 0;
            foreach (var player in GameManager.Players.Values)
                if (player && !player.BOT && player.Team == 0) count++;
            if (count == 0) return;

            string tierTwo = QueenTierTwoRewards[Random.Range(0, QueenTierTwoRewards.Length)];
            bool canDropGland = RangeResearchItems.CatalogIndex("BeetleGland") >= 0;
            float firstAngle = Random.Range(0f, 360f);
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Quaternion.Euler(0f, firstAngle + 360f * i / count, 0f) * Vector3.forward * 1.5f;
                string reward = canDropGland && Random.value <= .15f ? "BeetleGland" : tierTwo;
                DropRewardPickup(position + offset, 0, 0, 0, reward);
            }
        }

        void RefreshRainBannerBuffs()
        {
            if (rainBanners.Count == 0) return;
            foreach (var player in GameManager.Players.Values)
            {
                if (!player || player.BOT || !player.MyCharacter) continue;
                var character = player.MyCharacter;
                bool inside = false;
                if (character.CurrentHealth > 0)
                {
                    var point = character.transform.position;
                    foreach (var banner in rainBanners)
                    {
                        float x = point.x - banner.x, z = point.z - banner.z;
                        if (x * x + z * z > banner.w * banner.w) continue;
                        inside = true;
                        break;
                    }
                }
                character.ResearchBannerBuff = inside;
            }
        }

        public int NearbyRainChest(CharacterInstance character) => NearbyNode(character, false);
        public int NearbyRainBeacon(CharacterInstance character) => NearbyNode(character, true);

        bool RainNodeConsumed(int index, bool beacon) =>
            beacon ? (activeRainBeacons & (1 << index)) != 0 : openedRainChests.Contains(index);

        internal bool TryGetRainMapNode(int index, bool beacon, out Vector3 position)
        {
            var nodes = beacon ? rainBeacons : rainChests;
            position = default;
            if ((uint)index >= nodes.Count || RainNodeConsumed(index, beacon)) return false;
            position = nodes[index].transform.position;
            return true;
        }

        int NearbyNode(CharacterInstance character, bool beacon)
        {
            if (!character || character.CurrentHealth <= 0) return -1;
            var nodes = beacon ? rainBeacons : rainChests;
            int nearest = -1;
            float distance = 3f * 3f;
            for (int i = 0; i < nodes.Count; i++)
            {
                if (RainNodeConsumed(i, beacon)) continue;
                float candidate = (character.transform.position - nodes[i].transform.position).sqrMagnitude;
                if (candidate < distance) { nearest = i; distance = candidate; }
            }
            return nearest;
        }

        [Server]
        internal void ActivateRainBeacon(PlayerInstance player, int index)
        {
            if (!IsRainCoop || !player || player.BOT || index < 0 ||
                index != NearbyRainBeacon(player.MyCharacter)) return;
            int previousBeacons = RainBeaconsActivated;
            activeRainBeacons |= 1 << index;
            HealRainBeaconAllies(player.MyCharacter, rainBeacons[index].transform.position, previousBeacons);
            if (player.MyCharacter.TryGetComponent<RangeResearchItems>(out var items))
                items.OnRainInteraction(items.FireworkStacks);
            foreach (var teammate in GameManager.Players.Values)
                if (teammate && !teammate.BOT) teammate.RainGold += 20;
        }

        void HealRainBeaconAllies(CharacterInstance activator, Vector3 center, int previousBeacons)
        {
            long stacks = 0;
            foreach (var player in GameManager.Players.Values)
                if (player && !player.BOT && player.MyCharacter && player.MyCharacter.CurrentHealth > 0 &&
                    player.MyCharacter.Team == activator.Team)
                    stacks += player.MyCharacter.ResearchLeptonDaisies;
            long intervals = stacks + 1;
            long before = System.Math.Min(stacks, previousBeacons * intervals / RainBeaconCount);
            long after = System.Math.Min(stacks, RainBeaconsActivated * intervals / RainBeaconCount);
            if (after <= before) return;

            const float radius = 20f;
            RpcRainLeptonDaisyNova(center, radius);
            foreach (var ally in GameManager.HealthInstances.Values)
            {
                if (!ally || ally.Team != activator.Team || ally.CurrentHealth <= 0 ||
                    (ally.transform.position - center).sqrMagnitude > radius * radius) continue;
                for (long pulse = before; pulse < after && ally.CurrentHealth < ally.MaxHealth; pulse++)
                    if (ally.ServerHeal(Mathf.CeilToInt(ally.MaxHealth * .5f), activator.netId,
                        allowOverhealBarrier: false) <= 0) break;
            }
        }

        [ClientRpc]
        void RpcRainLeptonDaisyNova(Vector3 center, float radius)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var shader = Shader.Find("Sprites/Default");
            if (!shader) return;

            const float duration = .65f;
            var ring = new GameObject("Rain Lepton Daisy nova").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, true);
            ring.transform.position = center - Vector3.up * .85f;
            ring.sharedMaterial = new Material(shader) { color = new Color(.45f, 1f, .55f, .85f) };
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.widthMultiplier = .16f;
            ring.positionCount = 48;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ring.positionCount);
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)));
            }
            Destroy(ring.sharedMaterial, duration);
            Destroy(ring.gameObject, duration);
            StartCoroutine(ExpandLeptonDaisyNova(ring.transform, radius, duration));
        }

        static IEnumerator ExpandLeptonDaisyNova(Transform ring, float radius, float duration)
        {
            float start = Time.time;
            while (ring && Time.time - start < duration)
            {
                float progress = Mathf.Clamp01((Time.time - start) / duration);
                float size = Mathf.Lerp(.5f, radius, progress);
                ring.localScale = new Vector3(size, 1f, size);
                yield return null;
            }
        }

        [Server]
        internal void BuyRainChest(PlayerInstance player, int index)
        {
            if (!IsRainCoop || !player || player.BOT || index < 0 || index != NearbyRainChest(player.MyCharacter)) return;
            var character = player.MyCharacter;
            if (IsRainCookStation(index))
            {
                if (!character.TryGetComponent<RangeResearchItems>(out var pantry) || !pantry.TryCookMeal(RainChestRewardId(index))) return;
                pantry.OnRainInteraction(pantry.FireworkStacks);
                player.RecordRainChestItems(pantry);
                return;
            }
            if (IsRainWhiteItemStation(index))
            {
                if (!character.TryGetComponent<RangeResearchItems>(out var inventory) ||
                    !inventory.TryUseWhiteItemStation(RainChestRewardId(index))) return;
                inventory.OnRainInteraction(inventory.FireworkStacks);
                player.RecordRainChestItems(inventory);
                return;
            }
            if (player.RainGold < RainChestPrice) return;
            if (!character.TryGetComponent<RangeResearchItems>(out var items))
                items = character.gameObject.AddComponent<RangeResearchItems>();
            int fireworkStacks = items.FireworkStacks;
            bool hasCard = items.Equipped == RangeResearchItems.Equipment.ExecutiveCard;
            string reward = RainChestRewardId(index);
            if (IsRainEquipmentChest(index))
            {
                if (!items.Equip(RangeResearchItems.EquipmentForCatalog(reward))) return;
            }
            else
            {
                int item = RangeResearchItems.CatalogIndex(reward);
                if (item < 0) return;
                items.Grant(item);
            }
            openedRainChests.Add(index);
            if (!hasCard && IsRainMultishopChest(index))
                for (int terminal = FirstRainMultishopChest; terminal < FirstRainMultishopChest + RainMultishopSize; terminal++)
                    openedRainChests.Add(terminal);
            player.RainGold -= hasCard ? RainChestPrice - RainChestPrice / 10 : RainChestPrice;
            items.OnRainInteraction(fireworkStacks);
            player.RecordRainChestItems(items);
            // Commit payment/opened state before user callbacks can request the same chest again.
            var pickup = new RangePickupEvent(character, RangePickupClass.Item, reward);
            NotifyWorldPickupBody(in pickup);
            NotifyWorldPickupGlobal(in pickup);
        }

        [ServerCallback]
        void Update()
        {
            UpdateToothPickups();
            if (!IsRainCoop) return;
            if (!rainNavigationInitialized)
            {
                rainNavigationInitialized = true;
                var arena = GameObject.Find("Chapter One");
                if (arena)
                {
                    rainNavigation = arena.AddComponent<NavMeshSurface>();
                    rainNavigation.collectObjects = CollectObjects.Children;
                    rainNavigation.layerMask = GameManager.environmentLayer;
                    rainNavigation.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
                    rainNavigation.BuildNavMesh();
                    rainTargetPath = new NavMeshPath();
                }
            }
            RefreshRainBannerBuffs();
            if (RainVictory) return;
            if (Time.time >= nextRainTargetRefresh)
            {
                nextRainTargetRefresh = Time.time + .4f;
                foreach (var actor in rainEnemies.Values)
                {
                    if (!actor) continue;
                    Vector3 origin = actor.transform.position;
                    CharacterInstance reachable = null;
                    if (HasRainNavigation && actor.UsesGroundNavigation &&
                        NavMesh.SamplePosition(origin, out var start, 2f, NavMesh.AllAreas))
                        reachable = FindNearestRainTarget(start.position, requireReachable: true);
                    actor.SetTarget(reachable ? reachable : FindNearestRainTarget(origin));
                }
            }
            if (Time.time < nextRainSpawn || RainBossActive || rainEnemies.Count >= 3) return;
            nextRainSpawn = Time.time + 2f;
            bool boss = RainKills >= RainKillsForBoss && RainBeaconsActivated == RainBeaconCount;
            if (boss && RainBossKind < 0)
                RainBossKind = Random.Range(0, 2) == 0 ? 2 : RangeResearchEnemy.QueenKind;
            int kind = boss ? RainBossKind : RainNormalKinds[rainSpawnCount % RainNormalKinds.Length];
            if (!TryFindRainSpawn(kind, out var position, out var rotation, out var target)) return;
            var enemy = Instantiate(GetPlayerCharacterPrefab(), position, rotation).GetComponent<CharacterInstance>();
            foreach (var slot in enemy.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            ConfigurePlayerCharacter(enemy);
            enemy.Team = 1;
            enemy.RainEnemyKind = kind + 1;
            enemy.MaxHealth = enemy.CurrentHealth = kind switch
            {
                2 => 1500,
                1 => 300,
                RangeResearchEnemy.BeetleGuardKind => RangeResearchEnemy.BeetleGuardHealth,
                RangeResearchEnemy.LemurianKind => RangeResearchEnemy.LemurianHealth,
                RangeResearchEnemy.WispKind => RangeResearchEnemy.WispHealth,
                RangeResearchEnemy.JellyfishKind => RangeResearchEnemy.JellyfishHealth,
                RangeResearchEnemy.BruiserKind => RangeResearchEnemy.BruiserHealth,
                RangeResearchEnemy.GreaterWispKind => RangeResearchEnemy.GreaterWispHealth,
                RangeResearchEnemy.ImpKind => RangeResearchEnemy.ImpHealth,
                RangeResearchEnemy.BisonKind => RangeResearchEnemy.BisonHealth,
                RangeResearchEnemy.VerminKind => RangeResearchEnemy.VerminHealth,
                RangeResearchEnemy.FlyingVerminKind => RangeResearchEnemy.FlyingVerminHealth,
                RangeResearchEnemy.HermitCrabKind => RangeResearchEnemy.HermitCrabHealth,
                RangeResearchEnemy.AcidLarvaKind => RangeResearchEnemy.AcidLarvaHealth,
                RangeResearchEnemy.LunarExploderKind => RangeResearchEnemy.LunarExploderHealth,
                RangeResearchEnemy.ParentKind => RangeResearchEnemy.ParentHealth,
                RangeResearchEnemy.QueenKind => RangeResearchEnemy.QueenHealth,
                _ => 120
            };
            NetworkServer.Spawn(enemy.gameObject);
            enemy.SetAsBOT(true);
            var research = enemy.gameObject.AddComponent<RangeResearchEnemy>();
            research.OwnsCharacter = true;
            research.Configure(target, kind);
            research.SetCombat(true);
            rainEnemies.Add(enemy, research);
            if (boss) RainBossActive = true;
            else rainSpawnCount++;
        }

        bool TryFindRainSpawn(int kind, out Vector3 position, out Quaternion rotation, out CharacterInstance target)
        {
            position = default;
            rotation = Quaternion.identity;
            target = null;
            int count = _botsSpawnPoints.Spawnpoints.Count;
            bool airborne = RangeResearchEnemy.IsAirborneKind(kind);
            bool checkNavigation = !airborne && HasRainNavigation;
            bool queen = kind == RangeResearchEnemy.QueenKind;
            float radius = queen ? 3.990002f :
                kind == RangeResearchEnemy.ParentKind ? RangeResearchEnemy.ParentBodyRadius :
                kind is RangeResearchEnemy.BeetleGuardKind or RangeResearchEnemy.BruiserKind or RangeResearchEnemy.BisonKind ? .7f : .45f;
            float height = queen ? 8.090004f : kind == RangeResearchEnemy.ParentKind ? RangeResearchEnemy.ParentBodyHeight : 2f;
            for (int i = 0; i < count; i++)
            {
                var spawn = _botsSpawnPoints.GetNextSpawnPoint();
                if (!spawn || !Physics.Raycast(spawn.position + Vector3.up * 2f, Vector3.down, out var ground,
                    5f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;

                Vector3 feet = ground.point;
                if (airborne)
                {
                    const float hoverHeight = RangeResearchEnemy.WispHoverHeight;
                    if (Physics.Raycast(feet + Vector3.up * .1f, Vector3.up, hoverHeight + 2f,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;
                    feet += Vector3.up * hoverHeight;
                }
                if (checkNavigation)
                {
                    if (!NavMesh.SamplePosition(feet, out var navPoint, 1.25f, NavMesh.AllAreas)) continue;
                    feet = navPoint.position;
                }
                if (Physics.CheckCapsule(feet + Vector3.up * (radius + .1f),
                    feet + Vector3.up * (height - radius), radius, GameManager.characterLayer,
                    QueryTriggerInteraction.Ignore)) continue;

                var nearest = FindNearestRainTarget(feet);
                if (!nearest || (nearest.transform.position - feet).sqrMagnitude < 4f) continue;
                if (checkNavigation) nearest = FindNearestRainTarget(feet, requireReachable: true);
                if (!nearest) continue;
                position = feet + Vector3.up * .1f;
                rotation = spawn.rotation;
                target = nearest;
                return true;
            }
            return false;
        }

        CharacterInstance FindNearestRainTarget(Vector3 origin, bool requireReachable = false)
        {
            CharacterInstance nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var player in GameManager.Players.Values)
            {
                if (!player || player.BOT || !player.MyCharacter || player.MyCharacter.CurrentHealth <= 0) continue;
                var candidate = player.MyCharacter;
                float distance = (candidate.transform.position - origin).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                if (requireReachable && (
                    !NavMesh.SamplePosition(candidate.transform.position, out var destination, 2f, NavMesh.AllAreas) ||
                    !NavMesh.CalculatePath(origin, destination.position, NavMesh.AllAreas, rainTargetPath) ||
                    rainTargetPath.status != NavMeshPathStatus.PathComplete)) continue;
                nearest = candidate;
                nearestDistance = distance;
            }
            return nearest;
        }

        [Server]
        internal bool TryDropMonsterLog(Vector3 position, int archetype, float? roll = null, Health attacker = null)
        {
            if (!RainMonsterLogs.TryGet(archetype, out var entry) || !CanDropMonsterLog(entry.UnlockableId)) return false;
            bool success;
            if (roll.HasValue) success = roll.Value < entry.DropChance;
            else if (attacker && attacker.TryGetComponent<RangeResearchItems>(out var items))
                success = items.RollWithLuck(entry.DropChance);
            else success = Random.value < entry.DropChance;
            if (!success) return false;
            SpawnMonsterLog(position, entry.UnlockableId);
            return true;
        }

        public override void Server_OnPlayerKilled(Health victim, Health killer)
        {
            if (!rainEnemies.TryGetValue(victim, out var enemy)) return;
            rainEnemies.Remove(victim);
            if (killer) TryDropMonsterLog(victim.transform.position, enemy.Archetype, attacker: killer);
            if (killer == victim && enemy.Archetype is RangeResearchEnemy.JellyfishKind or RangeResearchEnemy.AcidLarvaKind)
            {
                RainKills++;
                return;
            }
            int reward = enemy.Archetype switch
            {
                2 => 100,
                RangeResearchEnemy.QueenKind => 240,
                RangeResearchEnemy.BeetleGuardKind or RangeResearchEnemy.BisonKind => 16,
                RangeResearchEnemy.BruiserKind => 46,
                RangeResearchEnemy.GreaterWispKind => 80,
                RangeResearchEnemy.ImpKind => 11,
                RangeResearchEnemy.VerminKind => 5,
                RangeResearchEnemy.FlyingVerminKind => 6,
                RangeResearchEnemy.HermitCrabKind => 10,
                RangeResearchEnemy.LunarExploderKind => 32,
                RangeResearchEnemy.ParentKind => 40,
                RangeResearchEnemy.LemurianKind or RangeResearchEnemy.WispKind or RangeResearchEnemy.JellyfishKind or
                    RangeResearchEnemy.AcidLarvaKind => 4,
                _ => 10
            };
            int experience = enemy.Archetype switch
            {
                RangeResearchEnemy.QueenKind => 120,
                RangeResearchEnemy.BeetleGuardKind or RangeResearchEnemy.BisonKind => 8,
                RangeResearchEnemy.BruiserKind => 23,
                RangeResearchEnemy.GreaterWispKind => 40,
                RangeResearchEnemy.ImpKind => 5,
                RangeResearchEnemy.VerminKind => 2,
                RangeResearchEnemy.FlyingVerminKind => 3,
                RangeResearchEnemy.HermitCrabKind => 5,
                RangeResearchEnemy.LunarExploderKind => 16,
                RangeResearchEnemy.ParentKind => 20,
                RangeResearchEnemy.LemurianKind or RangeResearchEnemy.WispKind or RangeResearchEnemy.JellyfishKind or
                    RangeResearchEnemy.AcidLarvaKind => 2,
                _ => reward
            };
            Health experienceOwner = killer;
            if (killer && killer.TryGetComponent<RangeDrone>(out var drone) && drone.EquipmentSummoner)
                experienceOwner = drone.EquipmentSummoner;
            foreach (var player in GameManager.Players.Values)
            {
                if (!player || player.BOT) continue;
                player.RainGold += reward;
                if (experienceOwner && player.MyCharacter == experienceOwner)
                    player.GrantRainExperience(experience);
            }
            if (enemy.Archetype == RainBossKind)
            {
                if (enemy.Archetype == RangeResearchEnemy.QueenKind) DropQueenRewards(victim.transform.position);
                RainBossActive = false;
                RainVictory = true;
                foreach (var player in GameManager.Players.Values)
                    if (player && !player.BOT && player.MyCharacter && player.connectionToClient != null)
                        player.TargetRecordRainFirstClear(player.connectionToClient);
            }
            else RainKills++;
        }

        public override void Server_OnPlayerInstanceAdded(PlayerInstance player)
        {
            if (player.BOT)
            {
                AssignPlayerToTeam(player, 1);
                player.SpawnCharacter(_botsSpawnPoints.GetNextSpawnPoint());
            }
            else
            {
                AssignPlayerToTeam(player, 0);
                player.SpawnCharacter(_playersSpawnPoints.GetNextSpawnPoint());
            }
        }

        public override void PlayerSpawnCharacterRequest(PlayerInstance playerInstance)
        {
            playerInstance.SpawnCharacter(playerInstance.BOT ? _botsSpawnPoints.GetNextSpawnPoint() : _playersSpawnPoints.GetNextSpawnPoint());
        }
    }
}
