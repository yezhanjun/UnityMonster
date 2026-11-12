using System;
using UnityEngine;

// Source FX.Spawn particle selection only. The caller owns same-group counting,
// registration, removal, wreck spawning and the surrounding FX lifecycle.
public sealed class DraugDeathParticleLod
{
 public const int Suppressed=-2;
 readonly ParticleSystem[] particles;
 readonly byte[] membership;
 public DraugDeathParticleLod(GameObject root,GameObject lod0,GameObject lod1,GameObject lod2)
 {
  if(!root || !lod0 || !lod1 || !lod2)throw new ArgumentException("Death LOD roots required");
  particles=root.GetComponentsInChildren<ParticleSystem>(true);
  membership=new byte[particles.Length];
  for(int i=0;i<particles.Length;i++)
  {
   var t=particles[i].transform;var parent=t.parent?t.parent.gameObject:null;
   // Source Awake compares self and immediate parent, not all ancestors.
   if(t.gameObject==lod0 || parent==lod0)membership[i]|=1;
   if(t.gameObject==lod1 || parent==lod1)membership[i]|=2;
   if(t.gameObject==lod2 || parent==lod2)membership[i]|=4;
  }
 }
 public static int Select(int sameGroupCount)
 {
  if(sameGroupCount>100)return Suppressed;
  if(sameGroupCount>=60)return 2;
  if(sameGroupCount>=25)return 1;
  return sameGroupCount>=0?0:-1;
 }
 public static float WreckPercent(int selected)=>selected==2?0:selected==1?50:100;
 public int Begin(int sameGroupCount)
 {
  int selected=Select(sameGroupCount);
  if(selected==Suppressed)return selected; // Source returns before mutating particles.
  for(int i=0;i<particles.Length;i++)
  {
   var particle=particles[i];if(!particle)continue;
   particle.Clear(true);
   if(selected>=0 && membership[i]!=0 && (membership[i]&(1<<selected))==0)
   {
    particle.Stop();continue;
   }
   var main=particle.main;main.simulationSpeed=1;particle.time=0;
   particle.Clear(true);particle.Play(true);
  }
  return selected;
 }
}
