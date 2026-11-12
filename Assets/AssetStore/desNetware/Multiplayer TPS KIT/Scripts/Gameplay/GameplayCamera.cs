using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT {
    public class GameplayCamera : MonoBehaviour
    {
        public static GameplayCamera _instance;
        private Transform target;

        private Camera _camera;

        
        private float _fovMultiplier = 1f;
        private float _rawRequestedFOV;

        private void Awake()
        {
            _rawRequestedFOV = UserSettings.FieldOfView;
            _instance = this;
            if (!target)
            {
                target = transform;
            }

            _camera = GetComponent<Camera>();
            if (!GetComponent<Gameplay.MeleeCameraFeedback>()) gameObject.AddComponent<Gameplay.MeleeCameraFeedback>();
            if (!GetComponent<Gameplay.FpsViewController>()) gameObject.AddComponent<Gameplay.FpsViewController>();
            if (!GetComponent<Gameplay.GodOfWarHeroCamera>()) gameObject.AddComponent<Gameplay.GodOfWarHeroCamera>();
        }

        public void SetFovMultiplier(float multiplier)
        {
            _fovMultiplier = multiplier;
        }

        private void Update()
        {
            if (Gameplay.GodOfWarHeroCamera.HandlesPlayer) return;
            if (target)
                transform.SetPositionAndRotation(target.position, target.rotation);

            float finalFOV = _rawRequestedFOV * _fovMultiplier;

            _camera.fieldOfView = finalFOV;

        }
        public void SetTarget(Transform _target)
        {
            if (!_target)
                return;
            target = _target;
        }

        public void SetFieldOfView(float fieldOfView)
        {
            _rawRequestedFOV = fieldOfView;
        }
    }

}
