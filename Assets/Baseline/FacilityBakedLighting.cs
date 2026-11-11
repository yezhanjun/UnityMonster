using System;
using UnityEngine;
using UnityEngine.SceneManagement;

[ExecuteAlways, DefaultExecutionOrder(-500)]
public sealed class FacilityBakedLighting : MonoBehaviour
{
    [Serializable] public struct Map { public Texture2D color, direction, shadowMask; }
    [Serializable] public struct Binding { public Renderer renderer; public int index; public Vector4 scaleOffset; }
    public Map[] maps = Array.Empty<Map>();
    public Binding[] bindings = Array.Empty<Binding>();
    void OnEnable() { SceneManager.sceneLoaded += Loaded; Apply(); }
    void Start() { Apply(); }
    void OnDisable() { SceneManager.sceneLoaded -= Loaded; }
    void Loaded(Scene scene, LoadSceneMode mode) { if(scene == gameObject.scene) Apply(); }
    public void Apply()
    {
        if(maps.Length == 0)return;
        var data = new LightmapData[maps.Length];
        for(int i=0;i<data.Length;i++)data[i]=new LightmapData{lightmapColor=maps[i].color,lightmapDir=maps[i].direction,shadowMask=maps[i].shadowMask};
        LightmapSettings.lightmapsMode=LightmapsMode.CombinedDirectional;
        LightmapSettings.lightmaps=data;
        foreach(var binding in bindings)if(binding.renderer && binding.index>=0 && binding.index<data.Length)
        { binding.renderer.lightmapIndex=binding.index;binding.renderer.lightmapScaleOffset=binding.scaleOffset; }
    }
}
