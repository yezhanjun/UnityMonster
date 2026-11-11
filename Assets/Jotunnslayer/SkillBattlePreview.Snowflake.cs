using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string SnowEvolution = "active_skadi_C_evolve";
        private int snowRank, snowShards, snowSeconds, snowDamage;
        private int snowCastCount;
        private float snowReady, snowNextShard, snowNextSecond, snowShardInterval;
        private Vector3 snowCenter;
        private int iceBlockRank;
        private float iceBlockUntil;
        private static readonly int[] SnowFightLevels = { 1, 8, 15 };
        private static readonly int[] SnowFightDamage = { 8, 11, 15 };
        private static readonly float[] SnowFightCooldown = { 1.5f, 1.1f, 0.7f };
        private int snowFightRank;
        private float snowFightReady;

        private bool CanChooseSnowFight() => HasLiveCaster && PendingRewards > 0 &&
            snowFightRank < SnowFightLevels.Length && raidLevel >= SnowFightLevels[snowFightRank] &&
            (snowFightRank != 0 || progress.IsLearned("passive_skadi_I"));

        internal bool TryChooseSnowFight()
        {
            if (!CanChooseSnowFight())
                return false;

            snowFightRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySnowFight(float now)
        {
            if (snowFightRank == 0 || now < snowFightReady)
                return;

            Vector3 origin = caster.transform.position;
            CollectTargets(origin, 8f);
            Health nearest = null;
            float nearestDistance = float.PositiveInfinity;
            foreach (var target in targets)
            {
                float distance = (target.transform.position - origin).sqrMagnitude;
                if (distance >= nearestDistance)
                    continue;

                nearest = target;
                nearestDistance = distance;
            }

            if (!nearest)
                return;

            int rankIndex = snowFightRank - 1;
            snowFightReady = now + ScaleSkillCooldown(SnowFightCooldown[rankIndex]);
            int before = nearest.CurrentHealth;
            DealGodDamage(nearest, SnowFightDamage[rankIndex]);
            if (nearest && nearest.CurrentHealth > 0 && nearest.CurrentHealth < before)
                PushSkillTarget(nearest, origin, 0.5f);
        }

        private bool CanChooseIceBlock() => HasLiveCaster && PendingRewards > 0 && iceBlockRank < 2 &&
            (iceBlockRank == 0 ? progress.IsLearned("passive_skadi_H") : raidLevel >= 8);

        internal bool TryChooseIceBlock()
        {
            if (!CanChooseIceBlock())
                return false;

            iceBlockRank++;
            rewardsSpent++;
            RegisterRaidStats();
            return true;
        }

        private void TriggerIceBlock(float now)
        {
            if (iceBlockRank != 0)
                iceBlockUntil = now + (iceBlockRank == 1 ? 4f : 7f);
        }

        internal static bool IsProtectedByIceBlock(Health target, float now) =>
            RaidStatOwners.TryGetValue(target, out var owner) && now < owner.iceBlockUntil;

        private bool CanChooseSnowflake() => PendingRewards > 0 && snowRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[snowRank] && (snowRank < 3 || progress.IsLearned(SnowEvolution));

        internal bool TryChooseSnowflake()
        {
            if (!CanChooseSnowflake())
                return false;

            snowRank++;
            rewardsSpent++;
            return true;
        }

        private void TrySnowflake(float now)
        {
            if (snowRank == 0 || snowSeconds > 0 || snowShards > 0 || now < snowReady)
                return;

            int rank = Mathf.Min(snowRank, 3);
            bool evolved = snowRank == 4 && progress.IsLearned(SnowEvolution);
            snowCenter = caster.transform.position;
            snowReady = now + ScaleSkillCooldown(8f);
            snowCastCount = RollAnsuzRepeat(ref snowReady) ? 2 : 1;
            snowSeconds = evolved ? 7 : 3 + rank;
            snowDamage = evolved ? 25 : 16;
            snowShards = evolved ? 36 : 2 + 4 * rank;
            snowShardInterval = (float)snowSeconds / snowShards;
            snowNextShard = now + snowShardInterval;
            snowNextSecond = now + 1f;
        }

        private void TickSnowflake(float now)
        {
            while (snowSeconds > 0 && now >= snowNextSecond)
            {
                snowSeconds--;
                snowNextSecond += 1f;
                for (int i = 0; i < snowCastCount; i++)
                    StrikeGodDamage(snowCenter, 3f, snowDamage);
            }

            while (snowShards > 0 && now + 0.0005f >= snowNextShard)
            {
                snowShards--;
                snowNextShard += snowShardInterval;
                for (int i = 0; i < snowCastCount; i++)
                {
                    CollectTargets(snowCenter, 8f);
                    if (targets.Count > 0)
                        DealGodDamage(targets[Random.Range(0, targets.Count)], 8);
                }
            }
        }
    }
}
