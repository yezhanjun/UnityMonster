using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        private static readonly int[] DischargeLevels = { 1, 8, 15 };
        private int dischargeRank;
        private float rageDuration, rageUntil, rageNextHeal;

        private bool CanChooseDischarge() => LiveMatch && SelectedRaidClass == RaidClass.Berserker &&
            PendingRewards > 0 && dischargeRank < DischargeLevels.Length &&
            raidLevel >= DischargeLevels[dischargeRank] &&
            (dischargeRank != 0 || progress.IsLearned("passive_berserker_P"));

        internal bool TryChooseDischarge()
        {
            if (!HasLiveCaster || !CanChooseDischarge())
                return false;

            dischargeRank++;
            rewardsSpent++;
            return true;
        }

        private bool IsRaging(float now) => now < rageUntil;

        private void TryStartRage(float now)
        {
            if (rageDuration <= 0f || !HasLiveCaster || IsRaging(now) || Random.value >= 0.10f)
                return;

            rageUntil = now + rageDuration;
            rageNextHeal = now + 1f;
            if (dischargeRank > 0)
                caster.ServerHeal(dischargeRank * 5, caster.netId, allowOverhealBarrier: false);
        }

        private void TickRage(float now)
        {
            if (rageNextHeal <= 0f)
                return;

            while (rageNextHeal <= rageUntil && rageNextHeal <= now)
            {
                caster.ServerHeal(3, caster.netId, allowOverhealBarrier: false);
                rageNextHeal += 1f;
            }

            if (!IsRaging(now))
                ClearRage();
        }

        private void ClearRage()
        {
            rageUntil = 0f;
            rageNextHeal = 0f;
        }

        private void DrawRageStatus(float now)
        {
            if (!IsRaging(now) || Event.current.type != EventType.Repaint)
                return;

            int seconds = Mathf.CeilToInt(rageUntil - now);
            GUI.Label(new Rect(16, 50, 260, 26),
                RewardText($"狂暴  {seconds}s", $"Rage  {seconds}s"), RewardLabelStyle);
        }
    }
}
