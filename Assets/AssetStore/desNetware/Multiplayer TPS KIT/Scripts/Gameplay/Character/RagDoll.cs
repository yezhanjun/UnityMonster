using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public class RagDoll : MonoBehaviour
    {
        [SerializeField] float _ragdollRecoil;
        [SerializeField] float _ragdollMovementRecoil = 100f;
        [SerializeField] GameObject _headExplosion;


        public Rigidbody _head;
        public Rigidbody _footL;
        public Rigidbody _footR;
        public Rigidbody _hips;

        [SerializeField] Rigidbody[] _rigidBodies;

        //for synchronization
        public Transform[] rigidBodies;
        public Transform[] limbFlexors;

        [Tooltip("If You want character movement direction and force to be used for ragdoll then set this to true")]
        public bool ApplyMovementVelocity = true;

        private void Awake()
        {
            if (_rigidBodies == null || _rigidBodies.Length == 0)
                _rigidBodies = GetComponentsInChildren<Rigidbody>();
            EnablePhysics(false);
        }

        public Rigidbody[] PhysicsBodies => _rigidBodies;
        public void ConfigureRuntimeBodies(Rigidbody[] bodies)
        {
            _rigidBodies = bodies;
            EnablePhysics(false);
        }

        //client side ragdoll preparation
        public void ActivateRagdoll(CharacterPart hittedPart)
        {
            if (hittedPart == CharacterPart.head)
            {
                _ragdollRecoil *= 1.6f;
                if (_headExplosion) Instantiate(_headExplosion, _head.position, _head.rotation).transform.SetParent(transform);
            }           
        }

        //server side ragdoll preparation
        public void ServerActivateRagdoll(Vector3 myPosition, Vector3 killerPosition, Vector3 movementDirection, CharacterPart hittedPart, short attackForce) 
        {
            //server will be the only one to calculate ragdoll physics, then data will be sent to client so they can synchronize it
            //so here we enable physics so game can calculate it
            EnablePhysics(true);

            _head.AddForce((transform.position - killerPosition).normalized * _ragdollRecoil);

            if (ApplyMovementVelocity)
                _hips.AddForce(movementDirection * _ragdollMovementRecoil);

            if(hittedPart == CharacterPart.head)
                _head.AddForce((myPosition - killerPosition).normalized * attackForce * 0.6f);
            else
                _hips.AddForce((myPosition - killerPosition).normalized * attackForce);

            //applying force for random foot of ragdoll to make it look more fun
            if (Random.Range(1, 3) == 1)
            {
                _footR.AddForce((killerPosition - transform.position).normalized * _ragdollRecoil);
            }
            else
            {
                _footL.AddForce((killerPosition - transform.position).normalized * _ragdollRecoil);
            }
        }

        public void EnablePhysics(bool enable) 
        {
            foreach (Rigidbody rg in _rigidBodies) 
            {
                rg.useGravity = enable;
                rg.isKinematic = !enable;
            }
        }

    }
}
