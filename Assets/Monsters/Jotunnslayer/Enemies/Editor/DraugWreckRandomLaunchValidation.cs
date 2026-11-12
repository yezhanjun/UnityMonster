using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random=UnityEngine.Random;

public static class DraugWreckRandomLaunchValidation
{
 public static void Run()
 {
  var original=Random.state;
  var go=new GameObject("Wreck random launch validation");var body=go.AddComponent<Rigidbody>();
  int cases=0;
  try
  {
   foreach(int seed in new[]{0,1,17,int.MaxValue})
   for(int branch=0;branch<2;branch++)
   {
    Vector3? center=branch==0?(Vector3?)null:new Vector3(-2,-1,3);
    Random.InitState(seed);
    float x=0,y=0;
    if(branch==0){x=Random.Range(-8f,8f);y=Random.Range(-8f,8f);}
    float speed=Random.Range(3f,6f);
    var angular=new Vector3(Random.Range(-2f,2f),Random.Range(-2f,2f),Random.Range(-2f,2f));
    var direction=center.HasValue?(body.position-center.Value).normalized:Quaternion.Euler(x,y,0)*Vector3.up;
    var expected=(direction*speed)*.52f;string expectedState=JsonUtility.ToJson(Random.state);
    Random.InitState(seed);body.isKinematic=true;
    DraugWreckLaunch.ApplyRandomized(body,center,.52f);
    if(JsonUtility.ToJson(Random.state)!=expectedState || body.isKinematic || (body.linearVelocity-expected).sqrMagnitude>1e-12f || (body.angularVelocity-angular).sqrMagnitude>1e-12f)
     throw new Exception("Random launch branch/order mismatch seed="+seed+" branch="+branch);
    cases++;
   }
   go.SetActive(false);string before=JsonUtility.ToJson(Random.state);
   DraugWreckLaunch.ApplyRandomized(body,null,.52f);
   if(JsonUtility.ToJson(Random.state)!=before)throw new Exception("Inactive body consumed RNG");cases++;
   DraugWreckLaunch.ApplyRandomized(null,null,.52f);
   if(JsonUtility.ToJson(Random.state)!=before)throw new Exception("Null body consumed RNG");cases++;
   File.WriteAllText("Docs/Validation/DraugWreckRandomLaunchVerification.json","{\"status\":\"PASS_UNITY_SHARED_RNG_LAUNCH\",\"cases\":"+cases+",\"limits\":\"Local Unity RNG branch/order/state and velocities only; original engine random sequence, source RandomFloat cached fields, scale draw, selection/global registration and lethal integration remain pending.\"}");
   Debug.Log("DRAUG_WRECK_RANDOM_LAUNCH_PASS cases="+cases);
  }
  finally{Random.state=original;UnityEngine.Object.DestroyImmediate(go);}
  EditorApplication.Exit(0);
 }
}
