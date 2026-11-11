using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public class HUDItem : MonoBehaviour
    {
        [SerializeField]
        GameObject _scopeHud;

        public bool Aiming;
        [SerializeField] float _aimTransitionDuration = 0.5f;
        [SerializeField] float _aim = 0;

        [SerializeField] Image[] _scopeElements;
        [SerializeField] Color _scopeColor = Color.white;

        float _colorLerpSpeed;

        Item _item;
        CharacterInstance _characterInstance;

        private void Awake()
        {
            for (int i = 0; i < _scopeElements.Length; i++)
            {
                _scopeElements[i].color = Color.clear;
            }
        }

        private void Start()
        {
            _colorLerpSpeed = 1f/_aimTransitionDuration;

            if(_scopeHud)
                _scopeHud.SetActive(false);
        }

        private void Update()
        {
            if (!_characterInstance) return;


            if (_characterInstance.IsAiming && _characterInstance.CharacterItemManager.CurrentlyUsedItem == _item)
                _aim += Time.deltaTime * _colorLerpSpeed;
            else
                _aim -= Time.deltaTime * _colorLerpSpeed;

            _aim = Mathf.Clamp(_aim, 0, 1);

            Color lerp = Color.Lerp(Color.clear, _scopeColor, _aim);
            for (int i = 0; i < _scopeElements.Length; i++)
            {
                _scopeElements[i].color = lerp;
            }

        }

        public void Assign(Item item, CharacterInstance characterInstance) 
        {
            _item = item; 
            _characterInstance = characterInstance;
        }

    }
}