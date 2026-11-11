using System;
using System.Collections;
using System.Reflection;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeGmSmokeTest
    {
        static IEnumerator VerifyCollectorPickups(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = (Gamemodes.RangeGamemode)GameManager.Gamemode;
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
            var drops = (SyncDictionary<uint, Gamemodes.RangeGamemode.ToothPickup>)
                typeof(Gamemodes.RangeGamemode).GetField("toothPickups", flags).GetValue(mode);
            var drop = typeof(Gamemodes.RangeGamemode).GetMethod("DropRewardPickup", flags);
            var nextId = typeof(Gamemodes.RangeGamemode).GetField("nextToothPickup", flags);
            var player = owner.connectionToClient.identity.GetComponent<PlayerInstance>();
            var motor = owner.GetComponent<CharacterMotor>();
            bool enabledMotor = motor.enabled;
            Vector3 position = owner.transform.position;
            int health = owner.CurrentHealth, gold = player.RainGold;
            float attack = owner.RainAttackPower, regen = owner.RainHealthRegen, timeScale = Time.timeScale;
            var snapshot = new RangeResearchItems.PersistentItems(items);
            int collector = RangeResearchItems.CatalogIndex("SpeedOnPickup");
            int local = 0, global = 0;
            bool order = true, metadata = true;
            int healthAtCallback = 0;
            uint activeId = 0;
            bool mutate = false;
            uint deferredId = 0;
            try
            {
                items.Clear();
                motor.enabled = false;
                owner.transform.position += Vector3.up * 250f;
                owner.RainAttackPower = 12f;
                owner.RainHealthRegen = 10f;
                items.ServerPickup += OnLocal;
                mode.ServerCharacterPickupItem += OnGlobal;

                Collect();
                bool withoutItem = local == 1 && global == 1 && items.CollectorBuffCount == 0;
                local = global = 0;
                items.Grant(collector);
                bool grantSilent = local == 0 && global == 0 && items.CollectorBuffCount == 0 &&
                    new RangeResearchItems.PersistentItems(items).NeedsRestore;
                items.Grant(RangeResearchItems.CatalogIndex("CritGlasses"));
                items.Grant(RangeResearchItems.CatalogIndex("Syringe"));
                items.Grant(RangeResearchItems.CatalogIndex("PersonalShield"));
                items.Grant(RangeResearchItems.CatalogIndex("Stew"));
                int maximum = owner.MaxHealth;
                float baseSpeed = motor.GetMoveSpeed(false), baseAttackSpeed = owner.ResearchAttackSpeed;
                float baseRegen = owner.EffectiveRainHealthRegen;
                owner.CurrentHealth = 1;
                Collect();
                bool healingOrder = healthAtCallback == 1 && owner.CurrentHealth ==
                    1 + 8 + Mathf.RoundToInt(owner.MaxHealth * .02f);
                owner.CurrentHealth = owner.MaxHealth;
                float expectedAttack = (12f + baseRegen) * 1.03f;
                bool stats = owner.MaxHealth == (int)Math.Round(maximum * (double)(1f + .03f)) &&
                    Mathf.Approximately(items.CriticalChance, .103f) &&
                    Mathf.Approximately(owner.ResearchAttackSpeed, baseAttackSpeed * 1.03f) &&
                    Mathf.Approximately(owner.EffectiveRainHealthRegen, baseRegen * 1.03f) &&
                    Mathf.Approximately(owner.EffectiveRainAttackPower, expectedAttack) &&
                    owner.ScaleRainWeaponDamage(24) == Mathf.RoundToInt(expectedAttack * 2f) &&
                    Mathf.Approximately(motor.GetMoveSpeed(false), baseSpeed * 1.03f) &&
                    owner.ResearchShieldCapacity == Mathf.RoundToInt(owner.MaxHealth * .08f) &&
                    items.ReduceIncomingDamage(1000, 20f) == (int)Math.Round(1000d * (100f / (100f + 20f * 1.03f))) &&
                    items.ReduceIncomingDamage(1000, -20f) == (int)Math.Round(1000d * (2f - 100f / (100f + 20f * 1.03f)));
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                owner.ServerSetMaximumHealth(owner.MaxHealth);
                stats &= owner.MaxHealth == (int)Math.Round(maximum * (double)(1f + .03f));
                Collect(); Collect(); Collect();
                bool cap = items.CollectorBuffCount == 3;
                yield return new WaitForSeconds(1.25f);
                float beforeRefresh = items.CollectorBuffRemaining;
                Collect();
                cap &= items.CollectorBuffCount == 3 && beforeRefresh < 19f && items.CollectorBuffRemaining > 19.9f;
                items.Grant(collector);
                Collect(); Collect(); Collect();
                cap &= items.CollectorBuffCount == 5;
                items.Remove(collector);
                Collect();
                cap &= items.CollectorBuffCount == 5;
                items.Remove(collector);
                yield return new WaitForSeconds(.2f);
                float beforeRemovedPickup = items.CollectorBuffRemaining;
                Collect();
                bool removed = items.CollectorBuffCount == 5 && items.CollectorBuffRemaining <= beforeRemovedPickup;
                float pauseRemaining = items.CollectorBuffRemaining;
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.2f);
                bool pause = Mathf.Abs(items.CollectorBuffRemaining - pauseRemaining) < .01f;
                Time.timeScale = timeScale;
                float expiryStart = Time.time;
                yield return new WaitForSeconds(items.CollectorBuffRemaining + .1f);
                bool expired = items.CollectorBuffCount == 0 && owner.MaxHealth == maximum;

                items.Clear();
                items.Grant(collector);
                local = global = 0;
                Collect(0, "Syringe");
                bool itemPickup = local == 1 && global == 1 && owner.ResearchSyringes == 1 && items.CollectorBuffCount == 1;
                local = global = 0;
                int beforeGold = player.RainGold;
                Collect(25);
                bool moneyPickup = local == 1 && global == 0 && player.RainGold == beforeGold + 25 && items.CollectorBuffCount == 2;
                items.enabled = false;
                bool disable = items.CollectorBuffCount == 0 && owner.ResearchCollectors == 1;
                items.enabled = true;
                Collect();
                var inventory = new RangeResearchItems.PersistentItems(items);
                items.Clear(); inventory.Restore(items);
                bool restore = owner.ResearchCollectors == 1 && items.CollectorBuffCount == 0;
                Collect();
                owner.CurrentHealth = 0;
                yield return null;
                bool death = items.CollectorBuffCount == 0;
                owner.CurrentHealth = owner.MaxHealth;
                items.Clear();
                items.Grant(RangeResearchItems.CatalogIndex("SpeedBoostPickup"));
                bool antlerRestore = new RangeResearchItems.PersistentItems(items).NeedsRestore;
                items.Clear();
                items.Grant(collector);
                local = global = 0;
                mutate = true;
                Collect();
                bool mutation = local == 1 && global == 1 && deferredId != 0 && drops.ContainsKey(deferredId);
                mutate = false;
                activeId = deferredId;
                mode.UpdateToothPickups();
                mutation &= local == 2 && global == 2 && !drops.ContainsKey(deferredId);
                if (withoutItem && grantSilent && healingOrder && stats && cap && removed && pause && expired &&
                    itemPickup && moneyPickup && disable && restore && death && antlerRestore && mutation && order && metadata)
                    Debug.Log($"RANGE_SMOKE_COLLECTOR_CHECKED eightStats=True healOrder=True capRefresh=True removedExpiry=True pause=True actualExpiryWait={Time.time - expiryStart:F1} item=True gold=True disable=True death=True inventoryRestore=True antlerRestore=True reentrantMutation=True");
                else Debug.LogError($"RANGE_SMOKE_COLLECTOR without={withoutItem} grant={grantSilent} heal={healingOrder} stats={stats} cap={cap} removed={removed} pause={pause} expired={expired} item={itemPickup} gold={moneyPickup} disable={disable} restore={restore} death={death} antlers={antlerRestore} mutation={mutation} order={order} metadata={metadata}");
            }
            finally
            {
                Time.timeScale = timeScale;
                items.ServerPickup -= OnLocal;
                mode.ServerCharacterPickupItem -= OnGlobal;
                if (activeId != 0) drops.Remove(activeId);
                if (deferredId != 0) drops.Remove(deferredId);
                items.Clear(); snapshot.Restore(items);
                owner.RainAttackPower = attack; owner.RainHealthRegen = regen;
                player.RainGold = gold;
                owner.CurrentHealth = health;
                owner.transform.position = position;
                motor.enabled = enabledMotor;
            }

            void Collect(int coins = 0, string catalog = null)
            {
                drop.Invoke(mode, new object[] { owner.transform.position, owner.Team, 1, coins, catalog });
                activeId = (uint)nextId.GetValue(mode);
                mode.UpdateToothPickups();
            }
            void OnLocal(RangePickupEvent pickup)
            {
                if (pickup.Body != owner) return;
                local++;
                healthAtCallback = owner.CurrentHealth;
                metadata &= pickup.BodyNetId == owner.netId && pickup.Quantity == 1 &&
                    pickup.PickupClass == (pickup.CatalogId == null ? RangePickupClass.Minor : RangePickupClass.Item);
                order &= drops.ContainsKey(activeId);
                int count = items.CollectorBuffCount;
                mode.UpdateToothPickups();
                order &= items.CollectorBuffCount == count;
                if (mutate)
                {
                    drops.Remove(activeId);
                    drop.Invoke(mode, new object[] { owner.transform.position, owner.Team, 1, 0, null });
                    deferredId = (uint)nextId.GetValue(mode);
                }
            }
            void OnGlobal(RangePickupEvent pickup)
            {
                if (pickup.Body == owner) global++;
            }
        }
    }
}
