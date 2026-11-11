using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string JavelinsEvolution = "active_valkyrie_A_evolve";
        private static readonly float[] JavelinBleedChance = { 0f, 0.1f, 0.2f, 0.25f };
        private readonly Dictionary<Health, int> javelinHits = new Dictionary<Health, int>();
        private readonly List<RaidBleed> raidBleeds = new List<RaidBleed>();
        private readonly Vector3[] javelinDirections = new Vector3[8];
        private int javelinRank, javelinCastRank, javelinCount;
        private float javelinReady, javelinStarted, javelinDistance;
        private Vector3 javelinOrigin;

        private struct RaidBleed
        {
            public Health target;
            public float next;
            public int ticks, damage;
            public bool requiresHeartUnlock;
        }

        private bool CanChooseJavelins() => AllowsRaidClass(RaidClass.Valkyrie) && PendingRewards > 0 && javelinRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[javelinRank] &&
            (javelinRank < 3 || progress.IsLearned(JavelinsEvolution));

        internal bool TryChooseJavelins()
        {
            if (!CanChooseJavelins())
                return false;

            javelinRank++;
            rewardsSpent++;
            return true;
        }

        private void TryJavelins(float now)
        {
            if (javelinRank == 0 || javelinCount != 0 || now < javelinReady)
                return;

            javelinCastRank = javelinRank == 4 && !progress.IsLearned(JavelinsEvolution) ? 3 : javelinRank;
            javelinCount = javelinCastRank == 4 ? 8 : javelinCastRank + 2;
            javelinReady = now + ScaleSkillCooldown(4f);
            javelinStarted = now;
            javelinDistance = 0f;
            javelinOrigin = caster.transform.position;
            float yaw = caster.transform.eulerAngles.y;
            for (int i = 0; i < javelinCount; i++)
                javelinDirections[i] = Quaternion.AngleAxis(yaw + i * 360f / javelinCount, Vector3.up) * Vector3.forward;
        }

        private void TickJavelins(float now)
        {
            for (int i = raidBleeds.Count - 1; i >= 0; i--)
            {
                var bleed = raidBleeds[i];
                if (bleed.requiresHeartUnlock && !progress.IsLearned(HeartUnlock))
                {
                    raidBleeds.RemoveAt(i);
                    continue;
                }
                while (bleed.target && bleed.target.CurrentHealth > 0 && bleed.ticks > 0 && now >= bleed.next)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    int healthBefore = bleed.target.CurrentHealth;
#endif
                    Deal(bleed.target, bleed.damage, periodic: true);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    ValidationBleedTick?.Invoke(bleed.ticks, healthBefore - bleed.target.CurrentHealth);
#endif
                    bleed.next += 1f;
                    bleed.ticks--;
                }

                if (!bleed.target || bleed.target.CurrentHealth <= 0 || bleed.ticks == 0)
                    raidBleeds.RemoveAt(i);
                else
                    raidBleeds[i] = bleed;
            }

            if (javelinCount == 0)
                return;

            const float range = 10f;
            const float contact = 0.45f;
            float distance = Mathf.Clamp01(now - javelinStarted) * range;
            if (distance <= javelinDistance)
                return;

            CollectTargets(javelinOrigin, distance + contact);
            float bleedChance = JavelinBleedChance[javelinCastRank - 1];
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - javelinOrigin;
                javelinHits.TryGetValue(target, out int hits);
                for (int i = 0; i < javelinCount && target && target.CurrentHealth > 0; i++)
                {
                    int bit = 1 << i;
                    if ((hits & bit) != 0)
                        continue;

                    Vector3 direction = javelinDirections[i];
                    float along = Mathf.Clamp(Vector3.Dot(offset, direction), javelinDistance, distance);
                    if ((offset - direction * along).sqrMagnitude > contact * contact)
                        continue;

                    hits |= bit;
                    javelinHits[target] = hits;
                    int before = target.CurrentHealth;
                    Deal(target, ScaleValkyrieDamage(javelinCastRank == 4 ? 14 : 10));
                    if (target && target.CurrentHealth > 0 && target.CurrentHealth < before &&
                        bleedChance > 0f && Random.value < bleedChance)
                        ApplyRaidBleed(target, now, ScaleValkyrieDamage(2));
                }
            }

            javelinDistance = distance;
            if (distance >= range)
            {
                javelinCount = 0;
                javelinHits.Clear();
            }
        }

        private void ApplyRaidBleed(Health target, float now, int damage, int ticks = 5, bool requiresHeartUnlock = false) =>
            raidBleeds.Add(new RaidBleed
            {
                target = target, next = now + 1f, ticks = ticks, damage = damage,
                requiresHeartUnlock = requiresHeartUnlock
            });

        private void ClearJavelins()
        {
            javelinCount = 0;
            javelinHits.Clear();
            raidBleeds.Clear();
        }
    }
}
