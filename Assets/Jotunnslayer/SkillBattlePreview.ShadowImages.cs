using System.Collections.Generic;
using UnityEngine;
using MTPSKIT.Gameplay;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string ShadowImagesEvolution = "active_valkyrie_D_evolve";
        private readonly List<ShadowImage> shadowImages = new List<ShadowImage>(14);
        private readonly List<ShadowImageBatch> shadowImageBatches = new List<ShadowImageBatch>(4);
        // Original attack event normalized by FollowerAnimatedCast's base BAT.
        private const float ShadowImageWindup = 0.638029575f / 1.433333397f * 0.3f;
        private readonly HashSet<Health> shadowImageCastHits = new HashSet<Health>();
        private Vector3 shadowImageCastPosition;
        private float shadowImageCastExpires;
        private bool shadowImageCastActive;
        private int shadowImagesRank;
        private float shadowImagesReady;

        private struct ShadowImage
        {
            public Vector3 position;
            public float expires;
            public bool explodes;
            public float attackAt;
            public Health target;
            public ShadowImagePresentation presentation;
        }

        private struct ShadowImageBatch
        {
            public int index, count;
            public float nextSpawn, delay, angle;
            public bool evolved;
        }

        private bool CanChooseShadowImages() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 && shadowImagesRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[shadowImagesRank] &&
            (shadowImagesRank < 3 || progress.IsLearned(ShadowImagesEvolution));

        internal bool TryChooseShadowImages()
        {
            if (!CanChooseShadowImages())
                return false;

            shadowImagesRank++;
            rewardsSpent++;
            return true;
        }

        private void TryShadowImages(float now)
        {
            if (shadowImagesRank == 0 || now < shadowImagesReady)
                return;

            shadowImagesReady = now + ScaleSkillCooldown(5f);
            bool evolved = shadowImagesRank == 4 && progress.IsLearned(ShadowImagesEvolution);
            int rank = Mathf.Min(shadowImagesRank, 3);
            float delay = rank == 1 ? 0.25f : rank == 2 ? 0.2f : 0.18f;
            shadowImageBatches.Add(new ShadowImageBatch
            {
                count = 1 + 2 * rank, delay = delay, nextSpawn = now + delay,
                angle = rank == 1 ? 120f : rank == 2 ? 72f : 51.4f, evolved = evolved
            });
            // The evolved summon chains a stationary projectile with a 0.1-second hit window.
            if (evolved)
            {
                shadowImageCastPosition = caster.transform.position;
                shadowImageCastExpires = now + 0.1f;
                shadowImageCastHits.Clear();
                shadowImageCastActive = true;
                TickShadowImageCast(now);
            }
        }

        private void ClearShadowImages()
        {
            for (int i = 0; i < shadowImages.Count; i++)
                ReleaseShadowPresentation(shadowImages[i].presentation);
            shadowImageBatches.Clear();
            shadowImages.Clear();
            shadowImageCastActive = false;
            shadowImageCastHits.Clear();
        }

        private Health FindShadowImageTarget(Vector3 origin)
        {
            CollectTargets(origin, 8f);
            Health closest = null;
            float distance = float.MaxValue;
            for (int i = 0; i < targets.Count; i++)
            {
                Health candidate = targets[i];
                float square = (candidate.transform.position - origin).sqrMagnitude;
                if (square < distance || (square == distance && closest && candidate.netId < closest.netId))
                {
                    closest = candidate;
                    distance = square;
                }
            }
            return closest;
        }

        private int ShadowImageDamage(int nominal)
        {
            // Original actor/effect math stays floating-point. Quantize only at the current
            // TPS integer-health boundary, after Spearhead and class modifiers, never before.
            float damage = nominal * Random.Range(0.9f, 1.1f);
            damage *= (4f + spearheadRank) * 0.25f;
            return Mathf.RoundToInt(ScaleClassSkillDamage(damage));
        }

        private void StrikeShadowImage(Vector3 position)
        {
            CollectTargets(position, 2f);
            for (int i = 0; i < targets.Count; i++)
                Deal(targets[i], ShadowImageDamage(30));
            RecordImpact(position + Vector3.up);
        }

        private void TickShadowImageCast(float now)
        {
            if (!shadowImageCastActive)
                return;
            if (now >= shadowImageCastExpires)
            {
                shadowImageCastActive = false;
                shadowImageCastHits.Clear();
                return;
            }
            CollectTargets(shadowImageCastPosition, 2f);
            for (int i = 0; i < targets.Count; i++)
                if (shadowImageCastHits.Add(targets[i]))
                    Deal(targets[i], ShadowImageDamage(30));
            RecordImpact(shadowImageCastPosition + Vector3.up);
        }

        private void TickShadowImages(float now)
        {
            TickShadowImageCast(now);
            for (int i = shadowImageBatches.Count - 1; i >= 0; i--)
            {
                var batch = shadowImageBatches[i];
                if (now < batch.nextSpawn)
                    continue;
                var pose = caster.transform;
                Vector3 direction = Quaternion.AngleAxis(batch.index * batch.angle, Vector3.up) * pose.forward;
                Vector3 position = pose.position + direction * Random.Range(4f, 8f);
                // Match the existing preview's terrain surface, without requiring a baked NavMesh.
                if (Physics.Raycast(position + Vector3.up * 4f, Vector3.down, out var ground, 12f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                    position = ground.point;
                Health target = FindShadowImageTarget(pose.position);
                Vector3 facing = target ? target.transform.position - position : pose.forward;
                facing.y = 0f;
                Quaternion rotation = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : pose.rotation;
                shadowImages.Add(new ShadowImage
                {
                    position = position, expires = now + 1f, explodes = batch.evolved,
                    target = target, attackAt = target ? now + ShadowImageWindup : float.PositiveInfinity,
                    presentation = AcquireShadowPresentation(position, rotation, target, now)
                });
                RecordImpact(position + Vector3.up);
                batch.index++;
                if (batch.index == batch.count)
                    shadowImageBatches.RemoveAt(i);
                else
                {
                    // Coroutine-equivalent: a delayed frame does not emit the whole remaining batch.
                    batch.nextSpawn = now + batch.delay;
                    shadowImageBatches[i] = batch;
                }
            }

            for (int i = shadowImages.Count - 1; i >= 0; i--)
            {
                var image = shadowImages[i];
                if (now >= image.expires)
                {
                    ReleaseShadowPresentation(image.presentation);
                    shadowImages.RemoveAt(i);
                    if (image.explodes)
                        StrikeShadowImage(image.position);
                    continue;
                }
                UpdateShadowPresentation(image.presentation, now);
                if (now < image.attackAt)
                    continue;
                Health target = image.target;
                image.attackAt = float.PositiveInfinity;
                image.target = null;
                shadowImages[i] = image;
                // Original first-chain resolution prioritizes the locked valid actor without an
                // extra follower-distance check. An invalid actor falls back around its current
                // transform; a destroyed transform falls back around the master.
                if (!target || !target.isServer || target.CurrentHealth <= 0 || target.Team == caster.Team)
                    target = FindShadowImageTarget(target ? target.transform.position : caster.transform.position);
                if (target)
                    Deal(target, ShadowImageDamage(25));
            }
        }
    }
}
