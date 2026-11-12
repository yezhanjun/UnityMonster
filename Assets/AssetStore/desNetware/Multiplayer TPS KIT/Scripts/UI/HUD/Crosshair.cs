using MTPSKIT;
using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public class Crosshair : CharacterHud
    {
        [SerializeField] int _maxSizeInPixels = 128;
        [SerializeField] int _minSizeInPixels = 32;

        private int _imageDefaultSize = 32;
        float _targetSize;

        [SerializeField] Image _horizontalCrosshair;
        [SerializeField] Image _verticalCrosshair;
        [SerializeField] Image _dotCrosshair;

        public static Crosshair Instance;
        private Item _myItem;

        private Gun _myGun;


        private void Start()
        {
            _targetSize = _minSizeInPixels;
            _imageDefaultSize = (int)_horizontalCrosshair.rectTransform.sizeDelta.y;
        }

        void Update()
        {
            if (!_myGun)
                return;

            bool showCrosshair = !(_myObservedCharacter.IsAiming && _myGun && _myGun.HideCrosshairWhenAiming);

            _horizontalCrosshair.enabled = showCrosshair;
            _verticalCrosshair.enabled = showCrosshair;
            _dotCrosshair.enabled = showCrosshair;

            _targetSize = (_maxSizeInPixels-_minSizeInPixels) * ((_myGun.CurrentRecoil- _myGun._recoil_minAngle) / (_myGun._recoil_maxAngle- _myGun._recoil_minAngle)) + _minSizeInPixels;

            _horizontalCrosshair.rectTransform.sizeDelta = new Vector2(_targetSize, _imageDefaultSize);
            _verticalCrosshair.rectTransform.sizeDelta = new Vector2(_targetSize, _imageDefaultSize);
        }

        protected override void AssignCharacterForUI(CharacterInstance _characterInstanceToAssignForUI)
        {
            base.AssignCharacterForUI(_characterInstanceToAssignForUI);
        
        
            if (_characterInstanceToAssignForUI)
            {
                _characterInstanceToAssignForUI.CharacterItemManager.Client_EquipmentChanged -= ObserveItem;
            }

            _characterInstanceToAssignForUI.CharacterItemManager.Client_EquipmentChanged += ObserveItem;

            ObserveItem(_characterInstanceToAssignForUI.CharacterItemManager.currentlyUsedSlotID);
        }

        protected override void DeassignCurrentCharacterFromUI(CharacterInstance _characterToDeassign)
        {
            base.DeassignCurrentCharacterFromUI(_characterToDeassign);
            _myObservedCharacter.CharacterItemManager.Client_EquipmentChanged -= ObserveItem;
        }
        void ObserveItem(int currentSlot)
        {
            _myItem = null;
            _myGun = null;

            _myItem = _myObservedCharacter.CharacterItemManager.CurrentlyUsedItem;

            if (!_myItem) 
            {
                _horizontalCrosshair.enabled = false;
                _verticalCrosshair.enabled = false;
                return;
            }

            _myGun = _myItem.GetComponent<Gun>();

            if (!_myGun)
                return;

            _minSizeInPixels = _myGun.Crosshair_minSize;
            _maxSizeInPixels = _myGun.Crosshair_maxSize;

            _horizontalCrosshair.enabled = true;
            _verticalCrosshair.enabled = true;
        }

        public void ShowCrosshair(bool show) 
        {
            _horizontalCrosshair.enabled = show;
            _verticalCrosshair.enabled = show;
            _dotCrosshair.enabled = show;
        }
    }
}
