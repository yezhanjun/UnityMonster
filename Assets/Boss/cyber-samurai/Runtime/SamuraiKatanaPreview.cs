using UnityEngine;
using MagicaCloth2;
using UnityEngine.InputSystem;

namespace CyberSamurai
{
    // Demonstration controls only. Combat transitions and damage belong to the game.
    public sealed class SamuraiKatanaPreview : MonoBehaviour
    {
        public Animator animator;
        public MagicaCloth cloth;
        public SamuraiClothBootstrap bootstrap;
        public string[] states;
        public bool autoCycle;
        [Range(.1f, 1.5f)] public float speed = 1;
        public int currentState;
        [HideInInspector] public Rect additionalInputBlock;
        int[] hashes;
        float elapsed;
        bool started;
        GUIContent[] buttons;
        static readonly Rect ControlsRect = new Rect(16, 16, 250, 500);
        Camera previewCamera;
        Vector3 pivot, initialPivot, initialPosition;
        Quaternion initialRotation;
        float distance, yaw, pitch;
        int dragButton = -1;
        int pendingState = -1;
        bool drawn;

        void EnsureCamera()
        {
            if (previewCamera) return;
            previewCamera = Camera.main;
            if (!previewCamera) return;
            initialPosition = previewCamera.transform.position;
            initialRotation = previewCamera.transform.rotation;
            initialPivot = animator.transform.position + Vector3.up * 1.25f;
            ResetView();
        }

        void ResetView()
        {
            if (!previewCamera) return;
            pivot = initialPivot;
            distance = Vector3.Distance(initialPosition, pivot);
            yaw = initialRotation.eulerAngles.y;
            pitch = Mathf.DeltaAngle(0, initialRotation.eulerAngles.x);
            previewCamera.transform.SetPositionAndRotation(initialPosition, initialRotation);
            dragButton = -1;
        }

        void OnApplicationFocus(bool focused) { if (!focused) dragButton = -1; }
        void OnDisable() { dragButton = -1; previewCamera = null; }

        // Poll the project's active Input System, independent of IMGUI event consumption.
        void HandleCameraInput()
        {
            EnsureCamera();
            var mouse = Mouse.current;
            if (!previewCamera || mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            bool inView = previewCamera.pixelRect.Contains(position);
            var guiPosition = new Vector2(position.x, Screen.height - position.y);
            bool overControls = ControlsRect.Contains(guiPosition) || additionalInputBlock.Contains(guiPosition);
            if (!inView) { dragButton = -1; return; }
            bool held = dragButton == 0 ? mouse.leftButton.isPressed : dragButton == 1 ? mouse.rightButton.isPressed : dragButton == 2 && mouse.middleButton.isPressed;
            if (!held) dragButton = -1;
            bool began = false;
            if (!overControls && dragButton < 0)
            {
                if (mouse.middleButton.wasPressedThisFrame) dragButton = 2;
                else if (mouse.rightButton.wasPressedThisFrame) dragButton = 1;
                else if (mouse.leftButton.wasPressedThisFrame) dragButton = 0;
                began = dragButton >= 0;
            }
            if (dragButton >= 0 && !began)
            {
                Vector2 delta = mouse.delta.ReadValue();
                if (dragButton == 2)
                {
                    float unitsPerPixel = 2 * distance * Mathf.Tan(previewCamera.fieldOfView * Mathf.Deg2Rad * .5f) / Mathf.Max(1, previewCamera.pixelHeight);
                    pivot -= (previewCamera.transform.right * delta.x + previewCamera.transform.up * delta.y) * unitsPerPixel;
                }
                else
                {
                    yaw += delta.x * .3f;
                    pitch = Mathf.Clamp(pitch - delta.y * .3f, -80, 80);
                }
                ApplyView();
            }
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0 && !overControls)
            {
                distance = Mathf.Clamp(distance * Mathf.Exp(-scroll * .004f), .4f, 20);
                ApplyView();
            }
        }

