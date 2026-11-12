using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    public class PickupObject : NetworkBehaviour
    {
        [SerializeField] public float TimeToReplenish = 5f;

        [SerializeField] MeshRenderer _pickupMesh;
        [SerializeField] AudioClip _pickupClip;

        bool _supply = true;

        private void Awake()
        {
            GameTools.SetLayerRecursively(gameObject, 11);
        }

        private void OnTriggerStay(Collider other)
        {
            if (!isServer) return;

            if (!_supply) return;

            CharacterInstance character = other.GetComponent<CharacterInstance>();

            if(character)
                Contact(character);
        }

        public void Pickedup()
        {
            _supply = false;

            SupplyDepleted();

            StartCoroutine(CountToReplenish());
            IEnumerator CountToReplenish()
            {
                yield return new WaitForSeconds(TimeToReplenish);
                SupplyReplenished();
                _supply = true;
            }
        }

        [ClientRpc]
        void SupplyDepleted() 
        {
            ClientSupplyDepleted();
        }
        [ClientRpc]
        void SupplyReplenished()
        {
            ClientSupplyReplenished();
        }
        protected virtual void ClientSupplyDepleted()
        {
            _pickupMesh.enabled = false;
            GetComponent<AudioSource>().PlayOneShot(_pickupClip);
        }

        protected virtual void ClientSupplyReplenished()
        {
            _pickupMesh.enabled = true;
        }

        protected virtual void Contact(CharacterInstance _character)
        {

        }
    }
}