using System;
using System.Collections;
using System.IO;
using System.Reflection;
using Mirror;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace MTPSKIT.Gameplay
{
    // Explicit development-client acceptance only. Uses real equip/reload RPCs and visible Wendy.
    public sealed class WendyFirearmsSmoke : MonoBehaviour
    {
        static readonly string[] Keys = { "Rifle", "Pistol", "Shotgun", "Sniper" };
        static readonly FieldInfo AmmoField = typeof(Item).GetField("CurrentAmmo", BindingFlags.Instance | BindingFlags.NonPublic);
        bool error, drive, aim;
        CharacterInstance character;
        Vector2 movement;
        string output;
        float maximumOutsideArms;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--wendy-firearms-smoke") < 0) return;
            var go = new GameObject("Wendy four firearms acceptance"); DontDestroyOnLoad(go); go.AddComponent<WendyFirearmsSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += OnLog;
        void OnDisable() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType type)
        { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) error = true; }
        void Update()
        {
            if (!drive || !character) return;
            character.SetMovementInput(movement); character.IsAiming = aim;
            character.lookInput = Vector2.zero;
            character.CharacterItemManager.UsePrimaryInput = character.CharacterItemManager.UseSecondaryInput = false;
        }
        static void Check(bool passed, string message)
        { if (!passed) throw new InvalidOperationException("WENDY_FIREARMS_FAILED " + message); }
        static int Ammo(Gun gun) => (int)AmmoField.GetValue(gun);
        static PlayerInstance Peer(PlayerInstance self)
        {
            foreach (var pair in GameManager.Players)
                if (pair.Value && pair.Value != self && !pair.Value.BOT && pair.Value.MyCharacter) return pair.Value;
            return null;
        }
        static bool ArmBone(int bone) => bone == (int)HumanBodyBones.LeftShoulder || bone == (int)HumanBodyBones.RightShoulder ||
            bone == (int)HumanBodyBones.LeftUpperArm || bone == (int)HumanBodyBones.RightUpperArm ||
            bone == (int)HumanBodyBones.LeftLowerArm || bone == (int)HumanBodyBones.RightLowerArm ||
            bone == (int)HumanBodyBones.LeftHand || bone == (int)HumanBodyBones.RightHand ||
            bone >= (int)HumanBodyBones.LeftThumbProximal && bone <= (int)HumanBodyBones.RightLittleDistal;
        void CheckReloadIsolation(RangeRobotVisual display)
        {
            var presentation = display.RiflePresentation;
            var type = typeof(WendyRiflePresentation);
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var graph = (PlayableGraph)type.GetField("graph", flags).GetValue(presentation);
            var layers = (AnimationLayerMixerPlayable)type.GetField("layers", flags).GetValue(presentation);
            var root = display.DisplayAnimator.transform;
            Vector3 position = root.position; Quaternion rotation = root.rotation;
            var handler = new HumanPoseHandler(display.DisplayAnimator.avatar, root);
            HumanPose saved = default, without = default, with = default;
            handler.GetHumanPose(ref saved);
            float weight = layers.GetInputWeight(4);
            try
            {
                layers.SetInputWeight(4, 0); graph.Evaluate(0); root.SetPositionAndRotation(position, rotation);
                handler.GetHumanPose(ref without);
                layers.SetInputWeight(4, 1); graph.Evaluate(0); root.SetPositionAndRotation(position, rotation);
                handler.GetHumanPose(ref with);
                float armChange = 0;
                for (int i = 0; i < with.muscles.Length; i++)
                {
                    float difference = Mathf.Abs(with.muscles[i] - without.muscles[i]);
                    if (ArmBone(HumanTrait.BoneFromMuscle(i))) armChange = Mathf.Max(armChange, difference);
                    else maximumOutsideArms = Mathf.Max(maximumOutsideArms, difference);
                }
                Check(maximumOutsideArms < .003f, "reload changed torso/head/leg muscles " + maximumOutsideArms);
                Check(armChange > .01f, "reload had no actual arm animation");
            }
            finally
            {
                layers.SetInputWeight(4, weight); graph.Evaluate(0);
                root.SetPositionAndRotation(position, rotation); handler.SetHumanPose(ref saved); handler.Dispose();
            }
        }
        IEnumerator Start()
        {
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "WendyFirearmsAcceptance"); Directory.CreateDirectory(output);
            float deadline = Time.realtimeSinceStartup + 110f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            var player = GameManager.myPlayerInstance; Check(player && player.MyCharacter, "player unavailable");
            while (!Peer(player) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(Peer(player), "peer unavailable");
            if (!NetworkServer.active)
            {
                int weapons = 0, reloads = 0;
                while (Time.realtimeSinceStartup < deadline + 150f)
                {
                    var peer = Peer(player);
                    var visual = peer ? peer.MyCharacter.GetComponent<RangeRobotVisual>() : null;
                    var pose = visual ? visual.RiflePresentation : null;
                    if (pose != null && pose.Active)
                        for (int i = 0; i < Keys.Length; i++)
                            if (pose.WeaponResource == "Fps" + Keys[i])
                            {
                                weapons |= 1 << i;
                                if (pose.ReloadWeight > .5f) reloads |= 1 << i;
                            }
                    if (weapons == 15 && reloads == 15)
                    {
                        Check(!error, "remote runtime error");
                        File.WriteAllText(Path.Combine(output, "remote-result.txt"), "REMOTE_WEAPONS=4/4\nREMOTE_RELOADS=4/4\nRUNTIME_ERRORS=0\n");
                        Debug.Log("WENDY_FIREARMS_REMOTE_PASSED"); yield break;
                    }
                    yield return null;
                }
                Check(false, "remote did not observe all weapons/reloads"); yield break;
            }
            yield return new WaitForSeconds(3f);
            character = player.MyCharacter; character.RangeInvulnerable = true;
            var input = PlayerGameplayInput.Instance; bool wasEnabled = input.enabled; input.enabled = false; drive = true;
            input.LookInput = new Vector2(5f, 140f);
            var items = character.CharacterItemManager;
            for (int i = 0; i < Keys.Length; i++)
            {
                string resource = "Fps" + Keys[i];
                Check(items.ServerEquipTestWeapon(Resources.Load<GameObject>(resource)), "equip " + resource);
                yield return new WaitForSeconds(1.8f);
                var gun = items.CurrentlyUsedItem as Gun;
                var visual = character.GetComponent<RangeRobotVisual>(); var pose = visual.RiflePresentation;
                Check(gun && pose != null && pose.Active && pose.WeaponResource == resource, "profile " + resource);
                Check(gun.ItemModel.transform.parent == pose.Hand, "mount " + resource);
                Check(Vector3.Distance(gun.ItemModel.transform.position, pose.Hand.position) < .08f, "grip offset " + resource);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, Keys[i] + "-Stand.png")); yield return new WaitForEndOfFrame();
                aim = true; yield return new WaitForSeconds(.3f);
                Check(pose.Pose == "Aim", "aim " + resource);
                ScreenCapture.CaptureScreenshot(Path.Combine(output, Keys[i] + "-Aim.png")); yield return new WaitForEndOfFrame();
                aim = false; yield return new WaitForSeconds(.3f);
                int before = Ammo(gun); gun.PushLeftTrigger(); yield return new WaitForSeconds(.2f);
                Check(Ammo(gun) < before, "shoot " + resource);
                yield return new WaitForSeconds(1.5f);
                for (int empty = 0; empty < 2; empty++)
                {
                    movement = empty == 0 ? Vector2.left : Vector2.down;
                    character.SetActionKeyCode(ActionCodes.LockedLocomotion, true);
                    yield return new WaitForSeconds(.4f);
                    AmmoField.SetValue(gun, empty == 1 ? 0 : gun.MagazineCapacity - 2);
                    int initialCycle = gun.PresentationReloadCycle;
                    gun.RequestReload(); yield return new WaitForSeconds(.18f);
                    Check(character.IsReloading && pose.ReloadWeight > .5f, "reload start " + resource);
                    Check(gun.PresentationReloadFromEmpty == (empty == 1), "empty selection " + resource);
                    Check(pose.ReloadClip.name.IndexOf(Keys[i], StringComparison.OrdinalIgnoreCase) >= 0, "wrong reload clip " + resource);
                    CheckReloadIsolation(visual);
                    Check(pose.RunWeight < .01f, "locked reload incorrectly uses forward run " + resource);
                    ScreenCapture.CaptureScreenshot(Path.Combine(output, Keys[i] + (empty == 1 ? "-EmptyReload.png" : "-Reload.png")));
                    yield return new WaitForEndOfFrame();
                    float end = Time.realtimeSinceStartup + 8f;
                    while (character.IsReloading && Time.realtimeSinceStartup < end) yield return null;
                    Check(!character.IsReloading && Ammo(gun) == gun.MagazineCapacity, "reload completion " + resource);
                    if (i == 2) Check(gun.PresentationReloadCycle - initialCycle == (empty == 1 ? gun.MagazineCapacity : 2), "per-shell animation stages");
                    yield return new WaitForSeconds(.3f);
                    Check(pose.ReloadWeight < .001f, "reload fade out " + resource);
                    character.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
                    movement = Vector2.up;
                    yield return new WaitForSeconds(.4f);
                    Check(pose.RunWeight > .95f, "free movement did not restore run " + resource);
                    movement = Vector2.zero;
                }
                // Switching during reload must release the old graph and restore the weapon's original hierarchy.
                AmmoField.SetValue(gun, gun.MagazineCapacity - 2); gun.RequestReload(); yield return new WaitForSeconds(.15f);
                Check(items.ServerRemoveTestWeapon(Resources.Load<GameObject>(resource)), "drop " + resource);
                yield return new WaitForSeconds(.3f);
                Check(gun.ItemModel.transform.parent == gun.transform, "drop retained hand mount " + resource);
                Debug.Log("WENDY_FIREARM_PASSED " + resource);
            }
            Check(items.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle")), "restore rifle");
            yield return new WaitForSeconds(.3f);
            for (int hero = 13; hero <= 15; hero++)
            {
                var previous = character.GetComponent<RangeRobotVisual>().RiflePresentation;
                player.ServerSetRangeHero(hero); yield return new WaitForSeconds(.5f);
                var shared = character.GetComponent<RangeRobotVisual>().RiflePresentation;
                Check(!previous.Active && shared != previous && shared.Active, "shared hero graph lifecycle " + hero);
                Check((items.CurrentlyUsedItem as Gun).ItemModel.transform.parent == shared.Hand, "shared hero hand mount " + hero);
            }
            player.ServerSetRangeHero(12); yield return new WaitForSeconds(.4f);
            Check(character.GetComponent<RangeRobotVisual>().RiflePresentation.Active, "restore Wendy");
            Check(Peer(player), "peer disconnected during weapon or hero transitions");
            drive = false; movement = Vector2.zero; character.SetMovementInput(Vector2.zero); input.enabled = wasEnabled;
            Check(!error, "runtime errors");
            File.WriteAllText(Path.Combine(output, "result.txt"), "WEAPONS=4/4\nNORMAL_EMPTY_RELOAD=8/8\nARMS_ONLY=True\nMAX_NON_ARM_DELTA=" + maximumOutsideArms +
                "\nSHOTGUN_PER_SHELL=True\nFIRE_AMMO=True\nDROP_DURING_RELOAD=True\nHERO_RESTORE=True\nRUNTIME_ERRORS=0\n");
            Debug.Log("WENDY_FIREARMS_ACCEPTANCE_PASSED");
        }
    }
}
