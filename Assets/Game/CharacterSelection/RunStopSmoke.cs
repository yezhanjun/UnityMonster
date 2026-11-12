using System;
using System.Collections;
using System.IO;
using UnityEngine;
using Mirror;
namespace MTPSKIT.Gameplay
{
    // Explicit development-only exercise of the live graph and movement chain.
    public sealed class RunStopSmoke : MonoBehaviour
    {
        CharacterInstance character;
        Vector2 movement;
        bool driving, error;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--run-stop-smoke") < 0) return;
            var go = new GameObject("Run stop acceptance"); DontDestroyOnLoad(go); go.AddComponent<RunStopSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) error = true; }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("RUN_STOP_FAILED " + message); }
        void Update()
        {
            if (!driving || !character) return;
            character.SetMovementInput(movement); character.lookInput = Vector2.zero;
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
        }
        IEnumerator StopAt(float phase, bool right, string label)
        {
            var display = character.GetComponent<RangeRobotVisual>();
            var pose = display.RiflePresentation;
            movement = Vector2.up;
            yield return new WaitForSeconds(.35f);
            float deadline = Time.realtimeSinceStartup + 3f;
            while (Mathf.Abs(Mathf.DeltaAngle(pose.GaitPhase * 360f, phase * 360f)) > 12f && Time.realtimeSinceStartup < deadline)
                yield return new WaitForEndOfFrame();
            movement = Vector2.zero;
            yield return new WaitForSeconds(.18f);
            Check(pose.RunStopActive && pose.RunStopRight == right, label + " wrong stop foot phase=" + pose.StopSelectedPhase);
            Check(pose.RunStopWeight > .8f, label + " stop not visible weight=" + pose.RunStopWeight);
            var firstClip = pose.RunStopClip;
            var rootLibrary = Resources.Load<HeroRootMotionLibrary>("DemonHunter3C/HeroRootMotion");
            Check(rootLibrary.Find(firstClip) != null, "missing stop root track");
            var folder = Path.Combine(Path.GetDirectoryName(Application.dataPath), "RunStopAcceptance"); Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, label + ".png"));
            Debug.Log("RUN_STOP_PHASE_PASSED " + label + " clip=" + firstClip.name + " weight=" + pose.RunStopWeight);
            for (int n = 0; n < 3; n++)
            {
                movement = Vector2.up;
                yield return new WaitForSeconds(.08f);
                Check(!pose.RunStopActive, "reinput did not release stop immediately");
                movement = Vector2.zero;
                right = !right;
                yield return new WaitForSeconds(.18f);
                Check(pose.RunStopActive && pose.RunStopRight == right, "repeat did not alternate foot " + n);
                Check(pose.RunStopWeight > .8f, "repeat stop invisible");
            }
            Debug.Log("RUN_STOP_REPEAT_PASSED " + label);
            yield return new WaitForSeconds(pose.RunStopClip.length + .3f);
            Check(!pose.RunStopActive && pose.RunStopWeight < .02f, "stop did not release into idle");
        }
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 100f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.myPlayerInstance && GameManager.myPlayerInstance.MyCharacter, "player missing");
            if (!NetworkServer.active) yield break;
            var player = GameManager.myPlayerInstance; character = player.MyCharacter;
            player.ServerSetRangeHero(12);
            yield return new WaitForSeconds(2f);
            var input = PlayerGameplayInput.Instance; input.enabled = false;
            character.RangeInvulnerable = true; driving = true;
            Check(character.GetComponent<CharacterMotor>().ServerSetHeroControlMode(false), "free mode unavailable");
            foreach (var weapon in new[] { "Rifle", "Pistol", "Shotgun", "Sniper" })
            {
                character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("Fps" + weapon));
                yield return new WaitForSeconds(1f);
                yield return StopAt(.10f, true, weapon + "-Right");
                yield return StopAt(.45f, false, weapon + "-Left");
            }
            for (int hero = 13; hero <= 15; hero++)
            {
                player.ServerSetRangeHero(hero);
                yield return new WaitForSeconds(1f);
                yield return StopAt(.45f, false, "Hero-" + hero);
            }
            driving = false; character.SetMovementInput(Vector2.zero); input.enabled = true;
            Check(!error, "runtime error observed");
            Debug.Log("RUN_STOP_ACCEPTANCE_PASSED");
        }
    }
}
