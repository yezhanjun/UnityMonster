using System.Collections;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeGmSmokeTest
    {
        static IEnumerator VerifyAntlerPickupCallbacks(CharacterInstance owner, RangeResearchItems items)
        {
            var mode = (Gamemodes.RangeGamemode)GameManager.Gamemode;
            var snapshot = new RangeResearchItems.PersistentItems(items);
            CharacterInstance recipient = null;
            RangeResearchItems receiver = null;
            int local = 0, global = 0, phase = 0;
            bool metadata = true, ordering = true, reentrant = true, mutation = true;
            uint recipientId = 0;
            Vector3 pickupPosition = owner.transform.position + Vector3.up * 200f;
            int antlers = RangeResearchItems.CatalogIndex("SpeedBoostPickup");
            try
            {
                items.Clear();
                items.Grant(antlers);
                recipient = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    pickupPosition - Vector3.up, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in recipient.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                recipient.Team = owner.Team;
                recipient.MaxHealth = recipient.CurrentHealth = 1000;
                recipient.RangeInvulnerable = true;
                NetworkServer.Spawn(recipient.gameObject);
                recipientId = recipient.netId;
                recipient.GetComponent<CharacterMotor>().enabled = false;
                var ai = recipient.GetComponent<BotAI>();
                if (ai) { ai.enabled = false; ai.StopAllCoroutines(); }
                receiver = recipient.gameObject.AddComponent<RangeResearchItems>();
                receiver.ServerPickup += OnLocal;
                mode.ServerCharacterPickupItem += OnGlobal;
                yield return null;
                receiver.Grant(RangeResearchItems.CatalogIndex("Syringe"));
                bool grantsSilent = local == 0 && global == 0;
                receiver.Clear();

                bool spawned = SpawnPair();
                Physics.SyncTransforms();
                mode.UpdateRainAntlers();
                bool normal = local == 2 && global == 2 && recipient.ResearchAntlerBuffs == 2 &&
                    mode.CountAntlerShards(owner.netId) == 0;

                phase = 1; local = global = 0;
                receiver.Clear();
                spawned &= SpawnPair();
                mode.UpdateRainAntlers();
                bool cleared = local == 1 && global == 1 && mode.CountAntlerShards(owner.netId) == 0;

                phase = 2; local = global = 0;
                receiver.Clear();
                spawned &= mode.SpawnAntlerShardAt(owner, pickupPosition);
                mode.UpdateRainAntlers();
                bool destruction = local == 1 && global == 1 &&
                    !NetworkServer.spawned.ContainsKey(recipientId) && mode.CountAntlerShards(owner.netId) == 0;
                yield return null;
                destruction &= !recipient;
                if (spawned && grantsSilent && normal && cleared && destruction && metadata && ordering && reentrant && mutation)
                    Debug.Log("RANGE_SMOKE_ANTLERS_PICKUP_CALLBACKS_CHECKED minor=True noneIndex=True quantity=1 ordered=True grantSilent=True reentrant=True clearDuringCallback=True bodyDestroy=True");
                else Debug.LogError($"RANGE_SMOKE_ANTLERS_PICKUP_CALLBACKS spawn={spawned} grants={grantsSilent} normal={normal} clear={cleared} destroy={destruction} metadata={metadata} order={ordering} reentrant={reentrant} mutation={mutation}");
            }
            finally
            {
                if (receiver) receiver.ServerPickup -= OnLocal;
                mode.ServerCharacterPickupItem -= OnGlobal;
                mode.ClearAntlerShards(owner.netId);
                if (recipient) NetworkServer.Destroy(recipient.gameObject);
                items.Clear();
                snapshot.Restore(items);
            }

            bool SpawnPair() => mode.SpawnAntlerShardAt(owner, pickupPosition) &&
                mode.SpawnAntlerShardAt(owner, pickupPosition);

            void CheckMetadata(RangePickupEvent pickup)
            {
                metadata &= ReferenceEquals(pickup.Body, recipient) && pickup.BodyNetId == recipientId &&
                    pickup.PickupClass == RangePickupClass.Minor && pickup.CatalogId == null && pickup.Quantity == 1;
            }

            void OnLocal(RangePickupEvent pickup)
            {
                CheckMetadata(pickup);
                local++;
                ordering &= global == local - 1;
                if (phase == 0)
                    ordering &= recipient.ResearchAntlerBuffs == local && mode.CountAntlerShards(owner.netId) == 3 - local;
                int buffs = recipient.ResearchAntlerBuffs;
                mode.UpdateRainAntlers();
                reentrant &= recipient.ResearchAntlerBuffs == buffs && global == local - 1;
                if (phase == 1)
                {
                    receiver.Clear();
                    mode.ClearAntlerShards(owner.netId);
                    mutation &= recipient.ResearchAntlerBuffs == 0;
                }
                if (phase == 2) NetworkServer.Destroy(recipient.gameObject);
            }

            void OnGlobal(RangePickupEvent pickup)
            {
                CheckMetadata(pickup);
                global++;
                ordering &= local == global && mode.CountAntlerShards(owner.netId) == (phase == 0 ? 2 - global : 0);
            }
        }
    }
}
