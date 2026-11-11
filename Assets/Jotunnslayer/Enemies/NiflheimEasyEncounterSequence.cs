using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DisallowMultipleComponent]
    public sealed class NiflheimEasyEncounterSequence : MonoBehaviour
    {
        static readonly int[] CoreWaveIndices = { 4, 7, 8 };

        [SerializeField] NiflheimEasyBossEncounter bossEncounter;

        readonly NiflheimEasySkeletonWave[] coreWaves = new NiflheimEasySkeletonWave[CoreWaveIndices.Length];
        CharacterInstance target;
        bool bossStarted;

        public bool TryStartCore(CharacterInstance player, float matchStartedAt)
        {
            if (!isActiveAndEnabled || target || bossStarted || !player || player.CurrentHealth <= 0)
                return false;

            for (int i = 0; i < CoreWaveIndices.Length; i++)
            {
                if (NiflheimEasySkeletonWave.TryStart(player, matchStartedAt, CoreWaveIndices[i], out coreWaves[i]))
                    continue;

                StopCoreWaves();
                return false;
            }

            target = player;
            return true;
        }

        public bool TryStartBoss()
        {
            if (!isActiveAndEnabled || bossStarted || !target || !bossEncounter ||
                !bossEncounter.TryBegin(target)) return false;

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
