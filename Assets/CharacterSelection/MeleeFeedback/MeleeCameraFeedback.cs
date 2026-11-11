using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using SCPE;

namespace MTPSKIT.Gameplay
{
    // Render callbacks apply shake after both TPS and FPS camera controllers finish.
    public sealed class MeleeCameraFeedback : MonoBehaviour
    {
        public static MeleeCameraFeedback Instance { get; private set; }
        Camera targetCamera;
        Volume volume;
        VolumeProfile volumeProfile;
        RadialBlur radial;
        MeleeFeedbackProfile settings;
        float started, radialEnds, shakeEnds;
        Vector3 cameraPosition;
        Quaternion cameraRotation;
        bool applied;

        void Awake()
        {
            Instance = this;
            targetCamera = GetComponent<Camera>();
            var data = targetCamera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            targetCamera.SetVolumeFrameworkUpdateMode(VolumeFrameworkUpdateMode.EveryFrame);
            var node = new GameObject("Melee feedback volume");
            node.transform.SetParent(transform, false);
            int mask = data.volumeLayerMask.value;
            for (int i = 0; i < 32; i++)
                if ((mask & (1 << i)) != 0) { node.layer = i; break; }
            volume = node.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 10000f;
            volumeProfile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.sharedProfile = volumeProfile;
            radial = volumeProfile.Add<RadialBlur>(true);
            radial.amount.Override(0f);
        }

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += BeforeCamera;
            RenderPipelineManager.endCameraRendering += AfterCamera;
        }

        public void Play(MeleeFeedbackProfile profile)
        {
            if (!profile) return;
            settings = profile;
            started = Time.unscaledTime;
            radialEnds = started + profile.radialDuration;
            shakeEnds = started + profile.shakeDuration;
            radial.shader = profile.radialShader;
            radial.center.Override(profile.radialCenter);
            radial.iterations.Override(profile.radialIterations);
            radial.amount.Override(profile.radialAmount);
        }

        public void Clear()
        {
            settings = null;
            radialEnds = shakeEnds = 0f;
            if (radial) radial.amount.value = 0f;
            RestoreCamera();
        }

        void Update()
        {
            if (!settings || !GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter ||
                GameManager.myPlayerInstance.MyCharacter.CurrentHealth <= 0)
            {
                radial.amount.value = 0f;
                return;
            }
            float remaining = Mathf.Clamp01((radialEnds - Time.unscaledTime) / Mathf.Max(0.001f, settings.radialDuration));
            radial.amount.value = settings.radialAmount * remaining;
        }

        void BeforeCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera != targetCamera || !settings || applied || Time.unscaledTime >= shakeEnds ||
                !GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter ||
                GameManager.myPlayerInstance.MyCharacter.CurrentHealth <= 0) return;
            cameraPosition = transform.position;
            cameraRotation = transform.rotation;
            float elapsed = Time.unscaledTime - started;
            float fade = Mathf.Clamp01((shakeEnds - Time.unscaledTime) / Mathf.Max(0.001f, settings.shakeDuration));
            float phase = elapsed * settings.shakeFrequency * Mathf.PI * 2f;
            Vector3 wave = new Vector3(Mathf.Sin(phase), Mathf.Sin(phase * 1.37f), Mathf.Sin(phase * 0.79f));
            transform.SetPositionAndRotation(cameraPosition + cameraRotation * (Vector3.Scale(settings.shakePosition, wave) * fade),
                cameraRotation * Quaternion.Euler(Vector3.Scale(settings.shakeRotation, wave) * fade));
            applied = true;
        }

        void AfterCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == targetCamera) RestoreCamera();
        }

        void RestoreCamera()
        {
            if (!applied) return;
            transform.SetPositionAndRotation(cameraPosition, cameraRotation);
            applied = false;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= BeforeCamera;
            RenderPipelineManager.endCameraRendering -= AfterCamera;
            RestoreCamera();
            radialEnds = shakeEnds = 0f;
            if (radial) radial.amount.value = 0f;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (volumeProfile)
            {
                foreach (var component in volumeProfile.components) if (component) Destroy(component);
                Destroy(volumeProfile);
            }
            if (volume) Destroy(volume.gameObject);
        }
    }
}
