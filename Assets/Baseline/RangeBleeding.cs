using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // A victim owns its active stacks. The original attacker retains kill credit.
    public sealed class RangeBleeding : MonoBehaviour
    {
        const int TickCount = 12;
        const float TickInterval = .25f;
        enum DotKind : byte { Bleed = 1, Burn = 2, Collapse = 4 }

        struct Stack
        {
            public Health Attacker;
            public int RemainingDamage, RemainingTicks;
            public float NextTick, Interval;
            public DotKind Kind;
        }

        readonly List<Stack> stacks = new List<Stack>(4);
        Health victim;

        void Awake() => victim = GetComponent<Health>();

        public void Add(Health attacker, float baseDamage, float procCoefficient)
        {
            int ticks = Mathf.Max(1, Mathf.CeilToInt(TickCount * procCoefficient));
            int damage = (int)System.Math.Min(int.MaxValue, System.Math.Round((double)baseDamage * .2d * ticks));
            AddStack(attacker, damage, ticks, TickInterval, DotKind.Bleed);
        }

        internal void AddBurn(Health attacker, int damage, int ticks)
        {
            if (attacker && attacker.TryGetComponent<RangeResearchItems>(out var items))
                damage = items.ScaleBurnDamage(damage);
            AddStack(attacker, damage, ticks, .2f, DotKind.Burn);
        }

        internal void AddIgnite(Health attacker, int hitDamage, float attackPower)
        {
            int total = Mathf.Max(1, hitDamage / 2);
            float perTick = Mathf.Max(1f, Mathf.Min(.1f * attackPower, .01f * victim.MaxHealth));
            int ticks = (int)System.Math.Ceiling(total / (double)perTick);
            AddBurn(attacker, total, ticks);
        }

        internal void ClearDebuffs()
        {
            stacks.Clear();
            enabled = false;
        }

        internal bool HasBurn()
        {
            foreach (var stack in stacks)
                if (stack.Kind == DotKind.Burn && stack.Attacker) return true;
            return false;
        }

        internal void AddCollapse(Health attacker, int damage)
        {
            if (!victim.isServer || victim.CurrentHealth <= 0) return;
            AddStack(attacker, damage, 1, 3f, DotKind.Collapse);
            if (attacker is CharacterInstance source)
                source.RpcRainNeedletick(victim.transform.position, false);
        }

        internal int ActiveDebuffCount()
        {
            int kinds = 0;
            foreach (var stack in stacks)
                if (stack.Attacker) kinds |= (int)stack.Kind;
            return (kinds & 1) + ((kinds >> 1) & 1) + ((kinds >> 2) & 1);
        }

        internal bool HasBleed()
        {
            foreach (var stack in stacks)
                if (stack.Attacker && stack.Kind == DotKind.Bleed) return true;
            return false;
        }

        void AddStack(Health attacker, int damage, int ticks, float interval, DotKind kind)
        {
            if (!victim.isServer || victim.CurrentHealth <= 0) return;
            stacks.Add(new Stack
            {
                Attacker = attacker,
                RemainingDamage = damage,
                RemainingTicks = ticks,
                NextTick = Time.time + interval,
                Interval = interval,
                Kind = kind
            });
            enabled = true;
        }

        void Update()
        {
            for (int i = stacks.Count - 1; i >= 0; i--)
            {
                if (!victim.isServer || victim.CurrentHealth <= 0)
                {
                    stacks.Clear();
                    break;
                }
                var stack = stacks[i];
                if (!stack.Attacker)
                {
                    stacks.RemoveAt(i);
                    continue;
                }
                while (stack.RemainingTicks > 0 && Time.time >= stack.NextTick && victim.CurrentHealth > 0)
                {
                    int damage = stack.RemainingDamage / stack.RemainingTicks;
                    stack.RemainingDamage -= damage;
                    stack.RemainingTicks--;
                    stack.NextTick += stack.Interval;
                    if (stack.Kind == DotKind.Collapse && stack.Attacker is CharacterInstance source)
                        source.RpcRainNeedletick(victim.transform.position, true);
                    if (damage > 0) victim.ServerApplyRangeDot(damage,
                        stack.Kind == DotKind.Burn ? AttackType.explosion : AttackType.melee, stack.Attacker);
                }
                if (stack.RemainingTicks == 0 || victim.CurrentHealth <= 0) stacks.RemoveAt(i);
                else stacks[i] = stack;
            }
            if (stacks.Count == 0) enabled = false;
        }
    }
}
