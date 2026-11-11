using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string WarriorEvolution = "active_odin_E_evolve";
        private int warriorRank;
        private float warriorReady, warriorAttackAt;
        private bool warriorEvolved;
        private int warriorAttackCount;
        private readonly WarriorAttack[] warriorAttacks = new WarriorAttack[2];
        private readonly List<PreviewPoison> previewPoisons = new List<PreviewPoison>();

        private struct WarriorAttack
        {
            public Health target;
            public bool archer;
        }

        private struct PreviewPoison
        {
            public Health victim;
            public float next;
            public int ticks;
        }

        private bool CanChooseWarrior() => PendingRewards > 0 && warriorRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[warriorRank] && (warriorRank < 3 || progress.IsLearned(WarriorEvolution));

        internal bool TryChooseWarrior()
        {
            if (!CanChooseWarrior())
                return false;

            warriorRank++;
            rewardsSpent++;
            return true;
        }

        private void TryWarrior(float now)
        {
            if (warriorRank == 0 || warriorAttackCount != 0 || now < warriorReady)
                return;

            CollectTargets(caster.transform.position, MissileRange);
            if (targets.Count == 0)
                return;

            warriorEvolved = warriorRank == 4 && progress.IsLearned(WarriorEvolution);
            QueueWarriorAttack();
            if (RollAnsuzRepeat(ref warriorReady))
                QueueWarriorAttack();
            warriorAttackAt = now + 0.25f;
            warriorReady = now + ScaleSkillCooldown(3.5f - Mathf.Min(warriorRank, 3) * 0.5f);
        }

        private void QueueWarriorAttack()
        {
            warriorAttacks[warriorAttackCount++] = new WarriorAttack
            {
                target = targets[Random.Range(0, targets.Count)],
                archer = warriorEvolved && Random.value < 0.75f
            };
        }

        private void TickWarrior(float now)
        {
            if (now < warriorAttackAt)
                return;

            int count = warriorAttackCount;
            warriorAttackCount = 0;
            for (int i = 0; i < count; i++)
            {
                var attack = warriorAttacks[i];
                warriorAttacks[i] = default;
                var target = attack.target;
                if (target && target.CurrentHealth > 0)
                {
                    StrikeGodDamage(target.transform.position, warriorEvolved ? 2f : 1f, warriorEvolved ? 20 : 14);
                    if (attack.archer && target && target.CurrentHealth > 0)
                    {
                        DealGodDamage(target, 20);
                        if (target && target.CurrentHealth > 0 && Random.value < 0.3f)
                            ApplyPreviewPoison(target, now);
                    }
                }
            }

        }

        private void ApplyPreviewPoison(Health target, float now) =>
            previewPoisons.Add(new PreviewPoison { victim = target, next = now + 0.3f, ticks = 10 });

        private void TickPreviewPoisons(float now)
        {
            for (int i = previewPoisons.Count - 1; i >= 0; i--)
            {
                var poison = previewPoisons[i];
                while (poison.victim && poison.victim.CurrentHealth > 0 && poison.ticks > 0 && now >= poison.next)
                {
                    DealGodDamage(poison.victim, 3);
                    poison.ticks--;
                    poison.next += 0.3f;
                }

                if (!poison.victim || poison.victim.CurrentHealth <= 0 || poison.ticks == 0)
                    previewPoisons.RemoveAt(i);
                else
                    previewPoisons[i] = poison;
            }
        }

        private void ClearWarrior()
        {
            for (int i = 0; i < warriorAttackCount; i++)
                warriorAttacks[i] = default;
            warriorAttackCount = 0;
        }
    }
}
