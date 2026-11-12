using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace MTPSKIT.Gameplay
{
    [RequireComponent(typeof(NetworkIdentity))]
    public class Minigun : Gun
    {

        [SerializeField] float _minCooldown = 0.08f;
        [SerializeField] float _maxCooldown = 0.4f;

        float _chargeFactor;
        [SerializeField] float _chargingSpeed = 1f;
        [SerializeField] float _dechargingSpeed = 1f;

        [Header("Barrels")]
        [SerializeField] float _barrelsRotationMaxSpeed = 720;
        [SerializeField] Transform _barrels;


        protected override void Update()
        {
            base.Update();
            if (!_myOwner) return;

            if (_myOwner.CharacterItemManager.UsePrimaryInput)
                _chargeFactor += _chargingSpeed * Time.deltaTime;
            else
                _chargeFactor -= _dechargingSpeed * Time.deltaTime;

            _chargeFactor = Mathf.Clamp(_chargeFactor, 0f, 1f);

            coolDown = Mathf.Lerp(_maxCooldown, _minCooldown, _chargeFactor);

            _barrels.Rotate(0, _chargeFactor * _barrelsRotationMaxSpeed * Time.deltaTime, 0);
        }

        public override void RequestReload()
        {
        }

    }
}