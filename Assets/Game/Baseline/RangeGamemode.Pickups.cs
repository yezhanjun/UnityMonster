using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay.Gamemodes
{
    public partial class RangeGamemode
    {
        readonly List<uint> worldPickupSnapshot = new List<uint>();
        bool updatingWorldPickups;

        internal void UpdateToothPickups()
        {
            if (!isServer || updatingWorldPickups || toothPickups.Count == 0) return;
            updatingWorldPickups = true;
            try
            {
                PublishMonsterLogMotion();
                worldPickupSnapshot.Clear();
                foreach (uint id in toothPickups.Keys) worldPickupSnapshot.Add(id);
                foreach (uint id in worldPickupSnapshot)
                {
                    if (!toothPickups.TryGetValue(id, out var pickup)) continue;
                    if (Time.time >= pickup.ExpiresAt) { RemoveWorldPickup(id); continue; }
                    if (!string.IsNullOrEmpty(pickup.MonsterLogId))
                    {
                        if (Time.time >= pickup.CollectAfter && monsterLogBodies.TryGetValue(id, out var motion) &&
                            motion.Body && TryCollectMonsterLog(pickup, motion.Body)) RemoveWorldPickup(id);
                        continue;
                    }

                    CharacterInstance recipient = null;
                    PlayerInstance player = null;
                    int itemIndex = string.IsNullOrEmpty(pickup.CatalogId) ? -1 :
                        RangeResearchItems.CatalogIndex(pickup.CatalogId);
                    if (!string.IsNullOrEmpty(pickup.CatalogId) && itemIndex < 0) continue;
                    // Finish enumerating bodies before Grant/Heal or external callbacks can destroy/spawn one.
                    foreach (var health in GameManager.HealthInstances.Values)
                    {
                        if (health is not CharacterInstance character || !character || character.CurrentHealth <= 0 ||
                            character.Team != pickup.Team ||
                            (character.transform.position - pickup.Position).sqrMagnitude > 1.25f * 1.25f) continue;
                        if (pickup.Gold > 0 || itemIndex >= 0)
                        {
                            var identity = character.connectionToClient?.identity;
                            if (!identity || !identity.TryGetComponent(out player) || player.BOT ||
                                player.MyCharacter != character) continue;
                        }
                        recipient = character;
                        break;
                    }
                    if (!recipient) continue;
                    var notification = new RangePickupEvent(recipient,
                        itemIndex >= 0 ? RangePickupClass.Item : RangePickupClass.Minor, pickup.CatalogId);
                    try
                    {
                        if (pickup.Gold > 0)
                        {
                            player.RainGold = (int)System.Math.Min(int.MaxValue, player.RainGold + (long)pickup.Gold);
                            NotifyWorldPickupBody(in notification);
                            // Original MoneyPickup has no GlobalEventManager pickup-item notification.
                        }
                        else if (itemIndex >= 0)
                        {
                            if (!recipient.TryGetComponent<RangeResearchItems>(out var items))
                                items = recipient.gameObject.AddComponent<RangeResearchItems>();
                            items.Grant(itemIndex);
                            player.RecordRainChestItems(items);
                            NotifyWorldPickupBody(in notification);
                            // This server event is the TPS integration interface, not the original
                            // GenericPickupController client message handler's presentation event.
                            NotifyWorldPickupGlobal(in notification);
                        }
                        else
                        {
                            // Original HealthPickup updates stats in OnPickup before calculating fractional healing.
                            NotifyWorldPickupBody(in notification);
                            if (recipient && recipient.CurrentHealth > 0)
                            {
                                recipient.ServerHeal(8 + Mathf.RoundToInt(recipient.MaxHealth * .02f * pickup.Stacks),
                                    notification.BodyNetId);
                                recipient.RpcRainMonsterToothPickup();
                            }
                            NotifyWorldPickupGlobal(in notification);
                        }
                    }
                    finally { RemoveWorldPickup(id); }
                }
            }
            finally
            {
                worldPickupSnapshot.Clear();
                updatingWorldPickups = false;
            }
        }

        void RemoveWorldPickup(uint id)
        {
            if (monsterLogBodies.Remove(id, out var motion) && motion.Body) Destroy(motion.Body.gameObject);
            toothPickups.Remove(id);
        }

        void NotifyWorldPickupBody(in RangePickupEvent pickup)
        {
            try
            {
                if (!pickup.Body) return;
                if (!pickup.Body.TryGetComponent<RangeResearchItems>(out var items))
                    items = pickup.Body.gameObject.AddComponent<RangeResearchItems>();
                items.NotifyServerPickup(in pickup);
            }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }

        void NotifyWorldPickupGlobal(in RangePickupEvent pickup)
        {
            try { ServerCharacterPickupItem?.Invoke(pickup); }
            catch (System.Exception exception) { Debug.LogException(exception); }
        }
    }
}
