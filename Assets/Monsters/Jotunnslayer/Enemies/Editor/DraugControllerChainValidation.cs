#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugControllerChainValidation
 {
  [Serializable] sealed class Result
  {
   public string scope="Original compiled controller with local Compact960 overrides on rebuilt Generic model versus original model/controller. Full idle/chase request->decide->attack->idle chains, matched manual frame steps. Candidate still depends on loaded source controller; no battle integration or native callbacks.";
   public int cases,frames,failedPoseFrames,failedStateFrames,bones,replacedOverrides;
   public float maxLocalPosition,maxRotationDegrees,maxRootPosition,maxScale,maxClockError;
   public List<Failure> failures=new List<Failure>();
  }
  [Serializable] sealed class Failure{public string entry,profile;public int index,frame;public float phase,position,rotation,root,clock;public bool state;}
  public static void Run()
  {
   const string folder="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new List<AssetBundle>();GameObject sourceModel=null,model=null;AnimatorOverrideController candidate=null;int exit=1;var result=new Result();
   try
   {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(folder,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(folder,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var original=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    sourceModel=Object.Instantiate(original.gameObject);sourceModel.SetActive(true);
    foreach(var component in sourceModel.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
    var source=sourceModel.GetComponent<Animator>();
    model=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur"));model.SetActive(true);
    var runtime=model.GetComponent<Animator>();
    var overrides=(AnimatorOverrideController)original.runtimeAnimatorController;
    var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(pairs);
    for(int i=0;i<pairs.Count;i++)
    {
     var pair=pairs[i];var effective=pair.Value?pair.Value:pair.Key;
     var baked=Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/"+effective.name);
     if(baked){pairs[i]=new KeyValuePair<AnimationClip,AnimationClip>(pair.Key,baked);result.replacedOverrides++;}
    }
    candidate=new AnimatorOverrideController(overrides.runtimeAnimatorController);candidate.ApplyOverrides(pairs);runtime.runtimeAnimatorController=candidate;
    foreach(var a in new[]{source,runtime}){a.fireEvents=false;a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
    var bones=sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(t=>t).Distinct().ToArray();result.bones=bones.Length;
    if(bones.Length!=37)throw new Exception("Unexpected skin bone count");
    var targets=model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,model.transform));
    var mapped=bones.Select(t=>targets[AnimationUtility.CalculateTransformPath(t,sourceModel.transform)]).ToArray();
    foreach(string profile in new[]{"30fps","60fps","120fps","variable"})
    foreach(string entry in new[]{"base.idle","base.chase"})
    foreach(float phase in new[]{0f,.049f,.25f,.75f})
    for(int index=0;index<3;index++)
    {
     foreach(var a in new[]{source,runtime}){a.Rebind();a.Update(0);a.SetFloat("idle_anim_speed",1);a.SetInteger("attack_index",index);a.Play(entry,0,phase);a.Update(0);a.SetTrigger("attack");}
     float[] steps=profile=="30fps"?new[]{1f/30}:profile=="60fps"?new[]{1f/60}:profile=="120fps"?new[]{1f/120}:new[]{1f/60,1f/120,1f/30,.007f,.025f,0f,.05f,.004f};
     bool attacked=false,finished=false;
     for(int frame=0;frame<500;frame++)
     {
      float delta=steps[frame%steps.Length];source.Update(delta);runtime.Update(delta);result.frames++;
      var sc=source.GetCurrentAnimatorStateInfo(0);var rc=runtime.GetCurrentAnimatorStateInfo(0);var sn=source.GetNextAnimatorStateInfo(0);var rn=runtime.GetNextAnimatorStateInfo(0);
      bool st=source.IsInTransition(0),rt=runtime.IsInTransition(0);
      bool state=sc.fullPathHash==rc.fullPathHash && st==rt && (!st || sn.fullPathHash==rn.fullPathHash);
      float clock=Mathf.Abs(sc.normalizedTime-rc.normalizedTime);
      if(st){clock=Mathf.Max(clock,Mathf.Abs(sn.normalizedTime-rn.normalizedTime));clock=Mathf.Max(clock,Mathf.Abs(source.GetAnimatorTransitionInfo(0).normalizedTime-runtime.GetAnimatorTransitionInfo(0).normalizedTime));}
      state &= clock<=.00001f;result.maxClockError=Mathf.Max(result.maxClockError,clock);
      float position=0,rotation=0,root=0,scale=0;
      for(int b=0;b<bones.Length;b++)
      {
       var a=bones[b];var target=mapped[b];position=Mathf.Max(position,Vector3.Distance(a.localPosition,target.localPosition));
       rotation=Mathf.Max(rotation,Quaternion.Angle(a.localRotation,target.localRotation));scale=Mathf.Max(scale,Vector3.Distance(a.localScale,target.localScale));
       root=Mathf.Max(root,Vector3.Distance(sourceModel.transform.InverseTransformPoint(a.position),model.transform.InverseTransformPoint(target.position)));
      }
      bool pose=position<=.0001f && rotation<=.05f && root<=.001f && scale<=.0001f;
      result.maxLocalPosition=Mathf.Max(result.maxLocalPosition,position);result.maxRotationDegrees=Mathf.Max(result.maxRotationDegrees,rotation);result.maxRootPosition=Mathf.Max(result.maxRootPosition,root);result.maxScale=Mathf.Max(result.maxScale,scale);
      if(!pose)result.failedPoseFrames++;if(!state)result.failedStateFrames++;
      if((!pose || !state) && result.failures.Count<30)result.failures.Add(new Failure{entry=entry,profile=profile,index=index,frame=frame,phase=phase,position=position,rotation=rotation,root=root,clock=clock,state=state});
      if(sc.IsName("base.attack.attack_0"+(index+1)))attacked=true;
      if(attacked && !st && sc.IsName("base.idle")){finished=true;break;}
     }
     if(!finished)throw new Exception("Chain did not return to idle "+entry+" "+profile+" "+phase+" "+index);
     result.cases++;
    }
    File.WriteAllText("Docs/Validation/DraugControllerChainVerification.json",JsonUtility.ToJson(result,true));
    Debug.Log($"DRAUG_CONTROLLER_CHAIN_{(result.failedPoseFrames==0 && result.failedStateFrames==0?"PASS":"FAIL")} cases={result.cases} frames={result.frames} poseFailures={result.failedPoseFrames} stateFailures={result.failedStateFrames}");
    exit=result.failedPoseFrames==0 && result.failedStateFrames==0?0:1;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{if(model)Object.DestroyImmediate(model);if(sourceModel)Object.DestroyImmediate(sourceModel);if(candidate)Object.DestroyImmediate(candidate);foreach(var b in bundles)if(b)b.Unload(true);}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
