using UnityEngine;

namespace CyberSamurai
{
    // The source animation supplies insertion weight; alignment is corrected after retargeting.
    public sealed class SamuraiWeaponSheathing : MonoBehaviour
    {
        public Transform blade;
        public Transform sheath;
        public Vector3 dockedPosition;
        public Quaternion dockedRotation = Quaternion.identity;
        [Range(0, 1)] public float sheathWeight = 1;

        void LateUpdate()
        {
            if (!blade || !sheath || sheathWeight <= 0) return;
            float weight = Mathf.Clamp01(sheathWeight);
            blade.SetPositionAndRotation(
                Vector3.Lerp(blade.position, sheath.TransformPoint(dockedPosition), weight),
                Quaternion.Slerp(blade.rotation, sheath.rotation * dockedRotation, weight));
        }
    }
}
