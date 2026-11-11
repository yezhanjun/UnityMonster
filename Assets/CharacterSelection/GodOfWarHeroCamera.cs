using Cinemachine;
using MTPSKIT.UI;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Camera-only migration. Input, weapon authority and network movement stay on the TPS player.
    // Manual evaluation avoids adding a second Brain that would compete for the output Camera.
    [DefaultExecutionOrder(21000)]
    [RequireComponent(typeof(Camera))]
    public sealed class GodOfWarHeroCamera : MonoBehaviour
    {
        public static GodOfWarHeroCamera Instance { get; private set; }
        public static bool HandlesPlayer => Instance && Instance.enabled && EligibleCharacter;
        static CharacterInstance EligibleCharacter
        {
            get
            {
                var player = GameManager.myPlayerInstance;
                return player && player.HasHeroLoadout &&
                    GameManager.Gamemode is Gamemodes.RangeGamemode && player.MyCharacter &&
                    player.MyCharacter.CurrentHealth > 0 ? player.MyCharacter : null;
            }
        }

        [SerializeField, Min(0f)] float blendDuration = .25f;
        [SerializeField] float targetHeight = 1f;
        [SerializeField, Min(.01f)] float collisionRadius = .15f;
        Camera output;
        GameObject rig;
        Transform pivot;
        CinemachineVirtualCamera stand, aim;
        CharacterInstance character;
        float aimWeight, previousNear;
        bool bound;
        bool rigGodWar;
        internal bool GodWarMode => character && character.GetComponent<CharacterMotor>().GodWarControl;
        internal bool? AcceptanceAim;
        internal bool Aiming { get; private set; }
        internal CinemachineVirtualCamera Stand => stand;
        internal CinemachineVirtualCamera Aim => aim;

        void Awake() { Instance = this; output = GetComponent<Camera>(); }

        void EnsureRig()
        {
            bool mode = EligibleCharacter && EligibleCharacter.GetComponent<CharacterMotor>().GodWarControl;
            if (rig && rigGodWar == mode) return;
            if (rig) { rig.SetActive(false); Destroy(rig); }
            rigGodWar = mode;
            rig = new GameObject(mode ? "DemonHunter · GodOfWar" : "DemonHunter · Conventional");
            pivot = new GameObject("Local player camera target").transform;
            pivot.SetParent(rig.transform, false);
            string resource = mode ? "DemonHunter3C/" : "DemonHunter3C/Conventional/";
            stand = Instantiate(Resources.Load<GameObject>(resource + "Stand"), rig.transform)
                .GetComponent<CinemachineVirtualCamera>();
            aim = Instantiate(Resources.Load<GameObject>(resource + "Aim"), rig.transform)
                .GetComponent<CinemachineVirtualCamera>();
            // Only this adapter evaluates them, once per rendered frame.
            stand.enabled = aim.enabled = false;
            stand.Follow = stand.LookAt = aim.Follow = aim.LookAt = pivot;
        }

        void LateUpdate()
        {
            var next = EligibleCharacter;
            if (!next || !PlayerGameplayInput.Instance) { Release(); return; }
            EnsureRig();
            bool changed = next != character || !bound;
            if (!bound) { previousNear = output.nearClipPlane; bound = true; }
            character = next;
            rig.SetActive(true);
            var input = PlayerGameplayInput.Instance;
            var look = input.LookInput;
            look.x = Mathf.Clamp(look.x, -40f, 80f);
            input.LookInput = look;
            pivot.SetPositionAndRotation(character.transform.position + Vector3.up *
                (targetHeight - (character.IsCrouching ? .3f : 0f)), Quaternion.Euler(look.x, look.y, 0));
            bool aimRequested = Input.GetMouseButton(1);
            if (MTPSKIT.LocalAcceptanceClient.Requested && AcceptanceAim.HasValue) aimRequested = AcceptanceAim.Value;
            var motor = character.GetComponent<CharacterMotor>();
            bool hasGun = character.CharacterItemManager && character.CharacterItemManager.CurrentlyUsedItem is Gun;
            Aiming = input.enabled && ClientFrontend.GamePlayInput() && !character.IsRunning &&
                hasGun && aimRequested;
            aimWeight = changed ? (Aiming ? 1f : 0f) : Mathf.MoveTowards(aimWeight, Aiming ? 1f : 0f,
                blendDuration <= 0 ? 1f : Time.deltaTime / blendDuration);
            Evaluate(stand, changed);
            Evaluate(aim, changed);
            var state = CameraState.Lerp(stand.State, aim.State, Mathf.SmoothStep(0f, 1f, aimWeight));
            Vector3 position = state.FinalPosition;
            Vector3 origin = pivot.position;
            Vector3 offset = position - origin;
            float distance = offset.magnitude;
            // Use the project's environment mask; players, pets and trigger volumes cannot pull the camera in.
            if (distance > .001f && Physics.SphereCast(origin, collisionRadius, offset / distance,
                out var hit, distance, GameManager.environmentLayer, QueryTriggerInteraction.Ignore))
                position = origin + offset / distance * Mathf.Max(0f, hit.distance - .03f);
            Quaternion rotation = state.FinalOrientation;
            if (character.PlayerRecoil && character.PlayerRecoil.recoilObject)
                rotation *= character.PlayerRecoil.recoilObject.localRotation;
            transform.SetPositionAndRotation(position, rotation);
            output.fieldOfView = state.Lens.FieldOfView;
            output.nearClipPlane = state.Lens.NearClipPlane;
            // Existing gun raycasts use this transform. Do not leave them on the outgoing TPS rig.
            character.FPPCameraTarget = transform;
        }

        void Evaluate(CinemachineVirtualCamera camera, bool reset)
        {
            if (reset) camera.PreviousStateIsValid = false;
            camera.InternalUpdateCameraState(Vector3.up, reset ? -1f : Time.deltaTime);
        }

        void Release()
        {
            if (bound && output) output.nearClipPlane = previousNear;
            bound = false; character = null; Aiming = false; aimWeight = 0;
            if (rig) rig.SetActive(false);
        }
        void OnDisable() => Release();
        void OnDestroy()
        {
            Release();
            if (rig) Destroy(rig);
            if (Instance == this) Instance = null;
        }
    }
}
