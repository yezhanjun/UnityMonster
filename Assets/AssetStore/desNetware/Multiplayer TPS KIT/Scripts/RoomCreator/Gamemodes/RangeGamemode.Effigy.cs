using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode
    {
        const float EffigyRadius = 15f;
        readonly SyncDictionary<uint, Vector3> rainEffigies = new SyncDictionary<uint, Vector3>();
        readonly Dictionary<uint, Queue<uint>> rainEffigiesByOwner = new Dictionary<uint, Queue<uint>>();
        readonly Dictionary<uint, GameObject> rainEffigyVisuals = new Dictionary<uint, GameObject>();
        Material rainEffigyMaterial;
        uint nextRainEffigy;

        [Server]
        internal bool TryPlaceRainEffigy(CharacterInstance character, PlayerInstance player)
        {
            Vector3 origin = character.CombatAimPoint;
            Vector3 direction = Quaternion.Euler(character.lookInput.x, character.lookInput.y, 0f) * Vector3.forward;
            if (!Physics.Raycast(origin, direction, out var hit, 60f, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore) || hit.normal.y < .5f) return false;

            uint ownerId = player ? player.netId : character.netId;
            if (!rainEffigiesByOwner.TryGetValue(ownerId, out var placements))
            {
                placements = new Queue<uint>(5);
                rainEffigiesByOwner.Add(ownerId, placements);
            }
            if (placements.Count >= 5) rainEffigies.Remove(placements.Dequeue());
            uint id = ++nextRainEffigy;
            placements.Enqueue(id);
            rainEffigies.Add(id, hit.point);
            return true;
        }

        internal bool IsWithinRainEffigy(Vector3 position)
        {
            if (rainEffigies.Count == 0) return false;
            foreach (var entry in rainEffigies)
                if ((entry.Value - position).sqrMagnitude <= EffigyRadius * EffigyRadius) return true;
            return false;
        }

        void StartRainEffigyVisuals()
        {
            rainEffigies.OnAdd += DrawRainEffigy;
            rainEffigies.OnRemove += RemoveRainEffigy;
            rainEffigies.OnClear += ClearRainEffigyVisuals;
            foreach (uint id in rainEffigies.Keys) DrawRainEffigy(id);
        }

        void StopRainEffigyVisuals()
        {
            rainEffigies.OnAdd -= DrawRainEffigy;
            rainEffigies.OnRemove -= RemoveRainEffigy;
            rainEffigies.OnClear -= ClearRainEffigyVisuals;
            ClearRainEffigyVisuals();
            if (rainEffigyMaterial) Destroy(rainEffigyMaterial);
        }

        void DrawRainEffigy(uint id)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                rainEffigyVisuals.ContainsKey(id)) return;
            if (!rainEffigyMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (!shader) return;
                rainEffigyMaterial = new Material(shader) { color = new Color(.57f, .32f, .85f) };
            }

            Vector3 center = rainEffigies[id];
            var ring = new GameObject("Rain effigy of grief").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = rainEffigyMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = .12f;
            ring.positionCount = 48;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ring.positionCount);
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * EffigyRadius, .15f, Mathf.Sin(angle) * EffigyRadius));
            }
            rainEffigyVisuals.Add(id, ring.gameObject);
        }

        void RemoveRainEffigy(uint id, Vector3 _)
        {
            if (rainEffigyVisuals.Remove(id, out var visual) && visual) Destroy(visual);
        }

        void ClearRainEffigyVisuals()
        {
            foreach (var visual in rainEffigyVisuals.Values)
                if (visual) Destroy(visual);
            rainEffigyVisuals.Clear();
        }
    }
}
