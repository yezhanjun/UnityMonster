using System;
using System.Collections;
using UnityEngine;
using Mirror;
namespace MTPSKIT.Gameplay
{
    public sealed class FullJumpSmoke : MonoBehaviour
    {
        CharacterInstance character; CharacterMotor motor; PlayerGameplayInput input;
        Vector2 move; bool driving, error, aiming;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--full-jump-smoke") < 0) return;
            var go = new GameObject("Full jump acceptance"); DontDestroyOnLoad(go); go.AddComponent<FullJumpSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) error = true; }
        static void Check(bool value, string m) { if (!value) throw new InvalidOperationException("FULL_JUMP_FAILED " + m); }
        void Update()
        {
            if (!driving || !character) return;
            character.SetMovementInput(move); character.lookInput = Vector2.zero;
            input.MovementInput = move; input.LookInput = Vector2.zero;
            character.IsAiming = aiming; character.CharacterItemManager.UseSecondaryInput = aiming; character.CharacterItemManager.UsePrimaryInput = false;
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
            if (motor.LocksAirFacing) input.ApplyLocomotion(true, false);
        }
        void PlaceAt(Vector3 position)
        {
            // Test-only teleport: avoid a stale CharacterController pose overwriting the high drop.
            var controller = character.GetComponent<CharacterController>();
            bool wasEnabled = controller.enabled;
            controller.enabled = false;
            character.transform.position = position;
            Physics.SyncTransforms();
            controller.enabled = wasEnabled;
        }
        IEnumerator ResetFloor()
        {
            move = Vector2.zero;
            yield return new WaitForSeconds(1.5f);
            if (NetworkServer.active) PlaceAt(new Vector3(0, 50.05f, 0));
            yield return new WaitForSeconds(.4f);
            Check(!motor.HeroJumpSession, "old session retained");
        }
        IEnumerator Single(int variant, bool high)
        {
            yield return ResetFloor();
            move = variant == 0 ? Vector2.zero : Vector2.up;
            yield return new WaitForSeconds(.3f);
            var pose = character.GetComponent<RangeRobotVisual>().RiflePresentation;
            float until = Time.realtimeSinceStartup + 3f;
            while (variant > 0 && motor.JumpProfile.RightFootAt(pose.GaitPhase) != (variant == 2) && Time.realtimeSinceStartup < until) yield return null;
            float takeoffY = character.transform.position.y;
            motor.Jump();
            float forceAt = variant == 0 ? 4f/30f : 5f/30f;
            yield return new WaitForSeconds(.07f);
            Check(!motor.JumpForceApplied && motor.JumpLaunchCount == 0, "jump launched before configured frame");
            Check(variant == 0 || motor.FourJumpRightFoot == (variant == 2), "wrong gait foot");
            until = Time.realtimeSinceStartup + 2f;
            while (!motor.JumpForceApplied && Time.realtimeSinceStartup < until) yield return null;
            Check(motor.JumpForceApplied && motor.JumpLaunchCount == 1, "missing launch");
            Check(motor.JumpElapsed >= forceAt && motor.JumpElapsed < forceAt + .12f, "force threshold wrong " + motor.JumpElapsed);
            Check(character.GetComponent<RangeRobotVisual>().JumpPresentation.Phase == "Start", "force point froze start into loop");
            move = Vector2.zero;
            if (high) PlaceAt(character.transform.position + Vector3.up * 20f);
            bool loop = false, land = false; float peak = character.transform.position.y;
            until = Time.realtimeSinceStartup + 5f;
            while (motor.HeroJumpSession && Time.realtimeSinceStartup < until)
            {
                var jump = character.GetComponent<RangeRobotVisual>().JumpPresentation;
                if (jump.Phase == "Loop")
                {
                    loop = true;
                    Check(jump.CurrentLoop == motor.JumpProfile.TailClip(false, motor.FourJumpDirection, motor.FourJumpRightFoot), "wrong tail loop");
                }
                if (motor.FourJumpLanded >= 0) land = true;
                peak = Mathf.Max(peak, character.transform.position.y);
                Check(motor.JumpLaunchCount <= 1, "duplicate first jump force");
                yield return null;
            }
            Check(!motor.HeroJumpSession && land, "did not land/cleanup");
            Check(high ? loop : !loop, "tail loop timing wrong high=" + high);
            if (!high) Check(peak - takeoffY < 2.2f && peak - takeoffY > 1f, "physics height/double root Y wrong rise=" + (peak-takeoffY) + " base=" + takeoffY);
            Debug.Log("FULL_JUMP_SINGLE_PASSED variant=" + variant + " high=" + high + " peak=" + peak + " base=" + takeoffY);
        }
        IEnumerator Start()
        {
            float until = Time.realtimeSinceStartup + 90f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < until) yield return null;
            Check(GameManager.myPlayerInstance && GameManager.myPlayerInstance.MyCharacter, "player missing");
            bool network = Array.IndexOf(Environment.GetCommandLineArgs(), "--full-jump-network") >= 0;
            if (network && NetworkServer.active)
            {
                PlayerInstance peer = null; until = Time.realtimeSinceStartup + 30f;
                while (!peer && Time.realtimeSinceStartup < until)
                {
                    foreach (var pair in GameManager.Players)
                        if (pair.Value && pair.Value != GameManager.myPlayerInstance && !pair.Value.BOT && pair.Value.MyCharacter) peer = pair.Value;
                    yield return null;
                }
                Check(peer, "network peer missing");
                peer.ServerSetRangeHero(12);
                peer.MyCharacter.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
            }
            character = GameManager.myPlayerInstance.MyCharacter; motor = character.GetComponent<CharacterMotor>();
            input = PlayerGameplayInput.Instance; input.enabled = false; driving = true; character.RangeInvulnerable = true;
            if (NetworkServer.active)
            {
                GameManager.myPlayerInstance.ServerSetRangeHero(12);
                character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
            }
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0,49.5f,0); floor.transform.localScale = new Vector3(100,1,100);
            floor.GetComponent<MeshRenderer>().enabled = false;
            yield return new WaitForSeconds(2f);
            if (!NetworkServer.active)
            {
                yield return Single(0, false);
                yield return Single(1, false);
                driving = false; character.SetMovementInput(Vector2.zero); input.enabled = true;
                Check(!error, "client runtime errors"); Debug.Log("FULL_JUMP_CLIENT_OWNER_PASSED"); yield break;
            }
            motor.ServerSetHeroControlMode(false);
            for (int variant = 0; variant < 3; variant++) { yield return Single(variant,false); yield return Single(variant,true); }
            yield return ResetFloor(); motor.Jump();
            yield return new WaitForSeconds(.07f); motor.Jump();
            Check(motor.DoubleJumpQueued && !motor.JumpForceApplied, "fast double press before launch was lost");
            yield return new WaitForSeconds(.15f);
            Check(motor.DoubleJumpQueued && motor.JumpVerticalSpeed > 0 && motor.JumpLaunchCount == 1, "rising double jump not buffered");
            until = Time.realtimeSinceStartup + 2f;
            while (motor.JumpLaunchCount < 2 && Time.realtimeSinceStartup < until) yield return null;
            Check(motor.JumpLaunchCount == 2 && motor.JumpDoubleAt >= 0 && motor.JumpVerticalSpeed > 0, "double did not launch at apex count=" + motor.JumpLaunchCount + " at=" + motor.JumpDoubleAt + " vy=" + motor.JumpVerticalSpeed + " elapsed=" + motor.JumpElapsed + " queue=" + motor.DoubleJumpQueued);
            motor.Jump(); yield return new WaitForSeconds(.1f);
            Check(motor.JumpLaunchCount == 2 && !motor.DoubleJumpQueued, "third jump allowed");
            Check(Mathf.Abs(Mathf.DeltaAngle(motor.AirFacing, 0f)) < 1f, "no-input double changed facing");
            Debug.Log("FULL_JUMP_DOUBLE_BUFFER_PASSED");
            // High drop allows the complete double animation to finish before landing.
            foreach (bool forward in new[] { false, true })
            {
                yield return ResetFloor(); move = forward ? Vector2.up : Vector2.zero;
                yield return new WaitForSeconds(.3f); motor.Jump();
                yield return new WaitForSeconds(.07f); motor.Jump();
                // Change direction after buffering Space but before the apex consumes it.
                move = forward ? Vector2.left : Vector2.right;
                float expectedFacing = forward ? 270f : 90f;
                until = Time.realtimeSinceStartup + 2f;
                while (motor.JumpDoubleAt < 0 && Time.realtimeSinceStartup < until) yield return null;
                Check(motor.JumpDoubleAt >= 0, "double missing");
                PlaceAt(character.transform.position + Vector3.up * 20f);
                var presentation = character.GetComponent<RangeRobotVisual>().JumpPresentation;
                float beforeTurn = motor.AirFacing;
                move = Vector2.down;
                yield return null;
                Check(Mathf.Abs(Mathf.DeltaAngle(beforeTurn, motor.AirFacing)) < 30f, "air turn snapped");
                yield return new WaitForSeconds(.55f);
                Check(Mathf.Abs(Mathf.DeltaAngle(motor.AirFacing, 180f)) < 45f,
                    "air turn did not follow smoothed velocity " + motor.AirFacing);
                Debug.Log("FULL_JUMP_DOUBLE_FACING_PASSED facing=" + expectedFacing);
                Check(presentation.Phase == "Double", "double cut off at old 40 percent threshold");
                until = Time.realtimeSinceStartup + 2f;
                while (presentation.Phase != "Loop" && motor.FourJumpLanded < 0 && Time.realtimeSinceStartup < until) yield return null;
                Check(presentation.Phase == "Loop" && presentation.CurrentLoop == motor.JumpProfile.doubleTail,
                    "double tail missing or wrong");
                Check(motor.JumpElapsed - motor.JumpDoubleAt >= motor.JumpProfile.doubleJump.length,
                    "double loop entered before full clip");
                Debug.Log("FULL_JUMP_DOUBLE_TAIL_PASSED forward=" + forward);
                until = Time.realtimeSinceStartup + 4f;
                while (motor.HeroJumpSession && Time.realtimeSinceStartup < until) yield return null;
                Check(!motor.HeroJumpSession, "double tail prevented landing");
            }
            foreach (Vector2 direction in new[] { Vector2.zero, Vector2.up, Vector2.down, Vector2.left, Vector2.right })
            {
                aiming = false;
                yield return ResetFloor();
                // Begin freely, then acquire aim in the air: second jump must re-evaluate it.
                motor.Jump(); yield return new WaitForSeconds(.2f);
                aiming = true; move = direction;
                yield return null; motor.Jump();
                until = Time.realtimeSinceStartup + 2f;
                while (motor.JumpDoubleAt < 0 && Time.realtimeSinceStartup < until) yield return null;
                Check(motor.JumpDoubleAt >= 0 && motor.DoubleJumpDirectional, "aim double mode missing");
                byte expected = CharacterMotor.SelectJumpDirection(direction);
                Check(motor.DoubleJumpDirection == expected, "aim double direction wrong");
                yield return new WaitForSeconds(.12f);
                var jump = character.GetComponent<RangeRobotVisual>().JumpPresentation;
                Check(jump.CurrentStart == motor.JumpProfile.starts[expected] && jump.CurrentLoop == motor.JumpProfile.loop,
                    "aim double uses ordinary flip or tail");
                aiming = false; // Chosen action remains stable after releasing aim.
                yield return new WaitForSeconds(.1f);
                Check(motor.DoubleJumpDirectional && jump.CurrentStart == motor.JumpProfile.starts[expected],
                    "aim release reselected double clip");
                Debug.Log("AIM_DOUBLE_LOCK_JUMP_PASSED direction=" + expected);
            }
            aiming = false;
            foreach (bool lateInput in new[] { false, true })
            {
                yield return ResetFloor(); move = lateInput ? Vector2.zero : Vector2.up;
                yield return new WaitForSeconds(.2f); motor.Jump();
                until = Time.realtimeSinceStartup + 3f;
                while (motor.FourJumpLanded < 0 && Time.realtimeSinceStartup < until) yield return null;
                Check(motor.FourJumpLanded >= 0, "new landing not reached");
                if (lateInput) { Check(!motor.JumpLandingRun, "idle landing incorrectly ran"); move = Vector2.up; }
                yield return new WaitForSeconds(.1f);
                Check(motor.JumpLandingRun && motor.HeroJumpSession, "landing run missing or exited early");
                var landingPosition = character.transform.position;
                yield return new WaitForSeconds(.1f);
                float travel = Vector3.ProjectOnPlane(character.transform.position - landingPosition, Vector3.up).magnitude;
                Check(travel > .001f && travel < 1f, "landing root motion missing or excessive " + travel);
                Debug.Log("RIFLE_LANDING_ROOT_PASSED travel=" + travel);
                move = Vector2.zero;
                yield return new WaitForSeconds(.5f);
                Check(!motor.HeroJumpSession, "landing run failed to return to ground");
                Debug.Log("RIFLE_LANDING_RUN_PASSED lateInput=" + lateInput);
            }
            yield return ResetFloor(); motor.Jump(); yield return new WaitForSeconds(.05f);
            float elapsed = motor.JumpElapsed; var position = character.transform.position; Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(.2f);
            Check(Mathf.Abs(motor.JumpElapsed-elapsed)<.001f && Vector3.Distance(position,character.transform.position)<.01f, "pause advanced jump");
            Time.timeScale = 1; Debug.Log("FULL_JUMP_PAUSE_PASSED");
            yield return ResetFloor();
            int fps = Application.targetFrameRate; int vsync = QualitySettings.vSyncCount; QualitySettings.vSyncCount = 0; Application.targetFrameRate = 15;
            motor.Jump(); yield return new WaitForSeconds(.28f);
            Check(motor.JumpForceApplied && motor.JumpLaunchCount == 1, "low FPS missed/duplicated force");
            Application.targetFrameRate = fps; QualitySettings.vSyncCount = vsync; Debug.Log("FULL_JUMP_LOW_FPS_PASSED");
            yield return ResetFloor(); motor.Jump(); double id = motor.FourJumpStarted;
            until = Time.realtimeSinceStartup+3f;
            while (motor.FourJumpLanded<0 && Time.realtimeSinceStartup<until) yield return null;
            Check(motor.FourJumpLanded>=0,"landing missing"); motor.Jump(); yield return new WaitForSeconds(motor.JumpProfile.landing.length * motor.JumpProfile.landingJumpWindow + .12f);
            Check(motor.FourJumpStarted>id && motor.FourJumpLanded<0,"landing jump buffer not consumed");
            Debug.Log("FULL_JUMP_LANDING_BUFFER_PASSED");
            GameManager.myPlayerInstance.ServerSetRangeHero(13); yield return new WaitForSeconds(.2f);
            Check(!motor.HeroJumpSession && !motor.DoubleJumpQueued,"hero swap retained jump");
            Debug.Log("FULL_JUMP_INTERRUPT_PASSED");
            driving=false; move=Vector2.zero; character.SetMovementInput(Vector2.zero); input.enabled=true; Destroy(floor);
            Check(!error,"runtime errors"); Debug.Log("FULL_JUMP_ACCEPTANCE_PASSED");
        }
    }
}
