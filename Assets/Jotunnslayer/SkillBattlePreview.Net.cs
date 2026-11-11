using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string NetEvolution = "active_ranger_D_evolve";
        private const float NetRadius = 1.5f;
        private static readonly List<NetCast> ActiveNets = new List<NetCast>();
        private int netRank;
        private float netReady;

        private sealed class NetCast
        {
            public SkillBattlePreview owner;
            public Vector3[] centers;
            public float starts, expires, nextTick;
            public int damage;
            public readonly Dictionary<Health, bool> roots = new Dictionary<Health, bool>();

            public bool Contains(Vector3 position)
            {
                foreach (Vector3 center in centers)
                {
                    if ((position - center).sqrMagnitude <= NetRadius * NetRadius)
                        return true;
                }

                return false;
            }
        }

        private bool CanChooseNet() => AllowsRaidClass(RaidClass.Ranger) && PendingRewards > 0 && netRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[netRank] && (netRank < 3 || progress.IsLearned(NetEvolution));

        internal bool TryChooseNet()
        {
            if (!CanChooseNet())
                return false;

            netRank++;
            rewardsSpent++;
            return true;
        }

        private void TryNet(float now)
        {
            if (netRank == 0 || now < netReady)
                return;

            bool evolved = netRank == 4 && progress.IsLearned(NetEvolution);
            int count = evolved ? 6 : 1 + Mathf.Min(netRank, 3);
            Vector3 origin = caster.transform.position;
            var centers = new Vector3[count];
            for (int i = 0; i < count; i++)
                centers[i] = origin + Quaternion.Euler(0, caster.transform.eulerAngles.y + i * 360f / count, 0) *
                    Vector3.forward * 4f;

            netReady = now + ScaleSkillCooldown(3f);
            ActiveNets.Add(new NetCast { owner = this, centers = centers, starts = now, expires = now + 2f,
                nextTick = now + 1f, damage = evolved ? 15 : 10 });
        }

        private void TickNets(float now)
        {
            for (int i = ActiveNets.Count - 1; i >= 0; i--)
            {
                var net = ActiveNets[i];
                if (net.owner != this)
                    continue;

                while (now >= net.nextTick && net.nextTick <= net.expires)
                {
                    CollectTargets(net.centers[0], 9.5f);
                    foreach (var target in targets)
                    {
                        if (net.Contains(target.transform.position))
                            Deal(target, ScaleClassSkillDamage(net.damage), periodic: true);
                    }

                    net.nextTick += 1f;
                }

                if (now >= net.expires)
                    ActiveNets.RemoveAt(i);
            }
        }

        private void ClearNets()
        {
            for (int i = ActiveNets.Count - 1; i >= 0; i--)
            {
                if (ActiveNets[i].owner == this)
                    ActiveNets.RemoveAt(i);
            }
        }

        internal static bool IsRootedByNet(Health target) => NetMoveMultiplier(target, Time.time) == 0f;

        private static bool NetContainsEnemy(NetCast net, Health target, float now)
        {
            var source = net.owner.caster;
            return now >= net.starts && now < net.expires && source && source.CurrentHealth > 0 &&
                source.Team != target.Team && net.Contains(target.transform.position);
        }

        private static bool IsAffectedByRaidNet(Health target, float now)
        {
            foreach (var net in ActiveNets)
                if (NetContainsEnemy(net, target, now))
                    return true;
            return false;
        }

        internal static float NetMoveMultiplier(Health target, float now)
        {
            if (!target || !target.isServer || target.CurrentHealth <= 0)
                return 1f;

            float multiplier = 1f;
            foreach (var net in ActiveNets)
            {
                if (!NetContainsEnemy(net, target, now))
                    continue;

                multiplier = 0.75f;
                if (!net.roots.TryGetValue(target, out bool rooted))
                {
                    rooted = Random.value < 0.75f;
                    net.roots.Add(target, rooted);
                }

                if (rooted)
                    return 0f;
            }

            return multiplier;
        }
    }
}
