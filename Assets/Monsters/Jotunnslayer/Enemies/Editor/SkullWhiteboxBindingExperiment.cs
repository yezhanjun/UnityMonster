using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    public static class SkullWhiteboxBindingExperiment
    {
        public static void Run()
        {
            var prefab = Resources.Load<GameObject>("Jotunnslayer/GhostSkull/ghost_skull_sur");
            var instance = Object.Instantiate(prefab);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            foreach (var bone in instance.GetComponentsInChildren<Transform>())
                Debug.Log($"SKULL_BIND name={bone.name} local={bone.localPosition:G6} scale={bone.localScale:G6} world={bone.position:G6}");
            var clip = Resources.Load<AnimationClip>("Jotunnslayer/GhostSkull/move");
            clip.SampleAnimation(instance, 0);
            foreach (var bone in instance.GetComponentsInChildren<Transform>())
                Debug.Log($"SKULL_SAMPLE name={bone.name} local={bone.localPosition:G6} scale={bone.localScale:G6} world={bone.position:G6}");
            Object.DestroyImmediate(instance);
            Skeleton1HHostSmoke.RunAlfheimBatch();
        }
    }
}
