using System;
using System.Collections;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed class HeroReviewSmoke : MonoBehaviour
    {
        CharacterInstance character;
        CharacterMotor motor;
        PlayerGameplayInput input;
        bool driving, error;
        Vector2 movement;
        float yaw;
        static bool NetworkTest => Array.IndexOf(Environment.GetCommandLineArgs(), "--review-network") >= 0;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || !NetworkTest && Array.IndexOf(Environment.GetCommandLineArgs(), "--review-smoke") < 0) return;
            var go = new GameObject("Hero review acceptance"); DontDestroyOnLoad(go); go.AddComponent<HeroReviewSmoke>();
        }
        void OnEnable() => Application.logMessageReceived += Log;
        void OnDisable() { Time.timeScale = 1f; Application.logMessageReceived -= Log; }
        void Log(string m, string s, LogType t) { if (t == LogType.Error || t == LogType.Exception || t == LogType.Assert) error = true; }
        static void Check(bool ok, string m) { if (!ok) throw new InvalidOperationException("HERO_REVIEW_FAILED " + m); }
        void Update()
        {
            if (!driving) return;
            input.MovementInput = movement; input.LookInput = new Vector2(0, yaw);
            character.CharacterItemManager.UsePrimaryInput = character.CharacterItemManager.UseSecondaryInput = false;
            input.ApplyLocomotion(true, false);
        }
        IEnumerator Roll(byte direction)
        {
            if (NetworkServer.active) Check(motor.ServerStartFourWayRoll(direction, character.lookInput.y), "roll rejected");
            else motor.RequestFourWayRoll(direction == 0 ? Vector2.up : Vector2.left, character.lookInput.y);
            float deadline = Time.realtimeSinceStartup + 4;
            while (!motor.IsFourWayRolling && Time.realtimeSinceStartup < deadline) yield return null;
            Check(motor.IsFourWayRolling, "roll did not start");
        }
        IEnumerator FinishRoll()
        {
            float deadline = Time.realtimeSinceStartup + 5;
            while (motor.IsFourWayRolling && Time.realtimeSinceStartup < deadline) yield return null;
            Check(!motor.IsFourWayRolling, "roll never ended");
            yield return new WaitForSeconds(.3f);
        }
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 100;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            Check(GameManager.myPlayerInstance && GameManager.myPlayerInstance.MyCharacter, "player missing");
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, 49.5f, 0); floor.transform.localScale = new Vector3(300, 1, 300);
            if (NetworkTest && NetworkServer.active)
            {
                PlayerInstance peer = null;
                deadline = Time.realtimeSinceStartup + 60;
                while (!peer && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var p in GameManager.Players)
                        if (p.Value && p.Value != GameManager.myPlayerInstance && !p.Value.BOT && p.Value.MyCharacter) peer = p.Value;
                    yield return null;
                }
                Check(peer, "network peer missing");
                peer.ServerSetRangeHero(12);
                peer.MyCharacter.RangeInvulnerable = true;
                peer.MyCharacter.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
                yield break;
            }
            character = GameManager.myPlayerInstance.MyCharacter;
            motor = character.GetComponent<CharacterMotor>(); input = PlayerGameplayInput.Instance;
            if (NetworkServer.active)
            {
                GameManager.myPlayerInstance.ServerSetRangeHero(12);
                character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
                character.RangeInvulnerable = true;
            }
            yield return new WaitForSeconds(4f);
            Check(motor.UsesFourWayRoll && motor.SprintProfile, "context not bound");
            input.enabled = false; driving = true;
            character.transform.position = new Vector3(0, 50.1f, 0);
            yield return new WaitForSeconds(1f);
            if (NetworkTest)
            {
                movement = Vector2.up;
                yield return new WaitForSeconds(.5f);
                yield return Roll(0); yield return FinishRoll();
                Check(motor.HeroSprint, "client Sprint missing");
                foreach (float angle in new[] { 170f, -170f })
                {
                    int requests = motor.SprintTurnRequests;
                    float from = character.lookInput.y; yaw = from + angle;
                    deadline = Time.realtimeSinceStartup + 3;
                    while (!motor.SprintTurning && Time.realtimeSinceStartup < deadline) yield return null;
                    Check(motor.SprintTurning, "client turn missing");
                    int movingFrames = 0, frozenFrames = 0;
                    float previous = motor.SprintTurnTime;
                    deadline = Time.realtimeSinceStartup + 3;
                    while (motor.SprintTurning && Time.realtimeSinceStartup < deadline)
                    {
                        yield return null;
                        float current = motor.SprintTurnTime;
                        if (current > previous + .0001f) movingFrames++; else frozenFrames++;
                        Check(current >= previous, "turn clock went backwards"); previous = current;
                    }
                    yield return new WaitForSeconds(.6f);
                    Check(movingFrames > 8 && frozenFrames <= 2, "client clock stepped at network rate " + movingFrames + "/" + frozenFrames);
                    Check(motor.SprintTurnRequests - requests == 1, "duplicate turn commands");
                    Check(Mathf.Abs(Mathf.DeltaAngle(from + angle, character.lookInput.y)) < 5, "client facing wrong");
                    Debug.Log("HERO_REVIEW_CLIENT_TURN_PASSED frames=" + movingFrames + " frozen=" + frozenFrames + " rtt=" + NetworkTime.rtt);
                }
                // Force a local prediction which the authoritative threshold rejects.
                var turnProfile = motor.SprintProfile;
                float threshold = turnProfile.turnThreshold;
                int rejected = motor.SprintTurnRejections, sent = motor.SprintTurnRequests;
                turnProfile.turnThreshold = 20f; yaw = character.lookInput.y + 60f;
                yield return new WaitForSeconds(1.2f);
                turnProfile.turnThreshold = threshold;
                Check(motor.SprintTurnRejections == rejected + 1 && motor.SprintTurnRequests == sent + 1 && !motor.SprintTurning,
                    "rejected prediction retried or stayed active");
                Debug.Log("HERO_REVIEW_TURN_REJECTION_PASSED");
                var latency = Transport.active as LatencySimulation;
                Check(latency, "latency fixture missing");
                latency.latency = 4000f;
                int timeouts = motor.SprintTurnTimeouts; sent = motor.SprintTurnRequests;
                yaw = character.lookInput.y + 170f;
                yield return new WaitForSecondsRealtime(3.3f);
                Check(motor.SprintTurnTimeouts == timeouts + 1 && motor.SprintTurnRequests == sent + 1,
                    "timeout did not clear/deduplicate request");
                latency.latency = 200f;
                yield return new WaitForSecondsRealtime(5f);
                Check(!motor.SprintTurning, "late confirmation restarted expired turn");
                Debug.Log("HERO_REVIEW_TURN_TIMEOUT_PASSED");
                Check(!error, "network runtime errors"); Debug.Log("HERO_REVIEW_NETWORK_PASSED");
            }
            else
            {
                foreach (var directionInput in new[] { Vector2.up, Vector2.down, Vector2.left, Vector2.right, new Vector2(1, 1), new Vector2(-1, -1) })
                {
                    CharacterMotor.ResolveMovementRoll(directionInput, 37f, 112f, false, out byte rollDirection, out float rollYaw);
                    var expected = Quaternion.Euler(0, 37f, 0) * new Vector3(directionInput.x, 0, directionInput.y).normalized;
                    Check(rollDirection == 0 && Vector3.Dot(Quaternion.Euler(0, rollYaw, 0) * Vector3.forward, expected) > .999f,
                        "free roll does not face input");
                    CharacterMotor.ResolveMovementRoll(directionInput, 37f, 112f, true, out rollDirection, out rollYaw);
                    Check(rollDirection == CharacterMotor.SelectRollDirection(directionInput) && rollYaw == 37f,
                        "locked roll selection changed");
                }
                CharacterMotor.ResolveMovementRoll(Vector2.zero, 37f, 112f, false, out byte idleDirection, out float idleYaw);
                Check(idleDirection == 1 && idleYaw == 112f, "idle free roll does not follow body");
                Debug.Log("FREE_ROLL_INPUT_SELECTION_PASSED");
                var visual = character.GetComponent<RangeRobotVisual>();
                int resolutions = motor.WeaponProfileResolutions;
                var profile = motor.SprintProfile;
                // Warm the getters, then measure only the stable hot path on this thread.
                for (int i = 0; i < 100; i++) Check(motor.SprintProfile == profile && motor.UsesFourWayRoll, "cache warmup");
                long before = GC.GetAllocatedBytesForCurrentThread();
                for (int i = 0; i < 10000; i++) { var a = motor.SprintProfile; var b = motor.UsesFourWayRoll; }
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                Check(allocated == 0 && motor.WeaponProfileResolutions == resolutions, "stable context allocates/resolves " + allocated);
                Debug.Log("HERO_REVIEW_CONTEXT_PASSED allocations=" + allocated + " iterations=10000");
                yield return Roll(2); yield return new WaitForSeconds(.12f);
                Time.timeScale = 0f;
                float clock = motor.RollElapsed; Vector3 position = character.transform.position;
                yield return new WaitForSecondsRealtime(.3f);
                Check(Mathf.Abs(motor.RollElapsed-clock) < .00001f && Vector3.Distance(position, character.transform.position) < .01f, "paused roll advanced");
                Time.timeScale = .2f; clock = motor.RollElapsed;
                yield return new WaitForSecondsRealtime(.3f);
                Check(motor.RollElapsed-clock > .03f && motor.RollElapsed-clock < .1f, "slow motion roll rate wrong");
                Time.timeScale = 1f;
                character.CharacterAnimator.PlayHitStop(0f, .4f); clock = motor.RollElapsed; position = character.transform.position;
                yield return new WaitForSecondsRealtime(.2f);
                Check(Mathf.Abs(motor.RollElapsed-clock) < .00001f && Vector3.Distance(position, character.transform.position) < .01f, "hitstop roll advanced");
                character.CharacterAnimator.CancelHitStop(); yield return FinishRoll();
                Debug.Log("HERO_REVIEW_ROLL_CLOCK_PASSED");
                yield return Roll(2); yield return FinishRoll();
                Check(visual.RollPresentation.BuildCount == 1, "roll graph rebuilt");
                for (int i = 0; i < 2; i++)
                {
                    motor.Jump(); float peakLanding = 0f; bool sawExitBlend = false;
                    float until = Time.realtimeSinceStartup + 2f;
                    while (Time.realtimeSinceStartup < until)
                    {
                        yield return null;
                        peakLanding = Mathf.Max(peakLanding, visual.JumpPresentation.LandingWeight);
                        sawExitBlend |= visual.JumpPresentation.LandingBlendOut;
                    }
                    Check(peakLanding > .5f && sawExitBlend, "JumpEnd entry/exit blend missing peak=" + peakLanding);
                    Debug.Log("JUMP_END_BLEND_PASSED peak=" + peakLanding);
                }
                Check(visual.JumpPresentation.BuildCount == 1, "jump graph rebuilt");
                Debug.Log("HERO_REVIEW_GRAPH_REUSE_PASSED");
                character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsPistol"));
                yield return new WaitForSeconds(.5f);
                Check(motor.SprintProfile && motor.SprintProfile.weaponResource == "FpsPistol", "new gun profile missing");
                character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
                yield return new WaitForSeconds(.5f);
                // Use the real old TPS weapon if available; it must clear the former four-gun configuration.
                GameObject unsupported = null;
                foreach (var prefab in NetworkManager.singleton.spawnPrefabs)
                {
                    var oldGun = prefab ? prefab.GetComponent<Gun>() : null;
                    if (oldGun && !prefab.GetComponent<FpsGunBinding>()) { unsupported = prefab; break; }
                }
                Check(unsupported, "unsupported weapon fixture missing");
                character.CharacterItemManager.ServerEquipTestWeapon(unsupported);
                yield return new WaitForSeconds(.5f);
                Check(!motor.SprintProfile, "old Sprint profile survived weapon change");
                movement = Vector2.up; yield return Roll(0); yield return FinishRoll();
                Check(!motor.HeroSprint, "unsupported gun entered Sprint");
                Debug.Log("HERO_REVIEW_PROFILE_INVALIDATION_PASSED");
                Check(!error, "runtime errors"); Debug.Log("HERO_REVIEW_ACCEPTANCE_PASSED");
            }
            movement = Vector2.zero; character.SetMovementInput(Vector2.zero); driving = false; input.enabled = true;
        }
    }
}
