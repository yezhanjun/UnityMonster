using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.AI;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode
    {
        internal struct AntlerShard
        {
            public uint OwnerId;
            public int Team;
            public Vector3 Position;
            public float SpawnedAt;
        }

        readonly SyncDictionary<uint, AntlerShard> antlerShards = new SyncDictionary<uint, AntlerShard>();
        readonly Dictionary<uint, GameObject> antlerVisuals = new Dictionary<uint, GameObject>();
        readonly List<uint> antlerRemovals = new List<uint>();
        readonly List<uint> antlerReplacements = new List<uint>();
        readonly Dictionary<uint, RangeAntlerShardMotion> antlerMotions = new Dictionary<uint, RangeAntlerShardMotion>();
        readonly List<KeyValuePair<uint, AntlerShard>> antlerPositionUpdates = new List<KeyValuePair<uint, AntlerShard>>();
        readonly List<uint> antlerIterationIds = new List<uint>(10);
        bool updatingAntlers;
        // Match-scoped equivalent of GlobalEventManager.onCharacterPickupItem.
        internal event System.Action<RangePickupEvent> ServerCharacterPickupItem;
        Collider[] antlerContacts = new Collider[8];
        uint nextAntlerShard;
        float nextAntlerPositionSync;

        internal bool TryGetAntlerMotion(uint ownerId, out RangeAntlerShardMotion motion)
        {
            foreach (var shard in antlerShards)
                if (shard.Value.OwnerId == ownerId) return antlerMotions.TryGetValue(shard.Key, out motion);
            motion = null;
            return false;
        }

        internal bool TryGetAntlerShardPosition(uint ownerId, out Vector3 position)
        {
            foreach (var shard in antlerShards.Values)
                if (shard.OwnerId == ownerId) { position = shard.Position; return true; }
            position = default;
            return false;
        }

        internal int CountAntlerShards(uint ownerId)
        {
            int count = 0;
            foreach (var shard in antlerShards.Values) if (shard.OwnerId == ownerId) count++;
            return count;
        }

        internal bool AllAntlerShardsWithin(uint ownerId, Vector3 origin, float distance)
        {
            foreach (var shard in antlerShards.Values)
                if (shard.OwnerId == ownerId && (shard.Position - origin).sqrMagnitude > distance * distance) return false;
            return true;
        }

        internal bool TrySpawnAntlerShard(CharacterInstance owner, Vector3 velocity)
        {
            if (!isServer || !owner || owner.CurrentHealth <= 0 || owner.ResearchAntlers <= 0 ||
                CountAntlerShards(owner.netId) >= 10 ||
                !owner.TryGetComponent<RangeResearchItems>(out var behavior) || !behavior.isActiveAndEnabled) return false;
            Vector3 aim = Quaternion.Euler(0f, owner.lookInput.y, 0f) * Vector3.forward;
            Vector3 heading = Quaternion.Euler(0f, Random.Range(-45f, 45f), 0f) * aim;
            Vector3 horizontalVelocity = new Vector3(velocity.x, 0f, velocity.z);
            if (horizontalVelocity.sqrMagnitude > .0001f)
                heading = Vector3.RotateTowards(heading, horizontalVelocity.normalized, Mathf.PI / 12f, 0f);
            float speedScale = Mathf.Lerp(1f, 2f, Mathf.InverseLerp(0f, 50f, velocity.magnitude));
            Vector3 candidate = owner.transform.position + heading * (Random.Range(15f, 30f) * speedScale);
            if (!NavMesh.SamplePosition(candidate, out var hit, 8f, NavMesh.AllAreas) ||
                (hit.position - owner.transform.position).sqrMagnitude > 70f * 70f ||
                Physics.CheckSphere(hit.position + Vector3.up * .95f, .7f,
                    GameManager.environmentLayer | GameManager.characterLayer, QueryTriggerInteraction.Ignore)) return false;
            return SpawnAntlerShardAt(owner, hit.position + Vector3.up * .85f);
        }

        // Kept server-only for deterministic world/pickup validation; no client position Command exists.
        internal bool SpawnAntlerShardAt(CharacterInstance owner, Vector3 position)
        {
            if (!isServer || !owner || owner.CurrentHealth <= 0 || owner.ResearchAntlers <= 0 ||
                CountAntlerShards(owner.netId) >= 10 ||
                !owner.TryGetComponent<RangeResearchItems>(out var behavior) || !behavior.isActiveAndEnabled) return false;
            uint id = ++nextAntlerShard;
            antlerMotions.Add(id, RangeAntlerShardMotion.Create(transform, position, owner.Team));
            antlerShards.Add(id, new AntlerShard
            {
                OwnerId = owner.netId, Team = owner.Team, Position = position, SpawnedAt = Time.time
            });
            return true;
        }

        internal void UpdateRainAntlers()
        {
            if (!isServer || updatingAntlers) return;
            updatingAntlers = true;
            try { UpdateRainAntlersCore(); }
            finally
            {
                antlerIterationIds.Clear();
                antlerPositionUpdates.Clear();
                antlerRemovals.Clear();
                antlerReplacements.Clear();
                updatingAntlers = false;
            }
        }

        void UpdateRainAntlersCore()
        {
            bool syncPositions = Time.time >= nextAntlerPositionSync;
            if (syncPositions) nextAntlerPositionSync = Time.time + .06f;
            foreach (uint id in antlerShards.Keys) antlerIterationIds.Add(id);
            foreach (uint id in antlerIterationIds)
            {
                if (!antlerShards.TryGetValue(id, out var shard)) continue;
                if (antlerMotions.TryGetValue(id, out var motion) && motion)
                {
                    Vector3 previousPosition = shard.Position;
                    shard.Position = motion.Position;
                    if (syncPositions && (shard.Position - previousPosition).sqrMagnitude > .000001f)
                        antlerPositionUpdates.Add(new KeyValuePair<uint, AntlerShard>(id, shard));
                }
                if (Time.time >= shard.SpawnedAt + 2000f)
                {
                    antlerRemovals.Add(id);
                    continue;
                }
                CharacterInstance owner = null;
                if (NetworkServer.spawned.TryGetValue(shard.OwnerId, out var identity) && identity)
                    owner = identity.GetComponent<CharacterInstance>();
                if (owner && Time.time >= shard.SpawnedAt + 60f &&
                    (owner.transform.position - shard.Position).sqrMagnitude > 120f * 120f)
                {
                    antlerRemovals.Add(id);
                    antlerReplacements.Add(shard.OwnerId);
                    continue;
                }
                int count;
                while ((count = Physics.OverlapSphereNonAlloc(shard.Position, .55f, antlerContacts,
                    GameManager.characterLayer, QueryTriggerInteraction.Collide)) == antlerContacts.Length)
                    System.Array.Resize(ref antlerContacts, antlerContacts.Length * 2);
                bool collected = false;
                RangePickupEvent pickup = default;
                for (int i = 0; i < count; i++)
                {
                    var contact = antlerContacts[i];
                    antlerContacts[i] = null;
                    if (collected || !contact) continue;
                    var body = contact.GetComponentInParent<CharacterInstance>();
                    if (!body || body.CurrentHealth <= 0 || body.Team != shard.Team) continue;
                    if (!body.TryGetComponent<RangeResearchItems>(out var items))
                        items = body.gameObject.AddComponent<RangeResearchItems>();
                    collected = items.TryCollectAntlerBuff();
                    if (collected)
                    {
                        RpcAntlerConsumed(body.netId, shard.Position);
                        pickup = new RangePickupEvent(body, RangePickupClass.Minor);
                    }
                }
                if (collected) NotifyAntlerPickup(id, in pickup);
            }
            foreach (var update in antlerPositionUpdates)
                if (antlerShards.ContainsKey(update.Key) && !antlerRemovals.Contains(update.Key))
                    antlerShards[update.Key] = update.Value;
            antlerPositionUpdates.Clear();
            foreach (uint id in antlerRemovals) RemoveAntlerShard(id);
            antlerRemovals.Clear();
            foreach (uint ownerId in antlerReplacements)
                if (NetworkServer.spawned.TryGetValue(ownerId, out var identity) && identity)
                    TrySpawnAntlerShard(identity.GetComponent<CharacterInstance>(), Vector3.zero);
            antlerReplacements.Clear();
        }

        void NotifyAntlerPickup(uint id, in RangePickupEvent pickup)
        {
            // The reused ID snapshot allows callbacks to clear/spawn shards without invalidating iteration.
            // Preserve per-shard source order: buff/effect, body callback, removal, global callback.
            try
            {
                if (pickup.Body && pickup.Body.TryGetComponent<RangeResearchItems>(out var items))
                    items.NotifyServerPickup(in pickup);
            }
            catch (System.Exception exception) { Debug.LogException(exception); }
            finally { RemoveAntlerShard(id); }
            try { ServerCharacterPickupItem?.Invoke(pickup); }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }

        internal void ClearAntlerShards(uint ownerId)
        {
            if (!isServer) return;
            foreach (var entry in antlerShards)
                if (entry.Value.OwnerId == ownerId) antlerRemovals.Add(entry.Key);
            foreach (uint id in antlerRemovals) RemoveAntlerShard(id);
            antlerRemovals.Clear();
        }

        void RemoveAntlerShard(uint id)
        {
            if (antlerMotions.Remove(id, out var motion) && motion)
            {
                motion.gameObject.SetActive(false);
                Destroy(motion.gameObject);
            }
            antlerShards.Remove(id);
        }

        public override void OnStopServer()
        {
            ServerCharacterPickupItem = null;
            foreach (var motion in antlerMotions.Values)
                if (motion) { motion.gameObject.SetActive(false); Destroy(motion.gameObject); }
            antlerMotions.Clear();
            antlerPositionUpdates.Clear();
            antlerRemovals.Clear();
            antlerReplacements.Clear();
            antlerShards.Clear();
            base.OnStopServer();
        }

        void StartRainAntlerVisuals()
        {
            antlerShards.OnAdd += DrawAntlerShard;
            antlerShards.OnSet += MoveAntlerShardVisual;
            antlerShards.OnRemove += RemoveAntlerShardVisual;
            antlerShards.OnClear += ClearAntlerShardVisuals;
            foreach (uint id in antlerShards.Keys) DrawAntlerShard(id);
        }

        void StopRainAntlerVisuals()
        {
            antlerShards.OnAdd -= DrawAntlerShard;
            antlerShards.OnSet -= MoveAntlerShardVisual;
            antlerShards.OnRemove -= RemoveAntlerShardVisual;
            antlerShards.OnClear -= ClearAntlerShardVisuals;
            ClearAntlerShardVisuals();
            ClearAntlerConsumedEffects();
        }

        void DrawAntlerShard(uint id)
        {
            var model = RangeAntlerParticleFactory.Create(antlerShards[id].Position);
            model.name = "Rain antler shard";
            model.transform.SetParent(transform);
            antlerVisuals.Add(id, model);
        }

        void RemoveAntlerShardVisual(uint id, AntlerShard previous)
        {
            if (antlerVisuals.Remove(id, out var model) && model) { model.SetActive(false); Destroy(model); }
        }

        void MoveAntlerShardVisual(uint id, AntlerShard previous)
        {
            if (antlerVisuals.TryGetValue(id, out var model) && model)
                model.transform.position = antlerShards[id].Position;
        }

        void ClearAntlerShardVisuals()
        {
            foreach (var model in antlerVisuals.Values) if (model) { model.SetActive(false); Destroy(model); }
            antlerVisuals.Clear();
        }
    }
}
