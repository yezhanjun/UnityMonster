using System.Collections.Generic;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        public const string TotemUnlock = "active_berserker_E";
        public const string TotemEvolution = "active_berserker_E_evolve";
        private int totemRank;
        private float totemReady;
        private readonly List<Totem> totems = new List<Totem>(2);

        private struct Totem
        {
            public Vector3 center;
            public float nextTick, expires;
            public int hooks, damage;
        }

        private bool CanChooseTotem() => AllowsRaidClass(RaidClass.Berserker) && PendingRewards > 0 && totemRank < RequiredLevels.Length &&
            raidLevel >= RequiredLevels[totemRank] && progress.IsLearned(TotemUnlock) &&
            (totemRank < 3 || progress.IsLearned(TotemEvolution));

        internal bool TryChooseTotem()
        {
            if (!CanChooseTotem())
                return false;

            totemRank++;
            rewardsSpent++;
            return true;
        }

        private void TryTotem(float now)
        {
            if (totemRank == 0 || now < totemReady || !progress.IsLearned(TotemUnlock))
                return;

            int rank = Mathf.Min(totemRank, 3);
            bool evolved = totemRank == 4 && progress.IsLearned(TotemEvolution);
            totemReady = now + ScaleSkillCooldown(evolved ? 2f : 10f);
            totems.Add(new Totem
            {
                center = caster.transform.position,
                nextTick = now + 1f,
                expires = now + (evolved ? 3f : 2f + rank),
                hooks = evolved ? 4 : 1 + 2 * rank,
                damage = evolved ? 14 : 10
            });
        }

        private void TickTotems(float now)
        {
            for (int i = totems.Count - 1; i >= 0; i--)
            {
                var totem = totems[i];
                if (now >= totem.nextTick && totem.nextTick <= totem.expires)
                {
                    PullTargets(totem.center, 8f, totem.hooks);
                    Strike(totem.center, 1.5f, ScaleClassSkillDamage(totem.damage));
                    totem.nextTick += 1f;
                    totems[i] = totem;
                }

                if (now >= totem.expires)
                    totems.RemoveAt(i);
            }
        }

        private void PullTargets(Vector3 center, float radius, int hooks)
        {
            CollectTargets(center, radius);
            for (int i = 0; i < targets.Count && hooks > 0; i++)
            {
                Health target = targets[i];
                if (target is CharacterInstance character && !character.BOT)
                    continue;

                Vector3 offset = center - target.transform.position;
                offset.y = 0;
                float distance = offset.magnitude;
                if (distance <= 1f)
                    continue;

                Vector3 step = offset * ((distance - 1f) / distance);
                if (target.TryGetComponent<CharacterController>(out var controller) && controller.enabled)
                    controller.Move(step);
                else if (target is not CharacterInstance)
                    target.transform.position += step;

                hooks--;
            }
        }
    }
}

