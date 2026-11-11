using Mirror;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    internal sealed class GoatRotatorVolley : MonoBehaviour
    {
        const int ProjectileCount = 4;
        const float Lifetime = 5f;
        const float Speed = 70f;
        const float HitRadius = .7f;

        readonly Transform[] orbs = new Transform[ProjectileCount];
        readonly AudioSource[] sounds = new AudioSource[ProjectileCount];
        readonly bool[] finished = new bool[ProjectileCount];
        CharacterInstance caster;
        CharacterInstance target;
        Vector3 center;
        float started;
        float yaw;
        internal int ConfirmedDamage { get; private set; }

        internal void Begin(CharacterInstance owner, CharacterInstance victim, float facing, AudioClip blast)
        {
            caster = owner;
            target = victim;
            center = owner.transform.position + Vector3.up;
            yaw = facing;
            started = Time.time;
            for (int i = 0; i < orbs.Length; i++)
            {
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.name = "Goat rotator projectile";
                orb.transform.SetParent(transform, true);
                orb.transform.position = Position(i, 0f);
                orb.transform.localScale = Vector3.one * (2f * HitRadius);
                var collider = orb.GetComponent<Collider>();
                collider.enabled = false;
                Destroy(collider);
                orbs[i] = orb.transform;
                if (!blast) continue;
                var source = orb.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.maxDistance = 24f;
                source.volume = .7f;
                source.clip = blast;
                sounds[i] = source;
            }
        }

        Vector3 Position(int index, float elapsed)
        {
            float angle = (yaw - index * 35f + elapsed * Speed) * Mathf.Deg2Rad;
            return center + new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * (4f + index * 3f);
        }

        void Update()
        {
            if (!caster || !caster.isServer || caster.CurrentHealth <= 0 || !target || target.CurrentHealth <= 0)
            {
                Destroy(this);
                return;
            }

            bool active = false;
            float elapsed = Time.time - started;
            for (int i = 0; i < orbs.Length; i++)
            {
                if (finished[i])
                {
                    active |= sounds[i] && sounds[i].isPlaying;
                    continue;
                }

                var orb = orbs[i];
                Vector3 previous = orb.position;
                Vector3 next = Position(i, Mathf.Min(elapsed, Lifetime));
                orb.position = next;
                Vector3 travel = next - previous;
                Vector3 toTarget = target.CombatAimPoint - previous;
                float fraction = travel.sqrMagnitude > 0f ? Mathf.Clamp01(Vector3.Dot(toTarget, travel) / travel.sqrMagnitude) : 0f;
                bool hit = target.Team != caster.Team &&
                    (toTarget - travel * fraction).sqrMagnitude <= HitRadius * HitRadius;
                if (hit)
                {
                    int health = target.CurrentHealth;
                    target.Server_ChangeHealthStateRaw(Random.Range(90, 111), CharacterPart.body, AttackType.explosion, caster, 0);
                    if (target) ConfirmedDamage += Mathf.Max(0, health - target.CurrentHealth);
                }
                if (hit || elapsed >= Lifetime)
                {
                    finished[i] = true;
                    orb.GetComponent<Renderer>().enabled = false;
                    if (sounds[i]) sounds[i].Play();
                    active |= sounds[i] && sounds[i].isPlaying;
                }
                else active = true;
            }
            if (!active) Destroy(this);
        }

        void OnDestroy()
        {
            foreach (var orb in orbs)
                if (orb) Destroy(orb.gameObject);
        }
    }
}
