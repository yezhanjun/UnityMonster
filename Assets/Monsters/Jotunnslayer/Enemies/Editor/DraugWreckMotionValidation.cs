using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.Universal;

public static class DraugWreckMotionValidation
{
 [Serializable] class StartCase {public int bodyCount,index;public float sample;}
 [Serializable] class Starts {public StartCase[] cases;}
 public static void Run()
 {
  var starts=JsonUtility.FromJson<Starts>(File.ReadAllText("Docs/Validation/DraugWreckStartIndexVerification.json"));
  foreach(var row in starts.cases)if(DraugWreckSelection.StartIndex(row.bodyCount,row.sample)!=row.index)throw new Exception("Native start index mismatch");
  EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
  var previousMode=Physics.simulationMode;Physics.simulationMode=SimulationMode.Script;
  GameObject root=null;RenderTexture rt=null;
  float maxMovement=0,removedAt=-1;
  try
  {
   root=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/DeathFx/DraugDeathDecodedStage"));
   var wreck=root.transform.Find("wrecks");if(!wreck)throw new Exception("Source wreck group absent");
   var bodies=wreck.GetComponentsInChildren<Rigidbody>(true);var colliders=wreck.GetComponentsInChildren<Collider>(true);
   if(bodies.Length!=9)throw new Exception("Expected nine source bodies");
   var initial=new Vector3[bodies.Length];
   // Check both source direction branches before the trajectory fixture.
   DraugWreckLaunch.Apply(bodies[0],bodies[0].transform.position-Vector3.right,0,0,3,new Vector3(1,-2,.5f),.52f);
   if((bodies[0].linearVelocity-new Vector3(1.56f,0,0)).sqrMagnitude>1e-12f || bodies[0].angularVelocity!=new Vector3(1,-2,.5f))throw new Exception("Center-relative launch mismatch");
   DraugWreckLaunch.Apply(bodies[0],null,0,0,6,Vector3.zero,.52f);
   if((bodies[0].linearVelocity-new Vector3(0,3.12f,0)).sqrMagnitude>1e-12f)throw new Exception("Null-center upward launch mismatch");
   for(int i=0;i<bodies.Length;i++)
   {
    initial[i]=bodies[i].position;
    // Deterministic diagnostic samples within source ranges; not source RNG replay.
    DraugWreckLaunch.Apply(bodies[i],null,i-4,i*2-8,3+i*.375f,new Vector3(i*.5f-2,0,0),.52f);
   }
   var retirement=new DraugWreckRetirement(wreck,bodies,colliders);
   var particles=root.GetComponentsInChildren<ParticleSystem>(true);
   for(int i=0;i<particles.Length;i++){particles[i].useAutoRandomSeed=false;particles[i].randomSeed=(uint)(123+i);}
   new DraugDeathParticleLod(root,root.transform.Find("lod1").gameObject,root.transform.Find("lod2").gameObject,root.transform.Find("lod3").gameObject).Begin(0);
   var selectedParticles=new bool[particles.Length];for(int i=0;i<particles.Length;i++)selectedParticles[i]=particles[i].isPlaying;
   var camera=new GameObject("Motion camera").AddComponent<Camera>();camera.transform.position=new Vector3(3,2,4);camera.transform.LookAt(new Vector3(0,.3f,0));camera.orthographic=true;camera.orthographicSize=3;
   camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.13f,.16f,1);camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
   var light=new GameObject("Motion light").AddComponent<Light>();light.type=LightType.Directional;light.intensity=2;light.transform.rotation=Quaternion.Euler(45,-35,0);
   rt=new RenderTexture(768,768,24);rt.Create();camera.targetTexture=rt;
   const string folder="Docs/Validation/DraugWreckMotionFinalPreview";Directory.CreateDirectory(folder);
   Physics.SyncTransforms();
   for(int frame=1;frame<=180;frame++)
   {
    const float dt=1f/60;Physics.Simulate(dt);
    for(int i=0;i<particles.Length;i++)if(selectedParticles[i])particles[i].Simulate(dt,false,false,true);
    if(removedAt<0)
    {
     for(int i=0;i<bodies.Length;i++)maxMovement=Mathf.Max(maxMovement,Vector3.Distance(initial[i],bodies[i].position));
     if(retirement.Tick(dt)){removedAt=frame*dt;wreck.gameObject.SetActive(false);}
    }
    if(frame==20 || frame==60 || frame==132)
    {
     int particleCount=0;foreach(var ps in particles)particleCount+=ps.particleCount;
     Debug.Log($"DRAUG_WRECK_MOTION_FRAME frame={frame} particles={particleCount}");
     if(frame==20 && particleCount==0)throw new Exception("Diagnostic particles did not advance");
     camera.Render();var previous=RenderTexture.active;RenderTexture.active=rt;
     var image=new Texture2D(768,768,TextureFormat.RGBA32,false);
     try{image.ReadPixels(new Rect(0,0,768,768),0,0);image.Apply();File.WriteAllBytes(folder+"/frame-"+frame+".png",image.EncodeToPNG());}
     finally{RenderTexture.active=previous;UnityEngine.Object.DestroyImmediate(image);}
    }
   }
   if(maxMovement<.05f || removedAt<0 || removedAt>2.5f)throw new Exception("Physics/retirement chain failed");
   foreach(var collider in colliders)if(collider.enabled)throw new Exception("Collider remained enabled after retirement");
   File.WriteAllText("Docs/Validation/DraugWreckMotionFinalVerification.json",$"{{\"status\":\"PASS_CONTROLLED_PHYSICS_AND_RETIREMENT\",\"startIndexCases\":{starts.cases.Length},\"bodies\":9,\"frames\":180,\"maxDisplacement\":{maxMovement.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"removeTime\":{removedAt.ToString(System.Globalization.CultureInfo.InvariantCulture)},\"limits\":\"Fixed diagnostic samples, no ground plane/source RNG trajectory/optimized spawn/global registry/audio/bodyshader fidelity or real lethal integration.\"}}");
   Debug.Log($"DRAUG_WRECK_MOTION_PASS starts={starts.cases.Length} displacement={maxMovement} removedAt={removedAt}");
  }
  finally{Physics.simulationMode=previousMode;if(root)UnityEngine.Object.DestroyImmediate(root);if(rt)UnityEngine.Object.DestroyImmediate(rt);}
  EditorApplication.Exit(0);
 }
}
