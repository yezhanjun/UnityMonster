using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace Jotunnslayer
{
    // Encounter 97679 uses separate actors. The level9 stage helpers share one pose.
    public sealed class AlfheimBossEncounter : MonoBehaviour
    {
        const float PhaseTwoStartingHealth = .5f;
        const float CameraStart = .01666667f;
        const float FadeLength = .5f;
        const float FadeToBlackStart = 4.9666667f;
        const float TransitionMotionStart = .06666667f;
        const float TransitionDuration = 5.4666667f;

        [SerializeField] Transform firstAnchor;
        [SerializeField] Transform secondAnchor;
        [SerializeField] Transform playerReturnAnchor;
        [SerializeField, Range(.01f, 1f)] float phaseThreshold = .5f;
        [SerializeField] UnityEvent completed;

        CharacterInstance target;
        CharacterInstance first;
        CharacterInstance second;
        bool started;
        bool transitioning;
        bool cleared;
        float retryAt;
        float transitionStartedAt;
        float motionStartsAt;
        bool cameraStarted;
        Camera gameplayCamera;
        PlayerGameplayInput suspendedInput;
        bool ownsPlayerBlock;
        Camera transitionCamera;
        float returnFadeStartedAt = float.NegativeInfinity;
        bool motionStarted;
        GameObject transitionModel;
        PlayableGraph transitionGraph;

        internal CharacterInstance FirstActor => first;
        internal CharacterInstance SecondActor => second;
        internal bool Cleared => cleared;
        internal bool TransitionCameraPlaying => transitionCamera && transitionCamera.enabled;
        internal bool TransitionModelPlaying => transitionModel && transitionGraph.IsValid();

        internal void BindAnchors(Transform phaseOne, Transform phaseTwo, Transform playerReturn = null)
        {
            if (started) return;
            firstAnchor = phaseOne;
            secondAnchor = phaseTwo;
            playerReturnAnchor = playerReturn;
        }

        public bool TryBegin(CharacterInstance player)
        {
            if (started || !isActiveAndEnabled || !player || player.CurrentHealth <= 0 ||
                !firstAnchor) return false;

            first = Spawn(player, firstAnchor, Skeleton1HSpawn.Kind.AlfheimPhase1);
            if (!first) return false;

            target = player;
            started = true;
            first.Server_OnDamaged += OnFirstDamaged;
            return true;
        }

        void OnFirstDamaged(int health, CharacterPart part, AttackType type, Health attacker, int force)
        {
            if (transitioning || !first || health > first.MaxHealth * phaseThreshold) return;

            transitioning = true;
            transitionStartedAt = Time.time;
            motionStartsAt = transitionStartedAt + TransitionMotionStart;
            retryAt = transitionStartedAt + TransitionDuration;
            first.Server_OnDamaged -= OnFirstDamaged;
            first.RangeInvulnerable = true;
            SuspendPlayerInput();
            if (first.TryGetComponent<Skeleton1HCombat>(out var combat)) combat.enabled = false;
        }

        void Update()
        {
            if (!transitioning) return;
            if (!NetworkServer.activeHost || !target || target.CurrentHealth <= 0)
            {
                enabled = false;
                return;
            }

            float now = Time.time;
            if (!cameraStarted && now >= transitionStartedAt + CameraStart && now < retryAt)
            {
                cameraStarted = true;
                TryBeginTransitionCamera();
            }
            if (!motionStarted && now >= motionStartsAt)
            {
                motionStarted = true;
                if (first)
                {
                    var visual = first.GetComponentInChildren<Skeleton1HVisual>();
                    if (visual && !TryPlayTransitionModel(visual))
                        visual.Play(Skeleton1HVisual.Motion.PhaseChange, restart: true);
                }
            }
            if (now < retryAt) return;

            Transform transitionAnchor = secondAnchor ? secondAnchor : firstAnchor;
            second = Spawn(target, transitionAnchor, Skeleton1HSpawn.Kind.AlfheimPhase2);
            if (!second)
            {
                EndTransitionCamera(fadeBack: true);
                RestorePlayerInput();
                retryAt = Time.time + .5f;
                return;
            }

            ReturnPlayerToArena();
            EndTransitionCamera(fadeBack: true);
            RestorePlayerInput();
            second.CurrentHealth = Mathf.Max(1, Mathf.RoundToInt(second.MaxHealth * PhaseTwoStartingHealth));
            transitioning = false;
            second.Server_OnHealthDepleted += OnSecondKilled;
            StopTransitionModel();
            if (first) NetworkServer.Destroy(first.gameObject);
            first = null;
        }

        void SuspendPlayerInput()
        {
            if (!target || !target.isServer || !target.isOwned) return;

            if (PlayerGameplayInput.Instance && PlayerGameplayInput.Instance.isActiveAndEnabled)
            {
                suspendedInput = PlayerGameplayInput.Instance;
                suspendedInput.MovementInput = Vector2.zero;
                suspendedInput.enabled = false;
            }

            target.SetMovementInput(Vector2.zero);
            target.SetActionKeyCode(ActionCodes.Sprint, false);
            target.SetActionKeyCode(ActionCodes.BladeWalk, false);
            target.SetActionKeyCode(ActionCodes.Crouch, false);
            target.IsAiming = false;
            if (target.CharacterItemManager)
            {
                target.CharacterItemManager.UsePrimaryInput = false;
                target.CharacterItemManager.UseSecondaryInput = false;
            }

            if (target.Block) return;
            target.BlockCharacter(true);
            ownsPlayerBlock = true;
        }

        void RestorePlayerInput()
        {
            if (ownsPlayerBlock && target && target.isServer && NetworkServer.active)
                target.BlockCharacter(false);
            ownsPlayerBlock = false;
            if (suspendedInput) suspendedInput.enabled = true;
            suspendedInput = null;
        }

        void ReturnPlayerToArena()
        {
            if (!target || !target.isOwned || !firstAnchor) return;

            Quaternion anchorYaw = Quaternion.Euler(0f, firstAnchor.eulerAngles.y, 0f);
            Vector3 candidate = playerReturnAnchor ? playerReturnAnchor.position :
                firstAnchor.position + anchorYaw * new Vector3(7.82288f, 0f, 10.31771f);
            float yaw = playerReturnAnchor ? playerReturnAnchor.eulerAngles.y : firstAnchor.eulerAngles.y - 153.8543f;
            const int mask = GameManager.environmentLayer;
            if (!Physics.Raycast(candidate + Vector3.up * 2f, Vector3.down, out var hit, 7f,
                mask, QueryTriggerInteraction.Ignore) || hit.normal.y < .65f) return;

            Vector3 landing = hit.point + Vector3.up * .15f;
            if (Physics.CheckCapsule(landing + Vector3.up * .5f, landing + Vector3.up * 1.5f, .45f,
                mask, QueryTriggerInteraction.Ignore)) return;

            var controller = target.GetComponent<CharacterController>();
            bool controllerEnabled = controller && controller.enabled;
            if (controllerEnabled) controller.enabled = false;
            target.transform.SetPositionAndRotation(landing, Quaternion.Euler(0f, yaw, 0f));
            if (controllerEnabled) controller.enabled = true;
            target.lookInput = new Vector2(0f, yaw);
            if (suspendedInput) suspendedInput.LookInput = target.lookInput;
            if (target.TryGetComponent<NetworkTransformBase>(out var networkTransform))
                networkTransform.ResetState();
        }

        void TryBeginTransitionCamera()
        {
            var source = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (!source || !source.isActiveAndEnabled || !firstAnchor) return;

            Quaternion anchorYaw = Quaternion.Euler(0f, firstAnchor.eulerAngles.y, 0f);
            var view = new GameObject("Alfheim transition camera");
            transitionCamera = view.AddComponent<Camera>();
            transitionCamera.CopyFrom(source);
            view.transform.SetPositionAndRotation(
                firstAnchor.position + anchorYaw * new Vector3(12.51749f, 25.63f, 21.01704f),
                anchorYaw * new Quaternion(.09513964f, -.88568996f, .40363218f, .20876488f));
            transitionCamera.fieldOfView = 40f;
            transitionCamera.nearClipPlane = .1f;
            transitionCamera.farClipPlane = 250f;
            transitionCamera.lensShift = Vector2.zero;
            transitionCamera.ResetProjectionMatrix();
            gameplayCamera = source;
            source.enabled = false;
        }

        void EndTransitionCamera(bool fadeBack)
        {
            if (!transitionCamera && !gameplayCamera) return;
            if (transitionCamera)
            {
                transitionCamera.enabled = false;
                Destroy(transitionCamera.gameObject);
            }
            if (gameplayCamera) gameplayCamera.enabled = true;
            transitionCamera = null;
            gameplayCamera = null;
            if (fadeBack) returnFadeStartedAt = Time.time;
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;

            float now = Time.time;
            float opacity;
            if (transitionCamera)
            {
                float elapsed = now - transitionStartedAt;
                opacity = Mathf.Max(1f - Mathf.Clamp01((elapsed - CameraStart) / FadeLength),
                    Mathf.Clamp01((elapsed - FadeToBlackStart) / FadeLength));
            }
            else
            {
                opacity = 1f - Mathf.Clamp01((now - returnFadeStartedAt) / FadeLength);
            }
            if (opacity <= 0f) return;

            Color previousColor = GUI.color;
            int previousDepth = GUI.depth;
            GUI.depth = -100;
            GUI.color = new Color(0f, 0f, 0f, opacity);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = previousColor;
            GUI.depth = previousDepth;
        }

        bool TryPlayTransitionModel(Skeleton1HVisual source)
        {
            var prefab = Resources.Load<GameObject>("Jotunnslayer/AlfheimBoss/jotunn_spider_cs_mid");
            if (!prefab || !source.PhaseChangeClip || !firstAnchor) return false;

            // Relative pose between the level9 spawn helper and its cutscene actor.
            Quaternion anchorYaw = Quaternion.Euler(0f, firstAnchor.eulerAngles.y, 0f);
            Vector3 position = firstAnchor.position + anchorYaw * new Vector3(3.9291f, 0f, 2.6805f);
            Quaternion rotation = anchorYaw * Quaternion.Euler(0f, 30.0575f, 0f);
            var model = Instantiate(prefab, position, rotation);
            if (!model.TryGetComponent<Animator>(out var animator) || !animator.avatar ||
                !animator.avatar.isValid || animator.avatar.isHuman)
            {
                Destroy(model);
                return false;
            }

            model.transform.localScale = Vector3.one * .75f;
            source.ApplyAlfheimMaterials(model);
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            AnimationPlayableUtilities.PlayClip(animator, source.PhaseChangeClip, out transitionGraph);
            transitionModel = model;
            source.gameObject.SetActive(false);
            return true;
        }

        void StopTransitionModel()
        {
            if (transitionGraph.IsValid()) transitionGraph.Destroy();
            if (transitionModel) Destroy(transitionModel);
            transitionModel = null;
        }

        static CharacterInstance Spawn(CharacterInstance player, Transform anchor, Skeleton1HSpawn.Kind kind)
        {
            if (!anchor) return null;
            Vector3 forward = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
            if (forward.sqrMagnitude < .0001f) return null;
            return Skeleton1HSpawn.TrySpawn(player, anchor.position, Quaternion.LookRotation(forward), kind);
        }

        void OnSecondKilled(CharacterPart part, AttackType type, Health attacker, int force)
        {
            if (cleared || !second || !second.isServer) return;

            cleared = true;
            completed?.Invoke();
        }

        void OnDisable()
        {
            transitioning = false;
            EndTransitionCamera(fadeBack: false);
            RestorePlayerInput();
            returnFadeStartedAt = float.NegativeInfinity;
            StopTransitionModel();
            if (first)
            {
                first.Server_OnDamaged -= OnFirstDamaged;
                if (NetworkServer.active) NetworkServer.Destroy(first.gameObject);
                first = null;
            }

            if (second)
            {
                second.Server_OnHealthDepleted -= OnSecondKilled;
                if (NetworkServer.active) NetworkServer.Destroy(second.gameObject);
                second = null;
            }
        }
    }
}
