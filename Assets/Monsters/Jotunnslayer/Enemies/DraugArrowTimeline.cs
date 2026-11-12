using System;

namespace Jotunnslayer
{
 // Source controller timing, independent of Unity frame-quantized transition evaluation.
 // Integration must preserve chase->stand(decide)->attack, not just change a release delay.
 internal readonly struct DraugArrowTimeline
 {
  internal const float AttackBlendSeconds=.05f;
  internal const float DecideSpeed=.5f;
  internal const float ReturnBlendSeconds=.1f;
  internal const float ChaseToDecideSeconds=.1590909958f;
  internal const float ExitNormalizedTime=.9499999881f;
  internal readonly float EntryDelay,ClipStartSeconds,ReleaseDelay,ReturnDelay,FinishDelay;
  internal static float EntryNormalizedTime(int index) => index switch
  {
   0=>.007662696298f,1=>.02923251316f,2=>.05747386441f,
   _=>throw new ArgumentOutOfRangeException(nameof(index))
  };
  internal static float ClipEventSeconds(int index) => index switch
  {
   0=>.5131691694f,1=>.5551713705f,2=>.4536709189f,
   _=>throw new ArgumentOutOfRangeException(nameof(index))
  };
  internal DraugArrowTimeline(int index,bool fromChase,float clipLength)
  {
   if(float.IsNaN(clipLength) || float.IsInfinity(clipLength) || clipLength<=0)
    throw new ArgumentOutOfRangeException(nameof(clipLength));
   EntryDelay=fromChase?ChaseToDecideSeconds:0;
   ClipStartSeconds=EntryNormalizedTime(index)*clipLength;
   ReleaseDelay=EntryDelay+ClipEventSeconds(index)-ClipStartSeconds;
   ReturnDelay=EntryDelay+ExitNormalizedTime*clipLength-ClipStartSeconds;
   FinishDelay=ReturnDelay+ReturnBlendSeconds;
   if(ReleaseDelay<EntryDelay || ReturnDelay<ReleaseDelay)
    throw new ArgumentException("Clip length does not preserve the source attack event ordering",nameof(clipLength));
  }
 }
}
