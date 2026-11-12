using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class DemonBundleAudit
{
    public static void Run()
    {
        const string source = "E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        foreach (string pattern in new[] { "*monoscripts*.bundle", "*unitybuiltinassets*.bundle", "duplicateassetisolation*.bundle" })
            foreach (string dependency in Directory.GetFiles(source, pattern)) AssetBundle.LoadFromFile(dependency);
        foreach (string file in Directory.GetFiles(source, "enemies_assets_demon_helheim_sur*.bundle"))
        {
            var bundle = AssetBundle.LoadFromFile(file);
            if (!bundle) throw new Exception("Cannot load beetle bundle");
            try
            {
                foreach (var clip in bundle.LoadAllAssets<AnimationClip>()) Debug.Log($"DEMON_CLIP {clip.name} duration={clip.length}");
                foreach (string asset in bundle.GetAllAssetNames())
                {
                    var model = bundle.LoadAsset<GameObject>(asset);
                    if (!model) continue;
                    Debug.Log($"DEMON_MODEL {asset} name={model.name} transforms={model.GetComponentsInChildren<Transform>(true).Length}");
                    foreach (var t in model.GetComponentsInChildren<Transform>(true))
                        Debug.Log($"DEMON_NODE {model.name} node={t.name} local={t.localPosition} scale={t.localScale} worldScale={t.lossyScale}");
                    foreach (var mesh in model.GetComponentsInChildren<MeshFilter>(true))
                        Debug.Log($"DEMON_PROP {model.name} node={mesh.name} mesh={mesh.sharedMesh}");
                    foreach(var sphere in model.GetComponentsInChildren<SphereCollider>(true)) Debug.Log($"DEMON_SOURCE_SPHERE {sphere.name} radius={sphere.radius} center={sphere.center} scale={sphere.transform.lossyScale}");
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                    {
                        var controller = animator.runtimeAnimatorController;
                        Debug.Log("DEMON_CONTROLLER " + (controller ? controller.name : "MISSING") + " animator=" + animator.name + " scale=" + animator.transform.localScale + " worldScale=" + animator.transform.lossyScale);
                        if (!controller) continue;
                        foreach (var c in controller.animationClips) if (c) { Debug.Log($"DEMON_RESOLVED_CLIP {c.name} duration={c.length}"); if (true) foreach (var ev in AnimationUtility.GetAnimationEvents(c)) Debug.Log($"DEMON_THROW_EVENT time={ev.time} function={ev.functionName} value={ev.stringParameter}"); }
                        var clips = new SerializedObject(controller).FindProperty("m_Clips");
                        if (clips == null) continue;
                        for (int i = 0; i < clips.arraySize; i++)
                        {
                            var clip = clips.GetArrayElementAtIndex(i).FindPropertyRelative("m_OverrideClip").objectReferenceValue as AnimationClip;
                            if (clip) Debug.Log($"DEMON_OVERRIDE {clip.name} length={clip.length}");
                        }
                    }
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        Debug.Log($"DEMON_SKIN {skin.name} mesh={(skin.sharedMesh ? skin.sharedMesh.name : "MISSING")} bones={skin.bones.Length}");
                        foreach (var mat in skin.sharedMaterials)
                            if (mat)
                            {
                                Debug.Log($"DEMON_MATERIAL {mat.name} shader={mat.shader.name} main={(mat.mainTexture ? mat.mainTexture.name : "MISSING")}");
                                var tex = new SerializedObject(mat).FindProperty("m_SavedProperties.m_TexEnvs");
                                if (tex == null) continue;
                                for (int i = 0; i < tex.arraySize; i++)
                                {
                                    var t = tex.GetArrayElementAtIndex(i);
                                    var image = t.FindPropertyRelative("second.m_Texture").objectReferenceValue;
                                    if (image) Debug.Log($"DEMON_TEXTURE {t.FindPropertyRelative("first").stringValue} {image.name}");
                                }
                            }
                    }
                }
            }
            finally { bundle.Unload(true); }
        }
        EditorApplication.Exit(0);
    }
}
