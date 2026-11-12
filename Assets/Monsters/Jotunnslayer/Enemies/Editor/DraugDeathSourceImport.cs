using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Stage original geometry and inspect dependencies before assigning URP materials.
// This does not produce a playable death prefab or replace source FX lifecycle code.
public static class DraugDeathSourceImport
{
    const string Folder = "Assets/Monsters/Jotunnslayer/Enemies/Editor/DeathSourceGeometry";
    [Serializable] class MeshRecord { public string name, path; public int vertices, subMeshes; public Bounds bounds; }
    [Serializable] class MaterialRecord { public string name, shader; public bool supported; public List<string> textures = new List<string>(); }
    [Serializable] class Report
    {
        public string status = "SOURCE_GEOMETRY_IMPORTED_RENDERING_PENDING";
        public int nodes, particles, rigidbodies, meshColliders;
        public List<MeshRecord> meshes = new List<MeshRecord>();
        public List<MaterialRecord> materials = new List<MaterialRecord>();
        public string limits = "Geometry counts/bounds reload checked; full vertex equality, shader port, particle simulation, audio and source MonoBehaviour lifecycle remain pending. No production integration.";
    }

    public static void Run()
    {
        var report = new Report();
        var bundles = new List<AssetBundle>();
        var meshes = new HashSet<Mesh>();
        var materials = new HashSet<Material>();
        try
        {
            Directory.CreateDirectory(Folder); AssetDatabase.Refresh();
            const string source = "E:/SteamLibrary/steamapps/common/Jotunnslayer Hordes of Hel/Jotunnslayer_Data/StreamingAssets/aa/StandaloneWindows64";
            foreach (var pattern in new[] { "*monoscripts*.bundle", "*unitybuiltinassets*.bundle", "duplicateassetisolation*.bundle" })
                foreach (var file in Directory.GetFiles(source, pattern))
                {
                    var dependency = AssetBundle.LoadFromFile(file);
                    if (!dependency) throw new Exception("Dependency load failed " + file);
                    bundles.Add(dependency);
                }
            var bundle = AssetBundle.LoadFromFile("Logs/draug-death-recovered.bundle");
            if (!bundle) throw new Exception("Death bundle load failed");
            bundles.Add(bundle);
            var root = bundle.LoadAsset<GameObject>("assets/recovered/draugdeath_visual.prefab");
            if (!root) throw new Exception("Death root missing");
            report.nodes = root.GetComponentsInChildren<Transform>(true).Length;
            report.particles = root.GetComponentsInChildren<ParticleSystem>(true).Length;
            report.rigidbodies = root.GetComponentsInChildren<Rigidbody>(true).Length;
            report.meshColliders = root.GetComponentsInChildren<MeshCollider>(true).Length;
            if (report.nodes != 26 || report.particles != 15 || report.rigidbodies != 9 || report.meshColliders != 9)
                throw new Exception("Unity hierarchy differs from source serialized counts");
            foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true)) meshes.Add(filter.sharedMesh);
            foreach (var collider in root.GetComponentsInChildren<MeshCollider>(true)) meshes.Add(collider.sharedMesh);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials) materials.Add(material);
                if (renderer is ParticleSystemRenderer particle)
                {
                    materials.Add(particle.trailMaterial);
                    var particleMeshes = new Mesh[particle.meshCount];
                    particle.GetMeshes(particleMeshes);
                    foreach (var mesh in particleMeshes) meshes.Add(mesh);
                }
            }
            foreach (var particle in root.GetComponentsInChildren<ParticleSystem>(true)) meshes.Add(particle.shape.mesh);
            meshes.Remove(null); materials.Remove(null);
            foreach (var mesh in meshes)
            {
                string path = Folder + "/mesh_" + report.meshes.Count + ".asset";
                var copy = Object.Instantiate(mesh);
                var old = AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if (old) { EditorUtility.CopySerialized(copy, old); Object.DestroyImmediate(copy); }
                else AssetDatabase.CreateAsset(copy, path);
                report.meshes.Add(new MeshRecord { name = mesh.name, path = path, vertices = mesh.vertexCount, subMeshes = mesh.subMeshCount, bounds = mesh.bounds });
            }
            foreach (var material in materials)
            {
                var row = new MaterialRecord { name = material.name, shader = material.shader ? material.shader.name : "MISSING", supported = material.shader && material.shader.isSupported };
                foreach (var key in material.GetTexturePropertyNames())
                {
                    var texture = material.GetTexture(key);
                    if (texture) row.textures.Add($"{key}={texture.name} {texture.width}x{texture.height} mips={texture.mipmapCount}");
                }
                report.materials.Add(row);
            }
            AssetDatabase.SaveAssets();
        }
        finally { foreach (var bundle in bundles) if (bundle) bundle.Unload(true); }
        // Reimport after source bundle unload, so source-owned objects cannot satisfy checks.
        foreach (var row in report.meshes)
        {
            AssetDatabase.ImportAsset(row.path, ImportAssetOptions.ForceUpdate);
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(row.path);
            if (!mesh || mesh.vertexCount != row.vertices || mesh.subMeshCount != row.subMeshes || mesh.bounds != row.bounds)
                throw new Exception("Local geometry reload mismatch " + row.path);
        }
        File.WriteAllText("Docs/Validation/DraugDeathUnitySourceImport.json", JsonUtility.ToJson(report, true));
        Debug.Log($"DRAUG_DEATH_SOURCE_IMPORT_PASS nodes={report.nodes} particles={report.particles} bodies={report.rigidbodies} meshes={report.meshes.Count} materials={report.materials.Count} renderingPending=True");
        EditorApplication.Exit(0);
    }
}
