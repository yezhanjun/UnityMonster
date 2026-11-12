using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DraugDeathPreviewIsolation
{
 public static void Run()=>Process(false);
 public static void Decoded()=>Process(false,true);
 public static void LodSelected()=>Process(false,true,0);
 public static void MeshDiagnostic()=>Process(true);
 static void Process(bool meshDiagnostic,bool decoded=false,int groupCount=-999)
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  string folder=groupCount>=0?"Docs/Validation/DraugDeathLod0Preview":decoded?"Docs/Validation/DraugDeathDecodedPreview":meshDiagnostic?"Docs/Validation/DraugDeathMeshDiagnostic":"Docs/Validation/DraugDeathPreviewIsolation";Directory.CreateDirectory(folder);
  var camera=new GameObject("Isolation camera").AddComponent<Camera>();
  camera.backgroundColor=new Color(.12f,.13f,.16f,1);camera.clearFlags=CameraClearFlags.SolidColor;
  camera.transform.position=new Vector3(3,2,4);camera.transform.LookAt(new Vector3(0,1,0));
  camera.orthographic=true;camera.orthographicSize=2.3f;
  camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
  var light=new GameObject("Isolation light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-35,0);
  var rt=new RenderTexture(768,768,24);rt.Create();camera.targetTexture=rt;
  var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/DeathFx/"+(decoded?"DraugDeathDecodedStage":"DraugDeathStaged"));
  try
  {
   foreach(var group in groupCount>=0?new[]{"all"}:meshDiagnostic?new[]{"body_unlit","body_lit"}:new[]{"empty","all","draugh_ranged_mat","M_mecha_particles","M_Shaman_Glow","M_smoke_earthquake","M_Glow_Alpha"})
   {
    GameObject obj=null;Material diagnostic=null;
    try
    {
     if(group!="empty")
     {
      obj=UnityEngine.Object.Instantiate(prefab);
      if(groupCount>=0)
      {
       var lod=new DraugDeathParticleLod(obj,obj.transform.Find("lod1").gameObject,obj.transform.Find("lod2").gameObject,obj.transform.Find("lod3").gameObject);
       int selected=lod.Begin(groupCount);Debug.Log($"DRAUG_DEATH_PREVIEW_LOD selected={selected} inputCount={groupCount}");
      }
      foreach(var ps in obj.GetComponentsInChildren<ParticleSystem>(true))
      {
       if(groupCount>=0 && !ps.isPlaying)continue;
       ps.useAutoRandomSeed=false;ps.randomSeed=123;
       ps.Simulate(.35f,false,true,true);
      }
      foreach(var renderer in obj.GetComponentsInChildren<Renderer>(true))
      {
       bool selected=group=="all";
       if(meshDiagnostic && renderer is MeshRenderer)
       {
        selected=true;
        var mesh=renderer.GetComponent<MeshFilter>().sharedMesh;
        var vertices=mesh.vertices;var bounds=new Bounds(vertices[0],Vector3.zero);
        foreach(var v in vertices)bounds.Encapsulate(v);
        Debug.Log($"DRAUG_DEATH_MESH_DIAGNOSTIC name={mesh.name} vertices={vertices.Length} actualBounds={bounds} declared={mesh.bounds} layout={string.Join(",",mesh.GetVertexAttributes())} matrix={renderer.localToWorldMatrix}");
        if(group=="body_unlit")
        {
         if(!diagnostic){diagnostic=new Material(Shader.Find("Universal Render Pipeline/Unlit"));diagnostic.SetColor("_BaseColor",new Color(.2f,.8f,.3f));}
         renderer.sharedMaterial=diagnostic;
        }
       }
       foreach(var material in renderer.sharedMaterials)if(material && material.name==group)selected=true;
       if(group=="all")Debug.Log($"DRAUG_DEATH_ISOLATION_RENDERER name={renderer.name} type={renderer.GetType().Name} active={renderer.gameObject.activeInHierarchy} enabled={renderer.enabled} bounds={renderer.bounds} material={(renderer.sharedMaterial?renderer.sharedMaterial.name:"null")}");
       renderer.enabled=renderer.enabled && selected;
      }
     }
     camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
     var tex=new Texture2D(768,768,TextureFormat.RGBA32,false);
     try{tex.ReadPixels(new Rect(0,0,768,768),0,0);tex.Apply();File.WriteAllBytes(folder+"/"+group+".png",tex.EncodeToPNG());}
     finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(tex);}
     Debug.Log("DRAUG_DEATH_ISOLATION_FRAME "+group);
    }
    finally{if(obj)UnityEngine.Object.DestroyImmediate(obj);if(diagnostic)UnityEngine.Object.DestroyImmediate(diagnostic);}
   }
  }
  finally{camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);}
  Debug.Log("DRAUG_DEATH_ISOLATION_PASS captureOnly=True");EditorApplication.Exit(0);
 }
}
