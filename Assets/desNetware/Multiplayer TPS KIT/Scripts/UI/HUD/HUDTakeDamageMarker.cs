using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MTPSKIT;
using MTPSKIT.Gameplay;
using System;

namespace MTPSKIT.UI.HUD
{
    public class HUDTakeDamageMarker : MonoBehaviour
    {
        [SerializeField] GameObject markerPrefab;
        List<HUDSingleHitIndicator> takeDamageMarker = new List<HUDSingleHitIndicator>();

        private CharacterInstance _observedCharacterInstance;
        byte maxMarkers = 5;
        byte currentUsedMarkerId = 0;


        private void Awake()
        {
            // GameManager.obser += Initialize;
        }
        private void OnDestroy()
        {
            // GameManager.GameEvent_ObservedCharacterSet -= Initialize;
        }
        void Start()
        {
            takeDamageMarker.Add(markerPrefab.GetComponent<HUDSingleHitIndicator>());

            for (int i = 0; i < maxMarkers - 1; i++)
                takeDamageMarker.Add(Instantiate(markerPrefab, transform).GetComponent<HUDSingleHitIndicator>());
        }

        void Update()
        {
        }

        
        public void Initialize(CharacterInstance _charInstance)
        {
            if (_observedCharacterInstance != null) 
                _observedCharacterInstance.Client_OnHealthStateChanged -= SetTakeDamageMarker; //desub previous character

            _observedCharacterInstance = _charInstance;
            _observedCharacterInstance.Client_OnHealthStateChanged += SetTakeDamageMarker;
            foreach (HUDSingleHitIndicator _indicator in takeDamageMarker)
            {
                _indicator.Clear();
            }
        }

        private void SetTakeDamageMarker(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {
            if (currentUsedMarkerId == maxMarkers)
                currentUsedMarkerId = 0;

            takeDamageMarker[currentUsedMarkerId].InitializeIndicator(attackerID.transform, GameplayCamera._instance.transform);
            currentUsedMarkerId++;
        }
    }
}