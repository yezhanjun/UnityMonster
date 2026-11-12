using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchEnemy
    {
        internal static void ClearHostileProjectiles(Health holder, Vector3 center, float radius)
        {
            if (!holder || !holder.isServer) return;
            float radiusSquared = radius * radius;
            foreach (var candidate in GameManager.HealthInstances.Values)
            {
                if (!candidate || candidate.Team < 0 || candidate.Team == holder.Team ||
                    !candidate.TryGetComponent<RangeResearchEnemy>(out var enemy)) continue;
                enemy.ClearProjectilesInRange(center, radiusSquared);
            }
        }

        void ClearProjectilesInRange(Vector3 center, float radiusSquared)
        {
            if (!body || !body.isServer) return;

            if (queenAcidShots != null)
                for (int i = 0; i < queenAcidShots.Length; i++)
                {
                    ref QueenAcidShot shot = ref queenAcidShots[i];
                    if (shot.Expires <= 0f || (shot.Position - center).sqrMagnitude > radiusSquared) continue;
                    shot = default;
                    HideQueenSpitVisual(i);
                    body.RainQueenSpit.Remove(i);
                }

            if (bruiserFireballs != null)
                for (int i = 0; i < bruiserFireballs.Length; i++)
                {
                    ref BruiserFireball shot = ref bruiserFireballs[i];
                    if (shot.Expires <= 0f || (shot.Position - center).sqrMagnitude > radiusSquared) continue;
                    FinishBruiserFireball(i);
                }

            if (greaterWispShots != null)
                for (int i = 0; i < greaterWispShots.Length; i++)
                {
                    ref GreaterWispShot shot = ref greaterWispShots[i];
                    if (shot.Expires <= 0f || (shot.Position - center).sqrMagnitude > radiusSquared) continue;
                    shot = default;
                    HideGreaterWispGhost(i);
                    body.RainGreaterWispShots.Remove(i);
                }
        }
    }
}
