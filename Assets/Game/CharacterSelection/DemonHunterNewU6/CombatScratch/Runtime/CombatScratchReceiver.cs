using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Unity.Collections;
using Unity.Jobs;

namespace MTPSKIT.Gameplay
{
    // Project native stamp masks in rest space, then reuse source skinning and blend shapes.
    // BakeMesh runs only on confirmed contact, never in Update/LateUpdate.
    [DisallowMultipleComponent]
    public sealed class CombatScratchReceiver : MonoBehaviour
    {
        public const int Capacity = 8;
        private static readonly int Centers = Shader.PropertyToID("_ScratchCenters");
        private static readonly int Tangents = Shader.PropertyToID("_ScratchTangents");
        private static readonly int Bitangents = Shader.PropertyToID("_ScratchBitangents");
        private static readonly int Normals = Shader.PropertyToID("_ScratchNormals");
        private static readonly int Timing = Shader.PropertyToID("_ScratchTiming");
        private static readonly int Glow = Shader.PropertyToID("_ScratchGlow");
        private static readonly int Scar = Shader.PropertyToID("_ScratchScar");
        private static readonly int Clock = Shader.PropertyToID("_ScratchClock");

        private sealed class Surface
        {
            public SkinnedMeshRenderer source, overlay;
            public Mesh baked, original, decalMesh;
            public Material[] materials;
            public Vector3[] rest, restNormals;
            public int[] triangles;
            public Vector3[] posedBuffer;
            public NativeArray<Vector3> nativePosed;
            public NativeArray<int> nativeTriangles;
            public NativeArray<CombatScratchProjection.Result> projection;
            public readonly List<Vector3> posed = new List<Vector3>();
            public readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
            public readonly Vector4[] centers = new Vector4[Capacity];
            public readonly Vector4[] tangents = new Vector4[Capacity];
            public readonly Vector4[] bitangents = new Vector4[Capacity];
            public readonly Vector4[] normals = new Vector4[Capacity];
            public readonly Vector4[] timing = new Vector4[Capacity];
            public readonly Vector4[] glow = new Vector4[Capacity];
            public readonly Vector4[] scar = new Vector4[Capacity];
            public int cursor;
            public float expires;
        }

        private Surface[] surfaces;

        private void Awake() => Prewarm();

        public static bool TrySpawn(Component target, Vector3 worldPoint, Vector3 worldSlash, CombatScratchConfig config,
            Vector3 worldApproach = default)
        {
            return TrySpawn(target, worldPoint, worldSlash, config, worldApproach, out _);
        }

        public static bool TrySpawn(Component target, Vector3 worldPoint, Vector3 worldSlash, CombatScratchConfig config,
            Vector3 worldApproach, out Vector3 surfacePoint)
        {
            surfacePoint = worldPoint;
            if (target == null || config == null || !config.enabled || config.material == null) return false;
            if (!target.TryGetComponent(out CombatScratchReceiver receiver))
                receiver = target.gameObject.AddComponent<CombatScratchReceiver>();
            return receiver.isActiveAndEnabled && receiver.Spawn(worldPoint, worldSlash, config, worldApproach, out surfacePoint);
        }

        // Call during enemy spawn/prewarm to move initialization allocations out of first contact.
        public void Prewarm()
        {
            if (surfaces != null) return;
            var renderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var prepared = new List<Surface>(renderers.Length);
            for (int i = 0; i < renderers.Length; i++)
            {
                var source = renderers[i];
                var mesh = source.sharedMesh;
                if (source.gameObject.name == "CombatScratchOverlay" || !source.gameObject.activeInHierarchy ||
                    mesh == null || !mesh.isReadable || source.forceRenderingOff ||
                    source.shadowCastingMode == ShadowCastingMode.ShadowsOnly) continue;
                var normals = mesh.normals;
                if (normals.Length != mesh.vertexCount) continue;
                var s = new Surface { source = source, original = mesh, baked = new Mesh { name = "ScratchContactBake" },
                    rest = mesh.vertices, restNormals = normals, triangles = mesh.triangles };
                s.posed.Capacity = mesh.vertexCount;
                s.posedBuffer = new Vector3[mesh.vertexCount];
                s.nativePosed = new NativeArray<Vector3>(mesh.vertexCount, Allocator.Persistent);
                s.nativeTriangles = new NativeArray<int>(s.triangles, Allocator.Persistent);
                s.projection = new NativeArray<CombatScratchProjection.Result>(1, Allocator.Persistent);
                prepared.Add(s);
            }
            surfaces = prepared.ToArray();
            if (surfaces.Length == 0)
                Debug.LogWarning("[CombatScratch] No readable skinned mesh found. Enable Read/Write on the enemy model importer.", this);
        }

