using System;
using UnityEngine;

namespace AvatarPortability.ExpressionClips
{
    [CreateAssetMenu(menuName = "Avatar Portability/Expression Clip Library")]
    public sealed class WendyExpressionClipLibrary : ScriptableObject
    {
        public ExpressionClipEntry[] entries = Array.Empty<ExpressionClipEntry>();
        public string rendererPath = "Body";
        public string[] managedBlendshapes = Array.Empty<string>();
        public int neutralIndex = -1;
    }

    [Serializable]
    public sealed class ExpressionClipEntry
    {
        public string originalName, category, sourceClipPath;
        public bool utility, timeVarying, loop;
        public AnimationClip clip;
    }
}
