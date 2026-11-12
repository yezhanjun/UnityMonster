using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string BarrierEvolution = "active_loki_B_evolve";
        private int barrierRank, barrierTicks, barrierDamage, barrierExplosion, barrierCastCount;
        private float barrierReady, barrierNextTick, barrierDepth;
        private Vector3 barrierCenter, barrierForward, barrierRight;

        private bool CanChooseFireBarrier() => PendingRewards > 0 && barrierRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[barrierRank] && (barrierRank < 3 || progress.IsLearned(BarrierEvolution));

        internal bool TryChooseFireBarrier()
        {
            if (!CanChooseFireBarrier())
                return false;

            barrierRank++;
            rewardsSpent++;
            return true;
        }

        private void TryFireBarrier(float now)
        {
            if (barrierRank == 0 || barrierTicks > 0 || now < barrierReady)
                return;

            int rank = Mathf.Min(barrierRank, 3);
            bool evolved = barrierRank == 4 && progress.IsLearned(BarrierEvolution);
            var pose = caster.transform;
            barrierForward = Quaternion.Euler(0, pose.eulerAngles.y, 0) * Vector3.forward;
            barrierRight = Vector3.Cross(Vector3.up, barrierForward);
            barrierCenter = pose.position + barrierForward * 3f;
            barrierDepth = evolved ? 2.5f : 0.5f;
            barrierDamage = evolved ? 36 : 12 + rank * 4;
            barrierExplosion = evolved ? 40 : 15 + rank * 5;
            barrierNextTick = now + 1f;
            barrierTicks = 2;
            barrierCastCount = RollAnsuzRepeat(ref barrierReady) ? 2 : 1;
            barrierReady = now + ScaleSkillCooldown(5.5f - rank * 0.5f);
        }

        private void TickFireBarrier(float now)
        {
            while (barrierTicks > 0 && now >= barrierNextTick)
            {
                barrierTicks--;
                barrierNextTick += 1f;
                for (int i = 0; i < barrierCastCount; i++)
                {
                    DamageFireBarrier(barrierDamage);
                    if (barrierTicks == 0)
                        DamageFireBarrier(barrierExplosion);
                }
            }
        }

        private void DamageFireBarrier(int damage)
        {
            CollectTargets(barrierCenter, 4f);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - barrierCenter;
                if (Mathf.Abs(offset.y) <= 1f && Mathf.Abs(Vector3.Dot(offset, barrierForward)) <= barrierDepth &&
                    Mathf.Abs(Vector3.Dot(offset, barrierRight)) <= 2.5f)
                    DealGodDamage(target, damage);
            }
        }
    }
}
