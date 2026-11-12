using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed class RainMonsterLogVisual : MonoBehaviour
    {
        const float Duration = 5f;
        const float Intensity = 7.39f;
        static readonly AnimationCurve IntensityCurve = new AnimationCurve(
            new Keyframe(0f, 1f), new Keyframe(.5f, .7f), new Keyframe(1f, 1f));

        Light pickupLight;
        float elapsed;

        void Awake()
        {
            var lightObject = new GameObject("Point light");
            lightObject.transform.SetParent(transform, false);
            pickupLight = lightObject.AddComponent<Light>();
            pickupLight.type = LightType.Point;
            pickupLight.color = new Color(.55707115f, .22004759f, .80882353f);
            pickupLight.range = 3f;
            pickupLight.shadows = LightShadows.None;
            pickupLight.intensity = 0f;
        }

        void Update()
        {
            elapsed += Time.deltaTime;
            pickupLight.intensity = IntensityCurve.Evaluate(elapsed / Duration) * Intensity;
            if (elapsed >= Duration) enabled = false;
        }
    }
}
