using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay {
    public class HealthPickup : PickupObject
    {
        public int HealthSupply = 50;

        protected override void Contact(CharacterInstance _character)
        {

            int supply = Jotunnslayer.SkillBattlePreview.ScaleFoodHealing(_character, HealthSupply);
            int heal = _character.ServerHeal(supply, netId);

            if (heal == 0) return;

            Pickedup();
        }

        protected override void ClientSupplyDepleted()
        {
            base.ClientSupplyDepleted();

            GetComponent<ParticleSystem>().Play();
        }
    }
}
