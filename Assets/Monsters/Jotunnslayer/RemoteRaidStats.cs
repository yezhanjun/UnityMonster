using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Owner weapon prediction reads the server-calculated base; existing slow deadlines stay separate.
    public sealed class RemoteRaidStats : MonoBehaviour
    {
        internal float AttackSpeed { get; private set; } = 1f;
        internal bool HasState { get; private set; }

        internal void Receive(CharacterInstance actor, float attackSpeed)
        {
            if (!actor || !actor.isOwned || actor.isServer || actor.CurrentHealth <= 0 ||
                float.IsNaN(attackSpeed) || float.IsInfinity(attackSpeed) || attackSpeed <= 0f) return;
            AttackSpeed = attackSpeed;
            HasState = true;
        }

        internal void Clear() { AttackSpeed = 1f; HasState = false; }
        private void OnDisable() => Clear();
    }
}
