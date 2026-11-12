using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT
{

    public class PooledParticleSystem : PooledObject
    {
        ParticleSystem _particleSystem;

        Transform _targetToFollow;
        private void Update()
        {
            if (_targetToFollow) transform.position = _targetToFollow.position;
            //transform.SetPositionAndRotation(_targetToFollow.position, _targetToFollow.rotation);
        }

        public override void OnObjectInstantiated()
        {
            base.OnObjectInstantiated();
            _particleSystem = GetComponent<ParticleSystem>();
        }
        public override void OnObjectReused()
        {
            base.OnObjectReused();
            _particleSystem.Play();
        }

        public void SetPositionTarget(Transform targetToFollow)
        {
            _targetToFollow = targetToFollow;
        }
    }
}