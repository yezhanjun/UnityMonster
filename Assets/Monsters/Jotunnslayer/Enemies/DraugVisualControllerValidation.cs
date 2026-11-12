#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using Object=UnityEngine.Object;
namespace Jotunnslayer
{
 internal static class DraugVisualControllerValidation
 {
  [Serializable] sealed class Source { public Case[] cases; }
  [Serializable] sealed class Case { public string entry,stepProfile;public int index;public float entryNormalizedTime,firstClipAt,eventCrossAt,exitStartAt,idleSettledAt; }
  [Serializable] sealed class Result {public int cases,failedCases,cancellations;public float maxTimeError;public bool disableReleased,reacquired;public List<string> failures=new List<string>();}
  const string PlayKey="DraugVisualControllerValidation.Play";
  [InitializeOnLoadMethod] static void Resume(){if(SessionState.GetBool(PlayKey,false))EditorApplication.playModeStateChanged+=OnPlay;}
  public static void RunPlayMode()
  {
   SessionState.SetBool(PlayKey,true);EditorApplication.playModeStateChanged+=OnPlay;
   EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();
  }
  static void OnPlay(PlayModeStateChange state)
  {
   if(state!=PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(PlayKey,false))return;
   SessionState.EraseBool(PlayKey);EditorApplication.playModeStateChanged-=OnPlay;Run();
  }
  public static void Run()
  {
   const BindingFlags flags=BindingFlags.Instance|BindingFlags.NonPublic;
   GameObject model=null,anchor=null;DraugControllerResources.Lease held=null;int exit=1;var result=new Result();
   try
   {
    if(!Application.isPlaying)EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
    var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/draug_thrower_sur");
    anchor=Object.Instantiate(prefab);held=DraugControllerResources.Acquire(anchor.GetComponent<Animator>());
    model=Object.Instantiate(prefab);model.SetActive(true);var animator=model.GetComponent<Animator>();animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
    var visual=model.AddComponent<Skeleton1HVisual>();typeof(Skeleton1HVisual).GetMethod("Awake",flags).Invoke(visual,null);
    AnimationClip Clip(string name)=>Resources.Load<AnimationClip>("Jotunnslayer/DraugThrower/Compact960/"+name);
    visual.Bind(Clip("summon_01"),Clip("stand"),Clip("move"),Clip("skill_throw"),Clip("stand"),null);
    var source=JsonUtility.FromJson<Source>(File.ReadAllText("Docs/Validation/DraugFrameStepTransitionIdentityVerification.json"));
    var cases=new List<Case>(source.cases);
    cases.AddRange(JsonUtility.FromJson<Source>(File.ReadAllText("Docs/Validation/DraugAnyStateBoundaryVerification.json")).cases);
    foreach(var c in cases)
    {
     if(c.entry=="base.attack.decide")continue;
     visual.Play(Skeleton1HVisual.Motion.Hit,true);
     bool chase=c.entry=="base.chase";
     visual.Play(chase?Skeleton1HVisual.Motion.Walk:Skeleton1HVisual.Motion.Idle,true,startSeconds:c.entryNormalizedTime*Clip(chase?"move":"stand").length);
     var graph=(PlayableGraph)typeof(Skeleton1HVisual).GetField("graph",flags).GetValue(visual);graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);graph.Evaluate(0);
     if(!visual.RequestDraugArrow(c.index))throw new Exception("Arrow request rejected");
     if(visual.RequestDraugArrow(c.index))throw new Exception("Duplicate pending request accepted");
     float[] steps=c.stepProfile=="30fps"?new[]{1f/30}:c.stepProfile=="60fps"?new[]{1f/60}:c.stepProfile=="120fps"?new[]{1f/120}:new[]{1f/60,1f/120,1f/30,.007f,.025f,0f,.05f,.004f};
     float time=0,first=-1,release=-1,returnAt=-1,finish=-1;
     for(int frame=0;frame<600;frame++)
     {
      float delta=steps[frame%steps.Length];time+=delta;animator.Update(delta);visual.SampleDraugArrow();
      if(visual.DraugArrowStarted && first<0)first=time;
      if(visual.DraugArrowStarted && visual.CurrentClipTime>=DraugArrowTimeline.ClipEventSeconds(c.index) && release<0)release=time;
      if(visual.DraugArrowReturning && returnAt<0)returnAt=time;
      if(visual.DraugArrowFinished){finish=time;break;}
     }
     float error=Mathf.Max(Mathf.Abs(first-c.firstClipAt),Mathf.Abs(release-c.eventCrossAt),Mathf.Abs(returnAt-c.exitStartAt),Mathf.Abs(finish-c.idleSettledAt));
     result.maxTimeError=Mathf.Max(result.maxTimeError,error);result.cases++;
     if(first<0 || release<0 || returnAt<0 || finish<0 || error>.00001f)
     {result.failedCases++;result.failures.Add(c.entry+" "+c.stepProfile+" "+c.entryNormalizedTime+" "+c.index+" error="+error);}
    }
    for(int stage=0;stage<3;stage++)
    {
     visual.Play(Skeleton1HVisual.Motion.Idle,true);if(!visual.RequestDraugArrow(stage))throw new Exception("Cancellation request rejected");
     if(stage>0)
      for(int frame=0;frame<200;frame++){animator.Update(1f/60);visual.SampleDraugArrow();if(stage==1?visual.DraugArrowStarted:visual.DraugArrowReturning)break;}
     if(stage==1 && !visual.DraugArrowStarted || stage==2 && !visual.DraugArrowReturning)throw new Exception("Cancellation stage not reached");
     visual.Play(Skeleton1HVisual.Motion.Idle,true);
     for(int frame=0;frame<90;frame++){animator.Update(1f/60);visual.SampleDraugArrow();if(visual.DraugArrowStarted || !animator.GetCurrentAnimatorStateInfo(0).IsName("base.idle") || animator.IsInTransition(0))throw new Exception("Cancelled controller resumed stale attack");}
     result.cancellations++;
    }
    visual.enabled=false;result.disableReleased=!visual.DraugControllerActive && DraugControllerResources.OwnerCount==1;
    visual.enabled=true;visual.Play(Skeleton1HVisual.Motion.Idle,true);result.reacquired=visual.RequestDraugArrow(0) && DraugControllerResources.OwnerCount==2;
    visual.enabled=false;
    File.WriteAllText("Docs/Validation/DraugVisualControllerVerification.json",JsonUtility.ToJson(result,true));
    if(result.cases!=184 || result.failedCases!=0 || result.cancellations!=3 || !result.disableReleased || !result.reacquired)throw new Exception("Visual controller gate failed");
    Debug.Log($"DRAUG_VISUAL_CONTROLLER_PASS cases={result.cases} maxTimeError={result.maxTimeError} cancellations=3 disableReenable=True");exit=0;
   }
   catch(Exception e){Debug.LogException(e);}
   finally{if(model)Object.DestroyImmediate(model);held?.Dispose();if(anchor)Object.DestroyImmediate(anchor);}
   if(DraugControllerResources.OwnerCount!=0 || DraugControllerResources.IsLoaded){Debug.LogError("Visual lease leak");exit=1;}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
