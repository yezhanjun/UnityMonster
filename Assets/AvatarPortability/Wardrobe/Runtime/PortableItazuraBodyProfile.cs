using UnityEngine;

namespace AvatarPortability.Wardrobe
{
    /// <summary>Author body rules owned by the visible Itazura pieces, with reversible writes.</summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(11010)]
    public sealed class PortableItazuraBodyProfile : MonoBehaviour
    {
        sealed class ShapeLease
        {
            public readonly string name;
            public int index = -1;
            public float before, lastApplied;
            public bool owned, requested, yielded;
            public ShapeLease(string name) { this.name = name; }
        }

        readonly ShapeLease[] shapes = {
            new ShapeLease("Toe_highheels"), new ShapeLease("Toe_heels"),
            new ShapeLease("S_Elbow"), new ShapeLease("Breasts_closer"),
            new ShapeLease("Breasts_big"), new ShapeLease("Breasts_small"),
            new ShapeLease("Breasts_flat")
        };
        SkinnedMeshRenderer body, pumps, arms, smallTop, smallBra;
        SkinnedMeshRenderer[] clothes;
        Mesh bodyMesh;
        bool initialized;

        void OnEnable() { Initialize(); Refresh(); }
        void LateUpdate() { Refresh(); }
        void OnTransformParentChanged()
        {
            Restore();
            initialized = false;
            if (isActiveAndEnabled) { Initialize(); Refresh(); }
        }

        void Initialize()
        {
            if (initialized) return;
            var controller = GetComponentInParent<WardrobeController>();
            if (!controller) return;
            Transform root = controller.avatarRoot ? controller.avatarRoot : controller.transform;
            body = null;
            foreach (var skin in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (skin.name == "Body_base" && !skin.transform.IsChildOf(transform) &&
                    !skin.GetComponentInParent<BakedWardrobeSlot>())
                { body = skin; break; }
            if (!body || !body.sharedMesh) return;
            clothes = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            pumps = arms = smallTop = smallBra = null;
            foreach (var skin in clothes)
                switch (skin.name)
                {
                    case "U_pumps": pumps = skin; break;
                    case "C_arm": arms = skin; break;
                    case "C_tops_Small": smallTop = skin; break;
                    case "U_bra_small": smallBra = skin; break;
                }
            bodyMesh = body.sharedMesh;
            foreach (var shape in shapes)
            {
                shape.index = bodyMesh.GetBlendShapeIndex(shape.name);
                shape.owned = shape.requested = shape.yielded = false;
            }
            initialized = true;
        }

        static bool Visible(SkinnedMeshRenderer skin) =>
            skin && skin.enabled && skin.gameObject.activeInHierarchy;

        /// <summary>Can be called after a UI toggle; normal runtime updates also run in LateUpdate.</summary>
        public void Refresh()
        {
            if (!isActiveAndEnabled || !Application.isPlaying) return;
            if (!initialized) return;
            if (!body || body.sharedMesh != bodyMesh)
            {
                // A replaced mesh has different indices; never restore stale indices into it.
                Restore(); initialized = false; Initialize();
                if (!initialized) return;
            }
            bool shoesVisible = Visible(pumps);
            bool smallVisible = Visible(smallTop) || Visible(smallBra);
            Apply(shapes[0], shoesVisible, 100);
            Apply(shapes[1], shoesVisible, 0);
            Apply(shapes[2], Visible(arms), 100);
            Apply(shapes[3], smallVisible, 0);
            Apply(shapes[4], smallVisible, 0);
            Apply(shapes[5], smallVisible, 100);
            Apply(shapes[6], smallVisible, 0);
        }

        void Apply(ShapeLease shape, bool requested, float value)
        {
            if (shape.index < 0) return;
            if (!requested)
            {
                Restore(shape);
                shape.requested = shape.yielded = false;
                return;
            }
            if (shape.owned)
            {
                if (body.GetBlendShapeWeight(shape.index) != shape.lastApplied)
                {
                    // Another writer took over. Yield until this piece is hidden/re-enabled.
                    shape.owned = false;
                    shape.yielded = true;
                }
                return;
            }
            if (shape.requested && shape.yielded) return;
            shape.requested = true;
            shape.before = body.GetBlendShapeWeight(shape.index);
            shape.lastApplied = value;
            body.SetBlendShapeWeight(shape.index, value);
            shape.owned = true;
        }

        void Restore(ShapeLease shape)
        {
            if (shape.owned && body && body.sharedMesh == bodyMesh && shape.index >= 0 &&
                body.GetBlendShapeWeight(shape.index) == shape.lastApplied)
                body.SetBlendShapeWeight(shape.index, shape.before);
            shape.owned = false;
        }

        void Restore()
        {
            foreach (var shape in shapes)
            {
                Restore(shape);
                shape.requested = shape.yielded = false;
            }
        }
        void OnDisable() { Restore(); initialized = false; }
        void OnDestroy() { Restore(); }
    }
}
