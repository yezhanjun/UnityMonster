using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MTPSKIT.Gameplay;
using UnityEngine.UI;
using System;

namespace MTPSKIT.UI.HUD
{
    public class PlayerNametag : UIWorldIcon
    {
        public Text namePlaceholder;
        public Image healthbar;

        CharacterInstance myCharacter;

        public void SetupNameplate(CharacterInstance _myCharacter) 
        {
            _myCharacter.Client_OnHealthStateChanged += OnPlayerHealthStateChanged;
            myCharacter = _myCharacter;

            namePlaceholder.text = myCharacter.CharacterName;

            InitializeWorldIcon(myCharacter.CharacterMarkerPosition, false);
        }

        private void OnPlayerHealthStateChanged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {
            healthbar.fillAmount = (float)currentHealth / myCharacter.MaxHealth;
        }
    }
}