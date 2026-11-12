using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int GreaterWispKind = 9;
        internal const int GreaterWispHealth = 750;
        internal const float GreaterWispSpawnDuration = 4f;
        internal const float GreaterWispCannonDuration = 2f;
        const float GreaterWispShotSpeed = 76f;
        const float GreaterWispShotLifetime = 5f;
        const float GreaterWispBlastRadius = 7f;
        const int GreaterWispShotCapacity = 4;
        const string GreaterWispResources = "RainActors/RoR2/Base/Characters/GreaterWisp/";
        static GameObject greaterWispGhostPrefab, greaterWispImpactPrefab;
        Transform greaterWispLeft, greaterWispRight;
        GreaterWispShot[] greaterWispShots;
        GameObject[] greaterWispGhosts;
        float nextGreaterWispCannon;

        struct GreaterWispShot
        {
            internal Vector3 Position, Direction;
            internal float Expires;
        }

        internal static bool IsAirborneKind(int archetype) =>
            archetype is WispKind or JellyfishKind or GreaterWispKind or FlyingVerminKind or BellKind;

        void TickGreaterWisp()
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
                if (distance <= 15f || distance > 50f || !visible || now < nextGreaterWispCannon)
                {
                    MoveGreaterWisp(distance);
                    PlayVisual(0);
                    return;
                }
                stage = 1;
                until = now + GreaterWispCannonDuration;
                nextGreaterWispCannon = now + 3f;
                warning.SetActive(true);
                laser.enabled = true;
                laser.widthMultiplier = .08f;
                material.color = Color.red;
                PlayVisual(24);
            }
            if (stage == 1)
            {
                Vector3 direction = distance > .01f ? aim / distance : transform.forward;
                laser.SetPosition(0, origin);
                laser.SetPosition(1, origin + direction * 50f);
                if (now < until) return;
                LaunchGreaterWispShot(greaterWispLeft, .339f, origin - transform.right, direction, now);
                LaunchGreaterWispShot(greaterWispRight, .235f, origin + transform.right, direction, now);
                warning.SetActive(false);
                stage = 2;
                until = now + GreaterWispCannonDuration;
                PlayVisual(25);
                return;
            }
            if (now >= until) stage = 0;
        }

        void MoveGreaterWisp(float distance)
        {
            if (!flyingController || !flyingController.enabled ||
                Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body)) return;

            Vector3 offset = target.transform.position + Vector3.up * WispHoverHeight - transform.position;
            Vector3 horizontal = new Vector3(offset.x, 0f, offset.z).normalized;
            if (distance <= 15f) horizontal = -horizontal;
            Vector3 direction = Vector3.ClampMagnitude(horizontal + Vector3.up * Mathf.Clamp(offset.y, -1f, 1f), 1f);
            flyingController.Move(direction * (7f * Time.deltaTime));
        }

        void LaunchGreaterWispShot(Transform hand, float muzzleOffset, Vector3 fallback, Vector3 direction, float now)
        {
            for (int i = 0; i < greaterWispShots.Length; i++)
            {
                if (greaterWispShots[i].Expires > 0f) continue;
                greaterWispShots[i] = new GreaterWispShot
                {
                    Position = hand ? hand.TransformPoint(0f, muzzleOffset, 0f) : fallback,
                    Direction = direction,
                    Expires = now + GreaterWispShotLifetime
                };
                Vector3 position = greaterWispShots[i].Position;
                if (target)
                {
                    Vector3 aim = target.CombatAimPoint - position;
                    if (aim.sqrMagnitude > .0001f) greaterWispShots[i].Direction = direction = aim.normalized;
                }
                ShowGreaterWispGhost(i, position, direction);
                if (body.RainEnemyKind > 0)
                    body.RainGreaterWispShots.Add(i, new CharacterInstance.RainLinearShotState
                    {
                        Origin = position, Direction = direction, StartedAt = Mirror.NetworkTime.time
                    });
                return;
            }
        }

        void AdvanceGreaterWispShots(float now)
        {
            for (int i = 0; i < greaterWispShots.Length; i++)
            {
                ref GreaterWispShot shot = ref greaterWispShots[i];
                if (shot.Expires <= 0f) continue;
                float travel = GreaterWispShotSpeed * Time.deltaTime;
                var victim = TraceFirstHostile(shot.Position, shot.Direction, .5f, travel, out float hitDistance);
                shot.Position += shot.Direction * hitDistance;
                if (!victim && hitDistance >= travel && now < shot.Expires)
                {
                    if (greaterWispGhosts[i]) greaterWispGhosts[i].transform.position = shot.Position;
                    continue;
                }
                ExplodeGreaterWispShot(shot.Position);
                PlayGreaterWispImpact(shot.Position);
                if (body.RainEnemyKind > 0)
                {
                    body.RainGreaterWispShots.Remove(i);
                    body.RpcRainGreaterWispImpact(shot.Position);
                }
                HideGreaterWispGhost(i);
                shot = default;
            }
        }

        void ConfigureReplicaGreaterWispShots()
        {
            greaterWispGhosts = new GameObject[GreaterWispShotCapacity];
            body.RainGreaterWispShots.OnRemove += RemoveReplicaGreaterWispShot;
            body.RainGreaterWispShots.OnClear += HideGreaterWispGhosts;
        }

        void UpdateReplicaGreaterWispShots()
        {
            double now = Mirror.NetworkTime.time;
            foreach (var entry in body.RainGreaterWispShots)
            {
                var shot = entry.Value;
                float age = Mathf.Max(0f, (float)(now - shot.StartedAt));
                if (age >= GreaterWispShotLifetime) HideGreaterWispGhost(entry.Key);
                else ShowGreaterWispGhost(entry.Key, shot.Origin + shot.Direction * (age * GreaterWispShotSpeed), shot.Direction);
            }
        }

        void ShowGreaterWispGhost(int index, Vector3 position, Vector3 direction)
        {
            if (!greaterWispGhostPrefab)
                greaterWispGhostPrefab = Resources.Load<GameObject>(GreaterWispResources + "WispCannonGhost");
            if (!greaterWispGhostPrefab) return;
            var ghost = greaterWispGhosts[index];
            if (!ghost)
                greaterWispGhosts[index] = Instantiate(greaterWispGhostPrefab, position, Quaternion.LookRotation(direction));
            else if (ghost.activeSelf)
                ghost.transform.position = position;
            else
            {
                ghost.transform.SetPositionAndRotation(position, Quaternion.LookRotation(direction));
                ghost.SetActive(true);
            }
        }

        void RemoveReplicaGreaterWispShot(int index, CharacterInstance.RainLinearShotState _) => HideGreaterWispGhost(index);

        void HideGreaterWispGhost(int index)
        {
            if (greaterWispGhosts[index]) greaterWispGhosts[index].SetActive(false);
        }

        void HideGreaterWispGhosts()
        {
            for (int i = 0; i < greaterWispGhosts.Length; i++) HideGreaterWispGhost(i);
        }

        void ClearGreaterWispShots()
        {
            System.Array.Clear(greaterWispShots, 0, greaterWispShots.Length);
            HideGreaterWispGhosts();
            if (body && body.isServer && body.RainGreaterWispShots.Count > 0) body.RainGreaterWispShots.Clear();
        }

        void DestroyGreaterWispGhosts()
        {
            if (body && !body.isServer)
            {
                body.RainGreaterWispShots.OnRemove -= RemoveReplicaGreaterWispShot;
                body.RainGreaterWispShots.OnClear -= HideGreaterWispGhosts;
            }
            if (greaterWispGhosts == null) return;
            foreach (var ghost in greaterWispGhosts)
                if (ghost) Destroy(ghost);
        }

        internal static void PlayGreaterWispImpact(Vector3 point)
        {
            if (!greaterWispImpactPrefab)
                greaterWispImpactPrefab = Resources.Load<GameObject>(GreaterWispResources + "OmniExplosionVFXGreaterWisp");
            if (!greaterWispImpactPrefab) return;
            var impact = Instantiate(greaterWispImpactPrefab, point, Quaternion.identity);
            impact.transform.localScale *= GreaterWispBlastRadius;
            if (impact.GetComponentInChildren<Light>() is Light flash && flash) Destroy(flash.gameObject, .12f);
            Destroy(impact, 4f);
        }

        void ExplodeGreaterWispShot(Vector3 position)
        {
            attackHits.Clear();
            int count = Physics.OverlapSphereNonAlloc(position, GreaterWispBlastRadius, attackContacts,
                GameManager.characterLayer, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var collider = attackContacts[i];
                attackContacts[i] = null;
                if (!collider) continue;
                var victim = collider.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0) continue;
                Vector3 point = collider.ClosestPoint(position);
                if (Physics.Linecast(position, point, GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                    !attackHits.Add(victim)) continue;
                int damage = Mathf.RoundToInt(45f * (1f - Mathf.Clamp01(Vector3.Distance(position, point) / GreaterWispBlastRadius)));
                if (damage > 0)
                    victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0);
            }
        }
    }
}
