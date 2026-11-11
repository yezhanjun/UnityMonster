using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;
using MTPSKIT.UI.HUD;

namespace MTPSKIT.Gameplay
{
    public partial class Gun : Item
    {
        [SerializeField] protected ParticleSystem _particleSystem;
        [SerializeField] protected AudioClip fireClip;

        protected Transform _firePoint;
        public float ReloadTime = 1.5f;
        protected bool _isReloading;

        Coroutine c_reload;
        // Driven by the same reload coroutine on owner/server/observers, not by HUD polling.
        internal float PresentationReloadStarted { get; private set; } = -1f;
        internal float PresentationReloadDuration { get; private set; }
        internal bool PresentationReloadFromEmpty { get; private set; }
        internal int PresentationReloadCycle { get; private set; }
        internal bool PresentationIncrementalReload => aaa && aaa.settings.shotCount > 1;
        void BeginReloadPresentation(float duration)
        {
            if (!isServer && !isOwned) return; // Observers use server stages, not their cosmetic ammo copy.
            PresentationReloadStarted = Time.time;
            PresentationReloadDuration = Mathf.Max(.01f, duration);
            PresentationReloadCycle++;
            if (isServer) RpcBeginReloadPresentation(NetworkTime.time, PresentationReloadDuration,
                PresentationReloadFromEmpty, PresentationReloadCycle);
        }
        [ClientRpc(includeOwner = false)]
        void RpcBeginReloadPresentation(double started, float duration, bool fromEmpty, int cycle)
        {
            if (isServer) return;
            PresentationReloadStarted = Time.time - Mathf.Max(0f, (float)(NetworkTime.time - started));
            PresentationReloadDuration = duration;
            PresentationReloadFromEmpty = fromEmpty;
            PresentationReloadCycle = cycle;
            _isReloading = true;
            if (_myOwner) _myOwner.IsReloading = true;
        }
        [ClientRpc(includeOwner = false)]
        void RpcEndReloadPresentation()
        {
            if (isServer) return;
            _isReloading = false;
            if (_myOwner) _myOwner.IsReloading = false;
        }

        [Header("Recoil stats")]
        [SerializeField] public float _recoil_minAngle = 0.01f;
        [SerializeField] public float _recoil_maxAngle = 3;
        [SerializeField] public float _recoil_scopeMultiplier = 0.5f; //set 0 for no recoil, useful for sniper rifle
        [SerializeField] protected float _recoil_angleAddedOnShot = 0.4f;
        [SerializeField] protected float _recoil_stabilizationSpeed = 6f; //how much angle devation from barrel to recover in one second
        public float Recoil_walkMultiplier = 1.5f; //how much multiply recoil when player using item is walking        
        [HideInInspector] public float CurrentRecoil;
        [SerializeField] protected float _characterModelRecoil = 5f;

        [Header("Crosshair")]
        public int Crosshair_minSize = 64;
        public int Crosshair_maxSize = 128;
        public bool HideCrosshairWhenAiming;

        [Header("ReloadAnimation")]
        [SerializeField] protected GameObject _magazinePrefab;
        protected Rigidbody _spawnedMagazine;
        [SerializeField] protected float _magazineDropTime; //at which point in reload animation should magazine be spawned
        [SerializeField] AudioClip _reloadAudioClip;

        [Header("Pooled prefabs")]
        [SerializeField] GameObject _bulletPrefab;
        ObjectPool _bulletPrefabPool;

        [SerializeField] GameObject[] surfaceImpacts;
        GameObject[] ImpactPrefabs => aaa ? aaa.impacts : surfaceImpacts;

        protected override void Awake()
        {
            base.Awake();
            aaa = GetComponent<FpsGunBinding>();

            if (_magazinePrefab)
            {
                _spawnedMagazine = Instantiate(_magazinePrefab).GetComponent<Rigidbody>();
                _spawnedMagazine.gameObject.SetActive(false);
                _magazinePrefab.SetActive(false);
            }

            _bulletPrefabPool = ObjectPooler.Instance.GetPoolByName(_bulletPrefab.name);
        }

        protected override void Update()
        {
            if (!_myOwner) return;

            if (aaa)
            {
                if (currentlyInUse)
                {
                    bool firing = _myOwner.CharacterItemManager.UsePrimaryInput && !_isReloading;
                    if (!firing) triggerHeld = false;
                    aaa.Tick(_myOwner, firing);
                }
                return;
            }

            float recoilMinAngle = _myOwner.CharacterItemManager.UseSecondaryInput ? _recoil_minAngle * _recoil_scopeMultiplier : _recoil_minAngle;

            if (CurrentRecoil > recoilMinAngle)
                CurrentRecoil -= _recoil_stabilizationSpeed * Time.deltaTime;
            else
                CurrentRecoil = recoilMinAngle;
        }

