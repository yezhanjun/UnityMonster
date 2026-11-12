using System.Collections;
using MTPSKIT;
using MTPSKIT.Gameplay;
using Mirror;
using UnityEngine;

namespace Jotunnslayer
{
    // Source default DeathType.Destroy: deactivate, wait two scaled seconds,
    // then remove only if still dead. Special death-effect overrides are separate.
    internal sealed class DraugDeathPresentation : MonoBehaviour
    {
        CharacterInstance owner;
        DraugDeathRemoval removal;
        internal bool RemovalPending => removal;

        void Awake() { owner = GetComponent<CharacterInstance>(); }
        void OnEnable() { if (owner) owner.Server_OnHealthDepleted += OnDeath; }
        void OnDisable() { if (owner) owner.Server_OnHealthDepleted -= OnDeath; }
        void OnDestroy()
        {
            if (removal) Destroy(removal.gameObject);
        }

        void OnDeath(CharacterPart part, AttackType attack, Health killer, int force)
        {
            if (!owner || !owner.isServer || owner.CurrentHealth > 0 || removal) return;
            // A coroutine on the actor would stop when its root is deactivated.
            var job = new GameObject("Draug delayed death removal");
            removal = job.AddComponent<DraugDeathRemoval>();
            removal.Begin(this, owner);
            owner.gameObject.SetActive(false);
        }

        internal void Release(DraugDeathRemoval job)
        {
            if (removal == job) removal = null;
        }
    }

    internal sealed class DraugDeathRemoval : MonoBehaviour
    {
        DraugDeathPresentation source;
        CharacterInstance owner;

        internal void Begin(DraugDeathPresentation presenter, CharacterInstance actor)
        {
            source = presenter;
            owner = actor;
            StartCoroutine(RemoveWhenStillDead());
        }

        IEnumerator RemoveWhenStillDead()
        {
            yield return new WaitForSeconds(2f);
            Detach();
            // Mirror destruction adapts source RemoveFromScene's pool removal.
            if (owner && NetworkServer.active && owner.isServer && owner.CurrentHealth <= 0)
                NetworkServer.Destroy(owner.gameObject);
            Destroy(gameObject);
        }

        void Detach()
        {
            if (source) source.Release(this);
            source = null;
        }
        void OnDestroy() { Detach(); }
    }
}
