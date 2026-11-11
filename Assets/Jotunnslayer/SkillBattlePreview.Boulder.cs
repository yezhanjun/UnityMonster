using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string BoulderEvolution = "active_odin_D_evolve";
        private int boulderRank;
        private float boulderReady, boulderContact, boulderBlast;
        private int followingBoulderCount;
        private readonly List<RollingBoulder> boulders = new List<RollingBoulder>(2);

        private sealed class RollingBoulder
        {
            public Vector3 origin, direction, previous;
            public float started, radius;
            public int damage;
            public readonly HashSet<Health> hits = new HashSet<Health>();
        }

        private bool CanChooseBoulder() => PendingRewards > 0 && boulderRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[boulderRank] && (boulderRank < 3 || progress.IsLearned(BoulderEvolution));

        internal bool TryChooseBoulder()
        {
            if (!CanChooseBoulder())
                return false;

            boulderRank++;
            rewardsSpent++;
            return true;
        }

        private void TryBoulder(float now)
        {
            if (boulderRank == 0 || now < boulderReady || followingBoulderCount != 0)
                return;

            if (boulderRank == 4 && progress.IsLearned(BoulderEvolution))
            {
                followingBoulderCount = RollAnsuzRepeat(ref boulderReady) ? 2 : 1;
                boulderContact = now;
                boulderBlast = now + 3f;
                return;
            }

            int rank = Mathf.Min(boulderRank, 3);
            boulderReady = now + ScaleSkillCooldown(3f);
            LaunchBoulder(now, rank);
            if (RollAnsuzRepeat(ref boulderReady))
                LaunchBoulder(now, rank);
        }

        private void LaunchBoulder(float now, int rank)
        {
            var pose = caster.transform;
            boulders.Add(new RollingBoulder { origin = pose.position, previous = pose.position,
                direction = Quaternion.Euler(0, pose.eulerAngles.y, 0) * Vector3.forward,
                started = now, radius = 0.2f + 0.2f * rank, damage = 12 + 6 * rank });
        }

        private void TickBoulders(float now)
        {
            for (int i = boulders.Count - 1; i >= 0; i--)
            {
                var boulder = boulders[i];
                float elapsed = Mathf.Clamp(now - boulder.started, 0f, 4f);
                Vector3 position = boulder.origin + boulder.direction * (elapsed * 4f);
                float length = (position - boulder.previous).magnitude;
                CollectTargets(boulder.previous, length + boulder.radius);
                foreach (var target in targets)
                {
                    Vector3 offset = target.transform.position - boulder.previous;
                    float distance = Mathf.Clamp(Vector3.Dot(offset, boulder.direction), 0f, length);
                    if ((offset - boulder.direction * distance).sqrMagnitude <= boulder.radius * boulder.radius &&
                        boulder.hits.Add(target))
                        DealGodDamage(target, boulder.damage);
                }

                boulder.previous = position;
                if (elapsed >= 4f)
                    boulders.RemoveAt(i);
            }

            if (followingBoulderCount == 0)
                return;
            if (!progress.IsLearned(BoulderEvolution))
            {
                followingBoulderCount = 0;
                return;
            }

            var pose = caster.transform;
            Vector3 center = pose.position - pose.forward * 1.5f;
            if (now >= boulderContact)
            {
                boulderContact = now + 0.5f;
                for (int i = 0; i < followingBoulderCount; i++)
                    StrikeGodDamage(center, 1f, 38);
            }
            if (now >= boulderBlast)
            {
                boulderBlast = now + 3f;
                for (int i = 0; i < followingBoulderCount; i++)
                    StrikeGodDamage(center, 2.5f, 20);
            }
        }

        private void ClearBoulders()
        {
            boulders.Clear();
            followingBoulderCount = 0;
        }
    }
}
