using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    // Server owns complete-body corpse physics.
    public class RagDollSyncer : NetworkBehaviour
    {
        RagDoll ragdoll;
        Health health;
        Vector3[] positions;
        Quaternion[] rotations;
        bool synchronizing, received;
        float nextSend, quietSince;
        [Min(.01f)] public float RagdollLerpSpeed = 20f;

        void Start()
        {
            health = GetComponent<Health>();
            health.Server_Resurrect += OnResurrect;
            health.Client_Resurrect += OnResurrect;
        }
        void OnResurrect(int value)
        {
            synchronizing = received = false;
            ragdoll = null;
            positions = null; rotations = null;
        }
        public void AssignRagdoll(RagDoll value)
        {
            if (ragdoll == value) return;
            ragdoll = value; received = false;
            int count = value.PhysicsBodies.Length;
            positions = new Vector3[count]; rotations = new Quaternion[count];
            var own = value.GetComponentsInChildren<Collider>();
            var actor = GetComponentsInChildren<Collider>(true);
            foreach (var a in own) foreach (var b in actor)
                if (a && b && a != b && !b.transform.IsChildOf(value.transform)) Physics.IgnoreCollision(a,b);
        }
        public void ServerStartSynchronizingRagdoll(RagDoll value)
        {
            AssignRagdoll(value);
            synchronizing = true; nextSend = 0; quietSince = Time.time;
        }
        void Update()
        {
            if(!ragdoll)return;
            var bodies = ragdoll.PhysicsBodies;
            if(isServer)
            {
                if(!synchronizing || Time.time<nextSend)return;
                nextSend=Time.time+.05f;
                bool moving=false;
                for(int i=0;i<bodies.Length;i++)
                {
                    positions[i]=bodies[i].position;rotations[i]=bodies[i].rotation;
                    moving |= !bodies[i].IsSleeping() && (bodies[i].linearVelocity.sqrMagnitude>.0004f || bodies[i].angularVelocity.sqrMagnitude>.0025f);
                }
                if(moving)quietSince=Time.time;
                bool settled=Time.time-quietSince>.75f;
                if(settled){RpcFinalPose(positions,rotations);synchronizing=false;}
                else RpcPose(positions,rotations);
            }
            else if(received)
            {
                float blend=1f-Mathf.Exp(-RagdollLerpSpeed*Time.deltaTime);
                // Apply parent bones before their children.
                for(int i=0;i<bodies.Length;i++)
                {bodies[i].transform.SetPositionAndRotation(Vector3.Lerp(bodies[i].position,positions[i],blend),Quaternion.Slerp(bodies[i].rotation,rotations[i],blend));}
            }
        }
        [ClientRpc(channel=Channels.Unreliable)]
        void RpcPose(Vector3[] p,Quaternion[] q) => Receive(p,q);
        [ClientRpc]
        void RpcFinalPose(Vector3[] p,Quaternion[] q) => Receive(p,q);
        void Receive(Vector3[] p,Quaternion[] q)
        {
            if(isServer || !ragdoll || p.Length!=positions.Length || q.Length!=rotations.Length)return;
            // Mirror owns incoming arrays. Copy into persistent interpolation buffers.
            System.Array.Copy(p,positions,p.Length);System.Array.Copy(q,rotations,q.Length);received=true;
        }
        void OnDestroy()
        {
            if(health){health.Server_Resurrect-=OnResurrect;health.Client_Resurrect-=OnResurrect;}
            ragdoll=null;
        }
    }
}
