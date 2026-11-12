using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Explicit development build test. It never runs on normal launches.
    public sealed class WendyRifleSmoke : MonoBehaviour
    {
        bool error;
        CharacterInstance character;
        bool drive, aiming;
        bool crouching, airborne;
        Vector2 movement;
        float pitch;
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--wendy-rifle-smoke") < 0) return;
            var go = new GameObject("Wendy rifle acceptance"); DontDestroyOnLoad(go); go.AddComponent<WendyRifleSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() => Application.logMessageReceived -= Log;
        void Log(string message, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        static void Check(bool value, string message)
        { if (!value) throw new InvalidOperationException("WENDY_RIFLE_FAILED " + message); }
        void LateUpdate()
        {
            if (!drive || !character) return;
            character.IsAiming = aiming; character.SetMovementInput(movement);
            character.lookInput.x = pitch;
            if (PlayerGameplayInput.Instance)
                PlayerGameplayInput.Instance.LookInput = new Vector2(pitch, character.lookInput.y);
            if (crouching) character.IsCrouching = true;
            if (airborne) character.IsGrounded = false;
        }
        static int Ammo(Gun gun) => (int)typeof(Item).GetField("CurrentAmmo", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(gun);
        IEnumerator Start()
        {
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "WendyRifleAcceptance");
            Directory.CreateDirectory(output);
            float deadline = Time.realtimeSinceStartup + 90;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            var player = GameManager.myPlayerInstance;
            Check(player && player.MyCharacter && NetworkServer.active, "host not ready");
            yield return new WaitForSeconds(3f);
            character = player.MyCharacter; character.RangeInvulnerable = true;
            Check(player.HeroSelection == 12, "default hero is not Wendy");
            var gun = character.CharacterItemManager.CurrentlyUsedItem as Gun;
            Check(gun && gun.AaaBinding && gun.name.StartsWith("FpsRifle", StringComparison.Ordinal), "default gun is not AAARifle");
            var display = character.GetComponent<RangeRobotVisual>();
            Check(display && display.RiflePresentation != null && display.RiflePresentation.Active, "rifle presentation inactive / missing clips");
            Check(gun.ItemModel.transform.parent == display.RiflePresentation.Hand, "gun not attached to visible hand");
            Check(Vector3.Distance(gun.ItemModel.transform.position, display.RiflePresentation.Hand.position) < .08f, "gun floating away from hand");
            yield return new WaitForSeconds(1.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "Stand.png")); yield return new WaitForEndOfFrame();
            drive = true; aiming = false; GodOfWarHeroCamera.Instance.AcceptanceAim = false;
            movement = Vector2.up;
            yield return new WaitForSeconds(.6f);
            Check(display.RiflePresentation.RunWeight > .95f, "run blend did not settle");
            aiming = true; GodOfWarHeroCamera.Instance.AcceptanceAim = true;
            yield return new WaitForEndOfFrame();
            Check(display.RiflePresentation.RunWeight > .001f, "run-to-aim discarded outgoing animation");
            for (int frame = 0; frame < 40; frame++)
            {
                movement = frame < 20 ? Vector2.right : Vector2.left;
                yield return new WaitForEndOfFrame();
                Check(Mathf.Abs(display.RiflePresentation.LocomotionWeightSum - 1f) < .001f,
                    "locomotion blend lost normalized weight");
                if (Time.deltaTime < .05f)
                    Check(display.RiflePresentation.MaximumWeightStep < .65f, "direction/aim transition snapped");
            }
            movement = Vector2.zero; aiming = false; GodOfWarHeroCamera.Instance.AcceptanceAim = false;
            yield return new WaitForSeconds(1.5f);
            pitch = -25f;
            gun.PushLeftTrigger(); yield return new WaitForSeconds(.25f);
            Check(display.RiflePresentation.Pose == "HipFire" && display.RiflePresentation.AppliedAimPitch < -20f,
                "hip fire upward aim offset missing");
            Check(!character.IsAiming && !GodOfWarHeroCamera.Instance.Aiming, "hip fire entered precision aim camera");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "HipFireUp.png")); yield return new WaitForEndOfFrame();
            pitch = 25f;
            gun.PushLeftTrigger(); yield return new WaitForSeconds(.25f);
            Check(display.RiflePresentation.AppliedAimPitch > 20f, "hip fire downward aim offset missing");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "HipFireDown.png")); yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(1.3f);
            Check(display.RiflePresentation.Pose == "Stand" && Mathf.Abs(display.RiflePresentation.AppliedAimPitch) < .01f,
                "hip fire pose did not recover after releasing fire");
            pitch = 0f;
            drive = true; aiming = true; GodOfWarHeroCamera.Instance.AcceptanceAim = true;
            yield return new WaitForSeconds(.5f);
            Check(display.RiflePresentation.Pose == "Aim", "aim pose");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "Aim.png")); yield return new WaitForEndOfFrame();
            movement = Vector2.right;
            yield return new WaitForSeconds(.4f);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "Strafe.png")); yield return new WaitForEndOfFrame();
            movement = Vector2.zero;
            int ammo = Ammo(gun); gun.PushLeftTrigger();
            yield return new WaitForSeconds(.15f);
            Check(Ammo(gun) < ammo, "fire did not consume ammo");
            gun.RequestReload(); yield return new WaitForSeconds(.4f);
            Check(character.IsReloading && display.RiflePresentation.Pose == "Reload", "reload body animation");
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "Reload.png")); yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(gun.ReloadTime + .5f);
            Check(Ammo(gun) == gun.MagazineCapacity && !character.IsReloading, "reload completion");
            crouching = true; yield return new WaitForSeconds(.2f);
            Check(display.RiflePresentation.Pose == "CrouchFallback", "crouch pose was overwritten");
            crouching = false;
            airborne = true; yield return new WaitForSeconds(.2f);
            Check(display.RiflePresentation.Pose == "AirFallback", "air pose was overwritten");
            airborne = false;
            Check(character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsPistol")), "equip pistol");
            yield return new WaitForSeconds(.3f);
            Check(display.RiflePresentation.Active && display.RiflePresentation.WeaponResource == "FpsPistol",
                "pistol did not replace rifle presentation");
            Check(character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle")), "restore rifle");
            yield return new WaitForSeconds(.4f);
            gun = character.CharacterItemManager.CurrentlyUsedItem as Gun;
            Check(display.RiflePresentation.Active && gun.ItemModel.transform.parent == display.RiflePresentation.Hand, "rifle re-equip failed");
            var dropped = gun;
            Check(character.CharacterItemManager.ServerRemoveTestWeapon(Resources.Load<GameObject>("FpsRifle")), "drop rifle");
            yield return new WaitForSeconds(.2f);
            Check(dropped && dropped.ItemModel.transform.parent == dropped.transform, "dropped gun retained on old hand");
            var droppedMesh = dropped.ItemModel.transform.Find("Assault Rifle_1");
            Check(droppedMesh && Mathf.Abs(droppedMesh.localPosition.y - .1125453f) < .001f, "dropped mesh grip correction not restored");
            Check(character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle")), "equip new rifle after drop");
            yield return new WaitForSeconds(.4f);
            gun = character.CharacterItemManager.CurrentlyUsedItem as Gun;
            Check(gun && display.RiflePresentation.Active, "new rifle after drop did not bind");
            drive = false; GodOfWarHeroCamera.Instance.AcceptanceAim = null;
            var oldHand = display.RiflePresentation.Hand;
            Check(player.ServerSetRangeHero(13), "switch hero"); yield return new WaitForSeconds(.5f);
            Check(gun && gun.ItemModel && gun.ItemModel.transform.parent != oldHand, "gun destroyed or retained on released display");
            Check(player.ServerSetRangeHero(12), "restore Wendy"); yield return new WaitForSeconds(.6f);
            Check(character.GetComponent<RangeRobotVisual>().RiflePresentation.Active, "rifle did not rebind");
            Check(!error, "runtime errors");
            File.WriteAllText(Path.Combine(output, "result.txt"), "DEFAULT_WENDY=True\nDEFAULT_AAARIFLE=True\nVISIBLE_HAND=True\nSTAND_AIM_STRAFE=True\nHIP_FIRE_UP_DOWN=True\nHIP_FIRE_STAND_CAMERA=True\nHIP_FIRE_RECOVERY=True\nFIRE_AMMO=True\nRELOAD=True\nCROUCH_FALLBACK=True\nAIR_POSE_FALLBACK=True\nWEAPON_SWITCH_RESTORE=True\nDROP_RESTORE=True\nHERO_SWITCH_RESTORE=True\nRUNTIME_ERRORS=0\n");
            Debug.Log("WENDY_RIFLE_ACCEPTANCE_PASSED");
        }
    }
}
