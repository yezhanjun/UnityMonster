using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT
{
    public class HUDSingleHitIndicator : MonoBehaviour
    {
        [SerializeField] Image _img;
        [SerializeField] float vanishingSpeed = 2f;
        Coroutine _myC;
        public void InitializeIndicator(Transform _source, Transform _player)
        {
            if (_myC != null)
            {
                StopCoroutine(_myC);
            }
            _myC = StartCoroutine(SetupIndicator(_source, _player));
        }
        private void Start()
        {
            Clear();
        }
        public void Clear()
        {
            _img.color = Color.clear;
        }
        IEnumerator SetupIndicator(Transform _damageSource, Transform _player)
        {
            _img.color = Color.white;
            float timer = 0f;
            while (timer < 2f && _damageSource && _player)
            {
                timer += Time.deltaTime;
                if (timer > 1.15f)
                {
                    _img.color = Color.Lerp(_img.color, Color.clear, Time.deltaTime * vanishingSpeed);
                }

                Vector3 direction = _player.transform.position - _damageSource.position + _damageSource.up * 0.2f; //changing source pos avoids look rotation zero error in case when player damages itself
                Quaternion tRot = Quaternion.LookRotation(direction);
                tRot.z = -tRot.y;
                tRot.x = 0;
                tRot.y = 0;

                Vector3 northDirection = new Vector3(0, 0, _player.transform.eulerAngles.y);

                transform.localRotation = tRot * Quaternion.Euler(northDirection);
                yield return null;
            }
            _img.color = Color.clear;
        }
    }
}