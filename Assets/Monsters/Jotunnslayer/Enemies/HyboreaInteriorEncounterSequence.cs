using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    [DisallowMultipleComponent]
    public sealed class HyboreaInteriorEncounterSequence : MonoBehaviour
    {
        readonly HyboreaInteriorWave[] waves = new HyboreaInteriorWave[10];
        HyboreaInteriorMiniBossWaves miniBossWaves;
        HyboreaEndlessInteriorOpeningWave endlessCore;
        HyboreaEndlessBonusWaves endlessBonus;
        bool ownsEndlessBonus;
        float matchStartedAt;
        float finalTime;
        bool started;
        bool ownsMiniBossWaves;

        public bool TryStartEndlessCore(CharacterInstance player, float matchStartedAt)
        {
            if (!isActiveAndEnabled || started || GetComponent<HyboreaEndlessInteriorOpeningWave>()) return false;
            var occupiedMiniBoss = GetComponent<HyboreaInteriorMiniBossWaves>();
            if (occupiedMiniBoss && occupiedMiniBoss.Running) return false;
            endlessCore = gameObject.AddComponent<HyboreaEndlessInteriorOpeningWave>();
            if (!endlessCore.TryStartGroup(player, matchStartedAt))
            {
                endlessCore.enabled = false;
                Destroy(endlessCore);
                endlessCore = null;
                return false;
            }
            if (!endlessBonus && !TryGetComponent(out endlessBonus))
                endlessBonus = gameObject.AddComponent<HyboreaEndlessBonusWaves>();
            if (!endlessBonus.TryStart(player, matchStartedAt))
            {
                StopCore();
                return false;
            }
            ownsEndlessBonus = true;
            started = true;
            return true;
        }

        public bool TryStartCore(CharacterInstance player, float matchStartedAt,
            HyboreaInteriorDifficulty difficulty = HyboreaInteriorDifficulty.Easy)
        {
            int count = HyboreaInteriorWave.GetWaveCount(difficulty);
            if (!isActiveAndEnabled || started || count == 0) return false;
            var occupiedEndlessBonus = GetComponent<HyboreaEndlessBonusWaves>();
            if (occupiedEndlessBonus && occupiedEndlessBonus.Running) return false;

            var random = new System.Random();
            for (int i = 0; i < count; i++)
            {
                if (HyboreaInteriorWave.TryStart(player, matchStartedAt, random, out waves[i], i + 1, difficulty))
                {
                    waves[i].BindSequence(this);
                    continue;
                }

                StopCore();
                return false;
            }

            if (!miniBossWaves && !TryGetComponent(out miniBossWaves))
                miniBossWaves = gameObject.AddComponent<HyboreaInteriorMiniBossWaves>();
            if (!miniBossWaves.TryStart(player, matchStartedAt, difficulty, includeMilipedes: true))
            {
                StopCore();
                return false;
            }
            ownsMiniBossWaves = true;

            this.matchStartedAt = matchStartedAt;
            finalTime = difficulty switch
            {
                HyboreaInteriorDifficulty.Hard => 1020f,
                HyboreaInteriorDifficulty.Insane => 1200f,
                _ => 900f
            };
            started = true;
            return true;
        }

        internal int GetAvailableSlots(HyboreaInteriorWave requester, int requesterActive, float now)
        {
            int active = requesterActive;
            foreach (var wave in waves)
                if (wave && wave != requester) active += wave.CountActiveActors();

            int limit = Mathf.RoundToInt(Mathf.Lerp(50f, 150f, (now - matchStartedAt) / finalTime));
            return Mathf.Max(0, limit - active);
        }

        public void StopCore()
        {
            if (endlessCore)
            {
                endlessCore.enabled = false;
                Destroy(endlessCore);
                endlessCore = null;
            }
            if (ownsMiniBossWaves && miniBossWaves) miniBossWaves.StopWaves();
            ownsMiniBossWaves = false;
            if (ownsEndlessBonus && endlessBonus) endlessBonus.StopWaves();
            ownsEndlessBonus = false;
            for (int i = 0; i < waves.Length; i++)
            {
                var wave = waves[i];
                if (wave)
                {
                    wave.enabled = false;
                    Destroy(wave);
                }
                waves[i] = null;
            }
            started = false;
        }

        void OnDisable() => StopCore();
    }
}
