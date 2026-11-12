using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class CultistWomanBundleAudit
{
    public static void Run()
    {
        const string source = "E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        foreach (string pattern in new[] { "*monoscripts*.bundle", "*unitybuiltinassets*.bundle", "duplicateassetisolation*.bundle" })
            foreach (string dependency in Directory.GetFiles(source, pattern)) AssetBundle.LoadFromFile(dependency);
        foreach (string file in Directory.GetFiles(source, "enemies_assets_cultist_woman_*.bundle"))
        {
            var bundle = AssetBundle.LoadFromFile(file);
            if (!bundle) throw new Exception("Cannot load beetle bundle");
            try
            {
                foreach (var clip in bundle.LoadAllAssets<AnimationClip>()) Debug.Log($"CULTIST_WOMAN_CLIP {clip.name} duration={clip.length}");
                foreach (string asset in bundle.GetAllAssetNames())
                {
                    var model = bundle.LoadAsset<GameObject>(asset);
                    if (!model) continue;
                    Debug.Log($"CULTIST_WOMAN_MODEL {asset} name={model.name} transforms={model.GetComponentsInChildren<Transform>(true).Length}");
                    foreach (var animator in model.GetComponentsInChildren<Animator>(true))
                    {
                        var controller = animator.runtimeAnimatorController;
                        Debug.Log("CULTIST_WOMAN_CONTROLLER " + (controller ? controller.name : "MISSING"));
                        if (!controller) continue;
                        foreach (var c in controller.animationClips) if (c) Debug.Log($"CULTIST_WOMAN_RESOLVED_CLIP {c.name} duration={c.length}");
                        var clips = new SerializedObject(controller).FindProperty("m_Clips");
                        if (clips == null) continue;
                        for (int i = 0; i < clips.arraySize; i++)
                        {
                            var clip = clips.GetArrayElementAtIndex(i).FindPropertyRelative("m_OverrideClip").objectReferenceValue as AnimationClip;
                            if (clip) Debug.Log($"CULTIST_WOMAN_OVERRIDE {clip.name} length={clip.length}");
                        }
                    }
                    foreach (var skin in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    {
                        Debug.Log($"CULTIST_WOMAN_SKIN {skin.name} mesh={(skin.sharedMesh ? skin.sharedMesh.name : "MISSING")} bones={skin.bones.Length}");
                        foreach (var mat in skin.sharedMaterials)
                            if (mat)
                            {
                                Debug.Log($"CULTIST_WOMAN_MATERIAL {mat.name} shader={mat.shader.name} main={(mat.mainTexture ? mat.mainTexture.name : "MISSING")}");
                                var tex = new SerializedObject(mat).FindProperty("m_SavedProperties.m_TexEnvs");
                                if (tex == null) continue;
                                for (int i = 0; i < tex.arraySize; i++)
                                {
                                    var t = tex.GetArrayElementAtIndex(i);
                                    var image = t.FindPropertyRelative("second.m_Texture").objectReferenceValue;
                                    if (image) Debug.Log($"CULTIST_WOMAN_TEXTURE {t.FindPropertyRelative("first").stringValue} {image.name}");
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
