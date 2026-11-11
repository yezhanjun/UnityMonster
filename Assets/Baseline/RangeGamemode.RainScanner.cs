using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode
    {
        const float RainScannerRadius = 500f;
        const float RainScannerPulseSpeed = 125f;
        const double RainScannerRevealDuration = 10d;

        public struct RainScannerPulse
        {
            public Vector3 Origin;
            public double StartedAt;
        }

        readonly SyncList<RainScannerPulse> rainScannerPulses = new SyncList<RainScannerPulse>();

        [SyncVar] double rainScannerTime;

        internal double RainScannerTime => rainScannerTime;
        internal bool HasRainScannerPulses => rainScannerPulses.Count != 0;

        [Server]
        internal void ServerStartRainScanner(Vector3 origin)
        {
            rainScannerPulses.Add(new RainScannerPulse { Origin = origin, StartedAt = rainScannerTime });
        }

        void UpdateRainScannerPulses()
        {
            if (rainScannerPulses.Count == 0) return;

            rainScannerTime += Time.fixedDeltaTime;
            double oldestStart = rainScannerTime - RainScannerRadius / RainScannerPulseSpeed - RainScannerRevealDuration;
            while (rainScannerPulses.Count != 0 && rainScannerPulses[0].StartedAt <= oldestStart)
                rainScannerPulses.RemoveAt(0);
        }

        internal bool IsRainScannerPointRevealed(Vector3 point, double now)
        {
            for (int i = 0; i < rainScannerPulses.Count; i++)
            {
                var pulse = rainScannerPulses[i];
                float distanceSquared = (point - pulse.Origin).sqrMagnitude;
                if (distanceSquared > RainScannerRadius * RainScannerRadius) continue;
                double elapsed = now - pulse.StartedAt - Mathf.Sqrt(distanceSquared) / RainScannerPulseSpeed;
                if (elapsed >= 0d && elapsed < RainScannerRevealDuration) return true;
            }
            return false;
        }
    }
}
