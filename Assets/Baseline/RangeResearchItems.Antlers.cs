using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeResearchItems
    {
        const int ElusiveAntlersIndex = 104;
        int elusiveAntlers, antlerBuffs;
        float antlerBuffUntil, antlerSpawnTimer = 3f;
        Vector3 antlerLastPosition;
        bool antlerPositionSampled;
        internal float AntlerSpawnRemaining => antlerSpawnTimer;

        internal static float AntlerSpawnInterval(int stacks) =>
            2f + 24f / (2f + Mathf.Max(1, stacks));
        internal static int AntlerBuffCap(int stacks) =>
            (int)System.Math.Min(int.MaxValue, 3L * System.Math.Max(1, stacks));

        void SyncAntlers()
        {
            if (owner is CharacterInstance body) body.SetResearchAntlerStacks(elusiveAntlers, antlerBuffs);
        }

        void ResetAntlerSpawnTimer()
        {
            antlerSpawnTimer = 3f;
            antlerPositionSampled = false;
        }

        void ClearAntlerBuff()
        {
            antlerBuffs = 0;
            antlerBuffUntil = 0f;
            SyncAntlers();
        }

        // Existing world shards remain collectible after their generator loses the item.
        // Only owned inventory is persisted across bodies; picked-up temporary buffs are not.
        internal bool TryCollectAntlerBuff()
        {
            if (!owner || !owner.isServer || owner.CurrentHealth <= 0 || !isActiveAndEnabled) return false;
            antlerBuffUntil = Time.time + 12f;
            if (antlerBuffs < AntlerBuffCap(elusiveAntlers)) antlerBuffs++;
            SyncAntlers();
            return true;
        }

        void FixedUpdate()
        {
            if (!owner || !owner.isServer) return;
            if (owner.CurrentHealth <= 0)
            {
                if (antlerBuffs != 0) ClearAntlerBuff();
                ResetAntlerSpawnTimer();
                return;
            }
            if (antlerBuffs > 0 && Time.time >= antlerBuffUntil) ClearAntlerBuff();
            Vector3 position = owner.transform.position;
            Vector3 velocity = antlerPositionSampled
                ? (position - antlerLastPosition) / Mathf.Max(.0001f, Time.fixedDeltaTime) : Vector3.zero;
            antlerLastPosition = position;
            antlerPositionSampled = true;
            if (elusiveAntlers <= 0 || owner is not CharacterInstance body ||
                GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                mode.CountAntlerShards(body.netId) >= 10) return;
            antlerSpawnTimer -= Time.fixedDeltaTime;
            if (antlerSpawnTimer > 0f) return;
            mode.TrySpawnAntlerShard(body, velocity);
            antlerSpawnTimer = AntlerSpawnInterval(elusiveAntlers);
        }
    }
}
