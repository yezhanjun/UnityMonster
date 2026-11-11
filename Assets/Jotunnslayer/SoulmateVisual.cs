using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DefaultExecutionOrder(5010)]
    internal sealed class SoulmateVisual : MonoBehaviour
    {
        private Transform marker, orb;
        private RangeRobotVisual appearance;
        private Animator baseAnimator, markerAnimator;
        private LineRenderer tether;
        private Material material;
        private Vector3 followPosition, velocity, strikePoint;
        private Health strikeTarget;
        private float windupUntil, strikeUntil;
        private const float OrbSize = 0.28f;

        internal void SetVisible(bool visible)
        {
            if (visible && !orb && !CreateVisual())
                return;
            if (visible && !appearance)
                TryGetComponent(out appearance);
            if (!orb || orb.gameObject.activeSelf == visible)
                return;

            orb.gameObject.SetActive(visible);
            tether.enabled = false;
            strikeTarget = null;
            windupUntil = 0f;
            strikeUntil = 0f;
            orb.localScale = Vector3.one * OrbSize;
            velocity = Vector3.zero;
            if (visible)
                orb.position = followPosition = FollowPoint();
        }

        internal void WindUp() => windupUntil = Time.time + SkillBattlePreview.SoulmateWindup;

        internal void Strike(Health target, Vector3 point)
        {
            if (!orb || !orb.gameObject.activeSelf)
                return;
            strikePoint = point;
            strikeTarget = target;
            windupUntil = 0f;
            strikeUntil = Time.time + 0.15f;
        }

        private bool CreateVisual()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (!shader) shader = Shader.Find("Sprites/Default");
            if (!shader) return false;
            material = new Material(shader) { color = new Color(.68f, .78f, 1f) };

            baseAnimator = GetComponentInChildren<Animator>();
            TryGetComponent(out appearance);

            var body = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body.name = "Raid Soulmate";
            orb = body.transform;
            orb.SetParent(transform, true);
            orb.localScale = Vector3.one * OrbSize;
            orb.position = followPosition = FollowPoint();
            var collider = body.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            body.GetComponent<Renderer>().sharedMaterial = material;

            tether = body.AddComponent<LineRenderer>();
            tether.useWorldSpace = true;
            tether.positionCount = 2;
            tether.sharedMaterial = material;
            tether.startWidth = .10f;
            tether.endWidth = .04f;
            tether.enabled = false;
            return true;
        }

        private Vector3 FollowPoint()
        {
            Animator display = appearance ? appearance.DisplayAnimator : null;
            Animator animator = display && display.isHuman ? display : baseAnimator;
            if (markerAnimator != animator)
            {
                markerAnimator = animator;
                marker = animator && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.LeftHand) : null;
            }
            return marker ? marker.position + Vector3.up * .28f :
                transform.TransformPoint(new Vector3(-.55f, 1.25f, -.25f));
        }

        private void LateUpdate()
        {
            if (!orb || !orb.gameObject.activeSelf)
                return;

            Vector3 destination = FollowPoint();
            if ((followPosition - destination).sqrMagnitude > 4.5f * 4.5f)
            {
                followPosition = destination;
                velocity = Vector3.zero;
            }
            else
                followPosition = Vector3.SmoothDamp(followPosition, destination, ref velocity, .2f);
            orb.position = followPosition;

            float windupRemaining = Mathf.Clamp01((windupUntil - Time.time) / SkillBattlePreview.SoulmateWindup);
            float pulse = Mathf.Sin(windupRemaining * Mathf.PI);
            orb.localScale = Vector3.one * (OrbSize + .16f * pulse);

            tether.enabled = Time.time < strikeUntil;
            if (tether.enabled)
            {
                if (strikeTarget && strikeTarget.isActiveAndEnabled)
                    strikePoint = SkillBattlePreview.RaidSkillAimPoint(strikeTarget);
                tether.SetPosition(0, orb.position);
                tether.SetPosition(1, strikePoint);
            }
            else
                strikeTarget = null;
        }

        private void OnDestroy()
        {
            if (orb) Destroy(orb.gameObject);
            if (material) Destroy(material);
        }
    }
}
