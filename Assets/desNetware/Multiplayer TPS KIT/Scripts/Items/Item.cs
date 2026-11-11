using System.Collections;
using UnityEngine;
using Mirror;
using MTPSKIT.UI.HUD;

namespace MTPSKIT.Gameplay
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(SphereCollider))]
    [RequireComponent(typeof(BoxCollider))]
    [RequireComponent(typeof(ModelSticker))]

    /// <summary>
    /// Base class for all items in the game
    /// </summary>   
    public class Item : NetworkBehaviour
    {
        /// <summary>
        /// its only for inventory system placed on player to check if it already has that item
        /// when player tries to grab it
        /// </summary>
        public string ItemName = "ItemName";
        public Sprite ItemIcon;

        /// <summary>
        /// player who uses this item, if item does not belong to anyone, than it is null
        /// </summary>
        protected CharacterInstance _myOwner;

        /// <summary>
        /// Time that has to pass for item to be usable since player took it 
        /// </summary>
        [SerializeField] float takingTime = 0.1f;

        /// <summary>
        /// for counting cooldown
        /// </summary>
        protected float coolDownTimer;

        [SerializeField] protected float coolDown = 0.5f;


        [Header("Model")]
        /// <summary>
        /// item model, we need access to that so we can hide it when its acquired by player and not in use
        /// </summary>
        public GameObject ItemModel;
        [SerializeField] Animator _animator;

        public RuntimeAnimatorController AnimatorControllerForCharacter;

        /// collisions
        private SphereCollider _interactionCollider;
        private BoxCollider _collider;
        private Rigidbody _rg;

        /// <summary>
        /// animations for player model relevant to item, if null then player model will play default "empty handed" animation set
        /// </summary>
        

        /// <summary>
        /// when item is dropped, we dont want it be in game world for eternity, so this coroutine will destroy it after
        /// amount of time given in static class "GameManager"
        /// </summary>
        private Coroutine DestroyCoroutine;

        protected bool currentlyInUse;

        /// <summary>
        /// audio source for playing item sounds like firing clip
        /// </summary>
        protected AudioSource _audioSource;

        //recoil/camera shake properties
        public float _recoil;
        public float _devation;
        public float _duration;

        /// <summary>
        /// icon that will be displayed on killfeed if someone dies by this item
        /// </summary>
        public Sprite KillFeedIcon;

        /// <summary>
        /// damage that this item deals on single attack
        /// </summary>
        [SerializeField] protected int _damage = 20;
        [SerializeField] protected short _ragdollPushForce = 1500;

        /// <summary>
        /// component that will stick item to hand of character model
        /// </summary>
        //ModelSticker _modelSticker;

        [Header("Ammo")]
        [SerializeField] protected int CurrentAmmo = 30;
        public int MagazineCapacity = 30;


        [SerializeField] Vector3 _itemModelPositionOffsetWhenInUse;

        public bool DisableRunningAbility = false;

        [Header("Item UI Prefab")]
        [SerializeField] protected GameObject _uiItemPrefab;
        protected HUDItem _itemUI;

        [Header("Camera setup")]
        public float FOVScopeMultiplier = 1f;
        [SerializeField] public float AimSensitivityMultiplier = 1f;


        protected virtual void Awake()
        {
            _rg = GetComponent<Rigidbody>();
            _interactionCollider = GetComponent<SphereCollider>();
            _audioSource = GetComponent<AudioSource>();
            _collider = GetComponent<BoxCollider>();

            _interactionCollider.isTrigger = true;

            //set item layer to item layer
            gameObject.layer = 7;

            Interactable(true);

            //set other audiosource values to work in 3D space
            if (_audioSource)
            {
                _audioSource.spread = 180;
                _audioSource.spatialBlend = 1;
            }
        }

        protected virtual void Update() 
        {
            if (_myOwner)
                transform.position = _myOwner.transform.position;
        }

        public virtual void Take()
        {
            _myOwner.CharacterItemManager.Client_PickedupAmmo += OnOwnerPickedupAmmo;
            currentlyInUse = true;
            coolDownTimer = -99f;
            ShowItemModel(true);
            coolDownTimer = Time.time + takingTime;

            PlayAnimation("take");
        }
        public virtual void PutDown()
        {
            _myOwner.CharacterItemManager.Client_PickedupAmmo -= OnOwnerPickedupAmmo;
            currentlyInUse = false;
            ShowItemModel(false);
            StopAllCoroutines();
            UpdateAmmoInHud();
        }

        public virtual void PushLeftTrigger()
        {
            if (!OwnerBlockedByRaidControl && coolDownTimer <= Time.time)
            {
                Use();
                coolDownTimer = Time.time + PrimaryCooldown;
            }
        }
        protected bool OwnerBlockedByRaidControl => _myOwner &&
            (this is Gun ? _myOwner.CrowdControlStopsActions : _myOwner.RaidControlStopsActions);
        protected float OwnerAttackSpeed => _myOwner
            ? _myOwner.ResearchAttackSpeed * Jotunnslayer.SkillBattlePreview.RaidAttackSpeedMultiplier(_myOwner) : 1f;
        protected virtual float PrimaryCooldown => coolDown;
        public virtual void PushRightTrigger()
        {
        }

        public virtual void RequestReload()
        {

        }
        /// <summary>
        /// Here put the code that only client who uses item or server can run, like giving damage to others
        /// </summary>
        protected virtual void Use()
        {
            if (isOwned)
            {
                SingleUse(); //for client to for example immediately see muzzleflash when he fires his gun
                CmdSingleUse();
            }
            else if (isServer)
            {
                SingleUse();
                RpcSingleUse();
            }
        }
        [Command(channel = Channels.Unreliable)]
        protected virtual void CmdSingleUse()
        {
            if (!OwnerBlockedByRaidControl)
                RpcSingleUse();
        }
        [ClientRpc(includeOwner = false)]
        protected virtual void RpcSingleUse()
        {
            if (_myOwner)
                SingleUse();
        }
        /// <summary>
        /// Here put the code that every client should run, like playing animations etc.
        /// </summary>
        protected virtual void SingleUse()
        {
            if (!_myOwner)
                return;

            _myOwner.CharacterItemManager.StartUsingItem();

            if (_myOwner.PlayerRecoil)
                _myOwner.PlayerRecoil.Recoil(_recoil, _devation, _duration);
        }
        #region pickup and drop
        /// <summary>
        /// Function launched when item is picked up by character
        /// </summary>
        public virtual void AssignToCharacter(CharacterInstance _owner)
        {
            _myOwner = _owner;
            //_modelSticker.SetSticker(_owner.ItemTarget, _owner.itemRotationCorrector);
            ItemModel.transform.SetParent(_owner.ItemTarget);
            ItemModel.transform.localEulerAngles = _owner.itemRotationCorrector;
            ItemModel.transform.localPosition = _itemModelPositionOffsetWhenInUse;
            Interactable(false);

            if (isServer && DestroyCoroutine != null)
            {
                StopCoroutine(DestroyCoroutine);
                DestroyCoroutine = null;
            }

            if(_audioSource)
                _audioSource.spatialBlend = _owner.isOwned ? 0 : 1f;

            if (_owner.isOwned && _uiItemPrefab)
            {
                _itemUI = Instantiate(_uiItemPrefab).GetComponent<HUDItem>();
                _itemUI.Assign(this, _owner);
            }
        }

        /// <summary>
        /// Function launched when item is dropped
        /// </summary>
        public virtual void Drop()
        {
            Interactable(true);
            ShowItemModel(true);
            _myOwner = null;

            ItemModel.transform.SetParent(transform);
            ItemModel.transform.localRotation = Quaternion.identity;

            //initialize self destruct coroutine to prevent excessive number of items in game world
            if (isServer)
            {
                DestroyCoroutine = StartCoroutine(CountToDestroy());
            }

            if (_itemUI)
                Destroy(_itemUI.gameObject);
        }

        /// <summary>
        /// client request to damage someone that he hitted
        /// </summary>
        [Command]
        protected void CmdDamage(Health healthToDamage, CharacterPart _hittedPart)
        {
            if (!OwnerBlockedByRaidControl)
                ServerDamage(healthToDamage, _hittedPart);
        }

        /// <summary>
        /// apply damage to victim
        /// </summary>
        protected void ServerDamage(Health h, CharacterPart hittedPart)
        {
            h.Server_ChangeHealthState(_damage, hittedPart, AttackType.hitscan, _myOwner, _ragdollPushForce);
        }


        #endregion

        /// <summary>
        /// If item is dropped and lonely for too long then we want to destroy it
        /// </summary>
        IEnumerator CountToDestroy()
        {
            yield return new WaitForSeconds(GameManager.TimeOfLivingLonelyItem);

            if (!_myOwner)
                NetworkServer.Destroy(gameObject);
        }

        //when item is in use, show it, if owned but not in use, hide it
        public void ShowItemModel(bool show)
        {
            ItemModel.SetActive(show);
        }

        /// <summary>
        /// Set permission to be collectable, turn of if owned by someone
        /// </summary>
        void Interactable(bool _interactable) //determines if item have physics and can picked or is already aquired by another player
        {
            if (_interactable)
            {
                //_modelSticker.SetSticker(null, Vector3.zero);
                ItemModel.transform.SetParent(transform);
                ItemModel.transform.localPosition = Vector3.zero;
                ItemModel.transform.localRotation = Quaternion.identity;
            }

            _rg.isKinematic = !_interactable;
            _rg.useGravity = _interactable;
            _interactionCollider.enabled = _interactable;
            _collider.enabled = _interactable;
        }

        protected void SnapFirePoint()
        {
            //we are shooting raycast from origin that is in front of our camera by 0.8meter, instead of directly from camera to avoid
            //occasionally hitting targets that are behind our player model   
            if (_myOwner.isOwned)
            {
                bool firstPerson = FpsViewController.Instance && FpsViewController.Instance.FirstPerson;
                Transform aimCamera = firstPerson ? GameplayCamera._instance.transform : _myOwner.FPPCameraTarget;
                RaycastHit[] hits = GameTools.HitScan(aimCamera.position + aimCamera.forward * (firstPerson ? 0f : 0.8f), aimCamera.forward, _myOwner.transform, GameManager.fireLayer, 350f);
                if(hits.Length>0)
                    _myOwner.characterMind.LookAt(hits[0].point);
                else
                    _myOwner.characterMind.eulerAngles = new Vector3(_myOwner.lookInput.x, _myOwner.lookInput.y, 0);
            }
            else
            {
                _myOwner.characterMind.eulerAngles = new Vector3(_myOwner.lookInput.x, _myOwner.lookInput.y, 0);
            }
        }

        public void PlayAnimation(string animName)
        {
            if (_animator && _animator.runtimeAnimatorController)
                _animator.CrossFade(animName, 0f, 0);
        }

        protected virtual void UpdateAmmoInHud()
        {
            if (_myOwner && !_myOwner.IsObserved) return;
            UICharacter._instance.OnAmmoStateChanged($"");
        }
        protected void UpdateAmmoInHud(string msg)
        {
            if (_myOwner && !_myOwner.IsObserved) return;

            UICharacter._instance.OnAmmoStateChanged(msg);
        }
        protected virtual void OnOwnerPickedupAmmo() { }

        public virtual bool CanBeUsed()
        {
            return true;
        }

        private void OnDestroy()
        {
            if (_itemUI)
                Destroy(_itemUI.gameObject);
        }
    }
}
