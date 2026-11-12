using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace AnimationPreviewTools.Editor
{
    /// <summary>Starts Unity's own Inspector preview once when a clip is selected.</summary>
    [InitializeOnLoad]
    internal static class AnimationClipInspectorAutoPlay
    {
        const string MenuPath = "Tools/Animation/选中 Clip 自动播放";
        static readonly string PreferenceKey = "AnimationPreviewTools.ClipAutoPlay." + Application.dataPath;
        const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        static readonly Dictionary<int, object> StartedEditors = new Dictionary<int, object>();
        static readonly List<UnityEditor.Editor> ModelEditors = new List<UnityEditor.Editor>();
        static readonly PropertyInfo ActiveTabProperty;
        static readonly FieldInfo NestedClipEditorField;
        static readonly FieldInfo PreviewField;
        static readonly FieldInfo TimeField;
        static readonly PropertyInfo PlayingProperty;
        static readonly FieldInfo CurrentTimeField;
        static readonly FieldInfo StartTimeField;
        static AnimationClip selectedClip;
        static UnityEngine.Object selectedAsset;
        static string selectedModelPath;
        static double retryUntil;
        static double nextRetry;
        static double startAfter;
        static bool warned;

        static AnimationClipInspectorAutoPlay()
        {
            // Unity 2022.3 AnimationClipEditor -> AvatarPreview -> TimeControl.
            // Keep the native Inspector, avatar picker, scrubber and pause controls intact.
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type editorType = assembly.GetType("UnityEditor.AnimationClipEditor");
                if (editorType == null) continue;
                PreviewField = editorType.GetField("m_AvatarPreview", Members);
                Type previewType = PreviewField?.FieldType;
                TimeField = previewType?.GetField("timeControl", Members);
                Type timeType = TimeField?.FieldType;
                PlayingProperty = timeType?.GetProperty("playing", Members);
                CurrentTimeField = timeType?.GetField("currentTime", Members);
                StartTimeField = timeType?.GetField("startTime", Members);
                break;
            }
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type tabbedEditor = assembly.GetType("UnityEditor.AssetImporterTabbedEditor");
                Type clipTab = assembly.GetType("UnityEditor.ModelImporterClipEditor");
                if (tabbedEditor != null) ActiveTabProperty = tabbedEditor.GetProperty("activeTab", Members);
                if (clipTab != null) NestedClipEditorField = clipTab.GetField("m_AnimationClipEditor", Members);
            }
            Selection.selectionChanged += OnSelectionChanged;
            UnityEditor.Editor.finishedDefaultHeaderGUI += OnHeader;
            EditorApplication.update += Update;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            EditorApplication.delayCall += OnSelectionChanged;
        }

        internal static bool Enabled => EditorPrefs.GetBool(PreferenceKey, true);

        [MenuItem(MenuPath)]
        static void Toggle()
        {
            EditorPrefs.SetBool(PreferenceKey, !Enabled);
            OnSelectionChanged();
        }

        [MenuItem(MenuPath, true)]
        static bool ValidateToggle()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return true;
        }

        static void OnPlayModeChanged(PlayModeStateChange state)
        {
            selectedClip = null;
            selectedAsset = null;
            selectedModelPath = null;
            ModelEditors.Clear();
            StartedEditors.Clear();
            if (state == PlayModeStateChange.EnteredEditMode) OnSelectionChanged();
        }

        static void OnSelectionChanged()
        {
            StartedEditors.Clear();
            ModelEditors.Clear();
            selectedAsset = Selection.activeObject;
            selectedClip = null;
            selectedModelPath = null;
            if (Enabled && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                selectedClip = selectedAsset as AnimationClip;
                if (selectedAsset is GameObject && AssetDatabase.Contains(selectedAsset))
                {
                    string path = AssetDatabase.GetAssetPath(selectedAsset);
                    if (AssetImporter.GetAtPath(path) is ModelImporter) selectedModelPath = path;
                }
            }
            retryUntil = EditorApplication.timeSinceStartup + 5;
            startAfter = EditorApplication.timeSinceStartup + 0.2;
            nextRetry = 0;
        }

        static void OnHeader(UnityEditor.Editor editor)
        {
            // A collapsed preview or a newly opened Inspector can initialize later.
            // The instance guard also preserves a manual pause on subsequent repaints.
            if (!Enabled || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (selectedClip != null && editor.target == selectedClip) TryStart(editor);
            else if (IsSelectedModelEditor(editor))
            {
                if (!ModelEditors.Contains(editor)) ModelEditors.Add(editor);
                TryStartModel(editor);
            }
        }

        static void Update()
        {
            if (Selection.activeObject != selectedAsset) OnSelectionChanged();
            if (!Enabled || (selectedClip == null && selectedModelPath == null) || EditorApplication.isCompiling ||
                EditorApplication.isPlayingOrWillChangePlaymode) return;
            double now = EditorApplication.timeSinceStartup;
            if (now < nextRetry) return;
            nextRetry = now + 0.15;
            if (now <= retryUntil)
                foreach (UnityEditor.Editor editor in Resources.FindObjectsOfTypeAll<UnityEditor.Editor>())
                {
                    if (editor == null) continue;
                    if (selectedClip != null && editor.target == selectedClip) TryStart(editor);
                    else if (IsSelectedModelEditor(editor) && !ModelEditors.Contains(editor)) ModelEditors.Add(editor);
                }
            // FBX clip-list changes do not raise Selection.selectionChanged. Watch only
            // the matching, cached import inspectors; a new native preview starts once.
            for (int i = ModelEditors.Count - 1; i >= 0; i--)
            {
                var editor = ModelEditors[i];
                if (editor == null) { ModelEditors.RemoveAt(i); continue; }
                TryStartModel(editor);
            }
        }

        static bool IsSelectedModelEditor(UnityEditor.Editor editor)
        {
            return selectedModelPath != null && editor != null &&
                editor.target is ModelImporter importer && importer.assetPath == selectedModelPath;
        }

        static void TryStartModel(UnityEditor.Editor editor)
        {
            if (ActiveTabProperty == null || NestedClipEditorField == null ||
                !ActiveTabProperty.DeclaringType.IsInstanceOfType(editor)) return;
            try
            {
                object tab = ActiveTabProperty.GetValue(editor);
                // Only drive the visible Animation tab. Never switch tabs or apply/reimport settings.
                if (tab == null || !NestedClipEditorField.DeclaringType.IsInstanceOfType(tab)) return;
                var clipEditor = NestedClipEditorField.GetValue(tab) as UnityEditor.Editor;
                if (clipEditor != null) TryStart(clipEditor);
            }
            catch (Exception exception) { WarnOnce("无法访问 FBX 动画预览：" + exception.GetBaseException().Message); }
        }

        static void TryStart(UnityEditor.Editor editor)
        {
            if (!Enabled || EditorApplication.timeSinceStartup < startAfter) return;
            try
            {
                if (PlayingProperty == null || CurrentTimeField == null || StartTimeField == null)
                {
                    WarnOnce("当前 Unity 版本的原生预览接口不兼容，自动播放已跳过。");
                    return;
                }
                object time = GetTimeControl(editor);
                if (time == null) return; // Preview is initialized lazily by the native Inspector.
                if (StartedEditors.TryGetValue(editor.GetInstanceID(), out object startedTime) &&
                    ReferenceEquals(time, startedTime)) return;
                CurrentTimeField.SetValue(time, StartTimeField.GetValue(time));
                PlayingProperty.SetValue(time, true);
                StartedEditors[editor.GetInstanceID()] = time;
                editor.Repaint();
            }
            catch (Exception exception)
            {
                WarnOnce("无法启动动画预览：" + exception.GetBaseException().Message);
            }
        }

        internal static object GetTimeControl(UnityEditor.Editor editor)
        {
            if (PreviewField == null || !PreviewField.DeclaringType.IsInstanceOfType(editor)) return null;
            object preview = PreviewField.GetValue(editor);
            return preview == null ? null : TimeField?.GetValue(preview);
        }

        internal static bool IsPlaying(object time) => (bool)PlayingProperty.GetValue(time);
        internal static void SetPlaying(object time, bool playing) => PlayingProperty.SetValue(time, playing);
        internal static float GetTime(object time) => (float)CurrentTimeField.GetValue(time);

        static void WarnOnce(string message)
        {
            if (warned) return;
            warned = true;
            Debug.LogWarning("[Animation Clip Auto Play] " + message);
        }
    }
}
