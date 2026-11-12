using Mirror;
using MTPSKIT.UI;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterMotor
    {
        [SyncVar] double fourJumpStarted = -1;
        [SyncVar] double fourJumpLanded = -1;
        [SyncVar] float landingRunAt = -1f;
        internal bool JumpLandingRun => landingRunAt >= 0f;
        internal float LandingRunElapsed => Mathf.Max(0f, landingClock - landingRunAt);
        [SyncVar] byte fourJumpDirection;
        [SyncVar] float fourJumpFacing;
        [SyncVar] bool fourJumpDirectional;
        [SyncVar] bool fourJumpRightFoot;
        double localJumpStarted = -1;
        [SyncVar] float synchronizedJumpClock;
        [SyncVar] float doubleJumpAt = -1f;
        [SyncVar] bool doubleJumpDirectional;
        [SyncVar] byte doubleJumpDirection;
        bool predictedDoubleDirectional;
        byte predictedDoubleDirection;
        internal bool DoubleJumpDirectional => isOwned && predictedDoubleAt >= 0f ? predictedDoubleDirectional : doubleJumpDirectional;
        internal byte DoubleJumpDirection => isOwned && predictedDoubleAt >= 0f ? predictedDoubleDirection : doubleJumpDirection;
        double simulatedJump = -1;
        float jumpClock, landingClock = -1f, lastLaunchClock, predictedDoubleAt = -1f;
        bool jumpForceApplied, doubleQueued, doubleUsed, landingJumpQueued;
        Vector3 jumpHorizontal;
        Item jumpItem;
        internal float JumpElapsed => jumpClock;
        internal float JumpLandingElapsed => landingClock;
        internal float JumpDoubleAt => isOwned && predictedDoubleAt >= 0f ? predictedDoubleAt : doubleJumpAt;
        internal bool JumpForceApplied => jumpForceApplied;
        internal bool DoubleJumpQueued => doubleQueued;
        internal int JumpLaunchCount { get; private set; }
        internal float JumpVerticalSpeed => force.y;
        internal bool HeroJumpSession => fourJumpStarted >= 0 && UsesFourWayRoll;
        internal bool HeroJumpAirMotion => HeroJumpSession && landingClock < 0;
        float predictedAirFacing, lastSentAirFacing, nextAirFacingSend;
        [Command]
        void CmdSetAirMovementFacing(double session, float facing)
        {
            if (session != fourJumpStarted || !LocksAirFacing || _charInstance.CurrentHealth <= 0 ||
                float.IsNaN(facing) || float.IsInfinity(facing)) return;
            fourJumpFacing = Mathf.Repeat(facing, 360f);
        }
        bool hasPredictedAirFacing;
        internal bool LocksAirFacing => fourJumpStarted >= 0 && fourJumpLanded < 0;
        internal float AirFacing => isOwned && hasPredictedAirFacing ? predictedAirFacing : fourJumpFacing;
        float jumpShotFacingUntil;
        internal void NotifyJumpShot()
        {
            if (LocksAirFacing && isOwned) jumpShotFacingUntil = jumpClock + .15f;
        }
        int fourJumpHero;
        FourWayJumpProfile fourJumpProfile;
        internal FourWayJumpProfile JumpProfile => fourJumpProfile ? fourJumpProfile :
            (fourJumpProfile = Resources.Load<FourWayJumpProfile>("DemonHunter3C/FourWayJump"));
        internal double FourJumpStarted => fourJumpStarted;
        internal double FourJumpLanded => fourJumpLanded;
        internal byte FourJumpDirection => fourJumpDirection;
        internal float FourJumpFacing => fourJumpFacing;
        internal bool FourJumpDirectional => fourJumpDirectional;
        internal bool FourJumpRightFoot => fourJumpRightFoot;
        internal static byte SelectJumpDirection(Vector2 input)
        {
            if (input.sqrMagnitude < .01f) return 0;
            if (Mathf.Abs(input.y) >= Mathf.Abs(input.x)) return input.y > 0 ? (byte)1 : (byte)2;
            return input.x < 0 ? (byte)3 : (byte)4;
        }
        void NotifyFourWayJump()
        {
            if (!UsesFourWayRoll || !isOwned) return;
            var input = PlayerGameplayInput.Instance;
            Vector2 movement = input ? input.MovementInput : _charInstance.movementInput;
            float facing = input ? input.LookInput.y : _charInstance.lookInput.y;
            localJumpStarted = NetworkTime.time;
            var items = _charInstance.CharacterItemManager;
            bool liveInput = input && input.enabled && ClientFrontend.GamePlayInput();
            bool directional = items.CurrentlyUsedItem is Gun && (liveInput
                ? Input.GetMouseButton(0) || Input.GetMouseButton(1)
                : _charInstance.IsAiming || items.UsePrimaryInput);
            var rifle = _charInstance.GetComponent<RangeRobotVisual>()?.RiflePresentation;
            bool rightFoot = rifle != null && rifle.Active && (rifle.Pose == "Run" || rifle.Pose == "Sprint") && JumpProfile.RightFootAt(rifle.GaitPhase);
            if (!directional) facing = _charInstance.lookInput.y;
            predictedAirFacing = facing; hasPredictedAirFacing = true;
            CmdBeginFourWayJump(SelectJumpDirection(movement), facing, directional, rightFoot);
        }
        [Command]
        void CmdBeginFourWayJump(byte direction, float facing, bool directional, bool rightFoot)
        {
            if (direction > 4 || float.IsNaN(facing) || float.IsInfinity(facing) || !enabled || !UsesFourWayRoll ||
                _charInstance.CurrentHealth <= 0 || _charInstance.RaidControlStopsActions || IsFourWayRolling ||
                !_charInstance.IsGrounded || !JumpProfile) return;
            fourJumpDirection = direction; fourJumpFacing = Mathf.Repeat(facing, 360f);
            fourJumpDirectional = directional;
            fourJumpRightFoot = !directional && direction != 0 && rightFoot;
            fourJumpStarted = NetworkTime.time; fourJumpLanded = -1; landingRunAt = -1f; synchronizedJumpClock = 0; doubleJumpAt = -1; doubleJumpDirectional = false; doubleJumpDirection = 0;
            fourJumpHero = CurrentHeroSelection;
        }
        void TickFourWayJump()
        {
            if (fourJumpStarted < 0) { ClearJumpSimulation(); return; }
            if (simulatedJump != fourJumpStarted)
            {
                ClearJumpSimulation(); simulatedJump = fourJumpStarted;
                jumpClock = synchronizedJumpClock;
                jumpItem = _charInstance.CharacterItemManager.CurrentlyUsedItem;
                jumpHorizontal = Vector3.ProjectOnPlane(_controller.velocity, Vector3.up);
                if (fourJumpDirection == 0) jumpHorizontal = Vector3.zero;
            }
            bool changed = isServer && CurrentHeroSelection != fourJumpHero;
            if (!enabled || _charInstance.CurrentHealth <= 0 || changed || !UsesFourWayRoll || IsFourWayRolling ||
                _charInstance.RaidControlStopsActions || jumpItem != _charInstance.CharacterItemManager.CurrentlyUsedItem)
            { CancelFourWayJump(); return; }
            float dt = _charInstance.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
            jumpClock += dt;
            if (isServer) synchronizedJumpClock = jumpClock;
            else if (!isOwned && Mathf.Abs(jumpClock - synchronizedJumpClock) > .2f) jumpClock = synchronizedJumpClock;
            if (fourJumpLanded >= 0 && landingClock < 0) landingClock = 0;
            if (landingClock >= 0)
            {
                landingClock += dt;
                if (isOwned && landingJumpQueued && (JumpLandingRun ? LandingRunElapsed : landingClock) >= (JumpLandingRun ? JumpProfile.landingRun.length : JumpProfile.landing.length) * JumpProfile.landingJumpWindow)
                { landingJumpQueued = false; NotifyFourWayJump(); return; }
                bool moving = _charInstance.movementInput.sqrMagnitude > .01f;
                if (isServer && !JumpLandingRun && !GodWarControl && JumpProfile.landingRun && moving &&
                    landingClock < JumpProfile.landing.length * .5f) landingRunAt = landingClock;
                bool moveExit = !JumpLandingRun && moving && landingClock >= JumpProfile.landing.length * .5f;
                bool finished = JumpLandingRun ? LandingRunElapsed >= JumpProfile.landingRun.length * JumpProfile.landingRunExit
                    : landingClock >= JumpProfile.landing.length * JumpProfile.landingExit;
                bool attacking = _charInstance.CharacterItemManager.UsePrimaryInput;
                if (isServer && !landingJumpQueued && (moveExit || finished || attacking))
                    fourJumpStarted = fourJumpLanded = -1;
                if (isOwned && !_controller.isGrounded)
                { CancelFourWayJump(); return; }

            }
            if (isServer && jumpClock > 30f) fourJumpStarted = fourJumpLanded = -1;
        }
        bool HandleHeroJumpInput()
        {
            if (!UsesFourWayRoll || !isOwned || !JumpProfile) return false;
            if (HeroJumpSession)
            {
                if (landingClock >= 0 || fourJumpLanded >= 0)
                { landingJumpQueued = true; CmdQueueLandingJump(fourJumpStarted); }
                else if (!doubleUsed) doubleQueued = true;
                return true;
            }
            if (_controller.isGrounded && NetworkTime.time - localJumpStarted >= .2) NotifyFourWayJump();
            return true;
        }
        bool ResolveHeroJumpDisplacement(ref Vector3 displacement)
        {
            if (HeroJumpSession && landingClock >= 0f && isOwned)
            {
                displacement.y = -2f * _charInstance.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
                return true;
            }
            if (!HeroJumpAirMotion || !isOwned) return false;
            float dt = _charInstance.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
            float forcePoint = JumpProfile.ForceTime(fourJumpDirectional, fourJumpDirection, fourJumpRightFoot);
            if (!jumpForceApplied && jumpClock >= forcePoint && dt > 0)
            {
                force.y = JumpProfile.jumpPower; jumpForceApplied = true;
                lastLaunchClock = jumpClock; JumpLaunchCount++;
            }
            if (jumpForceApplied && force.y < 0f && jumpClock - lastLaunchClock >= .1f && _controller.isGrounded)
            {
                landingClock = 0; force.y = -2f; doubleQueued = false; jumpHorizontal = Vector3.zero;
                CmdJumpLanded(fourJumpStarted);
                displacement = Vector3.up * (-2f * dt);
                return true;
            }
            Vector3 input = transform.rotation * new Vector3(_charInstance.movementInput.x, 0, _charInstance.movementInput.y);
            if (jumpForceApplied && doubleQueued && !doubleUsed && force.y <= 0f && dt > 0)
            {
                doubleQueued = false; doubleUsed = true; force.y = JumpProfile.jumpPower;
                lastLaunchClock = jumpClock; JumpLaunchCount++;
                predictedDoubleAt = jumpClock;
                // Consume the current camera-relative input at launch, not the queued keypress.
                var gameplayInput = PlayerGameplayInput.Instance;
                if (gameplayInput)
                    input = Quaternion.Euler(0, gameplayInput.LookInput.y, 0) *
                        new Vector3(gameplayInput.MovementInput.x, 0, gameplayInput.MovementInput.y);
                var jumpItems = _charInstance.CharacterItemManager;
                predictedDoubleDirectional = jumpItems.CurrentlyUsedItem is Gun &&
                    (_charInstance.IsAiming || jumpItems.UseSecondaryInput ||
                     (gameplayInput && gameplayInput.enabled && ClientFrontend.GamePlayInput() && Input.GetMouseButton(1)));
                predictedDoubleDirection = SelectJumpDirection(gameplayInput ? gameplayInput.MovementInput : _charInstance.movementInput);
                CmdJumpDouble(fourJumpStarted, jumpClock, AirFacing, predictedDoubleDirectional, predictedDoubleDirection);
            }
            bool hasInput = input.sqrMagnitude > .01f;
            Vector3 target = Vector3.ClampMagnitude(input, 1f) * JumpProfile.airSpeed;
            jumpHorizontal = Vector3.Lerp(jumpHorizontal, target, Mathf.Clamp01(dt * (hasInput ? JumpProfile.airAcceleration : JumpProfile.airBraking)));
            // Source HandleAirMovement: face smoothed velocity, or camera while aiming/firing.
            var controls = PlayerGameplayInput.Instance;
            var items = _charInstance.CharacterItemManager;
            bool aimFacing = items.CurrentlyUsedItem is Gun &&
                (_charInstance.IsAiming || items.UsePrimaryInput || items.UseSecondaryInput || jumpClock < jumpShotFacingUntil);
            if (controls && (aimFacing || (hasInput && jumpHorizontal.sqrMagnitude > .01f)))
            {
                float targetYaw = aimFacing ? controls.LookInput.y : Mathf.Atan2(jumpHorizontal.x, jumpHorizontal.z) * Mathf.Rad2Deg;
                predictedAirFacing = Mathf.Repeat(Mathf.LerpAngle(AirFacing, targetYaw,
                    Mathf.Clamp01(dt * (aimFacing ? 10f : 5f))), 360f);
                hasPredictedAirFacing = true;
                _charInstance.lookInput.y = predictedAirFacing;
                transform.rotation = Quaternion.Euler(0, predictedAirFacing, 0);
                var local = Quaternion.Inverse(transform.rotation) * input;
                _charInstance.SetMovementInput(local.x, local.z);
                if (isServer) fourJumpFacing = predictedAirFacing;
                else if (Mathf.Abs(Mathf.DeltaAngle(lastSentAirFacing, predictedAirFacing)) > 1f &&
                    Time.unscaledTime >= nextAirFacingSend)
                {
                    lastSentAirFacing = predictedAirFacing; nextAirFacingSend = Time.unscaledTime + .05f;
                    CmdSetAirMovementFacing(fourJumpStarted, predictedAirFacing);
                }
            }
            if (jumpForceApplied) force.y += JumpProfile.gravity * dt;
            else force.y = -2f;
            displacement = ((jumpForceApplied || fourJumpDirection != 0 ? jumpHorizontal : Vector3.zero) + Vector3.up * force.y) * dt;
            return true;
        }
        [Command]
        void CmdQueueLandingJump(double session)
        { if (session == fourJumpStarted && fourJumpLanded >= 0) landingJumpQueued = true; }
        [Command]
        void CmdJumpDouble(double session, float elapsed, float facing, bool directional, byte direction)
        {
            if (direction > 4 || session != fourJumpStarted || fourJumpLanded >= 0 || doubleJumpAt >= 0 ||
                float.IsNaN(elapsed) || float.IsInfinity(elapsed) || elapsed < 0 ||
                float.IsNaN(facing) || float.IsInfinity(facing)) return;
            doubleJumpDirectional = directional && _charInstance.CharacterItemManager.CurrentlyUsedItem is Gun;
            doubleJumpDirection = direction;
            doubleJumpAt = elapsed;
            fourJumpFacing = Mathf.Repeat(facing, 360f);
        }
        [Command]
        void CmdJumpLanded(double session)
        {
            if (session != fourJumpStarted || fourJumpLanded >= 0) return;
            fourJumpLanded = NetworkTime.time;
            landingRunAt = !GodWarControl && JumpProfile.landingRun && _charInstance.movementInput.sqrMagnitude > .01f ? 0f : -1f;
        }
        void ClearJumpSimulation()
        {
            simulatedJump = -1; jumpClock = 0; jumpShotFacingUntil = 0; nextAirFacingSend = 0; landingClock = predictedDoubleAt = -1;
            jumpForceApplied = doubleQueued = doubleUsed = landingJumpQueued = false;
            predictedDoubleDirectional = false; predictedDoubleDirection = 0;
            jumpHorizontal = Vector3.zero; jumpItem = null; JumpLaunchCount = 0;
        }
        void CancelFourWayJump()
        {
            if (isServer) fourJumpStarted = fourJumpLanded = -1;
            else if (isOwned && fourJumpStarted >= 0) CmdCancelHeroJump(fourJumpStarted);
            ClearJumpSimulation(); force.y = Mathf.Min(force.y, 0f);
            localJumpStarted = -1; hasPredictedAirFacing = false;
        }
        [Command]
        void CmdCancelHeroJump(double session)
        { if (session == fourJumpStarted) fourJumpStarted = fourJumpLanded = -1; }
    }
}
