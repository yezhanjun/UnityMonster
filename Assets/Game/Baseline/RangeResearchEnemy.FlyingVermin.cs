using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int FlyingVerminKind = 13;
        internal const int FlyingVerminHealth = 80;
        internal const float FlyingVerminSpawnDuration = 1.5f;
        internal const float FlyingVerminSpitDuration = 2f;
        internal const byte FlyingVerminSpitAnimation = 36;
        internal const byte FlyingVerminDeathLoopAnimation = 37;
        const float FlyingVerminDeathDuration = .4f;
        const float FlyingVerminSpitSpeed = 100f;
        const float FlyingVerminSpitLifetime = 5f;
        const int FlyingVerminShotCapacity = 3;
        const string FlyingVerminResources = "RainActors/RoR2/DLC1/Characters/FlyingVermin/";
        static GameObject flyingVerminSpitPrefab, flyingVerminImpactPrefab;
        static GameObject flyingVerminDeathPrefab;
        Transform flyingVerminMouth;
        FlyingVerminShot[] flyingVerminShots;
        GameObject[] flyingVerminGhosts;
        float flyingVerminRelease, nextFlyingVerminSpit;
        float flyingVerminDeathAge;

        struct FlyingVerminShot
        {
            internal Vector3 Position, Direction;
            internal float Expires;
        }

        void TickFlyingVermin()
        {
            float now = Time.time;
            Vector3 origin = body.CombatAimPoint;
            Vector3 aim = target.CombatAimPoint - origin;
            float distance = aim.magnitude;
            if (aim.x * aim.x + aim.z * aim.z > .001f)
            {
                float yaw = Mathf.Atan2(aim.x, aim.z) * Mathf.Rad2Deg;
                body.lookInput = new Vector2(0f, yaw);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            bool visible = !Physics.Linecast(origin, origin + aim, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore);
            if (stage == 0)
            {
                if (!visible || distance > 70f || now < nextFlyingVerminSpit)
                {
                    MoveFlyingVermin(visible && distance <= 50f);
                    PlayVisual(0);
                    return;
                }
                stage = 1;
                flyingVerminRelease = now + .35f;
                until = now + FlyingVerminSpitDuration;
                nextFlyingVerminSpit = until + .3f;
                warning.SetActive(true);
                laser.enabled = true;
                laser.widthMultiplier = .045f;
                material.color = Color.red;
                PlayVisual(FlyingVerminSpitAnimation);
            }
            if (stage == 1)
            {
                Vector3 muzzle = flyingVerminMouth
                    ? flyingVerminMouth.TransformPoint(0f, -.192f, .433f) : origin;
                Vector3 direction = distance > .01f ? aim / distance : transform.forward;
                laser.SetPosition(0, muzzle);
                laser.SetPosition(1, muzzle + direction * 70f);
                if (now < flyingVerminRelease) return;
                Vector3 pitchAxis = Vector3.Cross(Vector3.up, direction);
                if (pitchAxis.sqrMagnitude > .001f) direction = Quaternion.AngleAxis(-2f, pitchAxis) * direction;
                LaunchFlyingVerminShot(muzzle, ApplyEmberSpread(direction, 1f), now);
                warning.SetActive(false);
                stage = 2;
            }
            if (now < until) return;
            stage = 0;
            PlayVisual(0);
        }

        void MoveFlyingVermin(bool strafe)
        {
            if (!flyingController || !flyingController.enabled ||
                Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body)) return;

            Vector3 offset = target.transform.position + Vector3.up * WispHoverHeight - transform.position;
            Vector3 horizontal = strafe ? transform.right * wispStrafeDirection :
                new Vector3(offset.x, 0f, offset.z).normalized;
            Vector3 movement = Vector3.ClampMagnitude(horizontal + Vector3.up * Mathf.Clamp(offset.y, -1f, 1f), 1f);
            CollisionFlags contact = flyingController.Move(movement * (6f * Time.deltaTime));
            if (strafe && (contact & CollisionFlags.Sides) != 0) wispStrafeDirection = -wispStrafeDirection;
        }

        void LaunchFlyingVerminShot(Vector3 origin, Vector3 direction, float now)
        {
            for (int i = 0; i < flyingVerminShots.Length; i++)
            {
                if (flyingVerminShots[i].Expires > 0f) continue;
                flyingVerminShots[i] = new FlyingVerminShot
                {
                    Position = origin, Direction = direction, Expires = now + FlyingVerminSpitLifetime
                };
                ShowFlyingVerminSpit(i, origin, direction);
                if (body.RainEnemyKind > 0)
                    body.RainFlyingVerminShots.Add(i, new CharacterInstance.RainLinearShotState
                    {
                        Origin = origin, Direction = direction, StartedAt = Mirror.NetworkTime.time
                    });
                return;
            }
        }

        void AdvanceFlyingVerminShots(float now)
        {
            for (int i = 0; i < flyingVerminShots.Length; i++)
            {
                ref FlyingVerminShot shot = ref flyingVerminShots[i];
                if (shot.Expires <= 0f) continue;
                if (now >= shot.Expires)
                {
                    FinishFlyingVerminShot(i);
                    continue;
                }
                float travel = FlyingVerminSpitSpeed * Time.deltaTime;
                var victim = TraceFirstHostile(shot.Position, shot.Direction, 1f, travel, out float hitDistance,
                    out var impact);
                shot.Position += shot.Direction * hitDistance;
                if (!victim && hitDistance >= travel)
                {
                    if (flyingVerminGhosts[i]) flyingVerminGhosts[i].transform.position = shot.Position;
                    continue;
                }
                if (victim)
                    victim.Server_ChangeHealthStateRaw(30, CharacterPart.body, AttackType.hitscan, body, 0);
                PlayFlyingVerminImpact(impact.point, impact.normal);
                if (body.RainEnemyKind > 0) body.RpcRainFlyingVerminImpact(impact.point, impact.normal);
                FinishFlyingVerminShot(i);
            }
        }

        void FinishFlyingVerminShot(int index)
        {
            flyingVerminShots[index] = default;
            HideFlyingVerminSpit(index);
            if (body.RainEnemyKind > 0) body.RainFlyingVerminShots.Remove(index);
        }

        void ConfigureReplicaFlyingVerminSpit()
        {
            flyingVerminGhosts = new GameObject[FlyingVerminShotCapacity];
            body.RainFlyingVerminShots.OnRemove += RemoveReplicaFlyingVerminSpit;
            body.RainFlyingVerminShots.OnClear += HideFlyingVerminSpit;
        }

        void UpdateReplicaFlyingVerminSpit()
        {
            double now = Mirror.NetworkTime.time;
            foreach (var entry in body.RainFlyingVerminShots)
            {
                var shot = entry.Value;
                float age = Mathf.Max(0f, (float)(now - shot.StartedAt));
                if (age >= FlyingVerminSpitLifetime) HideFlyingVerminSpit(entry.Key);
                else ShowFlyingVerminSpit(entry.Key, shot.Origin + shot.Direction * (FlyingVerminSpitSpeed * age), shot.Direction);
            }
        }

        void ShowFlyingVerminSpit(int index, Vector3 position, Vector3 direction)
        {
            if (!flyingVerminSpitPrefab)
                flyingVerminSpitPrefab = Resources.Load<GameObject>(FlyingVerminResources + "VerminSpitGhost");
            if (!flyingVerminSpitPrefab) return;
            var ghost = flyingVerminGhosts[index];
            if (!ghost)
                flyingVerminGhosts[index] = Instantiate(flyingVerminSpitPrefab, position, Quaternion.LookRotation(direction));
            else if (ghost.activeSelf)
                ghost.transform.position = position;
            else
            {
                ghost.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
                ghost.SetActive(true);
            }
        }

        void RemoveReplicaFlyingVerminSpit(int index, CharacterInstance.RainLinearShotState _) =>
            HideFlyingVerminSpit(index);

        void HideFlyingVerminSpit(int index)
        {
            if (flyingVerminGhosts[index]) flyingVerminGhosts[index].SetActive(false);
        }

        void HideFlyingVerminSpit()
        {
            for (int i = 0; i < flyingVerminGhosts.Length; i++) HideFlyingVerminSpit(i);
        }

        void ClearFlyingVerminShots()
        {
            System.Array.Clear(flyingVerminShots, 0, flyingVerminShots.Length);
            HideFlyingVerminSpit();
            if (body && body.isServer && body.RainFlyingVerminShots.Count > 0) body.RainFlyingVerminShots.Clear();
        }

        void DestroyFlyingVerminSpit()
        {
            if (body && !body.isServer)
            {
                body.RainFlyingVerminShots.OnRemove -= RemoveReplicaFlyingVerminSpit;
                body.RainFlyingVerminShots.OnClear -= HideFlyingVerminSpit;
            }
            if (flyingVerminGhosts == null) return;
            foreach (var ghost in flyingVerminGhosts)
                if (ghost) Destroy(ghost);
        }

        internal static void PlayFlyingVerminImpact(Vector3 point, Vector3 normal)
        {
            if (!flyingVerminImpactPrefab)
                flyingVerminImpactPrefab = Resources.Load<GameObject>(FlyingVerminResources + "VerminSpitImpactEffect");
            if (!flyingVerminImpactPrefab) return;
            var effect = Instantiate(flyingVerminImpactPrefab, point, Quaternion.LookRotation(normal));
            var flash = effect.GetComponentInChildren<Light>();
            if (flash) Destroy(flash.gameObject, .12f);
            Destroy(effect, 3f);
        }

        void TickFlyingVerminDeath()
        {
            float now = Time.time;
            if (despawnAt == 0f)
            {
                SetCombat(false);
                despawnAt = now + FlyingVerminDeathDuration;
                flyingVerminDeathAge = 0f;
                PlayVisual(DeathAnimation);
                return;
            }

            float age = Mathf.Clamp(FlyingVerminDeathDuration - (despawnAt - now), 0f, FlyingVerminDeathDuration);
            float delta = (age - flyingVerminDeathAge) * (6f + .5f * Physics.gravity.y * (age + flyingVerminDeathAge));
            flyingVerminDeathAge = age;
            if (flyingController && flyingController.enabled)
                flyingController.Move(Vector3.up * delta);
            if (age >= .2f) PlayVisual(FlyingVerminDeathLoopAnimation);
            if (now < despawnAt) return;

            Vector3 point = body.CombatAimPoint;
            PlayFlyingVerminDeath(point);
            if (body.RainEnemyKind > 0) body.RpcRainFlyingVerminDeath(point);
            RemoveFromRange();
        }

        internal static void PlayFlyingVerminDeath(Vector3 point)
        {
            if (!flyingVerminDeathPrefab)
                flyingVerminDeathPrefab = Resources.Load<GameObject>(FlyingVerminResources + "VerminDeathExplosion");
            if (!flyingVerminDeathPrefab) return;
            var rotation = Quaternion.FromToRotation(Vector3.forward, Vector3.up);
            var effect = Instantiate(flyingVerminDeathPrefab, point, rotation);
            var flash = effect.GetComponentInChildren<Light>();
            if (flash) Destroy(flash.gameObject, .15f);
            Destroy(effect, 3f);
        }
    }
}
