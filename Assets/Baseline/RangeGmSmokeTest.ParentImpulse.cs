using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.IO;
using Mirror;
using MTPSKIT.RainImported;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeGmSmokeTest
    {
        [Serializable] sealed class ParentImpulseRemoteResult
        {
            public bool passed, owned, observedImpulse;
            public float movement, initialVelocityX;
        }
        static readonly Vector3 ParentImpulseRemotePosition=new Vector3(0,1000.15f,0);
        static string ParentImpulseSyncDirectory => Environment.GetEnvironmentVariable("TPSSHOOT_PARENT_IMPULSE_SYNC");

        IEnumerator VerifyParentImpulseHost(RangeResearchLab lab, PlayerInstance guest)
        {
            const string marker="RAIN_PARENT_IMPULSE_HOST";
            string sync=ParentImpulseSyncDirectory;
            if(string.IsNullOrEmpty(sync)||!Directory.Exists(sync)) { Debug.LogError(marker+"_SYNC_MISSING"); yield break; }
            var victim=guest.MyCharacter;
            var motor=victim.GetComponent<CharacterMotor>();
            int health=victim.CurrentHealth,maximum=victim.MaxHealth;
            float regen=victim.RainHealthRegen;
            bool invulnerable=victim.RangeInvulnerable;
            var entry=new BrowserEntry { id="PARENT_BODY",category=1 };
            GameObject floor=null;
            try
            {
                victim.RangeInvulnerable=true; victim.RainHealthRegen=0;
                victim.MaxHealth=victim.CurrentHealth=1000;
                floor=CreateParentImpulseRemoteFloor();
                lab.SetMonsterCombat(false); lab.ExecuteCatalog(entry);
                var actor=FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(a=>a.IsLabSummon&&a.Archetype==RangeResearchEnemy.ParentKind);
                if(!actor) { Debug.LogError(marker+"_SOURCE_MISSING"); yield break; }
                actor.enabled=false;
                float deadline=Time.realtimeSinceStartup+20;
                while((!File.Exists(Path.Combine(sync,"ready"))||Vector3.Distance(victim.transform.position,ParentImpulseRemotePosition)>1f)&&Time.realtimeSinceStartup<deadline)
                    yield return null;
                if(!File.Exists(Path.Combine(sync,"ready"))||Vector3.Distance(victim.transform.position,ParentImpulseRemotePosition)>1f)
                { Debug.LogError($"{marker}_CLIENT_READY_TIMEOUT ready={File.Exists(Path.Combine(sync,"ready"))} position={victim.transform.position} expected={ParentImpulseRemotePosition}"); yield break; }
                Vector3 before=victim.transform.position;
                victim.RangeInvulnerable=false;
                // Local integration covers the actual Parent overlap query. This
                // fixture isolates accepted-hit -> TargetRpc -> owner movement.
                victim.ServerApplyRainBlast(1,actor.GetComponent<CharacterInstance>(),Vector3.right*3000);
                victim.RangeInvulnerable=true;
                deadline=Time.realtimeSinceStartup+20;
                string resultPath=Path.Combine(sync,"result.json");
                while(!File.Exists(resultPath)&&Time.realtimeSinceStartup<deadline) yield return null;
                if(!File.Exists(resultPath)) { Debug.LogError(marker+"_CLIENT_RESULT_TIMEOUT"); yield break; }
                var result=JsonUtility.FromJson<ParentImpulseRemoteResult>(File.ReadAllText(resultPath));
                deadline=Time.realtimeSinceStartup+3;
                while(victim.transform.position.x-before.x<.5f&&Time.realtimeSinceStartup<deadline) yield return null;
                float replicaTravel=victim.transform.position.x-before.x;
                bool server=result.passed&&result.owned&&result.observedImpulse&&victim.CurrentHealth==999&&
                    !motor.RainImpulseActive&&replicaTravel>.5f&&guest.connectionToClient!=null;
                if(!server) Debug.LogError($"{marker}_FAILED client={result.passed} health={victim.CurrentHealth} serverActive={motor.RainImpulseActive} replicaTravel={replicaTravel}");
                else Debug.Log($"{marker}_CHECKED owner_only=True accepted_damage=1 client_delta_v={result.initialVelocityX} client_travel={result.movement} replicated_travel={replicaTravel}");
                File.WriteAllText(Path.Combine(sync,"release"),"release");
                deadline=Time.realtimeSinceStartup+5;
                while(!File.Exists(Path.Combine(sync,"cleaned"))&&Time.realtimeSinceStartup<deadline) yield return null;
                if(server&&File.Exists(Path.Combine(sync,"cleaned"))) Debug.Log(marker+"_PASSED remote_target_rpc=True production_blast_query_verified_locally=True");
                else if(server) Debug.LogError(marker+"_CLIENT_CLEANUP_TIMEOUT");
            }
            finally
            {
                File.WriteAllText(Path.Combine(sync,"release"),"release");
                lab.SetMonsterCombat(false);lab.RemoveCatalog(entry);
                if(victim) {victim.RangeInvulnerable=invulnerable;victim.RainHealthRegen=regen;victim.MaxHealth=maximum;victim.CurrentHealth=health;}
                if(floor) Destroy(floor);
            }
        }

        IEnumerator VerifyParentImpulseClient()
        {
            const string marker="RAIN_PARENT_IMPULSE_CLIENT";
            string sync=ParentImpulseSyncDirectory;
            if(string.IsNullOrEmpty(sync)||!Directory.Exists(sync)) { Debug.LogError(marker+"_SYNC_MISSING"); yield break; }
            float deadline=Time.realtimeSinceStartup+25;
            while((!NetworkClient.localPlayer||!NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter)&&Time.realtimeSinceStartup<deadline) yield return null;
            var player=NetworkClient.localPlayer?NetworkClient.localPlayer.GetComponent<PlayerInstance>():null;
            var owner=player?player.MyCharacter:null;
            if(!owner) {Debug.LogError(marker+"_OWNER_MISSING");yield break;}
            var motor=owner.GetComponent<CharacterMotor>();
            Vector3 originalPosition=owner.transform.position;
            Vector2 originalInput=owner.movementInput;
            bool enabled=motor.enabled;
            var floor=CreateParentImpulseRemoteFloor();
            try
            {
                motor.enabled=true;motor.ResetRainMonsterVelocity();owner.SetMovementInput(Vector2.zero);
                owner.transform.position=ParentImpulseRemotePosition;Physics.SyncTransforms();
                // Mirror explicitly requires its teleport API for discontinuities;
                // changing Transform alone leaves snapshot interpolation history intact.
                var networkTransform=owner.GetComponent<NetworkTransformBase>();
                if(networkTransform) networkTransform.CmdTeleport(ParentImpulseRemotePosition);
                yield return null;yield return null;
                Debug.Log($"{marker}_READY position={owner.transform.position} networkTransform={networkTransform!=null}");
                File.WriteAllText(Path.Combine(sync,"ready"),"ready");
                deadline=Time.realtimeSinceStartup+20;
                while(!motor.RainImpulseActive&&Time.realtimeSinceStartup<deadline) yield return null;
                var result=new ParentImpulseRemoteResult {owned=motor.isOwned,observedImpulse=motor.RainImpulseActive,initialVelocityX=motor.RainImpulseVelocity.x};
                Vector3 before=owner.transform.position;
                yield return new WaitForSeconds(.12f);
                result.movement=owner.transform.position.x-before.x;
                result.passed=result.owned&&result.observedImpulse&&result.initialVelocityX>25&&result.initialVelocityX<=30.01f&&result.movement>.5f&&result.movement<6;
                if(result.passed) Debug.Log($"{marker}_CHECKED target_rpc=True delta_v={result.initialVelocityX} movement={result.movement}");
                else Debug.LogError($"{marker}_FAILED owned={result.owned} observed={result.observedImpulse} delta_v={result.initialVelocityX} movement={result.movement}");
                string temp=Path.Combine(sync,"result.tmp");File.WriteAllText(temp,JsonUtility.ToJson(result));File.Move(temp,Path.Combine(sync,"result.json"));
                deadline=Time.realtimeSinceStartup+8;
                while(!File.Exists(Path.Combine(sync,"release"))&&Time.realtimeSinceStartup<deadline) yield return null;
                if(!File.Exists(Path.Combine(sync,"release"))) Debug.LogError(marker+"_RELEASE_TIMEOUT");
            }
            finally
            {
                motor.ResetRainMonsterVelocity();motor.enabled=enabled;owner.SetMovementInput(originalInput);
                owner.transform.position=originalPosition;Destroy(floor);Physics.SyncTransforms();
                var networkTransform=owner.GetComponent<NetworkTransformBase>();
                if(networkTransform) networkTransform.CmdTeleport(originalPosition);
                File.WriteAllText(Path.Combine(sync,"cleaned"),"cleaned");
            }
            Debug.Log(marker+"_CLEANUP_CHECKED");
        }

        static GameObject CreateParentImpulseRemoteFloor()
        {
            var floor=new GameObject("Parent remote impulse test floor");
            floor.transform.position=new Vector3(0,999.5f,0);
            floor.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
            Physics.SyncTransforms();return floor;
        }

        IEnumerator VerifyParentImpulseGm(RangeResearchLab lab, CharacterInstance owner)
        {
            const string marker = "RAIN_PARENT_IMPULSE";
            // Reference cases distinguish a force impulse from a displacement,
            // preserve the exact ground-stick threshold, and cover original mass zero.
            bool math = RainCharacterImpulseMath.BlastImpulse(Vector3.zero,10,6000)==Vector3.zero &&
                RainCharacterImpulseMath.BlastImpulse(Vector3.right*10,10,6000)==Vector3.zero &&
                RainCharacterImpulseMath.BlastImpulse(Vector3.right*12,10,6000)==Vector3.zero &&
                Vector3.Distance(RainCharacterImpulseMath.BlastImpulse(Vector3.right*5,10,6000),Vector3.right*3000)<.001f &&
                RainCharacterImpulseMath.VelocityChange(Vector3.up*599,100,true)==Vector3.zero &&
                RainCharacterImpulseMath.VelocityChange(Vector3.up*600,100,true)==Vector3.up*6 &&
                RainCharacterImpulseMath.VelocityChange(Vector3.down*600,100,true)==Vector3.zero &&
                RainCharacterImpulseMath.VelocityChange(Vector3.down*600,100,false)==Vector3.down*6 &&
                RainCharacterImpulseMath.VelocityChange(Vector3.one,0,false)==Vector3.zero &&
                RainCharacterImpulseMath.VelocityChange(Vector3.right*6000,300,false)==Vector3.right*20;
            var groundStep=RainCharacterImpulseMath.PreMove(Vector3.right*30,Vector3.zero,80,.25f,true,-30,.1f);
            var airStep=RainCharacterImpulseMath.PreMove(Vector3.right*30,Vector3.zero,80,.25f,false,-30,.1f);
            math &= Vector3.Distance(groundStep,new Vector3(22,0,0))<.001f &&
                Vector3.Distance(airStep,new Vector3(28,-3,0))<.001f;
            if(!math) { Debug.LogError(marker+"_MATH_FAILED"); yield break; }
            Debug.Log(marker+"_MATH_CHECKED center_edge=True mass=True ground_stick_6=True original_acceleration=True");

            var entry=new BrowserEntry { id="PARENT_BODY",category=1 };
            var motor=owner.GetComponent<CharacterMotor>();
            var controller=owner.GetComponent<CharacterController>();
            Vector3 originalPosition=owner.transform.position;
            Vector2 originalInput=owner.movementInput,originalLook=owner.lookInput;
            int health=owner.CurrentHealth,maximum=owner.MaxHealth;
            float regen=owner.RainHealthRegen,originalTimeScale=Time.timeScale;
            bool enabled=motor.enabled,invulnerable=owner.RangeInvulnerable;
            var originalDamageAllowed=owner.ServerDamageAllowed;
            RangeResearchEnemy actor=null;
            GameObject floor=null,hitbox=null,otherHitbox=null,wall=null;
            try
            {
                lab.SetMonsterCombat(false);
                for(int direction=0;direction<4&&!actor;direction++)
                {
                    owner.lookInput=new Vector2(0,originalLook.y+direction*90);
                    lab.ExecuteCatalog(entry);
                    actor=FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                        .FirstOrDefault(a=>a.IsLabSummon&&a.Archetype==RangeResearchEnemy.ParentKind);
                }
                if(!actor) { Debug.LogError(marker+"_SPAWN_MISSING"); yield break; }
                actor.enabled=false; // isolate the production blast from AI and natural attack timing
                var enemy=actor.GetComponent<CharacterInstance>();
                var zone=actor.GetComponent<RainActorVisual>().FindModelBone("SlamZone");
                var strike=typeof(RangeResearchEnemy).GetMethod("StrikeParentSlam",BindingFlags.Instance|BindingFlags.NonPublic);
                if(!zone||strike==null||!controller.enabled) { Debug.LogError(marker+"_FIXTURE_MISSING"); yield break; }
                bool profiles=motor.RainPhysicsMass==100 && motor.RainPhysicsAcceleration==80 &&
                    enemy.GetComponent<CharacterMotor>().RainPhysicsMass==300 && enemy.GetComponent<CharacterMotor>().RainPhysicsAcceleration==40;
                if(!profiles) Debug.LogError(marker+"_PROFILES_FAILED");
                owner.MaxHealth=owner.CurrentHealth=1000; owner.RainHealthRegen=0;
                owner.SetMovementInput(Vector2.zero); owner.ServerDamageAllowed=null;
                // The extra sphere is the unique nearest point. A second, smaller
                // collider of the same Health verifies one impulse per victim.
                hitbox=new GameObject("Parent impulse nearest hurtbox"); hitbox.layer=(int)GameLayers.character;
                hitbox.transform.SetParent(owner.transform,false); hitbox.transform.localPosition=Vector3.left*3;
                var sphere=hitbox.AddComponent<SphereCollider>(); sphere.radius=.6f; sphere.isTrigger=true;
                otherHitbox=new GameObject("Parent impulse duplicate hurtbox"); otherHitbox.layer=(int)GameLayers.character;
                otherHitbox.transform.SetParent(owner.transform,false); otherHitbox.transform.localPosition=Vector3.left*3;
                var other=otherHitbox.AddComponent<SphereCollider>(); other.radius=.4f; other.isTrigger=true;
                // Keep this fixture above the map. It does not assert original terrain navigation.
                actor.transform.position+=Vector3.up*60;
                Vector3 center=zone.position;
                floor=new GameObject("Parent impulse floor"); // Default layer, as in the existing Parent GM floor.
                floor.transform.position=new Vector3(center.x,center.y-.65f,center.z);
                floor.AddComponent<BoxCollider>().size=new Vector3(100,1,100);
                owner.transform.rotation=Quaternion.identity;
                owner.transform.position=center+Vector3.right*5;
                motor.enabled=true; motor.ResetRainMonsterVelocity();
                Physics.SyncTransforms();
                owner.RangeInvulnerable=true;
                strike.Invoke(actor,null);
                bool invulnerableRejected=owner.CurrentHealth==1000&&!motor.RainImpulseActive;
                owner.RangeInvulnerable=false; owner.ServerDamageAllowed=_=>false;
                strike.Invoke(actor,null);
                bool gateRejected=owner.CurrentHealth==1000&&!motor.RainImpulseActive;
                owner.ServerDamageAllowed=null;
                strike.Invoke(actor,null);
                // 2m sphere center - .6m radius => 1.4m nearest point:
                // damage round(64*.86)=55, impulse 6000*.86=5160, delta-v=51.6.
                bool accepted=owner.CurrentHealth==945&&motor.RainImpulseActive&&
                    Vector3.Distance(motor.RainImpulseVelocity,Vector3.right*51.6f)<.01f;
                if(!invulnerableRejected||!gateRejected||!accepted)
                    Debug.LogError($"{marker}_BLAST_FAILED invulnerable={invulnerableRejected} gate={gateRejected} accepted={accepted} health={owner.CurrentHealth} velocity={motor.RainImpulseVelocity}");
                else Debug.Log(marker+"_BLAST_CHECKED nearest_point=True duplicate_health_once=True damage=55 impulse=5160 delta_v=51.6 rejected_hits_no_force=True");

                Vector3 before=owner.transform.position;
                yield return new WaitForSeconds(.1f);
                float distance=owner.transform.position.x-before.x;
                bool moved=distance>.5f&&distance<6.5f;
                if(!moved) Debug.LogError(marker+"_MOVEMENT_FAILED distance="+distance);
                else Debug.Log(marker+"_MOVEMENT_CHECKED displacement="+distance);
                Time.timeScale=0;
                Vector3 paused=owner.transform.position,pausedVelocity=motor.RainImpulseVelocity;
                yield return new WaitForSecondsRealtime(.12f);
                bool pause=Vector3.Distance(paused,owner.transform.position)<.001f&&Vector3.Distance(pausedVelocity,motor.RainImpulseVelocity)<.001f;
                Time.timeScale=originalTimeScale;

                // The real CharacterController must stop an incoming velocity at a wall.
                motor.ResetRainMonsterVelocity(); owner.transform.position=before;
                wall=new GameObject("Parent impulse blocking wall"); wall.layer=floor.layer;
                wall.transform.position=before+new Vector3(2,1,0);
                wall.AddComponent<BoxCollider>().size=new Vector3(.3f,8,8);
                Physics.SyncTransforms();
                motor.ServerApplyRainImpulse(Vector3.right*3000);
                yield return new WaitForSeconds(.15f);
                float wallTravel=owner.transform.position.x-before.x;
                bool collision=wallTravel>.2f&&wallTravel<2&&Mathf.Abs(motor.RainImpulseVelocity.x)<.01f;
                if(!collision) Debug.LogError($"{marker}_COLLISION_FAILED distance={wallTravel} velocity={motor.RainImpulseVelocity}");
                else Debug.Log(marker+"_COLLISION_CHECKED no_wall_tunnel=True incoming_velocity_removed=True");
                motor.ServerApplyRainImpulse(Vector3.up*1000);
                bool launched=motor.RainImpulseActive&&motor.RainImpulseVelocity.y>0;
                motor.enabled=false;
                bool cleared=!motor.RainImpulseActive&&motor.RainImpulseVelocity==Vector3.zero;
                motor.enabled=true;
                float restartY=owner.transform.position.y;
                yield return new WaitForSeconds(.1f);
                bool noGhostLaunch=owner.transform.position.y<=restartY+.03f;
                if(!pause||!launched||!cleared||!noGhostLaunch)
                    Debug.LogError($"{marker}_LIFECYCLE_FAILED pause={pause} launched={launched} cleared={cleared} noGhostLaunch={noGhostLaunch}");
                else Debug.Log(marker+"_LIFECYCLE_CHECKED scaled_pause=True disable_clears=True reenable_no_ghost_launch=True");
                if(profiles&&invulnerableRejected&&gateRejected&&accepted&&moved&&pause&&collision&&launched&&cleared&&noGhostLaunch)
                    Debug.Log(marker+"_LOCAL_PASSED remote_owner_and_full_original_terrain_pending=True");
            }
            finally
            {
                Time.timeScale=originalTimeScale;
                lab.SetMonsterCombat(false); lab.RemoveCatalog(entry);
                motor.ResetRainMonsterVelocity(); motor.enabled=enabled;
                owner.ServerDamageAllowed=originalDamageAllowed; owner.RangeInvulnerable=invulnerable;
                owner.RainHealthRegen=regen; owner.MaxHealth=maximum; owner.CurrentHealth=health;
                owner.transform.position=originalPosition; owner.SetMovementInput(originalInput); owner.lookInput=originalLook;
                foreach(var go in new[] { floor,hitbox,otherHitbox,wall }) if(go) Destroy(go);
                Physics.SyncTransforms();
            }
        }
    }
}
