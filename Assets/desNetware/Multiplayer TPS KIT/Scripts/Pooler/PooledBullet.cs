using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace MTPSKIT
{
    public class PooledBullet : PooledObject
    {
        TrailRenderer _trailRenderer;
        [SerializeField] float _bulletSpeed = 30f;
        Coroutine _bulletLiveCounter;
        [SerializeField] GameObject _bulletModel;

        public override void OnObjectInstantiated()
        {
            base.OnObjectInstantiated();
            _trailRenderer = GetComponent<TrailRenderer>();
        }

        public override void StartBullet(Vector3 targetPoint)
        {
            _bulletModel.SetActive(true);
            enabled = true;
            _trailRenderer.Clear();

            if (_bulletLiveCounter != null)
                StopCoroutine(_bulletLiveCounter);

            float timeOfLiving = Vector3.Distance(transform.position, targetPoint) / _bulletSpeed;
            _bulletLiveCounter = StartCoroutine(CountToDisable(timeOfLiving));

            transform.LookAt(targetPoint);
        }

        void Update()
        {
            transform.position += Time.deltaTime * _bulletSpeed * transform.forward;
        }
        IEnumerator CountToDisable(float timeToEnd)
        {
            yield return new WaitForSeconds(timeToEnd);
            _bulletModel.SetActive(false);
            enabled = false;
        }
    }
}