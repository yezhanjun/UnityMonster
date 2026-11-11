using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using MTPSKIT.Gameplay;
using MTPSKIT;
using MTPSKIT.Gameplay.Gamemodes;
namespace MTPSKIT.UI.HUD
{

    public class UIKillFeedElementV2 : UIElementsLayoutGrid
    {
        [SerializeField] Text _textKiller;
        [SerializeField] Text _textVictim;
        [SerializeField] Image _weapon;
        [SerializeField] Image _headShotIcon;
        [SerializeField] Image _background;
        [SerializeField] Sprite _meleeIcon;
        [SerializeField] Sprite _grenadeIcon;
        [SerializeField] Sprite _fallDamageIcon;
        [SerializeField] UIKillFeedV2 _killfeedParent;


        Coroutine c_vanish;

        public void Write(Health victim, CharacterPart hittedPart, AttackType attackType, Health killer, Health assist)
        {
            //there is possibility that killfeed message will come late to the client, when killer or victim are no longer present on map,
            //due to respawn, disconnect, changed team etc. If this is the case omit killfeed message
            //This is extremely likely to happer if game run in webgl, and tab is not in focus


           // Health killer = GameSync.Singleton.Healths.GetObj(killerID);
            if (!killer)
                return;

            CharacterInstance charKiller = killer.GetComponent<CharacterInstance>();
            if (!charKiller)
                return;

          //  Health victim = GameSync.Singleton.Healths.GetObj(victimID);
            if (!victim)
                return;

            //to make sure that tile will always reappear at the bottom
            gameObject.transform.SetAsLastSibling();

            _headShotIcon.gameObject.SetActive(hittedPart == CharacterPart.head);

            gameObject.SetActive(true);


            if (killer != victim && killer)
            {
                if (assist && assist!= victim)
                    _textKiller.text = $" {killer.CharacterName} + {assist.CharacterName} ";
                else
                    _textKiller.text = $" {killer.CharacterName} ";

                _textKiller.color = ClientInterfaceManager.Instance.UIColorSet.TeamColors[killer.Team];
            }
            else
            {
                _textKiller.text = string.Empty;
            }

            _textVictim.text = " " + victim.CharacterName + " ";
            _textVictim.color = ClientInterfaceManager.Instance.UIColorSet.TeamColors[victim.Team];

            Sprite weaponSprite = null;
            if (attackType == AttackType.hitscan)
            {
                if (killer)
                    weaponSprite = charKiller.CharacterItemManager.LastUsedItem ? charKiller.CharacterItemManager.LastUsedItem.KillFeedIcon : null;
                else
                    weaponSprite = null;
            }
            else
            {
                switch (attackType)
                {
                    case AttackType.melee:
                        weaponSprite = _meleeIcon;
                        break;
                    case AttackType.falldamage:
                        weaponSprite = _fallDamageIcon;
                        break;
                    case AttackType.explosion:
                        weaponSprite = _grenadeIcon;
                        break;
                }
            }
            _weapon.sprite = weaponSprite;

            StopVanishCoroutine();
            c_vanish = StartCoroutine(VanishTimer());
            IEnumerator VanishTimer()
            {
                float width = SetupElements();
                _background.rectTransform.sizeDelta = new Vector2(width, _background.rectTransform.sizeDelta.y);
                _background.transform.localPosition = new Vector2(-width / 2, 0);

                _killfeedParent.SetTiles();


                yield return new WaitForSeconds(6f);
                gameObject.SetActive(false);
            }
        }

        private void OnDisable()
        {
            StopVanishCoroutine();
        }

        void StopVanishCoroutine()
        {
            if (c_vanish != null)
            {
                StopCoroutine(c_vanish);
                c_vanish = null;
            }
        }
    }
}