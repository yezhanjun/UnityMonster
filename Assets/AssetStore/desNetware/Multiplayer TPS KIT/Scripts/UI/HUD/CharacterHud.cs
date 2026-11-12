using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.UI.HUD
{
    /// <summary>
    /// Base class for every hud script that will display information about player
    /// </summary>
    public class CharacterHud : MonoBehaviour
    {
        protected CharacterInstance _myObservedCharacter;
        protected virtual void Awake()
        {
            ClientFrontend.ClientFrontendEvent_OnObservedCharacterSet += OnNewCharacterObserverd;
        }

        protected virtual void OnDestroy()
        {
            ClientFrontend.ClientFrontendEvent_OnObservedCharacterSet -= OnNewCharacterObserverd;
            if (_myObservedCharacter)
                DeassignCurrentCharacterFromUI(_myObservedCharacter);
        }

        private void OnNewCharacterObserverd(CharacterInstance _charInstance)
        {
            //desub from previously observed character
            if (_myObservedCharacter)
            {
                DeassignCurrentCharacterFromUI(_myObservedCharacter);
            }
            _myObservedCharacter = _charInstance;
            AssignCharacterForUI(_myObservedCharacter);
        }
        protected virtual void AssignCharacterForUI(CharacterInstance _characterInstanceToAssignForUI) 
        {

        }

        protected virtual void DeassignCurrentCharacterFromUI(CharacterInstance _characterToDeassign)
        {

        }
    }
}