using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
public static class DraugArrowFxPreview
{
 public static void Run()
 {
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  const string folder="Docs/Validation/DraugBaseArrowPreview";Directory.CreateDirectory(folder);
  var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.backgroundColor=new Color(.12f,.13f,.16f);camera.clearFlags=CameraClearFlags.SolidColor;
  camera.transform.position=new Vector3(3,2,4);camera.transform.LookAt(new Vector3(0,.4f,0));camera.orthographic=true;camera.orthographicSize=2.3f;
  var light=new GameObject("Preview light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-35,0);
  var rt=new RenderTexture(768,768,24);camera.targetTexture=rt;
  try
  {
   foreach(var kind in new[]{"muzzle","projectile","hit","end"})
   {
    var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/BaseArrowFx/DraugArrow_"+kind);if(!prefab)throw new Exception("Missing "+kind);
    var obj=UnityEngine.Object.Instantiate(prefab);
    camera.orthographicSize=kind=="flask"?.35f:2.3f;
    camera.transform.LookAt(kind=="flask"?Vector3.zero:new Vector3(0,.4f,0));
    foreach(var ps in obj.GetComponentsInChildren<ParticleSystem>(true))ps.Simulate(kind=="flask"?.1f:.35f,false,true,true);
    camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
    var tex=new Texture2D(768,768,TextureFormat.RGBA32,false);tex.ReadPixels(new Rect(0,0,768,768),0,0);tex.Apply();
    File.WriteAllBytes(folder+"/"+kind+".png",tex.EncodeToPNG());RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(tex);
    Debug.Log($"DRAUG_ARROW_FX_PREVIEW {kind} particles={obj.GetComponentsInChildren<ParticleSystem>(true).Length}");UnityEngine.Object.DestroyImmediate(obj);
   }
   Debug.Log("DRAUG_ARROW_FX_PREVIEW_PASS");
  }
  finally{camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(rt);}
  EditorApplication.Exit(0);
 }
}
