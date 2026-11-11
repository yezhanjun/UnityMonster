using System;
using System.Collections;
using System.IO;
using Cinemachine;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Explicit development Player acceptance only; never runs on a normal launch.
    public sealed class GodOfWarCameraSmoke : MonoBehaviour
    {
        string output;
        bool error;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--godwar-camera-smoke") < 0) return;
            var host = new GameObject("GodOfWar camera acceptance");
            DontDestroyOnLoad(host); host.AddComponent<GodOfWarCameraSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("GODWAR_CAMERA_FAILED " + message); }

        IEnumerator Start()
        {
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "GodWarCameraAcceptance");
            Directory.CreateDirectory(output);
            float deadline = Time.realtimeSinceStartup + 90f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            var player = GameManager.myPlayerInstance;
            Check(player && player.MyCharacter && NetworkServer.active, "host not ready");
            yield return new WaitForSeconds(3f);
            int previous = player.HeroSelection;
            player.MyCharacter.RangeInvulnerable = true;
            for (int hero = 12; hero <= 15; hero++)
            {
                Check(player.ServerSetRangeHero(hero), "select hero " + hero);
                Check(player.MyCharacter.GetComponent<CharacterMotor>().ServerSetHeroControlMode(true), "select GodOfWar control");
                yield return new WaitForSeconds(.7f);
                var camera = GodOfWarHeroCamera.Instance;
                Check(camera && GodOfWarHeroCamera.HandlesPlayer, "hero camera inactive " + hero);
                var stand = camera.Stand.GetCinemachineComponent<CinemachineFramingTransposer>();
                var aim = camera.Aim.GetCinemachineComponent<CinemachineFramingTransposer>();
                Check(stand && aim, "missing Cinemachine body");
                Check(stand.m_CameraDistance == 2.25f && aim.m_CameraDistance == 1.1f &&
                    stand.m_TrackedObjectOffset.x == .7f && aim.m_TrackedObjectOffset.x == .6f,
                    "source framing parameters changed");
                Check(camera.Stand.m_Lens.FieldOfView == 60f && camera.Aim.m_Lens.FieldOfView == 60f,
                    "source lens changed");
                var input = PlayerGameplayInput.Instance;
                input.LookInput = new Vector2(5f, player.MyCharacter.transform.eulerAngles.y);
                camera.AcceptanceAim = false;
                yield return new WaitForSeconds(.4f);
                Check(!camera.Aiming && player.MyCharacter.FPPCameraTarget == camera.transform, "Stand/raycast target");
                Check(Quaternion.Angle(camera.transform.rotation, Quaternion.Euler(input.LookInput.x, input.LookInput.y, 0)) < 2f,
                    "look input not driving camera");
                if (hero == 12)
                { ScreenCapture.CaptureScreenshot(Path.Combine(output, "Stand.png")); yield return new WaitForEndOfFrame(); }
                camera.AcceptanceAim = true;
                yield return new WaitForSeconds(.4f);
                Check(camera.Aiming, "Aim did not activate");
                Check((camera.Stand.State.FinalPosition - camera.Aim.State.FinalPosition).sqrMagnitude > .5f,
                    "Stand and Aim have identical positions");
                Check(Mathf.Abs(camera.GetComponent<Camera>().fieldOfView - 60f) < .01f, "legacy weapon FOV overwrote lens");
                if (hero == 12)
                { ScreenCapture.CaptureScreenshot(Path.Combine(output, "Aim.png")); yield return new WaitForEndOfFrame(); }
                if (hero == 12)
                {
                    var motor = player.MyCharacter.GetComponent<CharacterMotor>();
                    Check(motor.ServerStartFourWayRoll(0, player.MyCharacter.lookInput.y), "aim roll refused");
                    yield return new WaitForSeconds(.1f);
                    Check(motor.IsFourWayRolling && camera.Aiming, "roll dropped aim camera");
                    yield return new WaitForSeconds(1.5f);
                    Debug.Log("ROLL_AIM_CAMERA_PASSED");
                }
                // A held aim request cannot leak through a configuration panel that disables gameplay input.
                bool wasEnabled = input.enabled;
                input.enabled = false;
                yield return new WaitForSeconds(.3f);
                Check(!camera.Aiming, "aim input leaked while gameplay input disabled");
                input.enabled = wasEnabled;
                camera.AcceptanceAim = null;
                Debug.Log("GODWAR_CAMERA_HERO_PASSED " + hero + " Stand+Aim");
            }
            player.ServerSetRangeHero(0);
            yield return new WaitForSeconds(.3f);
            Check(!GodOfWarHeroCamera.HandlesPlayer, "legacy hero camera not restored");
            player.ServerSetRangeHero(previous);
            yield return new WaitForSeconds(.3f);
            Check(!error, "runtime errors recorded");
            File.WriteAllText(Path.Combine(output, "result.txt"),
                "HEROES=4/4\nCAMERA_MODES=8/8\nSOURCE_PARAMETERS=True\nLOOK_INPUT=True\nRAYCAST_TARGET=True\nUI_INPUT_BLOCK=True\nLEGACY_RESTORE=True\nRUNTIME_ERRORS=0\n");
            Debug.Log("GODWAR_CAMERA_ACCEPTANCE_PASSED");
        }
    }
}
