using UnityEngine;

namespace MTPSKIT
{
    // Short-lived substitute presentation. The server has already resolved the effect.
    public sealed class HeroSkillIndicator : MonoBehaviour
    {
        Material material;
        float expires;
        public static void Show(Vector3 center, float radius, bool healing)
        {
            var shader = Resources.Load<Shader>("Jotunnslayer/SkillIndicator");
            if (!shader) return;
            var host = new GameObject("Hero skill range");
            var life = host.AddComponent<HeroSkillIndicator>();
            life.material = new Material(shader);
            life.material.SetColor("_BaseColor", healing ? Color.green : Color.cyan);
            life.expires = Time.time + .7f;
            var line = host.AddComponent<LineRenderer>();
            line.sharedMaterial = life.material; line.widthMultiplier = .08f; line.loop = true;
            line.positionCount = 48; line.useWorldSpace = true;
            for (int i = 0; i < 48; i++)
            {
                float angle = i * Mathf.PI * 2f / 48;
                line.SetPosition(i, center + new Vector3(Mathf.Sin(angle) * radius, .15f, Mathf.Cos(angle) * radius));
            }
        }
        void Update() { if (Time.time >= expires) Destroy(gameObject); }
        void OnDestroy() { if (material) Destroy(material); }
    }
}
