#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System.Collections;
using System.IO;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed partial class CoopSkillValidation
    {
        static float HorizontalDistance(Vector3 a, Vector3 b) => Vector3.ProjectOnPlane(a - b, Vector3.up).magnitude;

        IEnumerator WaitDashSignal(string name, float seconds = 10)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!File.Exists(Path.Combine(signalDirectory, name)) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(Path.Combine(signalDirectory, name))) Fail("Remote dash signal timed out: " + name);
        }

        IEnumerator RunRemoteDashHost(SkillBattlePreview runtime, CharacterInstance owner, CharacterInstance host)
        {
            Vector3 hostStart = host.transform.position;
            WriteSignal("dash-setup", "ready");
            yield return WaitDashSignal("dash-positioned");
            if (failed) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            Vector3 firstStart = owner.transform.position;
            WriteSignal("dash-go", "ready");
            yield return WaitDashSignal("dash-first-done");
            if (failed) yield break;
            float first = HorizontalDistance(owner.transform.position, firstStart);
            if (first < 3.6f || first > 4.3f || runtime.DashCharges != 0 || HorizontalDistance(host.transform.position, hostStart) > .05f)
            { Fail("Remote dash server position/budget invalid first=" + first + " charges=" + runtime.DashCharges); yield break; }
            float deadline = Time.realtimeSinceStartup + 6;
            while (runtime.DashCharges == 0 && Time.realtimeSinceStartup < deadline) yield return null;
            if (runtime.DashCharges != 1) { Fail("Remote dash server cooldown did not recharge"); yield break; }
            Vector3 secondStart = owner.transform.position;
            WriteSignal("dash-second-go", "ready");
            yield return WaitDashSignal("dash-second-done");
            if (failed) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            float second = HorizontalDistance(owner.transform.position, secondStart);
            if (second < 3.6f || second > 4.3f || runtime.DashCharges != 0 || HorizontalDistance(host.transform.position, hostStart) > .05f)
            { Fail("Remote dash server second position/budget invalid distance=" + second); yield break; }
            Debug.Log("JOTUNN_REMOTE_DASH_SERVER_OK first=" + first + " second=" + second +
                " charges=0 recharged=True hostStationary=True ownerPositionReplicated=True");
            WriteSignal("dash-complete", "done");
        }

        IEnumerator SendDashPair(RemoteSkillPanel panel, PlayerInstance player)
        {
            var before = panel.Snapshot;
            panel.Request(JotunnSkillAction.Dash, -1);
            player.CmdJotunnSkillAction(before.session, before.revision, JotunnSkillAction.Dash, -1);
            float deadline = Time.realtimeSinceStartup + 5;
            while (panel.Snapshot.revision < before.revision + 2 && Time.realtimeSinceStartup < deadline) yield return null;
            if (panel.Snapshot.revision < before.revision + 2) Fail("Remote dash pair acknowledgment missing");
        }

        IEnumerator RunRemoteDashClient(RemoteSkillPanel panel, PlayerInstance player)
        {
            yield return WaitDashSignal("dash-setup");
            if (failed) yield break;
            var actor = player.MyCharacter;
            var motor = actor.GetComponent<CharacterMotor>();
            if (!motor.enabled || !TryFindOpenArenaPosition(actor.transform.position, out var ground))
            { Fail("Remote owner movement motor or clear terrain unavailable"); yield break; }
            var controller = actor.GetComponent<CharacterController>();
            controller.enabled = false;
            actor.transform.position = ground + Vector3.up * .1f;
            controller.enabled = true;
            actor.SetMovementInput(Vector2.zero);
            yield return new WaitForSecondsRealtime(.6f);
            float deadline = Time.realtimeSinceStartup + 5;
            while (!actor.IsGrounded && Time.realtimeSinceStartup < deadline) yield return null;
            if (!actor.IsGrounded) { Fail("Remote dash actor did not settle on actual arena ground"); yield break; }
            WriteSignal("dash-positioned", "ready");
            yield return WaitDashSignal("dash-go");
            if (failed) yield break;
            Vector3 firstStart = actor.transform.position;
            yield return SendDashPair(panel, player);
            if (failed) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            float first = HorizontalDistance(actor.transform.position, firstStart);
            if (first < 3.6f || first > 4.3f) { Fail("Approved remote dash did not move once: " + first); yield break; }
            Vector3 cooldownStart = actor.transform.position;
            yield return SendDashPair(panel, player);
            if (failed) yield break;
            yield return new WaitForSecondsRealtime(.4f);
            if (HorizontalDistance(actor.transform.position, cooldownStart) > .05f)
            { Fail("Remote cooldown or replay request moved the owner again"); yield break; }
            WriteSignal("dash-first-done", "done");
            yield return WaitDashSignal("dash-second-go");
            if (failed) yield break;
            Vector3 secondStart = actor.transform.position;
            yield return SendDashPair(panel, player);
            if (failed) yield break;
            yield return new WaitForSecondsRealtime(.5f);
            float second = HorizontalDistance(actor.transform.position, secondStart);
            if (second < 3.6f || second > 4.3f) { Fail("Remote recharged dash distance invalid: " + second); yield break; }
            Debug.Log("JOTUNN_REMOTE_DASH_CLIENT_OK first=" + first + " second=" + second +
                " duplicateAndCooldownRejected=True realCharacterController=True");
            WriteSignal("dash-second-done", "done");
            yield return WaitDashSignal("dash-complete");
        }
    }
}
#endif
