using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string DiseaseRayEvolution = "active_nidhogg_B_evolve";
        private int diseaseRayRank;
        private float diseaseRayReady;
        private readonly Health[] diseaseRayPrimaries = new Health[3];

        private static readonly List<Disease> Diseases = new List<Disease>();

        private struct Disease
        {
            public SkillBattlePreview source;
            public Health victim;
            public float expires;
        }

        private void TryApplyDisease(Health victim, float chance, float now)
        {
            if (!victim || victim.CurrentHealth <= 0 || Random.value >= chance)
                return;

            for (int i = 0; i < Diseases.Count; i++)
            {
                var entry = Diseases[i];
                if (entry.source != this || entry.victim != victim)
                    continue;
                entry.expires = now + 3f;
                Diseases[i] = entry;
                return;
            }

            Diseases.Add(new Disease { source = this, victim = victim, expires = now + 3f });
        }

        private void ClearDiseases(float now = float.PositiveInfinity)
        {
            for (int i = Diseases.Count - 1; i >= 0; i--)
            {
                var entry = Diseases[i];
                if (entry.source == this && (!entry.victim || entry.victim.CurrentHealth <= 0 || now >= entry.expires))
                    Diseases.RemoveAt(i);
            }
        }

        internal static bool HasDisease(Health victim)
        {
            if (!victim)
                return false;

            foreach (var entry in Diseases)
            {
                if (entry.victim == victim)
                    return true;
            }

            return false;
        }

        private bool CanChooseDiseaseRay() => PendingRewards > 0 && diseaseRayRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[diseaseRayRank] && (diseaseRayRank < 3 || progress.IsLearned(DiseaseRayEvolution));

        internal bool TryChooseDiseaseRay()
        {
            if (!CanChooseDiseaseRay())
                return false;

            diseaseRayRank++;
            rewardsSpent++;
            return true;
        }

        private void TryDiseaseRay(float now)
        {
            if (diseaseRayRank == 0 || now < diseaseRayReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            bool evolved = diseaseRayRank == 4 && progress.IsLearned(DiseaseRayEvolution);
            int rank = Mathf.Min(diseaseRayRank, 3);
            diseaseRayReady = now + ScaleSkillCooldown(3f);
            CastDiseaseRay(now, rank, evolved);
            if (RollAnsuzRepeat(ref diseaseRayReady))
            {
                CollectTargets(caster.transform.position, MissileRange);
                CastDiseaseRay(now, rank, evolved);
            }
        }

        private void CastDiseaseRay(float now, int rank, bool evolved)
        {
            float diseaseChance = evolved ? 0.5f : 0.15f * rank;
            int rays = Mathf.Min(targets.Count, evolved ? 1 : rank);
            for (int i = 0; i < rays; i++)
                diseaseRayPrimaries[i] = targets[i];

            for (int i = 0; i < rays; i++)
            {
                var primary = diseaseRayPrimaries[i];
                diseaseRayPrimaries[i] = null;
                if (!primary || primary.CurrentHealth <= 0)
                    continue;

                Vector3 impact = primary.transform.position;
                IndicatePath(caster.transform.position, impact, .15f);
                IndicateRing(impact, evolved ? 2f : .5f);
                CollectTargets(impact, evolved ? 2f : 0.5f);
                foreach (var target in targets)
                {
                    DealGodDamage(target, evolved ? 35 : 14);
                    TryApplyDisease(target, diseaseChance, now);
                }
                CollectTargets(impact, 6f);
                int remaining = evolved ? 6 : 2;
                foreach (var secondary in targets)
                {
                    if (secondary == primary)
                        continue;

                    DealGodDamage(secondary, evolved ? 15 : 4 + 2 * rank);
                    TryApplyDisease(secondary, diseaseChance, now);
                    if (--remaining == 0)
                        break;
                }
            }
        }
    }
}

