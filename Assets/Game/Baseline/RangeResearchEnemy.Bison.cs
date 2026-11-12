using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int BisonKind = 11;
        internal const int BisonHealth = 480;
        internal const float BisonSpawnDuration = 2f;
        internal const float BisonHeadbuttDuration = 2.5f;
        internal const byte BisonHeadbuttAnimation = 29;
        internal const byte BisonChargePrepAnimation = 30, BisonChargeAnimation = 31, BisonStunAnimation = 32;
        internal const float BisonChargePrepDuration = 1.5f;
        const float BisonChargeSpeed = 24f;
        static readonly Vector3 BisonHeadbuttOffset = new Vector3(-.001f, .379f, .354f);
        static readonly Vector3 BisonHeadbuttHalfSize = new Vector3(1.1500031f, .95000095f, 1.118191f);
        static readonly Vector3 BisonWarningScale = new Vector3(2.3f, .08f, 2.24f);
        Transform bisonHead, bisonModel, bisonRoot;
        static readonly float[] BisonChargeFootstepTimes = { .008635579f, .119170986f, .41968912f, .6079447f };
        static GameObject bisonChargeEffectPrefab, bisonChargeStepPrefab;
        Transform[] bisonChargeFeet;
        float previousBisonChargeAge = -1f;
        GameObject bisonChargeEffect;
        float bisonNextHeadbutt, bisonHeadbuttStarted, bisonNextCharge;
        float bisonPreChargeWalkSpeed, bisonPreChargeRunSpeed;
        bool bisonChargeSpeedApplied;
        bool bisonSampledHeadbutt;

        void TickBison()
        {
            float now = Time.time;
            if (stage >= 2)
            {
                TickBisonCharge(now);
                return;
            }
            if (stage == 0)
            {
                Vector3 offset = target.transform.position - transform.position;
                if (offset.x * offset.x + offset.z * offset.z > .001f)
                    body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
                if (now < bisonNextHeadbutt || offset.sqrMagnitude > 49f)
                {
                    bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                    bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                        Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
                    if (!rooted && visible && offset.sqrMagnitude >= 49f && offset.sqrMagnitude <= 3600f &&
                        now >= bisonNextCharge)
                    {
                        StartBisonCharge(now);
                        return;
                    }
                    bool moving = !rooted && offset.sqrMagnitude > 1f && SteerChase(visible, false, now);
                    body.SetMovementInput(moving ? Vector2.up : Vector2.zero);
                    PlayVisual(moving ? (byte)1 : (byte)0);
                    return;
                }
                stage = 1;
                bisonHeadbuttStarted = now;
                until = now + BisonHeadbuttDuration;
                bisonNextHeadbutt = now + 3f;
                bisonSampledHeadbutt = false;
                attackHits.Clear();
                body.SetMovementInput(Vector2.zero);
                headbuttForward = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.forward;
                warning.transform.SetPositionAndRotation(transform.position + headbuttForward * 1.3f + Vector3.up * .06f,
                    Quaternion.LookRotation(headbuttForward));
                warning.transform.localScale = BisonWarningScale;
                warning.GetComponent<MeshRenderer>().enabled = true;
                laser.enabled = false;
                material.color = Color.red;
                warning.SetActive(true);
                PlayVisual(BisonHeadbuttAnimation);
            }

            float clipTime = (now - bisonHeadbuttStarted) * (2.0833335f / BisonHeadbuttDuration);
            if (clipTime >= .0879869f && (clipTime <= .29821026f || !bisonSampledHeadbutt))
            {
                bisonSampledHeadbutt = true;
                StrikeBisonHeadbutt();
            }
            if (clipTime >= .29821026f) warning.SetActive(false);
            if (now < until) return;
            stage = 0;
            PlayVisual(0);
        }

        void StartBisonCharge(float now)
        {
            stage = 2;
            until = now + BisonChargePrepDuration;
            bisonNextCharge = now + 8f;
            body.SetMovementInput(Vector2.zero);
            material.color = Color.red;
            warning.GetComponent<MeshRenderer>().enabled = false;
            laser.enabled = true;
            laser.widthMultiplier = .08f;
            warning.SetActive(true);
            UpdateBisonChargeWarning();
            PlayVisual(BisonChargePrepAnimation);
        }

        void UpdateBisonChargeWarning()
        {
            Vector3 origin = body.CombatAimPoint;
            Vector3 direction = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.forward;
            laser.SetPosition(0, origin);
            laser.SetPosition(1, origin + direction * 60f);
        }

        void UpdateBisonChargeEffect()
        {
            bool charging = body && body.CurrentHealth > 0 &&
                (body.isServer ? stage == 2 : body.RainEnemyAnimation == BisonChargePrepAnimation);
            if (!charging)
            {
                if (bisonChargeEffect)
                {
                    bisonChargeEffect.SetActive(false);
                    Destroy(bisonChargeEffect);
                    bisonChargeEffect = null;
                }
                return;
            }
            if (bisonChargeEffect || !bisonRoot) return;
            float age = Mathf.Max(0f, (float)(Mirror.NetworkTime.time - body.RainEnemyAnimationStartedAt));
            if (!body.isServer && age >= BisonChargePrepDuration) return;
            if (!bisonChargeEffectPrefab)
                bisonChargeEffectPrefab = Resources.Load<GameObject>(
                    "RainActors/RoR2/Base/Characters/Bison/ChargeBisonCharge");
            if (!bisonChargeEffectPrefab) return;

            bisonChargeEffect = Instantiate(bisonChargeEffectPrefab, bisonRoot, false);
            bisonChargeEffect.transform.SetLocalPositionAndRotation(new Vector3(0f, 1.696f, 2.329f), Quaternion.identity);
            if (!body.isServer && age > .05f)
            {
                foreach (var particles in bisonChargeEffect.GetComponentsInChildren<ParticleSystem>())
                {
                    particles.Simulate(age, false, true);
                    particles.Play(false);
                }
            }
        }

        void UpdateBisonChargeFootsteps()
        {
            if (!body || body.CurrentHealth <= 0 || body.RainEnemyAnimation != BisonChargeAnimation)
            {
                previousBisonChargeAge = -1f;
                return;
            }
            if (bisonChargeFeet == null) return;
            float age = Mathf.Max(0f, (float)(Mirror.NetworkTime.time - body.RainEnemyAnimationStartedAt));
            float previous = previousBisonChargeAge;
            previousBisonChargeAge = age;
            if (previous < 0f)
            {
                if (age > .1f) return;
                previous = 0f;
            }
            for (int i = 0; i < BisonChargeFootstepTimes.Length; i++)
            {
                float step = BisonChargeFootstepTimes[i];
                var foot = bisonChargeFeet[i];
                if (!foot || Mathf.FloorToInt(age - step) <= Mathf.FloorToInt(previous - step) ||
                    !Physics.Raycast(foot.position + Vector3.up * 1.5f, Vector3.down, out var ground, 4f,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;
                if (!bisonChargeStepPrefab)
                    bisonChargeStepPrefab = Resources.Load<GameObject>(
                        "RainActors/RoR2/Base/Characters/Bison/BisonChargeStep");
                if (bisonChargeStepPrefab)
                    Destroy(Instantiate(bisonChargeStepPrefab, ground.point, Quaternion.LookRotation(ground.normal)), 2f);
            }
        }

        void TickBisonCharge(float now)
        {
            if (stage == 4)
            {
                if (now >= until) FinishBisonCharge();
                return;
            }
            if (stage == 2)
            {
                UpdateBisonChargeWarning();
                if (now < until) return;
                warning.SetActive(false);
                attackHits.Clear();
                stage = 3;
                until = now + 4f;
                if (configuredMotor)
                {
                    bisonPreChargeWalkSpeed = configuredMotor.WalkSpeed;
                    bisonPreChargeRunSpeed = configuredMotor.RunSpeed;
                    configuredMotor.WalkSpeed = configuredMotor.RunSpeed = BisonChargeSpeed;
                    bisonChargeSpeedApplied = true;
                }
                PlayVisual(BisonChargeAnimation);
            }

            if (now >= until || Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body))
            {
                FinishBisonCharge();
                return;
            }
            Vector3 offset = target.transform.position - transform.position;
            if (offset.x * offset.x + offset.z * offset.z > .001f)
            {
                float desired = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
                body.lookInput = new Vector2(0f, Mathf.MoveTowardsAngle(body.lookInput.y, desired, 300f * Time.deltaTime));
            }
            Vector3 forward = Quaternion.Euler(0f, body.lookInput.y, 0f) * Vector3.forward;
            if (bisonModel)
                StrikeBisonBox(bisonModel.TransformPoint(0f, 1.41f, 1.58f),
                    Vector3.Scale(Vector3.one * 1.144f, bisonModel.lossyScale), bisonModel.rotation);

            Vector3 probe = bisonRoot ? bisonRoot.TransformPoint(0f, 2.334f, 2.186f) : body.CombatAimPoint + forward * 2.186f;
            if (Physics.CheckSphere(probe, .2f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                Physics.SphereCast(probe, .2f, forward, out _, BisonChargeSpeed * Time.deltaTime,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
            {
                RestoreBisonChargeSpeed();
                body.SetMovementInput(Vector2.zero);
                stage = 4;
                until = now + 3f;
                PlayVisual(BisonStunAnimation);
                return;
            }
            body.SetMovementInput(Vector2.up);
        }

        void FinishBisonCharge()
        {
            RestoreBisonChargeSpeed();
            body.SetMovementInput(Vector2.zero);
            warning.SetActive(false);
            stage = 0;
            PlayVisual(0);
        }

        void RestoreBisonChargeSpeed()
        {
            if (!bisonChargeSpeedApplied) return;
            bisonChargeSpeedApplied = false;
            if (!configuredMotor) return;
            configuredMotor.WalkSpeed = bisonPreChargeWalkSpeed;
            configuredMotor.RunSpeed = bisonPreChargeRunSpeed;
        }

        void StrikeBisonHeadbutt()
        {
            if (!bisonHead) return;
            StrikeBisonBox(bisonHead.TransformPoint(BisonHeadbuttOffset),
                Vector3.Scale(BisonHeadbuttHalfSize, bisonHead.lossyScale), bisonHead.rotation);
        }

        void StrikeBisonBox(Vector3 center, Vector3 halfSize, Quaternion rotation)
        {
            int count = Physics.OverlapBoxNonAlloc(center, halfSize, attackContacts, rotation,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var contact = attackContacts[i];
                attackContacts[i] = null;
                if (!contact) continue;
                var victim = contact.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                    !attackHits.Add(victim)) continue;
                victim.Server_ChangeHealthStateRaw(36, CharacterPart.body, AttackType.melee, body, 0);
            }
        }
    }
}
