using System;
using System.Collections;
using UnityEngine;
using Mirror;
namespace MTPSKIT.Gameplay
{
    public sealed class HeroSprintSmoke : MonoBehaviour
    {
        CharacterInstance character;
        CharacterMotor motor;
        PlayerGameplayInput input;
        Vector2 movement;
        bool driving, combat, error;
        float cameraYaw;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--sprint-smoke") < 0) return;
            var go = new GameObject("Sprint acceptance"); DontDestroyOnLoad(go); go.AddComponent<HeroSprintSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) error = true; }
        static void Check(bool ok, string message)
        { if (!ok) throw new InvalidOperationException("SPRINT_FAILED " + message); }
        void Update()
        {
            if (!driving) return;
            input.MovementInput = movement; input.LookInput = new Vector2(0, cameraYaw);
            character.CharacterItemManager.UsePrimaryInput = combat;
            input.ApplyLocomotion(true, combat);
        }
        IEnumerator Forward()
        {
            movement = Vector2.up; cameraYaw = character.lookInput.y;
            yield return new WaitForSeconds(.3f);
            Check(motor.ServerStartFourWayRoll(0, character.lookInput.y, true), "forward roll refused");
            yield return new WaitForSeconds(1.4f);
            while (motor.IsFourWayRolling) yield return null;
            Check(motor.HeroSprint && character.IsRunning, "forward roll did not enter persistent Sprint");
        }
        IEnumerator Turn(float angle)
        {
            float start = character.lookInput.y;
            Debug.Log("SPRINT_TURN_BEGIN angle=" + angle + " pos=" + character.transform.position + " ground=" + character.IsGrounded + " sprint=" + motor.HeroSprint + " roll=" + motor.IsFourWayRolling + " yaw=" + start);
            cameraYaw = start + angle;
            float until = Time.realtimeSinceStartup + .5f;
            while (!motor.SprintTurning && Time.realtimeSinceStartup < until) yield return null;
            Check(motor.SprintTurning, "turn not entered angle=" + angle + " pos=" + character.transform.position + " ground=" + character.IsGrounded + " sprint=" + motor.HeroSprint + " yaw=" + character.lookInput.y);
            Check(Mathf.Abs(Mathf.DeltaAngle(start, character.lookInput.y)) < 3f, "turned before rotation window");
            yield return new WaitForSeconds(.24f);
            Check(character.GetComponent<RangeRobotVisual>().RiflePresentation.TurnWeight > .8f, "turn clip invisible");
            yield return new WaitForSeconds(1f);
            Check(!motor.SprintTurning && motor.HeroSprint, "turn did not return to Sprint");
            Check(Mathf.Abs(Mathf.DeltaAngle(start + angle, character.lookInput.y)) < 5f, "turn final facing wrong");
            Debug.Log("SPRINT_TURN_PASSED angle=" + angle + " pos=" + character.transform.position);
        }
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 100f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.myPlayerInstance && GameManager.myPlayerInstance.MyCharacter, "player missing");
            if (!NetworkServer.active)
            {
                var remoteFloor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                remoteFloor.transform.position = new Vector3(0, 49, 0);
                remoteFloor.transform.localScale = new Vector3(300, 1, 300);
                bool sawSprint = false, sawTurn = false;
                deadline = Time.realtimeSinceStartup + 45f;
                while (Time.realtimeSinceStartup < deadline && (!sawSprint || !sawTurn))
                {
                    foreach (var pair in GameManager.Players)
                    {
                        var peer = pair.Value;
                        if (!peer || peer == GameManager.myPlayerInstance || !peer.MyCharacter || peer.BOT) continue;
                        var remoteMotor = peer.MyCharacter.GetComponent<CharacterMotor>();
                        var remotePose = peer.MyCharacter.GetComponent<RangeRobotVisual>().RiflePresentation;
                        sawSprint |= remoteMotor.HeroSprint && remotePose != null && remotePose.Pose == "Sprint";
                        sawTurn |= remoteMotor.SprintTurning && remotePose != null && remotePose.TurnWeight > .5f;
                    }
                    yield return null;
                }
                Check(sawSprint && sawTurn, "remote Sprint/turn presentation missing sprint=" + sawSprint + " turn=" + sawTurn);
                Debug.Log("SPRINT_REMOTE_PASSED"); yield break;
            }
            var player = GameManager.myPlayerInstance; character = player.MyCharacter;
            player.ServerSetRangeHero(12);
            character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
            yield return new WaitForSeconds(2f);
            motor = character.GetComponent<CharacterMotor>();
            input = PlayerGameplayInput.Instance; input.enabled = false;
            character.RangeInvulnerable = true;
            // Wide isolated floor permits root-motion turns without arena obstacles.
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Sprint test floor"; floor.layer = 0;
            floor.transform.position = new Vector3(0, 49, 0); floor.transform.localScale = new Vector3(300, 1, 300);
            character.transform.position = new Vector3(0, 51, 0);
            Check(motor.ServerSetHeroControlMode(false), "free mode unavailable");
            driving = true;
            yield return new WaitForSeconds(1f);
            deadline = Time.realtimeSinceStartup + 30f;
            while (GameManager.Players.Count < 2 && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSeconds(5f);
            foreach (bool free in new[] { true, false })
            {
                movement = Vector2.zero;
                Check(motor.ServerStartFourWayRoll(0, character.lookInput.y, free), "chain initial roll refused");
                Check(!motor.ServerStartFourWayRoll(2, character.lookInput.y, false), "chain allowed before window");
                yield return new WaitForSeconds(motor.RollProfile.directions[0].clip.length * (free ? .2f : .4f) + .03f);
                Check(motor.CanReenterRoll, "chain input gate remained locked");
                // Exercise the actual owner request and server command, not just the helper.
                int accepted = motor.RollAcceptedCount;
                motor.RequestMovementRoll(Vector2.left, character.lookInput.y, !free);
                float requestDeadline = Time.realtimeSinceStartup + .5f;
                while (motor.RollAcceptedCount == accepted && Time.realtimeSinceStartup < requestDeadline) yield return null;
                Check(motor.IsFourWayRolling && motor.RollElapsed < .2f &&
                    motor.RollDirection == (free ? 0 : 2), "chain owner request lost free=" + free + " owned=" + motor.isOwned + " time=" + motor.RollElapsed + " direction=" + motor.RollDirection + " grounded=" + character.IsGrounded + " reload=" + character.IsReloading + " reject=" + motor.RollRejectReason);
                Debug.Log("ROLL_REENTRY_PASSED free=" + free);
                yield return new WaitForSeconds(1.5f);
            }
            // Reverse immediately after the forward-roll handoff, while its visual tail remains.
            foreach (float reverse in new[] { 170f, -170f })
            {
                movement = Vector2.up; cameraYaw = character.lookInput.y;
                yield return new WaitForSeconds(.3f);
                Check(motor.ServerStartFourWayRoll(0, character.lookInput.y, true), "early turn roll refused");
                yield return new WaitForSeconds(motor.RollProfile.directions[0].clip.length * .2f + .03f);
                Check(motor.IsFourWayRolling && motor.HeroSprint, "early turn handoff not reached");
                yield return Turn(reverse);
                Check(!motor.IsFourWayRolling, "accepted turn kept outgoing roll");
                Debug.Log("SPRINT_EARLY_HANDOFF_TURN_PASSED angle=" + reverse);
                movement = Vector2.zero; yield return new WaitForSeconds(2f);
            }
            // A single free forward dodge with no held movement must enter SprintStop.
            movement = Vector2.zero;
            Check(motor.ServerStartFourWayRoll(0, character.lookInput.y, true), "tap roll refused");
            yield return new WaitForSeconds(motor.RollProfile.directions[0].clip.length * .2f + .2f);
            Check(character.GetComponent<RangeRobotVisual>().RiflePresentation.SprintStopActive,
                "tap forward roll did not enter SprintStop");
            Debug.Log("FORWARD_TAP_SPRINT_STOP_PASSED");
            yield return new WaitForSeconds(2f);
            yield return Forward();
            var pose = character.GetComponent<RangeRobotVisual>().RiflePresentation;
            Check(pose.Pose == "Sprint", "sprint presentation missing " + pose.Pose);
            Vector3 position = character.transform.position;
            yield return new WaitForSeconds(.3f);
            Check(Vector3.Distance(position, character.transform.position) > .2f, "Sprint has no root travel");
            // Keep changing desired heading so Arc remains observable under normal input smoothing.
            for (int n = 0; n < 25; n++) { cameraYaw += 4f; yield return new WaitForSeconds(.02f); }
            Check(pose.Arc > .01f && pose.ArcWeight > .01f, "right Sprint Arc missing");
            for (int n = 0; n < 50; n++) { cameraYaw -= 4f; yield return new WaitForSeconds(.02f); }
            Check(pose.Arc < -.01f && pose.ArcWeight > .01f, "left Sprint Arc missing");
            yield return new WaitForSeconds(.4f);
            yield return Turn(170f); yield return Turn(-170f);
            movement = Vector2.zero; yield return new WaitForSeconds(.3f);
            Check(!motor.HeroSprint, "release did not end Sprint");
            Check(pose.SprintStopActive && pose.SprintStopWeight > .8f, "SprintEnd missing");
            Check(pose.SprintStopSample >= pose.SprintStopClip.length * motor.SprintProfile.sprintStopStart,
                "SprintEnd ignored start offset");
            Debug.Log("SPRINT_END_VISIBLE_PASSED clip=" + pose.SprintStopClip.name + " weight=" + pose.SprintStopWeight);
            // A neutral keyboard frame must not turn a reversal into ordinary Run.
            cameraYaw = character.lookInput.y + 170f; movement = Vector2.up;
            yield return new WaitForSeconds(1.4f);
            Check(motor.HeroSprint, "SprintEnd reversal lost Sprint session");
            movement = Vector2.zero; yield return new WaitForSeconds(.3f);
            cameraYaw = character.lookInput.y; movement = Vector2.up;
            yield return new WaitForSeconds(.3f);
            Check(!motor.HeroSprint && !pose.SprintStopActive && pose.Pose == "Run", "SprintEnd ordinary reinput must return Run");
            Debug.Log("SPRINT_END_REINPUT_PASSED");
            movement = Vector2.zero; yield return new WaitForSeconds(.5f);
            yield return Forward(); combat = true; yield return new WaitForSeconds(.2f);
            Check(!motor.HeroSprint, "fire did not cancel Sprint");
            combat = false; movement = Vector2.zero; yield return new WaitForSeconds(1.2f);
            movement = Vector2.up; yield return new WaitForSeconds(.3f);
            Check(motor.ServerStartFourWayRoll(2, character.lookInput.y), "side roll refused");
            yield return new WaitForSeconds(1.4f);
            Check(!motor.HeroSprint, "side roll incorrectly entered Sprint");
            for (int n = 0; n < 25; n++) { cameraYaw += 4f; yield return new WaitForSeconds(.02f); }
            Check(pose.Arc > .01f && pose.ArcWeight > .01f, "Run Arc missing");
            movement = Vector2.zero; yield return new WaitForSeconds(.5f);
            yield return Forward(); motor.Jump(); yield return new WaitForSeconds(.1f);
            Check(!motor.HeroSprint, "jump did not end Sprint");
            yield return new WaitForSeconds(2f);
            movement = Vector2.zero;
            yield return new WaitForSeconds(.5f);
            foreach (float phase in new[] { .1f, .5f })
            {
                yield return Forward();
                float until = Time.realtimeSinceStartup + 3f;
                while (Mathf.Abs(Mathf.DeltaAngle(pose.GaitPhase * 360f, phase * 360f)) > 6f && Time.realtimeSinceStartup < until)
                    yield return null;
                bool expectedRight = motor.SprintProfile.SprintStopRightFootAt(pose.GaitPhase, false);
                movement = Vector2.zero; yield return new WaitForSeconds(.2f);
                Check(pose.SprintStopActive && pose.SprintStopRight == expectedRight && pose.SprintStopWeight > .8f,
                    "SprintEnd phase foot mismatch " + phase);
                Debug.Log("SPRINT_END_FOOT_PASSED phase=" + phase + " right=" + pose.SprintStopRight);
                yield return new WaitForSeconds(pose.SprintStopClip.length + 1.5f);
                Check(!pose.SprintStopActive && pose.SprintStopWeight < .01f, "SprintEnd failed to settle idle");
            }
            driving = false; character.SetMovementInput(Vector2.zero); input.enabled = true;
            Check(!error, "runtime errors"); Debug.Log("SPRINT_ACCEPTANCE_PASSED");
        }
    }
}

