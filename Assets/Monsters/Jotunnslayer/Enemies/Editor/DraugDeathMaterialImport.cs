using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class DraugDeathMaterialImport
{
    const string Folder="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/DraugThrower/DeathFx";
    public static void Run()=>Process(true);
    public static void ValidateReload()=>Process(false);
    static T Save<T>(T value,string path) where T:Object
    {
        var old=AssetDatabase.LoadAssetAtPath<T>(path);
        if(old){EditorUtility.CopySerialized(value,old);Object.DestroyImmediate(value);return old;}
        AssetDatabase.CreateAsset(value,path);return value;
    }
    static Color32[] ReadMip(Texture value,int mip,Material sampler)
    {
        int width=Mathf.Max(1,value.width>>mip),height=Mathf.Max(1,value.height>>mip);
        bool srgb=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(value.graphicsFormat);
        var rt=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32,srgb?RenderTextureReadWrite.sRGB:RenderTextureReadWrite.Linear);
        var readback=new Texture2D(width,height,TextureFormat.RGBA32,false,!srgb);
        var previous=RenderTexture.active;
        try
        {
            sampler.SetFloat("_SourceMip",mip);Graphics.Blit(value,rt,sampler);RenderTexture.active=rt;
            readback.ReadPixels(new Rect(0,0,width,height),0,0);readback.Apply(false);
            return readback.GetPixels32();
        }
        finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);Object.DestroyImmediate(readback);}
    }
    static void Process(bool import)
    {
        var bundles=new List<AssetBundle>();Material sampler=null;int levels=0,materials=0;long pixels=0;
        try
        {
            if(import){Directory.CreateDirectory(Folder);AssetDatabase.Refresh();}
            const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
            foreach(var pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
                foreach(var path in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(path));
            var main=AssetBundle.LoadFromFile("Logs/draug-death-recovered.bundle");bundles.Add(main);
            var root=main.LoadAsset<GameObject>("assets/recovered/draugdeath_visual.prefab");
            var selected=new Dictionary<string,Material>();
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
                if(material && (material.name=="M_mecha_particles" || material.name=="M_Shaman_Glow" || material.name=="M_smoke_earthquake" || material.name=="M_Glow_Alpha"))selected[material.name]=material;
            if(selected.Count!=4)throw new Exception("Source death material count differs");
            sampler=new Material(Shader.Find("Hidden/Jotunnslayer/SourceTextureMipRead"));
            foreach(var pair in selected)
            {
                var sourceMaterial=pair.Value;
                bool legacy=pair.Key=="M_smoke_earthquake",glow=pair.Key=="M_Glow_Alpha";
                string textureKey=legacy?"_MainTex":glow?"_MainTexA":"_mainTexture",colorKey=legacy?"_TintColor":glow?"_Tint":"_color";
                string shaderName=legacy?"Jotunnslayer/SourceDeathLegacyAdd":glow?"Jotunnslayer/SourceDeathAnimatedAlpha":"Jotunnslayer/SourceDeathShuriken";
                var floatKeys=legacy?new[]{"_InvFade"}:glow?new[]{"_Fade","_Intensity","_BlendMode","_LocalDistortionStrength","_LocalDistortionRotation"}:new[]{"_noiseScale","_distortionAmount"};
                if(legacy && sourceMaterial.shader.name!="Legacy Shaders/Particles/Additive")throw new Exception("Source smoke shader changed");
                if(!legacy && !glow && (sourceMaterial.shader.name!="Shader Graphs/S_Shuriken" || sourceMaterial.GetFloat("_BlendMode")!=1 || sourceMaterial.GetFloat("_SrcBlend")!=1 || sourceMaterial.GetFloat("_DstBlend")!=1))
                    throw new Exception("Unsupported source death blend configuration");
                var vectorKeys=glow?new[]{"_MainTextureATo","_MainTextureBTo","_MainTextureCTo","_MaskTilingTo","_TilingAndOffset","_LocalDistortionMapTs","_AnimationAB","_AnimationC"}:Array.Empty<string>();
                if(glow)
                {
                    if(sourceMaterial.shader.name!="Games Farm/HDRP/Unlit/Particles Animated Alpha Blended" || sourceMaterial.GetFloat("_BlendMode")!=0 || sourceMaterial.GetFloat("_SrcBlend")!=1 || sourceMaterial.GetFloat("_DstBlend")!=10)throw new Exception("Source glow shader/blend changed");
                    foreach(var key in new[]{"_MainTexB","_MainTexC","_MaskTexture","_LocalDistortionMap"})if(sourceMaterial.GetTexture(key))throw new Exception("Additional source glow texture requires import "+key);
                }
                var original=sourceMaterial.GetTexture(textureKey);if(!original)throw new Exception("Source texture missing");
                string texturePath=Folder+"/"+pair.Key+"_SourceMips.asset",materialPath=Folder+"/"+pair.Key+".mat";
                if(import)
                {
                    bool srgb=UnityEngine.Experimental.Rendering.GraphicsFormatUtility.IsSRGBFormat(original.graphicsFormat);
                    var texture=new Texture2D(original.width,original.height,TextureFormat.RGBA32,original.mipmapCount>1,!srgb);
                    texture.name=original.name+"_SourceMips";
                    if(texture.mipmapCount!=original.mipmapCount)throw new Exception("Source partial mip chain unsupported");
                    for(int mip=0;mip<original.mipmapCount;mip++)texture.SetPixels32(ReadMip(original,mip,sampler),mip);
                    texture.Apply(false);texture.filterMode=original.filterMode;texture.anisoLevel=original.anisoLevel;texture.mipMapBias=original.mipMapBias;
                    texture.wrapModeU=original.wrapModeU;texture.wrapModeV=original.wrapModeV;texture.wrapModeW=original.wrapModeW;
                    var stored=Save(texture,texturePath);
                    var material=new Material(Shader.Find(shaderName)){name=pair.Key};
                    material.SetTexture(textureKey,stored);material.SetColor(colorKey,sourceMaterial.GetColor(colorKey));
                    if(legacy)
                    {
                        material.SetTextureScale(textureKey,sourceMaterial.GetTextureScale(textureKey));
                        material.SetTextureOffset(textureKey,sourceMaterial.GetTextureOffset(textureKey));
                        material.shaderKeywords=sourceMaterial.shaderKeywords;
                    }
                    else
                    {
                        if(!glow)material.SetVector("_noiseSpeed",sourceMaterial.GetVector("_noiseSpeed"));
                        material.SetFloat("_Cull",sourceMaterial.GetFloat("_CullModeForward"));
                    }
                    foreach(var key in vectorKeys)material.SetVector(key,sourceMaterial.GetVector(key));
                    foreach(var key in floatKeys)material.SetFloat(key,sourceMaterial.GetFloat(key));
                    Save(material,materialPath);
                    AssetDatabase.SaveAssets();AssetDatabase.ImportAsset(texturePath,ImportAssetOptions.ForceUpdate);
                }
                var actual=AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                var output=AssetDatabase.LoadAssetAtPath<Material>(materialPath);
                if(!actual || !output || output.GetTexture(textureKey)!=actual || output.shader.name!=shaderName)throw new Exception("Saved resource binding invalid");
                if(actual.mipmapCount!=original.mipmapCount || actual.filterMode!=original.filterMode || actual.wrapModeU!=original.wrapModeU || actual.wrapModeV!=original.wrapModeV || actual.anisoLevel!=original.anisoLevel || actual.mipMapBias!=original.mipMapBias)throw new Exception("Texture sampler/mips changed");
                if(output.GetColor(colorKey)!=sourceMaterial.GetColor(colorKey))throw new Exception("Material color changed");
                if(!legacy && ((!glow && output.GetVector("_noiseSpeed")!=sourceMaterial.GetVector("_noiseSpeed")) || output.GetFloat("_Cull")!=sourceMaterial.GetFloat("_CullModeForward")))throw new Exception("Material speed/cull changed");
                if(legacy && (output.GetTextureScale(textureKey)!=sourceMaterial.GetTextureScale(textureKey) || output.GetTextureOffset(textureKey)!=sourceMaterial.GetTextureOffset(textureKey) || output.IsKeywordEnabled("SOFTPARTICLES_ON")!=sourceMaterial.IsKeywordEnabled("SOFTPARTICLES_ON")))throw new Exception("Legacy UV/soft keyword changed");
                foreach(var key in vectorKeys)if(output.GetVector(key)!=sourceMaterial.GetVector(key))throw new Exception("Material vector changed "+key);
                foreach(var key in floatKeys)if(output.GetFloat(key)!=sourceMaterial.GetFloat(key))throw new Exception("Material parameter changed "+key);
                for(int mip=0;mip<original.mipmapCount;mip++)
                {
                    var a=ReadMip(original,mip,sampler);var b=ReadMip(actual,mip,sampler);
                    if(a.Length!=b.Length)throw new Exception("Mip dimensions changed");
                    for(int i=0;i<a.Length;i++)if(!a[i].Equals(b[i]))throw new Exception($"Source mip mismatch {pair.Key} mip={mip} pixel={i} source={a[i]} local={b[i]}");
                    pixels+=a.Length;levels++;
                }
                materials++;
            }
            File.WriteAllText("Docs/Validation/DraugDeathFourMaterial"+(import?"Import":"Reload")+"Verification.json",$"{{\"status\":\"PASS_SOURCE_MIP_BYTES_AND_BINDINGS\",\"materials\":{materials},\"mipLevels\":{levels},\"pixels\":{pixels},\"limits\":\"Decoded RGBA8 GPU samples verified; original compressed storage not preserved. Two Shuriken, one Legacy and one AnimatedAlpha material; no death prefab/runtime integration.\"}}");
            Debug.Log($"DRAUG_DEATH_MATERIAL_PASS import={import} materials={materials} levels={levels} pixels={pixels}");
        }
        finally{if(sampler)Object.DestroyImmediate(sampler);foreach(var bundle in bundles)if(bundle)bundle.Unload(true);}
        EditorApplication.Exit(0);
    }
}
