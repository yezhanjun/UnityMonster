using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

namespace MTPSKIT.Gameplay
{
    /// <summary>
    /// Script responsible for character movement
    /// </summary>
    [DefaultExecutionOrder(100)]
    [RequireComponent(typeof(CharacterInstance))]
    public partial class CharacterMotor : NetworkBehaviour
    {
        CharacterInstance _charInstance;
        CharacterController _controller;
        [SyncVar] private bool stoppedByRaidControl;
        [SyncVar] private bool rootedByRaidSkill;
        [SyncVar] private bool frozenByRaidSkill;
        private LineRenderer raidFreezeRing;
        private Material raidFreezeMaterial;
        internal bool RaidControlStopsActions => stoppedByRaidControl;

        public float WalkSpeed = 5f;
        public float RunSpeed = 10f;
        public float JumpHeight;
        public float FallingSpeed = 10f;
        float _speed;
        float hoofStepDistance;
        AudioSource hoofStepSource;
        static AudioClip hoofStepAudio;
        internal RangeResearchItems RangeItems { private get; set; }

        internal float GetMoveSpeed(bool sprinting)
        {
            if (_charInstance && (_charInstance.ResearchRooted || rootedByRaidSkill)) return 0f;
            float bannerBonus = _charInstance && _charInstance.ResearchBannerBuff ? .30f : 0f;
            float replicatedFrenzyBonus = _charInstance && _charInstance.ResearchBerserkerFrenzy ? .50f : 0f;
            float replicatedHarpoonBonus = _charInstance ? _charInstance.ResearchHarpoonMoveBonus : 0f;
            float replicatedRedWhipBonus = _charInstance ? _charInstance.ResearchRedWhipMoveBonus : 0f;
            float speed = RangeItems ? RangeItems.MoveSpeed(WalkSpeed, RunSpeed, sprinting, bannerBonus)
                : (sprinting ? RunSpeed : WalkSpeed) *
                    (1f + bannerBonus + replicatedFrenzyBonus + replicatedHarpoonBonus + replicatedRedWhipBonus +
                        (_charInstance ? _charInstance.ResearchMochaMoveBonus + _charInstance.ResearchAntlerMoveBonus : 0f));
            if (_charInstance)
            {
                double now = NetworkTime.time;
                float slow = 1f + _charInstance.ResearchStoneFlux;
                if (_charInstance.ResearchIceSlowUntil > now) slow += .8f;
                if (_charInstance.ResearchBaubleSlowUntil > now) slow += .6f;
                if (_charInstance.ResearchTarUntil > now) slow += .5f;
                speed /= slow;
                speed *= 1f - _charInstance.BeetleJuicePenalty;
                if (GameManager.Gamemode is Gamemodes.RangeGamemode effigyMode &&
                    effigyMode.IsWithinRainEffigy(_charInstance.transform.position))
                    speed *= .5f;
                speed *= _charInstance.ResearchCollectorMultiplier;
            }
            if (Jotunnslayer.SkillBattlePreview.HasDisease(_charInstance)) speed *= 0.8f;
            if (Jotunnslayer.SkillBattlePreview.IsSlowedByRaiseDead(_charInstance)) speed *= 0.75f;
            if (Jotunnslayer.SkillBattlePreview.IsSlowedByRaidSkill(_charInstance, Time.time)) speed *= 0.75f;
            return speed * (_charInstance ? 1f + _charInstance.HeroBonuses.speed : 1f) * Jotunnslayer.SkillBattlePreview.IceMoveMultiplier(_charInstance) *
                Jotunnslayer.SkillBattlePreview.NetMoveMultiplier(_charInstance, Time.time) *
                Jotunnslayer.SkillBattlePreview.RaidMoveMultiplier(_charInstance);
        }

        Vector3 force;
        bool _jumped;
        Vector3 pendingRootMotion;

        internal void ResetRainMonsterVelocity()
        {
            ResetRainImpulse();
            force = Vector3.zero;
            pendingRootMotion = Vector3.zero;
            _jumped = false;
        }

        public bool UsingAttackRootMotion => _charInstance && _controller &&
            _charInstance.CurrentHealth > 0 && !_charInstance.Block && _charInstance.IsGrounded && !_jumped &&
            _charInstance.movementInput.sqrMagnitude <= 0.01f &&
            _charInstance.CharacterItemManager.CurrentlyUsedItem is MeleeWeapon blade && blade.UsesAttackRootMotion;

