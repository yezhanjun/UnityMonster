using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DraugShootHitGpuValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = false;
        var shader = Shader.Find("Jotunnslayer/SourceFlaskShootHit");
        if (!shader || !shader.isSupported) throw new Exception("Shoot-hit shader unavailable");
        var material = new Material(shader);
        var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        var readback = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true);
        var rt = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var mesh = new Mesh();
        var obj = new GameObject("Shoot-hit GPU sample", typeof(MeshFilter), typeof(MeshRenderer));
        var camera = new GameObject("Shoot-hit test camera").AddComponent<Camera>();
        var previous = RenderTexture.active;
        try
        {
            texture.SetPixel(0, 0, new Color(.8f, .5f, .2f, 1)); texture.Apply();
            material.SetTexture("Texture2D_F593E37E", texture);
            material.SetFloat("Boolean_2FAC1FE4", 0); material.SetFloat("_Cull", 0);
            mesh.vertices = new[] {new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
            mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.colors = new[] {new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f)};
            mesh.SetUVs(0, new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            mesh.SetUVs(2, new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            obj.GetComponent<MeshFilter>().sharedMesh=mesh; obj.GetComponent<MeshRenderer>().sharedMaterial=material;
            camera.transform.position=new Vector3(0,0,-3);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=1;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;camera.allowHDR=true;camera.allowMSAA=false;camera.targetTexture=rt;
            foreach (float threshold in new[] {.2f,.5f,.8f})
            {
                var controls=new Vector4(threshold,.5f,0,2);
                mesh.SetUVs(1,new List<Vector4> {controls,controls,controls,controls});
                camera.Render(); RenderTexture.active=rt;
                readback.ReadPixels(new Rect(0,0,32,32),0,0);readback.Apply();
                var actual=readback.GetPixel(16,16);
                // Source saturate((x-.5)*infinity): NaN at equality saturates to zero,
                // so source alpha is one for x<=.5 and zero for x>.5.
                var expected=threshold<=.5f?new Color(.4f,.2f,.8f,1):Color.black;
                float error=Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b));
                if (float.IsNaN(error) || error>.003f) throw new Exception($"Shoot-hit GPU boundary x={threshold} actual={actual} expected={expected}");
                Debug.Log($"DRAUG_SHOOT_HIT_GPU_CASE q=.5 x={threshold} rgb={actual} error={error}");
            }
            Debug.Log("DRAUG_SHOOT_HIT_GPU_PASS singularBoundary/vertexStreams/additiveWeight");
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(rt);
        }
        DraugFxPreview.Run();
    }
}
