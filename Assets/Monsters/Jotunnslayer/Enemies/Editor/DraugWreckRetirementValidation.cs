using System;
using System.IO;
using UnityEditor;
using UnityEngine;

public static class DraugWreckRetirementValidation
{
 [Serializable] class Row {public Vector3 linear,angular;public bool active,stationary;}
 [Serializable] class Evidence {public Row[] cases;}
 public static void Run()
 {
  var evidence=JsonUtility.FromJson<Evidence>(File.ReadAllText("Docs/Validation/DraugWreckStationaryVerification.json"));int compared=0;
  foreach(var row in evidence.cases)
  {
   if((!row.active || DraugWreckRetirement.IsStationary(row.linear,row.angular))!=row.stationary)throw new Exception("Native stationary mismatch");compared++;
  }
  int scenarios=0;
  foreach(var scenario in new[]{"stationary","moving","fallen","inactive","reset"})
  {
   var root=new GameObject("Wreck retirement validation");
   try
   {
    var child=new GameObject("body");child.transform.SetParent(root.transform);
    var body=child.AddComponent<Rigidbody>();body.useGravity=false;var collider=child.AddComponent<BoxCollider>();
    var retirement=new DraugWreckRetirement(root.transform,new[]{body},new Collider[]{collider});
    if(scenario=="moving" || scenario=="fallen" || scenario=="reset")body.linearVelocity=Vector3.right;
    if(scenario=="fallen")body.position=new Vector3(0,-6,0);
    if(scenario=="inactive")child.SetActive(false);
    retirement.Tick(0);if(retirement.ActiveTime!=0 || retirement.InactiveTime!=0)throw new Exception("Pause advanced timers");
    if(scenario=="reset")
    {
     body.linearVelocity=Vector3.zero;retirement.Tick(.4f);body.linearVelocity=Vector3.right;retirement.Tick(.1f);
     if(retirement.StationaryTime!=0)throw new Exception("Moving body failed to reset stationary timer");
    }
    if(scenario=="moving" || scenario=="reset")
    {
     while(retirement.ActiveTime<1.5f)retirement.Tick(.1f);
     retirement.Tick(.05f);
     if(body.linearVelocity.x>=1 || retirement.Finished)throw new Exception("Stop damping failed");
     retirement.Tick(.1f);
    }
    else retirement.Tick(1);
    if(!retirement.Finished || !body.isKinematic)throw new Exception("Wreck failed to freeze");
    if(retirement.Tick(.1f) || collider.enabled || Mathf.Abs(root.transform.position.y+.2f)>.00001f)throw new Exception("Finished collider/sink stage failed");
    if(!retirement.Tick(.4f))throw new Exception("Finished duration did not request removal");
    scenarios++;
   }
   finally{UnityEngine.Object.DestroyImmediate(root);}
  }
  File.WriteAllText("Docs/Validation/DraugWreckRetirementVerification.json",$"{{\"status\":\"PASS_STATIONARY_CORPUS_AND_UNITY_RETIREMENT\",\"nativeCases\":{compared},\"scenarios\":{scenarios},\"limits\":\"Controlled Unity Rigidbody properties, not physics trajectory or complete native activation. No source global registration/optimized spawning/audio or production death integration.\"}}");
  Debug.Log($"DRAUG_WRECK_RETIREMENT_PASS native={compared} scenarios={scenarios}");EditorApplication.Exit(0);
 }
}