        protected override void Use()
        {
            if (_isReloading) return;
            if (CurrentAmmo <= 0) { RequestReload(); return; }
            if (_myOwner) _myOwner.GetComponent<CharacterMotor>()?.NotifyJumpShot();

            if (aaa) { FireAaa(); return; }

            CurrentAmmo--;
            UpdateAmmoInHud();

            byte surface = 0;
            Vector3 hitPoint;
            RaycastHit hit;

            SnapFirePoint();

            Transform shootPoint = _myOwner.characterFirePoint;
            Quaternion hitRotation = Quaternion.identity;

            shootPoint.localEulerAngles = new Vector3(
                Random.Range(-CurrentRecoil, CurrentRecoil),
                Random.Range(-CurrentRecoil, CurrentRecoil),
                Random.Range(-CurrentRecoil, CurrentRecoil));

            float range = Jotunnslayer.SkillBattlePreview.ScaleRaidWeaponRange(_myOwner, 350f);
            RaycastHit[] hitScan = GameTools.HitScan(shootPoint, _myOwner.transform, GameManager.fireLayer, range);

            if (hitScan.Length > 0) { 

                hit = hitScan[0];
                GameObject go = hit.collider.gameObject;
                hitPoint = hit.point;

                hitRotation = Quaternion.FromToRotation(Vector3.forward, hit.normal);

                HitBox hb = go.GetComponent<HitBox>();

                surface = Surface(hit.collider, hb);
                if (hb)
                {
                    if (!_myOwner.BOT)
                    {
                        CmdDamage(hb._health, hb.part);
                    }
                    else
                    {
                        ServerDamage(hb._health, hb.part);
                    }
                }
            }
            else 
            {
                hitPoint = shootPoint.forward * 99999f;
            }

            if(isOwned)
                Shoot(hitPoint, hitRotation, surface);

            if (_myOwner.BOT)
            {
                RpcShoot(hitPoint, hitRotation, surface);
            }
            else 
            {
               if(isOwned)
                    CmdShoot(hitPoint, hitRotation, surface);
            }

            if (CurrentAmmo <= 0)
                RequestReload();

            base.Use();
        }

       

        protected override void UpdateAmmoInHud()
        {
            if (_myOwner && !_myOwner.IsObserved) return;

            UICharacter._instance.OnAmmoStateChanged($"{CurrentAmmo}/{MagazineCapacity}");
        }

        //game logic side of reload, this code will only run on client that owns this item or on server if bot owns this item
        public override void RequestReload()
        {
            if (_myOwner && _myOwner.RaidControlStopsActions) return;
            //dont start realod procedure if magazine is full or if we are already reloading
            if (CurrentAmmo == MagazineCapacity || _isReloading) return;


            if(isOwned && !isServer)
                Reload();

            if (isServer)
                ServerReload();
            else
                CmdReloadGun();
        }
        void Reload() 
        {
            StopReloadingCoroutine();

            if (!_myOwner) return;

            _isReloading = true;
            PresentationReloadFromEmpty = CurrentAmmo == 0;
            FpsViewController.Instance?.Reload(_myOwner, this);

            _myOwner.CharacterAnimator.Play("reload");
            PlayAnimation("reload");

            if (_reloadAudioClip)
                _audioSource.PlayOneShot(_reloadAudioClip);

            // Observer ammo is cosmetic and may still be full. It cannot decide
            // how many shell stages exist or end the authoritative reload early.
            if (!isServer && !isOwned) return;
            c_reload = StartCoroutine(ReloadCounter());
            IEnumerator ReloadCounter()
            {
                yield return new WaitForSeconds(Mathf.Clamp(coolDownTimer - Time.time, 0, coolDown)); //to avoid reloading immediately after shooting

                _myOwner.IsReloading = true;

                if (aaa && aaa.settings.shotCount > 1)
                {
                    float interval = aaa.settings.reloadTime;
                    float firstShell = CurrentAmmo == 0 ? Mathf.Max(interval, aaa.settings.emptyReloadTime - interval * (MagazineCapacity - 1)) : interval;
                    while (CurrentAmmo < MagazineCapacity)
                    {
                        BeginReloadPresentation(firstShell);
                        yield return new WaitForSeconds(firstShell);
                        firstShell = interval;
                        CurrentAmmo++;
                        if (CurrentAmmo < MagazineCapacity) FpsViewController.Instance?.Reload(_myOwner, this);
                        else FpsViewController.Instance?.FinishReload(_myOwner, this);
                        UpdateAmmoInHud();
                    }
                    _myOwner.IsReloading = false;
                    _isReloading = false;
                    if (isServer) RpcEndReloadPresentation();
                    yield break;
                }
                BeginReloadPresentation(ReloadTime);
                yield return new WaitForSeconds(_magazineDropTime);
                if (_spawnedMagazine)
                {
                    _spawnedMagazine.transform.SetPositionAndRotation(_magazinePrefab.transform.position, _magazinePrefab.transform.rotation);
                    _spawnedMagazine.linearVelocity = Vector3.zero;
                    _spawnedMagazine.angularVelocity = Vector3.zero;
                    _spawnedMagazine.gameObject.layer = (int)GameLayers.ragdoll;
                    _spawnedMagazine.gameObject.SetActive(true);
                    _spawnedMagazine.AddForce(-_magazinePrefab.transform.up * 50);
                }

                yield return new WaitForSeconds(ReloadTime-_magazineDropTime);
                CurrentAmmo = MagazineCapacity;

                UpdateAmmoInHud();
                _myOwner.IsReloading = false;
                _isReloading = false;
                if (isServer) RpcEndReloadPresentation();
            }
        }

