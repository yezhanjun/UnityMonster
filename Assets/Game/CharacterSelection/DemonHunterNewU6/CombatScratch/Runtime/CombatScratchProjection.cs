using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // The projection stays exact, but triangle arithmetic runs as native Burst code.
    [BurstCompile(CompileSynchronously = true)]
    internal struct CombatScratchProjection : IJob
    {
        [ReadOnly] public NativeArray<Vector3> vertices;
        [ReadOnly] public NativeArray<int> triangles;
        public Matrix4x4 localToWorld;
        public Vector3 point, rayOrigin, approach;
        public float maxDistance, distance, rayDistance;
        public bool projectFromAttacker;
        public NativeArray<Result> output;
        internal struct Result
        {
            public int triangle;
            public Vector3 weights;
            public float distance, rayDistance;
        }
        public void Execute()
        {
            var result = new Result { triangle = -1, distance = distance, rayDistance = rayDistance };
            for (int t = 0; t + 2 < triangles.Length; t += 3)
            {
                Vector3 a = localToWorld.MultiplyPoint3x4(vertices[triangles[t]]);
                Vector3 b = localToWorld.MultiplyPoint3x4(vertices[triangles[t + 1]]);
                Vector3 c = localToWorld.MultiplyPoint3x4(vertices[triangles[t + 2]]);
                if (Vector3.Cross(b - a, c - a).sqrMagnitude < 1e-12f) continue;
                if (projectFromAttacker && RayTriangle(rayOrigin, approach, a, b, c,
                    out float rayT, out Vector3 rayWeights) && rayT < result.rayDistance &&
                    (rayOrigin + approach * rayT - point).sqrMagnitude <= maxDistance)
                {
                    result.rayDistance = rayT; result.triangle = t; result.weights = rayWeights;
                }
                if (!float.IsPositiveInfinity(result.rayDistance)) continue;
                Vector3 weights = ClosestBarycentric(point, a, b, c);
                float d = (a * weights.x + b * weights.y + c * weights.z - point).sqrMagnitude;
                if (d >= result.distance) continue;
                result.distance = d; result.triangle = t; result.weights = weights;
            }
            output[0] = result;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Warmup()
        {
            // Compile before combat, without loading or cloning an enemy mesh.
            using var vertices = new NativeArray<Vector3>(0, Allocator.TempJob);
            using var triangles = new NativeArray<int>(0, Allocator.TempJob);
            using var output = new NativeArray<Result>(1, Allocator.TempJob);
            new CombatScratchProjection { vertices = vertices, triangles = triangles, output = output }.Run();
        }
        private static bool RayTriangle(Vector3 origin, Vector3 direction, Vector3 a, Vector3 b, Vector3 c,
            out float distance, out Vector3 weights)
        {
            distance = 0; weights = default;
            Vector3 e1 = b - a, e2 = c - a, h = Vector3.Cross(direction, e2);
            float determinant = Vector3.Dot(e1, h);
            if (Mathf.Abs(determinant) < 1e-10f) return false;
            float inverse = 1f / determinant;
            Vector3 s = origin - a;
            float u = inverse * Vector3.Dot(s, h);
            if (u < 0 || u > 1) return false;
            Vector3 q = Vector3.Cross(s, e1);
            float v = inverse * Vector3.Dot(direction, q);
            if (v < 0 || u + v > 1) return false;
            distance = inverse * Vector3.Dot(e2, q);
            if (distance < 0) return false;
            weights = new Vector3(1 - u - v, u, v); return true;
        }

        // Ericson's closest point on triangle, returning rest-mesh interpolation weights.
        private static Vector3 ClosestBarycentric(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a, ap = p - a;
            float d1 = Vector3.Dot(ab, ap), d2 = Vector3.Dot(ac, ap);
            if (d1 <= 0 && d2 <= 0) return new Vector3(1, 0, 0);
            Vector3 bp = p - b;
            float d3 = Vector3.Dot(ab, bp), d4 = Vector3.Dot(ac, bp);
            if (d3 >= 0 && d4 <= d3) return new Vector3(0, 1, 0);
            float vc = d1 * d4 - d3 * d2;
            if (vc <= 0 && d1 >= 0 && d3 <= 0) { float v = d1 / (d1 - d3); return new Vector3(1 - v, v, 0); }
            Vector3 cp = p - c;
            float d5 = Vector3.Dot(ab, cp), d6 = Vector3.Dot(ac, cp);
            if (d6 >= 0 && d5 <= d6) return new Vector3(0, 0, 1);
            float vb = d5 * d2 - d1 * d6;
            if (vb <= 0 && d2 >= 0 && d6 <= 0) { float w = d2 / (d2 - d6); return new Vector3(1 - w, 0, w); }
            float va = d3 * d6 - d5 * d4;
            if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0)
            { float w = (d4 - d3) / ((d4 - d3) + (d5 - d6)); return new Vector3(0, 1 - w, w); }
            float inv = 1f / (va + vb + vc);
            float y = vb * inv, z = vc * inv;
            return new Vector3(1 - y - z, y, z);
        }
    }
}
