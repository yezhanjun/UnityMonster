using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterMotor
    {
        [SyncVar] double rollStarted = -1;
        [SyncVar] byte rollDirection;
        [SyncVar] bool rollFreeForward;
        [SyncVar] float rollStartFacing;
        float rollVisualFacing;
        internal bool RollFreeForward => rollFreeForward;
        [SyncVar] float rollFacing;
        [SyncVar] float rollMoveStarted = -1;
        [SyncVar] float synchronizedRollClock;
        HeroActionClock rollClock;
        double clockRollSession = -1;
        FourWayRollProfile rollProfile;
        float rollRecovery;
        float rollMovedTime;
        double movedRoll = -1;
        int rollHero;
        Item rollItem;
        internal FourWayRollProfile RollProfile => rollProfile ? rollProfile :
            (rollProfile = Resources.Load<FourWayRollProfile>("DemonHunter3C/FourWayRoll"));
        internal byte RollDirection => rollDirection;
        internal float RollFacing => rollFacing;
        internal float RollMoveWeight => !IsFourWayRolling || rollMoveStarted < 0 ? 0f :
            Mathf.SmoothStep(0f, 1f, (RollElapsed - rollMoveStarted) / Mathf.Max(.01f, RollProfile.moveBlendDuration));
        internal float RollDisplayFacing => Mathf.LerpAngle(rollVisualFacing, _charInstance.lookInput.y, RollMoveWeight);
        internal float RollElapsed => rollStarted < 0 ? 0f : rollClock.Time;
        internal bool IsFourWayRolling => enabled && _charInstance && _charInstance.CurrentHealth > 0 &&
            rollStarted >= 0 && RollProfile && RollElapsed < RollProfile.Duration(rollDirection);
        // Gameplay re-entry is independent of the outgoing animation's remaining blend.
        internal bool CanReenterRoll => !IsFourWayRolling || RollElapsed >=
            RollProfile.directions[rollDirection].clip.length *
            (rollFreeForward ? RollProfile.forwardUnlockNormalized : RollProfile.reenterNormalized);
        internal static byte SelectRollDirection(Vector2 input)
        {
            if (input.sqrMagnitude < .01f) return 1; // No movement: backstep roll.
            if (Mathf.Abs(input.x) >= Mathf.Abs(input.y)) return input.x < 0 ? (byte)2 : (byte)3;
            return input.y < 0 ? (byte)1 : (byte)0;
        }
        internal void RequestFourWayRoll(Vector2 input, float facing)
        {
            if (!isOwned || !CanReenterRoll || !UsesFourWayRoll) return;
            CmdFourWayRoll(SelectRollDirection(input), facing, false);
        }
        internal static void ResolveMovementRoll(Vector2 input, float cameraYaw, float bodyYaw, bool gunLocked,
            out byte direction, out float facing)
        {
            direction = gunLocked ? SelectRollDirection(input) : input.sqrMagnitude > .01f ? (byte)0 : (byte)1;
            facing = gunLocked ? cameraYaw : input.sqrMagnitude > .01f
                ? cameraYaw + Mathf.Atan2(input.x, input.y) * Mathf.Rad2Deg : bodyYaw;
        }
        internal void RequestMovementRoll(Vector2 input, float cameraYaw, bool gunLocked)
        {
            if (!isOwned || !CanReenterRoll || !UsesFourWayRoll) return;
            ResolveMovementRoll(input, cameraYaw, _charInstance.lookInput.y, gunLocked, out byte direction, out float facing);
            CmdFourWayRoll(direction, facing, !gunLocked && direction == 0);
        }
        [Command]
        void CmdFourWayRoll(byte direction, float facing, bool freeForward) => ServerStartFourWayRoll(direction, facing, freeForward);

        internal int RollAcceptedCount { get; private set; }
        internal string RollRejectReason { get; private set; }
        [Server]
        internal bool ServerStartFourWayRoll(byte direction, float facing, bool freeForward = false)
        {
            if (direction > 3 || float.IsNaN(facing) || float.IsInfinity(facing) || !UsesFourWayRoll ||
                !enabled || !_charInstance || _charInstance.CurrentHealth <= 0 || !_charInstance.IsGrounded ||
                _charInstance.IsCrouching || _charInstance.Block || _charInstance.IsReloading ||
                _charInstance.CrowdControlStopsActions || _charInstance.ResearchRooted || rootedByRaidSkill ||
                Time.timeScale <= 0f || !CanReenterRoll || !RollProfile)
            {
                if (LocalAcceptanceClient.Requested) RollRejectReason = "enabled=" + enabled + " ground=" + _charInstance.IsGrounded +
                    " crouch=" + _charInstance.IsCrouching + " block=" + _charInstance.Block + " reload=" + _charInstance.IsReloading +
                    " control=" + _charInstance.CrowdControlStopsActions + " root=" + _charInstance.ResearchRooted + "/" + rootedByRaidSkill +
                    " window=" + CanReenterRoll + " uses=" + UsesFourWayRoll;
                return false;
            }
            if (RollProfile.directions == null || RollProfile.directions.Length != 4 || !RollProfile.directions[direction].clip) return false;
            RollAcceptedCount++;
            rollHero = CurrentHeroSelection;
            CancelHeroSprint();
            rollItem = _charInstance.CharacterItemManager.CurrentlyUsedItem;
            rollFreeForward = freeForward && direction == 0;
            rollStartFacing = transform.eulerAngles.y; rollVisualFacing = rollStartFacing;
            rollDirection = direction; rollFacing = Mathf.Repeat(facing, 360f);
            rollStarted = NetworkTime.time;
            rollMoveStarted = -1;
            synchronizedRollClock = 0f; rollClock.Reset(); clockRollSession = rollStarted;
            movedRoll = -1; rollMovedTime = 0f; rollRecovery = 0f;
            pendingRootMotion = Vector3.zero;
            return true;
        }
        void TickFourWayRoll()
        {
            if (rollStarted < 0 && rollRecovery <= 0f) return;
            float delta = _charInstance.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
            if (isServer && rollRecovery > 0f) rollRecovery = Mathf.Max(0f, rollRecovery - delta);
            if (rollStarted >= 0)
            {
                if (clockRollSession != rollStarted)
                {
                    clockRollSession = rollStarted;
                    rollClock.Reset(synchronizedRollClock);
                    rollVisualFacing = rollStartFacing;
                }
                if (!isServer) rollClock.Observe(synchronizedRollClock);
                rollClock.Advance(delta);
                if (isServer) synchronizedRollClock = rollClock.Time;
            }
            if (isServer && rollStarted >= 0)
            {
                bool changedHero = CurrentHeroSelection != rollHero;
                if (!enabled || _charInstance.CurrentHealth <= 0 || changedHero || !UsesFourWayRoll ||
                    _charInstance.CharacterItemManager.CurrentlyUsedItem != rollItem ||
                    Jotunnslayer.SkillBattlePreview.IsStoppedByRaidControl(_charInstance, Time.time) ||
                    _charInstance.ResearchRooted || rootedByRaidSkill ||
                    RollElapsed >= RollProfile.Duration(rollDirection))
                { rollStarted = -1; rollRecovery = RollProfile.recovery; }
            }
            if (!IsFourWayRolling) return;
            rollVisualFacing = rollFreeForward
                ? Mathf.LerpAngle(rollVisualFacing, rollFacing, Mathf.Clamp01(RollElapsed * 10f)) : rollFacing;
            // Latch the handoff: releasing WASD during recovery must not restart roll travel.
            if (isServer && rollMoveStarted < 0 && (rollFreeForward || _charInstance.movementInput.sqrMagnitude > .01f) &&
                RollElapsed >= RollProfile.directions[rollDirection].clip.length *
                    Mathf.Min(rollFreeForward ? RollProfile.forwardUnlockNormalized : RollProfile.moveUnlockNormalized, RollProfile.exitNormalized))
                {
                rollMoveStarted = RollElapsed;
                if (rollFreeForward) StartForwardRollSprint();
            }
            _charInstance.IsRunning = false;
            // A gun keeps its aim/fire intent during the base-body dodge.
            _charInstance.SetActionKeyCode(ActionCodes.Sprint, false);
            var items = _charInstance.CharacterItemManager;
            if (items.CurrentlyUsedItem is not Gun) items.UsePrimaryInput = false;
            if (items.CurrentlyUsedItem is not Gun) { items.UseSecondaryInput = false; _charInstance.IsAiming = false; }
        }
        bool ResolveRollDisplacement(ref Vector3 displacement)
        {
            if (!IsFourWayRolling) return false;
            if (movedRoll != rollStarted) { movedRoll = rollStarted; rollMovedTime = 0f; }
            float time = Mathf.Min(RollElapsed, RollProfile.Duration(rollDirection));
            var motion = RollProfile.directions[rollDirection];
            var delta = motion.Position(time) - motion.Position(rollMovedTime);
            rollMovedTime = time;
            Vector3 rollDisplacement = Quaternion.Euler(0f, rollVisualFacing, 0f) * delta * (rollFreeForward ? 1f / Mathf.Max(.001f, motion.multiplier) : 1f);
            rollDisplacement.y = displacement.y;
            // Replace rather than add root travel, so both paths still use one controller Move.
            displacement = Vector3.Lerp(rollDisplacement, displacement, RollMoveWeight);
            return true;
        }
        void CancelFourWayRoll()
        {
            if (isServer) { rollStarted = -1; rollMoveStarted = -1; synchronizedRollClock = 0f; }
            clockRollSession = -1; rollClock.Reset(); rollRecovery = 0f;
            movedRoll = -1; rollMovedTime = 0; pendingRootMotion = Vector3.zero;
        }
    }
}