        [Command]
        void CmdReloadGun()
        {
            ServerReload();
        }

        protected void ServerReload() 
        {
            Reload();
            RpcReload();
        }

        [ClientRpc(includeOwner = false)]
        void RpcReload() 
        {
            if(!isServer)
                Reload();
        }

        void StopReloadingCoroutine() 
        {
            if (isServer && _isReloading) RpcEndReloadPresentation();
            if (c_reload != null)
            {
                StopCoroutine(c_reload);
                c_reload = null;
            }

            _isReloading = false;
            PresentationReloadStarted = -1f;
        }

        [Command(channel = Channels.Unreliable)]
        void CmdShoot(Vector3 decalPos, Quaternion decalRot, byte hittedMaterialID)
        {
            if (!OwnerBlockedByRaidControl)
                RpcShoot(decalPos, decalRot, hittedMaterialID);
        }
        [ClientRpc(includeOwner = false)]
        void RpcShoot(Vector3 _decalPos, Quaternion _decalRot, byte _hittedMaterialID)
        {
            Shoot(_decalPos, _decalRot, _hittedMaterialID);
        }
        void Shoot(Vector3 decalPos, Quaternion decalRot, byte _hittedMaterialID)
        {
            if (!_myOwner) return;

            _myOwner.CharacterAnimator.DoRecoil(_characterModelRecoil);
            FpsViewController.Instance?.Shot(_myOwner, this);

            _audioSource.PlayOneShot(fireClip);
            _particleSystem.Play();

            PlayAnimation("fire");

            ShowImpact(decalPos, decalRot * Vector3.forward, _hittedMaterialID);
            SpawnBullet(decalPos, decalRot);
        }

        protected void SpawnBullet(Vector3 decalPos, Quaternion decalRot) {
            _bulletPrefabPool.ReturnObject(_particleSystem.transform.position, decalRot).StartBullet(decalPos);//spawn bullet from barrel
        }

        protected void ServerDamageRaw(Health h, CharacterPart hittedPart, int rawDamage, float procCoefficient)
        {
            if (!_myOwner || !h) return;
            h.Server_ChangeHealthStateRaw(_myOwner.ScaleRainWeaponDamage(rawDamage), hittedPart,
                AttackType.hitscan, _myOwner, _ragdollPushForce, procCoefficient);
        }

        public override void Take()
        {
            base.Take();
            triggerHeld = false;
            if (aaa) aaa.Equip(_myOwner);
            _firePoint = _myOwner.characterFirePoint;
            UpdateAmmoInHud();
        }

        public override void AssignToCharacter(CharacterInstance owner)
        {
            if (_myOwner) _myOwner.GetComponent<RangeRobotVisual>()?.ReleaseRifle(this);
            base.AssignToCharacter(owner);
        }

        public override void Drop()
        {
            if (_myOwner) _myOwner.GetComponent<RangeRobotVisual>()?.ReleaseRifle(this);
            base.Drop();
            StopReloadingCoroutine();
        }

        public override void PutDown()
        {
            if (_myOwner) _myOwner.GetComponent<RangeRobotVisual>()?.ReleaseRifle(this);
            base.PutDown();
            StopReloadingCoroutine();
        }

        protected override void SingleUse()
        {
            base.SingleUse();
            CurrentRecoil += _recoil_angleAddedOnShot;
            CurrentRecoil = Mathf.Clamp(CurrentRecoil, _recoil_minAngle, _recoil_maxAngle);
        }

        private void OnDestroy()
        {
            if (_myOwner) _myOwner.GetComponent<RangeRobotVisual>()?.ReleaseRifle(this);
            ReleaseTracers();
            if (_spawnedMagazine)
                Destroy(_spawnedMagazine);
        }
    }
}
