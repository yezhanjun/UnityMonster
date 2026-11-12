using MTPSKIT.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public class PlayerRecoil : MonoBehaviour
    {

        public Transform recoilObject;
        Coroutine _CurrentRecoilCoroutine;
        public float recoilSpeed;

        int _lastHealth;

        [SerializeField] float _dmgMultiplier = 1f;
        [SerializeField] float _dmgMultiplierHeadshot = 1f;
        [SerializeField] ushort _maxDmg = 10;
        [SerializeField] float _minDmg = 200;

        private void Awake()
        {
            CharacterInstance characterInstance = GetComponent<CharacterInstance>();

            characterInstance.Client_OnHealthStateChanged += OnDamaged;
            characterInstance.Client_Resurrect += OnResurrect;

            _lastHealth = (short)characterInstance.CurrentHealth;
        }

        public void RecoilReset()
        {
            recoilObject.rotation = Quaternion.identity;
        }
        public void Recoil(float _recoil, float _devation, float _duration)
        {
            // Health RPCs can arrive after the server has deactivated a dead actor.
            if (!isActiveAndEnabled) return;
            if (_CurrentRecoilCoroutine != null)
            {
                StopCoroutine(_CurrentRecoilCoroutine);
                _CurrentRecoilCoroutine = null;
            }
            _CurrentRecoilCoroutine = StartCoroutine(DoRecoil(_recoil, _devation, _duration));
        }
        IEnumerator DoRecoil(float recoilVertical, float recoilHorizontal/*, float speed*/, float duration)
        {
            Quaternion recoilRot = Quaternion.Euler(recoilObject.localEulerAngles.x - recoilVertical, recoilObject.localEulerAngles.y + recoilHorizontal, 0);
            float timer = 0f;

            float comingBackDuration = 2f * duration;

            Quaternion startRot = recoilObject.localRotation;

            while (timer < duration)
            {
                timer += Time.deltaTime;
                recoilObject.localRotation = Quaternion.Slerp(startRot, recoilRot, (timer / duration));
                yield return null;
            }

            timer = 0f;
            startRot = recoilObject.localRotation;

            while (timer < comingBackDuration)
            {
                timer += Time.deltaTime;
                recoilObject.localRotation = Quaternion.Slerp(startRot, Quaternion.identity, (timer / comingBackDuration));
                yield return null;
            }
        }

        void OnResurrect(int health) => _lastHealth = health;


        private void OnDamaged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {
            Vector3 direction = transform.position - attackerID.transform.position;
            direction = transform.InverseTransformDirection(direction);
            direction.Normalize();

            int takenDamage = _lastHealth - currentHealth;
            _lastHealth = currentHealth;

            if (takenDamage <= 0) return;

            float dmgMultiplier = Mathf.Clamp(takenDamage, _minDmg, _maxDmg) * ((CharacterPart.head == damagedPart)
                ? _dmgMultiplierHeadshot
                : _dmgMultiplier);

            float x = direction.x * dmgMultiplier;
            float z = direction.z * dmgMultiplier;

            Recoil(x, z, 0.1f);
        }

    }
}

