using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal const int LunarExploderKind = 16;
        internal const int LunarExploderHealth = 180;
        internal const float LunarExploderSpawnDuration = .5f;
        internal const float LunarExploderDeathDuration = 1.3f;
        internal const byte LunarZoneActiveCue = 4;
        const float LunarWalkSpeed = 8f;
        const float LunarSprintSpeed = LunarWalkSpeed * 1.45f;
        const float LunarFireDuration = .1f;
        const float LunarShardSpeed = 80f;
        const float LunarShardLifetime = 2.5f;
        const float LunarShardRadius = .4f;
        const float LunarBlastRadius = 2f;
        const int LunarShardDamage = 11;
        const int LunarMaxStock = 3;
        const string LunarShardResources = "RainActors/RoR2/Base/Characters/LunarExploder/";
        const float LunarZoneLifetime = 7f;
        const float LunarZoneTickInterval = .2f;
        const int LunarZoneDamage = 13; // 11 × 3 × 0.4, using integer health.
        const int LunarHurtboxLayers = GameManager.characterLayer & ~GameManager.environmentLayer;
        static readonly Vector3 LunarZoneHalfSize = new Vector3(9.5f, 1.1377f, 9.5f);
        static readonly Quaternion LunarZoneCrossRotation = Quaternion.Euler(0f, 45f, 0f);

        struct LunarShard
        {
            internal int Id;
            internal Vector3 Position;
            internal Vector3 Direction;
            internal float LastUpdate;
            internal float Expires;
        }

        List<LunarShard> lunarShards;
        Dictionary<int, GameObject> lunarShardVisuals;
        int lunarShardSequence;
        Transform lunarMuzzle;
        int lunarStock = LunarMaxStock;
        float lunarRestockAt;
        float lunarFireUntil;
        Vector3 lunarZoneCenter;
        Quaternion lunarZoneRotation;
        float lunarZoneStartedAt;
        int lunarZoneTick;
        MeshRenderer lunarDeathWarningFill;

        void TickLunarExploder()
        {
            float now = Time.time;
            if (lunarStock < LunarMaxStock && now >= lunarRestockAt)
                lunarStock = LunarMaxStock;

            Vector3 aim = target.CombatAimPoint;
            Vector3 offset = target.transform.position - transform.position;
            bool close = offset.sqrMagnitude <= 20f * 20f;
            bool visible = !Physics.Linecast(body.CombatAimPoint, aim, GameManager.environmentLayer,
                QueryTriggerInteraction.Ignore);
            bool rooted = Jotunnslayer.SkillBattlePreview.IsRootedByNet(body) ||
                Jotunnslayer.SkillBattlePreview.IsRootedByCalamity(body);
            body.lookInput = new Vector2(0f, Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg);
            if (configuredMotor) configuredMotor.WalkSpeed = close ? LunarWalkSpeed : LunarSprintSpeed;
            Vector2 movement = Vector2.zero;
            if (!rooted)
            {
                if (close) movement = GetGroundStrafe();
                else if (SteerChase(visible, false, now)) movement = Vector2.up;
            }
            body.SetMovementInput(movement);

            if (now < lunarFireUntil) return;
            if (body.RainEnemyAnimation == 2 || lunarStock == 0 ||
                (!close && (!visible || offset.sqrMagnitude > 70f * 70f)))
            {
                PlayVisual(movement == Vector2.zero ? (byte)0 : (byte)1);
                return;
            }

            Vector3 origin = lunarMuzzle ? lunarMuzzle.position : body.CombatAimPoint;
            Vector3 direction = aim - origin;
            if (direction.sqrMagnitude < .0001f) direction = transform.forward;
            Quaternion rotation = Quaternion.LookRotation(direction);
            Quaternion roll = Quaternion.AngleAxis(Random.Range(0f, 360f), Vector3.forward);
            direction = rotation * roll * Quaternion.AngleAxis(Random.Range(0f, 3f), Vector3.up) * Vector3.forward;
            lunarStock--;
            lunarRestockAt = now + 1f;
            lunarFireUntil = now + LunarFireDuration;
            int id = ++lunarShardSequence;
            lunarShards.Add(new LunarShard
            {
                Id = id, Position = origin, Direction = direction, LastUpdate = now, Expires = now + LunarShardLifetime
            });
            body.RainLunarExploderShards.Add(id, new CharacterInstance.RainLinearShotState
            {
                Origin = origin, Direction = direction, StartedAt = Mirror.NetworkTime.time
            });
            PlayVisual(2);
        }

        void AdvanceLunarShards(float now)
        {
            for (int i = lunarShards.Count - 1; i >= 0 && i < lunarShards.Count; i--)
            {
                var shard = lunarShards[i];
                float end = Mathf.Min(now, shard.Expires);
                float travel = (end - shard.LastUpdate) * LunarShardSpeed;
                TraceFirstHostile(shard.Position, shard.Direction, LunarShardRadius, travel,
                    out float distance, out var impact);
                shard.Position += shard.Direction * distance;
                if (!impact.collider && now < shard.Expires)
                {
                    shard.LastUpdate = end;
                    lunarShards[i] = shard;
                    continue;
                }

                lunarShards.RemoveAt(i);
                body.RainLunarExploderShards.Remove(shard.Id);
                if (impact.collider)
                {
                    PlayLunarShardImpact(impact.point, impact.normal);
                    body.RpcRainLunarExploderShardImpact(impact.point, impact.normal);
                }
                ExplodeLunarShard(shard.Position);
            }
        }

        internal static void PlayLunarShardImpact(Vector3 point, Vector3 normal)
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            var effect = new GameObject("Rain Lunar Exploder shard impact");
            effect.transform.SetPositionAndRotation(point, Quaternion.FromToRotation(Vector3.forward, normal));
            EmitLunarImpactBurst(effect.transform, "Impact streaks", new ParticleSystem.MinMaxCurve(.1f, .2f),
                4f, new ParticleSystem.MinMaxCurve(.1f, .2f), Color.white, true);
            EmitLunarImpactBurst(effect.transform, "Blue flash", .05f, 0f, new ParticleSystem.MinMaxCurve(.5f, .7f),
                new ParticleSystem.MinMaxGradient(new Color(.2196f, .4196f, 1f), new Color(.00347f, .1912f, .7358f)),
                false);
            Destroy(effect, .25f);
        }

        static void EmitLunarImpactBurst(Transform parent, string label, ParticleSystem.MinMaxCurve lifetime,
            float speed, ParticleSystem.MinMaxCurve size, ParticleSystem.MinMaxGradient color, bool cone)
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
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = Resources.Load<Material>(LunarShardResources + "matLunarShardImpact");
            var shape = particles.shape;
            shape.enabled = cone;
            if (cone)
            {
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 38.8f;
                shape.radius = .08f;
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.lengthScale = 2f;
            }
            var emission = particles.emission;
            emission.enabled = false;
            particles.Emit(5);
        }

        void ConfigureLunarShardVisuals()
        {
            lunarShardVisuals = new Dictionary<int, GameObject>(9);
            body.RainLunarExploderShards.OnRemove += RemoveLunarShardVisual;
            body.RainLunarExploderShards.OnClear += ClearLunarShardVisuals;
        }

        void UpdateLunarShardVisuals()
        {
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            double now = Mirror.NetworkTime.time;
            foreach (var entry in body.RainLunarExploderShards)
            {
                var shot = entry.Value;
                float age = Mathf.Max(0f, (float)(now - shot.StartedAt));
                if (age >= LunarShardLifetime)
                {
                    RemoveLunarShardVisual(entry.Key, shot);
                    continue;
                }
                Vector3 position = shot.Origin + shot.Direction * (LunarShardSpeed * age);
                if (!lunarShardVisuals.TryGetValue(entry.Key, out var shard))
                {
                    shard = new GameObject("Rain Lunar Exploder shard");
                    shard.transform.SetPositionAndRotation(position,
                        Quaternion.LookRotation(shot.Direction) * Quaternion.Euler(0f, 90f, 0f));
                    shard.transform.localScale = new Vector3(.9851344f, 1f, 1f);
                    shard.AddComponent<MeshFilter>().sharedMesh = Resources.Load<Mesh>(LunarShardResources + "LunarInfectionSingleMesh");
                    shard.AddComponent<MeshRenderer>().sharedMaterial =
                        Resources.Load<Material>(LunarShardResources + "matLunarInfection");
                    var trail = shard.AddComponent<TrailRenderer>();
                    trail.sharedMaterial = Resources.Load<Material>(LunarShardResources + "matLunarShardTrail");
                    trail.time = .2f;
                    trail.minVertexDistance = .1f;
                    trail.startWidth = trail.endWidth = .4f;
                    trail.startColor = Color.white;
                    trail.endColor = new Color(1f, 1f, 1f, 0f);
                    lunarShardVisuals.Add(entry.Key, shard);
                }
                shard.transform.position = position;
            }
        }

        void RemoveLunarShardVisual(int id, CharacterInstance.RainLinearShotState _)
        {
            if (lunarShardVisuals.Remove(id, out var shard)) Destroy(shard);
        }

        void ClearLunarShardVisuals()
        {
            foreach (var shard in lunarShardVisuals.Values) Destroy(shard);
            lunarShardVisuals.Clear();
        }

        void DestroyLunarShardVisuals()
        {
            if (lunarShardVisuals == null) return;
            if (body)
            {
                body.RainLunarExploderShards.OnRemove -= RemoveLunarShardVisual;
                body.RainLunarExploderShards.OnClear -= ClearLunarShardVisuals;
            }
            ClearLunarShardVisuals();
        }

        void ExplodeLunarShard(Vector3 position)
        {
            int count;
            while ((count = Physics.OverlapSphereNonAlloc(position, LunarBlastRadius, attackContacts,
                       LunarHurtboxLayers, QueryTriggerInteraction.Collide)) == attackContacts.Length)
                System.Array.Resize(ref attackContacts, attackContacts.Length * 2);
            attackHits.Clear();
            DamageLunarContacts(count, LunarShardDamage, 1f);
            attackHits.Clear();
        }

        void TickLunarExploderDeath(float now)
        {
            if (despawnAt == 0f)
            {
                SetCombat(false);
                lunarZoneStartedAt = now + LunarExploderDeathDuration;
                despawnAt = lunarZoneStartedAt + LunarZoneLifetime + .5f;
                PlayVisual(DeathAnimation);
            }

            if (now >= despawnAt)
            {
                RemoveFromRange();
                return;
            }

            if (lunarZoneTick == 0)
            {
                Vector3 point = body.CombatAimPoint + Vector3.up * .5f;
                lunarZoneRotation = transform.rotation;
                if (Physics.Raycast(point + Vector3.up * 3f, Vector3.down, out var ground, 7f,
                    GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                {
                    point = ground.point;
                    lunarZoneRotation = Quaternion.FromToRotation(Vector3.up, ground.normal) * lunarZoneRotation;
                }
                lunarZoneCenter = point;
                if (now >= lunarZoneStartedAt)
                {
                    lunarZoneTick = 1;
                    if (visual) visual.SetModelVisible(false);
                }
            }

            float age = now - lunarZoneStartedAt;
            if (age < LunarZoneLifetime)
                ShowLunarDeathWarning(lunarZoneCenter + lunarZoneRotation * (Vector3.up * .06f),
                    lunarZoneRotation, charging: lunarZoneTick == 0);
            else
                warning.SetActive(false);
            while (lunarZoneTick > 0 && lunarZoneTick <= 35 && age >= lunarZoneTick * LunarZoneTickInterval)
            {
                lunarZoneTick++;
                attackHits.Clear();
                DamageLunarZoneBox(lunarZoneRotation);
                DamageLunarZoneBox(lunarZoneRotation * LunarZoneCrossRotation);
                attackHits.Clear();
            }
        }

        void ConfigureLunarDeathWarning()
        {
            lunarDeathWarningFill = warning.GetComponent<MeshRenderer>();
            laser.useWorldSpace = false;
            laser.loop = true;
            laser.widthMultiplier = .12f;
            laser.positionCount = 16;
            float halfWidth = LunarZoneHalfSize.x;
            float axisCorner = halfWidth * Mathf.Sqrt(2f);
            float edgeIntersection = axisCorner - halfWidth;
            for (int quadrant = 0; quadrant < 4; quadrant++)
            {
                Quaternion rotation = Quaternion.Euler(0f, quadrant * 90f, 0f);
                int vertex = quadrant * 4;
                laser.SetPosition(vertex, rotation * new Vector3(0f, 0f, axisCorner));
                laser.SetPosition(vertex + 1, rotation * new Vector3(edgeIntersection, 0f, halfWidth));
                laser.SetPosition(vertex + 2, rotation * new Vector3(halfWidth, 0f, halfWidth));
                laser.SetPosition(vertex + 3, rotation * new Vector3(halfWidth, 0f, edgeIntersection));
            }
        }

        void ShowLunarDeathWarning(Vector3 position, Quaternion rotation, bool charging)
        {
            warning.transform.SetPositionAndRotation(position, rotation);
            warning.transform.localScale = charging ? new Vector3(12f, .08f, 12f) : Vector3.one;
            lunarDeathWarningFill.enabled = charging;
            laser.enabled = !charging;
            warning.SetActive(true);
        }

        void DamageLunarZoneBox(Quaternion rotation)
        {
            int count;
            while ((count = Physics.OverlapBoxNonAlloc(lunarZoneCenter, LunarZoneHalfSize, attackContacts,
                       rotation, LunarHurtboxLayers, QueryTriggerInteraction.Collide)) == attackContacts.Length)
                System.Array.Resize(ref attackContacts, attackContacts.Length * 2);
            DamageLunarContacts(count, LunarZoneDamage, .5f);
        }

        void DamageLunarContacts(int count, int damage, float procCoefficient)
        {
            for (int i = 0; i < count; i++)
            {
                var victim = attackContacts[i].GetComponentInParent<Health>();
                attackContacts[i] = null;
                if (!victim || victim == body || victim.Team == body.Team || victim.CurrentHealth <= 0 ||
                    !attackHits.Add(victim)) continue;
                victim.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.explosion, body, 0,
                    procCoefficient);
            }
        }
    }
}
