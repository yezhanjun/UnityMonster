using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class Gun
    {
        FpsGunBinding aaa;
        bool triggerHeld;
        public FpsGunBinding AaaBinding => aaa;
        struct Pellet
        {
            public Vector3 origin, position, velocity;
            public float lifetime;
            public float procCoefficient;
            public int damage;
            public CharacterInstance shooter;
            public GameObject tracer;
        }

        readonly List<Pellet> flying = new List<Pellet>(24);
        readonly Stack<GameObject> spareTracers = new Stack<GameObject>();
        readonly RaycastHit[] contacts = new RaycastHit[16];
        GameObject tracerPrefab;
        double nextServerShot;
        float cycleEnds;
        protected override float PrimaryCooldown => coolDown / OwnerAttackSpeed;
        float AaaCycle => aaa.cycleSeconds / OwnerAttackSpeed;
        public override void PushLeftTrigger()
        {
            if (aaa && Time.time < cycleEnds) return;
            if (aaa && aaa.settings.fireMode == 1 && triggerHeld && _myOwner && !_myOwner.BOT) return;
            base.PushLeftTrigger();
        }

        void FireAaa()
        {

            cycleEnds = Time.time + AaaCycle;
            var settings = aaa.settings;

            triggerHeld = true;
            CurrentAmmo--;
            UpdateAmmoInHud();
            SnapFirePoint();
            Transform muzzle = _myOwner.characterFirePoint;
            muzzle.localRotation = Quaternion.identity;
            var directions = new Vector3[settings.shotCount];
            for (int i = 0; i < directions.Length; i++) directions[i] = aaa.Direction(muzzle, _myOwner);
            aaa.ApplyKick(_myOwner);
            if (isServer) ServerLaunch(directions);
            else
            {
                Launch(muzzle.position, aaa.OwnerVelocity, directions);
                ShowAaaShot();
                CmdLaunch(directions);
            }
            base.Use();
            if (CurrentAmmo == 0) RequestReload();
        }

        [Command]
        void CmdLaunch(Vector3[] directions)
        {
            if (!aaa || !_myOwner || _myOwner.CurrentHealth <= 0 || OwnerBlockedByRaidControl ||
                _myOwner.CharacterItemManager.CurrentlyUsedItem != this || directions == null ||
                directions.Length != aaa.settings.shotCount || NetworkTime.time + 0.015 < nextServerShot) return;
            foreach (var direction in directions)
                if (!(direction.sqrMagnitude >= 0.99f && direction.sqrMagnitude <= 1.01f)) return;
            nextServerShot = NetworkTime.time + Mathf.Max(PrimaryCooldown, AaaCycle);
            ServerLaunch(directions);
        }

        void ServerLaunch(Vector3[] directions)
        {
            var origin = _myOwner.characterFirePoint.position;
            var velocity = aaa.OwnerVelocity;
            Launch(origin, velocity, directions);
            if (isClient) ShowAaaShot();
            RpcLaunch(origin, velocity, directions);
        }

        [ClientRpc(includeOwner = false, channel = Channels.Unreliable)]
        void RpcLaunch(Vector3 origin, Vector3 inheritedVelocity, Vector3[] directions)
        {
            if (isServer || !_myOwner || !aaa) return;
            Launch(origin, inheritedVelocity, directions);
            ShowAaaShot();
        }

        void Launch(Vector3 origin, Vector3 inheritedVelocity, Vector3[] directions)
        {
            if (isClient && !tracerPrefab) tracerPrefab = Resources.Load<GameObject>("AaaTracer");
            for (int i = 0; i < directions.Length; i++)
            {
                GameObject tracer = null;
                if (tracerPrefab)
                {
                    tracer = spareTracers.Count > 0 ? spareTracers.Pop() : Instantiate(tracerPrefab);
                    tracer.transform.SetPositionAndRotation(origin, Quaternion.LookRotation(directions[i]));
                    tracer.transform.localScale = Vector3.one * aaa.settings.projectileSize;
                    tracer.SetActive(true);
                    var trail = tracer.GetComponentInChildren<TrailRenderer>();
                    if (trail) { trail.Clear(); trail.widthMultiplier = aaa.settings.projectileSize; }
                }
                int damage = Mathf.RoundToInt((i+1) * aaa.settings.damage / (float)directions.Length)
                           - Mathf.RoundToInt(i * aaa.settings.damage / (float)directions.Length);
                flying.Add(new Pellet { position = origin, origin = origin, shooter = _myOwner, damage = damage,
                    procCoefficient = 1f / directions.Length,
                    velocity = directions[i] * aaa.settings.muzzleVelocity + inheritedVelocity, tracer = tracer });
            }
        }

        void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var pellet = flying[i];
                pellet.lifetime += dt;
                pellet.velocity = (pellet.velocity + Physics.gravity * dt) * Mathf.Max(0, 1 - 0.3f * dt);
                Vector3 step = pellet.velocity * dt;
                Vector3 direction = step.normalized;
                bool hit = FindContact(pellet, direction, step.magnitude, out var contact);
                if (hit && isServer)
                {
                    var box = contact.collider.GetComponent<HitBox>();
                    if (box && box._health && pellet.shooter)
                    {
                        float distance = Vector3.Distance(pellet.origin, contact.point);
                        float range = Jotunnslayer.SkillBattlePreview.ScaleRaidWeaponRange(pellet.shooter, aaa.settings.range);
                        float falloff = 1 - Mathf.SmoothStep(0, 1, Mathf.Clamp01(distance / range));
                        int damage = Mathf.RoundToInt(pellet.damage * falloff);
                        if (damage > 0) box._health.Server_ChangeHealthState(damage, box.part, AttackType.hitscan,
                            pellet.shooter, _ragdollPushForce, pellet.procCoefficient, contact.point);
                    }
                    RpcImpact(contact.point, contact.normal, Surface(contact.collider, box));
                }
                if (hit || pellet.lifetime >= 5)
                {
                    if (pellet.tracer) { pellet.tracer.SetActive(false); spareTracers.Push(pellet.tracer); }
                    flying.RemoveAt(i);
                    continue;
                }
                pellet.position += step;
                if (pellet.tracer) pellet.tracer.transform.SetPositionAndRotation(pellet.position, Quaternion.LookRotation(direction));
                flying[i] = pellet;
            }
        }

        bool FindContact(Pellet pellet, Vector3 direction, float distance, out RaycastHit nearest)
        {
            int count = Physics.SphereCastNonAlloc(pellet.position, 0.01f, direction, contacts, distance, GameManager.fireLayer);
            var hits = contacts;
            if (count == contacts.Length)
            {
                hits = Physics.SphereCastAll(pellet.position, 0.01f, direction, distance, GameManager.fireLayer);
                count = hits.Length;
            }
            nearest = default;
            float best = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                if (pellet.shooter && hits[i].transform.IsChildOf(pellet.shooter.transform)) continue;
                if (hits[i].distance >= best) continue;
                nearest = hits[i];
                best = nearest.distance;
            }
            return best < float.PositiveInfinity;
        }

        void ReleaseTracers()
        {
            foreach (var pellet in flying) if (pellet.tracer) Destroy(pellet.tracer);
            foreach (var tracer in spareTracers) if (tracer) Destroy(tracer);
        }
        protected byte Surface(Collider collider, HitBox hitBox)
        {
            if (hitBox && hitBox._health is CharacterInstance) return 4;
            var custom = collider.GetComponentInParent<Akila.FPSFramework.CustomDecal>();
            if (custom && custom.decalVFX)
            {
                int index = Array.IndexOf(ImpactPrefabs, custom.decalVFX);
                if (index >= 0) return (byte)(index + 1);
            }
            byte surface = Classify(collider.sharedMaterial ? collider.sharedMaterial.name : collider.name);
            if (surface != 1) return surface;
            var renderer = collider.GetComponent<Renderer>();
            return renderer && renderer.sharedMaterial ? Classify(renderer.sharedMaterial.name) : (byte)1;
        }

        static byte Classify(string name)
        {
            if (name.IndexOf("metal", StringComparison.OrdinalIgnoreCase) >= 0 || name.Contains("����")) return 2;
            if (name.IndexOf("wood", StringComparison.OrdinalIgnoreCase) >= 0 || name.Contains("ľ")) return 3;
            return 1;
        }

        void ShowAaaShot()
        {
            if (!_myOwner || !aaa) return;
            _myOwner.CharacterAnimator.DoRecoil(_characterModelRecoil);
            FpsViewController.Instance?.Shot(_myOwner, this);
            _myOwner.GetComponent<RangeRobotVisual>()?.RifleShot(this);
            _audioSource.pitch = aaa.settings.pitch + UnityEngine.Random.Range(-aaa.settings.pitchVariation, aaa.settings.pitchVariation);
            _audioSource.PlayOneShot(fireClip);
            _particleSystem.Play();
            PlayAnimation("fire");
        }

        [ClientRpc(channel = Channels.Unreliable)]
        void RpcImpact(Vector3 point, Vector3 normal, byte surface)
        {
            ShowImpact(point, normal, surface);
        }

        protected void ShowImpact(Vector3 point, Vector3 normal, byte surface)
        {
            var impacts = ImpactPrefabs;
            if (!isClient || surface == 0 || impacts == null || surface > impacts.Length || !impacts[surface - 1]) return;
            Quaternion rotation = Quaternion.FromToRotation(Vector3.forward, normal);
            var effect = Instantiate(impacts[surface - 1], point + normal * 0.002f, rotation);
            if (aaa) effect.transform.localScale *= aaa.settings.decalSize;
            if (surface == 2 || surface == 3)
            {
                Transform hole = impacts[0].transform.Find("Decal");
                if (hole) Instantiate(hole, effect.transform, false).name = "Decal";
            }
            if (Physics.Raycast(point + normal * 0.025f, -normal, out var contact, 0.05f, GameManager.fireLayer))
                effect.transform.SetParent(contact.transform, true);
        }
    }
}
