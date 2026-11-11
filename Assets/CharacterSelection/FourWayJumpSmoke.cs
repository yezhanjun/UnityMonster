using System;
using System.Collections;
using System.IO;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed class FourWayJumpSmoke : MonoBehaviour
    {
        bool error, drive;
        CharacterInstance character;
        Vector2 movement;
        string output;
        static readonly Vector2[] Directions = { Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right };
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--four-way-jump-smoke") < 0) return;
            var go = new GameObject("Firearm 4way jump acceptance"); DontDestroyOnLoad(go); go.AddComponent<FourWayJumpSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        void Update()
        {
            if (!drive || !character) return;
            character.SetMovementInput(movement); character.lookInput = Vector2.zero;
        }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("FOUR_WAY_JUMP_FAILED " + message); }
        static PlayerInstance Peer(PlayerInstance self)
        {
            foreach (var pair in GameManager.Players)
                if (pair.Value && pair.Value != self && !pair.Value.BOT && pair.Value.MyCharacter) return pair.Value;
            return null;
        }
        IEnumerator Start()
        {
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "FourWayJumpAcceptance"); Directory.CreateDirectory(output);
            float deadline = Time.realtimeSinceStartup + 100f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            var player = GameManager.myPlayerInstance; Check(player && player.MyCharacter, "player missing");
            while (!Peer(player) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Peer(player), "second client missing");
            if (!NetworkServer.active)
            {
                int mask = 0; bool landing = false, loop = false;
                while (Time.realtimeSinceStartup < deadline + 120f)
                {
                    var peer = Peer(player);
                    var visual = peer ? peer.MyCharacter.GetComponent<RangeRobotVisual>() : null;
                    var motor = peer ? peer.MyCharacter.GetComponent<CharacterMotor>() : null;
                    if (motor && visual && visual.JumpPresentation != null && visual.JumpPresentation.Applied)
                    {
                        mask |= 1 << motor.FourJumpDirection;
                        landing |= visual.JumpPresentation.Phase == "Landing";
                        loop |= visual.JumpPresentation.Phase == "Loop";
                    }
                    if (mask == 31 && loop && landing)
                    {
                        Check(!error, "remote runtime errors");
                        File.WriteAllText(Path.Combine(output, "remote-result.txt"), "REMOTE_DIRECTIONS=5/5\nREMOTE_START_LOOP_LAND=True\nRUNTIME_ERRORS=0\n");
                        Debug.Log("FOUR_WAY_JUMP_REMOTE_PASSED"); yield break;
                    }
                    yield return null;
                }
                Check(false, "remote did not observe jump phases/directions"); yield break;
            }
            yield return new WaitForSeconds(3f);
            character = player.MyCharacter; character.RangeInvulnerable = true;
            var motorSelf = character.GetComponent<CharacterMotor>(); var input = PlayerGameplayInput.Instance;
            bool previousInput = input.enabled; input.enabled = false; drive = true;
            // A focus click can leave primary fire held when the scripted test takes over.
            // Release input and let the real post-shot facing hold expire before testing free movement.
            character.CharacterItemManager.UsePrimaryInput = false;
            character.CharacterItemManager.UseSecondaryInput = false;
            character.IsAiming = false;
            yield return new WaitForSeconds(.4f);
            Vector3 originalPosition = character.transform.position;
            float originalHeight = motorSelf.JumpProfile.jumpPower;
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.layer = 0;
            floor.transform.position = new Vector3(0, 49.5f, 0); floor.transform.localScale = new Vector3(100, 1, 100);
            // Reuse a built scene material, so the test geometry does not require an unshipped shader.
            foreach (var renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                if (renderer.gameObject != floor && renderer.sharedMaterial && renderer.sharedMaterial.shader && renderer.sharedMaterial.shader.isSupported)
                { floor.GetComponent<MeshRenderer>().sharedMaterial = renderer.sharedMaterial; break; }
            Check(!motorSelf.GodWarControl, "startup is not conventional mode");
            input.LookInput = Vector2.zero; input.MovementInput = Vector2.right;
            character.lookInput = Vector2.zero;
            input.ApplyLocomotion(true, false);
            Check(character.lookInput.y > .1f, "legacy snap setting overrode conventional facing");
            character.lookInput = Vector2.zero; input.MovementInput = Vector2.zero;
            character.transform.position = new Vector3(0, 50.1f, 0);
            movement = Vector2.up;
            yield return new WaitForSeconds(.5f);
            var rifle = character.GetComponent<RangeRobotVisual>().RiflePresentation;
            Check(rifle != null && rifle.Pose == "Run", "conventional rifle run clip not active");
            movement = Vector2.zero;
            yield return new WaitForSeconds(.2f);
            Check(rifle.RunStopActive && rifle.RunStopWeight > .8f && (rifle.Pose == "RunStopL" || rifle.Pose == "RunStopR"), "conventional stop clip not active");
            yield return new WaitForSeconds(1.5f);
            Check(rifle.Pose == "Stand", "conventional stop did not return to idle");
            motorSelf.JumpProfile.jumpPower = 20f; // Longer real airtime exercises the shared airborne loop as well.
            Check(motorSelf.ServerSetHeroControlMode(true), "set directional mode");
            yield return new WaitForSeconds(.3f);
            Check(GodOfWarHeroCamera.Instance.GodWarMode, "GodOfWar camera did not switch");
            input.MovementInput = Vector2.right;
            input.ApplyLocomotion(true, false);
            Check(Mathf.Abs(character.lookInput.y) < .01f && character.movementInput.x > .9f,
                "GodOfWar did not retain camera-relative strafe");
            input.MovementInput = Vector2.zero;
            motorSelf.ServerSetHeroControlMode(false);
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
            // Ordinary free Run selects gait feet. Both authored running feet must play.
            for (int foot = 0; foot < 3; foot++)
            {
                character.transform.position = new Vector3(0, 50.1f, 0);
                movement = input.MovementInput = foot == 0 ? Vector2.zero : Vector2.up;
                character.IsAiming = false;
                character.CharacterItemManager.UsePrimaryInput = false;
                yield return new WaitForSeconds(.4f);
                float phaseDeadline = Time.realtimeSinceStartup + 2f;
                while (foot > 0 && motorSelf.JumpProfile.RightFootAt(rifle.GaitPhase) != (foot == 2) && Time.realtimeSinceStartup < phaseDeadline)
                    yield return null;
                bool expectedRight = foot == 2;
                motorSelf.Jump(); yield return new WaitForSeconds(.22f);
                Check(!motorSelf.FourJumpDirectional, "N alone selected directional jump");
                Check(foot == 0 ? motorSelf.FourJumpDirection == 0 : motorSelf.FourJumpRightFoot == expectedRight,
                    "regular jump foot selection " + foot);
                Check(character.GetComponent<RangeRobotVisual>().JumpPresentation.Applied, "regular jump pose " + foot);
                yield return new WaitForSeconds(3f);
            }
            motorSelf.ServerSetHeroControlMode(true);
            for (byte direction = 0; direction < 5; direction++)
            {
                character.transform.position = new Vector3(0, 50.1f, 0); movement = Vector2.zero;
                yield return new WaitForSeconds(.3f);
                Check(CharacterMotor.SelectJumpDirection(Directions[direction]) == direction, "direction mapping");
                input.MovementInput = movement = Directions[direction]; input.LookInput = Vector2.zero;
                float before = character.transform.position.y;
                character.IsAiming = direction % 2 == 0;
                character.CharacterItemManager.UsePrimaryInput = direction % 2 != 0;
                motorSelf.Jump();
                character.IsAiming = false;
                character.CharacterItemManager.UsePrimaryInput = false;
                yield return new WaitForSeconds(.22f);
                Check(motorSelf.FourJumpStarted >= 0 && motorSelf.FourJumpDirection == direction, "jump start not synchronized " + direction);
                Check(motorSelf.FourJumpDirectional, "fire/aim did not select directional jump");
                Check(!motorSelf.ServerSetHeroControlMode(false), "mode change allowed in mid jump");
                var visual = character.GetComponent<RangeRobotVisual>();
                Check(visual.JumpPresentation != null && visual.JumpPresentation.Applied && visual.JumpPresentation.Phase == "Start", "start animation missing");
                Check(character.transform.position.y > before + .3f, "real jump did not move upwards");
                float takeoffFacing = motorSelf.AirFacing;
                input.LookInput = new Vector2(0, takeoffFacing + 90f);
                input.ApplyLocomotion(true, false);
                Check(Mathf.Abs(Mathf.DeltaAngle(character.lookInput.y, takeoffFacing)) < .01f,
                    "camera/input rotated airborne character");
                if (direction == 1)
                {
                    var gun = character.CharacterItemManager.CurrentlyUsedItem as Gun;
                    Check(gun, "air shot gun missing");
                    gun.PushLeftTrigger();
                    Check(Mathf.Abs(Mathf.DeltaAngle(motorSelf.AirFacing, takeoffFacing + 90f)) < .01f,
                        "air shot did not turn toward camera");
                    input.LookInput = new Vector2(0, takeoffFacing + 180f);
                    input.ApplyLocomotion(true, false);
                    Check(Mathf.Abs(Mathf.DeltaAngle(character.lookInput.y, takeoffFacing + 90f)) < .01f,
                        "air facing did not stay at last shot direction");
                }
                input.LookInput = Vector2.zero;
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "Jump-" + direction + ".png")); yield return new WaitForEndOfFrame();
                movement = input.MovementInput = Vector2.zero;
                bool sawLoop = false, sawLanding = false;
                float end = Time.realtimeSinceStartup + 6f;
                while (Time.realtimeSinceStartup < end && motorSelf.FourJumpStarted >= 0)
                {
                    sawLoop |= visual.JumpPresentation.Phase == "Loop";
                    if (visual.JumpPresentation.Phase == "Landing")
                    {
                        sawLanding = true;
                        Check(character.IsGrounded, "landing animation played in air");
                    }
                    yield return null;
                }
                // Motor state resets in Update; presentation releases in LateUpdate.
                yield return new WaitForEndOfFrame();
                Check(sawLoop && sawLanding && motorSelf.FourJumpStarted < 0 && !visual.JumpPresentation.Applied,
                    "start/loop/landing/cleanup incomplete " + direction + " loop=" + sawLoop + " land=" + sawLanding +
                    " started=" + motorSelf.FourJumpStarted + " applied=" + visual.JumpPresentation.Applied);
                Debug.Log("FOUR_WAY_JUMP_DIRECTION_PASSED " + direction);
            }
            motorSelf.JumpProfile.jumpPower = originalHeight;
            Check(motorSelf.ServerSetHeroControlMode(false), "restore regular mode");
            yield return new WaitForSeconds(.3f);
            Check(!GodOfWarHeroCamera.Instance.GodWarMode, "conventional camera did not restore");
            for (int hero = 13; hero <= 15; hero++)
            {
                player.ServerSetRangeHero(hero); yield return new WaitForSeconds(.3f);
                character.transform.position = new Vector3(0, 50.1f, 0); movement = input.MovementInput = Vector2.zero;
                yield return new WaitForSeconds(.3f); motorSelf.Jump(); yield return new WaitForSeconds(.25f);
                Check(character.GetComponent<RangeRobotVisual>().JumpPresentation.Applied, "hero jump " + hero);
                Check(!motorSelf.FourJumpDirectional, "regular jump used firearm directional pose without firing");
                yield return new WaitForSeconds(2.5f);
            }
            player.ServerSetRangeHero(12); yield return new WaitForSeconds(.3f);
            character.transform.position = new Vector3(0, 50.1f, 0); yield return new WaitForSeconds(.3f);
            motorSelf.Jump(); yield return new WaitForSeconds(.2f);
            double previousJump = motorSelf.FourJumpStarted;
            float landingDeadline = Time.realtimeSinceStartup + 4f;
            while (motorSelf.FourJumpLanded < 0 && Time.realtimeSinceStartup < landingDeadline) yield return null;
            Check(motorSelf.FourJumpLanded >= 0, "repeat jump did not reach landing");
            yield return new WaitForSeconds(.05f);
            motorSelf.Jump(); yield return new WaitForSeconds(.2f);
            Check(motorSelf.FourJumpStarted > previousJump && motorSelf.FourJumpLanded < 0 &&
                character.GetComponent<RangeRobotVisual>().JumpPresentation.Phase == "Start",
                "new jump reused old landing state");
            player.ServerSetRangeHero(13); yield return new WaitForSeconds(.2f);
            Check(motorSelf.FourJumpStarted < 0, "hero change retained old jump");
            player.ServerSetRangeHero(12); character.transform.position = originalPosition;
            movement = Vector2.zero; character.SetMovementInput(Vector2.zero); drive = false; input.enabled = previousInput; Destroy(floor);
            Check(!error, "runtime errors");
            File.WriteAllText(Path.Combine(output, "result.txt"), "CONTROL_MODES=2/2\nDEFAULT_CONVENTIONAL=True\nCONVENTIONAL_RUN_STOP_IDLE=True\nMODE_CAMERA_SWITCH=True\nREGULAR_JUMP_WITHOUT_FIRE=True\nDIRECTIONS=5/5\nHEROES=4/4\nREAL_JUMP=True\nSTART_LOOP_LAND=True\nGROUND_DRIVEN_LANDING=True\nCLEANUP=True\nHERO_CHANGE_CANCEL=True\nRUNTIME_ERRORS=0\n");
            Debug.Log("FOUR_WAY_JUMP_ACCEPTANCE_PASSED");
        }
    }
}
