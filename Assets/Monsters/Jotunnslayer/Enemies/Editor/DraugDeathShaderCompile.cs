using System;
using UnityEditor;
using UnityEngine;
public static class DraugDeathShaderCompile
{
    public static void Run()
    {
        const string path="Assets/Monsters/Jotunnslayer/Enemies/SourceDeathShuriken.shader";
        AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceUpdate);
        var shader=AssetDatabase.LoadAssetAtPath<Shader>(path);
        if(!shader || !shader.isSupported || ShaderUtil.ShaderHasError(shader))
            throw new Exception("Death source shader import/support failed");
        var material=new Material(shader);
        try
        {
            if(!material.SetPass(0) || ShaderUtil.ShaderHasError(shader))
                throw new Exception("Death source shader pass compile failed");
            Debug.Log("DRAUG_DEATH_SHADER_COMPILE_PASS sourceSurfaceOnly=True gpuParityPending=True");
        }
        finally {UnityEngine.Object.DestroyImmediate(material);}
        EditorApplication.Exit(0);
    }
}
