#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object = UnityEngine.Object;

namespace Jotunnslayer
{
 internal static class DraugAnimatorTransitionValidation
 {
  [Serializable] sealed class Result { public string scope="Source Animator evaluated in 1ms steps; original script callbacks absent and events disabled. Event crossing is inferred from clip time, not an original callback execution."; public List<Case> cases=new List<Case>(); }
  [Serializable] sealed class EntryFrame
  {
   public int transitionHash;public float transitionDuration;public string durationUnit;
   public int frame;public float time,delta,currentTime,nextTime,transitionProgress,currentWeight,nextWeight;
   public string currentState,nextState,currentClip,nextClip;public bool transitioning;
  }
  static EntryFrame Capture(Animator animator,int frame,float elapsed,float step)
  {
   var current=animator.GetCurrentAnimatorStateInfo(0);var next=animator.GetNextAnimatorStateInfo(0);
   bool transitioning=animator.IsInTransition(0);var transition=animator.GetAnimatorTransitionInfo(0);
   var cc=animator.GetCurrentAnimatorClipInfo(0).FirstOrDefault();
   var nc=transitioning?animator.GetNextAnimatorClipInfo(0).FirstOrDefault():default;
   return new EntryFrame{frame=frame,time=elapsed,delta=step,currentState=StateName(current),nextState=StateName(next),
    currentTime=current.normalizedTime,nextTime=next.normalizedTime,transitioning=transitioning,transitionProgress=transition.normalizedTime,
    transitionHash=transition.fullPathHash,transitionDuration=transition.duration,durationUnit=transition.durationUnit.ToString(),
    currentClip=cc.clip?cc.clip.name:"",nextClip=nc.clip?nc.clip.name:"",currentWeight=cc.weight,nextWeight=nc.weight};
  }
  static string StateName(AnimatorStateInfo state)
  {
   foreach(string name in new[]{"base.idle","base.chase","base.attack.decide","base.attack.attack_01","base.attack.attack_02","base.attack.attack_03"})
    if(state.fullPathHash==Animator.StringToHash(name))return name;
   return state.fullPathHash.ToString();
  }
  [Serializable] sealed class Case
  {
   public string entry,clip,transitionDurationUnit,stepProfile;
   public float firstTransitionProgress,exitTransitionProgress,exitBlendElapsed,idleTimeAtExit;
   public EntryFrame beforeTrigger;
   public int index;
   public List<EntryFrame> entryFrames=new List<EntryFrame>();
   public float entryNormalizedTime,firstClipAt,firstNormalizedTime,transitionDuration,settledAt,eventCrossAt,exitStartAt,exitNormalizedTime,idleSettledAt,attackSpeed,clipLength;
  }
  [Serializable] sealed class IdleEvidence { public string status;public float idleAnimationSpeed; }
  static bool configured,frameSteps,boundarySteps,localController;
  public static void RunLocalController(){configured=frameSteps=localController=true;Run();}
  public static void RunBoundarySteps(){configured=true;frameSteps=true;boundarySteps=true;Run();}
  public static void RunFrameSteps(){configured=true;frameSteps=true;Run();}
  public static void RunConfigured(){configured=true;Run();}
  public static void Run()
  {
   const string source="E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
   var bundles=new List<AssetBundle>();AnimatorOverrideController localCandidate=null;GameObject localTemplate=null;GameObject model=null;var result=new Result();int exit=1;
   try
   {
    EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    Animator original;
    if(localController)
    {
     localCandidate=DraugLocalControllerCandidate.Load(bundles);
     localTemplate=Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur"));
     original=localTemplate.GetComponent<Animator>();original.runtimeAnimatorController=localCandidate;
    }
    else
    {
    foreach(string pattern in new[]{"*monoscripts*.bundle","*unitybuiltinassets*.bundle","duplicateassetisolation*.bundle"})
     foreach(string file in Directory.GetFiles(source,pattern))bundles.Add(AssetBundle.LoadFromFile(file));
    var main=AssetBundle.LoadFromFile(Directory.GetFiles(source,"enemies_assets_draug_thrower_sur_*.bundle").Single());bundles.Add(main);
    var prefab=main.GetAllAssetNames().Select(n=>main.LoadAsset<GameObject>(n)).First(p=>p && p.name=="draug_thrower_sur");
    original=prefab.GetComponentsInChildren<Animator>(true).First(a=>a.runtimeAnimatorController);
    }
    foreach(string profile in frameSteps?new[]{"30fps","60fps","120fps","variable"}:new[]{"1ms"})
    foreach(string entry in new[]{"base.idle","base.chase","base.attack.decide"})
     foreach(float entryTime in boundarySteps?new[]{0f,.01f,.024f,.03f,.049f,.05f,.051f,.99f,1.01f,1.049f,1.051f}:new[]{0f,.25f,.5f,.75f})
     for(int index=0;index<3;index++)
     {
      if(boundarySteps && (entry=="base.attack.decide" || index!=0))continue;
      if(entry=="base.attack.decide" && entryTime>0)continue;
      model=Object.Instantiate(original.gameObject);model.SetActive(true);
      foreach(var component in model.GetComponentsInChildren<MonoBehaviour>(true))if(component)Object.DestroyImmediate(component);
      var animator=model.GetComponent<Animator>();animator.fireEvents=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
      animator.Rebind();animator.Update(0);animator.SetInteger("attack_index",index);
      if(configured)
      {
       var evidence=JsonUtility.FromJson<IdleEvidence>(File.ReadAllText("Docs/Validation/DraugIdleSpeedEvidence.json"));
       if(evidence.status!="STATIC_NATIVE_ASSIGNMENT_VERIFIED")throw new Exception("Missing native idle initialization evidence");
       animator.SetFloat("idle_anim_speed",evidence.idleAnimationSpeed);
       result.scope="Source Animator with serialized idle speed applied from verified native assignment, evaluated at1ms; native callbacks not executed; event crossing inferred from clip time.";
      }
      animator.Play(entry,0,entryTime);animator.Update(0);
      var beforeTrigger=Capture(animator,0,0,0);
      if(entry!="base.attack.decide")animator.SetTrigger("attack");
      var observation=new Case{beforeTrigger=beforeTrigger,stepProfile=profile,entry=entry,entryNormalizedTime=entryTime,index=index,firstClipAt=-1,settledAt=-1,eventCrossAt=-1,exitStartAt=-1,idleSettledAt=-1,attackSpeed=animator.GetFloat("attack_anim_speed")};
      float eventTime=new[]{.5131691694f,.5551713705f,.4536709189f}[index];
      float elapsed=0;
      float[] steps=profile=="30fps"?new[]{1f/30}:profile=="60fps"?new[]{1f/60}:profile=="120fps"?new[]{1f/120}:profile=="variable"?new[]{1f/60,1f/120,1f/30,.007f,.025f,0f,.05f,.004f}:new[]{.001f};
      for(int frame=1;frame<=4000;frame++)
      {
       float step=steps[(frame-1)%steps.Length];
       animator.Update(step);elapsed+=step;
       var current=animator.GetCurrentAnimatorStateInfo(0);
       bool transitioning=animator.IsInTransition(0);
       var next=animator.GetNextAnimatorStateInfo(0);
       if(frameSteps && observation.firstClipAt<0)
       {
        observation.entryFrames.Add(Capture(animator,frame,elapsed,step));
       }
       var currentAttack=animator.GetCurrentAnimatorClipInfo(0).FirstOrDefault(c=>c.clip && c.clip.name.StartsWith("attack_"));
       var nextAttack=transitioning?animator.GetNextAnimatorClipInfo(0).FirstOrDefault(c=>c.clip && c.clip.name.StartsWith("attack_")):default;
       var attack=currentAttack.clip?currentAttack:nextAttack;
       var state=currentAttack.clip?current:next;
       if(attack.clip)
       {
        if(attack.clip.name!="attack_0"+(index+1))throw new Exception("Unexpected attack "+attack.clip.name);
        if(observation.firstClipAt<0)
        {
         observation.firstClipAt=elapsed;observation.firstNormalizedTime=state.normalizedTime;
         observation.clip=attack.clip.name;observation.clipLength=attack.clip.length;
         var transition=animator.GetAnimatorTransitionInfo(0);
         observation.firstTransitionProgress=transition.normalizedTime;
         observation.transitionDuration=transition.duration;observation.transitionDurationUnit=transition.durationUnit.ToString();
        }
        if(observation.eventCrossAt<0 && state.normalizedTime*attack.clip.length>=eventTime)observation.eventCrossAt=elapsed;
        if(currentAttack.clip && !transitioning && observation.settledAt<0)observation.settledAt=elapsed;
        if(currentAttack.clip && transitioning && next.fullPathHash==Animator.StringToHash("base.idle") && observation.exitStartAt<0)
        {observation.exitStartAt=elapsed;observation.exitNormalizedTime=current.normalizedTime;
         var transition=animator.GetAnimatorTransitionInfo(0);observation.exitTransitionProgress=transition.normalizedTime;
         observation.exitBlendElapsed=transition.normalizedTime*transition.duration;
         observation.idleTimeAtExit=next.normalizedTime*next.length;}
       }
       if(observation.exitStartAt>=0 && !transitioning && !currentAttack.clip)
       {observation.idleSettledAt=elapsed;break;}
      }
      if(observation.firstClipAt<0 || observation.settledAt<0 || observation.eventCrossAt<0 || observation.exitStartAt<0 || observation.idleSettledAt<0)
       throw new Exception("Incomplete source transition sample "+JsonUtility.ToJson(observation));
      result.cases.Add(observation);
      Debug.Log($"DRAUG_ANIMATOR_TRANSITION profile={profile} entry={entry} phase={entryTime} index={index} first={observation.firstClipAt} normalized={observation.firstNormalizedTime} settle={observation.settledAt} eventCross={observation.eventCrossAt} exit={observation.exitStartAt} idle={observation.idleSettledAt}");
      Object.DestroyImmediate(model);model=null;
     }
    if(frameSteps)result.scope="Source Animator with verified idle parameter; 27 cases each at 30/60/120fps and variable steps including zero. Native callbacks absent, events disabled; inferred clip crossing only. Records observed fractional transition progress; no runtime parity assertion.";
    if(boundarySteps)result.scope="Source Animator entry boundaries around AnyState exitTime .05, 11 phases x idle/chase x4 step profiles; attack index0 only. Native callbacks absent; no runtime parity assertion.";
    if(localController)result.scope="Fresh process using only project-local controller bundle and Compact960 overrides, rebuilt Generic model;108 matched source timing samples. Native callbacks absent; not battle integration.";
    File.WriteAllText(localController?"Docs/Validation/DraugLocalControllerTransitionVerification.json":boundarySteps?"Docs/Validation/DraugAnyStateBoundaryVerification.json":frameSteps?"Docs/Validation/DraugFrameStepTransitionIdentityVerification.json":configured?"Docs/Validation/DraugConfiguredAnimatorTransitionVerification.json":"Docs/Validation/DraugAnimatorTransitionVerification.json",JsonUtility.ToJson(result,true));
    Debug.Log("DRAUG_ANIMATOR_TRANSITION_PASS cases="+result.cases.Count);exit=0;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{if(localTemplate)Object.DestroyImmediate(localTemplate);if(localCandidate)Object.DestroyImmediate(localCandidate);if(model)Object.DestroyImmediate(model);foreach(var bundle in bundles)if(bundle)bundle.Unload(true);}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
