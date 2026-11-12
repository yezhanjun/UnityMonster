using System;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    public static class HumanoidWhiteboxRestExperiment
    {
        public static void Run()
        {
            const string root = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/";
            foreach (string relative in new[] { "SkeletonBowRegular/skeleton_bow_VarRegular.fbx", "SkeletonGuardian/skeleton_guardian_sur.fbx" })
            {
                string path = root + relative;
                var importer = (ModelImporter)AssetImporter.GetAtPath(path);
                var description = importer.humanDescription;
                Debug.Log($"HUMANOID_REST_REIMPORT {relative} oldSkeleton={description.skeleton.Length}");
                description.skeleton = Array.Empty<SkeletonBone>();
                importer.humanDescription = description;
                importer.SaveAndReimport();
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                var animator = model.GetComponent<Animator>();
                Debug.Log($"HUMANOID_REST_AVATAR {relative} valid={animator && animator.avatar && animator.avatar.isValid} human={animator && animator.avatar && animator.avatar.isHuman}");
            }
            Skeleton1HHostSmoke.RunAlfheimBatch();
        }
    }
}
