using UnityEngine;

namespace AvatarPortability.ExpressionClips
{
    /// <summary>One-time method-group listener; retains its index without a capturing lambda.</summary>
    [AddComponentMenu("")]
    public sealed class WendyExpressionClipUIAction : MonoBehaviour
    {
        internal WendyExpressionClipUI owner;
        internal int command, index;
        public void Invoke() { if (owner) owner.Execute(command, index); }
    }
}
