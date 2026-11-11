using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SpearBarrierEvolution = "active_valkyrie_B_evolve";
        private static readonly List<SkillBattlePreview> SpearBarriers = new List<SkillBattlePreview>();
        private int spearBarrierRank, spearBarrierTicks;
        private float spearBarrierReady, spearBarrierEnd, spearBarrierRadius, spearBarrierNextTick;
        private Vector3 spearBarrierCenter;
        private const int SpearBarrierRingSegments = 64;
        private LineRenderer spearBarrierRing;
        private Material spearBarrierMaterial;

        private bool CanChooseSpearBarrier() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 && spearBarrierRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[spearBarrierRank] &&
            (spearBarrierRank < 3 || progress.IsLearned(SpearBarrierEvolution));

        internal bool TryChooseSpearBarrier()
        {
            if (!CanChooseSpearBarrier())
                return false;

            spearBarrierRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySpearBarrier(float now)
        {
            if (spearBarrierRank == 0 || spearBarrierRadius > 0f || now < spearBarrierReady)
                return;

            bool evolved = spearBarrierRank == 4 && progress.IsLearned(SpearBarrierEvolution);
            spearBarrierReady = now + ScaleSkillCooldown(7f);
            spearBarrierEnd = now + (evolved ? 3f : 1f + Mathf.Min(spearBarrierRank, 3) * 0.5f);
            spearBarrierCenter = caster.transform.position;
            spearBarrierRadius = evolved ? 5.5f : 4f;
            spearBarrierTicks = evolved ? 3 : 0;
            spearBarrierNextTick = now + 1f;
            SpearBarriers.Add(this);
            ShowSpearBarrierRing(evolved);
            Strike(spearBarrierCenter, spearBarrierRadius, ScaleValkyrieDamage(evolved ? 15 : 10));
        }

        private void TickSpearBarrier(float now)
        {
            if (spearBarrierRadius <= 0f)
                return;

            while (spearBarrierTicks > 0 && now >= spearBarrierNextTick)
            {
                spearBarrierTicks--;
                spearBarrierNextTick += 1f;
                Strike(spearBarrierCenter, spearBarrierRadius, ScaleValkyrieDamage(7));
            }

            if (now >= spearBarrierEnd)
                ClearSpearBarrier();
        }

        internal static Vector3 LimitSpearBarrierMovement(Health target, Vector3 movement, float now)
        {
            if (SpearBarriers.Count == 0 || !target || !target.isServer)
                return movement;

            Vector3 start = target.transform.position;
            Vector2 step = new Vector2(movement.x, movement.z);
            float lengthSquared = step.sqrMagnitude;
            if (lengthSquared <= 0.000001f)
                return movement;

            float limit = 1f;
            foreach (var wall in SpearBarriers)
            {
                if (!wall.HasLiveCaster || now >= wall.spearBarrierEnd || target.Team == wall.caster.Team)
                    continue;

                Vector3 relative = start - wall.spearBarrierCenter;
                Vector2 offset = new Vector2(relative.x, relative.z);
                float b = Vector2.Dot(offset, step);
                float c = offset.sqrMagnitude - wall.spearBarrierRadius * wall.spearBarrierRadius;
                float discriminant = b * b - lengthSquared * c;
                if (discriminant <= 0f)
                    continue;

                float root = Mathf.Sqrt(discriminant);
                float crossing = (-b + (c < 0f ? root : -root)) / lengthSquared;
                if (crossing < 0f || crossing > limit || Mathf.Abs(relative.y + movement.y * crossing) > 2.5f)
                    continue;

                limit = Mathf.Max(0f, crossing - 0.05f / Mathf.Sqrt(lengthSquared));
            }

            return new Vector3(movement.x * limit, movement.y, movement.z * limit);
        }

        private void ClearSpearBarrier()
        {
            if (spearBarrierRadius <= 0f)
                return;

            spearBarrierRadius = 0f;
            spearBarrierTicks = 0;
            SpearBarriers.Remove(this);
            if (spearBarrierRing)
                spearBarrierRing.enabled = false;
        }

        private void ShowSpearBarrierRing(bool evolved)
        {
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                return;

            if (!spearBarrierRing)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader)
                    shader = Shader.Find("Sprites/Default");
                if (!shader)
                    return;

                var marker = new GameObject("Spear Barrier Boundary");
                marker.transform.SetParent(transform, false);
                spearBarrierRing = marker.AddComponent<LineRenderer>();
                spearBarrierRing.useWorldSpace = true;
                spearBarrierRing.loop = true;
                spearBarrierRing.positionCount = SpearBarrierRingSegments;
                spearBarrierRing.widthMultiplier = 0.06f;
                spearBarrierRing.shadowCastingMode = ShadowCastingMode.Off;
                spearBarrierRing.receiveShadows = false;
                spearBarrierMaterial = new Material(shader);
                spearBarrierRing.sharedMaterial = spearBarrierMaterial;
            }

            spearBarrierMaterial.color = evolved ? new Color(1f, 0.65f, 0.25f) : new Color(0.48f, 0.8f, 1f);
            Vector3 center = spearBarrierCenter + Vector3.up * 0.08f;
            for (int i = 0; i < SpearBarrierRingSegments; i++)
            {
                float angle = i * 2f * Mathf.PI / SpearBarrierRingSegments;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * spearBarrierRadius;
                spearBarrierRing.SetPosition(i, center + offset);
            }
            spearBarrierRing.enabled = true;
        }
    }
}
