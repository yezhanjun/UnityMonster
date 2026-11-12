using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugWreckTimingValidation
{
 [Serializable] class TimerCase {public float timer,delta,duration,fadeDuration,nextTimer;public string outcome;}
 [Serializable] class DampingCase {public float activeTime,stopTime,stopDuration,multiplier;public bool finish;}
 [Serializable] class Evidence {public TimerCase[] timerCases;public DampingCase[] dampingCases;}
 public static void Run()
 {
  var evidence=JsonUtility.FromJson<Evidence>(File.ReadAllText("Docs/Validation/DraugWreckTimingVerification.json"));
  foreach(var row in evidence.timerCases)
  {
   float timer=row.timer;
   var outcome=DraugWreckTiming.AdvanceFinished(ref timer,row.delta,row.duration,row.fadeDuration);
   if(timer!=row.nextTimer || !string.Equals(outcome.ToString(),row.outcome,StringComparison.OrdinalIgnoreCase))throw new Exception("Source wreck finished timing mismatch");
  }
  foreach(var row in evidence.dampingCases)
  {
   float multiplier=DraugWreckTiming.StopMultiplier(row.activeTime,row.stopTime,row.stopDuration);
   if(multiplier!=row.multiplier || (multiplier<=0)!=row.finish)throw new Exception($"Source wreck damping mismatch active={row.activeTime:R} stop={row.stopTime:R} duration={row.stopDuration:R} actual={multiplier:R} expected={row.multiplier:R} finish={row.finish}");
  }
  if(DraugWreckTiming.StopMultiplier(1.5f,1.5f,.1f)!=1 || DraugWreckTiming.SinkDisplacement(0,2)!=0)throw new Exception("Boundary/pause mismatch");
  File.WriteAllText("Docs/Validation/DraugWreckTimingUnityVerification.json",$"{{\"status\":\"PASS_CSHARP_NATIVE_TIMING_CORPUS\",\"timerCases\":{evidence.timerCases.Length},\"dampingCases\":{evidence.dampingCases.Length},\"limits\":\"Arithmetic only, not full native lifecycle, Unity physics or real death integration.\"}}");
  Debug.Log($"DRAUG_WRECK_TIMING_PASS timer={evidence.timerCases.Length} damping={evidence.dampingCases.Length}");EditorApplication.Exit(0);
 }
}
