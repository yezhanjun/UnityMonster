using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.Pool;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode
    {
        readonly SyncDictionary<uint, Vector4> rainHealingPlants = new SyncDictionary<uint, Vector4>();
        readonly Dictionary<uint, GameObject> rainHealingPlantVisuals = new Dictionary<uint, GameObject>();
        Material rainHealingPlantMaterial;
        uint nextRainHealingPlant;
        internal int ActiveRainHealingPlantCount => rainHealingPlants.Count;

        [Server]
        internal void PlantRainHealingSeed(Vector3 position, int team, uint planter, int stacks)
        {
            if (stacks <= 0 || team < 0) return;
            if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out var ground, 8f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                position = ground.point;

            StartCoroutine(GrowRainHealingPlant(position, team, planter, 5f + 5f * stacks));
        }

        IEnumerator GrowRainHealingPlant(Vector3 center, int team, uint planter, float radius)
        {
            yield return new WaitForSeconds(5f);
            if (!isServer) yield break;
            uint id = ++nextRainHealingPlant;
            rainHealingPlants.Add(id, new Vector4(center.x, center.y, center.z, radius));
            var interval = new WaitForSeconds(.5f);
            float radiusSquared = radius * radius;
            try
            {
                for (int pulse = 0; pulse < 20; pulse++)
                {
                    yield return interval;
                    if (!isServer) yield break;
                    var allies = ListPool<Health>.Get();
                    try
                    {
                        foreach (var ally in GameManager.HealthInstances.Values)
                            if (ally && ally.Team == team && ally.CurrentHealth > 0 &&
                                (ally.transform.position - center).sqrMagnitude <= radiusSquared)
                                allies.Add(ally);

                        foreach (var ally in allies)
                            if (ally && ally.CurrentHealth > 0 && ally.Team == team)
                                ally.ServerHeal(Mathf.Max(1, Mathf.RoundToInt(.05f * ally.MaxHealth)), planter);
                    }
                    finally { ListPool<Health>.Release(allies); }
                }
            }
            finally
            {
                if (isServer) rainHealingPlants.Remove(id);
            }
        }

        void StartRainHealingPlantVisuals()
        {
            rainHealingPlants.OnAdd += DrawRainHealingPlant;
            rainHealingPlants.OnRemove += RemoveRainHealingPlant;
            rainHealingPlants.OnClear += ClearRainHealingPlantVisuals;
            foreach (uint id in rainHealingPlants.Keys) DrawRainHealingPlant(id);
        }

        void StopRainHealingPlantVisuals()
        {
            rainHealingPlants.OnAdd -= DrawRainHealingPlant;
            rainHealingPlants.OnRemove -= RemoveRainHealingPlant;
            rainHealingPlants.OnClear -= ClearRainHealingPlantVisuals;
            ClearRainHealingPlantVisuals();
            if (rainHealingPlantMaterial) Destroy(rainHealingPlantMaterial);
        }

        void DrawRainHealingPlant(uint id)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null ||
                rainHealingPlantVisuals.ContainsKey(id)) return;
            if (!rainHealingPlantMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) shader = Shader.Find("Sprites/Default");
                if (!shader) return;
                rainHealingPlantMaterial = new Material(shader) { color = new Color(.4f, 1f, .46f) };
            }

            Vector4 state = rainHealingPlants[id];
            Vector3 center = new Vector3(state.x, state.y, state.z);
            var ring = new GameObject("Rain desk plant healing ring").AddComponent<LineRenderer>();
            ring.transform.SetParent(transform, false);
            ring.sharedMaterial = rainHealingPlantMaterial;
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.widthMultiplier = .1f;
            ring.positionCount = 48;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / ring.positionCount);
                ring.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * state.w, .15f, Mathf.Sin(angle) * state.w));
            }
            rainHealingPlantVisuals.Add(id, ring.gameObject);
        }

        void RemoveRainHealingPlant(uint id, Vector4 _)
        {
            if (rainHealingPlantVisuals.Remove(id, out var visual) && visual) Destroy(visual);
        }

        void ClearRainHealingPlantVisuals()
        {
            foreach (var visual in rainHealingPlantVisuals.Values)
                if (visual) Destroy(visual);
            rainHealingPlantVisuals.Clear();
        }
    }
}
