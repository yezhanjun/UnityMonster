using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
    public static class ElfWomanWhiteboxBindingExperiment
    {
        public static void Run()
        {
            var instance = Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/ElfWoman/elf_woman_sur"));
            var skin = instance.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var weights = mesh.boneWeights;
            var influences = new int[bones.Length];
            int invalid = 0, unnormalized = 0;
            foreach (var weight in weights)
            {
                if (Mathf.Abs(weight.weight0 + weight.weight1 + weight.weight2 + weight.weight3 - 1) > .001f) unnormalized++;
                for (int slot = 0; slot < 4; slot++)
                {
                    float value = slot == 0 ? weight.weight0 : slot == 1 ? weight.weight1 : slot == 2 ? weight.weight2 : weight.weight3;
                    int index = slot == 0 ? weight.boneIndex0 : slot == 1 ? weight.boneIndex1 : slot == 2 ? weight.boneIndex2 : weight.boneIndex3;
                    if (value <= 0) continue;
                    if (index < 0 || index >= bones.Length) invalid++;
                    else influences[index]++;
                }
            }
            Debug.Log($"ELF_WEIGHT_SUMMARY vertices={mesh.vertexCount} weights={weights.Length} invalid={invalid} unnormalized={unnormalized}");
            var rotations = new Quaternion[bones.Length];
            for (int i = 0; i < bones.Length; i++) rotations[i] = bones[i].localRotation;
            Resources.Load<AnimationClip>("Jotunnslayer/ElfWoman/woman_spider_init").SampleAnimation(instance, 0);
            for (int i = 0; i < bones.Length; i++)
                Debug.Log($"ELF_WEIGHT_BONE index={i} bone={bones[i].name} vertices={influences[i]} angle={Quaternion.Angle(rotations[i], bones[i].localRotation):F4} bind={rotations[i]:G6} sampled={bones[i].localRotation:G6}");
            Object.DestroyImmediate(instance);
        }
    }
}
