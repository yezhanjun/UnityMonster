using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    public class DeathZone : NetworkBehaviour
    {
        private void OnTriggerEnter(Collider other)
        {
            if (!isServer) return;

            Health h = other.GetComponent<Health>();
            if (h)
                h.Server_ChangeHealthState(9999, CharacterPart.body, AttackType.falldamage, h, 4000);
        }
    }
}