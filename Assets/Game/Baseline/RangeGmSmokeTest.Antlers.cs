using System.Collections;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeGmSmokeTest
    {
        static IEnumerator VerifyAntlersInventoryHost(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            var owner = player.MyCharacter;
            var snapshot = new RangeResearchItems.PersistentItems(items);
            var motor = owner.GetComponent<CharacterMotor>();
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            var direction = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            int antlers = RangeResearchItems.CatalogIndex("SpeedBoostPickup");
            int syringe = RangeResearchItems.CatalogIndex("Syringe");
            int mocha = RangeResearchItems.CatalogIndex("AttackSpeedAndMoveSpeed");
            int stone = RangeResearchItems.CatalogIndex("HalfSpeedDoubleHealth");
            try
            {
                motor.enabled = false;
                if (networkTransform) networkTransform.syncDirection = SyncDirection.ServerToClient;
                items.Clear();
                owner.transform.position += Vector3.up * 200f;
                bool purchased = VerifyPassiveItemChest(mode, player, "SpeedBoostPickup") && owner.ResearchAntlers == 1;
                items.Grant(syringe); items.Grant(mocha); items.Grant(stone);
                Vector3 pickup = owner.transform.position + Vector3.up + Vector3.right * 8f;
                bool spawned = mode.SpawnAntlerShardAt(owner, pickup);
                Teleport();
                yield return new WaitForSeconds(2f);
                owner.transform.position = pickup + Vector3.right * 5.5f - Vector3.up;
                Teleport();
                Physics.SyncTransforms();
                float attractionDeadline = Time.realtimeSinceStartup + 3f;
                yield return new WaitUntil(() => owner.ResearchAntlerBuffs > 0 || Time.realtimeSinceStartup >= attractionDeadline);
                bool collected = mode.CountAntlerShards(owner.netId) == 0 && owner.ResearchAntlerBuffs == 1;
                yield return new WaitForSeconds(2f);
                items.Grant(antlers);
                pickup = owner.transform.position + Vector3.up;
                for (int i = 0; i < 7; i++)
                {
                    spawned &= mode.SpawnAntlerShardAt(owner, pickup);
                    mode.UpdateRainAntlers();
                }
                bool capped = owner.ResearchAntlers == 2 && owner.ResearchAntlerBuffs == 6;
                bool packed = owner.ResearchSyringes == 1 && owner.ResearchMochas == 1 && owner.ResearchStoneFlux == 1;
                yield return new WaitForSeconds(2f);
                bool removed = items.Remove(antlers) && items.Remove(antlers) && owner.ResearchAntlers == 0 && owner.ResearchAntlerBuffs == 6;
                yield return new WaitForSeconds(10.3f);
                bool expired = owner.ResearchAntlerBuffs == 0;
                items.Clear();
                yield return new WaitForSeconds(2f);
                bool cleared = owner.ResearchSyringes == 0 && owner.ResearchMochas == 0 && owner.ResearchStoneFlux == 0;
                if (purchased && spawned && collected && capped && packed && removed && expired && cleared)
                    Debug.Log("RAIN_COOP_ANTLERS_INVENTORY_CHECKED");
                else Debug.LogError($"RAIN_COOP_ANTLERS_INVENTORY purchase={purchased} spawn={spawned} collect={collected} cap={capped} packed={packed} remove={removed} expiry={expired} clear={cleared}");
            }
            finally
            {
                if (owner)
                {
                    mode.ClearAntlerShards(owner.netId);
                    if (items) { items.Clear(); snapshot.Restore(items); }
                    owner.transform.position = originalPosition;
                    Teleport();
                    if (networkTransform) networkTransform.syncDirection = direction;
                    if (motor) motor.enabled = motorEnabled;
                    if (player && items) player.RecordRainChestItems(items);
                }
            }

            void Teleport()
            {
                if (!networkTransform) return;
                networkTransform.RpcTeleport(owner.transform.position);
                networkTransform.ResetState();
            }
        }

        static IEnumerator VerifyAntlersInventoryClient(CharacterInstance owner)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            float deadline = Time.realtimeSinceStartup + 12f;
            yield return new WaitUntil(() => !owner || (owner.ResearchAntlers == 1 && mode.CountAntlerShards(owner.netId) == 1) || Time.realtimeSinceStartup >= deadline);
            yield return new WaitForSeconds(.3f);
            Vector3 initialPosition = default;
            bool world = owner && mode.TryGetAntlerShardPosition(owner.netId, out initialPosition) && HasWorldMarker(initialPosition);
            bool packed = owner && owner.ResearchSyringes == 1 && owner.ResearchMochas == 1 && owner.ResearchStoneFlux == 1;
            var expectedIcon = Resources.Load<Sprite>("RainImported/LogIcons/texElusiveAntlersIcon");
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            var slot = overlay ? overlay.Find("Combat inventory/Inventory item 52") : null;
            bool icon = false;
            if (slot && expectedIcon)
                foreach (var image in slot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (image.sprite == expectedIcon) icon = true;
            deadline = Time.realtimeSinceStartup + 6f;
            bool worldMoved = false;
            while (owner && owner.ResearchAntlerBuffs == 0 && Time.realtimeSinceStartup < deadline)
            {
                if (mode.TryGetAntlerShardPosition(owner.netId, out var current))
                    worldMoved |= Vector3.Distance(current, initialPosition) > .1f && HasWorldMarker(current);
                yield return null;
            }
            bool collected = owner && owner.ResearchAntlerBuffs == 1 && mode.CountAntlerShards(owner.netId) == 0;
            bool consumedVisual = owner && HasAntlerConsumedVisual(owner.transform);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => !owner || owner.ResearchAntlerBuffs == 6 || Time.realtimeSinceStartup >= deadline);
            bool capped = owner && owner.ResearchAntlers == 2 && Mathf.Approximately(owner.ResearchAntlerMoveBonus, .72f);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => !owner || owner.ResearchAntlers == 0 || Time.realtimeSinceStartup >= deadline);
            bool removed = owner && owner.ResearchAntlerBuffs == 6;
            deadline = Time.realtimeSinceStartup + 15f;
            yield return new WaitUntil(() => !owner || owner.ResearchAntlerBuffs == 0 || Time.realtimeSinceStartup >= deadline);
            bool expired = owner && owner.ResearchAntlerBuffs == 0;
            deadline = Time.realtimeSinceStartup + 4f;
            yield return new WaitUntil(() => !owner || (owner.ResearchSyringes == 0 && owner.ResearchMochas == 0 && owner.ResearchStoneFlux == 0) || Time.realtimeSinceStartup >= deadline);
            bool cleared = owner && owner.ResearchSyringes == 0 && owner.ResearchMochas == 0 && owner.ResearchStoneFlux == 0;
            if (world && worldMoved && icon && packed && collected && consumedVisual && capped && removed && expired && cleared)
                Debug.Log("RAIN_CLIENT_ANTLERS_INVENTORY_CHECKED");
            else Debug.LogError($"RAIN_CLIENT_ANTLERS_INVENTORY world={world} moved={worldMoved} icon={icon} packed={packed} collect={collected} consumedVisual={consumedVisual} cap={capped} remove={removed} expiry={expired} clear={cleared}");

            bool HasWorldMarker(Vector3 position)
            {
                foreach (var root in mode.GetComponentsInChildren<Transform>())
                {
                    if (root.name != "Rain antler shard" || Vector3.Distance(root.position, position) >= .01f) continue;
                    var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>();
                    if (renderers.Length != 3 || root.GetComponentsInChildren<RangeAntlerParticleFade>().Length != 3 ||
                        !root.GetComponentInChildren<Light>()) continue;
                    bool materials = true;
                    foreach (var renderer in renderers)
                        materials &= renderer.sharedMaterial && renderer.sharedMaterial.shader.name ==
                            (renderer.name == "WispBase" ? "RainItems/AntlersIntersectionRemap" : "RainActors/ParentCloudRemap");
                    if (materials) return true;
                }
                return false;
            }
        }

        static IEnumerator VerifyElusiveAntlers(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            if (!mode) { Debug.LogError("RANGE_SMOKE_ANTLERS_MODE"); yield break; }
            int antlers = RangeResearchItems.CatalogIndex("SpeedBoostPickup");
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            int health = owner.CurrentHealth;
            CharacterInstance recipient = null;
            try
            {
                items.Clear();
                motor.enabled = false;
                owner.transform.position += Vector3.up * 200f;
                items.Grant(antlers);
                bool inventory = antlers >= 0 && owner.ResearchAntlers == 1 &&
                    Mathf.Approximately(AntlerInterval(1), 10f) && Mathf.Approximately(AntlerInterval(2), 8f);
                Vector3 pickupPosition = owner.transform.position + Vector3.right * 8f + Vector3.up;
                recipient = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    pickupPosition - Vector3.up, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in recipient.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                recipient.Team = owner.Team == 0 ? 1 : 0;
                recipient.MaxHealth = recipient.CurrentHealth = 1000;
                recipient.RangeInvulnerable = true;
                NetworkServer.Spawn(recipient.gameObject);
                recipient.GetComponent<CharacterMotor>().enabled = false;
                var ai = recipient.GetComponent<BotAI>();
                if (ai) { ai.enabled = false; ai.StopAllCoroutines(); }
                yield return null;
                bool spawned = mode.SpawnAntlerShardAt(owner, pickupPosition);
                Physics.SyncTransforms();
                mode.UpdateRainAntlers();
                bool enemyRejected = mode.CountAntlerShards(owner.netId) == 1 && recipient.ResearchAntlerBuffs == 0;
                enemyRejected &= recipient.GetComponentsInChildren<RangeAntlerConsumedEffect>().Length == 0;
                recipient.Team = owner.Team;
                recipient.CurrentHealth = 0;
                mode.UpdateRainAntlers();
                bool deadRejected = mode.CountAntlerShards(owner.netId) == 1 && recipient.ResearchAntlerBuffs == 0;
                deadRejected &= recipient.GetComponentsInChildren<RangeAntlerConsumedEffect>().Length == 0;
                recipient.CurrentHealth = 1000;
                mode.UpdateRainAntlers();
                bool friendlyCollected = mode.CountAntlerShards(owner.netId) == 0 &&
                    recipient.ResearchAntlers == 0 && recipient.ResearchAntlerBuffs == 1;
                mode.UpdateRainAntlers();
                bool single = recipient.ResearchAntlerBuffs == 1;
                // Mirror's host LocalConnectionToClient enqueues RPCs until the next network update.
                // Also allow Texture's original .05-second emission delay before inspecting all four systems.
                yield return new WaitForSeconds(.1f);
                single &= recipient.GetComponentsInChildren<RangeAntlerConsumedEffect>().Length == 1;
                yield return VerifyAntlerConsumedLifecycle(recipient);
                yield return VerifyAntlerConsumedPool(mode, recipient.transform);
                var recipientItems = recipient.GetComponent<RangeResearchItems>();
                for (int i = 0; i < 5; i++) recipientItems.TryCollectAntlerBuff();
                bool allyCap = recipient.ResearchAntlerBuffs == 3;
                recipientItems.Clear();
                Vector3 far = pickupPosition + Vector3.forward * 30f;
                bool worldCap = true;
                for (int i = 0; i < 10; i++) worldCap &= mode.SpawnAntlerShardAt(owner, far + Vector3.right * i);
                worldCap &= mode.CountAntlerShards(owner.netId) == 10 && !mode.SpawnAntlerShardAt(owner, far);
                mode.ClearAntlerShards(owner.netId);
                items.Grant(antlers);
                for (int i = 0; i < 8; i++) items.TryCollectAntlerBuff();
                bool cap = owner.ResearchAntlers == 2 && owner.ResearchAntlerBuffs == 6 &&
                    Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.72f);
                var snapshot = new RangeResearchItems.PersistentItems(items);
                items.Clear();
                snapshot.Restore(items);
                bool restored = owner.ResearchAntlers == 2 && owner.ResearchAntlerBuffs == 0;
                items.TryCollectAntlerBuff();
                yield return new WaitForSeconds(1f);
                items.TryCollectAntlerBuff();
                bool refreshed = owner.ResearchAntlerBuffs == 2;
                bool removed = items.Remove(antlers) && items.Remove(antlers) &&
                    owner.ResearchAntlers == 0 && owner.ResearchAntlerBuffs == 2;
                yield return new WaitForSeconds(11.1f);
                bool stillActive = owner.ResearchAntlerBuffs == 2;
                yield return new WaitForSeconds(1.1f);
                bool expired = owner.ResearchAntlerBuffs == 0 &&
                    Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed);
                items.TryCollectAntlerBuff();
                owner.CurrentHealth = 0;
                yield return new WaitForFixedUpdate();
                bool deathCleared = owner.ResearchAntlerBuffs == 0;
                if (inventory && spawned && enemyRejected && deadRejected && friendlyCollected && single &&
                    allyCap && worldCap && cap && restored && refreshed && removed && stillActive && expired && deathCleared)
                    Debug.Log("RANGE_SMOKE_ELUSIVE_ANTLERS_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_ELUSIVE_ANTLERS inventory={inventory} spawn={spawned} enemy={enemyRejected} dead={deadRejected} friendly={friendlyCollected} single={single} allyCap={allyCap} worldCap={worldCap} cap={cap} restore={restored} refresh={refreshed} remove={removed} active={stillActive} expired={expired} death={deathCleared}");
            }
            finally
            {
                owner.CurrentHealth = health;
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                if (recipient) NetworkServer.Destroy(recipient.gameObject);
                owner.transform.position = originalPosition;
                motor.enabled = motorEnabled;
            }

            static float AntlerInterval(int stacks) => RangeResearchItems.AntlerSpawnInterval(stacks);
        }

        static bool HasAntlerConsumedVisual(Transform recipient)
        {
            var effect = recipient.GetComponentInChildren<RangeAntlerConsumedEffect>();
            if (!effect || effect.transform.parent != recipient) return false;
            var systems = effect.GetComponentsInChildren<ParticleSystem>();
            var renderers = effect.GetComponentsInChildren<ParticleSystemRenderer>();
            if (systems.Length != 4 || renderers.Length != 4) return false;
            int meshes = 0;
            foreach (var renderer in renderers)
            {
                if (!renderer.sharedMaterial || renderer.sharedMaterial.shader.name != "RainItems/AntlersConsumedCloudRemap") return false;
                if (renderer.renderMode == ParticleSystemRenderMode.Mesh)
                {
                    if (!renderer.mesh || renderer.mesh.vertexCount != 66) return false;
                    meshes++;
                }
            }
            return meshes == 2;
        }

        static IEnumerator VerifyAntlerConsumedLifecycle(CharacterInstance recipient)
        {
            var effect = recipient.GetComponentInChildren<RangeAntlerConsumedEffect>();
            bool bound = HasAntlerConsumedVisual(recipient.transform);
            bool paused = false, follows = false, particles = false, automatic = false, growOwnsLifetime = false;
            float pauseDelta = float.NaN;
            float scale = Time.timeScale;
            Vector3 original = recipient.transform.position;
            RangeAntlerConsumedEffect explicitEffect = null;
            try
            {
                yield return null;
                if (effect)
                {
                    particles = true;
                    foreach (var system in effect.GetComponentsInChildren<ParticleSystem>()) particles &= system.particleCount > 0;
                    var grow = effect.transform.Find("Grow").GetComponent<ParticleSystem>();
                    Time.timeScale = 0f;
                    // Let the native particle step already scheduled this frame finish before taking the baseline.
                    yield return null;
                    float age = grow.time;
                    Vector3 before = effect.transform.position;
                    recipient.transform.position += Vector3.right * 2f;
                    yield return new WaitForSecondsRealtime(1.1f);
                    follows = effect && Vector3.Distance(effect.transform.position, before + Vector3.right * 2f) < .001f;
                    pauseDelta = grow.time - age;
                    paused = effect && !grow.main.useUnscaledTime && Time.timeScale == 0f && Mathf.Abs(pauseDelta) < .001f;
                    Time.timeScale = scale;
                    recipient.transform.position = original;
                    yield return new WaitForSeconds(2f);
                    automatic = effect && !effect.gameObject.activeSelf;
                }
                // Force only the tracked system to finish while another system is still alive.
                explicitEffect = RangeAntlerConsumedEffect.Create(null, recipient.transform, original);
                yield return null;
                var tracked = explicitEffect.transform.Find("Grow").GetComponent<ParticleSystem>();
                var lingering = explicitEffect.transform.Find("LingeringSpark").GetComponent<ParticleSystem>();
                lingering.Play();
                tracked.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                bool otherAlive = lingering.IsAlive();
                yield return null;
                yield return null; // Destroy is deferred until the end of the Update frame.
                growOwnsLifetime = otherAlive && !explicitEffect;
                if (bound && particles && follows && paused && automatic && growOwnsLifetime)
                    Debug.Log("RANGE_SMOKE_ANTLERS_CONSUMED_LIFECYCLE_CHECKED server_pickup_once=True trackedGrow=True pause=True");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_CONSUMED_LIFECYCLE bound={bound} particles={particles} follow={follows} pause={paused} pauseDelta={pauseDelta:R} auto={automatic} grow={growOwnsLifetime}");
            }
            finally
            {
                Time.timeScale = scale;
                if (recipient) recipient.transform.position = original;
                if (effect) effect.Release();
                if (explicitEffect) explicitEffect.Release();
            }
        }

        static IEnumerator VerifyAntlerConsumedPool(Gamemodes.RangeGamemode mode, Transform recipient)
        {
            mode.ClearAntlerConsumedEffects();
            var other = new GameObject("Antler pool second recipient");
            other.transform.position = recipient.position + Vector3.forward * 4f;
            var held = new RangeAntlerConsumedEffect[21];
            bool capacity = false, reused = false, reset = false, destroyedRecipient = false, cleanup = false;
            try
            {
                for (int i = 0; i < held.Length; i++) held[i] = mode.PlayAntlerConsumedEffect(recipient, recipient.position);
                capacity = mode.AntlerConsumedActiveCount == 21 && mode.AntlerConsumedIdleCount == 0;
                yield return new WaitForSeconds(.12f);
                foreach (var effect in held) effect.Release();
                capacity &= mode.AntlerConsumedActiveCount == 0 && mode.AntlerConsumedIdleCount == 20;
                held[0].Release();
                capacity &= mode.AntlerConsumedIdleCount == 20; // Duplicate return is harmless.
                var next = mode.PlayAntlerConsumedEffect(other.transform, recipient.position);
                reused = next == held[19] && next.transform.parent == other.transform &&
                    Vector3.Distance(next.transform.position, other.transform.position) < .001f;
                var grow = next.transform.Find("Grow").GetComponent<ParticleSystem>();
                reset = grow.time < .05f;
                yield return new WaitForSeconds(.12f);
                foreach (var system in next.GetComponentsInChildren<ParticleSystem>()) reset &= system.particleCount > 0;
                Destroy(other);
                yield return null;
                yield return null;
                destroyedRecipient = !next && mode.AntlerConsumedActiveCount == 0 && mode.AntlerConsumedIdleCount == 19;
                mode.PlayAntlerConsumedEffect(null, recipient.position); // Unparented fallback also belongs to the mode.
                mode.ClearAntlerConsumedEffects();
                yield return null;
                cleanup = mode.AntlerConsumedActiveCount == 0 && mode.AntlerConsumedIdleCount == 0;
                foreach (var effect in held) cleanup &= !effect;
                if (capacity && reused && reset && destroyedRecipient && cleanup)
                    Debug.Log("RANGE_SMOKE_ANTLERS_CONSUMED_POOL_CHECKED initial=1 idleCapacity=20 active=21 reuse=True recipientDestroy=True cleanup=True");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_CONSUMED_POOL capacity={capacity} reuse={reused} reset={reset} recipientDestroy={destroyedRecipient} cleanup={cleanup}");
            }
            finally
            {
                mode.ClearAntlerConsumedEffects();
                if (other) Destroy(other);
            }
        }

        static IEnumerator VerifyAntlerNaturalGeneration(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            var motor = owner.GetComponent<CharacterMotor>();
            Vector3 originalPosition = owner.transform.position;
            bool motorEnabled = motor.enabled;
            GameObject floor = null;
            Unity.AI.Navigation.NavMeshSurface navigation = null;
            try
            {
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                motor.enabled = false;
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Antler generation validation floor";
                floor.transform.position = originalPosition + Vector3.up * 500f;
                floor.transform.localScale = new Vector3(120f, 1f, 120f);
                navigation = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                navigation.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
                navigation.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                navigation.BuildNavMesh();
                owner.transform.position = floor.transform.position + Vector3.up * .55f;
                Physics.SyncTransforms();
                int antlers = RangeResearchItems.CatalogIndex("SpeedBoostPickup");
                items.Grant(antlers);
                yield return new WaitForSeconds(2.7f);
                bool firstDelay = mode.CountAntlerShards(owner.netId) == 0;
                yield return new WaitForSeconds(.5f);
                bool firstSpawn = mode.CountAntlerShards(owner.netId) == 1 &&
                    mode.TryGetAntlerShardPosition(owner.netId, out var position) &&
                    Vector2.Distance(new Vector2(position.x, position.z),
                        new Vector2(owner.transform.position.x, owner.transform.position.z)) >= 13f &&
                    Mathf.Abs(position.y - (floor.transform.position.y + 1.35f)) < .2f;
                yield return new WaitForSeconds(9.5f);
                bool intervalDelay = mode.CountAntlerShards(owner.netId) == 1;
                yield return new WaitForSeconds(.6f);
                bool secondSpawn = mode.CountAntlerShards(owner.netId) == 2;
                Vector3 far = owner.transform.position + Vector3.up * 10f + Vector3.right * 40f;
                bool filled = true;
                for (int i = mode.CountAntlerShards(owner.netId); i < 10; i++)
                    filled &= mode.SpawnAntlerShardAt(owner, far + Vector3.forward * i);
                float pausedAt = items.AntlerSpawnRemaining;
                yield return new WaitForSeconds(1f);
                bool paused = mode.CountAntlerShards(owner.netId) == 10 &&
                    Mathf.Abs(items.AntlerSpawnRemaining - pausedAt) < .001f;
                mode.ClearAntlerShards(owner.netId);
                yield return new WaitForSeconds(.5f);
                bool resumed = items.AntlerSpawnRemaining < pausedAt - .3f && mode.CountAntlerShards(owner.netId) == 0;
                yield return new WaitForSeconds(Mathf.Max(0f, items.AntlerSpawnRemaining) + .2f);
                bool respawned = mode.CountAntlerShards(owner.netId) == 1;
                items.Remove(antlers);
                bool retained = mode.CountAntlerShards(owner.netId) == 1;
                mode.ClearAntlerShards(owner.netId);
                yield return new WaitForSeconds(3.2f);
                bool stopped = mode.CountAntlerShards(owner.netId) == 0;
                if (firstDelay && firstSpawn && intervalDelay && secondSpawn && filled && paused && resumed && respawned && retained && stopped)
                    Debug.Log("RANGE_SMOKE_ANTLERS_NATURAL_GENERATION_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_NATURAL_GENERATION firstDelay={firstDelay} firstSpawn={firstSpawn} interval={intervalDelay} second={secondSpawn} fill={filled} pause={paused} resume={resumed} respawn={respawned} retain={retained} stop={stopped}");
            }
            finally
            {
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                owner.transform.position = originalPosition;
                motor.enabled = motorEnabled;
                if (navigation) navigation.RemoveData();
                if (floor) Destroy(floor);
            }
        }

        static IEnumerator VerifyAntlerAttraction(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            CharacterInstance recipient = null;
            try
            {
                items.Clear();
                motor.enabled = false;
                owner.transform.position += Vector3.up * 200f;
                items.Grant(RangeResearchItems.CatalogIndex("SpeedBoostPickup"));
                Vector3 shardPosition = owner.transform.position + Vector3.up + Vector3.right * 8f;
                recipient = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    shardPosition + Vector3.right * 4f - Vector3.up, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in recipient.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                recipient.Team = owner.Team == 0 ? 1 : 0;
                recipient.MaxHealth = recipient.CurrentHealth = 1000;
                recipient.RangeInvulnerable = true;
                NetworkServer.Spawn(recipient.gameObject);
                recipient.GetComponent<CharacterMotor>().enabled = false;
                if (recipient.TryGetComponent<BotAI>(out var ai)) { ai.enabled = false; ai.StopAllCoroutines(); }
                bool spawned = mode.SpawnAntlerShardAt(owner, shardPosition) &&
                    mode.TryGetAntlerMotion(owner.netId, out _);
                mode.TryGetAntlerMotion(owner.netId, out var motion);
                var collision = motion ? motion.GetComponent<SphereCollider>() : null;
                var material = collision ? collision.sharedMaterial : null;
                bool sourceMaterial = material && Mathf.Approximately(material.dynamicFriction, 20f) &&
                    Mathf.Approximately(material.staticFriction, 1f) && Mathf.Approximately(material.bounciness, .2f) &&
                    material.frictionCombine == PhysicsMaterialCombine.Average && material.bounceCombine == PhysicsMaterialCombine.Average;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.3f);
                bool enemyIgnored = motion && !motion.HasTarget &&
                    Vector3.Distance(motion.Position, shardPosition) < .02f;
                recipient.transform.position += Vector3.right * 8f;
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
                recipient.Team = owner.Team;
                recipient.transform.position -= Vector3.right * 8f;
                Physics.SyncTransforms();
                bool targeted = false, moved = false, boundedSpeed = true;
                float deadline = Time.realtimeSinceStartup + 3f;
                while (recipient.ResearchAntlerBuffs == 0 && Time.realtimeSinceStartup < deadline)
                {
                    if (motion)
                    {
                        targeted |= motion.HasTarget;
                        moved |= Vector3.Distance(motion.Position, shardPosition) > .1f;
                        boundedSpeed &= motion.Velocity.magnitude <= 40.01f;
                    }
                    yield return null;
                }
                bool collected = recipient.CurrentHealth == 1000 && recipient.ResearchAntlers == 0 &&
                    recipient.ResearchAntlerBuffs == 1 && mode.CountAntlerShards(owner.netId) == 0;
                yield return null;
                bool released = !motion;
                if (spawned && sourceMaterial && enemyIgnored && targeted && moved && boundedSpeed && collected && released)
                    Debug.Log("RANGE_SMOKE_ANTLERS_ATTRACTION_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_ATTRACTION spawn={spawned} material={sourceMaterial} enemy={enemyIgnored} target={targeted} moved={moved} speed={boundedSpeed} collect={collected} release={released}");
            }
            finally
            {
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                if (recipient) NetworkServer.Destroy(recipient.gameObject);
                owner.transform.position = originalPosition;
                motor.enabled = motorEnabled;
            }
        }

        static IEnumerator VerifyAntlerParticleLifecycle()
        {
            GameObject root = null;
            float originalTimeScale = Time.timeScale;
            try
            {
                root = RangeAntlerParticleFactory.Create(Vector3.up * 2000f);
                var fades = root.GetComponentsInChildren<RangeAntlerParticleFade>();
                var lamp = root.GetComponentInChildren<Light>();
                bool initial = fades.Length == 3 && lamp && Mathf.Abs(lamp.intensity) < .00001f;
                yield return new WaitForSeconds(.25f);
                var renderers = root.GetComponentsInChildren<ParticleSystemRenderer>();
                bool materials = renderers.Length == 3;
                foreach (var renderer in renderers)
                    materials &= renderer.sharedMaterial && renderer.sharedMaterial.shader.name ==
                        (renderer.name == "WispBase" ? "RainItems/AntlersIntersectionRemap" : "RainActors/ParentCloudRemap");
                foreach (var system in root.GetComponentsInChildren<ParticleSystem>()) materials &= system.particleCount > 0;
                var block = new MaterialPropertyBlock();
                float alphaSum = 0f;
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(block);
                    alphaSum += block.GetFloat("_ExternalAlpha");
                }
                bool advancing = alphaSum > .2f && alphaSum < 1.5f && lamp && lamp.intensity > 0f && lamp.intensity < 5f;
                float pausedIntensity = lamp ? lamp.intensity : -1f;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.15f);
                bool paused = lamp && Mathf.Abs(lamp.intensity - pausedIntensity) < .00001f;
                Time.timeScale = originalTimeScale;
                yield return new WaitForSeconds(.85f);
                bool finished = lamp && Mathf.Abs(lamp.intensity - 5f) < .00001f;
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(block);
                    if (renderer.name == "WispFlame" || renderer.name == "WispBase")
                        finished &= Mathf.Abs(block.GetFloat("_ExternalAlpha") - 1f) < .00001f;
                }
                root.SetActive(false);
                bool restored = lamp && Mathf.Abs(lamp.intensity - 5f) < .00001f;
                root.SetActive(true);
                bool reset = lamp && Mathf.Abs(lamp.intensity) < .00001f;
                yield return new WaitForSeconds(.1f);
                bool alphaRetained = true;
                foreach (var renderer in renderers)
                {
                    renderer.GetPropertyBlock(block);
                    if (renderer.name == "WispFlame" || renderer.name == "WispBase")
                        alphaRetained &= Mathf.Abs(block.GetFloat("_ExternalAlpha") - 1f) < .00001f;
                }
                bool lightRestarted = lamp && lamp.intensity > 0f && lamp.intensity < 5f;
                if (initial && materials && advancing && paused && finished && restored && reset && alphaRetained && lightRestarted)
                    Debug.Log("RANGE_SMOKE_ANTLERS_PARTICLE_LIFECYCLE_CHECKED original_material_ports=True rendered_fidelity_pending=True");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_PARTICLE_LIFECYCLE initial={initial} materials={materials} advancing={advancing} pause={paused} finish={finished} restore={restored} reset={reset} alpha={alphaRetained} light={lightRestarted}");
            }
            finally
            {
                Time.timeScale = originalTimeScale;
                if (root) Destroy(root);
            }
        }

        static IEnumerator VerifyAntlerDistanceLifetime(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            try
            {
                items.Clear();
                motor.enabled = false;
                owner.transform.position += Vector3.up * 200f;
                items.Grant(RangeResearchItems.CatalogIndex("SpeedBoostPickup"));
                bool spawned = mode.SpawnAntlerShardAt(owner, owner.transform.position + Vector3.right * 119.9f) &&
                    mode.SpawnAntlerShardAt(owner, owner.transform.position + Vector3.right * 120.1f);
                items.Remove(RangeResearchItems.CatalogIndex("SpeedBoostPickup"));
                bool preservedAfterRemoval = mode.CountAntlerShards(owner.netId) == 2;
                yield return new WaitForSeconds(59.5f);
                bool noEarlyCull = mode.CountAntlerShards(owner.netId) == 2;
                yield return new WaitForSeconds(.8f);
                bool distanceBoundary = mode.CountAntlerShards(owner.netId) == 1;
                owner.transform.position += Vector3.left * .4f;
                yield return new WaitForSeconds(.2f);
                bool culledWithoutReplacement = mode.CountAntlerShards(owner.netId) == 0;
                if (spawned && preservedAfterRemoval && noEarlyCull && distanceBoundary && culledWithoutReplacement)
                    Debug.Log("RANGE_SMOKE_ANTLERS_DISTANCE_LIFETIME_CHECKED actualWaitSeconds=60.5");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_DISTANCE_LIFETIME spawn={spawned} retained={preservedAfterRemoval} early={noEarlyCull} boundary={distanceBoundary} cull={culledWithoutReplacement}");
            }
            finally
            {
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                owner.transform.position = originalPosition;
                motor.enabled = motorEnabled;
            }
        }

        static IEnumerator VerifyAntlerImmediateReplacement(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            var motor = owner.GetComponent<CharacterMotor>();
            Vector3 originalPosition = owner.transform.position;
            bool motorEnabled = motor.enabled;
            GameObject floor = null;
            Unity.AI.Navigation.NavMeshSurface navigation = null;
            try
            {
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                motor.enabled = false;
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.name = "Antler immediate replacement validation floor";
                floor.transform.position = originalPosition + Vector3.up * 500f;
                floor.transform.localScale = new Vector3(120f, 1f, 120f);
                navigation = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                navigation.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
                navigation.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                navigation.BuildNavMesh();
                owner.transform.position = floor.transform.position + Vector3.up * .55f;
                Physics.SyncTransforms();
                items.Grant(RangeResearchItems.CatalogIndex("SpeedBoostPickup"));
                bool filled = true;
                // Keep the normal generation timer paused at the source cap. Only distance
                // destruction can replenish these shards before the initial three seconds elapse.
                for (int i = 0; i < 10; i++)
                    filled &= mode.SpawnAntlerShardAt(owner, owner.transform.position + Vector3.right * (150f + 2f * i));
                float pausedAt = items.AntlerSpawnRemaining;
                yield return new WaitForSeconds(59.5f);
                bool noEarlyCull = mode.CountAntlerShards(owner.netId) == 10 &&
                    Mathf.Abs(items.AntlerSpawnRemaining - pausedAt) < .001f;
                yield return new WaitForSeconds(.8f);
                bool replaced = mode.CountAntlerShards(owner.netId) == 10 &&
                    mode.AllAntlerShardsWithin(owner.netId, owner.transform.position, 70f) &&
                    Mathf.Abs(items.AntlerSpawnRemaining - pausedAt) < .001f;
                if (filled && noEarlyCull && replaced)
                    Debug.Log("RANGE_SMOKE_ANTLERS_IMMEDIATE_REPLACEMENT_CHECKED actualWaitSeconds=60.3 normalGenerationTimerPaused=True");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_IMMEDIATE_REPLACEMENT fill={filled} early={noEarlyCull} replacement={replaced} count={mode.CountAntlerShards(owner.netId)} timer={items.AntlerSpawnRemaining}/{pausedAt}");
            }
            finally
            {
                items.Clear();
                mode.ClearAntlerShards(owner.netId);
                owner.transform.position = originalPosition;
                motor.enabled = motorEnabled;
                if (navigation) navigation.RemoveData();
                if (floor) Destroy(floor);
            }
        }
    }
}
