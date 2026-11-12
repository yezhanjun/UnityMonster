using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Local-host adapter. Difficulty scaling, object destruction and original FX remain unverified.
    // The shared Health friendly-fire gate still applies; original enemy-group damage needs integration.
    [RequireComponent(typeof(CharacterInstance), typeof(CharacterMotor))]
    internal sealed class HorrorJotunCombat : MonoBehaviour
    {
        const float CastSpeed = .75f;
        const float CastRelease = .08489895f / CastSpeed;
        const float BlastDelay = .6f;
        const float SelfDestruct = .6666292f / CastSpeed;
        const float Radius = 3f;
        const float CastRange = 1.8f;
        // 36 * 1.5 without additional RPG or encounter modifiers; not final original difficulty HP.
        internal const int LocalBaseHealth = 54;
        // RPGConstants category 8 (Damage100Percent), level 1; encounter level scaling is pending.
        const float BaseAttackPower = 20f;
        readonly List<CharacterInstance> blastTargets = new List<CharacterInstance>(32);
        CharacterInstance body;
        CharacterInstance target;
        Skeleton1HVisual visual;
        float spawnEnds;
        float castStarted;
        bool casting;
        bool blasted;
        bool gmWaiting;
        bool alfheim;
        LineRenderer warning;
        Material warningMaterial;
        AudioClip originalBlast;

        internal void SetGmCombat(bool active)
        {
            if (!body || !body.isServer) return;
            gmWaiting = !active;
            if (active) return;
            casting = blasted = false;
            if (warning) warning.enabled = false;
            body.SetMovementInput(Vector2.zero);
            visual.Play(Skeleton1HVisual.Motion.Idle);
        }

        internal bool Begin(CharacterInstance victim, Skeleton1HVisual presentation, bool alfheimVariant = false)
        {
            var actor = GetComponent<CharacterInstance>();
            if (body || !isActiveAndEnabled || !actor.isServer || actor.CurrentHealth <= 0 ||
                !victim || victim.CurrentHealth <= 0 || victim.Team == actor.Team || !presentation ||
                !presentation.Play(Skeleton1HVisual.Motion.Spawn)) return false;
            body = actor;
            target = victim;
            visual = presentation;
            alfheim = alfheimVariant;
            originalBlast = Resources.Load<AudioClip>("Jotunnslayer/HorrorAlf/OriginalAlfExplosion");
            if (TryGetComponent<BotAI>(out var bot))
            {
                bot.enabled = false;
                bot.StopAllCoroutines();
            }
            var motor = GetComponent<CharacterMotor>();
            motor.WalkSpeed = motor.RunSpeed = 5.7f;
            body.MaxHealth = body.CurrentHealth = LocalBaseHealth;
            body.SetMovementInput(Vector2.zero);
            spawnEnds = Time.time + visual.SpawnDuration;
            return true;
        }

        void Update()
        {
            if (!body || !body.isServer) return;
            if (body.CurrentHealth <= 0)
            {
                enabled = false;
                return;
            }
            if (gmWaiting) return;
            float now = Time.time;
            if (body.Block || SkillBattlePreview.IsStoppedByRaidControl(body, now))
            {
                // A stopped cast cannot keep its delayed blast or animation death alive.
                // Releasing control starts a fresh action rather than resuming expired timers.
                casting = false;
                blasted = false;
                if (warning) warning.enabled = false;
                body.SetMovementInput(Vector2.zero);
                visual.Play(Skeleton1HVisual.Motion.Idle);
                return;
            }
            if (casting)
            {
                // The victim may die in our blast; its death must not cancel our own animation death.
                body.SetMovementInput(Vector2.zero);
                float blastAt = (alfheim ? .12783404f / 2f : CastRelease) + BlastDelay;
                if (!blasted && now >= castStarted + blastAt)
                {
                    blasted = true;
                    if (warning) warning.enabled = false;
                    if (originalBlast) AudioSource.PlayClipAtPoint(originalBlast, body.CombatAimPoint, .75f);
                    ResolveBlast();
                }
                // Keep the carrier alive through the delayed hit when the TPS clocks put it after OnExplode.
                float destroyAt = alfheim ? Mathf.Max(1.285815f / 2f, blastAt) : SelfDestruct;
                if (body && now >= castStarted + destroyAt)
                {
                    // Explicit no-reward despawn. Original corpse/DoExplode FX still need restoration.
                    NetworkServer.Destroy(gameObject);
                }
                return;
            }
            if (!target || target.CurrentHealth <= 0 || target.Team == body.Team)
            {
                body.SetMovementInput(Vector2.zero);
                return;
            }
            if (now < spawnEnds) return;
            Vector3 offset = target.transform.position - body.transform.position;
            if (offset.x * offset.x + offset.z * offset.z > .0001f)
                body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            if (offset.sqrMagnitude > CastRange * CastRange)
            {
                body.SetMovementInput(Vector2.up);
                visual.Play(Skeleton1HVisual.Motion.Walk, speed: 2f);
                return;
            }
            body.SetMovementInput(Vector2.zero);
            if (!visual.Play(Skeleton1HVisual.Motion.Attack, restart: true, speed: alfheim ? 2f : CastSpeed)) return;
            castStarted = now;
            casting = true;
            ShowWarning();
        }

        void ShowWarning()
        {
            if (!warning)
            {
                var shader = Shader.Find("Sprites/Default");
                if (!shader) return;
                warningMaterial = new Material(shader);
                var indicator = new GameObject("Alfheim explosion warning");
                indicator.transform.SetParent(transform);
                warning = indicator.AddComponent<LineRenderer>();
                warning.sharedMaterial = warningMaterial;
                warning.loop = true;
                warning.positionCount = 32;
                warning.widthMultiplier = .1f;
                warning.startColor = warning.endColor = new Color(1f, .25f, .07f);
            }
            Vector3 center = transform.position + Vector3.up * .07f;
            for (int i = 0; i < warning.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / warning.positionCount);
                warning.SetPosition(i, center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Radius);
            }
            warning.enabled = true;
        }

        void ResolveBlast()
        {
            // Snapshot before damage callbacks, which may remove network identities synchronously.
            blastTargets.Clear();
            Vector3 center = body.CombatAimPoint;
            foreach (var identity in NetworkServer.spawned.Values)
            {
                if (!identity || !identity.TryGetComponent<CharacterInstance>(out var candidate) ||
                    candidate == body || candidate.CurrentHealth <= 0 ||
                    (candidate.CombatAimPoint - center).sqrMagnitude > Radius * Radius) continue;
                bool enemyActor = candidate.TryGetComponent<Skeleton1HCombat>(out _) ||
                    candidate.TryGetComponent<MonsterPassThrough>(out _) ||
                    candidate.TryGetComponent<HorrorJotunCombat>(out _);
                if (candidate.Team == body.Team && !enemyActor) continue;
                blastTargets.Add(candidate);
            }
            for (int i = 0; i < blastTargets.Count; i++)
            {
                if (!body || body.CurrentHealth <= 0) break;
                var candidate = blastTargets[i];
                if (!candidate || candidate.CurrentHealth <= 0) continue;
                bool allied = candidate.Team == body.Team;
                float percent = allied ? Random.Range(9f, 10f) : Random.Range(95f, 105f);
                int amount = Mathf.Max(1, Mathf.RoundToInt(BaseAttackPower * percent * .01f));
                candidate.Server_ChangeHealthStateRaw(amount, CharacterPart.body, AttackType.explosion, body, 0);
            }
            blastTargets.Clear();
        }

        void OnDisable()
        {
            if (body && body.isServer) body.SetMovementInput(Vector2.zero);
            casting = false;
            blasted = false;
            if (warning) warning.enabled = false;
            blastTargets.Clear();
            body = null;
            target = null;
        }

        void OnDestroy()
        {
            if (warningMaterial) Destroy(warningMaterial);
        }
    }
}