        void ApplyView()
        {
            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            previewCamera.transform.SetPositionAndRotation(pivot - rotation * Vector3.forward * distance, rotation);
        }

        void OnEnable()
        {
            if (states == null) states = System.Array.Empty<string>();
            hashes = new int[states.Length]; buttons = new GUIContent[states.Length];
            for (int i = 0; i < states.Length; i++) { hashes[i] = Animator.StringToHash(states[i]); buttons[i] = new GUIContent(states[i]); }
            started = false; pendingState = -1; dragButton = -1;
        }

        void Update()
        {
            HandleCameraInput();
            if (!bootstrap.Ready || states.Length == 0) return;
            if (!started) { started = true; PlayImmediate(0); }
            animator.speed = speed;
            elapsed += Time.deltaTime * speed;
            var state = animator.GetCurrentAnimatorStateInfo(0);
            if (!animator.IsInTransition(0) && state.shortNameHash == hashes[currentState] && state.normalizedTime >= .99f)
            {
                string name = states[currentState];
                if (name == "Idle_To_Idle_Combat" || name == "Idle_Combat_To_Idle")
                {
                    int next = pendingState >= 0 ? pendingState : System.Array.IndexOf(states, name == "Idle_To_Idle_Combat" ? "Idle_Combat" : "Idle");
                    pendingState = -1; PlayImmediate(next); return;
                }
                if (name.StartsWith("Combo_")) { PlayImmediate(0); return; }
            }
            if (autoCycle && elapsed > Mathf.Max(3, state.length + .5f)) Play((currentState + 1) % states.Length);
        }

        public void Play(int index)
        {
            if (hashes == null || index < 0 || index >= hashes.Length || !bootstrap.Ready) return;
            string target = states[index];
            bool wantsDrawn = target == "Idle_Combat";
            bool wantsSheathed = target == "Idle" || target == "Walk_Loop_F_0" || target == "Run_Loop_F_0" || target.StartsWith("Combo_");
            pendingState = -1;
            if ((!drawn && wantsDrawn) || (drawn && wantsSheathed))
            {
                pendingState = index;
                PlayImmediate(System.Array.IndexOf(states, wantsDrawn ? "Idle_To_Idle_Combat" : "Idle_Combat_To_Idle"));
            }
            else PlayImmediate(index);
        }

        public void PlayImmediate(int index)
        {
            if (hashes == null || index < 0 || index >= hashes.Length || !bootstrap.Ready) return;
            currentState = index; elapsed = 0;
            drawn = states[index] == "Idle_To_Idle_Combat" || states[index] == "Idle_Combat";
            animator.CrossFadeInFixedTime(hashes[index], .2f, 0, 0);
        }

        void OnGUI()
        {
            GUILayout.BeginArea(ControlsRect, GUI.skin.box);
            GUILayout.Label("Cyber Samurai / Katana preview");
            if (!bootstrap.Ready) GUILayout.Label(bootstrap.Failed ? "Cloth build failed - check Console" : "Building cloth...");
            autoCycle = GUILayout.Toggle(autoCycle, "Auto cycle");
            speed = GUILayout.HorizontalSlider(speed, .1f, 1.5f);
            for (int i = 0; i < buttons.Length; i++) if (GUILayout.Button(buttons[i])) { autoCycle = false; Play(i); }
            bool simulate = GUILayout.Toggle(cloth.SerializeData.blendWeight > 0, "Cloth simulation");
            float weight = simulate ? 1 : 0;
            if (cloth.SerializeData.blendWeight != weight)
                foreach (var item in bootstrap.cloths) { item.SerializeData.blendWeight = weight; item.SetParameterChange(); }
            if (GUILayout.Button("Reset cloth")) foreach (var item in bootstrap.cloths) item.ResetCloth();
            GUILayout.Label("LMB / RMB drag: orbit\nMMB drag: pan | Wheel: zoom");
            if (GUILayout.Button("Reset view")) ResetView();
            GUILayout.EndArea();
        }
    }
}
