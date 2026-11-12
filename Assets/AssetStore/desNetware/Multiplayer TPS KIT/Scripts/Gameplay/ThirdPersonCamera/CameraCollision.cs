using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public class CameraCollision : MonoBehaviour
    {
        public Transform parent;
        public float minDistance = 0.4f;
        public float maxDistance = 4f;
        Vector3 dollyDir;
        float _distance;

        public float backWardCameraTrackerRange = 1f;
        public float minimalCameraDistanceFromEnvironment = 0.45f;
        private int layer = (1 << 0);

        Vector3 cameraPosToLookAt;

        RaycastHit _rayHit;

        public void UpdateCameraProperties(Vector3 _cameraPos)
        {
            dollyDir = _cameraPos.normalized;
            maxDistance = _cameraPos.magnitude;
        }
        void Update()
        {
            cameraPosToLookAt = transform.position -= (transform.parent.position - transform.position) * backWardCameraTrackerRange;
            
            if (Physics.Linecast(transform.parent.position, cameraPosToLookAt, out _rayHit, layer))
            {
                _distance = Mathf.Clamp(_rayHit.distance-minimalCameraDistanceFromEnvironment, minDistance, maxDistance);
                Debug.DrawRay(transform.parent.position, cameraPosToLookAt - transform.parent.position, Color.red, _distance);
            }
            else
            {
                _distance = maxDistance;
            }

            transform.localPosition = dollyDir * _distance;
        }
    }

}