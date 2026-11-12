using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DisallowMultipleComponent]
    public sealed class HyboreaEasyBossEncounter : MonoBehaviour
    {
        const float ReturnFadeDuration = 1f;
        [SerializeField] Transform spawnAnchor;
        [SerializeField] Transform[] teleportHelpers;
        [SerializeField] HyboreaEasySubBossPhase subBossPhase;

        CharacterInstance actor;
        CharacterInstance target;
        Skeleton1HCombat combat;
        Skeleton1HVisual visual;
        int phase;
        bool priorInvulnerability;
        float returnFadeEndsAt;

        public bool Cleared { get; private set; }
        internal int CurrentPhase => phase;

        internal void BindAnchors(Transform spawn, Transform[] helpers, HyboreaEasySubBossPhase subBosses = null)
        {
            spawnAnchor = spawn;
            teleportHelpers = helpers;
            subBossPhase = subBosses;
        }

        public bool TryBegin(CharacterInstance target)
        {
            if (!isActiveAndEnabled || (actor && !Cleared) || !spawnAnchor ||
                teleportHelpers == null || teleportHelpers.Length != 4)
                return false;

            foreach (var helper in teleportHelpers)
                if (!helper) return false;

            var spawned = Skeleton1HSpawn.TrySpawn(target, spawnAnchor.position, spawnAnchor.rotation,
                Skeleton1HSpawn.Kind.HybBossEasy);
            if (!spawned) return false;

            StopEncounter();
            actor = spawned;
            this.target = target;
            actor.Server_OnHealthDepleted += OnActorKilled;
            combat = actor.GetComponent<Skeleton1HCombat>();
            visual = actor.GetComponentInChildren<Skeleton1HVisual>();
            combat.BindHyboreaTeleportHelpers(teleportHelpers);
            return true;
        }

        public bool TryBeginSplit()
        {
            if (!actor || !target || Cleared || !subBossPhase || (phase != 0 && phase != 2)) return false;
            if (!subBossPhase.TryBegin(target, phase + 1, actor.CombatAimPoint)) return false;

            priorInvulnerability = actor.RangeInvulnerable;
            actor.RangeInvulnerable = true;
            combat.SetHyboreaBossSuspended(true);
            phase++;
            return true;
        }

        void Update()
        {
            if (phase != 1 && phase != 3) return;
            if (!actor || !target || target.CurrentHealth <= 0 || !spawnAnchor || !subBossPhase ||
                !subBossPhase.IsRunning)
            {
                StopEncounter();
                return;
            }
            if (!subBossPhase.Cleared) return;
            if (!subBossPhase.ReturnArrived)
            {
                subBossPhase.TryBeginReturn(spawnAnchor);
                return;
            }

            float now = Time.time;
            if (returnFadeEndsAt == 0f)
            {
                var controller = actor.GetComponent<CharacterController>();
                bool restoreController = controller && controller.enabled;
                if (restoreController) controller.enabled = false;
                actor.transform.SetPositionAndRotation(spawnAnchor.position, spawnAnchor.rotation);
                if (restoreController) controller.enabled = true;
                visual.BeginHyboreaReturnFade();
                returnFadeEndsAt = now + ReturnFadeDuration;
                return;
            }
            if (now < returnFadeEndsAt)
            {
                visual.SetHyboreaReturnFade(1f - (returnFadeEndsAt - now) / ReturnFadeDuration);
                return;
            }
            visual.EndHyboreaReturnFade();
            returnFadeEndsAt = 0f;
            phase++;
            combat.SetHyboreaBossSkillSet(phase / 2);
            combat.SetHyboreaBossSuspended(false);
            actor.RangeInvulnerable = priorInvulnerability;
        }

        void OnActorKilled(CharacterPart part, AttackType type, Health attacker, int force) => Cleared = true;

        public void StopEncounter()
        {
            if (subBossPhase) subBossPhase.StopPhase();
            if (actor)
            {
                actor.Server_OnHealthDepleted -= OnActorKilled;
                if (NetworkServer.active) NetworkServer.Destroy(actor.gameObject);
            }
            actor = null;
            target = null;
            combat = null;
            visual = null;
            returnFadeEndsAt = 0f;
            phase = 0;
            Cleared = false;
        }

        void OnDisable() => StopEncounter();
    }
}