        public void QueueRootMotion(Vector3 delta)
        {
            if (!enabled || !_charInstance || !_controller || !(isOwned || (_charInstance && _charInstance.BOT && isServer)) || !(UsingAttackRootMotion || _charInstance.CharacterAnimator.UsingTurnRootMotion)) return;
            delta.y = 0f;
            pendingRootMotion += delta;
        }

        [Server]
        internal void ApplyServerRaidKnockback(Vector3 displacement)
        {
            if (isOwned || _charInstance.BOT)
                ApplyRaidKnockback(displacement);
            else if (connectionToClient != null)
                TargetApplyRaidKnockback(connectionToClient, displacement);
        }

        [TargetRpc]
        private void TargetApplyRaidKnockback(NetworkConnectionToClient owner, Vector3 displacement)
        {
            if (isOwned)
                ApplyRaidKnockback(displacement);
        }

        private void ApplyRaidKnockback(Vector3 displacement)
        {
            if (isActiveAndEnabled && _charInstance && _charInstance.CurrentHealth > 0 &&
                _controller && _controller.enabled)
                _controller.Move(displacement);
        }

#if UNITY_EDITOR
        [Header("Noclip")]
        bool _noclip = false;
        [SerializeField] float noclipSpeed = 10;
        [SerializeField] float noclipRunSpeed = 30;
#endif

        Vector3 defaultAttackPos;
        Vector3 crouchAttackPos = new Vector3(0, 0.7f, 0);

        

        void Awake()
        {
            _charInstance = GetComponent<CharacterInstance>();
            _controller = GetComponent<CharacterController>();
        }
        void Update()
        {
            if (isServer)
            {
                bool alive = _charInstance.CurrentHealth > 0;
                float now = Time.time;
                stoppedByRaidControl = alive && Jotunnslayer.SkillBattlePreview.IsStoppedByRaidControl(_charInstance, now);
                rootedByRaidSkill = alive && Jotunnslayer.SkillBattlePreview.IsRootedByRaidSkill(_charInstance, now);
                frozenByRaidSkill = alive && Jotunnslayer.SkillBattlePreview.IsFrozenByRaidSkill(_charInstance, now);
            }

            if (isClient)
                RefreshRaidFreezeVisual();

            TickFourWayRoll();
            TickFourWayJump();
            TickHeroSprint();

            //rotate character based on player mouse input/bot input
            if(_charInstance.CurrentHealth>0)
                transform.rotation = Quaternion.Euler(0, IsFourWayRolling ? RollDisplayFacing : LocksAirFacing ? AirFacing : _charInstance.lookInput.y, 0);

            if(isOwned || (_charInstance.BOT&&isServer))
                MovementTick();

        }

