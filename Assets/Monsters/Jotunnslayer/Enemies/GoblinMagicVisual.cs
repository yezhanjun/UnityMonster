using UnityEngine;
namespace Jotunnslayer
{
    // Source particle hierarchy with approximate URP materials; combat owns all playback.
    internal sealed class GoblinMagicVisual : MonoBehaviour
    {
        sealed class Effect
        {
            internal Transform root;
            internal ParticleSystem[] particles;
            internal Vector3 point;
            internal float expires;
            internal bool world;
            internal Effect(GameObject prefab, Transform parent)
            {
                if(!prefab)return;
                root=Instantiate(prefab,parent,false).transform;
                particles=root.GetComponentsInChildren<ParticleSystem>(true);
                Clear();
            }
            internal void Play(Vector3 position, bool fixedWorld, bool oneShot)
            {
                if(!root)return;
                Clear();world=fixedWorld;point=position;
                if(world)root.position=point;
                float lifetime=0;
                foreach(var ps in particles)
                {
                    var main=ps.main;
                    if(oneShot)main.loop=false;
                    lifetime=Mathf.Max(lifetime,main.startDelay.constantMax+main.duration+main.startLifetime.constantMax);
                    ps.Play(false);
                }
                expires=oneShot?Time.time+lifetime:float.PositiveInfinity;
            }
            internal void Tick()
            {
                if(!root || expires==0)return;
                if(world)root.position=point;
                if(Time.time>=expires)Clear();
            }
            internal void Clear()
            {
                if(particles!=null)foreach(var ps in particles)if(ps)ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                expires=0;
            }
        }
        Effect cast,muzzle,hit;
        GameObject projectile,banish;
        Transform center;
        internal Vector3 ReturnPoint => center?center.position:transform.position+Vector3.up*.676f;
        void Awake()
        {
            Transform hand=transform;
            foreach(var t in GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="L_fingerEnd")hand=t;
                if(t.name=="bodyCenter")center=t;
            }
            cast=new Effect(Load("cast"),hand);
            muzzle=new Effect(Load("muzzle"),transform);
            hit=new Effect(Load("hit"),transform);
            projectile=Load("projectile");banish=Load("banish");
        }
        static GameObject Load(string kind)=>Resources.Load<GameObject>("Jotunnslayer/Goblin/OriginalFx/Goblin_"+kind);
        internal void BeginCast()=>cast.Play(Vector3.zero,false,false);
        internal void Release(Transform flight)
        {
            cast.Clear();muzzle.Play(flight.position,true,true);
            Attach(projectile,flight);Attach(banish,flight);
        }
        static void Attach(GameObject prefab,Transform flight)
        {
            if(!prefab)return;
            var effect=Instantiate(prefab,flight,false);
            foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))ps.Play(false);
        }
        internal void Impact(Vector3 position)=>hit.Play(position,true,true);
        internal void Clear(){cast?.Clear();muzzle?.Clear();hit?.Clear();}
        void LateUpdate(){cast?.Tick();muzzle?.Tick();hit?.Tick();}
        void OnDisable()=>Clear();
    }
}
