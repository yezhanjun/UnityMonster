using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string PlagueUnlock = "active_loki_E";
        public const string PlagueEvolution = "active_loki_E_evolve";
        private int plagueRank;
        private float plagueReady;
        private readonly List<Infection> infections = new List<Infection>(2);

        private struct Infection
        {
            public Health target;
            public float next;
            public int hits;
            public bool evolved;
        }

        private bool CanChoosePlague() => PendingRewards > 0 && plagueRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[plagueRank] && progress.IsLearned(PlagueUnlock) &&
            (plagueRank < 3 || progress.IsLearned(PlagueEvolution));

        internal bool TryChoosePlague()
        {
            if (!CanChoosePlague())
                return false;

            plagueRank++;
            rewardsSpent++;
            return true;
        }

        private void TryPlague(float now)
        {
            if (plagueRank == 0 || now < plagueReady || !progress.IsLearned(PlagueUnlock))
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            int rank = Mathf.Min(plagueRank, 3);
            bool evolved = plagueRank == 4 && progress.IsLearned(PlagueEvolution);
            plagueReady = now + ScaleSkillCooldown(evolved ? 2f : 3f - rank * 0.25f);
            InfectPlagueHost(targets[Random.Range(0, targets.Count)], now, rank, evolved);
            if (RollAnsuzRepeat(ref plagueReady))
            {
                CollectTargets(caster.transform.position, MissileRange);
                if (targets.Count > 0)
                    InfectPlagueHost(targets[Random.Range(0, targets.Count)], now, rank, evolved);
            }
        }

        private void InfectPlagueHost(Health host, float now, int rank, bool evolved)
        {
            if (evolved && RollSlay(host, 0.1f))
            {
                Vector3 impact = host.transform.position;
                DealGodDamage(host, host.CurrentHealth);
                if (!host || host.CurrentHealth <= 0)
                {
                    StrikeGodDamage(impact, 2f, 25);
                    return;
                }
            }

            infections.Add(new Infection { target = host, next = now, hits = 3 + rank * 3, evolved = evolved });
        }

        private void TickPlague(float now)
        {
            for (int i = infections.Count - 1; i >= 0; i--)
            {
                var infection = infections[i];
                var host = infection.target;
                while (host && host.CurrentHealth > 0 && infection.hits > 0 && now >= infection.next)
                {
                    infection.hits--;
                    infection.next += infection.evolved ? 0.25f : 0.3f;
                    Vector3 impact = host.transform.position;
                    DealGodDamage(host, infection.evolved ? 15 : 10);
                    if (infection.evolved && (!host || host.CurrentHealth <= 0))
                        StrikeGodDamage(impact, 2f, 25);
                }

                if (!host || host.CurrentHealth <= 0 || infection.hits == 0)
                    infections.RemoveAt(i);
                else
                    infections[i] = infection;
            }
        }
    }
}
