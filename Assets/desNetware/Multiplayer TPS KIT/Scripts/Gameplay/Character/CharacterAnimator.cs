
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace MTPSKIT.Gameplay
{
    public class CharacterAnimator : MonoBehaviour
    {
        [SerializeField] OverrideTransform _chestIK;
        [SerializeField] OverrideTransform _headIK;

        public RuntimeAnimatorController BaseAnimatorController;
        [SerializeField] KINEMATION.FPSAnimationFramework.Runtime.Layers.LookLayer.LookLayerSettings gunLookProfile;
        HumanoidLookOffset weaponLook;
        CharacterInstance _characterInstance;

        /// <summary>
        /// player model animator
        /// </summary>
        [SerializeField] Animator _animator;
        private float _upperBodyAnimateFactor;

        static readonly int HoldState = Animator.StringToHash("upperBody.look");
        Transform _spine;
        MeleeHitStopService hitStop;
        public bool UsingTurnRootMotion => _adjustingRotation && _characterInstance.CurrentHealth > 0 && !_characterInstance.Block &&
            _characterInstance.CharacterItemManager.CurrentlyUsedItem is MeleeWeapon blade &&
            blade.FullBodyLocomotion && !blade.ComboActive && _characterInstance.IsGrounded &&
            _characterInstance.movementInput.sqrMagnitude <= 0.01f;
        public float CombatDeltaTime(float delta) => hitStop ? hitStop.GetMotionDeltaTime(_animator, delta) : delta;
        public void PlayHitStop(float rate, float duration) => hitStop.PlayHitStop(_animator, rate, duration);
        public void CancelHitStop() { if (hitStop) hitStop.CancelHitStop(_animator); }
        bool bladeGuard;
        float blockHitEnds;
        RuntimeAnimatorController blockController;

        float _targetRecoilUp;
        float _currentRecoilUp;

        float _currentBodyRotation;
        [SerializeField] float _rotationAngleThreshold = 80;
        [SerializeField] float _rotationAngleThresholdLeftSide = 20;
        bool _adjustingRotation = false;
        float bladeTurnElapsed, bladeTurnDuration, bladeTurnFrom, bladeTurnTo;
        [SerializeField] float _adjustingRotationLerpSpeed = 180;

        float _takingDamageFactor;
        float _takingDamageHeadshotFactor;

        [SerializeField] float _damageAnimationRecoverSpeed = 30;

        float _lerpedMovementInputX;
        float _lerpedMovementInputY;

        [Header("Audio")]
        [SerializeField] AudioSource _movementAudioSource;
        [SerializeField] AudioClip _movementClip;

        void Awake()
        {
            _characterInstance = GetComponent<CharacterInstance>();
            hitStop = GetComponent<MeleeHitStopService>();
            if (!hitStop) hitStop = gameObject.AddComponent<MeleeHitStopService>();
            _currentBodyRotation = transform.eulerAngles.y;
            var rootMotion = _animator.GetComponent<MeleeRootMotionReceiver>();
            if (!rootMotion) rootMotion = _animator.gameObject.AddComponent<MeleeRootMotionReceiver>();
            rootMotion.Initialize(_animator, GetComponent<CharacterMotor>(), this);
            _animator.applyRootMotion = true;
            if (gunLookProfile && _animator.isHuman)
            {
                weaponLook = new HumanoidLookOffset(_animator, gunLookProfile);
                _spine = _animator.GetBoneTransform(HumanBodyBones.Spine);
            }
            GetComponent<Health>().Client_OnHealthStateChanged += OnDamaged;
        }

        IEnumerator Start()
        {
            // Allow the selected display model to finish hiding/replacing the template skin.
            yield return null;
            if (_characterInstance && _characterInstance.isClient && !GetComponent<CombatScratchReceiver>())
                gameObject.AddComponent<CombatScratchReceiver>();
        }

        public void ShowModel(bool show) => _animator.gameObject.SetActive(show);

        private void OnDamaged(int currentHealth, CharacterPart damagedPart, AttackType attackType, Health attackerID)
        {
            if (currentHealth > 0 && _characterInstance.IsBladeWalking && _animator.layerCount > 2)
            {
                _animator.Play("blockReaction.BlockHit", 2, 0f);
                _animator.SetLayerWeight(2, 1f);
                blockHitEnds = Time.time + 0.8333334f;
            }
            if (damagedPart == CharacterPart.head)
            {
                _takingDamageHeadshotFactor = 30;
                _takingDamageFactor = 14;
            }
            else
                _takingDamageFactor = 12;

            if (currentHealth <= 0)
            {
                _movementAudioSource.enabled = false;
                CancelHitStop();
                if (_characterInstance.IsObserved && MeleeCameraFeedback.Instance) MeleeCameraFeedback.Instance.Clear();
            }
        }

        void Update()
        {
            //animate character
            var item = _characterInstance.CharacterItemManager.CurrentlyUsedItem;
            UpdateBladeGuard(item);
            UpdateBladeAttackTorso(item);
            UpdateBladeFullBodyAttack(item);
            bool proceduralLook = UsesWeaponLook(item);
            _animator.SetFloat("look", (proceduralLook || item is MeleeWeapon) ? 0f : -_characterInstance.lookInput.x);
            float bodyAimWeight = proceduralLook ? 0f : _upperBodyAnimateFactor;
            _chestIK.weight = bodyAimWeight;
            Vector3 chestRot = new Vector3(_characterInstance.lookInput.x * 0.15f - _currentRecoilUp, _characterInstance.lookInput.y, -_takingDamageFactor);
            _chestIK.transform.localEulerAngles = chestRot;

            _headIK.weight = bodyAimWeight;
            Vector3 headRot = new Vector3(_characterInstance.lookInput.x * 0.8f, _characterInstance.lookInput.y, -_takingDamageHeadshotFactor);
            _headIK.transform.localEulerAngles = headRot;

            float actionWeight = _upperBodyAnimateFactor;
            if (item is MeleeWeapon blade && blade.FullBodyLocomotion)
                actionWeight = 1f - HoldStateWeight();
            _animator.SetLayerWeight(1, actionWeight);

            if (_currentRecoilUp < _targetRecoilUp)
            {
                _currentRecoilUp += 350 * Time.deltaTime;

                if (_currentRecoilUp > _targetRecoilUp) 
                    _targetRecoilUp = 0;
            }

            if (_targetRecoilUp == 0 && _currentRecoilUp > 0)
                _currentRecoilUp -= 45 * Time.deltaTime;

            _currentRecoilUp = Mathf.Clamp(_currentRecoilUp, 0, 30);


            _takingDamageFactor = Mathf.MoveTowards(_takingDamageFactor, 0, _damageAnimationRecoverSpeed * Time.deltaTime);
            _takingDamageHeadshotFactor = Mathf.MoveTowards(_takingDamageHeadshotFactor, 0, _damageAnimationRecoverSpeed * Time.deltaTime);

            if (item is MeleeWeapon bladeTurn && bladeTurn.FullBodyLocomotion)
                UpdateBladeTurn(bladeTurn);
            else
            {
            float difference = -Mathf.DeltaAngle(_characterInstance.lookInput.y, _currentBodyRotation);
            if (difference > 0 && Mathf.Abs(difference) > _rotationAngleThreshold ||
                difference < 0 && Mathf.Abs(difference) > _rotationAngleThresholdLeftSide)
            {
                if (!_adjustingRotation)
                {
                    _animator.CrossFade("turn", 0.05f);
                    _adjustingRotation = true;
                }
            }
            else if (Mathf.Abs(difference) < 1)
                _adjustingRotation = false;

            if (_characterInstance.movementInput != Vector2.zero || !_characterInstance.IsGrounded)
                _currentBodyRotation = _characterInstance.lookInput.y;

            if(_adjustingRotation)
                _currentBodyRotation = ClampRotation(
                    Mathf.Lerp(_currentBodyRotation, _characterInstance.lookInput.y, _adjustingRotationLerpSpeed * Time.deltaTime),
                    _characterInstance.lookInput.y,_rotationAngleThreshold);
            
            }
            _animator.transform.eulerAngles = new Vector3(0, _currentBodyRotation, 0);
        }

        bool bladeBodyAttack;
        void UpdateBladeFullBodyAttack(Item item)
        {
            if (!(item is MeleeWeapon blade) || !blade.FullBodyLocomotion) { bladeBodyAttack = false; return; }
            bool stationary = blade.ComboActive && _characterInstance.CurrentHealth > 0 &&
                _characterInstance.IsGrounded && _characterInstance.movementInput.sqrMagnitude <= 0.01f;
            if (stationary && !bladeBodyAttack)
                _animator.CrossFade("body.BladeAttack" + (blade.ComboIndex + 1), 0.05f, 0, blade.ComboNormalizedTime);
            else if (!stationary && bladeBodyAttack && _characterInstance.CurrentHealth > 0)
                _animator.CrossFadeInFixedTime(!_characterInstance.IsGrounded ? "body.jump" :
                    (_characterInstance.movementInput.sqrMagnitude > 0.01f ? "body.walk" : "body.idle"), 0.08f, 0);
            bladeBodyAttack = stationary;
        }

        void UpdateBladeAttackTorso(Item item)
        {
            if (!(item is MeleeWeapon blade) || !blade.FullBodyLocomotion || _animator.layerCount < 4) return;
            Vector2 movement = _characterInstance.movementInput;
            bool useWalkTorso = blade.ComboActive && _characterInstance.CurrentHealth > 0 &&
                _characterInstance.IsGrounded && !_characterInstance.IsRunning &&
                !_characterInstance.IsBladeWalking && movement.sqrMagnitude > 0.01f && movement.y >= 0f;
            _animator.SetLayerWeight(3, Mathf.MoveTowards(_animator.GetLayerWeight(3),
                useWalkTorso ? 1f : 0f, Time.deltaTime * 10f));
        }
        void UpdateBladeGuard(Item item)
        {
            if (_animator.runtimeAnimatorController != blockController)
            {
                blockController = _animator.runtimeAnimatorController;
                bladeGuard = false;
                blockHitEnds = 0f;
            }
            if (!(item is MeleeWeapon blade) || !blade.FullBodyLocomotion || _animator.layerCount < 3) return;
            bool held = _characterInstance.CurrentHealth > 0 && _characterInstance.IsBladeWalking && !blade.ComboActive;
            if (held != bladeGuard)
            {
                if (held) _animator.CrossFadeInFixedTime("upperBody.BlockStart", 0.08f, 1, 0f);
                else if (!blade.ComboActive && _characterInstance.CurrentHealth > 0)
                    _animator.CrossFadeInFixedTime("upperBody.BlockEnd", 0.08f, 1, 0f);
                bladeGuard = held;
            }
            if (!held) blockHitEnds = 0f;
            float reaction = held && Time.time < blockHitEnds ? Mathf.Clamp01((blockHitEnds - Time.time) / 0.12f) : 0f;
            _animator.SetLayerWeight(2, reaction);
        }
        void UpdateBladeTurn(MeleeWeapon blade)
        {
            if (blade.ComboActive || _characterInstance.movementInput != Vector2.zero || !_characterInstance.IsGrounded)
            {
                if (_adjustingRotation && !blade.ComboActive)
                    _animator.CrossFadeInFixedTime(_characterInstance.IsGrounded ? "body.walk" : "body.jump", 0.08f, 0);
                _adjustingRotation = false;
                _currentBodyRotation = _characterInstance.lookInput.y;
                return;
            }
            float delta = Mathf.DeltaAngle(_currentBodyRotation, _characterInstance.lookInput.y);
            if (!_adjustingRotation && Mathf.Abs(delta) > _rotationAngleThreshold)
            {
                // Preserve the camera's side at the otherwise ambiguous +/-180 boundary.
                float raw = _characterInstance.lookInput.y - _currentBodyRotation;
                if (Mathf.Abs(delta) > 179.9f) delta = raw < 0 ? -180f : 180f;
                bool halfTurn = Mathf.Abs(delta) >= 135f;
                _animator.SetFloat("turnAngle", Mathf.Sign(delta) * (halfTurn ? 180f : 90f));
                _animator.CrossFadeInFixedTime("body.turn", 0.04f, 0, 0f);
                bladeTurnElapsed = 0f;
                bladeTurnDuration = blade.TurnDuration(halfTurn, delta < 0);
                bladeTurnFrom = _currentBodyRotation;
                bladeTurnTo = bladeTurnFrom + Mathf.Sign(delta) * (halfTurn ? 180f : 90f);
                _adjustingRotation = true;
            }
            if (!_adjustingRotation) return;
            bladeTurnElapsed += CombatDeltaTime(Time.deltaTime);
            var state = _animator.GetCurrentAnimatorStateInfo(0);
            if ((state.IsName("body.turn") && state.normalizedTime >= 1f) ||
                bladeTurnElapsed > bladeTurnDuration + 0.25f)
            {
                _currentBodyRotation = bladeTurnTo;
                _adjustingRotation = false;
            }
        }
        public void ApplyTurnRootRotation(Quaternion deltaRotation)
        {
            if (!_adjustingRotation || _characterInstance.CurrentHealth <= 0 ||
                !(_characterInstance.CharacterItemManager.CurrentlyUsedItem is MeleeWeapon blade) ||
                !blade.FullBodyLocomotion || blade.ComboActive || !_characterInstance.IsGrounded ||
                _characterInstance.movementInput != Vector2.zero) return;
            _currentBodyRotation += Mathf.DeltaAngle(0f, deltaRotation.eulerAngles.y);
            _animator.transform.rotation = Quaternion.Euler(0f, _currentBodyRotation, 0f);
        }

        bool UsesWeaponLook(Item item) => weaponLook != null && (item is Gun || item is MeleeWeapon);

        void LateUpdate()
        {
            var item = _characterInstance.CharacterItemManager.CurrentlyUsedItem;
            if (!UsesWeaponLook(item) || !_animator.isActiveAndEnabled || _characterInstance.CurrentHealth <= 0) return;
            // Guard poses own the arms; keep camera pitch/yaw out of their authored animation.
            if (_characterInstance.IsBladeWalking && item is MeleeWeapon guard && !guard.ComboActive) return;
            // The eight-way clips turn the hips underneath the masked weapon pose.
            // Correct the common parent of both hands before applying the FAF look profile.
            Quaternion holdRotation = item is MeleeWeapon melee ? melee.NeutralHoldRotation : Quaternion.identity;
            if (!(item is MeleeWeapon blade && blade.FullBodyLocomotion))
                StabilizeWeaponHold(item, holdRotation);
            float yaw = Mathf.DeltaAngle(_animator.transform.eulerAngles.y, _characterInstance.lookInput.y);
            float weight = Mathf.Lerp(0.3f, 1f, _upperBodyAnimateFactor);
            float pitch = _characterInstance.lookInput.x - (item is Gun ? _currentRecoilUp : 0f);
            weaponLook.Apply(_animator.transform.rotation, yaw * weight, pitch * weight);
        }

        float HoldStateWeight()
        {
            float hold = _animator.GetCurrentAnimatorStateInfo(1).fullPathHash == HoldState ? 1f : 0f;
            if (_animator.IsInTransition(1))
            {
                float next = _animator.GetNextAnimatorStateInfo(1).fullPathHash == HoldState ? 1f : 0f;
                hold = Mathf.Lerp(hold, next, _animator.GetAnimatorTransitionInfo(1).normalizedTime);
            }
            return hold;
        }

        void StabilizeWeaponHold(Item item, Quaternion neutralHold)
        {
            float hold = HoldStateWeight();
            if (_spine && item.ItemModel && hold > 0f)
            {
                Quaternion targetRotation = _animator.transform.rotation * neutralHold;
                Quaternion correction = targetRotation * Quaternion.Inverse(item.ItemModel.transform.rotation);
                _spine.rotation = Quaternion.Slerp(Quaternion.identity, correction, hold * _upperBodyAnimateFactor) * _spine.rotation;
            }
        }

        private void FixedUpdate()
        {
            //apply animation properties
            _lerpedMovementInputX = Mathf.MoveTowards(_lerpedMovementInputX, _characterInstance.movementInput.x, Time.fixedDeltaTime*10);
            _lerpedMovementInputY = Mathf.MoveTowards(_lerpedMovementInputY, _characterInstance.movementInput.y, Time.fixedDeltaTime*10);

            _animator.SetFloat("X", Mathf.Clamp(_lerpedMovementInputX, -1, 1));
            _animator.SetFloat("Y", Mathf.Clamp(_lerpedMovementInputY, -1, 1));
            if (_characterInstance.CharacterItemManager.CurrentlyUsedItem is MeleeWeapon blade && blade.FullBodyLocomotion)
                _animator.SetFloat("bladeWalk", _characterInstance.IsBladeWalking ? 1f : 0f, 0.1f, Time.fixedDeltaTime);
            _animator.SetFloat("speed", _characterInstance.IsRunning ? 2f: (_characterInstance.movementInput != Vector2.zero? 1: 0));
            _animator.SetFloat("isCrouchingFloat", _characterInstance.IsCrouching ? 1f:0f);
            _animator.SetBool("isCrouching", _characterInstance.IsCrouching);
            _animator.SetBool("isGrounded", _characterInstance.IsGrounded);

            _upperBodyAnimateFactor = Mathf.Lerp(_upperBodyAnimateFactor, System.Convert.ToInt32(!_characterInstance.IsRunning || _characterInstance.IsReloading || _characterInstance.IsCrouching), 20f * Time.deltaTime);

            _movementAudioSource.enabled = _characterInstance.IsRunning;
        }

        public void SetRuntimeAnimatorController(RuntimeAnimatorController controller)
        {
            _adjustingRotation = false;
            var selected = controller ? controller : BaseAnimatorController;
            if (TryGetComponent<RangeRobotVisual>(out var visual))
                visual.SetSourceController(_animator, selected);
            else
                _animator.runtimeAnimatorController = selected;
        }

        public void DoRecoil(float recoilUp) 
        {
            _targetRecoilUp = recoilUp;
            _currentRecoilUp = 0;
        }

        public void PlayMeleeCombo(int index, float playbackSpeed)
        {
            _animator.SetFloat("bladeAttackSpeed", playbackSpeed);
            _animator.Play("upperBody.BladeAttack" + (index + 1));
            bool stationary = _characterInstance.IsGrounded && _characterInstance.movementInput.sqrMagnitude <= 0.01f;
            if (stationary) _animator.Play("body.BladeAttack" + (index + 1), 0, 0f);
            else if (bladeBodyAttack)
                _animator.CrossFadeInFixedTime(!_characterInstance.IsGrounded ? "body.jump" : "body.walk", 0.08f, 0);
            bladeBodyAttack = stationary;
        }

        public void Play(string animName)
        {
            _animator.Play(animName);
        }

        float ClampRotation(float currentRot, float targetRot, float threshold)
        {
            float difference = Mathf.DeltaAngle(currentRot, targetRot);

            if (difference > threshold || difference < -threshold)
            {
                float angle = Mathf.Clamp(currentRot, targetRot - threshold, targetRot + threshold);
                return angle;
            }
            else
                return currentRot;
        }
    }
}
