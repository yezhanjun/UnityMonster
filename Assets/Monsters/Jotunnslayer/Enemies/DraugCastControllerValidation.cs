#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal sealed class DraugCastEventProbe : MonoBehaviour
 {
  internal float clock,releaseAt=-1,endAt=-1,releaseWeight,endWeight;internal int releases,ends;
  internal void ResetProbe(){clock=0;releaseAt=endAt=-1;releases=ends=0;releaseWeight=endWeight=0;}
  void OnCastEvent(AnimationEvent e){releases++;releaseAt=clock;releaseWeight=e.animatorClipInfo.weight;}
  void OnCastEndEvent(AnimationEvent e){ends++;endAt=clock;endWeight=e.animatorClipInfo.weight;}
  void OnStep(AnimationEvent e){}
 }
 internal static class DraugCastControllerValidation
 {
  const string PlayKey="DraugCastControllerValidation.Play";
  [InitializeOnLoadMethod] static void Resume(){if(SessionState.GetBool(PlayKey,false))EditorApplication.playModeStateChanged+=OnPlay;}
  public static void RunPlayMode(){SessionState.SetBool(PlayKey,true);EditorApplication.playModeStateChanged+=OnPlay;EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
  static void OnPlay(PlayModeStateChange state){if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey,false))return;SessionState.EraseBool(PlayKey);EditorApplication.playModeStateChanged-=OnPlay;Run();}
  [Serializable] sealed class Result
  {
   public string scope="Source original model/compiled controller versus rebuilt Generic model with Compact960 overrides. Actual AnimationEvent delivery captured in test receivers, native AI handlers absent. Manual matched Animator.Update in PlayMode; complete idle/chase cast chains.";
   public int frames,bones,failedFrames;public float maxPosition,maxRotation,maxRoot,maxScale,maxClock;public List<Case> cases=new List<Case>();
  }
  [Serializable] sealed class Case
  {
   public string entry,profile;public float phase,firstCastAt=-1,releaseCrossAt=-1,endCrossAt=-1,returnAt=-1,finishAt=-1;
   public float sourceReleaseAt,sourceEndAt,runtimeReleaseAt,runtimeEndAt,sourceEndWeight,runtimeEndWeight;
   public int sourceReleases,sourceEnds,runtimeReleases,runtimeEnds;public bool passed;
  }
  public static void Run()
  {
   const string folder="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new List<AssetBundle>();GameObject sourceModel=null,model=null;AnimatorOverrideController candidate=null;var result=new Result();int exit=1;
   try
   {
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(folder,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(folder,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var original=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    sourceModel=Object.Instantiate(original.gameObject);sourceModel.SetActive(true);
    foreach(var component in sourceModel.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
    var source=sourceModel.GetComponent<Animator>();var sp=sourceModel.AddComponent<DraugCastEventProbe>();
    model=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur"));model.SetActive(true);
    var runtime=model.GetComponent<Animator>();var rp=model.AddComponent<DraugCastEventProbe>();
    var overrides=(AnimatorOverrideController)original.runtimeAnimatorController;var pairs=new List<KeyValuePair<AnimationClip,AnimationClip>>();overrides.GetOverrides(pairs);
    foreach(var pair in pairs.ToArray())
    {
     var effective=pair.Value?pair.Value:pair.Key;var baked=Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/"+effective.name);
     if(baked)pairs[pairs.IndexOf(pair)]=new KeyValuePair<AnimationClip,AnimationClip>(pair.Key,baked);
    }
    candidate=new AnimatorOverrideController(overrides.runtimeAnimatorController);candidate.ApplyOverrides(pairs);runtime.runtimeAnimatorController=candidate;
    foreach(var a in new[]{source,runtime}){a.applyRootMotion=false;a.cullingMode=AnimatorCullingMode.AlwaysAnimate;}
    var bones=sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(t=>t).Distinct().ToArray();result.bones=bones.Length;
    if(bones.Length!=37)throw new Exception("Unexpected source bone count");
    var targets=model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,model.transform));
    var mapped=bones.Select(t=>targets[AnimationUtility.CalculateTransformPath(t,sourceModel.transform)]).ToArray();
    int castHash=Animator.StringToHash("base.cast_tree"),idleHash=Animator.StringToHash("base.idle");
    foreach(string profile in new[]{"1ms","30fps","60fps","120fps","variable"})
    foreach(string entry in new[]{"base.idle","base.chase"})
    foreach(float phase in new[]{0f,.049f,.25f,.75f})
    {
     sp.ResetProbe();rp.ResetProbe();
     foreach(var a in new[]{source,runtime}){a.fireEvents=false;a.Rebind();a.Update(0);a.SetFloat("idle_anim_speed",1);a.SetFloat("cast_sequence",1);a.Play(entry,0,phase);a.Update(0);a.fireEvents=true;a.SetTrigger("cast");}
     var item=new Case{profile=profile,entry=entry,phase=phase};
     float[] steps=profile=="1ms"?new[]{.001f}:profile=="30fps"?new[]{1f/30}:profile=="60fps"?new[]{1f/60}:profile=="120fps"?new[]{1f/120}:new[]{1f/60,1f/120,1f/30,.007f,.025f,0f,.05f,.004f};
     float elapsed=0;int beforeFailures=result.failedFrames;
     for(int frame=0;frame<4000;frame++)
     {
      float delta=steps[frame%steps.Length];elapsed+=delta;sp.clock=rp.clock=elapsed;source.Update(delta);runtime.Update(delta);result.frames++;
      var sc=source.GetCurrentAnimatorStateInfo(0);var rc=runtime.GetCurrentAnimatorStateInfo(0);var sn=source.GetNextAnimatorStateInfo(0);var rn=runtime.GetNextAnimatorStateInfo(0);
      bool st=source.IsInTransition(0),rt=runtime.IsInTransition(0);float clock=Mathf.Abs(sc.normalizedTime-rc.normalizedTime);
      if(st)clock=Mathf.Max(clock,Mathf.Abs(sn.normalizedTime-rn.normalizedTime));result.maxClock=Mathf.Max(result.maxClock,clock);
      bool state=sc.fullPathHash==rc.fullPathHash && st==rt && (!st || sn.fullPathHash==rn.fullPathHash) && clock<.00001f;
      float position=0,rotation=0,root=0,scale=0;
      for(int i=0;i<bones.Length;i++)
      {
       var a=bones[i];var b=mapped[i];position=Mathf.Max(position,Vector3.Distance(a.localPosition,b.localPosition));rotation=Mathf.Max(rotation,Quaternion.Angle(a.localRotation,b.localRotation));scale=Mathf.Max(scale,Vector3.Distance(a.localScale,b.localScale));
       root=Mathf.Max(root,Vector3.Distance(sourceModel.transform.InverseTransformPoint(a.position),model.transform.InverseTransformPoint(b.position)));
      }
      result.maxPosition=Mathf.Max(result.maxPosition,position);result.maxRotation=Mathf.Max(result.maxRotation,rotation);result.maxRoot=Mathf.Max(result.maxRoot,root);result.maxScale=Mathf.Max(result.maxScale,scale);
      if(!state || position>.0001f || rotation>.05f || root>.001f || scale>.0001f)result.failedFrames++;
      bool inCast=sc.fullPathHash==castHash || st && sn.fullPathHash==castHash;
      if(inCast)
      {
       var cs=sc.fullPathHash==castHash?sc:sn;float time=cs.normalizedTime*cs.length;
       if(item.firstCastAt<0)item.firstCastAt=elapsed;
       if(item.releaseCrossAt<0 && time>=.49205490946769714f)item.releaseCrossAt=elapsed;
       if(item.endCrossAt<0 && time>=1.1088453531265259f)item.endCrossAt=elapsed;
       if(item.returnAt<0 && sc.fullPathHash==castHash && st && sn.fullPathHash==idleHash)item.returnAt=elapsed;
      }
      if(item.returnAt>=0 && !st && sc.fullPathHash==idleHash){item.finishAt=elapsed;break;}
     }
     item.sourceReleaseAt=sp.releaseAt;item.sourceEndAt=sp.endAt;item.runtimeReleaseAt=rp.releaseAt;item.runtimeEndAt=rp.endAt;item.sourceEndWeight=sp.endWeight;item.runtimeEndWeight=rp.endWeight;
     item.sourceReleases=sp.releases;item.sourceEnds=sp.ends;item.runtimeReleases=rp.releases;item.runtimeEnds=rp.ends;
     item.passed=item.finishAt>0 && sp.releases==1 && sp.ends==1 && rp.releases==1 && rp.ends==1 && sp.releaseAt==rp.releaseAt && sp.endAt==rp.endAt && result.failedFrames==beforeFailures;
     result.cases.Add(item);
    }
    File.WriteAllText("Docs/Validation/DraugCastControllerVerification.json",JsonUtility.ToJson(result,true));
    bool passed=result.cases.Count==40 && result.cases.All(c=>c.passed);
    Debug.Log($"DRAUG_CAST_CONTROLLER_{(passed?"PASS":"FAIL")} cases={result.cases.Count} frames={result.frames} failedFrames={result.failedFrames} failedCases={result.cases.Count(c=>!c.passed)}");exit=passed?0:1;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{if(model)Object.DestroyImmediate(model);if(sourceModel)Object.DestroyImmediate(sourceModel);if(candidate)Object.DestroyImmediate(candidate);foreach(var b in bundles)if(b)b.Unload(true);}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
