using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        private struct Impact
        {
            public Vector3 position;
            public float expires;
        }

        private const float ImpactDuration = 0.35f;
        private readonly Impact[] impacts = new Impact[48];
        private int nextImpact;
        private readonly LineRenderer[] gmLightning = new LineRenderer[8];
        private readonly float[] gmLightningEnds = new float[8];
        private Material gmLightningMaterial;
        private int gmLightningIndex;
        private AudioSource godCastAudio;
        private AudioClip lightningSound, evolvedLightningSound;

        private void PlayLightningAudio(bool evolved)
        {
            if (evolved)
                PlayGodCastAudio(ref evolvedLightningSound, "Jotunnslayer/ThorLightningEvolvedOriginal", caster.transform.position);
            else
                PlayGodCastAudio(ref lightningSound, "Jotunnslayer/ThorLightningOriginal", caster.transform.position);
        }

        private void PlayGodCastAudio(ref AudioClip clip, string resourcePath, Vector3 position)
        {
            if (ShowControls || SuppressPresentation) return;
            if (!clip) clip = Resources.Load<AudioClip>(resourcePath);
            if (!clip) return;
            if (!godCastAudio)
            {
                godCastAudio = new GameObject("GM God Cast Audio").AddComponent<AudioSource>();
                godCastAudio.transform.SetParent(transform, false);
                godCastAudio.playOnAwake = false;
                godCastAudio.spatialBlend = 1f;
                godCastAudio.volume = .65f;
                godCastAudio.maxDistance = 30f;
            }
            if (godCastAudio.isPlaying && godCastAudio.clip == clip) return;
            godCastAudio.transform.position = position;
            godCastAudio.clip = clip;
            godCastAudio.Play();
        }

        private void ShowGmLightning(Vector3 position)
        {
            if (ShowControls || SuppressPresentation) return;
            if (!gmLightningMaterial)
            {
                var shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) return;
                gmLightningMaterial = new Material(shader);
                gmLightningMaterial.SetColor("_BaseColor", new Color(.4f, .8f, 1f));
            }
            int slot = gmLightningIndex++ % gmLightning.Length;
            var line = gmLightning[slot];
            if (!line)
            {
                line = new GameObject("GM Lightning Strike").AddComponent<LineRenderer>();
                line.transform.SetParent(transform, false);
                line.sharedMaterial = gmLightningMaterial;
                line.useWorldSpace = true;
                line.positionCount = 5;
                line.widthMultiplier = .1f;
                gmLightning[slot] = line;
            }
            line.SetPosition(0, position + Vector3.up * 7f);
            line.SetPosition(1, position + new Vector3(.25f, 5f, .1f));
            line.SetPosition(2, position + new Vector3(-.3f, 3.2f, -.1f));
            line.SetPosition(3, position + new Vector3(.15f, 1.7f, .1f));
            line.SetPosition(4, position + Vector3.up * .1f);
            line.enabled = true;
            gmLightningEnds[slot] = Time.time + .3f;
        }

        private void TickGmLightning(float now)
        {
            for (int i = 0; i < gmLightning.Length; i++)
                if (gmLightning[i] && gmLightning[i].enabled && now >= gmLightningEnds[i])
                    gmLightning[i].enabled = false;
        }

        private void ClearGmLightning()
        {
            for (int i = 0; i < gmLightning.Length; i++)
                if (gmLightning[i]) { Destroy(gmLightning[i].gameObject); gmLightning[i] = null; }
            if (gmLightningMaterial) Destroy(gmLightningMaterial);
            gmLightningMaterial = null;
            gmLightningIndex = 0;
        }

        private void ClearGodCastAudio()
        {
            if (!godCastAudio) return;
            godCastAudio.Stop();
            Destroy(godCastAudio.gameObject);
            godCastAudio = null;
        }

        private void RecordImpact(Vector3 position)
        {
            impacts[nextImpact] = new Impact { position = position, expires = Time.time + ImpactDuration };
            nextImpact = (nextImpact + 1) % impacts.Length;
        }

        private void DrawImpacts()
        {
            if (Event.current.type != EventType.Repaint)
                return;

            var camera = Camera.main;
            if (!camera)
                return;

            Color previous = GUI.color;
            float now = Time.time;
            foreach (var impact in impacts)
            {
                if (impact.expires <= now)
                    continue;

                Vector3 point = camera.WorldToScreenPoint(impact.position);
                if (point.z <= 0)
                    continue;

                float remaining = (impact.expires - now) / ImpactDuration;
                float reach = Mathf.Lerp(18f, 4f, remaining);
                float y = Screen.height - point.y;
                GUI.color = new Color(1f, .73f, .28f, remaining);
                GUI.DrawTexture(new Rect(point.x - reach, y - 1, reach * 2, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x - 1, y - reach, 2, reach * 2), Texture2D.whiteTexture);
            }

            foreach (var image in shadowImages)
            {
                if (image.presentation != null)
                    continue;
                if (now >= image.expires)
                    continue;

                Vector3 point = camera.WorldToScreenPoint(image.position + Vector3.up);
                if (point.z <= 0)
                    continue;

                float y = Screen.height - point.y;
                GUI.color = image.explodes ? new Color(1f, 0.55f, 0.95f, 0.9f) : new Color(0.6f, 0.54f, 1f, 0.85f);
                GUI.DrawTexture(new Rect(point.x - 4, y - 4, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x - 4, y + 2, 8, 2), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x - 4, y - 2, 2, 4), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(point.x + 2, y - 2, 2, 4), Texture2D.whiteTexture);
            }
            GUI.color = previous;
        }
    }
}
