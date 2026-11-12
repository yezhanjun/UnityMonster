using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DraugSmokeGpuValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = false;
        var prefab=Resources.Load<GameObject>("Jotunnslayer/DraugThrower/OriginalFx/Draug_projectile");
        bool foundSourceMask=false;
        foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        foreach(var sourceMaterial in renderer.sharedMaterials)
        {
            if(!sourceMaterial || sourceMaterial.shader.name!="Jotunnslayer/SourceSmokeErosion")continue;
            bool additive=sourceMaterial.name.StartsWith("M_thrower_fire002");
            if(sourceMaterial.GetFloat("_DstBlend")!=(additive?1:10) || sourceMaterial.GetFloat("_BlendMode")!=(additive?1:0))throw new Exception("Source erosion blend mode mismatch");
            string path=AssetDatabase.GetAssetPath(sourceMaterial.GetTexture("_MaskTexture"));
            var importer=AssetImporter.GetAtPath(path) as TextureImporter;
            if(!path.Contains("source_texture_") || (importer && importer.alphaIsTransparency) || (!importer && !path.EndsWith(".asset")))throw new Exception("Smoke packed channels were not preserved");
            var check=new Texture2D(2,2);
            try
            {
                Color[] pixels;
                if(path.EndsWith(".asset"))pixels=AssetDatabase.LoadAssetAtPath<Texture2D>(path).GetPixels(0);
                else {check.LoadImage(System.IO.File.ReadAllBytes(path));pixels=check.GetPixels();}
                float minimum=1,maximum=0;
                foreach(var pixel in pixels){minimum=Mathf.Min(minimum,pixel.r);maximum=Mathf.Max(maximum,pixel.r);}
                if(maximum-minimum<.1f)throw new Exception("Smoke red mask lost its variation");
                foundSourceMask=true;Debug.Log($"DRAUG_SMOKE_MASK_PASS redRange={minimum}..{maximum} path={path}");
            }
            finally{UnityEngine.Object.DestroyImmediate(check);}
        }
        if(!foundSourceMask)throw new Exception("No imported source smoke material");
        var shader = Shader.Find("Jotunnslayer/SourceSmokeErosion");
        if (!shader || !shader.isSupported) throw new Exception("Smoke shader unavailable");
        var material = new Material(shader);
        var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        var readback = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true);
        var rt = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var mesh = new Mesh();
        var obj = new GameObject("Smoke GPU sample", typeof(MeshFilter), typeof(MeshRenderer));
        var camera = new GameObject("Smoke test camera").AddComponent<Camera>();
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wallMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wallMaterial.SetColor("_BaseColor",Color.black);wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;
        wall.transform.localScale=new Vector3(20,20,.1f);
        var previous = RenderTexture.active;
        try
        {
            texture.SetPixel(0, 0, new Color(.8f, .5f, .2f, 0)); texture.Apply();
            material.SetTexture("_MaskTexture", texture);
            material.SetFloat("_Cull", 0);
            mesh.vertices = new[] {new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
            mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.colors = new[] {new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f)};
            mesh.SetUVs(0, new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            mesh.SetUVs(2, new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            obj.GetComponent<MeshFilter>().sharedMesh=mesh; obj.GetComponent<MeshRenderer>().sharedMaterial=material;
            camera.transform.position=new Vector3(0,0,-3);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=1;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;camera.allowHDR=true;camera.allowMSAA=false;camera.targetTexture=rt;
            foreach(bool orthographic in new[]{true,false})
            foreach (var sample in new[] {new Vector2(.25f,.2f),new Vector2(.5f,.2f),new Vector2(1,.2f),new Vector2(.5f,1)})
            {
                camera.orthographic=orthographic;
                wall.transform.position=new Vector3(0,0,sample.x+.05f);
                var controls=new Vector4(sample.y,0,0,0);
                mesh.SetUVs(1,new List<Vector4> {controls,controls,controls,controls});
                camera.Render(); RenderTexture.active=rt;
                readback.ReadPixels(new Rect(0,0,32,32),0,0);readback.Apply();
                var actual=readback.GetPixel(16,16);
                float alpha=Mathf.Clamp01((.8f-sample.y)*sample.x)*.5f;
                var expected=new Color(.4f*alpha,.2f*alpha,.8f*alpha,1);
                float error=Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b));
                if (float.IsNaN(error) || error>.003f) throw new Exception($"Smoke GPU boundary depth={sample.x} erosion={sample.y} ortho={orthographic} actual={actual} expected={expected}");
                Debug.Log($"DRAUG_SMOKE_GPU_CASE depth={sample.x} erosion={sample.y} ortho={orthographic} rgb={actual} error={error}");
            }
            Debug.Log("DRAUG_SMOKE_GPU_PASS redMask/erosion/depth/vertexAlpha/materialAlphaIgnored/perspectiveAndOrtho");
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(wallMaterial);
            UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(rt);
        }
        DraugFxPreview.Run();
    }
}
