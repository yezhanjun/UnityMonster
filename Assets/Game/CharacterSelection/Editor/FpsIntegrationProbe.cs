using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;

[InitializeOnLoad]
public static class FpsIntegrationProbe
{
    static int stage; static double deadline, next; static Gun gun; static int ammo; static string errors="";
    static Transform[] fingerBones;
    static Quaternion[] beforeReload;
    static int weaponIndex;
    static GameObject panel;
    static int Ammo => (int)typeof(Item).GetField("CurrentAmmo", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(gun);
    static FpsIntegrationProbe() { EditorApplication.update+=Tick; Application.logMessageReceived+=Log; }
    static void Log(string message,string stack,LogType type) { if(SessionState.GetBool("FpsProbe",false) && (type==LogType.Error || type==LogType.Exception) && errors.Length<6000 && !errors.Contains(message)) errors+=message+"\n"; }
    [MenuItem("TPS/Run FPS Integration Probe")]
    public static void Run()
    {
        weaponIndex=0;stage=0;deadline=0;next=0;errors="";
        if(EditorApplication.isPlaying) throw new InvalidOperationException("Probe requires stopped editor");
        if(EditorSceneManager.GetActiveScene().isDirty && !EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene())) throw new InvalidOperationException("Could not preserve current scene");
        SessionState.SetString("FpsProbeScene",EditorSceneManager.GetActiveScene().path);
        SessionState.SetBool("FpsProbe",true);
        EditorSceneManager.OpenScene(DNNetworkManager.MainMenuScene);
        EditorApplication.isPlaying=true;
    }
    static void Tick()
    {
        if(File.Exists("Temp/run-fps-probe" ) && !EditorApplication.isPlayingOrWillChangePlaymode) { File.Delete("Temp/run-fps-probe");Run();return; }
        if(!SessionState.GetBool("FpsProbe",false) || !EditorApplication.isPlaying) return;
        double now=EditorApplication.timeSinceStartup;
        if(deadline==0)deadline=now+150;
        if(now>deadline){Finish("FAIL timeout stage="+stage);return;}
        if(now<next)return;
        try {
            switch(stage){
            case 0:
                if(!DNNetworkManager.Instance || !ItemManager.Instance)return;
                DNNetworkManager.Instance.onlineScene="Assets/AssetStore/desNetware/Multiplayer TPS KIT/Levels/map_range.unity";
                RoomSetup.Properties=new RoomProperties{P_MaxPlayers=1,P_GameDuration=600,P_RespawnCooldown=1};
                DNNetworkManager.Instance.StartHost();stage=1;next=now+3;break;
            case 1:
                var player=GameManager.myPlayerInstance;if(!player || !player.MyCharacter)return;
                int slot = weaponIndex == 1 ? 1 : 0;
                var prefab = Resources.Load<GameObject>(FpsGunBinding.ResourceNames[weaponIndex]);
                int id = Array.IndexOf(ItemManager.Instance.SlotsLodout[slot].availableItemsForSlot, prefab);
                if (id < 0 || !DNNetworkManager.Instance.spawnPrefabs.Contains(prefab)) throw new Exception("Missing loadout or network registration");
                var loadout = new int[2];
                loadout[slot] = id;
                player.CmdSendNewLodoutInfo(loadout, true);
                stage=2;next=now+2;break;
            case 2:
                if (weaponIndex == 1) GameManager.myPlayerInstance.MyCharacter.CharacterItemManager.ClientTakeItem(1);
                stage=3;next=now+1;break;
            case 3:
                gun=GameManager.myPlayerInstance.MyCharacter.CharacterItemManager.CurrentlyUsedItem as Gun;
                if (!gun || !gun.name.StartsWith(FpsGunBinding.ResourceNames[weaponIndex]) || !gun.AaaBinding || !gun.ItemIcon) throw new Exception("Wrong weapon or missing icon: " + weaponIndex);
                if (!FpsViewController.Instance.FirstPerson) FpsViewController.Instance.ToggleView();
                stage=4;next=now+1;break;
            case 4:
                if(!FpsViewController.Instance.Ready)throw new Exception("FAF view not ready");
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Temp/aaa-view-" + weaponIndex + ".png"));
                var camera = GameplayCamera._instance.transform;
                panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                panel.name = new[] { "Stone", "Metal", "Wood", "Flesh" }[weaponIndex];
                panel.GetComponent<Renderer>().enabled = false;
                if (weaponIndex == 3) panel.AddComponent<Akila.FPSFramework.CustomDecal>().decalVFX = gun.AaaBinding.impacts[3];
                panel.transform.SetPositionAndRotation(camera.position + camera.forward * 3, camera.rotation);
                panel.transform.localScale = new Vector3(8, 6, 0.1f);
                Physics.SyncTransforms();
                var activeRig = UnityEngine.Object.FindFirstObjectByType<AaaViewAnimations>();
                fingerBones = Array.FindAll(activeRig.GetComponentsInChildren<Transform>(), t => t.name.Contains("hand") || t.name.Contains("thumb") || t.name.Contains("lowerarm"));
                beforeReload = Array.ConvertAll(fingerBones, bone => bone.localRotation);
                ammo=Ammo;
                float pitch = PlayerGameplayInput.Instance.LookInput.x;
                gun.PushLeftTrigger();
                if(PlayerGameplayInput.Instance.LookInput.x >= pitch) throw new Exception("Missing AAA aim kick");
                stage=7;next=now+.25;break;
            case 7:
                if (weaponIndex >= 2)
                {
                    if (!Array.Exists(Array.ConvertAll(fingerBones, bone => bone.localRotation), q => q != Quaternion.identity)) throw new Exception("Missing animated arms");
                    bool cycled = false;
                    for (int i=0; i<fingerBones.Length; i++) cycled |= Quaternion.Angle(fingerBones[i].localRotation, beforeReload[i]) > 1;
                    if (!cycled) throw new Exception("Recharge clip did not move the hands: " + gun.name);
                    gun.PushLeftTrigger();
                    if (Ammo != ammo-1) throw new Exception("Fired during pump/bolt cycle");
                }
                int holes = 0;
                foreach(var impact in UnityEngine.Object.FindObjectsByType<Akila.FPSFramework.CustomDecal>(FindObjectsSortMode.None))
                    if (impact.transform.IsChildOf(panel.transform) && impact.name.StartsWith(gun.AaaBinding.impacts[weaponIndex].name) && (weaponIndex == 3 || impact.transform.Find("Decal"))) holes++;
                if(holes < gun.AaaBinding.settings.shotCount) throw new Exception("Missing pellet impacts: " + holes);
                UnityEngine.Object.Destroy(panel);
                stage=5;next=now+.4;break;
            case 5:
                if(Ammo!=ammo-1)throw new Exception("Shot did not consume exactly one round");
                var view = UnityEngine.Object.FindFirstObjectByType<AaaViewAnimations>();
                if (!view || !view.Ready || view.fire.clip.name.Split('|')[1] != (weaponIndex >= 2 ? "Recharge" : "Fire"))
                    throw new Exception("Incorrect firing clip for " + gun.name);
                fingerBones = Array.FindAll(view.GetComponentsInChildren<Transform>(), t => t.name.Contains("hand") || t.name.Contains("thumb") || t.name.Contains("lowerarm"));
                beforeReload = Array.ConvertAll(fingerBones, bone => bone.localRotation);
                gun.RequestReload();stage=8;next=now+(weaponIndex == 2 ? .15 : .45);break;
            case 8:
                bool moved = false;
                for (int i=0; i<fingerBones.Length; i++) moved |= Quaternion.Angle(beforeReload[i], fingerBones[i].localRotation) > 1;
                if (!moved) throw new Exception("Reload did not move the arm skeleton: " + gun.name);
                ScreenCapture.CaptureScreenshot(System.IO.Path.GetFullPath("Temp/aaa-reload-" + weaponIndex + ".png"));
                stage=6;next=now+3;break;
            case 6:
                if(Ammo!=gun.MagazineCapacity)throw new Exception("Reload failed");
                FpsViewController.Instance.ToggleView();
                if(FpsViewController.Instance.FirstPerson)throw new Exception("Third person restore failed");
                if (++weaponIndex < 4) {stage=1;break;}
                Finish((errors.Length==0 ? "PASS" : "FAIL runtime errors")+" four AAA weapons: loadout/spawn/FAF/aim kick/ammo/reload/pellet decals/view-toggle\n"+errors);
                break;
            }
        }catch(Exception e){Finish("FAIL "+e+"\n"+errors);}
    }
    static void Finish(string result)
    {
        File.WriteAllText("Temp/fps-probe-result.txt",result);
        SessionState.SetBool("FpsProbe",false);
        // Leave the successful first-person scene available for visual inspection.
        Debug.Log("FPS_PROBE_RESULT "+result);
    }
}
