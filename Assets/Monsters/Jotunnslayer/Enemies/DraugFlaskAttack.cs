using System.Collections.Generic;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
namespace Jotunnslayer
{
    internal sealed class DraugFlaskAttack : MonoBehaviour
    {
        internal const float FlightTime=1.3f, AreaDuration=3f, Radius=1.5f, TickPeriod=.25f;
        const float Delay=.05f, ArcHeight=5f;
        CharacterInstance owner,damageSource,target;
        bool reflectionChecked;
        int baseDamage,phase;
        float elapsed,nextTick;
        Vector3 origin,landing;
        GameObject warning,flight,muzzle;
        AudioSource currentAudio;
        bool audioPaused;
        readonly List<Health> victims=new List<Health>(16);
        internal bool InFlight=>enabled && phase<2;
        internal bool AreaActive=>enabled && phase==2;
        internal float FlightProgress=>phase==1?Mathf.Clamp01(elapsed/FlightTime):0f;
        internal Vector3 LandingPoint=>landing;
        internal int DamageEvents {get;private set;}
        internal bool Reflected {get;private set;}
        internal AudioSource CurrentAudio=>currentAudio;
        internal bool AudioTailActive=>enabled && phase==3;
        internal static Vector3 EvaluateFlightPosition(Vector3 start,Vector3 end,float progress)
        {
            float t=Mathf.Clamp01(progress);
            // Source ThrowToTarget/no-curve branch; see native instruction and emulation evidence.
            return Vector3.Lerp(start,end,t)+Vector3.up*(ArcHeight*Mathf.Sin(Mathf.PI*t));
        }
        internal static DraugFlaskAttack Launch(CharacterInstance caster,CharacterInstance target,int damage)
        {
            var root=new GameObject("Draug flask and splash");root.transform.SetParent(caster.transform,false);
            var attack=root.AddComponent<DraugFlaskAttack>();attack.owner=caster;attack.damageSource=caster;
            attack.target=target;attack.baseDamage=damage;
            attack.origin=caster.CombatAimPoint;attack.landing=target.transform.position;
            if(Physics.Raycast(attack.landing+Vector3.up*3,Vector3.down,out var hit,8,GameManager.environmentLayer,QueryTriggerInteraction.Ignore))attack.landing=hit.point;
            root.transform.position=attack.landing;
            attack.warning=attack.SpawnFx("warning",attack.landing);
            return attack;
        }
        GameObject SpawnFx(string kind,Vector3 point,Transform parent=null)
        {
            var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/OriginalFx/Draug_"+kind);
            if(!prefab){Debug.LogError("DRAUG_FX_MISSING "+kind);return null;}
            var effect=Instantiate(prefab,parent?parent:transform,false);effect.transform.position=point;
            foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))ps.Play(false);
            return effect;
        }
        void FixedUpdate()
        {
            if(!owner || !owner.isServer || owner.CurrentHealth<=0 || !damageSource || damageSource.CurrentHealth<=0){Cancel();return;}
            transform.position=landing;elapsed+=Time.fixedDeltaTime;
            if(phase==0)
            {
                if(elapsed<Delay)return;
                elapsed-=Delay;phase=1;
                flight=new GameObject("Draug source flask");flight.transform.SetParent(transform,false);flight.transform.position=origin;
                SpawnFx("projectile",origin,flight.transform);SpawnFx("flask",origin,flight.transform);
                muzzle=SpawnFx("muzzle",origin);
                PlaySourceAudio("Flight",flight);
                Debug.Log("DRAUG_FLASK_RELEASE");
            }
            if(phase==1)
            {
                float t=Mathf.Clamp01(elapsed/FlightTime);
                Vector3 point=EvaluateFlightPosition(origin,landing,t);
                if(ResolveFlightSegment(flight.transform.position,point))return;
                flight.transform.position=point;
                if(t<1f)return;
                BeginArea(landing);return;
            }
            if(phase==2)
            {
                while(nextTick<=Mathf.Min(elapsed,AreaDuration)+.0001f)
                {ApplyTick();nextTick+=TickPeriod;}
                if(elapsed>=AreaDuration && enabled)
                {
                    phase=3;
                    foreach(var ps in GetComponentsInChildren<ParticleSystem>())
                        ps.Stop(false,ParticleSystemStopBehavior.StopEmittingAndClear);
                    Debug.Log("DRAUG_SPLASH_END audioTail=True");
                }
            }
        }
        void Update()
        {
            if(!owner || !owner.isServer || owner.CurrentHealth<=0 || !damageSource || damageSource.CurrentHealth<=0)
            {Cancel();return;}
            if(currentAudio)
            {
                if(Time.timeScale<=0f)
                {
                    if(!audioPaused){currentAudio.Pause();audioPaused=true;}
                }
                else
                {
                    currentAudio.pitch=Mathf.Clamp(Time.timeScale,.01f,3f);
                    if(audioPaused){currentAudio.UnPause();audioPaused=false;}
                }
            }
            if(phase==3 && !audioPaused && (!currentAudio || !currentAudio.isPlaying))Cancel();
        }
        // Compare swept contacts before moving the visual. A trap consumes the entire attack.
        internal bool ResolveFlightSegment(Vector3 start,Vector3 end)
        {
            Vector3 delta=end-start;float distance=delta.magnitude;
            Vector3 direction=distance>.00001f?delta/distance:Vector3.forward;
            RaycastHit terrain=default;
            bool blocked=distance>.00001f && Physics.SphereCast(start,1f,direction,out terrain,distance,
                GameManager.environmentLayer,QueryTriggerInteraction.Ignore);
            float trap=SkillBattlePreview.TimeTrapProjectileContact(damageSource,start,direction,
                blocked?terrain.distance:distance,Time.time);
            if(!reflectionChecked && target && target.CurrentHealth>0 && target.Team!=damageSource.Team)
            {
                Vector3 offset=target.CombatAimPoint-start;
                float along=Vector3.Dot(offset,direction),lateral=(offset-direction*along).sqrMagnitude;
                if(lateral<=1f)
                {
                    float chord=Mathf.Sqrt(1f-lateral),contact=Mathf.Max(0,along-chord);
                    if(along+chord>=0 && contact<=distance && contact<trap && (!blocked || contact<terrain.distance))
                    {
                        // One reflection roll per flight, rather than one per overlapping physics tick.
                        reflectionChecked=true;
                        if(SkillBattlePreview.TryReflectRaidProjectile(target,damageSource))
                        {Reflect(start+direction*contact);return true;}
                    }
                }
            }
            if(!float.IsPositiveInfinity(trap))
            {Debug.Log("DRAUG_FLASK_TRAP_INTERCEPT");Cancel();return true;}
            if(!blocked)return false;
            // Surface impact is the local integration policy; native on-hit dispatch remains unverified.
            BeginArea(terrain.point);Debug.Log("DRAUG_FLASK_TERRAIN_IMPACT");return true;
        }
        void Reflect(Vector3 contact)
        {
            // Lifecycle remains owned by the caster; only damage/team attribution transfers.
            damageSource=target;target=owner;Reflected=true;
            origin=contact;landing=owner.transform.position;elapsed=0;phase=1;
            Vector3 muzzlePosition=muzzle?muzzle.transform.position:Vector3.zero;
            transform.position=landing;
            if(flight)flight.transform.position=contact;
            if(warning)warning.transform.position=landing;
            if(muzzle)muzzle.transform.position=muzzlePosition;
            Debug.Log("DRAUG_FLASK_REFLECT attribution=defender lifecycle=originalCaster");
        }
        void BeginArea(Vector3 point)
        {
            if(currentAudio)currentAudio.Stop();
            landing=point;transform.position=point;
            if(flight)Destroy(flight);if(warning)Destroy(warning);if(muzzle)Destroy(muzzle);
            phase=2;elapsed=0;nextTick=TickPeriod;SpawnFx("explosion",landing);
            PlaySourceAudio("Impact",gameObject);
            Debug.Log("DRAUG_SPLASH_BEGIN radius=1.5 duration=3 period=.25");
        }
        void PlaySourceAudio(string kind,GameObject emitter)
        {
            var clip=Resources.Load<AudioClip>("Jotunnslayer/DraugThrower/OriginalDraug"+kind);
            if(!clip){Debug.LogError("DRAUG_AUDIO_MISSING "+kind);return;}
            currentAudio=emitter.AddComponent<AudioSource>();currentAudio.playOnAwake=false;
            currentAudio.clip=clip;currentAudio.loop=false;currentAudio.spatialBlend=1f;
            currentAudio.volume=.7f;currentAudio.maxDistance=24f;
            audioPaused=false;currentAudio.pitch=Mathf.Clamp(Time.timeScale,.01f,3f);
            currentAudio.Play();
            Debug.Log("DRAUG_SOURCE_AUDIO "+clip.name);
        }
        void ApplyTick()
        {
            victims.Clear();
            // Snapshot before damage callbacks, which may remove dead actors from the registry.
            foreach(var pair in GameManager.HealthInstances)
            {
                var victim=pair.Value;
                if(victim && victim.CurrentHealth>0 && victim.Team!=damageSource.Team &&
                    (victim.transform.position-landing).sqrMagnitude<=Radius*Radius)victims.Add(victim);
            }
            foreach(var victim in victims)
            {
                if(!enabled || !owner || owner.CurrentHealth<=0 || !damageSource || damageSource.CurrentHealth<=0)break;
                if(!victim || victim.CurrentHealth<=0 || victim.Team==damageSource.Team)continue;
                int amount=Mathf.Max(1,Mathf.RoundToInt(baseDamage*Random.Range(.9f,1.1f)));
                victim.Server_ChangeHealthStateRaw(amount,CharacterPart.body,AttackType.hitscan,damageSource,0);DamageEvents++;
            }
            Debug.Log($"DRAUG_SPLASH_TICK victims={victims.Count} damageEvents={DamageEvents}");
        }
        internal void Cancel()
        {
            if(currentAudio)currentAudio.Stop();
            enabled=false;gameObject.SetActive(false);Destroy(gameObject);
        }
    }
}
