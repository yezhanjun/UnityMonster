using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string FirestarEvolution = "active_loki_C_evolve";
        private static readonly float[] FirestarBurnChance = { 0.08f, 0.16f, 0.25f };
        private int firestarRank;
        private float firestarReady;
        private readonly List<Firestar> firestars = new List<Firestar>(2);
        private readonly List<FirestarBurn> firestarBurns = new List<FirestarBurn>();
        private float blazingSoulReady;
        private int scorchingHeatPercent;

        private void ResetBlazingSoul(SkillProgress profile)
        {
            blazingSoulReady = 0f;
            scorchingHeatPercent = LiveMatch && SelectedRaidClass == RaidClass.Rogue && profile != null
                ? ReadFourTierHalfPoints(profile, "virtue_rogue_02_") : 0;
        }

        private void TryBlazingSoul(float now)
        {
            if (!LiveMatch || SelectedRaidClass != RaidClass.Rogue || now < blazingSoulReady)
                return;

            CollectTargets(caster.transform.position, 8f);
            if (targets.Count == 0)
                return;

            Health target = targets[Random.Range(0, targets.Count)];
            blazingSoulReady = now + ScaleSkillCooldown(Random.Range(4f, 16f));
            bool repeat = progress.IsLearned("virtue_rogue_04_a") && Random.value < 0.3f;
            ExplodeBlazingSoul(target, now);
            if (repeat && target && target.CurrentHealth > 0 && HasLiveCaster)
                ExplodeBlazingSoul(target, now);
        }

        private void ExplodeBlazingSoul(Health target, float now)
        {
            int before = target.CurrentHealth;
            Deal(target, ScaleClassSkillDamage(26));
            if (!target || target.CurrentHealth >= before)
                return;

            caster.ServerHeal(20, caster.netId, allowOverhealBarrier: false);
            if (target.CurrentHealth > 0 && scorchingHeatPercent > 0 && Random.value < scorchingHeatPercent / 100f)
                ApplyPreviewBurn(target, now, ScaleClassSkillDamage(4), ticks: 6, periodic: true);
        }

        private struct Firestar
        {
            public Health target;
            public Vector3 center;
            public float next, chance;
            public int hits;
            public bool evolved;
        }

        private struct FirestarBurn
        {
            public Health target;
            public float next;
            public int ticks, damage;
            public bool periodic;
        }

        private bool CanChooseFirestar() => PendingRewards > 0 && firestarRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[firestarRank] && (firestarRank < 3 || progress.IsLearned(FirestarEvolution));

        internal bool TryChooseFirestar()
        {
            if (!CanChooseFirestar())
                return false;

            firestarRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFirestar(float now)
        {
            if (firestarRank == 0 || now < firestarReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            int rank = Mathf.Min(firestarRank, 3);
            bool evolved = firestarRank == 4 && progress.IsLearned(FirestarEvolution);
            var target = targets[0];
            firestarReady = now + ScaleSkillCooldown(evolved ? 4.5f : 2.75f - rank * 0.25f);
            var cast = new Firestar { target = target, center = target.transform.position, next = now,
                chance = evolved ? 1f : FirestarBurnChance[rank - 1], hits = evolved ? 3 : rank + 1,
                evolved = evolved };
            firestars.Add(cast);
            if (RollAnsuzRepeat(ref firestarReady))
                firestars.Add(cast);
        }

        private void ApplyPreviewBurn(Health target, float now, int damage = 4, int ticks = 8, bool periodic = false) =>
            firestarBurns.Add(new FirestarBurn { target = target, next = now + 0.5f, ticks = ticks, damage = damage, periodic = periodic });

        private void TickFirestars(float now)
        {
            for (int i = firestars.Count - 1; i >= 0; i--)
            {
                var star = firestars[i];
                if (now < star.next)
                    continue;

                if (star.target && star.target.CurrentHealth > 0)
                    star.center = star.target.transform.position;

                CollectTargets(star.center, 1.5f);
                foreach (var target in targets)
                {
                    DealGodDamage(target, star.evolved ? 24 : 16);
                    if (target && target.CurrentHealth > 0 && (star.chance >= 1f || Random.value < star.chance))
                        ApplyPreviewBurn(target, now, ScaleGodDamage(4));
                }

                star.hits--;
                if (star.hits == 0)
                {
                    firestars.RemoveAt(i);
                    continue;
                }

                CollectTargets(star.center, 6f);
                foreach (var target in targets)
                {
                    if (target == star.target)
                        continue;
                    star.target = target;
                    break;
                }

                star.next = now + (star.evolved ? 1f : 0.35f);
                firestars[i] = star;
            }

            for (int i = firestarBurns.Count - 1; i >= 0; i--)
            {
                var burn = firestarBurns[i];
                while (burn.target && burn.target.CurrentHealth > 0 && burn.ticks > 0 && now >= burn.next)
                {
                    Deal(burn.target, burn.damage, burn.periodic);
                    burn.ticks--;
                    burn.next += 0.5f;
                }

                if (!burn.target || burn.target.CurrentHealth <= 0 || burn.ticks == 0)
                    firestarBurns.RemoveAt(i);
                else
                    firestarBurns[i] = burn;
            }
        }
    }
}
