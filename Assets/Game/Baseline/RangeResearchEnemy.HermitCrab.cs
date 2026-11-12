using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int HermitCrabKind = 14;
        internal const int HermitCrabHealth = 100;
        internal const float HermitCrabSpawnDuration = 1.3f;
        internal const float HermitBurrowInDuration = 1f;
        internal const float HermitBurrowOutDuration = 1.3f;
        internal const byte HermitBurrowInAnimation = 38;
        internal const byte HermitBurrowedAnimation = 39;
        internal const byte HermitBurrowOutAnimation = 40;
        internal const byte HermitFireAnimation = 41;
        const float HermitMortarFlightTime = 4f;
        const float HermitMortarLifetime = 5f;
        const float HermitBlastRadius = 7f;
        const float HermitMortarSpinDegreesPerSecond = 720f;
        const string HermitResourceFolder = "RainActors/RoR2/Base/Characters/HermitCrab/";
        static readonly Vector3 HermitWarningScale = new Vector3(14f, .08f, 14f);

        struct HermitMortar
        {
            internal int Id;
            internal Vector3 Origin, Velocity, Landing;
            internal float StartedAt, Age;
        }

        List<HermitMortar> hermitMortars;
        Dictionary<Health, float> hermitBlastDistances;
        Dictionary<int, GameObject> hermitMortarVisuals;
        int hermitMortarSequence;

        void ConfigureHermitMortarVisuals()
        {
            hermitMortarVisuals = new Dictionary<int, GameObject>(2);
            body.RainHermitMortars.OnRemove += RemoveHermitMortarVisual;
            body.RainHermitMortars.OnClear += ClearHermitMortarVisuals;
        }

        void TickHermitCrab()
        {
            float now = Time.time;
            Vector3 offset = target.transform.position - transform.position;
            float distance = offset.magnitude;
            bool flee = distance < 20f;
            bool fireRange = distance >= 20f && distance <= 120f;
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);

            body.SetMovementInput(Vector2.zero);
            if (stage == 0)
            {
                bool visible = !Physics.Linecast(body.CombatAimPoint, target.CombatAimPoint,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                if (fireRange && visible)
                {
                    stage = 1;
                    until = now + HermitBurrowInDuration;
                    PlayVisual(HermitBurrowInAnimation);
                    return;
                }

                bool moving = false;
                if (!rooted && flee)
                {
                    body.lookInput = new Vector2(0f, Mathf.Atan2(-offset.x, -offset.z) * Mathf.Rad2Deg);
                    moving = true;
                }
                else if (!rooted) moving = SteerChase(visible, false, now);
                if (moving) body.SetMovementInput(Vector2.up);
                PlayVisual(moving ? (byte)1 : (byte)0);
                return;
            }
            if (now < until && stage != 2) return;
            if (stage == 4)
            {
                stage = 0;
                PlayVisual(0);
                return;
            }
            if (stage == 1 || stage == 3)
            {
                until = stage == 3 ? until + 3f : now;
                stage = 2;
                PlayVisual(HermitBurrowedAnimation);
            }
            if (!fireRange)
            {
                stage = 4;
                until = now + HermitBurrowOutDuration;
                PlayVisual(HermitBurrowOutAnimation);
                return;
            }
            if (now < until) return;

            stage = 3;
            until = now + 1f;
            body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            PlayVisual(HermitFireAnimation);
            LaunchHermitMortar(now);
        }

        void LaunchHermitMortar(float now)
        {
            Vector3 origin = body.CombatAimPoint;
            Vector3 aimDirection = (target.CombatAimPoint - origin).normalized;
            Vector3 velocity = Vector3.up * 50f;
            if (TryFindHermitMortarAim(origin, aimDirection, out var aimPoint))
            {
                Vector3 offset = aimPoint - origin;
                Vector3 planar = new Vector3(offset.x, 0f, offset.z);
                velocity = planar.normalized * (Mathf.Max(25f, planar.magnitude) / HermitMortarFlightTime);
                velocity.y = offset.y / HermitMortarFlightTime - RainOriginalGravityY * HermitMortarFlightTime * .5f;
            }
            Vector3 landing = HermitMortarPosition(origin, velocity, HermitMortarFlightTime);
            if (Physics.Raycast(landing + Vector3.up, Vector3.down, out var ground, 6f,
                GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) landing = ground.point;
            int id = ++hermitMortarSequence;
            hermitMortars.Add(new HermitMortar
            {
                Id = id, Origin = origin, Velocity = velocity, StartedAt = now, Landing = landing
            });
            body.RainHermitMortars.Add(id, new CharacterInstance.RainHermitMortarState
            {
                Origin = origin, Velocity = velocity, StartedAt = Mirror.NetworkTime.time
            });

            RefreshHermitMortarWarning();
        }

        void RefreshHermitMortarWarning()
        {
            bool active = hermitMortars.Count != 0;
            warning.SetActive(active);
            if (!active) return;
            warning.transform.SetPositionAndRotation(
                hermitMortars[0].Landing + Vector3.up * .06f, Quaternion.identity);
            warning.transform.localScale = HermitWarningScale;
            warning.GetComponent<MeshRenderer>().enabled = true;
            laser.enabled = false;
            material.color = Color.red;
        }

        bool TryFindHermitMortarAim(Vector3 origin, Vector3 direction, out Vector3 point)
        {
            float bestDot = float.NegativeInfinity;
            point = default;
            foreach (var identity in Mirror.NetworkServer.spawned.Values)
            {
                if (!identity || !identity.TryGetComponent<CharacterInstance>(out var candidate) ||
                    candidate.Team == body.Team || candidate.CurrentHealth <= 0) continue;
                Vector3 aim = candidate.CombatAimPoint;
                Vector3 offset = aim - origin;
                if (offset.sqrMagnitude < .0001f) continue;
                float dot = Vector3.Dot(direction, offset.normalized);
                if (dot <= bestDot) continue;
                bestDot = dot;
                point = aim;
            }
            if (bestDot > float.NegativeInfinity) return true;
            if (!Physics.Raycast(origin, direction, out var hit, 1000f, GameManager.characterLayer,
                QueryTriggerInteraction.Ignore)) return false;
            point = hit.point;
            return true;
        }

        static Vector3 HermitMortarPosition(Vector3 origin, Vector3 velocity, float age) =>
            origin + velocity * age + Vector3.up * (.5f * RainOriginalGravityY * age * age);

        void AdvanceHermitMortars(float now)
        {
            int countBefore = hermitMortars.Count;
            for (int i = hermitMortars.Count - 1; i >= 0 && i < hermitMortars.Count; i--)
            {
                var mortar = hermitMortars[i];
                float age = Mathf.Min(now - mortar.StartedAt, HermitMortarLifetime);
                Vector3 start = HermitMortarPosition(mortar.Origin, mortar.Velocity, mortar.Age);
                Vector3 end = HermitMortarPosition(mortar.Origin, mortar.Velocity, age);
                Vector3 delta = end - start;
                float distance = delta.magnitude;
                RaycastHit impact = default;
                if (distance > .0001f)
                    TraceFirstHostile(start, delta / distance, .5f, distance, out _, out impact);
                if (!impact.collider && age < HermitMortarLifetime)
                {
                    mortar.Age = age;
                    hermitMortars[i] = mortar;
                    continue;
                }

                Vector3 center = impact.collider ? start + delta * (impact.distance / distance) : end;
                Vector3 normal = impact.collider ? impact.normal : Vector3.up;
                hermitMortars.RemoveAt(i);
                body.RainHermitMortars.Remove(mortar.Id);
                PlayHermitMortarImpact(center, normal);
                if (body.RainEnemyKind > 0) body.RpcRainHermitMortarImpact(center, normal);
                DetonateHermitMortar(center);
            }
            if (hermitMortars.Count != countBefore) RefreshHermitMortarWarning();
            UpdateHermitMortarVisuals();
        }

        void UpdateHermitMortarVisuals()
        {
            double now = Mirror.NetworkTime.time;
            foreach (var entry in body.RainHermitMortars)
            {
                var shot = entry.Value;
                float age = Mathf.Max(0f, (float)(now - shot.StartedAt));
                if (age >= HermitMortarLifetime)
                {
                    RemoveHermitMortarVisual(entry.Key, shot);
                    continue;
                }
                Vector3 position = HermitMortarPosition(shot.Origin, shot.Velocity, age);
                if (!hermitMortarVisuals.TryGetValue(entry.Key, out var visual))
                {
                    visual = CreateHermitMortarVisual(position);
                    hermitMortarVisuals.Add(entry.Key, visual);
                }
                visual.transform.SetPositionAndRotation(position,
                    Quaternion.AngleAxis(age * HermitMortarSpinDegreesPerSecond, Vector3.up));
            }
        }

        static GameObject CreateHermitMortarVisual(Vector3 position)
        {
            var visual = new GameObject("Rain Hermit Crab mortar");
            visual.transform.position = position;
            var shell = new GameObject("Shell");
            shell.transform.SetParent(visual.transform, false);
            shell.transform.localScale = Vector3.one * .29080145f;
            shell.AddComponent<MeshFilter>().sharedMesh =
                Resources.Load<Mesh>(HermitResourceFolder + "HermitCrabMortarShell");
            shell.AddComponent<MeshRenderer>().sharedMaterial =
                Resources.Load<Material>(HermitResourceFolder + "matHermitCrabProjectile");

            var trail = visual.AddComponent<TrailRenderer>();
            trail.sharedMaterial = new Material(Shader.Find("Sprites/Default"));
            trail.time = 1f;
            trail.minVertexDistance = .1f;
            trail.startWidth = trail.endWidth = 1f;
            trail.textureMode = LineTextureMode.Tile;
            trail.startColor = new Color(.43543f, .85355f, .89623f, 1f);
            trail.endColor = new Color(.43543f, .85355f, .89623f, 0f);
            return visual;
        }

        void RemoveHermitMortarVisual(int id, CharacterInstance.RainHermitMortarState _)
        {
            if (hermitMortarVisuals.Remove(id, out var visual))
                DestroyHermitMortarVisual(visual);
        }

        void ClearHermitMortarVisuals()
        {
            foreach (var visual in hermitMortarVisuals.Values) DestroyHermitMortarVisual(visual);
            hermitMortarVisuals.Clear();
        }

        static void DestroyHermitMortarVisual(GameObject visual)
        {
            if (!visual) return;
            visual.SetActive(false);
            Destroy(visual.GetComponent<TrailRenderer>().sharedMaterial);
            Destroy(visual);
        }

        internal static void PlayHermitMortarImpact(Vector3 point, Vector3 normal)
        {
            var effect = new GameObject("Rain Hermit Crab mortar impact");
            effect.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.forward, normal));
            EmitHermitImpactParticles(effect.transform, "Water", new Color(.44f, .85f, .9f, .6f),
                new ParticleSystem.MinMaxCurve(.3f, 1f), new ParticleSystem.MinMaxCurve(3f, 4f),
                new ParticleSystem.MinMaxCurve(2f, 3f), 5);
            EmitHermitImpactParticles(effect.transform, "Sparks", new Color(1f, .59f, .14f),
                new ParticleSystem.MinMaxCurve(.5f, 1f), new ParticleSystem.MinMaxCurve(3f, 20f), .1f, 10);

            var lightObject = new GameObject("Impact flash");
            lightObject.transform.SetParent(effect.transform, false);
            var flash = lightObject.AddComponent<Light>();
            flash.type = LightType.Point;
            flash.color = new Color(1f, .5872f, .1368f);
            flash.range = 1f;
            flash.intensity = 30f;
            Destroy(lightObject, .1f);
            Destroy(effect, 1.2f);
        }

        static void EmitHermitImpactParticles(Transform parent, string label, Color color,
            ParticleSystem.MinMaxCurve lifetime, ParticleSystem.MinMaxCurve speed,
            ParticleSystem.MinMaxCurve size, int count)
        {
            var node = new GameObject(label);
            node.transform.SetParent(parent, false);
            var particles = node.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.loop = false;
            main.startLifetime = lifetime;
            main.startSpeed = speed;
            main.startSize = size;
            main.startColor = color;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = .1f;
            var emission = particles.emission;
            emission.enabled = false;
            particles.Emit(count);
        }

        void DestroyHermitMortarVisuals()
        {
            if (hermitMortarVisuals == null) return;
            if (body)
            {
                body.RainHermitMortars.OnRemove -= RemoveHermitMortarVisual;
                body.RainHermitMortars.OnClear -= ClearHermitMortarVisuals;
            }
            ClearHermitMortarVisuals();
        }

        void DetonateHermitMortar(Vector3 center)
        {
            const int hurtboxLayers = GameManager.characterLayer & ~GameManager.environmentLayer;
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(center, HermitBlastRadius, attackContacts,
                       hurtboxLayers, QueryTriggerInteraction.Collide)) == attackContacts.Length)
                System.Array.Resize(ref attackContacts, attackContacts.Length * 2);

            for (int i = 0; i < count; i++)
            {
                var collider = attackContacts[i];
                attackContacts[i] = null;
                var victim = collider.GetComponentInParent<Health>();
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0) continue;
                float distance = (collider.ClosestPoint(center) - center).sqrMagnitude;
                if (!hermitBlastDistances.TryGetValue(victim, out float best) || distance < best)
                    hermitBlastDistances[victim] = distance;
            }
            foreach (var hit in hermitBlastDistances)
            {
                var victim = hit.Key;
                if (!victim || victim.CurrentHealth <= 0) continue;
                int damage = hit.Value <= HermitBlastRadius * HermitBlastRadius * .25f ? 48 : 12;
                victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0, .5f);
            }
            hermitBlastDistances.Clear();
        }
    }
}
