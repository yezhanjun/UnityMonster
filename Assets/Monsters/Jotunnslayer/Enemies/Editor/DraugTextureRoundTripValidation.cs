using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugTextureRoundTripValidation
{
    static Color[] Read(Texture texture,int mip=-1)
    {
        int width=Mathf.Max(1,texture.width>>Mathf.Max(0,mip)),height=Mathf.Max(1,texture.height>>Mathf.Max(0,mip));
        var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGBFloat,RenderTextureReadWrite.Linear);
        Material sampler=null;
        if(mip>=0){sampler=new Material(Shader.Find("Hidden/Jotunnslayer/SourceTextureMipRead"));sampler.SetFloat("_SourceMip",mip);}
        var previous=RenderTexture.active;
        var image=new Texture2D(width,height,TextureFormat.RGBAFloat,false,true);
        try
        {
            if(sampler)Graphics.Blit(texture,rt,sampler);else Graphics.Blit(texture,rt);RenderTexture.active=rt;
            image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();
            return image.GetPixels();
        }
        finally {RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);if(sampler)UnityEngine.Object.DestroyImmediate(sampler);}
    }
    public static void Run()=>Validate(false);
    public static void RunMipChain()=>Validate(true);
    static void Validate(bool mipChain)
    {
        var bundles=new List<AssetBundle>();var seen=new HashSet<string>();
        int checkedCount=0,failures=0;
        try
        {
            const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
            foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
                foreach(var path in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(path));
            var main=AssetBundle.LoadFromFile("Logs/draug-recovered.bundle");bundles.Add(main);
            foreach(var kind in new[]{"muzzle","projectile","flask","warning","explosion"})
            {
                var original=main.LoadAsset<GameObject>("assets/recovered/draug_"+kind+".prefab");
                var imported=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/OriginalFx/Draug_"+kind);
                var materials=new Dictionary<string,Material>();
                foreach(var renderer in imported.GetComponentsInChildren<Renderer>(true))
                    foreach(var material in renderer.sharedMaterials)if(material)materials[material.name]=material;
                foreach(var renderer in original.GetComponentsInChildren<Renderer>(true))
                foreach(var material in renderer.sharedMaterials)
                {
                    if(!material || !materials.TryGetValue(material.name+"_SourceSurfaceURP",out var copy))continue;
                    foreach(var property in copy.GetTexturePropertyNames())
                    {
                        var a=material.GetTexture(property);var b=copy.GetTexture(property);
                        if(!a || !b)continue; // Unassigned source textures retain shader defaults.
                        string key=a.GetInstanceID()+":"+b.GetInstanceID();if(!seen.Add(key))continue;
                        if(a.width!=b.width || a.height!=b.height)throw new Exception("Texture dimensions changed: "+a.name);
                        if(a.mipmapCount!=b.mipmapCount)throw new Exception("Mip count mismatch "+a.name);
                        for(int level=0;level<(mipChain?a.mipmapCount:1);level++)
                        {
                        var x=Read(a,mipChain?level:-1);var y=Read(b,mipChain?level:-1);float maximum=0;double total=0;
                        for(int i=0;i<x.Length;i++)for(int c=0;c<4;c++)
                        {float error=Mathf.Abs(x[i][c]-y[i][c]);maximum=Mathf.Max(maximum,error);total+=error;}
                        checkedCount++;
                        // Allows an 8-bit export round trip; not a license for a color-space conversion.
                        if(maximum>.012f)failures++;
                        Debug.Log($"DRAUG_TEXTURE_ROUNDTRIP_CASE source={a.name} mip={level} path={AssetDatabase.GetAssetPath(b)} maxError={maximum} meanError={total/(x.Length*4)} sourceFormat={a.graphicsFormat} copyFormat={b.graphicsFormat}");
                        }
                    }
                }
            }
            if(checkedCount==0)throw new Exception("No source texture pairs checked");
            Debug.Log($"DRAUG_TEXTURE_ROUNDTRIP_RESULT checked={checkedCount} failures={failures} mipChain={mipChain}");
        }
        finally {foreach(var bundle in bundles)if(bundle)bundle.Unload(true);}
        EditorApplication.Exit(failures==0?0:1);
    }
}
