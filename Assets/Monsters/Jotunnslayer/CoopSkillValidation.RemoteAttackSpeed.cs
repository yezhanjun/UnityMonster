#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.Reflection;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed partial class CoopSkillValidation
    {
        IEnumerator RunRemoteAttackSpeedHost(PlayerInstance player, SkillBattlePreview runtime, CharacterInstance owner, CharacterInstance host)
        {
            var profile = SkillMatchSession.Active.ValidationProfile(player);
            int level = runtime.RaidLevel, choices = runtime.PendingRewards;
            for (int phase = 0; phase < 3; phase++)
            {
                if (phase == 1) profile.Reset();
                if (phase == 2)
                    foreach (string node in AttackSpeedNodes)
                        if (!profile.TryLearn(node)) { Fail("Remote attack speed relearn unavailable"); yield break; }
                float expected = phase == 1 ? 1f : 1.57f;
                if (!Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(owner), expected) ||
                    !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(host), 1f) ||
                    runtime.RaidLevel != level || runtime.PendingRewards != choices)
                { Fail("Remote attack speed server stats/host isolation invalid phase=" + phase); yield break; }
                WriteSignal("attack-phase-" + phase, "ready");
                yield return WaitDashSignal("attack-observed-" + phase);
                if (failed) yield break;
                Debug.Log("JOTUNN_REMOTE_ATTACK_SPEED_SERVER_OK phase=" + phase + " speed=" + expected +
                    " host=1 sameCharacter=True raidPreserved=True");
            }
            WriteSignal("attack-complete", "ready");
        }

        IEnumerator RunRemoteAttackSpeedClient(RemoteSkillPanel panel, PlayerInstance player)
        {
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var speedProperty = typeof(Item).GetProperty("OwnerAttackSpeed", flags);
            var cooldownProperty = typeof(Gun).GetProperty("PrimaryCooldown", flags);
            var baseCooldownField = typeof(Item).GetField("coolDown", flags);
            var timerField = typeof(Item).GetField("coolDownTimer", flags);
            var ammoField = typeof(Item).GetField("CurrentAmmo", flags);
            var actor = player.MyCharacter;
            Gun gun = null;
            float deadline = Time.realtimeSinceStartup + 10;
            while (!(gun = actor.CharacterItemManager.CurrentlyUsedItem as Gun) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!gun || speedProperty == null || cooldownProperty == null || baseCooldownField == null || timerField == null || ammoField == null)
            { Fail("Remote equipped gun attack speed inspection unavailable"); yield break; }
            for (int phase = 0; phase < 3; phase++)
            {
                yield return WaitDashSignal("attack-phase-" + phase);
                if (failed) yield break;
                float expected = phase == 1 ? 1f : 1.57f;
                deadline = Time.realtimeSinceStartup + 5;
                while ((!Mathf.Approximately(panel.Snapshot.attackSpeed, expected) ||
                        !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), expected)) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                float actual = SkillBattlePreview.RaidAttackSpeedMultiplier(actor);
                float itemSpeed = (float)speedProperty.GetValue(gun);
                float baseCooldown = (float)baseCooldownField.GetValue(gun);
                float actualCooldown = (float)cooldownProperty.GetValue(gun);
                if (!Mathf.Approximately(actual, expected) || !Mathf.Approximately(itemSpeed, actor.ResearchAttackSpeed * expected) ||
                    !Mathf.Approximately(actualCooldown, baseCooldown / itemSpeed) || player.MyCharacter != actor)
                { Fail("Remote attack speed did not reach owner weapon phase=" + phase + " speed=" + actual + " item=" + itemSpeed); yield break; }
                deadline = Time.realtimeSinceStartup + 5;
                while ((float)timerField.GetValue(gun) > Time.time && Time.realtimeSinceStartup < deadline) yield return null;
                int ammo = (int)ammoField.GetValue(gun);
                float shotAt = Time.time;
                gun.PushLeftTrigger(); // Production trigger, ammo consumption and cooldown path.
                float scheduled = (float)timerField.GetValue(gun) - shotAt;
                if ((int)ammoField.GetValue(gun) != ammo - 1 || Mathf.Abs(scheduled - actualCooldown) > .005f)
                { Fail("Remote weapon trigger did not consume one round with the computed cooldown phase=" + phase); yield break; }
                Debug.Log("JOTUNN_REMOTE_ATTACK_SPEED_CLIENT_OK phase=" + phase + " speed=" + actual +
                    " item=" + itemSpeed + " cooldown=" + actualCooldown + " actualShot=True ammoConsumed=1");
                WriteSignal("attack-observed-" + phase, "done");
            }
            yield return WaitDashSignal("attack-complete");
        }
    }
}
#endif
