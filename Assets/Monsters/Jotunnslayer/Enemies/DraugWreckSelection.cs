using System;

// Source optimized Wreck activation loop. The caller owns the shared counter
// and provides the already-selected start offset and per-body random samples.
public static class DraugWreckSelection
{
 public static int StartIndex(int bodyCount,float sample)
 {
  if(bodyCount<1)throw new ArgumentOutOfRangeException(nameof(bodyCount));
  return UnityEngine.Mathf.RoundToInt((float)((bodyCount-1)*sample));
 }
 public static bool CanSpawn(float percent)=>percent>0;
 public static void Select(float percent,int startOffset,float[] samples,ref int activationCounter,bool[] keep)
 {
  if(samples==null || keep==null || samples.Length!=keep.Length || keep.Length==0)throw new ArgumentException("Matching nonempty body buffers required");
  if(startOffset<0 || startOffset>=keep.Length)throw new ArgumentOutOfRangeException(nameof(startOffset));
  for(int i=0;i<keep.Length;i++)keep[i]=CanSpawn(percent);
  if(!CanSpawn(percent))return;
  float ratio=(float)(percent/100f);
  float period=(float)(1f/(float)(1f-ratio));
  if(period<=1){activationCounter=unchecked(activationCounter+keep.Length);return;}
  float phase=(float)((float)activationCounter%period)-1f;
  for(int i=0;i<keep.Length;i++)
  {
   activationCounter=unchecked(activationCounter+1);
   float threshold=(float)((float)activationCounter-phase)%period;
   if(samples[i]>=threshold)keep[(keep.Length-1+startOffset-i)%keep.Length]=false;
  }
 }
}
