using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugDeathFxCompletionValidation
{
 [Serializable] class Row {public int state,remainingWrecks,particleCount;public bool initialized,spawned,loop,finished;public float lifeTime,duration;}
 [Serializable] class Corpus {public Row[] cases;}
 public static void Run()
 {
  var data=JsonUtility.FromJson<Corpus>(File.ReadAllText("Docs/Validation/DraugFxFinishedVerification.json"));
  var go=new GameObject("Completion corpus particle");var ps=go.AddComponent<ParticleSystem>();GameObject actual=null;
  try
  {
   foreach(var row in data.cases)
   {
    ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
    var main=ps.main;main.duration=row.duration;main.loop=row.loop;main.startLifetime=10;main.playOnAwake=false;
    if(row.particleCount>0)ps.Emit(row.particleCount);
    if(ps.particleCount!=row.particleCount)throw new Exception("Fixture particle count mismatch");
    var completion=new DraugDeathFxCompletion(new[]{ps});
    if(completion.IsFinished(row.lifeTime,row.spawned,row.remainingWrecks,row.initialized,row.state)!=row.finished)throw new Exception("Completion differs from native");
   }
   ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
   var inactiveMain=ps.main;inactiveMain.loop=false;inactiveMain.duration=1;
   go.SetActive(false);var inactive=new DraugDeathFxCompletion(new[]{ps});
   if(inactive.IsFinished(0,false,0) || !inactive.IsFinished(1,false,0))throw new Exception("Inactive particle must still gate on duration");
   actual=UnityEngine.Object.Instantiate(Resources.Load<GameObject>("Jotunnslayer/DraugThrower/DeathFx/DraugDeathDecodedStage"));
   var systems=actual.GetComponentsInChildren<ParticleSystem>(true);
   if(systems.Length!=15 || actual.GetComponentsInChildren<Animation>(true).Length!=0 || actual.GetComponentsInChildren<TrailRenderer>(true).Length!=0)throw new Exception("Death prefab completion profile changed");
   float maximum=0;
   foreach(var system in systems){system.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);if(system.main.loop)throw new Exception("Unexpected looping source particle");maximum=Mathf.Max(maximum,system.main.duration);}
   var actualCompletion=new DraugDeathFxCompletion(systems);
   if(actualCompletion.IsFinished(0,false,0) || !actualCompletion.IsFinished(maximum,false,0) || actualCompletion.IsFinished(maximum,true,1))throw new Exception("Actual death prefab duration/wreck gate mismatch");
   File.WriteAllText("Docs/Validation/DraugDeathFxCompletionUnityVerification.json","{\"status\":\"PASS_UNITY_NATIVE_COMPLETION_AND_PREFAB_PROFILE\",\"nativeCases\":"+data.cases.Length+",\"inactiveCases\":2,\"prefabParticles\":15,\"maxDuration\":"+maximum.ToString(System.Globalization.CultureInfo.InvariantCulture)+",\"limits\":\"Completion predicate only; source removal coroutine, production lifecycle and lethal integration remain pending.\"}");
   Debug.Log("DRAUG_DEATH_FX_COMPLETION_PASS native="+data.cases.Length+" inactive=2 particles=15 maxDuration="+maximum);
  }
  finally{UnityEngine.Object.DestroyImmediate(go);if(actual)UnityEngine.Object.DestroyImmediate(actual);}
  EditorApplication.Exit(0);
 }
}
