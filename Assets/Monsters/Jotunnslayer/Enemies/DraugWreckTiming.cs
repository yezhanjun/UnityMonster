using UnityEngine;

// Source Wreck.GFUpdate arithmetic. Entry into finished state and actual
// Rigidbody/Transform mutation remain owned by the surrounding wreck lifecycle.
public static class DraugWreckTiming
{
 public enum FinishedAction { Keep, Sink, Remove }
 public static float StopMultiplier(float activeTime,float stopTime,float stopDuration)
 {
  if(activeTime<=stopTime)return 1;
  // Match the native SUBSS/DIVSS/SUBSS rounding boundaries on Mono as well.
  float elapsed=(float)(activeTime-stopTime);
  float fraction=(float)(elapsed/Mathf.Max(.1f,stopDuration));
  return (float)(1-fraction);
 }
 public static FinishedAction AdvanceFinished(ref float inactiveTimer,float scaledDeltaTime,float duration,float fadeDuration)
 {
  inactiveTimer+=scaledDeltaTime;
  if(inactiveTimer>=duration)return FinishedAction.Remove;
  if(inactiveTimer>=duration-fadeDuration && fadeDuration>.001f)return FinishedAction.Sink;
  return FinishedAction.Keep;
 }
 public static float SinkDisplacement(float scaledDeltaTime,float fadeSpeed)=>-scaledDeltaTime*fadeSpeed;
}
