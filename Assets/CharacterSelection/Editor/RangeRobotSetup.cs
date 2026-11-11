using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class RangeRobotSetup
{
    static RangeRobotSetup() { EditorApplication.delayCall += EnsurePrefab; }
    [MenuItem("TPS/Prepare Range Robot")]
    public static void EnsurePrefab()
    {
        const string output = "Assets/Resources/RangeRobot.prefab";
        if (EditorApplication.isPlayingOrWillChangePlaymode || AssetDatabase.LoadAssetAtPath<GameObject>(output)) return;
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/CharacterSelection/UnityRobot/Skeleton/IKRig_Humanoid.fbx");
        if (!asset) return;
        var root = Object.Instantiate(asset);
        try
        {
            root.name = "FAF Range Robot";
            var animator = root.GetComponent<Animator>();
            if (!animator || !animator.avatar || !animator.avatar.isHuman) throw new System.InvalidOperationException("Robot Avatar is not humanoid");
            animator.runtimeAnimatorController = null;
            animator.applyRootMotion = false;
            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>()) renderer.updateWhenOffscreen = true;
            PrefabUtility.SaveAsPrefabAsset(root, output);
            Debug.Log("RANGE_ROBOT_PREFAB_READY");
        }
        finally { Object.DestroyImmediate(root); }
    }
}
