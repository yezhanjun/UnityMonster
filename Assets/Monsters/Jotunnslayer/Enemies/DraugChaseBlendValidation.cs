#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugChaseBlendValidation
 {
  [Serializable] sealed class IdleEvidence { public string status;public float idleAnimationSpeed; }
  [Serializable] sealed class Result
  {
   public string scope="Original Animator chase->decide transition versus Skeleton1HVisual mixer, four entry phases, first150ms at1ms steps. Native callbacks absent; checked idle parameter initialized. Excludes later attack entry.";
   public int bones,failedFrames;public List<Frame> frames=new List<Frame>();
  }
  [Serializable] sealed class Frame
  {
   public int index;public float entryPhase,time,maxLocalPosition,maxRotationDegrees,maxRootPosition,maxScale;
   public string worstBone;public bool passed;
  }
  static bool attackBlend,returnBlend,idleEntry,freezePrevious;
  public static void RunIdleSnapshotBlend(){attackBlend=idleEntry=freezePrevious=true;Run();}
  public static void RunIdleAttackBlend(){attackBlend=idleEntry=true;Run();}
  public static void RunReturnBlend(){attackBlend=returnBlend=true;Run();}
  public static void RunAttackBlend(){attackBlend=true;Run();}
  public static void Run()
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   var bundles=new List<AssetBundle>();GameObject sourceModel=null,model=null;Skeleton1HVisual visual=null;
   var result=new Result();int exit=1;
   if(attackBlend)result.scope="Isolated source decide->attack versus runtime mixer at matching initial clip times, four decision phases x3 clips, first70ms including completion. Does not cover chained scheduling or interrupted idle entry.";
   if(returnBlend)result.scope="Isolated source attack->idle versus runtime mixer, three attacks x4 initial attack phases warmed to actual source exit;130ms including completion. Source-aligned start excludes combat scheduling.";
   if(idleEntry)result.scope="Source idle->decide interrupted by attack versus current direct idle->attack mixer; zero-time entry cascade aligns attack clocks;4 idle phases x3 attacks x70ms. Tests interrupted pose retention, not request-to-entry scheduling.";
   try
   {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(source,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    var original=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    sourceModel=Object.Instantiate(original.gameObject);sourceModel.SetActive(true);
    foreach(var component in sourceModel.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
    var animator=sourceModel.GetComponent<Animator>();animator.fireEvents=false;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    model=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur"));model.SetActive(true);
    var runtime=model.GetComponent<Animator>();runtime.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    visual=model.AddComponent<Skeleton1HVisual>();typeof(Skeleton1HVisual).GetMethod("Awake",flags).Invoke(visual,null);
    AnimationClip Clip(string name)=>Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/"+name);
    visual.Bind(Clip("summon_01"),Clip("stand"),Clip("move"),Clip("skill_throw"),Clip("stand"),null);
    var bones=sourceModel.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r=>r.bones).Where(t=>t).Distinct().ToArray();result.bones=bones.Length;
    if(bones.Length!=37)throw new Exception("Unexpected skin bone count");
    var targets=model.GetComponentsInChildren<Transform>(true).ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,model.transform));
    var pairs=bones.Select(t=>new{a=t,path=AnimationUtility.CalculateTransformPath(t,sourceModel.transform)}).ToArray();
    foreach(var pair in pairs)if(!targets.ContainsKey(pair.path))throw new Exception("Missing bone "+pair.path);
    var evidence=JsonUtility.FromJson<IdleEvidence>(File.ReadAllText("Docs/Validation/DraugIdleSpeedEvidence.json"));
    if(evidence.status!="STATIC_NATIVE_ASSIGNMENT_VERIFIED")throw new Exception("Missing idle initialization evidence");
    foreach(float phase in new[]{0f,.25f,.5f,.75f})
    for(int index=0;index<(attackBlend?3:1);index++)
    {
     animator.Rebind();animator.Update(0);animator.SetFloat("idle_anim_speed",evidence.idleAnimationSpeed);animator.SetInteger("attack_index",index);
     var attackClip=visual.DraugArrowClip(index);
     animator.Play(idleEntry?"base.idle":returnBlend?"base.attack.attack_0"+(index+1):attackBlend?"base.attack.decide":"base.chase",0,phase);animator.Update(0);
     if(returnBlend)
     {
      int warmup=0;
      while(!animator.IsInTransition(0) && warmup++<1500)animator.Update(.001f);
      if(!animator.IsInTransition(0))throw new Exception("Source attack never exited");
     }
     visual.Play(returnBlend?Skeleton1HVisual.Motion.Attack:attackBlend?Skeleton1HVisual.Motion.Idle:Skeleton1HVisual.Motion.Walk,true,
      speed:returnBlend || idleEntry?1f:attackBlend?DraugArrowTimeline.DecideSpeed:1f,overrideClip:returnBlend?attackClip:null,
      startSeconds:returnBlend?animator.GetCurrentAnimatorStateInfo(0).normalizedTime*attackClip.length:phase*Clip(attackBlend?"stand":"move").length);
     var graph=(PlayableGraph)typeof(Skeleton1HVisual).GetField("graph",flags).GetValue(visual);
     graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Evaluate(0);
     if(!attackBlend || idleEntry){animator.SetTrigger("attack");animator.Update(0);}
     if(attackBlend && !returnBlend)animator.Update(0); // Evaluate the newly entered decision state's outgoing transition without advancing time.
     string destination=returnBlend?"base.idle":attackBlend?"base.attack.attack_0"+(index+1):"base.attack.decide";
     if(!animator.IsInTransition(0) || animator.GetNextAnimatorStateInfo(0).fullPathHash!=Animator.StringToHash(destination))throw new Exception("Source did not enter expected blend "+destination);
     float observedBlendElapsed=returnBlend?animator.GetAnimatorTransitionInfo(0).normalizedTime*animator.GetAnimatorTransitionInfo(0).duration:0f;
     float observedIdleTime=returnBlend?animator.GetNextAnimatorStateInfo(0).normalizedTime*Clip("stand").length:0f;
     if(returnBlend)Debug.Log($"DRAUG_RETURN_ALIGNMENT index={index} phase={phase} sourceBlendElapsed={observedBlendElapsed} nextClipTime={observedIdleTime}");
     visual.Play(attackBlend && !returnBlend?Skeleton1HVisual.Motion.Attack:Skeleton1HVisual.Motion.Idle,true,
      speed:attackBlend?1f:DraugArrowTimeline.DecideSpeed,overrideClip:attackBlend && !returnBlend?attackClip:null,
      startSeconds:returnBlend?observedIdleTime:attackBlend?attackClip.length*DraugArrowTimeline.EntryNormalizedTime(index):Clip("stand").length*.6590912342f,
      blendSeconds:returnBlend?DraugArrowTimeline.ReturnBlendSeconds:attackBlend?DraugArrowTimeline.AttackBlendSeconds:DraugArrowTimeline.ChaseToDecideSeconds,freezePrevious:freezePrevious);
     if(returnBlend)visual.AdvanceTransition(observedBlendElapsed);
     graph.Evaluate(0);
     if(Mathf.Abs(animator.GetNextAnimatorStateInfo(0).speed-(attackBlend?1f:DraugArrowTimeline.DecideSpeed))>.00001f)throw new Exception("Decision state speed mismatch");
     for(int step=1;step<=(returnBlend?130:attackBlend?70:150);step++)
     {
      animator.Update(.001f);visual.AdvanceTransition(.001f);graph.Evaluate(.001f);
      bool validState=animator.IsInTransition(0)?animator.GetNextAnimatorStateInfo(0).fullPathHash==Animator.StringToHash(destination):attackBlend && animator.GetCurrentAnimatorStateInfo(0).fullPathHash==Animator.StringToHash(destination);
      if(!validState)throw new Exception("Source left isolated blend early");
      var item=new Frame{index=index,entryPhase=phase,time=step*.001f};
      foreach(var pair in pairs)
      {
       var a=pair.a;var b=targets[pair.path];
       item.maxLocalPosition=Mathf.Max(item.maxLocalPosition,Vector3.Distance(a.localPosition,b.localPosition));
       float angle=Quaternion.Angle(a.localRotation,b.localRotation);
       if(angle>item.maxRotationDegrees){item.maxRotationDegrees=angle;item.worstBone=pair.path;}
       item.maxRootPosition=Mathf.Max(item.maxRootPosition,Vector3.Distance(sourceModel.transform.InverseTransformPoint(a.position),model.transform.InverseTransformPoint(b.position)));
       item.maxScale=Mathf.Max(item.maxScale,Vector3.Distance(a.localScale,b.localScale));
      }
      item.passed=item.maxLocalPosition<=.0001f && item.maxRotationDegrees<=.05f && item.maxRootPosition<=.001f && item.maxScale<=.0001f;
      if(!item.passed)result.failedFrames++;
      result.frames.Add(item);
     }
     if(attackBlend && (graph.GetPlayableCount()!=1 || animator.IsInTransition(0)))throw new Exception("Blend did not settle or retained old playable nodes");
    }
    File.WriteAllText(freezePrevious?"Docs/Validation/DraugIdleSnapshotBlendVerification.json":idleEntry?"Docs/Validation/DraugIdleInterruptedBlendVerification.json":returnBlend?"Docs/Validation/DraugAttackReturnBlendVerification.json":attackBlend?"Docs/Validation/DraugDecideAttackBlendVerification.json":"Docs/Validation/DraugChaseBlendVerification.json",JsonUtility.ToJson(result,true));
    Debug.Log($"DRAUG_{(freezePrevious?"IDLE_SNAPSHOT":idleEntry?"IDLE_INTERRUPTED":returnBlend?"ATTACK_RETURN":attackBlend?"DECIDE_ATTACK":"CHASE")}_BLEND_{(result.failedFrames==0?"PASS":"FAIL")} frames={result.frames.Count} failed={result.failedFrames} bones={result.bones}");
    exit=result.failedFrames==0?0:1;
   }
   catch(Exception error){Debug.LogException(error);}
   finally
   {
    if(visual)typeof(Skeleton1HVisual).GetMethod("OnDisable",flags).Invoke(visual,null);
    if(model)Object.DestroyImmediate(model);if(sourceModel)Object.DestroyImmediate(sourceModel);
    foreach(var bundle in bundles)if(bundle)bundle.Unload(true);
   }
   EditorApplication.Exit(exit);
  }
 }
}
#endif
