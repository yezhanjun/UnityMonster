#if UNITY_EDITOR
using System;
using System.Reflection;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class DraugCombatRandomValidation
    {
        internal static bool Run(CharacterInstance owner, CharacterInstance target)
        {
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var combat = owner.GetComponent<Skeleton1HCombat>(); var type = typeof(Skeleton1HCombat);
            var ownedField = type.GetField("draugRandom", flags); var savedOwned = ownedField.GetValue(combat);
            var visual = (Skeleton1HVisual)type.GetField("visual", flags).GetValue(combat);
            var schedule = (DraugAttackSchedule)type.GetField("draugSchedule", flags).GetValue(combat);
            var update = type.GetMethod("Update", flags);
            Vector3 op = owner.transform.position, tp = target.transform.position;
            int oh = owner.CurrentHealth, th = target.CurrentHealth;
            bool block = owner.Block; var global = UnityEngine.Random.state;
            float attack = schedule.AttackRemaining, skill = schedule.SkillRemaining;
            bool passed = true; int cases = 0, arrows = 0, flasks = 0;
            void Check(bool value, string why) { if (!value) { passed = false; Debug.LogError("DRAUG_COMBAT_RANDOM_FAIL " + why); } }
            void Clock(string name, float value) => typeof(DraugAttackSchedule).GetProperty(name, flags).SetValue(schedule, value);
            int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);
            try
            {
                for (int seed = 0; seed < 16; seed++)
                for (int cooling = 0; cooling < 2; cooling++)
                {
                    owner.CurrentHealth = target.CurrentHealth = 1000;
                    owner.transform.position = new Vector3(400, 100, 400); target.transform.position = new Vector3(400, 100, 406);
                    Physics.SyncTransforms(); owner.BlockCharacter(false); combat.SetGmCombat(true);
                    visual.Play(Skeleton1HVisual.Motion.Hit, true); visual.Play(Skeleton1HVisual.Motion.Idle, true);
                    var owned = new DraugRandomSource(seed); ownedField.SetValue(combat, owned);
                    var reference = new System.Random(seed);
                    bool expectedFlask = cooling == 0 && reference.Next(1000000) / 999999f < DraugAttackSchedule.Chance;
                    Clock("AttackRemaining", 0); Clock("SkillRemaining", cooling == 0 ? 0 : 100);
                    type.GetField("nextAttack", flags).SetValue(combat, Time.time - 1);
                    type.GetField("phaseEnds", flags).SetValue(combat, Time.time - 1);
                    string before = JsonUtility.ToJson(UnityEngine.Random.state);
                    update.Invoke(combat, null);
                    Check(schedule.Current == (expectedFlask ? DraugAttackSchedule.Action.Flask : DraugAttackSchedule.Action.Arrow), "wrong action seed=" + seed);
                    Check(before == JsonUtility.ToJson(UnityEngine.Random.state), "chance touched Unity RNG");
                    Check(owned.IsInitialized == (cooling == 0), "cooldown sample consumption mismatch");
                    Check(Bits(owned.Value) == Bits(reference.Next(1000000) / 999999f), "owned stream advanced incorrectly");
                    if (expectedFlask) flasks++; else arrows++;
                    combat.SetGmCombat(false); cases++;
                }
                Check(cases == 32 && arrows > 16 && flasks > 0, "selection coverage incomplete");
            }
            finally
            {
                combat.SetGmCombat(false); ownedField.SetValue(combat, savedOwned); owner.BlockCharacter(block);
                Clock("AttackRemaining", attack); Clock("SkillRemaining", skill);
                owner.CurrentHealth = oh; target.CurrentHealth = th; owner.transform.position = op; target.transform.position = tp;
                UnityEngine.Random.state = global; Physics.SyncTransforms();
            }
            Debug.Log($"DRAUG_COMBAT_RANDOM_{(passed ? "PASS" : "FAILED")} cases={cases} arrows={arrows} flasks={flasks} ownedStream/cooldown/noUnityConsumption");
            return passed;
        }
    }
}
#endif
