using UnityEngine;
namespace Jotunnslayer
{
    internal sealed class DraugHandVisual : MonoBehaviour
    {
        Skeleton1HVisual visual;
        ParticleSystem[] particles;
        bool attacking;
        float started;
        void Start()
        {
            visual=GetComponent<Skeleton1HVisual>();
            var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/HandFx/DraugOriginalHandFx");
            if(!prefab)return;
            Transform anchor=null;
            foreach(var t in GetComponentsInChildren<Transform>(true)) if(t.name=="fx_draug_thrower_hand") {anchor=t;break;}
            if(!anchor)return;
            var effect=Instantiate(prefab,anchor,false);
            particles=effect.GetComponentsInChildren<ParticleSystem>(true);
        }
        void LateUpdate()
        {
            bool now=visual && visual.CurrentMotion==Skeleton1HVisual.Motion.Attack;
            if(now && !attacking) {started=Time.time; if(particles!=null)foreach(var p in particles)p.Play(false);}
            if(!now || Time.time-started>=.4920549f) Clear();
            attacking=now;
        }
        void Clear() {if(particles!=null)foreach(var p in particles)if(p.isPlaying)p.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);}
        void OnDisable() {Clear();attacking=false;}
    }
}
