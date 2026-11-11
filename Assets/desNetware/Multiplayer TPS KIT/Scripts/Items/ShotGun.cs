using MTPSKIT.Gameplay;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    public class ShotGun : Gun
    {
        [SerializeField] int bulletsInOneShoot;
        public float RecoilFactor = 2f;

        public struct PelletContact
        {
            public Vector3 point;
            public Vector3 normal;
            public byte surface;
        }
        PelletContact[] _contacts;

        protected override void Awake()
        {
            base.Awake();
            _contacts = new PelletContact[bulletsInOneShoot];
        }

        [Command]
        void CmdDamageByMultipleBullets(List<Health> targets, List<CharacterPart> parts, List<int> damage, List<int> hits)
        {
            if (!OwnerBlockedByRaidControl)
                ServerDamageByMultipleBullets(targets, parts, damage, hits);
        }

        void ServerDamageByMultipleBullets(List<Health> targets, List<CharacterPart> parts, List<int> damage, List<int> hits)
        {
            if (targets == null || parts == null || damage == null || hits == null ||
                targets.Count != parts.Count || targets.Count != damage.Count || targets.Count != hits.Count ||
                targets.Count > bulletsInOneShoot) return;
            int remaining = bulletsInOneShoot;
            foreach (int count in hits)
            {
                if (count <= 0 || count > remaining) return;
                remaining -= count;
            }
            for (int i = 0; i < targets.Count; i++)
                ServerDamageRaw(targets[i], parts[i], damage[i], hits[i] / (float)bulletsInOneShoot);
        }

        protected override void Use()
        {
            if (CurrentAmmo <= 0 || _isReloading) return;

            CurrentAmmo--;
            UpdateAmmoInHud();

            SnapFirePoint();

            List<Health> hittedHealths = new List<Health>();
            List<int> damage = new List<int>();
            List<CharacterPart> hittedParts = new List<CharacterPart>();
            List<int> hitCounts = new List<int>();

            for (int i = 0; i < bulletsInOneShoot; i++) //single shoot
            {
                RandomRecoil();

                float range = Jotunnslayer.SkillBattlePreview.ScaleRaidWeaponRange(_myOwner, 250f);
                RaycastHit[] hittedObjects = GameTools.HitScan(_firePoint, _myOwner.transform, GameManager.fireLayer, range);

                if (hittedObjects.Length > 0)
                {
                    var hit = hittedObjects[0];

                    HitBox phb = hittedObjects[0].collider.gameObject.GetComponent<HitBox>();

                    _contacts[i] = new PelletContact { point = hit.point, normal = hit.normal, surface = Surface(hit.collider, phb) };
                    if (phb)
                    {
                        //damage
                        if (!hittedHealths.Contains(phb._health))
                        {
                            hittedHealths.Add(phb._health);
                            hittedParts.Add(phb.part); //causes to damage only first hitted object
                            damage.Add(phb._health.CountDamage((byte)phb.part, _damage));
                            hitCounts.Add(1);
                        }
                        else
                        {
                            int _helthIndexToDamageAgain = hittedHealths.IndexOf(phb._health);

                            damage[_helthIndexToDamageAgain] += phb._health.CountDamage((byte)phb.part, _damage);
                            hitCounts[_helthIndexToDamageAgain]++;

                            //if any bullet from shotgun hits the head, the we count whole shoot as a headshot
                            if (phb.part == CharacterPart.head) hittedParts[_helthIndexToDamageAgain] = phb.part;
                        }
                    }
                }
                else
                {
                    _contacts[i] = new PelletContact { point = _firePoint.position + _firePoint.forward * 999f };
                }
            }

            if (isOwned) ShotgunShoot(_contacts);
            if (isServer)
                RpcShotgunShoot(_contacts);
            else
                CmdShotgunShoot(_contacts);

            //damaging object after summarizing damage
            if (hittedHealths.Count > 0)
            {
                if (!_myOwner.BOT)
                {
                    CmdDamageByMultipleBullets(hittedHealths, hittedParts, damage, hitCounts);
                }
                else
                {
                    ServerDamageByMultipleBullets(hittedHealths, hittedParts, damage, hitCounts);
                }
            }

            if (isOwned)
            {
                SingleUse(); //for client to for example immediately see muzzleflash when he fires his gun
                CmdSingleUse();
                if (CurrentAmmo <= 0)
                    RequestReload();
            }
            else if (isServer)
            {
                SingleUse();
                RpcSingleUse();

                if (CurrentAmmo <= 0)
                    ServerReload();
            }


        }

        [Command]
        void CmdShotgunShoot(PelletContact[] contacts)
        {
            if (!OwnerBlockedByRaidControl)
                RpcShotgunShoot(contacts);
        }

        //dont want to launch this method for client who took a shoot because
        //he already launched it locally
        [ClientRpc(includeOwner = false)]
        void RpcShotgunShoot(PelletContact[] contacts)
        {
            ShotgunShoot(contacts);
        }

        void ShotgunShoot(PelletContact[] contacts)
        {
            if (!_myOwner) return;

            _myOwner.CharacterAnimator.DoRecoil(_characterModelRecoil);
            _audioSource.PlayOneShot(fireClip);
            _particleSystem.Play();
            PlayAnimation("fire");

            for (int i = 0; i < contacts.Length; i++)
            {
                var hit = contacts[i];
                ShowImpact(hit.point, hit.normal, hit.surface);
                SpawnBullet(hit.point, Quaternion.identity);
            }
        }

        public void RandomRecoil()
        {
            _firePoint.localRotation = Quaternion.identity;
            _firePoint.localRotation = Quaternion.Euler(UnityEngine.Random.Range(-RecoilFactor, RecoilFactor), UnityEngine.Random.Range(-RecoilFactor, RecoilFactor), 0);
        }
    }
}
