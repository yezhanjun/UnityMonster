using System;
using UnityEngine;

namespace AvatarPortability.Expressions
{
    [CreateAssetMenu(menuName = "Avatar Portability/Expression Library")]
    public sealed class WendyExpressionLibrary : ScriptableObject
    {
        public string rendererPath = "Body";
        public string[] managedBlendshapes = Array.Empty<string>();
        public float[] neutralWeights = Array.Empty<float>();
        public ExpressionPreset[] presets = Array.Empty<ExpressionPreset>();
    }

    [Serializable]
    public sealed class ExpressionPreset
    {
        public string originalName, category, sourceClipPath;
        public bool utility, timeVaryingSnapshot;
        public float sampleTime, sourceDuration;
        public float[] weights = Array.Empty<float>();
        public string[] skippedBindings = Array.Empty<string>();
    }
}
