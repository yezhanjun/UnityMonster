using Mirror;
using MTPSKIT.Gameplay;
using MTPSKIT.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jotunnslayer
{
    internal enum RaidClass
    {
        Berserker, Seeress, Ranger, Rogue, Warden, Valkyrie
    }

    public sealed class SkillPlayground : MonoBehaviour
    {
        private SkillCatalog catalog;
        private SkillProgress progress;
        private SkillTreePanel tree;
        private SkillBattlePreview battle;
        private Font font;
        private Canvas menu;
        private GUIStyle trainingButton;
        private bool controlsOpen;
        private bool ownsCursor;
        private bool liveMatch;
        private bool englishRewards;
        private static string pendingMatchScene;
        private static RaidClass pendingRaidClass;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static SkillProgress ValidationMatchProgress { get; set; }
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterSceneEntry()
        {
            pendingMatchScene = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        internal static bool SupportsMatch(string scene) =>
            !string.IsNullOrEmpty(scene) && !MTPSKIT.RoomCreator.IsRainScene(scene) &&
            !MTPSKIT.RoomCreator.IsTrainingScene(scene);

        internal static void LaunchMatch(MTPSKIT.RoomCreator creator, int map, int character, RaidClass raidClass)
        {
            if (!creator || NetworkClient.active || NetworkServer.active || map < 0 || map >= creator.Maps.Length)
                return;

            string scene = creator.Maps[map].Scene;
            if (!SupportsMatch(scene))
                return;

            pendingMatchScene = scene;
            pendingRaidClass = raidClass;
            try
            {
                creator.LaunchFromFrontEnd(map, character, false);
            }
            finally
            {
                if (!NetworkServer.active)
                    pendingMatchScene = null;
            }
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            bool match = NetworkServer.active && pendingMatchScene == scene.path;
            if (match || mode == LoadSceneMode.Single)
                pendingMatchScene = null;
            if (!match && !MTPSKIT.RoomCreator.IsTrainingScene(scene.path))
                return;

            var root = new GameObject(match ? "Jotunnslayer match" : "Jotunnslayer training");
            SceneManager.MoveGameObjectToScene(root, scene);
            var playground = root.AddComponent<SkillPlayground>();
            playground.liveMatch = match;
            playground.Initialize();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (match && ValidationMatchProgress != null)
            {
                playground.progress = ValidationMatchProgress;
                ValidationMatchProgress = null;
            }
#endif
            playground.battle = root.AddComponent<SkillBattlePreview>();
            playground.battle.LiveMatch = match;
            playground.battle.UseExperienceCrystals = match;
            if (match)
            {
                playground.battle.SelectedRaidClass = pendingRaidClass;
                playground.englishRewards = PlayerPrefs.GetInt("FrontEnd.English", 0) != 0;
                playground.battle.ConfigureRewardDisplay(playground.catalog, playground.font, playground.englishRewards);
                root.AddComponent<SkillMatchSession>().Initialize(playground.catalog, playground.font);
            }
            playground.battle.ShowControls = false;
        }

        public static void OpenFromMenu(Canvas canvas)
        {
            var root = new GameObject("Jotunnslayer skill tree");
            root.transform.SetParent(canvas.transform.parent, false);
            var playground = root.AddComponent<SkillPlayground>();
            playground.Initialize();
            playground.menu = canvas;
            canvas.gameObject.SetActive(false);
            playground.OpenTree();
        }

        private void Initialize()
        {
            catalog = SkillCatalog.Load();
            progress = new SkillProgress(catalog, "jotunnslayer.preview.v1", 10, 15);
            font = Font.CreateDynamicFontFromOSFont("Microsoft YaHei", 18);
            tree = gameObject.AddComponent<SkillTreePanel>();
            tree.enabled = false;
        }

        private void OpenTree() => tree.Open(catalog, progress, font);

        private void Update()
        {
            if (menu)
            {
                if (!tree.enabled)
                    Destroy(gameObject);
                return;
            }

            if (!battle)
                return;

            var local = NetworkClient.localPlayer;
            var player = local ? local.GetComponent<PlayerInstance>() : null;
            var character = player ? player.MyCharacter : null;
            if (!character || !character.isServer)
            {
                battle.Bind(null, progress, tree);
                SetControls(false);
                return;
            }

            battle.Bind(character, progress, tree);
            if (!battle.HasLiveCaster)
            {
                SetControls(false);
                return;
            }

            if (Input.GetKeyDown(KeyCode.F6) && (controlsOpen || ClientFrontend.GamePlayInput()))
                SetControls(!controlsOpen);
            if (!liveMatch && !controlsOpen && ClientFrontend.GamePlayInput() && Input.GetKeyDown(KeyCode.F9))
                battle.Cast(Time.time);
            if (liveMatch && !controlsOpen && ClientFrontend.GamePlayInput() && Input.GetKeyDown(player && player.HasHeroLoadout ? KeyCode.H : KeyCode.V))
                battle.RequestRaidDash(character);
        }

        private void SetControls(bool open)
        {
            if (controlsOpen == open)
                return;

            controlsOpen = open;
            battle.ShowControls = open;
            ClientFrontend.ShowCursor(open);
            ownsCursor = open;
            if (!open)
                tree.enabled = false;
        }

        private void OnGUI()
        {
            if (!battle || tree.enabled || !battle.HasLiveCaster)
                return;

            trainingButton ??= new GUIStyle(GUI.skin.button) { font = font };
            string label = englishRewards
                ? (controlsOpen ? "Class / God Skill Tree" : "F6 Skill Rewards")
                : (controlsOpen ? "职业 / 神祇技能树" : liveMatch ? "F6 技能奖励" : "F6 技能面板  /  F9 施放");
            if (GUI.Button(new Rect(16, 16, 250, 30), label, trainingButton))
            {
                if (controlsOpen)
                    OpenTree();
                else if (ClientFrontend.GamePlayInput())
                    SetControls(true);
            }
        }

        private void OnDestroy()
        {
            if (menu)
                menu.gameObject.SetActive(true);
            if (ownsCursor)
                ClientFrontend.ShowCursor(false);
            if (font)
                Destroy(font);
        }
    }
}
