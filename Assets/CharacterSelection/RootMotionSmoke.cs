using System;
using System.Collections;
using UnityEngine;
namespace MTPSKIT.Gameplay
{
    public sealed class RootMotionSmoke : MonoBehaviour
    {
        CharacterInstance character;
        Vector2 movement;
        bool driving, locked, aiming, firing, error;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--root-motion-smoke") < 0) return;
            var go = new GameObject("Root motion acceptance"); DontDestroyOnLoad(go); go.AddComponent<RootMotionSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) error = true; }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("ROOT_MOTION_FAILED " + message); }
        void Update()
        {
            if (!driving || !character) return;
            character.SetMovementInput(movement); character.lookInput = Vector2.zero;
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, locked);
            character.IsAiming = aiming;
            character.CharacterItemManager.UsePrimaryInput = firing;
        }
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 90f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.myPlayerInstance && GameManager.myPlayerInstance.MyCharacter, "player missing");
            character = GameManager.myPlayerInstance.MyCharacter;
            GameManager.myPlayerInstance.ServerSetRangeHero(12);
            character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
            yield return new WaitForSeconds(2f);
            var input = PlayerGameplayInput.Instance; input.enabled = false;
            character.RangeInvulnerable = true; driving = true;
            var motor = character.GetComponent<CharacterMotor>(); motor.ServerSetHeroControlMode(false);
            var pose = character.GetComponent<RangeRobotVisual>().RiflePresentation;
            yield return new WaitForSeconds(2f);
            float phase = pose.GaitPhase;
            yield return new WaitForSeconds(.3f);
            Check(Mathf.Abs(pose.GaitPhase - phase) < .00001f, "idle gait clock kept advancing");
            Debug.Log("ROOT_MOTION_IDLE_PHASE_PASSED");
            movement = Vector2.right; // Body held at yaw zero: delta must follow body, not snap to desired heading.
            yield return new WaitForSeconds(.6f);
            int samples = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForEndOfFrame();
                if (pose.TryGetRootDisplacement(out var delta) && delta.sqrMagnitude > .0000001f)
                { Check(Vector3.Dot(delta.normalized, Vector3.forward) > .95f, "free delta detached from body facing " + delta); samples++; }
            }
            Check(samples > 10, "insufficient root motion samples");
            Debug.Log("ROOT_MOTION_BODY_BASIS_PASSED samples=" + samples);
            locked = true; movement = Vector2.left;
            yield return new WaitForSeconds(.7f);
            samples = 0;
            for (int i = 0; i < 20; i++)
            {
                yield return new WaitForEndOfFrame();
                if (pose.TryGetRootDisplacement(out var delta) && delta.sqrMagnitude > .0000001f)
                { Check(Vector3.Dot(delta.normalized, Vector3.left) > .8f, "locked strafe basis wrong " + delta); samples++; }
            }
            Check(samples > 10, "locked movement missing");
            Debug.Log("ROOT_MOTION_LOCKED_BASIS_PASSED");
            aiming = true;
            yield return new WaitForSeconds(.3f);
            var gun = character.CharacterItemManager.CurrentlyUsedItem as Gun;
            float expected = motor.GetMoveSpeed(false) / motor.WalkSpeed * gun.AaaBinding.settings.aimWalkPlayerSpeed;
            Check(Mathf.Abs(pose.MotionTimeScale - expected) < .0001f, "aim slowdown missing");
            aiming = false; firing = true;
            yield return new WaitForSeconds(.12f);
            expected = motor.GetMoveSpeed(false) / motor.WalkSpeed * gun.AaaBinding.settings.fireWalkPlayerSpeed;
            Check(Mathf.Abs(pose.MotionTimeScale - expected) < .0001f, "fire slowdown missing");
            Debug.Log("ROOT_MOTION_WEAPON_SPEED_PASSED");
            firing = locked = false; movement = Vector2.zero;
            yield return new WaitForSeconds(2f);
            phase = pose.GaitPhase;
            yield return new WaitForSeconds(.3f);
            Check(Mathf.Abs(pose.GaitPhase-phase) < .00001f, "gait did not settle after stopping");
            driving = false; character.SetMovementInput(Vector2.zero); input.enabled = true;
            Check(!error, "runtime error"); Debug.Log("ROOT_MOTION_ACCEPTANCE_PASSED");
        }
    }
}
