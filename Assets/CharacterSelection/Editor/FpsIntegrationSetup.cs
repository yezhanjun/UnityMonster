using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using MTPSKIT.Gameplay;
using KINEMATION.FPSAnimationFramework.Runtime.Core;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using KINEMATION.ProceduralRecoilAnimationSystem.Runtime;

[InitializeOnLoad]
public static class FpsIntegrationSetup
{
    static FpsIntegrationSetup() { EditorApplication.delayCall += Prepare; }
    [MenuItem("TPS/Prepare FPS Integration")]
    public static void Prepare()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var source = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AaaWeapons/Loadouts.json");
        if (!source) return;
        var configs = JsonUtility.FromJson<Catalog>(source.text).weapons;
        for (int i = 0; i < configs.Length; i++) PrepareGun(i, configs[i]);
    }

    [Serializable] class Catalog { public AaaWeaponSettings[] weapons; }
    static readonly string[] Labels = { "AAA 突击步枪", "AAA 手枪", "AAA 霰弹枪", "AAA 狙击枪" };
    static T FromGuid<T>(string guid) where T : UnityEngine.Object =>
        AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));

    static void PrepareGun(int index, AaaWeaponSettings settings)
    {
        string resource = FpsGunBinding.ResourceNames[index];
        string path = "Assets/Resources/" + resource + ".prefab";
        var existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (existing && existing.GetComponent<FpsGunBinding>().setupVersion == 9) return;
        var model = FromGuid<GameObject>(settings.modelGuid);
        GameObject view = null, gun = null;
        try
        {
            view = new GameObject(resource + "View");
            AaaViewBuilder.Build(view, index, resource, settings.reloadTime);
            var viewPrefab = PrefabUtility.SaveAsPrefabAsset(view, "Assets/Resources/" + resource + "View.prefab");
            if (!viewPrefab) throw new InvalidOperationException("Failed to save FAF view prefab");
            gun = PrefabUtility.LoadPrefabContents("Assets/desNetware/Multiplayer TPS KIT/Levels/Prefabs/Gameplay/item_rifle.prefab");
            gun.name = resource;
            var binding = gun.AddComponent<FpsGunBinding>();
            binding.setupVersion = 9;
            binding.viewPrefab = viewPrefab;

            var fire = view.GetComponent<AaaViewAnimations>().fire;
            binding.cycleSeconds = index >= 2 ? fire.clip.length / fire.blendTime.rateScale : 0;
            binding.settings = settings;
            binding.impacts = new[] { "Stone", "Metal", "Wood", "Blood" }.Select(surface =>
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AaaWeapons/Akila/FPS Framework/Prefabs/Particle Systems/Bullet Impact/" + surface + " Impact.prefab")).ToArray();
            var item = gun.GetComponent<Gun>();
            item.ItemName = Labels[index];
            item.ItemIcon = FromGuid<Sprite>(settings.iconGuid);
            item.KillFeedIcon = item.ItemIcon;
            item.MagazineCapacity = settings.magazineCapacity;
            item.ReloadTime = settings.reloadTime;
            item.FOVScopeMultiplier = settings.aimFieldOfView / 90f;
            var muzzleFlash = FitGunModel(item.ItemModel, model, index, item.ItemModel.transform.forward);
            var serialized = new SerializedObject(item);
            serialized.FindProperty("_particleSystem").objectReferenceValue = muzzleFlash;
            serialized.FindProperty("coolDown").floatValue = 60f / settings.fireRate;
            serialized.FindProperty("_damage").intValue = settings.damage;
            serialized.FindProperty("CurrentAmmo").intValue = settings.magazineCapacity;
            serialized.FindProperty("_recoil").floatValue = 0;
            serialized.FindProperty("_devation").floatValue = 0;
            serialized.FindProperty("fireClip").objectReferenceValue = FromGuid<AudioClip>(settings.audioGuid);
            serialized.ApplyModifiedPropertiesWithoutUndo();
            // A distinct prefab asset generates a distinct Mirror asset ID.
            var savedGun = PrefabUtility.SaveAsPrefabAsset(gun, path);
            EnsureNetworkId(savedGun);
            AssetDatabase.SaveAssets();
            Debug.Log("AAA_WEAPON_READY " + resource);
        }
        catch (Exception e) { Debug.LogException(e); }
        finally { if (view) UnityEngine.Object.DestroyImmediate(view); if (gun) PrefabUtility.UnloadPrefabContents(gun); }
    }
    static ParticleSystem FitGunModel(GameObject anchor, GameObject model, int index, Vector3 forward)
    {
        // Keep the animator, aim point and hand targets on the original rig.
        // Only its visible weapon geometry is replaced.
        var renderers = anchor.GetComponentsInChildren<Renderer>(true)
            .Where(r => r is SkinnedMeshRenderer || r is MeshRenderer).ToArray();
        var visible = renderers.Where(r => r.enabled && r.gameObject.activeSelf).ToArray();
        if (visible.Length == 0) throw new InvalidOperationException("No weapon renderer on " + anchor.name);
        var reference = visible.OrderByDescending(r => r.bounds.size.sqrMagnitude).First();
        Vector3 origin = reference.bounds.center;
        foreach (var renderer in renderers) renderer.enabled = false;
        var replacement = UnityEngine.Object.Instantiate(model, anchor.transform, false);
        replacement.name = model.name;
        foreach (var animator in replacement.GetComponentsInChildren<Animator>()) animator.enabled = false;
        var meshes = replacement.GetComponentsInChildren<Renderer>();
        Bounds bounds = meshes[0].bounds;
        foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
        Vector3 size = bounds.size;
        float length = new[] { 0.79f, 0.22f, 0.98f, 1.14f }[index];
        replacement.transform.localScale *= length / Mathf.Max(size.x, Mathf.Max(size.y, size.z));
        bounds = meshes[0].bounds;
        foreach (var mesh in meshes) bounds.Encapsulate(mesh.bounds);
        replacement.transform.position += origin - bounds.center;
        var flashAsset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/AaaWeapons/Akila/FPS Framework/Prefabs/Particle Systems/Weapons/Muzzle Flash.prefab");
        var flash = UnityEngine.Object.Instantiate(flashAsset);
        flash.name = "AAA Muzzle Flash";
        flash.transform.SetPositionAndRotation(origin + forward * (length * 0.48f) + Vector3.up * (bounds.size.y * 0.16f),
            Quaternion.LookRotation(forward));
        flash.transform.SetParent(replacement.transform, true);
        return flash.GetComponent<ParticleSystem>();
    }

    static void EnsureNetworkId(GameObject prefab)
    {
        var identity = prefab.GetComponent<Mirror.NetworkIdentity>();
        var data = new SerializedObject(identity);
        var guid = new Guid(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(prefab)));
        data.FindProperty("_assetId").longValue = Mirror.NetworkIdentity.AssetGuidToUint(guid);
        data.ApplyModifiedPropertiesWithoutUndo();
        PrefabUtility.SavePrefabAsset(prefab);
    }
}
