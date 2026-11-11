using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;

namespace MTPSKIT.UI.HUD
{
    internal sealed class RainScrapperMenu : MonoBehaviour
    {
        internal static RainScrapperMenu Current { get; private set; }
        PlayerInstance player;
        PlayerGameplayInput heldInput;
        int node;
        uint body;
        bool closing;

        internal static void Open(PlayerInstance player, int node, uint body, int[] types, int[] counts)
        {
            if (!player || !player.MyCharacter || player.MyCharacter.netId != body ||
                types == null || counts == null || types.Length != counts.Length ||
                types.Length > RangeResearchItems.WhiteScrappableTypeCount || !ClientFrontend.GamePlayInput()) return;
            if (Current) Current.Close();
            ReferenceBattleHud.CloseInspection();
            if (!PlayerGameplayInput.Instance || !PlayerGameplayInput.Instance.enabled) return;
            var canvas = FrontEndUI.Canvas(null, 30);
            canvas.name = "Rain scrapper selection";
            var menu = canvas.gameObject.AddComponent<RainScrapperMenu>();
            Current = menu;
            menu.player = player;
            menu.node = node;
            menu.body = body;
            menu.heldInput = PlayerGameplayInput.Instance;
            menu.heldInput.enabled = false;
            var character = player.MyCharacter;
            character.SetMovementInput(Vector2.zero);
            character.IsAiming = false;
            character.SetActionKeyCode(ActionCodes.Sprint, false);
            character.SetActionKeyCode(ActionCodes.BladeWalk, false);
            character.CharacterItemManager.UsePrimaryInput = character.CharacterItemManager.UseSecondaryInput = false;
            ClientFrontend.ShowCursor(true);
            menu.Draw(types, counts);
        }

        void Draw(int[] types, int[] counts)
        {
            FrontEndUI.Box(transform, "Scrapper shade", 0, 0, 1920, 1080, new Color(0, 0, 0, .7f));
            var panel = FrontEndUI.Box(transform, "Scrapper choices", 180, 230, 1560, 620, FrontEndUI.Panel);
            FrontEndUI.Text(panel.transform, "选择要粉碎的物品 · 每次最多 10 层", 30, 20, 1500, 48, 30);
            var catalog = new RainImported.LogbookCatalog().Table.items;
            int visible = 0;
            for (int i = 0; i < types.Length; i++)
            {
                if (counts[i] <= 0) continue;
                int choice = types[i];
                var entry = System.Array.Find(catalog, row => RangeResearchItems.CatalogIndex(row.catalogName) == choice);
                string title = (entry?.name ?? "物品") + " ×" + counts[i] + "\n转换 " + Mathf.Min(10, counts[i]) + " 份白色碎片";
                var button = FrontEndUI.Button(panel.transform, title, 30 + visible % 5 * 300, 90 + visible / 5 * 70,
                    285, 62, () => Select(choice));
                button.GetComponentInChildren<TMPro.TMP_Text>().fontSize = 21;
                visible++;
            }
            if (visible == 0) FrontEndUI.Text(panel.transform, "没有可粉碎的普通物品", 30, 100, 1500, 50, 26);
            FrontEndUI.Button(panel.transform, "取消 · Esc", 1230, 540, 285, 50, Close);
        }

        internal bool Select(int type)
        {
            if (closing || !player) return false;
            bool submitted = player.SubmitRainScrapperSelection(node, body, type);
            Close();
            return submitted;
        }

        void Update()
        {
            if (!player || !player.MyCharacter || player.MyCharacter.netId != body ||
                player.MyCharacter.CurrentHealth <= 0 || !Application.isFocused ||
                GameManager.Gamemode is not RangeGamemode mode || !mode.IsRainCoop ||
                mode.NearbyRainChest(player.MyCharacter) != node ||
                Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Equals)) Close();
        }

        internal void Close()
        {
            if (closing) return;
            closing = true;
            ReleaseInput();
            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        void ReleaseInput()
        {
            if (Current == this) Current = null;
            if (!heldInput) return;
            heldInput.enabled = true;
            heldInput = null;
            ClientFrontend.ShowCursor(false);
        }

        void OnDisable() => Close();
        void OnDestroy() => ReleaseInput();
    }
}
