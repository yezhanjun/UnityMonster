using MTPSKIT.Gameplay;

using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using MTPSKIT.UI;
namespace MTPSKIT.UI.HUD
{
    public class HUDDamagePopup : CharacterHud
    {
        public static HUDDamagePopup Instance;

        [SerializeField] HUDDamagePopupElement _popupPrefab;
        [SerializeField] int _maxPopupsAtOnce = 6;
        [SerializeField] float _liveTime;
        public VerticalLayoutGroup GridParent;

        HUDDamagePopupElement[] _popups;

        int _nextPopupIDtoUse = 0;

        Health _lastVictim;

        [HideInInspector] public HUDDamagePopupElement _currentElement;

        [SerializeField] bool _separatePopupForElimination = true;

        protected override void Awake()
        {
            base.Awake();

            _popupPrefab.Init(this, _liveTime);
            _popupPrefab.gameObject.SetActive(false);

            _popups = new HUDDamagePopupElement[_maxPopupsAtOnce];
            for (int i = 0; i < _maxPopupsAtOnce - 1; i++)
            {
                HUDDamagePopupElement popup = Instantiate(_popupPrefab.gameObject, GridParent.transform).GetComponent<HUDDamagePopupElement>();
                _popups.SetValue(popup, i);
                popup.Init(this, _liveTime);
            }

            _popups.SetValue(_popupPrefab.GetComponent<HUDDamagePopupElement>(), _maxPopupsAtOnce - 1);

            Instance = this;
        }


        void OnDamageDealt(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health victim)
        {
            if (UICharacter._instance && UICharacter._instance.DamageNumbers && UICharacter._instance.DamageNumbers.Ready) return;
            UpdateState(currentHealth, takenDamage, damagedPart, attackType, victim, false);
        }

        public void UpdateState(int currentHealth, int takenDamage, CharacterPart damagedPart, AttackType attackType, Health victim, bool assist = false)
        {
            if (victim == ClientFrontend.ObservedCharacter && takenDamage > 0) //dont display damage popup for self harm 
                return;

            //if one of those conditions is met use another element instead of one at the top
            if (victim != _lastVictim || takenDamage<0 && _separatePopupForElimination || _currentElement == null)
            {
                _lastVictim = victim;

                _nextPopupIDtoUse++;
                if (_nextPopupIDtoUse >= _popups.Length)
                    _nextPopupIDtoUse = 0;

                _currentElement = _popups[_nextPopupIDtoUse];

                _currentElement.ResetPopup();
            }
            _currentElement.transform.SetAsFirstSibling();
            _currentElement.Set(takenDamage, victim, assist);
        }
        void Killed(CharacterPart damagedPart, Health victim)
        {
            UpdateState(0, -1, damagedPart, AttackType.hitscan, victim, false);
        }
        void OnEarnedAssist(Health victim) 
        {
            UpdateState(0, -1, CharacterPart.body, AttackType.hitscan, victim, true);
        }

        protected override void AssignCharacterForUI(CharacterInstance _characterInstanceToAssignForUI)
        {
            _characterInstanceToAssignForUI.Client_OnDamageDealt += OnDamageDealt;
            _characterInstanceToAssignForUI.Client_KillConfirmation += Killed;
            _characterInstanceToAssignForUI.Client_OnEarnedAssist += OnEarnedAssist;
        }
        protected override void DeassignCurrentCharacterFromUI(CharacterInstance _characterToDeassign)
        {
            _characterToDeassign.Client_OnDamageDealt -= OnDamageDealt;
            _characterToDeassign.Client_KillConfirmation -= Killed;
            _characterToDeassign.Client_OnEarnedAssist -= OnEarnedAssist;
        }
    }
}