        private static void EnsureOverlay(Surface s)
        {
            if (s.overlay != null) return;
            // Only clone surfaces actually contacted, not every clothing/accessory mesh.
            // Unity supplies deformed POSITION; spare UVs preserve the rest coordinates.
            s.decalMesh = Instantiate(s.original);
            s.decalMesh.name = "ScratchRestCoordinates";
            s.decalMesh.SetUVs(3, new List<Vector3>(s.rest));
            s.decalMesh.SetUVs(4, new List<Vector3>(s.restNormals));
            s.materials = new Material[s.original.subMeshCount];
            var go = new GameObject("CombatScratchOverlay");
            go.layer = s.source.gameObject.layer;
            go.transform.SetParent(s.source.transform, false);
            s.overlay = go.AddComponent<SkinnedMeshRenderer>();
            s.overlay.sharedMesh = s.decalMesh;
            s.overlay.bones = s.source.bones;
            s.overlay.rootBone = s.source.rootBone;
            s.overlay.localBounds = s.source.localBounds;
            s.overlay.updateWhenOffscreen = s.source.updateWhenOffscreen;
            s.overlay.shadowCastingMode = ShadowCastingMode.Off;
            s.overlay.receiveShadows = false;
            s.overlay.enabled = false;
        }

        private bool Spawn(Vector3 worldPoint, Vector3 worldSlash, CombatScratchConfig config, Vector3 worldApproach,
            out Vector3 surfacePoint)
        {
            surfacePoint = worldPoint;
            Prewarm();
            Surface best = null;
            int triangle = -1;
            Vector3 barycentric = default;
            float distance = config.maxSurfaceDistance * config.maxSurfaceDistance;
            float maxDistance = distance;
            bool projectFromAttacker = worldApproach.sqrMagnitude > 0.0001f;
            Vector3 approach = projectFromAttacker ? worldApproach.normalized : Vector3.zero;
            Vector3 rayOrigin = worldPoint - approach * config.maxSurfaceDistance;
            float rayDistance = float.PositiveInfinity;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var s = surfaces[i];
                if (s.source == null || !s.source.enabled || s.source.forceRenderingOff ||
                    s.source.shadowCastingMode == ShadowCastingMode.ShadowsOnly || !s.source.gameObject.activeInHierarchy ||
                    s.source.sharedMesh != s.original || s.source.bounds.SqrDistance(worldPoint) >
                    (projectFromAttacker ? maxDistance : distance)) continue;
                s.source.BakeMesh(s.baked, false);
                s.baked.GetVertices(s.posed);
                s.posed.CopyTo(s.posedBuffer);
                s.nativePosed.CopyFrom(s.posedBuffer);
                new CombatScratchProjection {
                    vertices = s.nativePosed, triangles = s.nativeTriangles,
                    localToWorld = s.source.transform.localToWorldMatrix,
                    point = worldPoint, rayOrigin = rayOrigin, approach = approach,
                    projectFromAttacker = projectFromAttacker, maxDistance = maxDistance,
                    distance = distance, rayDistance = rayDistance, output = s.projection
                }.Run();
                var projected = s.projection[0];
                if (projected.triangle < 0) continue;
                best = s; triangle = projected.triangle; barycentric = projected.weights;
                distance = projected.distance; rayDistance = projected.rayDistance;
            }
            if (best == null) return false;
            EnsureOverlay(best);
            int ia = best.triangles[triangle], ib = best.triangles[triangle + 1], ic = best.triangles[triangle + 2];
            Vector3 ra = best.rest[ia], rb = best.rest[ib], rc = best.rest[ic];
            Vector3 e1 = rb - ra, e2 = rc - ra;
            // Use the interpolated shading normal at the hit, so a tiny fold or
            // seam triangle cannot orient the whole stamp sideways into the skin.
            Vector3 normal = (best.restNormals[ia] * barycentric.x +
                              best.restNormals[ib] * barycentric.y +
                              best.restNormals[ic] * barycentric.z).normalized;
            if (normal.sqrMagnitude < 0.5f) return false;
            // Map the slash direction through this triangle's posed-to-rest differential.
            Vector3 p1 = best.source.transform.TransformVector(best.posed[ib] - best.posed[ia]);
            Vector3 p2 = best.source.transform.TransformVector(best.posed[ic] - best.posed[ia]);
            float d11 = Vector3.Dot(p1, p1), d12 = Vector3.Dot(p1, p2), d22 = Vector3.Dot(p2, p2);
            float determinant = d11 * d22 - d12 * d12;
            if (determinant < 1e-12f) return false;
            float v1 = Vector3.Dot(worldSlash, p1), v2 = Vector3.Dot(worldSlash, p2);
            Vector3 tangent = (e1 * ((v1 * d22 - v2 * d12) / determinant) +
                               e2 * ((v2 * d11 - v1 * d12) / determinant)).normalized;
            tangent = Vector3.ProjectOnPlane(tangent, normal).normalized;
            if (tangent.sqrMagnitude < 0.5f) tangent = e1.normalized;
            Vector3 bitangent = Vector3.Cross(normal, tangent).normalized;
            // The rest-to-posed differential includes bone scale (FBX bind rigs can
            // shrink vertices by 100x while the renderer Transform remains scale 1).
            float r11 = Vector3.Dot(e1, e1), r12 = Vector3.Dot(e1, e2), r22 = Vector3.Dot(e2, e2);
            float restDeterminant = r11 * r22 - r12 * r12;
            if (restDeterminant < 1e-12f) return false;
            float scale = Mathf.Max(0.00001f, MapRestVector(tangent, e1, e2, p1, p2,
                r11, r12, r22, restDeterminant).magnitude);
            float widthScale = Mathf.Max(0.00001f, MapRestVector(bitangent, e1, e2, p1, p2,
                r11, r12, r22, restDeterminant).magnitude);
            float depthScale = Mathf.Max(0.00001f, Mathf.Sqrt(scale * widthScale));
            int slot = best.cursor++ % Capacity;
            Vector3 center = ra * barycentric.x + rb * barycentric.y + rc * barycentric.z;
            best.centers[slot] = new Vector4(center.x, center.y, center.z, config.stampKind);
            best.tangents[slot] = tangent / Mathf.Max(0.001f, config.length / scale);
            best.bitangents[slot] = bitangent / Mathf.Max(0.001f, config.width / widthScale);
            best.normals[slot] = new Vector4(normal.x, normal.y, normal.z, config.depth / depthScale);
            float life = Mathf.Max(0.01f, config.lifetime);
            best.timing[slot] = new Vector4(Time.time, life, Mathf.Max(0.01f, config.glowDuration),
                Mathf.Clamp(config.fadeFraction, 0.01f, 1f));
            best.glow[slot] = config.glowColor;
            best.scar[slot] = config.scarColor;
            best.expires = Mathf.Max(best.expires, Time.time + life);
            best.block.SetVectorArray(Centers, best.centers);
            best.block.SetVectorArray(Tangents, best.tangents);
            best.block.SetVectorArray(Bitangents, best.bitangents);
            best.block.SetVectorArray(Normals, best.normals);
            best.block.SetVectorArray(Timing, best.timing);
            best.block.SetVectorArray(Glow, best.glow);
            best.block.SetVectorArray(Scar, best.scar);
            best.block.SetFloat(Clock, Time.time);
            if (best.materials.Length > 0 && best.materials[0] != config.material)
            {
                for (int i = 0; i < best.materials.Length; i++) best.materials[i] = config.material;
                best.overlay.sharedMaterials = best.materials;
            }
            best.overlay.SetPropertyBlock(best.block);
            best.overlay.enabled = true;
            surfacePoint = best.source.transform.TransformPoint(best.posed[ia] * barycentric.x +
                best.posed[ib] * barycentric.y + best.posed[ic] * barycentric.z);
            return true;
        }

