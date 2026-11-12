using System;
using UnityEngine;

// Post-activation Wreck.GFUpdate state for this source configuration. The caller
// supplies scaled delta and owns activation, global registration and removal.
public sealed class DraugWreckRetirement
{
 readonly Transform root;
 readonly Rigidbody[] bodies;
 readonly Collider[] colliders;
 readonly float spawnY;
 bool componentsDeactivated;
 float inactiveTimer;
 public float ActiveTime {get;private set;}
 public float StationaryTime {get;private set;}
 public float InactiveTime=>inactiveTimer;
 public bool Finished {get;private set;}
 public DraugWreckRetirement(Transform root,Rigidbody[] bodies,Collider[] colliders)
 {
  if(!root || bodies==null || colliders==null)throw new ArgumentException("Wreck root/bodies/colliders required");
  this.root=root;this.bodies=bodies;this.colliders=colliders;spawnY=root.position.y;
 }
 static float Squared(Vector3 v)
 {
  float x=(float)(v.x*v.x),y=(float)(v.y*v.y),z=(float)(v.z*v.z);
  return (float)((float)(y+x)+z);
 }
 public static bool IsStationary(Vector3 linear,Vector3 angular)=>!(Squared(linear)>.09f || Squared(angular)>1);
 bool Stationary()
 {
  foreach(var body in bodies)if(body && body.gameObject.activeInHierarchy && !IsStationary(body.linearVelocity,body.angularVelocity))return false;
  return true;
 }
 bool AllFallen()
 {
  foreach(var body in bodies)if(body && body.gameObject.activeInHierarchy && (float)(body.position.y-spawnY)>-6)return false;
  return true;
 }
 void Freeze()
 {
  foreach(var body in bodies)if(body)body.isKinematic=true;
  Finished=true;
 }
 public bool Tick(float scaledDeltaTime)
 {
  ActiveTime=(float)(ActiveTime+scaledDeltaTime);
  if(Finished)
  {
   if(!componentsDeactivated)
   {
    Freeze();foreach(var collider in colliders)if(collider)collider.enabled=false;
    componentsDeactivated=true;
   }
   var action=DraugWreckTiming.AdvanceFinished(ref inactiveTimer,scaledDeltaTime,.5f,.4f);
   if(action==DraugWreckTiming.FinishedAction.Remove)return true;
   if(action==DraugWreckTiming.FinishedAction.Sink && root)
   {
    var position=root.position;position.y+=DraugWreckTiming.SinkDisplacement(scaledDeltaTime,2);root.position=position;
   }
   return false;
  }
  if(ActiveTime>1.5f)
  {
   StationaryTime=0;
   float multiplier=DraugWreckTiming.StopMultiplier(ActiveTime,1.5f,.1f);
   if(multiplier<=0){Freeze();return false;}
   if(!Stationary())foreach(var body in bodies)if(body && body.gameObject.activeInHierarchy)
   {
    body.linearVelocity*=multiplier;body.angularVelocity*=multiplier;
   }
  }
  else if(Stationary() || AllFallen())
  {
   StationaryTime=(float)(StationaryTime+scaledDeltaTime);
   if(StationaryTime>=1)Freeze();
  }
  else StationaryTime=0;
  return false;
 }
}
