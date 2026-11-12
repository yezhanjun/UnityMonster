using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace MTPSKIT
{
    public class GranadePickup : PickupObject
    {
        public int GrenadeSupply = 2;
        protected override void Contact(CharacterInstance character)
        {
            if (character.CharacterItemManager.ServerGranadeSupply >= character.CharacterItemManager.MaxGranadeSupply) return;

            character.CharacterItemManager.AddGranadeNumber(GrenadeSupply);
            Pickedup();
        }
    }
}