        private void LateUpdate()
        {
            if (surfaces == null) return;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var s = surfaces[i];
                if (s.source == null || s.overlay == null) continue;
                s.overlay.enabled = Time.time < s.expires && s.source.enabled && !s.source.forceRenderingOff &&
                    s.source.shadowCastingMode != ShadowCastingMode.ShadowsOnly && s.source.gameObject.activeInHierarchy && s.source.sharedMesh == s.original;
                if (!s.overlay.enabled) continue;
                s.block.SetFloat(Clock, Time.time);
                s.overlay.SetPropertyBlock(s.block);
                s.overlay.localBounds = s.source.localBounds;
                for (int shape = 0; shape < s.overlay.sharedMesh.blendShapeCount; shape++)
                    s.overlay.SetBlendShapeWeight(shape, s.source.GetBlendShapeWeight(shape));
            }
        }

        private void OnDisable()
        {
            if (surfaces == null) return;
            for (int i = 0; i < surfaces.Length; i++)
            {
                var s = surfaces[i];
                s.expires = 0f;
                System.Array.Clear(s.timing, 0, Capacity);
                if (s.overlay != null) s.overlay.enabled = false;
            }
        }

        public void RefreshSurfaces()
        {
            ReleaseSurfaces();
            Prewarm();
        }

        private void OnDestroy() => ReleaseSurfaces();

        private void ReleaseSurfaces()
        {
            if (surfaces == null) return;
            for (int i = 0; i < surfaces.Length; i++)
            {
                if (surfaces[i].nativePosed.IsCreated) surfaces[i].nativePosed.Dispose();
                if (surfaces[i].nativeTriangles.IsCreated) surfaces[i].nativeTriangles.Dispose();
                if (surfaces[i].projection.IsCreated) surfaces[i].projection.Dispose();
                if (surfaces[i].baked != null) Destroy(surfaces[i].baked);
                if (surfaces[i].decalMesh != null) Destroy(surfaces[i].decalMesh);
                if (surfaces[i].overlay != null)
                {
                    surfaces[i].overlay.gameObject.SetActive(false);
                    Destroy(surfaces[i].overlay.gameObject);
                }
            }
            surfaces = null;
        }

        private static Vector3 MapRestVector(Vector3 direction, Vector3 e1, Vector3 e2,
            Vector3 p1, Vector3 p2, float r11, float r12, float r22, float determinant)
        {
            float a = Vector3.Dot(direction, e1), b = Vector3.Dot(direction, e2);
            return p1 * ((a * r22 - b * r12) / determinant) +
                   p2 * ((b * r11 - a * r12) / determinant);
        }

    }
}
