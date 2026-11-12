using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DisallowMultipleComponent]
    public sealed class HyboreaEasySubBossPhase : MonoBehaviour
    {
        const int AnchorCount = 6;
        const float SplitSpeed = 10f;
        const float SplitLifetime = 3f;
        const string VisualResources = "Jotunnslayer/HybBossEasy/";

        [SerializeField] Transform[] spawnAnchors;

        readonly CharacterInstance[] actors = new CharacterInstance[3];
        readonly Health.SHealthDepleted[] killCallbacks = new Health.SHealthDepleted[3];
        readonly Transform[] splitProjectiles = new Transform[3];
        readonly GameObject[] arrivalEffects = new GameObject[3];
        readonly float[] arrivalTimes = new float[3];
        readonly Transform[] destinations = new Transform[AnchorCount];
        CharacterInstance target;
        Vector3 splitOrigin;
        float launchedAt;
        int expectedCount;
        int killedCount;
        int skillSet;
        Material sparkMaterial;
        Material trailMaterial;
        Material arrivalMaterial;
        AudioClip divideClip;
        AudioClip flyingClip;
        AudioClip arrivalClip;
        GameObject divideAudio;
        Vector3 lastKillPosition;
        Transform returnAnchor;
        Transform returnProjectile;
        float returnStartedAt;
        float returnDuration;
        GameObject returnEffect;

        public bool Cleared => expectedCount > 0 && killedCount == expectedCount;
        internal bool IsRunning => isActiveAndEnabled && expectedCount > 0;
        public bool ReturnArrived { get; private set; }

        internal void BindAnchors(Transform[] anchors) => spawnAnchors = anchors;

        public bool TryBegin(CharacterInstance player, int mapPhase, Vector3 origin)
        {
            if (!isActiveAndEnabled || !NetworkServer.activeHost || NetworkServer.connections.Count != 1 ||
                !player || player.CurrentHealth <= 0 || returnProjectile || (expectedCount > 0 && !Cleared))
                return false;

            var (count, phaseSkillSet) = mapPhase switch
            {
                1 => (2, 0),
                3 => (3, 1),
                _ => (0, 0)
            };
            if (count == 0 || spawnAnchors == null || spawnAnchors.Length != AnchorCount) return false;
            for (int i = 0; i < AnchorCount; i++)
            {
                if (!spawnAnchors[i]) return false;
                for (int j = 0; j < i; j++)
                    if (spawnAnchors[i] == spawnAnchors[j]) return false;
            }
            if (!TryLoadSplitMaterials()) return false;

            StopPhase();
            spawnAnchors.CopyTo(destinations, 0);
            target = player;
            splitOrigin = origin;
            launchedAt = Time.time;
            expectedCount = count;
            skillSet = phaseSkillSet;
            if (divideClip)
            {
                divideAudio = new GameObject("Hyborea phase divide audio");
                divideAudio.transform.SetParent(transform, true);
                divideAudio.transform.position = origin;
                PlaySplitAudio(divideAudio, divideClip, .6f);
                Destroy(divideAudio, divideClip.length);
            }
            for (int i = 0; i < count; i++)
            {
                int selected = Random.Range(i, AnchorCount);
                (destinations[i], destinations[selected]) = (destinations[selected], destinations[i]);
                splitProjectiles[i] = CreateSplitProjectile(origin, destinations[i].position);
                arrivalTimes[i] = FlightDuration(origin, destinations[i].position);
            }
            return true;
        }

        public bool TryBeginReturn(Transform bossAnchor)
        {
            if (!isActiveAndEnabled || !NetworkServer.activeHost || !target || target.CurrentHealth <= 0 ||
                !bossAnchor || !Cleared || returnProjectile || ReturnArrived)
                return false;

            returnAnchor = bossAnchor;
            returnStartedAt = Time.time;
            returnDuration = FlightDuration(lastKillPosition, bossAnchor.position);
            returnProjectile = CreateSplitProjectile(lastKillPosition, bossAnchor.position);
            returnProjectile.name = "Hyborea phase return projectile";
            return true;
        }

        static float FlightDuration(Vector3 origin, Vector3 landing) =>
            Mathf.Min(SplitLifetime, Vector3.Distance(origin, landing) / SplitSpeed);

        bool TryLoadSplitMaterials()
        {
            if (sparkMaterial && trailMaterial && arrivalMaterial) return true;
            var shader = Resources.Load<Shader>(VisualResources + "HybSplitAdditive");
            var texture = Resources.Load<Texture2D>(VisualResources + "T_sparks01");
            var arrivalAtlas = Resources.Load<Texture2D>(VisualResources + "HybSplitArrivalAtlas");
            if (!shader || !texture || !arrivalAtlas) return false;
            sparkMaterial = new Material(shader) { mainTexture = texture };
            trailMaterial = new Material(shader);
            arrivalMaterial = new Material(shader) { mainTexture = arrivalAtlas };
            arrivalMaterial.SetFloat("_MaskOnly", 1f);
            arrivalMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            divideClip = Resources.Load<AudioClip>(VisualResources + "HybSplitDivide");
            flyingClip = Resources.Load<AudioClip>(VisualResources + "HybSplitFlying");
            arrivalClip = Resources.Load<AudioClip>(VisualResources + "HybSplitArrive");
            return true;
        }

        static void PlaySplitAudio(GameObject owner, AudioClip clip, float volume)
        {
            if (!clip) return;
            var audio = owner.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.clip = clip;
            audio.volume = volume;
            audio.spatialBlend = 1f;
            audio.minDistance = 1f;
            audio.maxDistance = 20f;
            audio.Play();
        }

        Transform CreateSplitProjectile(Vector3 origin, Vector3 landing)
        {
            var projectile = new GameObject("Hyborea phase split projectile");
            projectile.transform.SetParent(transform, true);
            Vector3 direction = landing - origin;
            Quaternion rotation = direction.sqrMagnitude > .0001f ? Quaternion.LookRotation(direction) : Quaternion.identity;
            projectile.transform.SetPositionAndRotation(origin, rotation);

            var sparks = projectile.AddComponent<ParticleSystem>();
            sparks.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = sparks.main;
            main.loop = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.3f, .5f);
            main.startSize = new ParticleSystem.MinMaxCurve(.05f, .15f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 5f);
            var emission = sparks.emission;
            emission.rateOverTime = 15f;
            var sheet = sparks.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 2;
            sheet.numTilesY = 2;
            sheet.frameOverTime = new ParticleSystem.MinMaxCurve(0f, .9999f);
            var renderer = projectile.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = sparkMaterial;

            var trail = projectile.AddComponent<TrailRenderer>();
            trail.sharedMaterial = trailMaterial;
            trail.time = .25f;
            trail.startWidth = .28f;
            trail.endWidth = .02f;
            trail.startColor = new Color(.48f, 1f, .11f, .9f);
            trail.endColor = new Color(.96f, .72f, .1f, 0f);
            sparks.Play();
            PlaySplitAudio(projectile, flyingClip, .4f);
            return projectile.transform;
        }

        GameObject PlayArrivalDust(Transform anchor)
        {
            var effect = new GameObject("Hyborea phase arrival dust");
            effect.transform.SetParent(transform, true);
            effect.transform.SetPositionAndRotation(anchor.position,
                anchor.rotation * Quaternion.Euler(-90f, 0f, 0f));

            var dust = effect.AddComponent<ParticleSystem>();
            dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = dust.main;
            main.loop = false;
            main.duration = 1.5f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.2f);
            main.startSize = new ParticleSystem.MinMaxCurve(1.5f, 2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 7.5f);
            main.startColor = new Color(.82f, .88f, .9f, .65f);
            var shape = dust.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 80f;
            shape.radius = .1f;
            var emission = dust.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 15) });
            var sheet = dust.textureSheetAnimation;
            sheet.enabled = true;
            sheet.numTilesX = 3;
            sheet.numTilesY = 1;
            sheet.startFrame = new ParticleSystem.MinMaxCurve(0f, .9999f);
            sheet.frameOverTime = 0f;
            var fade = new Gradient();
            fade.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(.97f, .03f), new GradientAlphaKey(0f, 1f) });
            var color = dust.colorOverLifetime;
            color.enabled = true;
            color.color = fade;
            effect.GetComponent<ParticleSystemRenderer>().sharedMaterial = arrivalMaterial;
            dust.Play();
            PlaySplitAudio(effect, arrivalClip, .55f);
            Destroy(effect, arrivalClip ? Mathf.Max(1.5f, arrivalClip.length) : 1.5f);
            return effect;
        }

        void Update()
        {
            if (expectedCount == 0 || (Cleared && !returnProjectile)) return;
            if (!NetworkServer.activeHost || !target || target.CurrentHealth <= 0)
            {
                StopPhase();
                return;
            }

            if (returnProjectile)
            {
                UpdateReturn();
                return;
            }

            float elapsed = Time.time - launchedAt;
            for (int i = 0; i < expectedCount; i++)
            {
                var projectile = splitProjectiles[i];
                if (!projectile) continue;
                var anchor = destinations[i];
                if (!anchor)
                {
                    StopPhase();
                    return;
                }
                if (elapsed < arrivalTimes[i])
                {
                    float t = elapsed / arrivalTimes[i];
                    projectile.position = Vector3.Lerp(splitOrigin, anchor.position, t);
                    continue;
                }

                Destroy(projectile.gameObject);
                splitProjectiles[i] = null;
                var actor = Skeleton1HSpawn.TrySpawn(target, anchor.position, anchor.rotation,
                    Skeleton1HSpawn.Kind.HybSubBossEasy);
                if (!actor)
                {
                    StopPhase();
                    return;
                }
                actors[i] = actor;
                arrivalEffects[i] = PlayArrivalDust(anchor);
                killCallbacks[i] = (part, type, attacker, force) => OnActorKilled(actor);
                actor.Server_OnHealthDepleted += killCallbacks[i];
                actor.GetComponent<Skeleton1HCombat>().SetHyboreaSkillSet(skillSet);
            }
        }

        void OnActorKilled(CharacterInstance actor)
        {
            killedCount++;
            lastKillPosition = actor.CombatAimPoint;
        }

        void UpdateReturn()
        {
            if (!returnAnchor)
            {
                StopPhase();
                return;
            }
            float elapsed = Time.time - returnStartedAt;
            if (elapsed < returnDuration)
            {
                returnProjectile.position = Vector3.Lerp(lastKillPosition, returnAnchor.position, elapsed / returnDuration);
                return;
            }
            Destroy(returnProjectile.gameObject);
            returnProjectile = null;
            returnEffect = PlayArrivalDust(returnAnchor);
            ReturnArrived = true;
        }

        public void StopPhase()
        {
            if (divideAudio) Destroy(divideAudio);
            divideAudio = null;
            if (returnProjectile) Destroy(returnProjectile.gameObject);
            if (returnEffect) Destroy(returnEffect);
            returnProjectile = null;
            returnEffect = null;
            returnAnchor = null;
            ReturnArrived = false;
            for (int i = 0; i < expectedCount; i++)
            {
                if (splitProjectiles[i]) Destroy(splitProjectiles[i].gameObject);
                splitProjectiles[i] = null;
                if (arrivalEffects[i]) Destroy(arrivalEffects[i]);
                arrivalEffects[i] = null;
                var actor = actors[i];
                if (actor)
                {
                    actor.Server_OnHealthDepleted -= killCallbacks[i];
                    if (NetworkServer.active) NetworkServer.Destroy(actor.gameObject);
                }
                actors[i] = null;
                killCallbacks[i] = null;
            }
            target = null;
            expectedCount = 0;
            killedCount = 0;
        }

        void OnDisable() => StopPhase();

        void OnDestroy()
        {
            if (sparkMaterial) Destroy(sparkMaterial);
            if (trailMaterial) Destroy(trailMaterial);
            if (arrivalMaterial) Destroy(arrivalMaterial);
        }
    }
}
