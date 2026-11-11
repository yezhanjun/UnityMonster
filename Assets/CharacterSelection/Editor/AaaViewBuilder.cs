using System;
using System.Collections.Generic;
using System.Linq;
using KINEMATION.FPSAnimationFramework.Runtime.Playables;
using MTPSKIT.Gameplay;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

static class AaaViewBuilder
{
    [Serializable] class Catalog { public Rig[] guns; }
    [Serializable] class Rig { public string model; public Node[] parents; public Modification[] mods; public Vector3 aim, sprintPosition, sprintRotation, walkPosition, walkRotation, walkPositionFrequency, walkRotationFrequency; }
    [Serializable] class Node { public string name; public Vector3 m_LocalPosition, m_LocalScale; public Quaternion m_LocalRotation; }
    [Serializable] class Modification { public string id, path, value; }

    public static void Build(GameObject view, int index, string resource, float reloadTime)
    {
        const string folder = "Assets/AaaWeapons/AnimationSlots";
        if (!AssetDatabase.IsValidFolder(folder)) AssetDatabase.CreateFolder("Assets/AaaWeapons", "AnimationSlots");
        string prefix = folder + "/" + resource;
        var config = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets/AaaWeapons/ViewRigs.json");
        var rig = JsonUtility.FromJson<Catalog>(config.text).guns[index];
        string modelPath = AssetDatabase.GUIDToAssetPath(rig.model);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
        Transform parent = view.transform;
        foreach (var node in rig.parents)
        {
            var child = new GameObject(node.name).transform;
            child.SetParent(parent, false);
            child.SetLocalPositionAndRotation(node.m_LocalPosition, node.m_LocalRotation);
            child.localScale = node.m_LocalScale;
            parent = child;
        }
        var arms = (GameObject)PrefabUtility.InstantiatePrefab(model, parent);
        var originals = model.GetComponentsInChildren<Transform>(true)
            .SelectMany(t => t.GetComponents<Component>().Cast<UnityEngine.Object>().Append(t.gameObject));
        var targets = new Dictionary<string, UnityEngine.Object>();
        foreach (var original in originals)
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(original, out string _, out long id)) targets[id.ToString()] = original;
        PrefabUtility.SetPropertyModifications(arms, rig.mods.Where(m => targets.ContainsKey(m.id))
            .Select(m => new PropertyModification { target = targets[m.id], propertyPath = m.path, value = m.value }).ToArray());
        PrefabUtility.UnpackPrefabInstance(arms, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        var clips = AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<AnimationClip>()
            .Where(c => c.name.StartsWith(model.name.Replace(" Animated", "") + "|", StringComparison.Ordinal)).ToDictionary(c => c.name.Split('|').Last());
        var mask = AssetDatabase.LoadAssetAtPath<AvatarMask>(prefix + ".mask");
        if (!mask) { mask = new AvatarMask(); AssetDatabase.CreateAsset(mask, prefix + ".mask"); }
        var bones = arms.GetComponentsInChildren<Transform>(true);
        mask.transformCount = bones.Length;
        for (int i=0; i<bones.Length; i++)
        {
            mask.SetTransformPath(i, AnimationUtility.CalculateTransformPath(bones[i], arms.transform));
            mask.SetTransformActive(i, true);
        }
        EditorUtility.SetDirty(mask);
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(prefix + ".controller");
        if (!controller) controller = AnimatorController.CreateAnimatorControllerAtPath(prefix + ".controller");
        var machine = controller.layers[0].stateMachine;
        if (!machine.defaultState) machine.defaultState = machine.AddState("Idle");
        machine.defaultState.motion = clips["Idle"];
        var animator = arms.GetComponent<Animator>();
        if (!animator) animator = arms.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var slots = view.AddComponent<AaaViewAnimations>();
        slots.player = arms.AddComponent<FPSPlayablesController>();
        slots.player.upperBodyMask = mask;
        slots.player.enabled = false;
        slots.aimOffset = rig.aim;
        slots.sprintPosition = rig.sprintPosition;
        slots.sprintRotation = rig.sprintRotation;
        slots.walkPosition = rig.walkPosition;
        slots.walkRotation = rig.walkRotation;
        slots.walkPositionFrequency = rig.walkPositionFrequency;
        slots.walkRotationFrequency = rig.walkRotationFrequency;
        FPSAnimationAsset Slot(string key, float duration = 0)
        {
            if (!clips.TryGetValue(key, out var clip)) return null;
            string path = prefix + "-" + key + ".asset";
            var asset = AssetDatabase.LoadAssetAtPath<FPSAnimationAsset>(path);
            if (!asset) { asset = ScriptableObject.CreateInstance<FPSAnimationAsset>(); AssetDatabase.CreateAsset(asset, path); }
            asset.clip = clip;
            asset.mask = mask;
            asset.curves = new List<AnimCurve>();
            asset.blendTime = new BlendTime(0.025f, 0.04f) { rateScale = duration > 0 ? clip.length / duration : 1 };
            EditorUtility.SetDirty(asset);
            return asset;
        }
        slots.idle = Slot("Idle");
        slots.equip = Slot("Take");
        slots.fire = Slot(index >= 2 ? "Recharge" : "Fire");
        if (index == 2) slots.fire.blendTime.rateScale = 0.86f;
        slots.reload = Slot("Reload", reloadTime);
        slots.reloadAimed = Slot(index == 1 ? "Aim Reload" : "Reload Aimed", reloadTime);
        slots.finishReload = Slot("Reload Transition");
        slots.finishReloadAimed = Slot("Reload Transition Aimed");
        if (!slots.fire || !slots.reload || !slots.equip) throw new InvalidOperationException("Incomplete action slots: " + modelPath);

        var idle = clips["Idle"];
        idle.SampleAnimation(arms, 0);
        var visible = arms.GetComponentsInChildren<Renderer>().Where(r => r.enabled && r.name.IndexOf("hand", StringComparison.OrdinalIgnoreCase) < 0).ToArray();
        var barrel = visible.OrderByDescending(r => r.bounds.max.z).First();
        var flash = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/AaaWeapons/Akila/FPS Framework/Prefabs/Particle Systems/Weapons/Muzzle Flash.prefab"));
        flash.transform.position = new Vector3(barrel.bounds.center.x, barrel.bounds.center.y, barrel.bounds.max.z);
        flash.transform.SetParent(barrel.transform, true);
        slots.muzzle = flash.GetComponent<ParticleSystem>();
    }
}
