using UnityEngine;

namespace Jotunnslayer
{
    public sealed partial class SkillBattlePreview
    {
        // Replaceable GM presentation. Damage, targeting and timing remain in the skill runtime.
        const int IndicatorCapacity = 64;
        readonly SkillIndicator[] skillIndicators = new SkillIndicator[IndicatorCapacity];
        Material indicatorMaterial;
        int indicatorCursor;
        bool gmIndicators;
        internal float LastIndicatorAt { get; private set; } = -1f;
        internal int IndicatorEvents { get; private set; }
        internal int VisibleIndicatorCount
        {
            get
            {
                int count = 0;
                foreach (var item in skillIndicators) if (item != null && item.line && item.line.enabled) count++;
                return count;
            }
        }
        static readonly Color AreaColor = new Color(.15f, .85f, 1f);
        static readonly Color WarningColor = new Color(1f, .7f, .1f);
        static readonly Color HealingColor = new Color(.3f, 1f, .35f);
        static readonly Color HitColor = new Color(1f, .25f, .2f);

        sealed class SkillIndicator
        {
            internal LineRenderer line;
            internal Vector3 origin, end;
            internal float radius, angle, expires;
            internal int shape;
            internal Color color;
        }

        void IndicateRing(Vector3 origin, float radius, float seconds = .45f, bool warning = false, bool healing = false)
            => Indicate(0, origin, Vector3.zero, radius, 0f, seconds, healing ? HealingColor : warning ? WarningColor : AreaColor);

        void IndicatePath(Vector3 start, Vector3 end, float radius = .6f, float seconds = .45f, bool warning = false)
            => Indicate(1, start, end, radius, 0f, seconds, warning ? WarningColor : AreaColor);

        void IndicateSector(Vector3 origin, Vector3 forward, float radius, float halfAngle, float seconds = .45f)
            => Indicate(2, origin, forward, radius, halfAngle, seconds, AreaColor);

        void IndicateHit(Vector3 point) => Indicate(0, point, Vector3.zero, .3f, 0f, .2f, HitColor);

        void Indicate(int shape, Vector3 origin, Vector3 end, float radius, float angle, float seconds, Color color)
        {
            if (!gmIndicators || SuppressPresentation) return;
            LastIndicatorAt = Time.time;
            IndicatorEvents++;
            if (SystemInfo.graphicsDeviceType == UnityEngine.Rendering.GraphicsDeviceType.Null) return;
            SkillIndicator marker = null;
            foreach (var item in skillIndicators)
                if (item != null && item.shape == shape && item.color == color &&
                    (item.origin - origin).sqrMagnitude < .0001f && (item.end - end).sqrMagnitude < .0001f &&
                    Mathf.Abs(item.radius - radius) < .001f && Mathf.Abs(item.angle - angle) < .001f)
                { marker = item; break; }
            if (marker == null)
            {
                int index = indicatorCursor++ % IndicatorCapacity;
                marker = skillIndicators[index];
                if (marker == null)
                {
                    var host = new GameObject("GM skill indicator");
                    host.transform.SetParent(transform, false);
                    marker = new SkillIndicator { line = host.AddComponent<LineRenderer>() };
                    marker.line.useWorldSpace = true;
                    marker.line.widthMultiplier = .075f;
                    marker.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    marker.line.receiveShadows = false;
                    skillIndicators[index] = marker;
                }
            }
            if (!indicatorMaterial)
            {
                var shader = Resources.Load<Shader>("Jotunnslayer/SkillIndicator");
                if (!shader) shader = Shader.Find("Universal Render Pipeline/Unlit");
                if (!shader) return;
                indicatorMaterial = new Material(shader);
                indicatorMaterial.SetColor("_BaseColor", Color.white);
            }
            marker.origin = origin; marker.end = end; marker.radius = radius; marker.angle = angle;
            marker.shape = shape; marker.color = color; marker.expires = Time.time + seconds;
            var line = marker.line;
            line.sharedMaterial = indicatorMaterial;
            // LineRenderer vertex colours are not consumed by every URP Unlit shader.
            // A cached property block also keeps the three phases visually distinct.
            indicatorProperties ??= new MaterialPropertyBlock();
            indicatorProperties.SetColor("_BaseColor", color);
            line.SetPropertyBlock(indicatorProperties);
            line.enabled = true;
            line.loop = shape != 2;
            var up = Vector3.up * .12f;
            if (shape == 0)
            {
                line.positionCount = 40;
                for (int i = 0; i < 40; i++)
                {
                    float yaw = i * (Mathf.PI * 2f / 40);
                    line.SetPosition(i, origin + up + new Vector3(Mathf.Cos(yaw), 0, Mathf.Sin(yaw)) * radius);
                }
            }
            else if (shape == 1)
            {
                Vector3 right = Vector3.Cross(Vector3.up, end - origin).normalized * radius;
                if (right.sqrMagnitude < .001f) right = Vector3.right * radius;
                line.positionCount = 4;
                line.SetPosition(0, origin + right + up); line.SetPosition(1, end + right + up);
                line.SetPosition(2, end - right + up); line.SetPosition(3, origin - right + up);
            }
            else
            {
                line.positionCount = 23;
                line.SetPosition(0, origin + up);
                for (int i = 0; i <= 20; i++)
                    line.SetPosition(i + 1, origin + up + Quaternion.Euler(0, Mathf.Lerp(-angle, angle, i / 20f), 0) * end.normalized * radius);
                line.SetPosition(22, origin + up);
            }
        }

