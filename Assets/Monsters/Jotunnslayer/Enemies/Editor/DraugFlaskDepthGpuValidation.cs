using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class DraugFlaskDepthGpuValidation
{
    public static void Run()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        RenderSettings.fog = false;
        Material material=null;
        var texture = new Texture2D(1, 1, TextureFormat.RGBAFloat, false, true);
        var readback = new Texture2D(32, 32, TextureFormat.RGBAFloat, false, true);
        var rt = new RenderTexture(32, 32, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear);
        var mesh = new Mesh();
        var obj = new GameObject("Flask depth GPU sample", typeof(MeshFilter), typeof(MeshRenderer));
        var camera = new GameObject("Flask depth test camera").AddComponent<Camera>();
        var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
        var wallMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wallMaterial.SetColor("_BaseColor",Color.black);wall.GetComponent<Renderer>().sharedMaterial=wallMaterial;
        wall.transform.localScale=new Vector3(20,20,.1f);
        var previous = RenderTexture.active;
        try
        {
            texture.SetPixel(0, 0, new Color(.8f, .5f, .2f, 1)); texture.Apply();

            mesh.vertices = new[] {new Vector3(-1,-1,0),new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(-1,1,0)};
            mesh.triangles = new[] {0,1,2,0,2,3};
            mesh.colors = new[] {new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f),new Color(.5f,.25f,1,.5f)};
            mesh.SetUVs(0, new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            mesh.SetUVs(2, new List<Vector4> {Vector4.zero,Vector4.zero,Vector4.zero,Vector4.zero});
            obj.GetComponent<MeshFilter>().sharedMesh=mesh;
            camera.transform.position=new Vector3(0,0,-3);camera.transform.LookAt(Vector3.zero);
            camera.orthographic=true;camera.orthographicSize=1;camera.clearFlags=CameraClearFlags.SolidColor;
            camera.backgroundColor=Color.black;camera.allowHDR=true;camera.allowMSAA=false;camera.targetTexture=rt;
            int count=0;float maximum=0;
            foreach(bool dissolve in new[]{true,false})
            {
                var shader=Shader.Find(dissolve?"Jotunnslayer/SourceFlaskDissolve":"Jotunnslayer/SourceFlaskShootHit");
                if(!shader || !shader.isSupported)throw new Exception("Flask shader unavailable");
                material=new Material(shader);material.SetFloat("_Cull",0);
                material.SetTexture("Texture2D_F593E37E",texture);
                obj.GetComponent<MeshRenderer>().sharedMaterial=material;
                foreach(bool orthographic in new[]{true,false})
                foreach(bool soft in new[]{true,false})
                foreach(float depth in new[]{.25f,.5f,1f})
                foreach(float scale in new[]{.5f,2f})
                {
                    camera.orthographic=orthographic;
                    wall.transform.position=new Vector3(0,0,depth+.05f);
                    material.SetFloat("Boolean_2FAC1FE4",soft?1:0);
                    var controls=dissolve?new Vector4(0,0,1,scale):new Vector4(0,.5f,0,2);
                    var warp=dissolve?new Vector4(0,1,0,0):new Vector4(scale,0,0,0);
                    mesh.SetUVs(1,new List<Vector4>{controls,controls,controls,controls});
                    mesh.SetUVs(2,new List<Vector4>{warp,warp,warp,warp});
                    camera.Render();RenderTexture.active=rt;
                    readback.ReadPixels(new Rect(0,0,32,32),0,0);readback.Apply();
                    var actual=readback.GetPixel(16,16);
                    float fade=soft?Mathf.Clamp01(dissolve?depth/scale:depth*scale):1;
                    float weight=(dissolve?.5f:1)*fade;
                    var expected=new Color(.4f*weight,.2f*weight,.8f*weight,1);
                    float error=Mathf.Max(Mathf.Abs(actual.r-expected.r),Mathf.Abs(actual.g-expected.g),Mathf.Abs(actual.b-expected.b));
                    if(float.IsNaN(error) || error>.003f)throw new Exception($"Flask depth GPU dissolve={dissolve} ortho={orthographic} soft={soft} depth={depth} scale={scale} actual={actual} expected={expected}");
                    maximum=Mathf.Max(maximum,error);count++;
                    Debug.Log($"DRAUG_FLASK_DEPTH_GPU_CASE dissolve={dissolve} ortho={orthographic} soft={soft} depth={depth} scale={scale} error={error}");
                }
                UnityEngine.Object.DestroyImmediate(material);material=null;
            }
            Debug.Log($"DRAUG_FLASK_DEPTH_GPU_PASS cases={count} maxError={maximum} divideWidthVsMultiplyCoefficient/softBypass/orthoAndPerspective");
        }
        finally
        {
            RenderTexture.active=previous;camera.targetTexture=null;
            UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(wallMaterial);
            UnityEngine.Object.DestroyImmediate(obj);UnityEngine.Object.DestroyImmediate(camera.gameObject);
            UnityEngine.Object.DestroyImmediate(mesh);UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(texture);UnityEngine.Object.DestroyImmediate(readback);UnityEngine.Object.DestroyImmediate(rt);
        }
        EditorApplication.Exit(0);
    }
}
