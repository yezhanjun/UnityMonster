using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed class FourWayRollSmoke : MonoBehaviour
    {
        bool error;
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--four-way-roll-smoke") < 0) return;
            var go = new GameObject("4wayRoll acceptance"); DontDestroyOnLoad(go); go.AddComponent<FourWayRollSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("FOUR_WAY_ROLL_FAILED " + message); }
        static PlayerInstance Peer(PlayerInstance self)
        {
            foreach (var pair in GameManager.Players)
                if (pair.Value && pair.Value != self && !pair.Value.BOT && pair.Value.MyCharacter) return pair.Value;
            return null;
        }
        IEnumerator Start()
        {
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "FourWayRollAcceptance"); Directory.CreateDirectory(output);
            float deadline = Time.realtimeSinceStartup + 100f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            var player = GameManager.myPlayerInstance;
            Check(player && player.MyCharacter, "player missing");
            while (!Peer(player) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Peer(player), "second client missing");
            if (!NetworkServer.active)
            {
                int mask = 0, moveMask = 0;
                while (Time.realtimeSinceStartup < deadline + 90f)
                {
                    var peer = Peer(player);
                    var visual = peer ? peer.MyCharacter.GetComponent<RangeRobotVisual>() : null;
                    var motor = peer ? peer.MyCharacter.GetComponent<CharacterMotor>() : null;
                    if (motor && motor.IsFourWayRolling && visual && visual.RollPresentation != null && visual.RollPresentation.Applied)
                        mask |= 1 << motor.RollDirection;
                    if (motor && motor.IsFourWayRolling && motor.RollMoveWeight > .99f)
                        moveMask |= 1 << motor.RollDirection;
                    if (mask == 15 && moveMask == 15)
                    {
                        Check(!error, "remote runtime errors");
                        File.WriteAllText(Path.Combine(output, "remote-result.txt"), "REMOTE_ROLL_DIRECTIONS=4/4\nREMOTE_EARLY_MOVEMENT=4/4\nREMOTE_ANIMATIONS=True\nRUNTIME_ERRORS=0\n");
                        Debug.Log("FOUR_WAY_ROLL_REMOTE_PASSED"); yield break;
                    }
                    yield return null;
                }
                Check(false, "remote did not observe four directions"); yield break;
            }
            yield return new WaitForSeconds(3f);
            var character = player.MyCharacter;
            character.RangeInvulnerable = true;
            var motorSelf = character.GetComponent<CharacterMotor>();
            var input = PlayerGameplayInput.Instance;
            bool wasEnabled = input.enabled; input.enabled = false;
            Vector3 originalPosition = character.transform.position;
            var platform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            platform.name = "Acceptance isolated roll floor"; platform.layer = 0;
            platform.transform.position = new Vector3(0, 49.5f, 0); platform.transform.localScale = new Vector3(100, 1, 100);
            Check(CharacterMotor.SelectRollDirection(Vector2.zero) == 1 &&
                CharacterMotor.SelectRollDirection(Vector2.right) == 3 && CharacterMotor.SelectRollDirection(Vector2.left) == 2 &&
                CharacterMotor.SelectRollDirection(Vector2.up) == 0 && CharacterMotor.SelectRollDirection(Vector2.down) == 1,
                "direction selection");
            character.transform.position = new Vector3(0, 50.1f, 0);
            yield return new WaitForSeconds(.5f);
            for (byte direction = 0; direction < 4; direction++)
            {
                character.transform.position = new Vector3(0, 50.1f, 0); character.SetMovementInput(Vector2.zero);
                character.SetActionKeyCode(ActionCodes.Sprint, true);
                yield return new WaitForSeconds(.2f);
                Check(!character.IsRunning, "Shift still activates sprint");
                Vector3 before = character.transform.position;
                Check(motorSelf.ServerStartFourWayRoll(direction, 0f), "roll request rejected " + direction);
                Check(!motorSelf.ServerStartFourWayRoll(direction, 0f), "repeat accepted while rolling");
                yield return new WaitForSeconds(.4f);
                Check(motorSelf.IsFourWayRolling, "roll state inactive " + direction);
                var visual = character.GetComponent<RangeRobotVisual>();
                Check(visual.RollPresentation != null && visual.RollPresentation.Applied, "roll animation missing " + direction);
                var gun = character.CharacterItemManager.CurrentlyUsedItem as Gun;
                var field = typeof(Item).GetField("CurrentAmmo", BindingFlags.NonPublic | BindingFlags.Instance);
                int ammo = gun ? (int)field.GetValue(gun) : 0;
                if (gun) { gun.PushLeftTrigger(); gun.RequestReload(); }
                yield return new WaitForSeconds(.05f);
                Check(!gun || (int)field.GetValue(gun) < ammo, "fire blocked during roll");
                Check(!character.IsReloading, "reload allowed during roll");
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "Roll-" + direction + ".png")); yield return new WaitForEndOfFrame();
                yield return new WaitForSeconds(motorSelf.RollProfile.Duration(direction) + .2f);
                Check(!motorSelf.IsFourWayRolling && !visual.RollPresentation.Applied, "roll did not release");
                Vector3 travel = character.transform.position - before; travel.y = 0;
                Vector3 expected = motorSelf.RollProfile.directions[direction].Position(motorSelf.RollProfile.Duration(direction));
                Check(travel.magnitude > .5f && Vector3.Dot(travel.normalized, expected.normalized) > .95f, "wrong movement direction " + direction);
                Check(Mathf.Abs(travel.magnitude - expected.magnitude) < .8f, "motion curve mismatch " + direction + " actual=" + travel + " expected=" + expected);
                Debug.Log("FOUR_WAY_ROLL_DIRECTION_PASSED " + direction + " travel=" + travel);
            }
            // Fixed-facing orthogonal movement is locked strafe; free Run now follows body root motion.
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, true);
            // Hold an orthogonal movement input through each dodge. Before the window it
            // must not affect travel; afterwards movement must own travel before roll ends.
            for (byte direction = 0; direction < 4; direction++)
            {
                character.transform.position = new Vector3(0, 50.1f, 0);
                character.SetMovementInput(Vector2.zero);
                yield return new WaitForSeconds(.3f);
                Check(motorSelf.ServerStartFourWayRoll(direction, 0f), "handoff roll request " + direction);
                Vector2 steer = direction < 2 ? Vector2.right : Vector2.up;
                character.lookInput = Vector2.zero; character.SetMovementInput(steer);
                float unlock = motorSelf.RollProfile.directions[direction].clip.length * motorSelf.RollProfile.moveUnlockNormalized;
                yield return new WaitForSeconds(Mathf.Max(.01f, unlock - .08f));
                Check(motorSelf.RollMoveWeight == 0f, "movement opened before window " + direction);
                yield return new WaitForSeconds(.08f + motorSelf.RollProfile.moveBlendDuration + .06f);
                Check(motorSelf.IsFourWayRolling && motorSelf.RollMoveWeight > .99f, "movement did not open early " + direction);
                Vector3 from = character.transform.position;
                yield return new WaitForSeconds(.06f);
                Vector3 delta = character.transform.position - from; delta.y = 0;
                Vector3 wanted = new Vector3(steer.x, 0, steer.y);
                Check(delta.magnitude > .05f && Vector3.Dot(delta.normalized, wanted) > .9f,
                    "movement failed to replace roll travel " + direction + " delta=" + delta);
                character.SetMovementInput(Vector2.zero);
                yield return new WaitForSeconds(.05f);
                Check(motorSelf.RollMoveWeight > .99f, "release restarted roll travel");
                yield return new WaitForSeconds(motorSelf.RollProfile.Duration(direction) + .2f);
                Debug.Log("FOUR_WAY_ROLL_EARLY_MOVE_PASSED " + direction);
            }
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
            // Other formal heroes use the same roll overlay; no second movement controller.
            for (int hero = 13; hero <= 15; hero++)
            {
                Check(player.ServerSetRangeHero(hero), "hero change"); yield return new WaitForSeconds(.4f);
                character.transform.position = new Vector3(0, 50.1f, 0); yield return new WaitForSeconds(.2f);
                Check(motorSelf.ServerStartFourWayRoll(2, 0f), "hero roll " + hero);
                yield return new WaitForSeconds(.3f);
                Check(character.GetComponent<RangeRobotVisual>().RollPresentation.Applied, "hero animation " + hero);
                yield return new WaitForSeconds(2f);
            }
            player.ServerSetRangeHero(12); yield return new WaitForSeconds(.4f);
            character.transform.position = new Vector3(0, 50.1f, 0); yield return new WaitForSeconds(.2f);
            Check(motorSelf.ServerStartFourWayRoll(2, 0f), "cancel test request"); yield return new WaitForSeconds(.2f);
            player.ServerSetRangeHero(13); yield return new WaitForSeconds(.2f);
            Check(!motorSelf.IsFourWayRolling, "hero change did not cancel roll");
            player.ServerSetRangeHero(12);
            character.transform.position = originalPosition; character.SetMovementInput(Vector2.zero);
            character.SetActionKeyCode(ActionCodes.Sprint, false); input.enabled = wasEnabled; Destroy(platform);
            Check(!error, "runtime errors");
            File.WriteAllText(Path.Combine(output, "result.txt"), "DIRECTIONS=4/4\nHEROES=4/4\nSOURCE_MOTION_CURVES=True\nEARLY_MOVE_DIRECTIONS=4/4\nMOVEMENT_BEFORE_EXIT=True\nNO_ROLL_RESTART_ON_RELEASE=True\nSPRINT_DISABLED=True\nFIRE_DURING_ROLL=True\nRELOAD_BLOCKED=True\nREPEAT_REJECTED=True\nEXIT_CLEANUP=True\nHERO_CHANGE_CANCEL=True\nRUNTIME_ERRORS=0\n");
            Debug.Log("FOUR_WAY_ROLL_ACCEPTANCE_PASSED");
        }
    }
}
