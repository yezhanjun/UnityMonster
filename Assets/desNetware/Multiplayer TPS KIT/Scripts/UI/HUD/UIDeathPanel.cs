using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI.HUD
{
    public class UIDeathPanel : CharacterHud
    {
        [Header("Killer representer")]
        [SerializeField] GameObject _deathPanel;
        [SerializeField] Text _killerNameRenderer;
        [SerializeField] Image _killerHpBar;
        [SerializeField] Image _weaponSpriteRenderer;
        [SerializeField] Text _killerHpRender;

        [Header("Spawn cooldown counter")]
        [SerializeField] GameObject _spawnCooldownPanel;
        [SerializeField] Image _bar;
        [SerializeField] Text _spawnCooldownCounterText;
        

        protected override void Awake()
        {
            base.Awake();
            _deathPanel.SetActive(false);
            _spawnCooldownPanel.SetActive(false);
        }

        protected override void AssignCharacterForUI(CharacterInstance _characterInstanceToAssignForUI)
        {
            base.AssignCharacterForUI(_characterInstanceToAssignForUI);
            _characterInstanceToAssignForUI.Client_OnHealthDepleted += OnPlayerDied;
            _characterInstanceToAssignForUI.Client_Resurrect += OnPlayerResurrected;
            _deathPanel.SetActive(false);
        }

        protected override void DeassignCurrentCharacterFromUI(CharacterInstance _characterToDeassign)
        {
            _characterToDeassign.Client_OnHealthDepleted -= OnPlayerDied;
            _characterToDeassign.Client_Resurrect -= OnPlayerResurrected;
        }


        private void OnPlayerResurrected(int health)
        {
            _spawnCooldownPanel.SetActive(false);
            _deathPanel.SetActive(false);
        }

        private void OnPlayerDied(CharacterPart damagedPart, Health attacker)
        {
            _spawnCooldownPanel.SetActive(true);
            StartCoroutine(RespawnCooldownCounter());

            if (attacker == ClientFrontend.ObservedCharacter) return;

            _deathPanel.SetActive(true);
            _killerHpRender.text = attacker.CurrentHealth.ToString();
            _killerNameRenderer.text = attacker.CharacterName.ToString();
            _killerHpBar.fillAmount = (float)attacker.CurrentHealth / attacker.MaxHealth;


            CharacterItemManager killerItemManger = attacker.GetComponent<CharacterItemManager>();
            if (killerItemManger)
            {
                _weaponSpriteRenderer.sprite = killerItemManger.LastUsedItem ? killerItemManger.LastUsedItem.ItemIcon : null;
                _weaponSpriteRenderer.color = killerItemManger.LastUsedItem ? Color.white : Color.clear;
            }
        }

        IEnumerator RespawnCooldownCounter() 
        {
            float lastRoundedTime = RoomSetup.Properties.P_RespawnCooldown;
            float currentTime = RoomSetup.Properties.P_RespawnCooldown;
            while (_myObservedCharacter.CurrentHealth <= 0) 
            {
                currentTime -= Time.deltaTime;
                float currentRoundedTime = (float)Math.Round(currentTime,1);

                if (lastRoundedTime != currentRoundedTime) 
                {
                    lastRoundedTime = currentRoundedTime;
                    _spawnCooldownCounterText.text = $"{currentRoundedTime} 秒后复活";
                }

                _bar.fillAmount = Mathf.Clamp(1f - (currentTime / RoomSetup.Properties.P_RespawnCooldown),0,1f);

                yield return null;
            }

            _spawnCooldownPanel.SetActive(false);
        }
    }
}