using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.Events;

namespace Jotunnslayer
{
    // Niflheim easy boss encounter 97791, wave 0: one fixed-position actor, AllKilled.
    public sealed class NiflheimEasyBossEncounter : MonoBehaviour
    {
        [SerializeField] Transform spawnPoint;
        [SerializeField] UnityEvent completed;

        CharacterInstance boss;
        bool started;
        bool cleared;

        internal CharacterInstance SpawnedBoss => boss;
        internal bool Cleared => cleared;

        public bool TryBegin(CharacterInstance target) => TryBegin(target, spawnPoint);

        internal bool TryBegin(CharacterInstance target, Transform anchor)
        {
            if (started || !isActiveAndEnabled || !anchor || !target ||
                target.CurrentHealth <= 0 || !NetworkServer.activeHost) return false;

            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) return false;

            boss = Skeleton1HSpawn.TrySpawn(target, anchor.position, Quaternion.LookRotation(forward),
                Skeleton1HSpawn.Kind.NiflheimBossEasy);
            if (!boss) return false;

            started = true;
            boss.Server_OnHealthDepleted += OnBossKilled;
            return true;
        }

        void OnBossKilled(CharacterPart part, AttackType attackType, Health attacker, int attackForce)
        {
            if (!NetworkServer.active || cleared || !boss || boss.CurrentHealth > 0) return;
            cleared = true;
            completed?.Invoke();
        }

        void OnDisable()
        {
            if (!boss) return;
            boss.Server_OnHealthDepleted -= OnBossKilled;
            if (NetworkServer.active) NetworkServer.Destroy(boss.gameObject);
            boss = null;
        }
    }
}
