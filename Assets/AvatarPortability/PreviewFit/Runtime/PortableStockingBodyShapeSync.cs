using UnityEngine;
using AvatarPortability.Wardrobe;

namespace AvatarPortability.PreviewFit
{
    [DisallowMultipleComponent, DefaultExecutionOrder(11200)]
    public sealed class PortableStockingBodyShapeSync : MonoBehaviour
    {
        SkinnedMeshRenderer body, garment;
        Mesh bodyMesh, garmentMesh;
        int[] indices;
        int retries;
        void OnEnable() { Rebind(); }
        void OnTransformParentChanged() { if (isActiveAndEnabled) Rebind(); }
        public void Rebind() { retries = 30; Cache(); }
        void Cache()
        {
            body = null; indices = null;
            garment = GetComponentInChildren<SkinnedMeshRenderer>(true);
            var controller = GetComponentInParent<WardrobeController>();
            if (!controller || !garment || !garment.sharedMesh) return;
            var root = controller.avatarRoot ? controller.avatarRoot : controller.transform;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "Body_base" && !skin.transform.IsChildOf(transform) && !skin.GetComponentInParent<BakedWardrobeSlot>()) { body = skin; break; }
            if (!body || !body.sharedMesh) return;
            bodyMesh = body.sharedMesh; garmentMesh = garment.sharedMesh;
            indices = new int[garmentMesh.blendShapeCount];
            for (int i = 0; i < indices.Length; i++) indices[i] = bodyMesh.GetBlendShapeIndex(garmentMesh.GetBlendShapeName(i));
            Synchronize();
        }
        void LateUpdate()
        {
            if (!body && retries > 0) { retries--; Cache(); }
            if (body && garment && (body.sharedMesh != bodyMesh || garment.sharedMesh != garmentMesh)) Cache();
            Synchronize();
        }
        public void Synchronize()
        {
            if (!body || !garment || indices == null) return;
            for (int i = 0; i < indices.Length; i++) if (indices[i] >= 0) garment.SetBlendShapeWeight(i, body.GetBlendShapeWeight(indices[i]));
        }
    }
}
