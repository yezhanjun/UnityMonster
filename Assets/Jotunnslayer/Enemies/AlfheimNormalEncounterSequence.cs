using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DisallowMultipleComponent]
    public sealed class AlfheimNormalEncounterSequence : MonoBehaviour
    {
        // W1–W81 contain all actors in the 97680 core group.
        internal const int OpeningWaveCount = 81;

        [SerializeField] AlfheimBossEncounter bossEncounter;

        readonly AlfheimNormalOpeningWave[] coreWaves = new AlfheimNormalOpeningWave[OpeningWaveCount];
        CharacterInstance target;
        bool coreStarted;
        bool bossStarted;

        internal void BindBoss(AlfheimBossEncounter encounter)
        {
            if (!coreStarted) bossEncounter = encounter;
        }

        public bool TryStartCore(CharacterInstance player, float matchStartedAt)
        {
            if (!isActiveAndEnabled || coreStarted || !player || player.CurrentHealth <= 0)
                return false;

            var random = new System.Random();
            for (int i = 0; i < coreWaves.Length; i++)
            {
                if (AlfheimNormalOpeningWave.TryStart(player, matchStartedAt, random, out coreWaves[i], i + 1))
                    continue;

                StopCoreWaves();
                return false;
            }

            target = player;
            coreStarted = true;
            return true;
        }

        public bool TryStartBoss()
        {
            if (!isActiveAndEnabled || !coreStarted || bossStarted || !target || !bossEncounter ||
                !bossEncounter.TryBegin(target))
                return false;

            bossStarted = true;
            StopCoreWaves();
            return true;
        }

        void StopCoreWaves()
        {
            for (int i = 0; i < coreWaves.Length; i++)
            {
                if (coreWaves[i])
                {
                    coreWaves[i].enabled = false;
                    Destroy(coreWaves[i]);
                }
                coreWaves[i] = null;
            }
        }

        void OnDisable()
        {
            StopCoreWaves();
            if (bossStarted && bossEncounter) bossEncounter.enabled = false;
            target = null;
        }
    }
}
