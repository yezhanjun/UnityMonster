using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public partial class CharacterMotor
    {
        [SyncVar] bool heroSprint;
        [SyncVar] bool heroSprintStopping;
        float sprintStopClock;
        [SyncVar] bool sprintTurning;
        [SyncVar] float sprintTurnClock, sprintTurnFrom, sprintTurnAngle;
        [SyncVar] uint sprintTurnSession, sprintAcceptedRequest;
        Item sprintItem;
        int sprintHero;
        HeroActionClock localTurnClock;
        uint observedTurnSession, requestSequence, pendingTurnRequest, abandonedTurnRequest, cancelledTurnRequest;
        bool predictedTurn, turnRequestBlocked;
        float predictedTurnFrom, predictedTurnAngle, requestedTurnYaw, requestDeadline;
        internal int SprintTurnRequests { get; private set; }
        internal int SprintTurnRejections { get; private set; }
        internal int SprintTurnTimeouts { get; private set; }
        internal bool HeroSprintStopping => heroSprintStopping;
        internal bool HeroSprint => heroSprint && enabled && _charInstance.CurrentHealth > 0;
        bool HasLocalTurn => predictedTurn || sprintTurning;
        internal bool SprintTurning => (HeroSprint || heroSprintStopping) && HasLocalTurn && SprintTurnClip &&
            localTurnClock.Time < SprintTurnClip.length * SprintProfile.turnExit;
        internal float SprintTurnTime => localTurnClock.Time;
        float TurnAngle => predictedTurn ? predictedTurnAngle : sprintTurnAngle;
        float TurnFrom => predictedTurn ? predictedTurnFrom : sprintTurnFrom;
        internal bool SprintTurnRight => TurnAngle >= 0f;
        internal AnimationClip SprintTurnClip => !SprintProfile ? null : SprintTurnRight ? SprintProfile.sprintTurnRight : SprintProfile.sprintTurnLeft;
        internal float SprintTurnYaw => !SprintProfile ? _charInstance.lookInput.y : TurnFrom + TurnAngle * Mathf.Clamp01(
            (SprintTurnTime / Mathf.Max(.01f, SprintTurnClip ? SprintTurnClip.length : 1f) - SprintProfile.turnRotationStart) /
            Mathf.Max(.001f, SprintProfile.turnRotationDuration));

        bool RollBlocksSprintTurn => IsFourWayRolling &&
            !(RollFreeForward && rollMoveStarted >= 0f);
        bool CanHeroSprint => UsesFourWayRoll && !GodWarControl && enabled && _charInstance.CurrentHealth > 0 &&
            _charInstance.IsGrounded && !LocksAirFacing && !_charInstance.IsCrouching && !_charInstance.Block &&
            !_charInstance.CrowdControlStopsActions && !_charInstance.ResearchRooted && !rootedByRaidSkill &&
            !_charInstance.ReadActionKeyCode(ActionCodes.LockedLocomotion) && !_charInstance.IsAiming &&
            !_charInstance.CharacterItemManager.UsePrimaryInput && !_charInstance.CharacterItemManager.UseSecondaryInput && SprintProfile;

        void StartForwardRollSprint()
        {
            if (!isServer || !CanHeroSprint) return;
            heroSprint = true; heroSprintStopping = false; sprintHero = CurrentHeroSelection;
            sprintItem = _charInstance.CharacterItemManager.CurrentlyUsedItem;
        }

        void TickHeroSprint()
        {
            float delta = _charInstance.CharacterAnimator.CombatDeltaTime(Time.deltaTime);
            if (isServer && (heroSprint || heroSprintStopping))
            {
                if (CurrentHeroSelection != sprintHero || !CanHeroSprint ||
                    _charInstance.CharacterItemManager.CurrentlyUsedItem != sprintItem) CancelHeroSprint();
                else if (_charInstance.movementInput.sqrMagnitude <= .01f)
                {
                    if (heroSprint) { heroSprintStopping = true; sprintStopClock = 0f; }
                    heroSprint = false; sprintTurning = false;
                    sprintStopClock += delta;
                    float duration = Mathf.Max(SprintProfile.sprintStopLeft ? SprintProfile.sprintStopLeft.length : 0f,
                        SprintProfile.sprintStopRight ? SprintProfile.sprintStopRight.length : 0f);
                    if (sprintStopClock >= duration * (SprintProfile.sprintStopExit - SprintProfile.sprintStopStart))
                        heroSprintStopping = false;
                }
                else if (heroSprintStopping)
                {
                    float angle = Mathf.Abs(Mathf.Atan2(_charInstance.movementInput.x, _charInstance.movementInput.y) * Mathf.Rad2Deg);
                    if (angle < SprintProfile.turnThreshold) heroSprintStopping = false; // Ordinary re-entry returns to Run.
                }
            }

            if (!isServer && sprintTurnSession != observedTurnSession)
            {
                bool confirmedPrediction = predictedTurn && pendingTurnRequest == sprintAcceptedRequest;
                observedTurnSession = sprintTurnSession;
                if (!confirmedPrediction)
                    localTurnClock.Reset(sprintAcceptedRequest == abandonedTurnRequest && SprintTurnClip
                        ? Mathf.Max(sprintTurnClock, SprintTurnClip.length * SprintProfile.turnExit) : sprintTurnClock);
                if (pendingTurnRequest == sprintAcceptedRequest) pendingTurnRequest = 0;
                predictedTurn = false;
            }
            if (pendingTurnRequest != 0 && Time.unscaledTime >= requestDeadline)
            {
                abandonedTurnRequest = pendingTurnRequest;
                CmdCancelTurnRequest(pendingTurnRequest);
                SprintTurnTimeouts++;
                pendingTurnRequest = 0; predictedTurn = false; turnRequestBlocked = true;
            }
            if ((!HeroSprint && !heroSprintStopping) || !SprintProfile)
            { predictedTurn = false; pendingTurnRequest = 0; turnRequestBlocked = false; }
            if ((HeroSprint || heroSprintStopping) && HasLocalTurn)
            {
                if (!isServer && !predictedTurn) localTurnClock.Observe(sprintTurnClock);
                localTurnClock.Advance(delta);
                if (isServer)
                {
                    sprintTurnClock = localTurnClock.Time;
                    if (!SprintTurnClip || sprintTurnClock >= SprintTurnClip.length * SprintProfile.turnExit)
                        sprintTurning = false;
                }
            }
            if (UsesFourWayRoll) _charInstance.IsRunning = HeroSprint;
        }

        internal bool ApplyHeroSprintFacing(float desiredYaw, bool moving, bool locked, out float yaw)
        {
            yaw = _charInstance.lookInput.y;
            if ((!HeroSprint && !heroSprintStopping) || !moving || locked || LocksAirFacing) return false;
            if (SprintTurning) { yaw = SprintTurnYaw; return true; }
            // A completed predicted turn waits for its existing authority result, never submits again.
            if (HasLocalTurn || pendingTurnRequest != 0) return true;
            if (RollBlocksSprintTurn || !SprintProfile || !isOwned) return false;
            float angle = Mathf.DeltaAngle(yaw, desiredYaw);
            if (turnRequestBlocked)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(requestedTurnYaw, desiredYaw)) < 15f) return false;
                turnRequestBlocked = false;
            }
            if (Mathf.Abs(angle) < SprintProfile.turnThreshold) return false;
            var clip = angle >= 0 ? SprintProfile.sprintTurnRight : SprintProfile.sprintTurnLeft;
            if (!clip) return false;
            uint request = ++requestSequence;
            if (request == 0) request = ++requestSequence;
            pendingTurnRequest = request; requestedTurnYaw = desiredYaw;
            requestDeadline = Time.unscaledTime + 3f;
            if (!isServer)
            {
                predictedTurn = true; predictedTurnFrom = yaw; predictedTurnAngle = angle;
                localTurnClock.Reset();
            }
            SprintTurnRequests++;
            CmdSprintTurn(request, yaw, desiredYaw);
            return true;
        }

        [Command]
        void CmdSprintTurn(uint request, float fromYaw, float desiredYaw)
        {
            bool valid = (HeroSprint || heroSprintStopping) && !sprintTurning && !RollBlocksSprintTurn && CanHeroSprint && request != 0 && request != cancelledTurnRequest &&
                !float.IsNaN(fromYaw) && !float.IsInfinity(fromYaw) && !float.IsNaN(desiredYaw) && !float.IsInfinity(desiredYaw);
            float angle = valid ? Mathf.DeltaAngle(fromYaw, desiredYaw) : 0f;
            valid = valid && Mathf.Abs(angle) >= SprintProfile.turnThreshold &&
                Mathf.Abs(Mathf.DeltaAngle(_charInstance.lookInput.y, fromYaw)) < 90f &&
                (angle >= 0 ? SprintProfile.sprintTurnRight : SprintProfile.sprintTurnLeft);
            if (valid)
            {
                // Sprint owns the action now; discard the outgoing roll's remaining travel/overlay.
                if (IsFourWayRolling) CancelFourWayRoll();
                heroSprint = true; heroSprintStopping = false;
                sprintTurnFrom = fromYaw; sprintTurnAngle = angle; sprintAcceptedRequest = request;
                sprintTurnClock = 0f; localTurnClock.Reset(); sprintTurning = true;
                sprintTurnSession++; observedTurnSession = sprintTurnSession;
            }
            if (isOwned) FinishTurnRequest(request, valid);
            else TargetTurnRequestResult(connectionToClient, request, valid);
        }
        [TargetRpc]
        void TargetTurnRequestResult(NetworkConnectionToClient target, uint request, bool accepted) => FinishTurnRequest(request, accepted);
        void FinishTurnRequest(uint request, bool accepted)
        {
            if (pendingTurnRequest != request) return;
            if (accepted && !isServer) return; // Keep prediction until the matching SyncVar session arrives.
            pendingTurnRequest = 0;
            if (!accepted) { SprintTurnRejections++; predictedTurn = false; turnRequestBlocked = true; }
        }

        [Command]
        void CmdCancelTurnRequest(uint request)
        {
            cancelledTurnRequest = request;
            if (sprintAcceptedRequest == request) sprintTurning = false;
        }

        void CancelHeroSprint()
        {
            if (isServer) { heroSprint = heroSprintStopping = sprintTurning = false; sprintTurnClock = 0f; }
            predictedTurn = false; pendingTurnRequest = 0; turnRequestBlocked = false;
            localTurnClock.Reset();
        }
    }
}
