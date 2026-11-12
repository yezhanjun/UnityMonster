using Mirror;
using MTPSKIT.Gameplay.Gamemodes;

namespace MTPSKIT.Gameplay
{
    public partial class PlayerInstance
    {
        internal bool TryOpenRainScrapper()
        {
            if (!isOwned || GameManager.Gamemode is not RangeGamemode mode || !mode.IsRainCoop ||
                !MyCharacter || MyCharacter.CurrentHealth <= 0 || mode.NearbyRainBeacon(MyCharacter) >= 0) return false;
            int node = mode.NearbyRainChest(MyCharacter);
            if (node < 0 || RangeGamemode.RainChestRewardId(node) != "ScrapBisonSteak") return false;
            CmdOpenRainScrapper(node, MyCharacter.netId);
            return true;
        }

        bool ValidateRainScrapper(int node, uint body, out RangeResearchItems items)
        {
            items = null;
            return !BOT && MyCharacter && MyCharacter.netId == body && MyCharacter.CurrentHealth > 0 &&
                GameManager.Gamemode is RangeGamemode mode && mode.IsRainCoop &&
                node >= 0 && node == mode.NearbyRainChest(MyCharacter) &&
                RangeGamemode.RainChestRewardId(node) == "ScrapBisonSteak" &&
                MyCharacter.TryGetComponent(out items);
        }

        [Command]
        void CmdOpenRainScrapper(int node, uint body)
        {
            if (!ValidateRainScrapper(node, body, out var items)) return;
            int length = RangeResearchItems.WhiteScrappableTypeCount;
            var types = new int[length];
            var counts = new int[length];
            for (int i = 0; i < length; i++)
            {
                types[i] = RangeResearchItems.WhiteScrappableTypeAt(i);
                counts[i] = items.WhitePrinterPaymentCount(types[i]);
            }
            TargetOpenRainScrapper(connectionToClient, node, body, types, counts);
        }

        [TargetRpc]
        void TargetOpenRainScrapper(NetworkConnection connection, int node, uint body, int[] types, int[] counts)
        {
            if (isLocalPlayer && MyCharacter && MyCharacter.netId == body)
                UI.HUD.RainScrapperMenu.Open(this, node, body, types, counts);
        }

        internal bool SubmitRainScrapperSelection(int node, uint body, int type)
        {
            if (!isOwned || !MyCharacter || MyCharacter.netId != body || MyCharacter.CurrentHealth <= 0) return false;
            CmdScrapRainSelection(node, body, type);
            return true;
        }

        [Command]
        void CmdScrapRainSelection(int node, uint body, int type)
        {
            ServerScrapRainSelection(node, body, type);
        }

        [Server]
        internal int ServerScrapRainSelection(int node, uint body, int type)
        {
            if (!ValidateRainScrapper(node, body, out var items)) return 0;
            int fireworks = items.FireworkStacks;
            int converted = items.TryScrapWhiteItem(type);
            if (converted == 0) return 0;
            items.OnRainInteraction(fireworks);
            RecordRainChestItems(items);
            return converted;
        }
    }
}