        private void RefreshRaidFreezeVisual()
        {
            bool show = frozenByRaidSkill && _charInstance.CurrentHealth > 0;
            if (raidFreezeRing)
            {
                raidFreezeRing.enabled = show;
                return;
            }
            if (!show || SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null)
                return;

            var shader = Shader.Find("Sprites/Default");
            if (!shader)
                return;

            var marker = new GameObject("Raid freeze marker");
            marker.transform.SetParent(transform, false);
            raidFreezeRing = marker.AddComponent<LineRenderer>();
            raidFreezeRing.useWorldSpace = false;
            raidFreezeRing.loop = true;
            raidFreezeRing.positionCount = 24;
            raidFreezeRing.widthMultiplier = 0.055f;
            raidFreezeRing.startColor = raidFreezeRing.endColor = new Color(0.36f, 0.85f, 1f);
            raidFreezeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            raidFreezeRing.receiveShadows = false;
            raidFreezeMaterial = new Material(shader);
            raidFreezeRing.sharedMaterial = raidFreezeMaterial;
            float radius = _controller ? _controller.radius + 0.16f : 0.6f;
            float height = _controller ? _controller.center.y - _controller.height * 0.5f + 0.08f : 0.08f;
            for (int i = 0; i < raidFreezeRing.positionCount; i++)
            {
                float angle = i * (2f * Mathf.PI / raidFreezeRing.positionCount);
                raidFreezeRing.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, height, Mathf.Sin(angle) * radius));
            }
        }

        private void OnDisable()
        {
            CancelFourWayRoll();
            CancelFourWayJump();
            CancelHeroSprint();
            ResetRainImpulse();
            if (raidFreezeRing)
                raidFreezeRing.enabled = false;
        }

        private void OnDestroy()
        {
            if (raidFreezeRing)
                Destroy(raidFreezeRing.gameObject);
            if (raidFreezeMaterial)
                Destroy(raidFreezeMaterial);
        }

        private void FixedUpdate()
        {
            _charInstance.IsGrounded = Physics.CheckSphere(transform.position + new Vector3(0, 0.3f, 0), 0.5f, GameManager.environmentLayer);

            _charInstance.IsCrouching = _charInstance.ReadActionKeyCode(ActionCodes.Crouch) && _charInstance.IsGrounded;

            _charInstance.IsRunning = HeroSprint || !UsesFourWayRoll && _charInstance.ReadActionKeyCode(ActionCodes.Sprint) &&
                !_charInstance.IsUsingItem && CanSprint(_charInstance.movementInput);


            if (!_charInstance.IsCrouching)
            {
                if (_charInstance.ReadActionKeyCode(ActionCodes.Crouch) && _charInstance.IsGrounded && !CheckSphere())
                {
                    Crouch();
                    _charInstance.IsCrouching = true;
                }
            }
            else
            {
                if (!_charInstance.ReadActionKeyCode(ActionCodes.Crouch) && !CheckSphere())
                {
                    Stand();
                    _charInstance.IsCrouching = false;
                }
            }
        }

        public bool CanSprint(Vector2 direction)
        {
            var item = _charInstance.CharacterItemManager.CurrentlyUsedItem;
            bool forward = direction.y > 0.1f && (_charInstance.BOT || Mathf.Abs(direction.x) < 0.1f);
            return forward && !_charInstance.IsBladeWalking && _charInstance.IsGrounded && !_charInstance.Block && !_charInstance.IsReloading &&
                !_charInstance.ReadActionKeyCode(ActionCodes.Crouch) && (!item || !item.DisableRunningAbility);
        }

        public void MovementTick()
        {
            Vector3 rootDelta = pendingRootMotion;
            pendingRootMotion = Vector3.zero;
            if (_charInstance.ResearchRooted) rootDelta = Vector3.Project(rootDelta, Vector3.up);

#if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.J))
                _noclip = !_noclip;

            if (_noclip && isOwned)
            {
                Vector3 noclipInput = new Vector3(_charInstance.movementInput.x, (Input.GetKey(KeyCode.LeftControl) ? -1 : Input.GetKey(KeyCode.Space) ? 1f : 0), _charInstance.movementInput.y);
                noclipInput = _charInstance.FPPCameraTarget.rotation * noclipInput;
                float speed = _charInstance.ReadActionKeyCode(ActionCodes.Sprint) ? noclipRunSpeed : noclipSpeed;
                noclipInput = noclipInput * speed;
                transform.position += noclipInput * Time.deltaTime;
                return;
            }
#endif

            if (_charInstance.CurrentHealth <= 0) return;

            //decide character speed
            if (_controller.isGrounded)
            {
                //sprint/walk speed

                _speed = GetMoveSpeed(_charInstance.IsRunning);
            }
            else
            {
                //dont let character be as fast when falling, as if it was running
                _speed = Mathf.Clamp(_speed, 0f, GetMoveSpeed(true) * 0.8f);
            }

            //get input and, make vector from that and, multiply it by speed and give it appropriate direction based on character rotation
            Vector3 playerInput = new Vector3(_charInstance.movementInput.x, 0, _charInstance.movementInput.y);
            float gunSpeed = 1;
            if (_charInstance.CharacterItemManager.CurrentlyUsedItem is Gun gun && gun.AaaBinding)
            {
                var settings = gun.AaaBinding.settings;
                if (_charInstance.CharacterItemManager.UsePrimaryInput && !_charInstance.IsReloading) gunSpeed = settings.fireWalkPlayerSpeed;
                else if (_charInstance.IsAiming) gunSpeed = settings.aimWalkPlayerSpeed;
            }
            playerInput *= _speed * gunSpeed;
            playerInput = transform.rotation * playerInput; //give movement direction dependent on camera

            if (HeroJumpAirMotion) { /* Jump session owns vertical integration. */ }
            else if (_controller.isGrounded)
            {
                //if character jumped dont treat it as if it was grounded
                if (!_jumped)
                    force.y = -6f;
            }
            else
            {
                //when not grounded make it fall
                force.y -= FallingSpeed * Time.deltaTime;
            }

            //finally move character
            Vector3 displacement = playerInput * _charInstance.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
            var heroVisual = GetComponent<RangeRobotVisual>();
            if (!UsingAttackRootMotion && !_charInstance.CharacterAnimator.UsingTurnRootMotion &&
                heroVisual && heroVisual.RiflePresentation != null &&
                heroVisual.SampleHeroMotion(out var heroDelta))
                displacement = _charInstance.ResearchRooted ? Vector3.zero : heroDelta;
            if (UsingAttackRootMotion || _charInstance.CharacterAnimator.UsingTurnRootMotion) displacement = rootDelta;
            if (!ResolveHeroJumpDisplacement(ref displacement)) displacement += force * Time.deltaTime;
            ResolveRollDisplacement(ref displacement);
            displacement = ResolveRainImpulseDisplacement(displacement, Time.deltaTime);
            displacement = Jotunnslayer.SkillBattlePreview.ApplyRaidDash(_charInstance, displacement, Time.time);
            if (stoppedByRaidControl || rootedByRaidSkill)
                displacement = Vector3.Project(displacement, Vector3.up);
            Vector3 beforeMove = transform.position;
            _controller.Move(Jotunnslayer.SkillBattlePreview.LimitSpearBarrierMovement(_charInstance, displacement, Time.time));
            UpdateHoofFootstep(transform.position - beforeMove);
            _jumped = false;
        }

        void UpdateHoofFootstep(Vector3 displacement)
        {
            if (!isClient || !RangeItems || RangeItems.Hooves <= 0 || !_controller.isGrounded ||
                _charInstance.movementInput.sqrMagnitude < .01f || IsFourWayRolling || UsingAttackRootMotion)
            {
                hoofStepDistance = 0f;
                return;
            }
            displacement.y = 0f;
            hoofStepDistance += displacement.magnitude;
            // The TPS controller has no common right-foot animation event. Use one sound per full stride.
            const float strideDistance = 2.5f;
            if (hoofStepDistance < strideDistance) return;
            hoofStepDistance %= strideDistance;
            if (!hoofStepAudio) hoofStepAudio = Resources.Load<AudioClip>("RainImported/GoatHoofStep");
            if (!hoofStepAudio) return;
            if (!hoofStepSource)
            {
                hoofStepSource = gameObject.AddComponent<AudioSource>();
                hoofStepSource.playOnAwake = false;
                hoofStepSource.spatialBlend = 1f;
            }
            hoofStepSource.clip = hoofStepAudio;
            hoofStepSource.Play();
        }

        public void Jump()
        {
            if (IsFourWayRolling || !enabled || _charInstance.CurrentHealth <= 0 ||
                _charInstance.RaidControlStopsActions || Time.timeScale <= 0) return;
            if (HandleHeroJumpInput()) return;
            if (_controller.isGrounded)
            {
                force.y = JumpHeight;
                _jumped = true;
            }
        }
        /// <summary>
        /// Dont let character move when is dead
        /// </summary>
        public void Die(CharacterPart hittedPartID, Health _attacker)
        {
            enabled = false;
        }

        void Crouch()
        {
            _controller.center = new Vector3(0, 0.5f, 0);
            _controller.height = 1f;

            _charInstance.centerPosition = crouchAttackPos;

            //LerpCamera(1f);
        }
        void Stand()
        {
            _controller.center = new Vector3(0, 1, 0);
            _controller.height = 2f;

            _charInstance.centerPosition = defaultAttackPos;

            //LerpCamera(_charInstance.CameraHeight);
        }

        bool CheckSphere()
        {
            Collider[] col = Physics.OverlapSphere(transform.position + new Vector3(0, 1.5f, 0), 0.4f, GameManager.environmentLayer);

            return col.Length > 0;
        }
    }
}
