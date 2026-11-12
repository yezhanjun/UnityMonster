using System;
using UnityEngine;

namespace AvatarPortability.Expressions
{
    /// <summary>Explicit one-shot facial preset application. No animator, VRC, or Update loop.</summary>
    [ExecuteAlways, DisallowMultipleComponent]
    public sealed class WendyExpressionPlayer : MonoBehaviour
    {
        public WendyExpressionLibrary library;
        public SkinnedMeshRenderer faceRenderer;
        [HideInInspector] public int selectedPreset;
        [NonSerialized] private SkinnedMeshRenderer baselineRenderer;
        [NonSerialized] private Mesh baselineMesh;
        [NonSerialized] private WendyExpressionLibrary baselineLibrary;
        [NonSerialized] private int[] indices;
        [NonSerialized] private float[] baseline;
        public bool HasBaseline => baseline != null && baselineRenderer;
        public SkinnedMeshRenderer BaselineRenderer => baselineRenderer;

        public bool TryResolveFace()
        {
            if (faceRenderer) return true;
            string path = library && !string.IsNullOrEmpty(library.rendererPath) ? library.rendererPath : "Body";
            Transform node = transform.Find(path);
            if (node) faceRenderer = node.GetComponent<SkinnedMeshRenderer>();
            return faceRenderer;
        }

        public bool ApplySelected(out string report) { return Apply(selectedPreset, out report); }
        public bool Apply(string originalName, out string report)
        {
            RestoreIfTargetChanged();
            if (library && library.presets != null)
                for (int i = 0; i < library.presets.Length; i++)
                    if (library.presets[i] != null && library.presets[i].originalName == originalName) return Apply(i, out report);
            report = "未找到表情：" + WendyExpressionChinese.Name(originalName);
            return false;
        }

        public bool Apply(int presetIndex, out string report)
        {
            RestoreIfTargetChanged();
            if (!library || library.presets == null || presetIndex < 0 || presetIndex >= library.presets.Length || library.presets[presetIndex] == null)
            { report = "请选择有效的表情库与预设。"; return false; }
            if (!TryResolveFace() || !faceRenderer.sharedMesh)
            { report = "缺少 Body 面部蒙皮渲染器或其网格。"; return false; }
            ExpressionPreset preset = library.presets[presetIndex];
            string[] names = library.managedBlendshapes;
            if (names == null || names.Length == 0 || preset.weights == null || preset.weights.Length != names.Length)
            { report = "预设未包含完整的受管面部形态快照。"; return false; }
            var mapped = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                mapped[i] = faceRenderer.sharedMesh.GetBlendShapeIndex(names[i]);
                if (mapped[i] < 0) { report = "面部网格缺少受管形态：" + names[i]; return false; }
                if (float.IsNaN(preset.weights[i]) || float.IsInfinity(preset.weights[i])) { report = "预设形态权重不是有限数值：" + names[i]; return false; }
            }
            if (baseline == null)
            {
                baselineRenderer = faceRenderer; baselineMesh = faceRenderer.sharedMesh; baselineLibrary = library;
                indices = mapped; baseline = new float[names.Length];
                for (int i = 0; i < baseline.Length; i++) baseline[i] = faceRenderer.GetBlendShapeWeight(indices[i]);
            }
            for (int i = 0; i < mapped.Length; i++) faceRenderer.SetBlendShapeWeight(mapped[i], preset.weights[i]);
            selectedPreset = presetIndex;
            report = "已应用：" + WendyExpressionChinese.Name(preset.originalName) + "；已重置全部 " + mapped.Length + " 个受管形态。恢复按钮可还原预览前的面部状态。";
            return true;
        }

        private void RestoreIfTargetChanged()
        {
            if (baseline != null && (baselineRenderer != faceRenderer || baselineLibrary != library || !faceRenderer || baselineMesh != faceRenderer.sharedMesh)) Restore(out _);
        }

        public bool Restore(out string report)
        {
            bool restored = baseline != null && baselineRenderer && baselineRenderer.sharedMesh == baselineMesh;
            if (restored)
                for (int i = 0; i < baseline.Length; i++) baselineRenderer.SetBlendShapeWeight(indices[i], baseline[i]);
            baseline = null; indices = null; baselineRenderer = null; baselineMesh = null; baselineLibrary = null;
            report = restored ? "已恢复预览前的面部状态。" : "没有可恢复的有效面部状态记录。";
            return restored;
        }

        private void OnDisable() { Restore(out _); }
        private void OnDestroy() { Restore(out _); }
    }
}