        MaterialPropertyBlock indicatorProperties;

        void TickGmIndicators(float now)
        {
            foreach (var item in skillIndicators)
                if (item != null && item.line && now >= item.expires) item.line.enabled = false;
            if (!gmIndicators || !HasLiveCaster) return;
            if (barrierTicks > 0)
                IndicatePath(barrierCenter - barrierRight * 2.5f, barrierCenter + barrierRight * 2.5f, barrierDepth, .12f, true);
            if (blessingTicks > 0) IndicateRing(blessingCenter, blessingEvolved ? 3f : 2.5f, .12f, healing: true);
            if (snowSeconds > 0) IndicateRing(snowCenter, 3f, .12f);
            if (iceRank > 0 && now < iceExpires) IndicateRing(iceCenter, iceRadius, .12f);
            foreach (var zone in valkyrieZones)
                if (now < zone.expiry)
                {
                    if (!zone.evolved) IndicateRing(zone.center, 1.5f, .12f);
                    else
                    {
                        IndicateRing(zone.center + Vector3.forward * 1.5f, 1.5f, .12f);
                        IndicateRing(zone.center - Vector3.forward * 1.5f, 1.5f, .12f);
                        IndicateRing(zone.center + Vector3.right * 1.5f, 1.5f, .12f);
                        IndicateRing(zone.center - Vector3.right * 1.5f, 1.5f, .12f);
                    }
                }
            foreach (var orb in orbs)
                IndicateRing(orb.previous, 1.25f, .12f);
            foreach (var boulder in boulders)
                IndicateRing(boulder.previous, boulder.radius, .12f);
            if (shipActive)
                IndicatePath(shipPrevious - shipDirection * 1f, shipPrevious + shipDirection * 1f, 1.25f, .12f);
            for (int i = 0; i < pendingCometCount; i++)
                IndicateRing(cometPoints[i], Mathf.Min(cometCastRank + 1, 4), .12f, true);
            foreach (var viper in vipers) IndicatePath(viper.start, viper.end, .6f, .12f, true);
            foreach (var infection in infections)
                if (infection.target) IndicateRing(infection.target.transform.position, .55f, .12f);
            foreach (var star in firestars)
                IndicateRing(star.center, 1.5f, .12f);
            for (int i = 0; i < leviathanCasts.Length; i++)
                if (leviathanCasts[i].hits > 0) IndicateRing(leviathanCasts[i].point, 2f, .12f, true);
            for (int i = 0; i < warriorAttackCount; i++)
                if (warriorAttacks[i].target) IndicateRing(warriorAttacks[i].target.transform.position,
                    warriorEvolved ? 2f : 1f, .12f, true);
            if (ravenTicks > 0)
            {
                if (ravenEvolved) IndicatePath(ravenOrigin, ravenPoint, .6f, .12f, true);
                else IndicateRing(ravenPoint, ravenRadius, .12f, true);
            }
            if (bubbleActor && now < bubbleExpiry) IndicateRing(caster.transform.position, .9f, .12f, healing: true);
            if (RaiseZones.Contains(this))
            {
                DrawRaiseIndicator(raiseOrigin);
                if (raiseCastCount == 2) DrawRaiseIndicator(raiseRepeatOrigin);
            }
            if (breathTicks > 0) IndicatePath(breathOrigin, breathOrigin + breathDirection * 12f, 1.5f, .12f);
        }

        void DrawRaiseIndicator(Vector3 origin)
        {
            if (!raiseEvolved) IndicateRing(origin, 2.5f, .12f);
            else
                for (int side = -1; side <= 1; side += 2)
                {
                    Vector3 start = origin + raiseRight * (side * 1.5f);
                    IndicatePath(start, start + raiseForward * 10f, .75f, .12f);
                }
        }

        void ClearGmIndicators()
        {
            for (int i = 0; i < skillIndicators.Length; i++)
            {
                if (skillIndicators[i] != null && skillIndicators[i].line) Destroy(skillIndicators[i].line.gameObject);
                skillIndicators[i] = null;
            }
            if (indicatorMaterial) Destroy(indicatorMaterial);
            indicatorMaterial = null; indicatorProperties = null; indicatorCursor = 0;
        }
    }
}
