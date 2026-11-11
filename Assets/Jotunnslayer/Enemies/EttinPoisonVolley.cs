using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class EttinPoisonVolley : MonoBehaviour
    {
        internal const int ShotCount = 4;
        const float FlightTime = 1.3f;
        const float LaunchSpacing = .05f;
        const float Radius = 1f;
        const float Height = 5f;

        readonly Transform[] shots = new Transform[ShotCount];
        readonly Vector3[] destinations = new Vector3[ShotCount];
        CharacterInstance owner;
        CharacterInstance target;
        Vector3 origin;
        float startTime;

        internal void Begin(CharacterInstance caster, CharacterInstance victim, AudioClip flight)
        {
            owner = caster;
            target = victim;
            origin = caster.CombatAimPoint;
            startTime = Time.time;
            Vector3 center = victim.CombatAimPoint;
            for (int i = 0; i < ShotCount; i++)
            {
                Vector2 offset = Random.insideUnitCircle * 6f;
                destinations[i] = center + new Vector3(offset.x, 0f, offset.y);
                var shot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                shot.name = "Ettin poison projectile";
                shot.transform.SetParent(transform, true);
                shot.transform.position = origin;
                shot.transform.localScale = Vector3.one * .45f;
                var collider = shot.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);

                var tint = new MaterialPropertyBlock();
                tint.SetColor("_BaseColor", new Color(.25f, .85f, .16f));
                shot.GetComponent<Renderer>().SetPropertyBlock(tint);
                if (flight)
                {
                    var source = shot.AddComponent<AudioSource>();
                    source.clip = flight;
                    source.playOnAwake = true;
                    source.loop = true;
                    source.spatialBlend = 1f;
                    source.maxDistance = 24f;
                    source.volume = .5f;
                }
                shot.SetActive(false);
                shots[i] = shot.transform;
            }
        }

        void Update()
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0 || !target || target.CurrentHealth <= 0)
            {
                Destroy(this);
                return;
            }

            bool pending = false;
            float elapsed = Time.time - startTime;
            for (int i = 0; i < shots.Length; i++)
            {
                var shot = shots[i];
                if (!shot) continue;
                float age = elapsed - (i + 1) * LaunchSpacing;
                if (age < 0f)
                {
                    pending = true;
                    continue;
                }
                if (!shot.gameObject.activeSelf) shot.gameObject.SetActive(true);
                float t = Mathf.Clamp01(age / FlightTime);
                Vector3 next = Vector3.Lerp(origin, destinations[i], t) + Vector3.up * (4f * t * (1f - t) * Height);
                Vector3 step = next - shot.position;
                Vector3 toTarget = target.CombatAimPoint - shot.position;
                float fraction = step.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(toTarget, step) / step.sqrMagnitude) : 0f;
                bool hitTarget = owner.Team != target.Team && (toTarget - step * fraction).sqrMagnitude <= Radius * Radius;
                bool hitTerrain = Physics.Linecast(shot.position, next, GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                if (hitTarget || hitTerrain || t >= 1f)
                {
                    Destroy(shot.gameObject);
                    shots[i] = null;
                    continue;
                }
                shot.position = next;
                pending = true;
            }
            if (!pending) Destroy(this);
        }

        void OnDestroy()
        {
            foreach (var shot in shots)
                if (shot) Destroy(shot.gameObject);
        }
    }
}
