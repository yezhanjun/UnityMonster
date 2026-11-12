using UnityEngine;

// Completion conditions for the recovered death prefab (particles + wrecks,
// without Animation/TrailRenderer/VFXGraph/decal components). The owning FX
// starts its source removal delay after this returns true; this is not removal.
public sealed class DraugDeathFxCompletion
{
 readonly ParticleSystem[] particles;
 readonly bool[] loops;
 public DraugDeathFxCompletion(ParticleSystem[] particles)
 {
  this.particles=particles;loops=new bool[particles.Length];
  for(int i=0;i<particles.Length;i++)if(particles[i])loops[i]=particles[i].main.loop;
 }

 public bool IsFinished(float lifeTime,bool wreckSpawnerSpawned,int remainingWrecks,bool initialized=true,int state=0)
 {
  if(state!=0 || !initialized || (wreckSpawnerSpawned && remainingWrecks>0))return false;
  for(int i=0;i<particles.Length;i++)
  {
   var particle=particles[i];if(!particle || !particle.gameObject)continue;
   if(particle.particleCount>0 || loops[i] || particle.main.duration>lifeTime)return false;
  }
  return true;
 }
}
