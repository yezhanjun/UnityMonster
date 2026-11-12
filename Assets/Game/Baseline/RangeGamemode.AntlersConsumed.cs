using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode
    {
        readonly HashSet<RangeAntlerConsumedEffect> antlerConsumedEffects = new HashSet<RangeAntlerConsumedEffect>();
        // Source EffectManager.Initialize(1,1), AlwaysGrowable=true; EffectPool caps idle retention at 20.
        readonly List<RangeAntlerConsumedEffect> antlerConsumedPool = new List<RangeAntlerConsumedEffect>(20);
        bool antlerConsumedPoolInitialized;
        internal int AntlerConsumedIdleCount => antlerConsumedPool.Count;
        internal int AntlerConsumedActiveCount => antlerConsumedEffects.Count;

        [ClientRpc]
        void RpcAntlerConsumed(uint recipientId, Vector3 shardOrigin)
        {
            Transform recipient = null;
            if (NetworkClient.spawned.TryGetValue(recipientId, out var identity) && identity)
                recipient = identity.transform;
            // A vanished body falls back to the transmitted origin, as unresolved EffectData does.
            PlayAntlerConsumedEffect(recipient, shardOrigin);
        }

        internal RangeAntlerConsumedEffect PlayAntlerConsumedEffect(Transform recipient, Vector3 origin)
        {
            if (!antlerConsumedPoolInitialized)
            {
                antlerConsumedPool.Add(RangeAntlerConsumedEffect.CreateInactive(this, true));
                antlerConsumedPoolInitialized = true;
            }
            RangeAntlerConsumedEffect effect = null;
            while (!effect && antlerConsumedPool.Count > 0)
            {
                int index = antlerConsumedPool.Count - 1;
                effect = antlerConsumedPool[index];
                antlerConsumedPool.RemoveAt(index);
            }
            if (!effect) effect = RangeAntlerConsumedEffect.CreateInactive(this, true);
            antlerConsumedEffects.Add(effect);
            effect.Activate(recipient, origin);
            return effect;
        }

        internal void ReturnAntlerConsumedEffect(RangeAntlerConsumedEffect effect)
        {
            if (!antlerConsumedEffects.Remove(effect)) return; // Duplicate release cannot cache an instance twice.
            effect.gameObject.SetActive(false);
            if (antlerConsumedPool.Count < 20) antlerConsumedPool.Add(effect);
            else effect.ReleaseFromOwner();
        }

        internal void ForgetAntlerConsumedEffect(RangeAntlerConsumedEffect effect)
        {
            antlerConsumedEffects.Remove(effect);
            antlerConsumedPool.Remove(effect); // Body destruction also invalidates idle children in the original pool.
        }

        internal void ClearAntlerConsumedEffects()
        {
            foreach (var effect in antlerConsumedEffects)
                if (effect) effect.ReleaseFromOwner();
            antlerConsumedEffects.Clear();
            foreach (var effect in antlerConsumedPool)
                if (effect) effect.ReleaseFromOwner();
            antlerConsumedPool.Clear();
            antlerConsumedPoolInitialized = false;
        }
    }
}
