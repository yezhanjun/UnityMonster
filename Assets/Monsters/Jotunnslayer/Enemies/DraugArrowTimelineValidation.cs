#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Jotunnslayer
{
 internal static class DraugArrowTimelineValidation
 {
  [Serializable] sealed class Cases { public Case[] cases; }
  [Serializable] sealed class Case
  {
   public string entry,transitionDurationUnit;
   public int index;
   public float firstNormalizedTime,transitionDuration,eventCrossAt,exitStartAt,idleSettledAt,clipLength,attackSpeed;
  }
  public static void Run()
  {
   int exit=1;
   try
   {
    var measurements=JsonUtility.FromJson<Cases>(File.ReadAllText("Docs/Validation/DraugAnimatorTransitionVerification.json"));
    if(measurements.cases.Length!=27)throw new Exception("Expected 27 independent source Animator measurements");
    float maximum=0;
    foreach(var sample in measurements.cases)
    {
     var timeline=new DraugArrowTimeline(sample.index,sample.entry=="base.chase",sample.clipLength);
     if(Mathf.Abs(sample.firstNormalizedTime-DraugArrowTimeline.EntryNormalizedTime(sample.index))>.000001f ||
       sample.transitionDurationUnit!="Fixed" || Mathf.Abs(sample.transitionDuration-DraugArrowTimeline.AttackBlendSeconds)>.000001f ||
       sample.attackSpeed!=1)throw new Exception("Source transition metadata mismatch");
     float error=Mathf.Max(Mathf.Abs(sample.eventCrossAt-timeline.ReleaseDelay),Mathf.Abs(sample.exitStartAt-timeline.ReturnDelay),Mathf.Abs(sample.idleSettledAt-timeline.FinishDelay));
     // Source transitions are observed in 1ms ticks across up to three state evaluations.
     // Keep that observation tolerance out of runtime timing constants.
     if(error>.004f)throw new Exception($"Source timeline mismatch {sample.entry}/{sample.index}: {error}");
     maximum=Mathf.Max(maximum,error);
    }
    Debug.Log($"DRAUG_ARROW_TIMELINE_PASS sourceCases=27 maxObservationDifference={maximum} runtimeIntegration=False");
    exit=0;
   }
   catch(Exception error){Debug.LogException(error);}
   EditorApplication.Exit(exit);
  }
 }
}
#endif
