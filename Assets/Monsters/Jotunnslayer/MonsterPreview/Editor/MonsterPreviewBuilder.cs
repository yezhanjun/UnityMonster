using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Jotunnslayer.Preview
{
 public static class MonsterPreviewBuilder
 {
  public const string ScenePath="Assets/Monsters/Jotunnslayer/MonsterPreview/MonsterPreview.unity";
  const string Root="Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer";
  const string Folder="Assets/Monsters/Jotunnslayer/MonsterPreview";
  [MenuItem("Jotunnslayer/怪物预览/打开预览场景")]
  public static void Open()
  {if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())EditorSceneManager.OpenScene(ScenePath);}
  [MenuItem("Jotunnslayer/怪物预览/重建资源目录与场景")]
  public static void Build()
  {
   if(!Application.isBatchMode && !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
   var entries=new List<MonsterPreviewGallery.Entry>();
   foreach(string rawFolder in Directory.GetDirectories(Root).OrderBy(p=>p))
   {
    string folder=rawFolder.Replace('\\','/');
    var clips=new Dictionary<string,AnimationClip>();
    foreach(string file in Directory.GetFiles(folder,"*.anim",SearchOption.TopDirectoryOnly).OrderBy(p=>p))
    {var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file.Replace('\\','/'));if(clip)clips[clip.name]=clip;}
    if(Directory.Exists(folder+"/Compact960"))foreach(string file in Directory.GetFiles(folder+"/Compact960","*.anim"))
    {var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file.Replace('\\','/'));if(clip)clips[clip.name]=clip;}
    // Phase-specific clips are resources too; keep their original names.
    foreach(string sub in new[]{"Phase1","Phase2"})if(Directory.Exists(folder+"/"+sub))foreach(string file in Directory.GetFiles(folder+"/"+sub,"*.anim"))
    {var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(file.Replace('\\','/'));if(clip)clips[clip.name]=clip;}
    if(clips.Count==0)continue;
    var models=new Dictionary<string,GameObject>();
    foreach(string extension in new[]{"*.fbx","*.prefab"})foreach(string file in Directory.GetFiles(folder,extension))
    {
     var model=AssetDatabase.LoadAssetAtPath<GameObject>(file.Replace('\\','/'));
     if(model && model.GetComponentsInChildren<SkinnedMeshRenderer>(true).Length>0)models[Path.GetFileNameWithoutExtension(file)]=model;
    }
    var effects=new List<GameObject>();
    foreach(string file in Directory.GetFiles(folder,"*.prefab",SearchOption.AllDirectories))
    {
     string path=file.Replace('\\','/');if(Path.GetDirectoryName(file).Replace('\\','/')==folder || path.Contains("DraugDeathStaged"))continue;
     var effect=AssetDatabase.LoadAssetAtPath<GameObject>(path);
     if(effect && effect.GetComponentsInChildren<ParticleSystem>(true).Length>0)effects.Add(effect);
    }
    foreach(var model in models)
    {
     var animator=model.Value.GetComponent<Animator>();
     var avatar=AssetDatabase.LoadAssetAtPath<Avatar>(folder+"/DisplayAvatar.asset");if(!avatar && animator)avatar=animator.avatar;
     entries.Add(new MonsterPreviewGallery.Entry{id=model.Key,folder=folder,model=model.Value,avatar=avatar,clips=clips.Values.OrderBy(c=>c.name).ToArray(),effects=effects.OrderBy(e=>e.name).ToArray()});
    }
   }
   string catalogPath=Folder+"/MonsterPreviewGallery.asset";
   var gallery=AssetDatabase.LoadAssetAtPath<MonsterPreviewGallery>(catalogPath);
   if(!gallery){gallery=ScriptableObject.CreateInstance<MonsterPreviewGallery>();AssetDatabase.CreateAsset(gallery,catalogPath);}
   gallery.entries=entries.OrderBy(e=>e.id).ToArray();EditorUtility.SetDirty(gallery);
   var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
   RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.55f,.58f,.65f);
   var controller=new GameObject("Monster preview controls").AddComponent<MonsterPreviewRoom>();controller.gallery=gallery;
   var camera=new GameObject("Preview camera").AddComponent<Camera>();camera.tag="MainCamera";camera.backgroundColor=new Color(.055f,.075f,.11f);camera.clearFlags=CameraClearFlags.SolidColor;camera.nearClipPlane=.03f;camera.farClipPlane=250;
   camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;camera.gameObject.AddComponent<AudioListener>();controller.previewCamera=camera;
   var light=new GameObject("Key light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.6f;light.transform.rotation=Quaternion.Euler(45,-35,0);
   var fill=new GameObject("Fill light").AddComponent<Light>();fill.type=LightType.Directional;fill.intensity=.5f;fill.transform.rotation=Quaternion.Euler(35,140,0);
   var groundMaterial=MaterialAt("PreviewGround", "Universal Render Pipeline/Lit",new Color(.045f,.065f,.09f));
   controller.lineMaterial=MaterialAt("PreviewLines","Sprites/Default",Color.white);
   var ground=GameObject.CreatePrimitive(PrimitiveType.Plane);ground.name="One metre grid floor";ground.transform.localScale=Vector3.one*10;ground.GetComponent<Renderer>().sharedMaterial=groundMaterial;
   var grid=new GameObject("Metre grid");
   for(int i=-15;i<=15;i++)for(int direction=0;direction<2;direction++)
   {var line=new GameObject("Grid").AddComponent<LineRenderer>();line.transform.SetParent(grid.transform);line.sharedMaterial=controller.lineMaterial;line.widthMultiplier=i==0?.025f:.012f;line.startColor=line.endColor=i==0?new Color(.35f,.5f,.6f):new Color(.23f,.29f,.35f);line.positionCount=2;line.SetPosition(0,direction==0?new Vector3(i,.012f,-30):new Vector3(-30,.012f,i));line.SetPosition(1,direction==0?new Vector3(i,.012f,30):new Vector3(30,.012f,i));}
   AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene,ScenePath);
   Debug.Log("MONSTER_PREVIEW_BUILD_PASS models="+entries.Count+" clips="+entries.Sum(e=>e.clips.Length)+" effectLinks="+entries.Sum(e=>e.effects.Length));
  }
  static Material MaterialAt(string name,string shader,Color color)
  {string path=Folder+"/"+name+".mat";var material=AssetDatabase.LoadAssetAtPath<Material>(path);if(!material){material=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(material,path);}material.color=color;EditorUtility.SetDirty(material);return material;}
  public static void OpenAndPlay(){Open();EditorApplication.isPlaying=true;}
  public static void BuildAndValidate(){Build();SessionState.SetBool("MonsterPreviewValidation",true);EditorApplication.isPlaying=true;}
 }

 [InitializeOnLoad]
 public static class MonsterPreviewValidation
 {
  static double began;static int stage;static string failure;
  static MonsterPreviewValidation(){EditorApplication.update+=Tick;Application.logMessageReceived+=Log;EditorApplication.playModeStateChanged+=RoutePreview;}
  static void RoutePreview(PlayModeStateChange state)
  {
   if(state==PlayModeStateChange.ExitingEditMode && UnityEngine.SceneManagement.SceneManager.GetActiveScene().path==MonsterPreviewBuilder.ScenePath)
   {
    SessionState.SetString("MonsterPreviewPreviousStart",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));
    SessionState.SetBool("MonsterPreviewOwnsStart",true);
    EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(MonsterPreviewBuilder.ScenePath);
   }
   if((state==PlayModeStateChange.EnteredPlayMode || state==PlayModeStateChange.EnteredEditMode) && SessionState.GetBool("MonsterPreviewOwnsStart",false))
   {
    EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString("MonsterPreviewPreviousStart",""));
    SessionState.EraseBool("MonsterPreviewOwnsStart");
   }
  }
  static void Log(string text,string trace,LogType type)
  {if(SessionState.GetBool("MonsterPreviewValidation",false)&&(type==LogType.Exception||type==LogType.Error))failure=text;}
  static void Tick()
  {
   if(!SessionState.GetBool("MonsterPreviewValidation",false)||!EditorApplication.isPlaying)return;
   if(began==0)began=EditorApplication.timeSinceStartup;
   try
   {
    if(failure!=null)throw new Exception(failure);
    double age=EditorApplication.timeSinceStartup-began;
    if(age>90)throw new Exception("Preview startup timeout");
    var room=UnityEngine.Object.FindFirstObjectByType<MonsterPreviewRoom>();if(!room || !room.Model)return;
    if(stage==0 && age>2)
    {
     if(room.gallery.entries.Length<20)throw new Exception("Too few extracted model entries");
     var entry=room.gallery.entries[room.SelectedMonster];if(entry.id!="draug_thrower_sur")throw new Exception("Default preview absent");
     int action=Array.FindIndex(entry.clips,c=>c.name=="skill_throw");if(action<0)throw new Exception("Throw animation missing");room.SelectAction(action);room.paused=true;room.Scrub(.5f);
     int effect=Array.FindIndex(entry.effects,e=>e.name=="Draug_explosion");if(effect<0)throw new Exception("Explosion missing");room.PlayEffect(effect);room.paused=false;stage=1;
    }
    if(stage==1 && age>2.3)
    {
     Directory.CreateDirectory("Docs/Validation/MonsterPreview");Capture(room.previewCamera,"Docs/Validation/MonsterPreview/model-effect.png");
     ScreenCapture.CaptureScreenshot("Docs/Validation/MonsterPreview/controls.png");stage=2;
    }
    if(stage==2 && age>5)
    {
     room.paused=true;room.Scrub(.25f);if(Mathf.Abs(room.Clock-.25f)>.0001f)throw new Exception("Scrub failed");
     room.ClearEffect();if(room.Effect)throw new Exception("Effect cleanup failed");
     int original=room.SelectedMonster;int switched=0;
     foreach(string id in new[]{"beetle_sur","skeleton_1h_sur","draug_sur","goblin_melee_sur"})
     {int index=Array.FindIndex(room.gallery.entries,e=>e.id==id);if(index<0)continue;room.SelectMonster(index);room.Scrub(.2f);if(!room.Model || !room.Clip)throw new Exception("Model switch failed "+id);switched++;}
     room.SelectMonster(original);if(switched<3)throw new Exception("Insufficient model switching coverage");
     if(failure!=null)throw new Exception(failure);
     File.WriteAllText("Docs/Validation/MonsterPreview/verification.json","{\"status\":\"PASS_RUNTIME_PREVIEW_CONTROLS\",\"models\":"+room.gallery.entries.Length+",\"default\":\"draug_thrower_sur\",\"checks\":[\"scene loads\",\"action selection\",\"scrub\",\"effect spawn\",\"effect cleanup\",\"switch at least3 other models\"],\"limits\":\"Catalog availability is not full pose or combat fidelity validation of every monster. Range is explicitly marked custom except associated known attack radius.\"}");
     Debug.Log("MONSTER_PREVIEW_RUNTIME_PASS");SessionState.SetBool("MonsterPreviewValidation",false);EditorApplication.Exit(0);
    }
    if(age>60)throw new Exception("Preview startup timeout");
   }
   catch(Exception e){Debug.LogException(e);SessionState.SetBool("MonsterPreviewValidation",false);EditorApplication.Exit(1);}
  }
  static void Capture(Camera camera,string path)
  {
   var rt=new RenderTexture(1200,900,24);var previous=RenderTexture.active;var oldRect=camera.rect;
   var texture=new Texture2D(1200,900,TextureFormat.RGB24,false);
   try{camera.rect=new Rect(0,0,1,1);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1200,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
   finally{camera.targetTexture=null;camera.rect=oldRect;RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(rt);}
  }
 }
}
