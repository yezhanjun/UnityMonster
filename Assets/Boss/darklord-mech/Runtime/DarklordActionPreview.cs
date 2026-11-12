using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace DarklordPreview
{
    // Scene-only animation inspection. The original Avatar, meshes and clips are used directly.
    public sealed class DarklordActionPreview : MonoBehaviour
    {
        public Animator animator;
        public AnimationClip[] clips;
        public string[] labels;
        public Camera previewCamera;
        public float pivotHeight = 2;
        [Range(.1f, 2)] public float speed = 1;
        public bool autoCycle;
        public bool paused;
        public int currentClip;
        [HideInInspector] public Rect additionalInputBlock;
        PlayableGraph graph;
        AnimationClipPlayable playable;
        AnimationPlayableOutput output;
        bool oldRootMotion, oldFireEvents;
        readonly List<int> filtered = new List<int>();
        GUIContent[] buttons;
        Vector2 scrollPosition;
        string search = "";
        int sourceFilter;
        static readonly string[] Sources = { "All", "SwordShield", "Paladin V2" };
        Vector3 pivot, initialPivot, initialPosition;
        Quaternion initialRotation;
        float distance, yaw, pitch;
        int dragButton = -1;
        bool cameraInitialized;
        Rect PanelRect => new Rect(16, 16, 310, Mathf.Max(250, Screen.height - 32));
        void EnsureCamera()
        {
            if (cameraInitialized) return;
            previewCamera = previewCamera ? previewCamera : Camera.main;
            if (!previewCamera) return;
            initialPosition = previewCamera.transform.position;
            initialRotation = previewCamera.transform.rotation;
            initialPivot = animator.transform.position + Vector3.up * pivotHeight;
            cameraInitialized = true;
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

        // Poll the project's active Input System, independent of IMGUI event consumption.
        void HandleCameraInput()
        {
            EnsureCamera();
            var mouse = Mouse.current;
            if (!previewCamera || mouse == null) return;
            Vector2 position = mouse.position.ReadValue();
            bool inView = previewCamera.pixelRect.Contains(position);
            var guiPosition = new Vector2(position.x, Screen.height - position.y);
            bool overControls = PanelRect.Contains(guiPosition) || additionalInputBlock.Contains(guiPosition);
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
#if UNITY_EDITOR
            RefreshEditorClips();
#endif
            if (!animator || !animator.avatar || !animator.avatar.isHuman || !animator.avatar.isValid || clips == null || clips.Length == 0)
            {
                Debug.LogError("Darklord preview needs a valid existing Humanoid Avatar and animation clips.", this);
                enabled = false;
                return;
            }
            oldRootMotion = animator.applyRootMotion;
            oldFireEvents = animator.fireEvents;
            animator.applyRootMotion = false;
            animator.fireEvents = false;
            buttons = new GUIContent[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                string label = labels != null && i < labels.Length ? labels[i] : clips[i].name;
                string prefix = label.StartsWith("Paladin/Humanoid_V2/", StringComparison.Ordinal) ? "[Paladin V2] "
                    : label.StartsWith("Paladin/", StringComparison.Ordinal) ? "[Paladin] " : "";
                buttons[i] = new GUIContent(prefix + clips[i].name, label);
            }
            Filter();
            graph = PlayableGraph.Create("Darklord Sword and Shield Preview");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            output = AnimationPlayableOutput.Create(graph, "Existing Humanoid Animator", animator);
            Play(Mathf.Clamp(currentClip, 0, clips.Length - 1));
            graph.Play();
        }

#if UNITY_EDITOR
        // Read assets when entering preview so an already open scene cannot retain an old list.
        public void RefreshEditorClips()
        {
            const string swordRoot = "Assets/Sword and Shield Animation Pack/Animation/Humanoid";
            const string paladinRoot = "Assets/Paladin_Anim_Set/Animations/Humanoid_V2";
            var newClips = new List<AnimationClip>();
            var newLabels = new List<string>();
            string[] featured = { "Idle_Combat", "Walk_Combat_Loop_F_0", "Run_Combat_Loop_F_0", "Combo_Attack_01_All", "Combo_Attack_02_All", "Combo_Attack_03_All", "Combo_Attack_04_All", "Block_Loop", "Block_Hit", "Dodge_Combat_F_0", "Roll_Combat_F_0", "Hit_Combat_F", "Turn_Combat_180_R", "Idle" };
            foreach (string root in new[] { swordRoot, paladinRoot })
            {
                if (!UnityEditor.AssetDatabase.IsValidFolder(root)) continue;
                var paths = new HashSet<string>();
                foreach (string guid in UnityEditor.AssetDatabase.FindAssets("t:AnimationClip", new[] { root }))
                    paths.Add(UnityEditor.AssetDatabase.GUIDToAssetPath(guid));
                var sorted = new List<string>(paths); sorted.Sort(StringComparer.Ordinal);
                var entries = new List<AnimationClip>();
                foreach (string path in sorted)
                    foreach (var asset in UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path))
                        if (asset is AnimationClip clip && clip.isHumanMotion && !clip.name.StartsWith("__preview__", StringComparison.Ordinal)) entries.Add(clip);
                if (root == swordRoot)
                    entries.Sort((a, b) =>
                    {
                        int ai = Array.IndexOf(featured, a.name), bi = Array.IndexOf(featured, b.name);
                        int order = (ai < 0 ? int.MaxValue : ai).CompareTo(bi < 0 ? int.MaxValue : bi);
                        return order != 0 ? order : StringComparer.Ordinal.Compare(UnityEditor.AssetDatabase.GetAssetPath(a), UnityEditor.AssetDatabase.GetAssetPath(b));
                    });
                foreach (var clip in entries)
                {
                    newClips.Add(clip);
                    string path = UnityEditor.AssetDatabase.GetAssetPath(clip);
                    newLabels.Add(root == swordRoot ? "SwordShield/" + path.Substring(root.Length + 1) : "Paladin/Humanoid_V2/" + path.Substring(root.Length + 1));
                }
            }
            var previous = clips != null && currentClip >= 0 && currentClip < clips.Length ? clips[currentClip] : null;
            clips = newClips.ToArray(); labels = newLabels.ToArray();
            int selected = previous ? Array.IndexOf(clips, previous) : -1;
            currentClip = selected >= 0 ? selected : 0;
            sourceFilter = Mathf.Clamp(sourceFilter, 0, Sources.Length - 1);
        }
#endif

        public void Play(int index)
        {
            if (!graph.IsValid() || index < 0 || index >= clips.Length) return;
            if (playable.IsValid()) graph.DestroyPlayable(playable);
            currentClip = index;
            playable = AnimationClipPlayable.Create(graph, clips[index]);
            playable.SetApplyFootIK(false);
            playable.SetApplyPlayableIK(false);
            output.SetSourcePlayable(playable);
            playable.SetTime(0);
            playable.SetSpeed(paused ? 0 : speed);
        }

        void Update()
        {
            HandleCameraInput();
            if (!playable.IsValid()) return;
            playable.SetSpeed(paused ? 0 : speed);
            if (!paused && playable.GetTime() >= clips[currentClip].length)
            {
                if (autoCycle) Play((currentClip + 1) % clips.Length);
                else if (!clips[currentClip].isLooping) playable.SetTime(0);
            }
        }

        void Filter()
        {
            filtered.Clear();
            if (clips == null) return;
            for (int i = 0; i < clips.Length; i++)
            {
                string label = labels != null && i < labels.Length ? labels[i] : "";
                bool paladin = label.StartsWith("Paladin/", StringComparison.Ordinal);
                if (sourceFilter == 1 && paladin || sourceFilter == 2 && !paladin) continue;
                if (string.IsNullOrEmpty(search) || clips[i].name.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) filtered.Add(i);
            }
        }

        void OnGUI()
        {
            if (buttons == null || buttons.Length == 0) return;
            GUILayout.BeginArea(PanelRect, GUI.skin.box);
            GUILayout.Label("Darklord / Sword and Shield");
            GUILayout.Label(clips[currentClip].name);
            paused = GUILayout.Toggle(paused, "Pause");
            autoCycle = GUILayout.Toggle(autoCycle, "Auto cycle");
            GUILayout.Label("Playback speed");
            speed = GUILayout.HorizontalSlider(speed, .1f, 2);
            if (playable.IsValid())
            {
                float duration = clips[currentClip].length;
                float time = (float)(playable.GetTime() % Mathf.Max(.001f, duration));
                float selected = GUILayout.HorizontalSlider(time, 0, duration);
                if (Mathf.Abs(selected - time) > .001f) { playable.SetTime(selected); graph.Evaluate(0); }
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Previous")) Play((currentClip + clips.Length - 1) % clips.Length);
            if (GUILayout.Button("Restart")) Play(currentClip);
            if (GUILayout.Button("Next")) Play((currentClip + 1) % clips.Length);
            GUILayout.EndHorizontal();
            GUILayout.Label("LMB / RMB: orbit | MMB: pan\nWheel: zoom");
            if (GUILayout.Button("Reset view")) ResetView();
            int nextSource = GUILayout.SelectionGrid(sourceFilter, Sources, 3);
            if (nextSource != sourceFilter) { sourceFilter = nextSource; scrollPosition = Vector2.zero; Filter(); }
            string nextSearch = GUILayout.TextField(search);
            if (nextSearch != search) { search = nextSearch; scrollPosition = Vector2.zero; Filter(); }
            GUILayout.Label(filtered.Count + " / " + clips.Length + " clips");
            scrollPosition = GUILayout.BeginScrollView(scrollPosition);
            for (int i = 0; i < filtered.Count; i++)
            {
                int index = filtered[i];
                if (GUILayout.Button(buttons[index])) { autoCycle = false; Play(index); }
            }
            GUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        void OnDisable()
        {
            dragButton = -1;
            cameraInitialized = false;
            if (graph.IsValid()) graph.Destroy();
            if (animator) { animator.applyRootMotion = oldRootMotion; animator.fireEvents = oldFireEvents; }
        }
    }
}
