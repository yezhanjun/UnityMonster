using System.Collections.Generic;
using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Whitebox approximation: one cancellable radial blast, reused audio and no-reward self-despawn.
    // The shared Health friendly-fire gate still applies; original enemy-group damage needs integration.
    [RequireComponent(typeof(CharacterInstance), typeof(CharacterMotor))]
    internal sealed class BeetleExplosionCombat : MonoBehaviour
    {
        const float CastSpeed = .75f;
        const float CastRelease = .08489895f / CastSpeed;
        const float BlastDelay = .6f;
        const float SelfDestruct = CastRelease + BlastDelay + .55f;
        const float Radius = 3f;
        const float CastRange = 1.8f;
        // Whitebox tuning, not reconstructed original beetle difficulty scaling.
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
        AudioSource attackAudio;
        GameObject burst;
        Material burstMaterial;
        internal AudioSource AttackAudio => attackAudio;
        internal bool IsCasting => casting;
        internal bool BurstVisible => burst && burst.activeSelf;
        internal float BurstDiameter => burst ? burst.transform.localScale.x : 0f;
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
            if (burst) burst.SetActive(false);
            if (attackAudio) attackAudio.Stop();
            body.SetMovementInput(Vector2.zero);
            visual.Play(Skeleton1HVisual.Motion.Idle);
        }

        internal bool Begin(CharacterInstance victim, Skeleton1HVisual presentation)
        {
            var actor = GetComponent<CharacterInstance>();
            if (body || !isActiveAndEnabled || !actor.isServer || actor.CurrentHealth <= 0 ||
                !victim || victim.CurrentHealth <= 0 || victim.Team == actor.Team || !presentation ||
                !presentation.Play(Skeleton1HVisual.Motion.Spawn)) return false;
            body = actor;
            target = victim;
            visual = presentation;

            originalBlast = Resources.Load<AudioClip>("Jotunnslayer/HorrorAlf/OriginalAlfExplosion");
            attackAudio = gameObject.AddComponent<AudioSource>(); attackAudio.playOnAwake=false; attackAudio.spatialBlend=1f; attackAudio.maxDistance=24f; attackAudio.volume=.75f; attackAudio.clip=originalBlast;
            burst=GameObject.CreatePrimitive(PrimitiveType.Sphere); burst.name="Beetle explosion burst"; burst.layer=2;
            burst.transform.SetParent(transform,false); burst.transform.position=body.CombatAimPoint;
            var collider=burst.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
            burstMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit")); burstMaterial.SetColor("_BaseColor",new Color(1f,.35f,.025f));
            burst.GetComponent<Renderer>().sharedMaterial=burstMaterial; burst.SetActive(false);
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
                if (burst) burst.SetActive(false);
                if (attackAudio) attackAudio.Stop();
                if (warning) warning.enabled = false;
                body.SetMovementInput(Vector2.zero);
                visual.Play(Skeleton1HVisual.Motion.Idle);
                return;
            }
            if (casting)
            {
                // The victim may die in our blast; its death must not cancel our own animation death.
                body.SetMovementInput(Vector2.zero);
                float blastAt = CastRelease + BlastDelay;
                if (!blasted && now >= castStarted + blastAt)
                {
                    blasted = true;
                    if (warning) warning.enabled = false;
                    if (attackAudio && originalBlast) attackAudio.Play();
                    burst.SetActive(true);
                    ResolveBlast();
                }
                // Keep the carrier alive through the delayed hit when the TPS clocks put it after OnExplode.
                if (blasted && burst) burst.transform.localScale = Vector3.one * Mathf.Lerp(.2f, Radius * 2f, Mathf.Clamp01((now-castStarted-blastAt)/.55f));
                float destroyAt = SelfDestruct;
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
            if (!visual.Play(Skeleton1HVisual.Motion.Attack, restart: true, speed: CastSpeed)) return;
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
                    candidate.TryGetComponent<BeetleExplosionCombat>(out _);
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
            if (burst) burst.SetActive(false);
            if (attackAudio) attackAudio.Stop();
            blastTargets.Clear();
            body = null;
            target = null;
        }

        void OnDestroy()
        {
            if (warningMaterial) Destroy(warningMaterial);
            if (burstMaterial) Destroy(burstMaterial);
        }
    }
}
