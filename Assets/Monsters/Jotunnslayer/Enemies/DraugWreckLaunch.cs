using UnityEngine;

// Source ActivateImpl motion with explicit already-drawn inputs. This does not
// own source RNG consumption, optimization selection or bone alignment.
public static class DraugWreckLaunch
{
 // Source applies one shared additive offset after all body velocity draws.
 // This intentionally preserves nonuniform scales and does not clamp axes.
 public static void ApplyScaleOffset(Transform root,float offset)
 {
  var scale=root.localScale;
  root.localScale=new Vector3(scale.x+offset,scale.y+offset,scale.z+offset);
 }

 public static void RandomizeScale(Transform root)
 {
  ApplyScaleOffset(root,Random.Range(-.05f,.05f));
 }

 // Source RandomFloat.Randomize calls Unity's shared float RNG. Draw order is
 // angle X/Y (only without a center), speed, angular X/Y/Z. The separate
 // optimization/start-index and root scale draws belong to the caller.
 public static void ApplyRandomized(Rigidbody body,Vector3? explosionCenter,float velocityScale)
 {
  if(!body || !body.gameObject.activeInHierarchy)return;
  float angleX=0,angleY=0;
  if(!explosionCenter.HasValue){angleX=Random.Range(-8f,8f);angleY=Random.Range(-8f,8f);}
  float speed=Random.Range(3f,6f);
  var angular=new Vector3(Random.Range(-2f,2f),Random.Range(-2f,2f),Random.Range(-2f,2f));
  Apply(body,explosionCenter,angleX,angleY,speed,angular,velocityScale);
 }

 public static void Apply(Rigidbody body,Vector3? explosionCenter,float angleX,float angleY,float speed,Vector3 angular,float velocityScale)
 {
  if(!body || !body.gameObject.activeInHierarchy)return;
  body.isKinematic=false;body.WakeUp();
  Vector3 direction=explosionCenter.HasValue?(body.transform.position-explosionCenter.Value).normalized:Quaternion.Euler(angleX,angleY,0)*Vector3.up;
  body.linearVelocity=(direction*speed)*velocityScale;
  body.angularVelocity=angular;
 }
}
