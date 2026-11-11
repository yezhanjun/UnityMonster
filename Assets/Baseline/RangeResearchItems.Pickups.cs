using System;

namespace MTPSKIT.Gameplay
{
    internal enum RangePickupClass : byte { None, Minor, TempItem, Item }

    // CatalogId is null for non-item world pickups (original PickupIndex.none).
    internal readonly struct RangePickupEvent
    {
        internal readonly CharacterInstance Body;
        internal readonly uint BodyNetId;
        internal readonly RangePickupClass PickupClass;
        internal readonly string CatalogId;
        internal readonly int Quantity;

        internal RangePickupEvent(CharacterInstance body, RangePickupClass pickupClass,
            string catalogId = null, int quantity = 1)
        {
            Body = body;
            BodyNetId = body.netId;
            PickupClass = pickupClass;
            CatalogId = catalogId;
            Quantity = quantity;
        }
    }

    public sealed partial class RangeResearchItems
    {
        // Body-scoped event; listeners must unsubscribe when their own lifetime ends.
        // Direct inventory grants are not world pickups and never raise this event.
        internal event Action<RangePickupEvent> ServerPickup;

        internal void NotifyServerPickup(in RangePickupEvent pickup)
        {
            if (!owner || !owner.isServer || pickup.Body != owner) return;
            ApplyCollectorPickup(in pickup);
            ServerPickup?.Invoke(pickup);
        }
    }
}
