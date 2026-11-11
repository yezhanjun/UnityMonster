using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string GungnirEvolution = "active_odin_A_evolve";
        private int gungnirRank;
        private float gungnirReady;

        private bool CanChooseGungnir() => PendingRewards > 0 && gungnirRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[gungnirRank] && (gungnirRank < 3 || progress.IsLearned(GungnirEvolution));

        internal bool TryChooseGungnir()
        {
            if (!CanChooseGungnir())
                return false;

            gungnirRank++;
            rewardsSpent++;
            return true;
        }

        private void TryGungnir(float now)
        {
            if (gungnirRank == 0 || now < gungnirReady)
                return;

            int rank = Mathf.Min(gungnirRank, 3);
            gungnirReady = now + ScaleSkillCooldown(4f - rank * 0.5f);
            CastGungnir(rank);
            if (RollAnsuzRepeat(ref gungnirReady))
                CastGungnir(rank);
        }

        private void CastGungnir(int rank)
        {
            Vector3 origin = caster.transform.position;
            if (gungnirRank != 4 || !progress.IsLearned(GungnirEvolution))
            {
                float yaw = Random.Range(0f, 360f);
                Vector3 direction = Quaternion.Euler(0, yaw, 0) * Vector3.forward;
                SweepGodDamage(origin, origin + direction * 10f, 15 + rank * 5);
                return;
            }

            // Five ground impact circles at two-metre intervals, evaluated once per target.
            Vector3 forward = Quaternion.Euler(0, caster.transform.eulerAngles.y, 0) * Vector3.forward;
            for (int i = 1; i <= 5; i++) IndicateRing(origin + forward * (i * 2f), 1.5f);
            CollectTargets(origin, 11.5f);
            foreach (var target in targets)
            {
                Vector3 offset = target.transform.position - origin;
                float projection = Vector3.Dot(offset, forward);
                float center = 2f * Mathf.Clamp(Mathf.RoundToInt(projection / 2f), 1, 5);
                if ((offset - forward * center).sqrMagnitude <= 2.25f)
                    DealGodDamage(target, 35);
            }
        }
    }
}
