#if UNITY_EDITOR
using System.Collections;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Jotunnslayer
{
    [InitializeOnLoad]
    public sealed class ShadowImageVisualValidation : MonoBehaviour
    {
        private const string Pending = "Jotunnslayer.ShadowImageVisualValidation";
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        static ShadowImageVisualValidation() => EditorApplication.playModeStateChanged += OnPlayMode;

        public static void Run()
        {
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayMode(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.EraseBool(Pending);
            new GameObject("Shadow image visual validation").AddComponent<ShadowImageVisualValidation>();
        }

        private IEnumerator Start()
        {
            yield return null;
            try
            {
                if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
                    throw new System.InvalidOperationException("Graphics device required for shadow presentation validation.");
                var runtime = new GameObject("Shadow presentation owner").AddComponent<SkillBattlePreview>();
                runtime.enabled = false;
                var acquire = typeof(SkillBattlePreview).GetMethod("AcquireShadowPresentation", Private);
                var update = typeof(SkillBattlePreview).GetMethod("UpdateShadowPresentation", Private);
                var release = typeof(SkillBattlePreview).GetMethod("ReleaseShadowPresentation", BindingFlags.Static | BindingFlags.NonPublic);
                object visual = acquire.Invoke(runtime, new object[] { Vector3.zero, Quaternion.identity, true, 0f });
                if (visual == null) throw new System.InvalidOperationException("Original ghost presentation unavailable.");
                var visualType = visual.GetType();
                var body = (GameObject)visualType.GetField("body", Private | BindingFlags.Public).GetValue(visual);
                var renderers = body.GetComponentsInChildren<SkinnedMeshRenderer>();
                if (renderers.Length == 0) throw new System.InvalidOperationException("Original ghost has no skinned renderer.");
                foreach (var renderer in renderers)
                {
                    if (!renderer.sharedMesh || renderer.bones.Length == 0 || !renderer.sharedMaterial ||
                        renderer.sharedMaterial.shader.name != "Jotunnslayer/Shadow Image Presentation")
                        throw new System.InvalidOperationException("Original ghost mesh, bones or material binding missing.");
                    foreach (var bone in renderer.bones)
                        if (!bone) throw new System.InvalidOperationException("Original ghost has an unresolved skin bone.");
                }
                var bones = body.GetComponentsInChildren<Transform>();
                var pose = new Quaternion[bones.Length];
                for (int i = 0; i < bones.Length; i++) pose[i] = bones[i].localRotation;
                var cameraObject = new GameObject("Ghost evidence camera");
                var camera = cameraObject.AddComponent<Camera>();
                camera.transform.position = new Vector3(3.8f, 2.7f, 4.8f);
                camera.transform.LookAt(Vector3.up * 1.0f);
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(0.025f, 0.025f, 0.04f, 1f);
                camera.nearClipPlane = .05f;
                camera.farClipPlane = 30f;
                camera.fieldOfView = 34f;
                int layer = 30;
                foreach (var bone in bones) bone.gameObject.layer = layer;
                camera.cullingMask = 1 << layer;
                var target = new RenderTexture(640, 640, 24);
                camera.targetTexture = target;
                string directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TPSJotunn-ShadowVisual-20261108");
                System.IO.Directory.CreateDirectory(directory);
                Capture(camera, target, System.IO.Path.Combine(directory, "spawn.png"));
                update.Invoke(runtime, new object[] { visual, 0.134f });
                float changed = 0f;
                for (int i = 0; i < bones.Length; i++)
                {
                    Quaternion q = bones[i].localRotation;
                    Vector3 p = bones[i].localPosition;
                    if (!float.IsFinite(q.x + q.y + q.z + q.w + p.x + p.y + p.z))
                        throw new System.InvalidOperationException("Original ghost animation produced nonfinite pose.");
                    changed += Quaternion.Angle(pose[i], q);
                }
                if (changed < 1f) throw new System.InvalidOperationException("Original ghost attack did not animate the skeleton.");
                Capture(camera, target, System.IO.Path.Combine(directory, "attack.png"));
                update.Invoke(runtime, new object[] { visual, .5f });
                Capture(camera, target, System.IO.Path.Combine(directory, "idle.png"));
                release.Invoke(null, new[] { visual });
                if (body.activeSelf) throw new System.InvalidOperationException("Released ghost remained visible.");
                var reused = acquire.Invoke(runtime, new object[] { Vector3.right, Quaternion.identity, false, 2f });
                if (!ReferenceEquals(visual, reused)) throw new System.InvalidOperationException("Ghost presentation pool did not reuse its entry.");
                release.Invoke(null, new[] { reused });
                typeof(SkillBattlePreview).GetMethod("DisposeShadowPresentations", Private).Invoke(runtime, null);
                camera.targetTexture = null;
                Destroy(target);
                Destroy(cameraObject);
                Destroy(runtime.gameObject);
                Debug.Log($"JOTUNNSLAYER_SHADOW_VISUAL_OK renderers={renderers.Length} transforms={bones.Length} rotationChange={changed} screenshots={directory} originalModelClipsMaps=True poolReuse=True originalShaderParity=False remote=False");
                EditorApplication.Exit(0);
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        private static void Capture(Camera camera, RenderTexture target, string file)
        {
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            texture.Apply();
            RenderTexture.active = previous;
            int visible = 0;
            foreach (var pixel in texture.GetPixels32())
                if (pixel.r > 25 || pixel.g > 25 || pixel.b > 30) visible++;
            if (visible < 100) throw new System.InvalidOperationException("Ghost render has too few visible pixels: " + visible);
            System.IO.File.WriteAllBytes(file, texture.EncodeToPNG());
            Destroy(texture);
            Debug.Log($"JOTUNNSLAYER_SHADOW_SCREENSHOT file={file} dimensions=640x640 visiblePixels={visible}");
        }
    }
}
#endif
