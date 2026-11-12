using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class DraugArrowAttack : MonoBehaviour
    {
        CharacterInstance owner;
        SkeletonBowArrow flight;
        AudioSource audioSource;
        ParticleSystem[] tailParticles;
        bool ended, paused;
        internal bool InFlight => !ended && flight && flight.enabled;
        internal SkeletonBowArrow Flight => flight;

        internal static DraugArrowAttack Launch(CharacterInstance caster, CharacterInstance target, Vector3 direction, int damage)
        {
            var root = new GameObject("Draug source arrow effects");
            var attack = root.AddComponent<DraugArrowAttack>();attack.owner = caster;
            root.transform.position = caster.CombatAimPoint;
            attack.flight = SkeletonBowArrow.Launch(caster,target,direction,damage);
            attack.flight.ConfigureDraug(attack);
            attack.Spawn("muzzle",caster.CombatAimPoint,root.transform);
            attack.Spawn("projectile",caster.CombatAimPoint,attack.flight.transform);
            attack.audioSource=attack.flight.gameObject.AddComponent<AudioSource>();
            attack.audioSource.clip=Resources.Load<AudioClip>("Jotunnslayer/DraugThrower/OriginalDraugArrowFlight");
            attack.audioSource.playOnAwake=false;attack.audioSource.spatialBlend=1;
            attack.audioSource.minDistance=1;attack.audioSource.maxDistance=20;attack.audioSource.volume=.7f;
            attack.audioSource.Play();
            return attack;
        }

        void Spawn(string kind,Vector3 position,Transform parent)
        {
            var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/BaseArrowFx/DraugArrow_"+kind);
            if(!prefab){Debug.LogError("DRAUG_ARROW_FX_MISSING "+kind);return;}
            var effect=Instantiate(prefab,parent,false);effect.transform.position=position;
            foreach(var ps in effect.GetComponentsInChildren<ParticleSystem>(true))ps.Play(false);
        }

        internal void FlightEnded(Vector3 position,bool hit,bool cancelled)
        {
            if(ended)return;
            ended=true;flight=null;
            if(audioSource)audioSource.Stop();
            if(cancelled){enabled=false;Destroy(gameObject);return;}
            Spawn(hit?"hit":"end",position,transform);
            tailParticles=GetComponentsInChildren<ParticleSystem>(true);
        }

        internal void Cancel()
        {
            if(flight)flight.CancelFlight();
            if(audioSource)audioSource.Stop();
            ended=true;enabled=false;Destroy(gameObject);
        }

        void Update()
        {
            if(!owner || !owner.isServer || owner.CurrentHealth<=0){Cancel();return;}
            if(!ended && !flight){Cancel();return;}
            if(audioSource)
            {
                if(Time.timeScale<=0){if(!paused){audioSource.Pause();paused=true;}}
                else{audioSource.pitch=Mathf.Clamp(Time.timeScale,.01f,3f);if(paused){audioSource.UnPause();paused=false;}}
            }
            if(!ended)return;
            foreach(var ps in tailParticles)if(ps && ps.IsAlive(true))return;
            Destroy(gameObject);
        }

        void OnDestroy(){if(flight)flight.CancelFlight();}
    }
}
