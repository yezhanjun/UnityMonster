using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class DraugHandImport
{
    public static void Run()
    {
        const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
        const string folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/HandFx";
        Directory.CreateDirectory(folder); AssetDatabase.Refresh();
        var bundles=new List<AssetBundle>(); GameObject copy=null;
        try
        {
            foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle","enemies_assets_draug_thrower_sur*.bundle"})
                foreach(var file in Directory.GetFiles(source,pattern)) bundles.Add(AssetBundle.LoadFromFile(file));
            var main=bundles.Last();
            var prefab=main.LoadAsset<GameObject>(main.GetAllAssetNames().First(n=>n.EndsWith("draug_thrower_sur.prefab")));
            var hand=prefab.GetComponentsInChildren<Transform>(true).First(t=>t.name=="fx_draug_thrower_hand");
            copy=Object.Instantiate(hand.gameObject); copy.name="DraugOriginalHandFx";
            foreach(var c in copy.GetComponentsInChildren<MonoBehaviour>(true)) if(c) Object.DestroyImmediate(c);
            foreach(var t in copy.GetComponentsInChildren<Transform>(true)) GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
            int index=0;
            foreach(var r in copy.GetComponentsInChildren<ParticleSystemRenderer>(true))
            {
                var original=r.sharedMaterial; if(!original) continue;
                var mat=new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
                mat.name=original.name+"_Converted";
                mat.SetFloat("_Surface",1); mat.SetFloat("_Blend",0); mat.SetFloat("_SrcBlend",5); mat.SetFloat("_DstBlend",10); mat.SetFloat("_ZWrite",0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); mat.renderQueue=3000;
                var props=new SerializedObject(original).FindProperty("m_SavedProperties.m_TexEnvs");
                Texture selected=null;
                for(int i=0;props!=null && i<props.arraySize;i++)
                {
                    var e=props.GetArrayElementAtIndex(i); var t=e.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                    if(!t)continue;
                    Debug.Log($"DRAUG_HAND_TEXTURE material={original.name} property={e.FindPropertyRelative("first").stringValue} texture={t.name}");
                    if(selected==null || e.FindPropertyRelative("first").stringValue=="_MainTex")selected=t;
                }
                if(selected)
                {
                    var rt=RenderTexture.GetTemporary(selected.width,selected.height,0,RenderTextureFormat.ARGB32);
                    var previous=RenderTexture.active;
                    try
                    {
                        Graphics.Blit(selected,rt); RenderTexture.active=rt;
                        var tex=new Texture2D(selected.width,selected.height,TextureFormat.RGBA32,false);
                        tex.ReadPixels(new Rect(0,0,selected.width,selected.height),0,0);tex.Apply();
                        // Source _MaskTexture is a grayscale opacity mask, not an RGBA albedo.
                        if(original.name.Contains("thrower"))
                        {
                            var pixels=tex.GetPixels();
                            for(int pixel=0;pixel<pixels.Length;pixel++)
                            {
                                float opacity=Mathf.Max(pixels[pixel].r,Mathf.Max(pixels[pixel].g,pixels[pixel].b));
                                pixels[pixel]=new Color(1f,1f,1f,opacity);
                            }
                            tex.SetPixels(pixels);tex.Apply();
                        }
                        string path=folder+"/texture_"+index+".png";File.WriteAllBytes(path,tex.EncodeToPNG());Object.DestroyImmediate(tex);
                        AssetDatabase.ImportAsset(path);mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
                    }
                    finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);}
                }
                string materialPath=folder+"/material_"+index+++".mat";
                var existing=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(existing) {EditorUtility.CopySerialized(mat,existing);Object.DestroyImmediate(mat);mat=existing;} else AssetDatabase.CreateAsset(mat,materialPath);
                r.sharedMaterial=mat;
            }
            foreach(var ps in copy.GetComponentsInChildren<ParticleSystem>(true)) {ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);var m=ps.main;m.playOnAwake=false;}
            copy.transform.SetLocalPositionAndRotation(Vector3.zero,Quaternion.identity);copy.transform.localScale=Vector3.one;
            var saved=PrefabUtility.SaveAsPrefabAsset(copy,folder+"/DraugOriginalHandFx.prefab");
            if(!saved)throw new Exception("Draug hand prefab save failed");
            AssetDatabase.SaveAssets();
            Debug.Log("DRAUG_HAND_IMPORTED source particles preserved; shader conversion approximate");
        }
        finally {if(copy)Object.DestroyImmediate(copy);foreach(var b in bundles)if(b)b.Unload(true);}
        EditorApplication.Exit(0);
    }
}
