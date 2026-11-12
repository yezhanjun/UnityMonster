#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jotunnslayer
{
    [InitializeOnLoad]
    public sealed partial class PlaygroundValidation : MonoBehaviour
    {
        private const string Pending = "Jotunnslayer.PlaygroundValidation";
        private const string Scenario = "Jotunnslayer.PlaygroundValidation.Scenario";
        private enum ValidationScenario
        {
            Training, Lightning, Missile, Soulmate, Heartseeker, FocusFire,
            WardRestoring, WardRestoringRankOne, WardRestoringRankTwo, WardRestoringRankThree,
            DeeperCutsRankOne, DeeperCutsRankTwo, DeeperCutsRankThree, DeeperCutsRankFour,
            PainVesselRankOne, PainVesselRankTwo, PainVesselRankThree, PainVesselRankFour,
            WildHeart, BraveryRankOne, BraveryRankTwo, BraveryRankThree, BraveryRankFour, ChainReaction, MissileEvolution, HeartEvolution,
            GourmetOne, GourmetTwo, GourmetThree, GourmetFour, DeeperCutsRefund, PainVesselRefund, MilitiaFourRanks,
            LichborneFourRanks, DivineDamageSevenRanks, ShadowImagesFourRanks
        }
        private ValidationScenario scenario;

        static PlaygroundValidation() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void Run() => StartValidation(ValidationScenario.Training);

        public static void RunMatch() => StartValidation(ValidationScenario.Lightning);

        public static void RunMatchMissile() => StartValidation(ValidationScenario.Missile);

        public static void RunMatchSoulmate() => StartValidation(ValidationScenario.Soulmate);

        public static void RunMatchHeartseeker() => StartValidation(ValidationScenario.Heartseeker);

        public static void RunMatchFocusFire() => StartValidation(ValidationScenario.FocusFire);

        public static void RunMatchWardRestoring() => StartValidation(ValidationScenario.WardRestoring);
        public static void RunMatchWardRestoringRankOne() => StartValidation(ValidationScenario.WardRestoringRankOne);
        public static void RunMatchWardRestoringRankTwo() => StartValidation(ValidationScenario.WardRestoringRankTwo);
        public static void RunMatchWardRestoringRankThree() => StartValidation(ValidationScenario.WardRestoringRankThree);
        public static void RunMatchDeeperCutsRankOne() => StartValidation(ValidationScenario.DeeperCutsRankOne);
        public static void RunMatchDeeperCutsRankTwo() => StartValidation(ValidationScenario.DeeperCutsRankTwo);
        public static void RunMatchDeeperCutsRankThree() => StartValidation(ValidationScenario.DeeperCutsRankThree);
        public static void RunMatchDeeperCutsRankFour() => StartValidation(ValidationScenario.DeeperCutsRankFour);
        public static void RunMatchPainVesselRankOne() => StartValidation(ValidationScenario.PainVesselRankOne);
        public static void RunMatchPainVesselRankTwo() => StartValidation(ValidationScenario.PainVesselRankTwo);
        public static void RunMatchPainVesselRankThree() => StartValidation(ValidationScenario.PainVesselRankThree);
        public static void RunMatchPainVesselRankFour() => StartValidation(ValidationScenario.PainVesselRankFour);
        public static void RunMatchWildHeart() => StartValidation(ValidationScenario.WildHeart);
        public static void RunMatchBraveryRankOne() => StartValidation(ValidationScenario.BraveryRankOne);
        public static void RunMatchBraveryRankTwo() => StartValidation(ValidationScenario.BraveryRankTwo);
        public static void RunMatchBraveryRankThree() => StartValidation(ValidationScenario.BraveryRankThree);
        public static void RunMatchBraveryRankFour() => StartValidation(ValidationScenario.BraveryRankFour);
        public static void RunMatchChainReaction() => StartValidation(ValidationScenario.ChainReaction);
        public static void RunMatchMissileEvolution() => StartValidation(ValidationScenario.MissileEvolution);
        public static void RunMatchHeartEvolution() => StartValidation(ValidationScenario.HeartEvolution);
        public static void RunMatchGourmetOne() => StartValidation(ValidationScenario.GourmetOne);
        public static void RunMatchMilitiaFourRanks() => StartValidation(ValidationScenario.MilitiaFourRanks);
        public static void RunMatchLichborneFourRanks() => StartValidation(ValidationScenario.LichborneFourRanks);
        public static void RunMatchDivineDamageSevenRanks() => StartValidation(ValidationScenario.DivineDamageSevenRanks);
        public static void RunMatchShadowImagesFourRanks() => StartValidation(ValidationScenario.ShadowImagesFourRanks);
        public static void RunMatchGourmetTwo() => StartValidation(ValidationScenario.GourmetTwo);
        public static void RunMatchGourmetThree() => StartValidation(ValidationScenario.GourmetThree);
        public static void RunMatchGourmetFour() => StartValidation(ValidationScenario.GourmetFour);
        public static void RunMatchDeeperCutsRefund() => StartValidation(ValidationScenario.DeeperCutsRefund);
        public static void RunMatchPainVesselRefund() => StartValidation(ValidationScenario.PainVesselRefund);

        private static void StartValidation(ValidationScenario scenario)
        {
            var startup = EditorSceneManager.OpenScene("Assets/Game/Baseline/Scenes/Startup.unity");
            if (scenario != ValidationScenario.Training)
            {
                string services = string.Join(", ", startup.GetRootGameObjects().Select(root =>
                    $"{root.name}:{root.GetComponent<RoomCreator>() != null}" +
                    $"/missing={GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(root)}"));
                Debug.Log($"JOTUNNSLAYER_FORMAL_STARTUP_ROOTS {services}");
                const string roomCreatorScript = "Assets/AssetStore/desNetware/Multiplayer TPS KIT/Scripts/RoomCreator/RoomCreator.cs";
                var script = AssetDatabase.LoadAssetAtPath<MonoScript>(roomCreatorScript);
                Debug.Log($"JOTUNNSLAYER_FORMAL_ROOM_SCRIPT guid={AssetDatabase.AssetPathToGUID(roomCreatorScript)} " +
                          $"type={script?.GetClass()?.FullName ?? "<missing>"}");
            }
            SessionState.SetBool(Pending, true);
            SessionState.SetInt(Scenario, (int)scenario);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false))
                return;

            SessionState.EraseBool(Pending);
            EditorApplication.LockReloadAssemblies();
            var root = new GameObject("Jotunnslayer playground validation");
            DontDestroyOnLoad(root);
            var validation = root.AddComponent<PlaygroundValidation>();
            validation.scenario = (ValidationScenario)SessionState.GetInt(Scenario, 0);
            SessionState.EraseInt(Scenario);
        }

        private static Button FindButton(string name, string page = "home") =>
            GameObject.Find("Page " + page)?.GetComponentsInChildren<Button>()
                .FirstOrDefault(button => button.name == name);

        private IEnumerator Start()
        {
            // Allow NewFrontEnd to replace its serialized design preview with the live page.
            yield return new WaitForSecondsRealtime(1f);
            if (scenario != ValidationScenario.Training)
            {
                yield return ValidateMatch(scenario);
                yield break;
            }

            float deadline = Time.realtimeSinceStartup + 15f;
            Button skillButton;
            while (!(skillButton = FindButton("巨人杀手技能树") ?? FindButton("Jotunnslayer Skill Tree")) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!skillButton)
            {
                Fail("No skill tree button on the startup page.");
                yield break;
            }

            skillButton.onClick.Invoke();
            yield return null;
            var tree = FindFirstObjectByType<SkillTreePanel>();
            if (!tree || !tree.enabled || skillButton.gameObject.activeInHierarchy)
            {
                Fail("The startup button did not open the tree and hide the menu.");
                yield break;
            }

            tree.enabled = false;
            yield return new WaitForSecondsRealtime(.2f);
            var training = FindButton("训练场") ?? FindButton("Training Range");
            if (!training)
            {
                Fail("The startup menu did not return after closing the tree.");
                yield break;
            }

            training.onClick.Invoke();
            deadline = Time.realtimeSinceStartup + 35f;
            CharacterInstance owner = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                var local = NetworkClient.localPlayer;
                if (local && (owner = local.GetComponent<PlayerInstance>().MyCharacter))
                    break;
                yield return null;
            }

            yield return null;
            var battle = FindFirstObjectByType<SkillBattlePreview>();
            if (!owner || !owner.isServer || !battle)
            {
                Fail("Training did not create a local server character and skill runtime.");
                yield break;
            }

            var dummy = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + owner.transform.forward * 3f, Quaternion.identity)
                .GetComponent<CharacterInstance>();
            foreach (var slot in dummy.CharacterItemManager.Slots)
                slot.ItemOnSpawn = null;
            dummy.Team = owner.Team == 0 ? 1 : 0;
            dummy.MaxHealth = dummy.CurrentHealth = 1000;
            NetworkServer.Spawn(dummy.gameObject);
            dummy.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            dummy.transform.position = owner.transform.position + owner.transform.forward * 3f;
            int before = dummy.CurrentHealth;
            if (!battle.TryChooseLightning())
            {
                Fail("The training character could not select Thor's Lightning Strike.");
                yield break;
            }

            battle.Cast(Time.time);
            if (before - dummy.CurrentHealth != 17)
            {
                Fail($"Lightning Strike did {before - dummy.CurrentHealth} damage to the training target, expected 17.");
                yield break;
            }

            battle.SetRaidLevel(2);
            if (!battle.TryChooseDaggerDance())
            {
                Fail("The training character could not select Dagger Dance.");
                yield break;
            }

            before = dummy.CurrentHealth;
            battle.Cast(Time.time);
            if (before - dummy.CurrentHealth != 13)
            {
                Fail($"Dagger Dance did {before - dummy.CurrentHealth} damage to the training target, expected 13.");
                yield break;
            }

            battle.SetRaidLevel(3);
            if (!battle.TryChooseRegenerationRune())
            {
                Fail("The training character could not select Thurisaz Rune.");
                yield break;
            }

            int wounded = owner.MaxHealth - 20;
            owner.CurrentHealth = wounded;
            battle.Tick(Time.time);
            battle.Tick(Time.time + 1f);
            if (owner.CurrentHealth != wounded + 2)
            {
                Fail("Thurisaz Rune did not restore two health to the training character.");
                yield break;
            }

            DNNetworkManager.Instance.StopHost();
            deadline = Time.realtimeSinceStartup + 15f;
            while (!(FindButton("训练场") ?? FindButton("Training Range")) && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (!(FindButton("训练场") ?? FindButton("Training Range")) || FindFirstObjectByType<SkillPlayground>())
            {
                Fail("Training did not return to the startup menu and release the skill playground.");
                yield break;
            }

            Debug.Log("JOTUNNSLAYER_PLAYGROUND_OK startup menu, tree return, training host, Lightning Strike 17, Dagger Dance 13, Thurisaz healing 2, training teardown");
            EditorApplication.Exit(0);
        }

        private static IEnumerator ValidateMatch(ValidationScenario scenario)
        {
            bool combatRefund = scenario == ValidationScenario.DeeperCutsRefund || scenario == ValidationScenario.PainVesselRefund;
            bool militia = scenario == ValidationScenario.MilitiaFourRanks;
            bool lichborne = scenario == ValidationScenario.LichborneFourRanks;
            bool divineDamage = scenario == ValidationScenario.DivineDamageSevenRanks;
            bool shadowImages = scenario == ValidationScenario.ShadowImagesFourRanks;
            SkillProgress matchProfile = null;
            int gourmetRank = (int)scenario >= (int)ValidationScenario.GourmetOne && (int)scenario <= (int)ValidationScenario.GourmetFour
                ? (int)scenario - (int)ValidationScenario.GourmetOne + 1 : 0;
            bool missileEvolution = scenario == ValidationScenario.MissileEvolution;
            bool missile = scenario == ValidationScenario.Missile || missileEvolution;
            bool soulmate = scenario == ValidationScenario.Soulmate;
            bool chainReaction = scenario == ValidationScenario.ChainReaction;
            bool heartEvolution = scenario == ValidationScenario.HeartEvolution;
            bool heartseeker = scenario == ValidationScenario.Heartseeker || heartEvolution;
            bool ranger = heartseeker || scenario == ValidationScenario.FocusFire;
            (int wardRank, float wardInterval) = scenario switch
            {
                ValidationScenario.WardRestoringRankOne => (1, 33.25f),
                ValidationScenario.WardRestoringRankTwo => (2, 31.5f),
                ValidationScenario.WardRestoringRankThree => (3, 28f),
                ValidationScenario.WardRestoring => (4, 22.75f),
                _ => (0, 0f)
            };
            string trailName = missile ? "Raid Magic Missile trail" : ranger ? "Raid Heart Seeker trail" : null;
            (int damageRank, int expectedDamage, bool incomingHit, string damageVirtue) = scenario switch
            {
                ValidationScenario.DeeperCutsRankOne => (1, 105, false, "DEEPER_CUTS"),
                ValidationScenario.DeeperCutsRankTwo => (2, 110, false, "DEEPER_CUTS"),
                ValidationScenario.DeeperCutsRankThree => (3, 120, false, "DEEPER_CUTS"),
                ValidationScenario.DeeperCutsRankFour => (4, 135, false, "DEEPER_CUTS"),
                ValidationScenario.DeeperCutsRefund => (4, 135, false, "DEEPER_CUTS"),
                ValidationScenario.PainVesselRankOne => (1, 98, true, "PAIN_VESSEL"),
                ValidationScenario.PainVesselRankTwo => (2, 96, true, "PAIN_VESSEL"),
                ValidationScenario.PainVesselRankThree => (3, 92, true, "PAIN_VESSEL"),
                ValidationScenario.PainVesselRankFour => (4, 85, true, "PAIN_VESSEL"),
                ValidationScenario.PainVesselRefund => (4, 85, true, "PAIN_VESSEL"),
                ValidationScenario.WildHeart => (1, 110, false, "WILD_HEART"),
                ValidationScenario.BraveryRankOne => (1, 103, false, "BRAVERY"),
                ValidationScenario.BraveryRankTwo => (2, 106, false, "BRAVERY"),
                ValidationScenario.BraveryRankThree => (3, 111, false, "BRAVERY"),
                ValidationScenario.BraveryRankFour => (4, 120, false, "BRAVERY"),
                _ => (0, 0, false, null)
            };
            RaidClass expectedClass = lichborne || divineDamage ? RaidClass.Ranger : gourmetRank > 0 || militia ? RaidClass.Warden : damageVirtue switch
            {
                "DEEPER_CUTS" or "PAIN_VESSEL" => RaidClass.Berserker,
                "WILD_HEART" => RaidClass.Ranger,
                _ => chainReaction ? RaidClass.Rogue :
                    (soulmate || shadowImages) ? RaidClass.Valkyrie : ranger ? RaidClass.Ranger : RaidClass.Seeress
            };
            const int selection = RangeCharacterSelection.FirstPortable;
            const string arena = "map_arena";
            var singleplayer = FindButton("单人游戏") ?? FindButton("Singleplayer");
            if (!singleplayer)
            {
                Fail("Formal validation did not find the singleplayer menu.");
                yield break;
            }

            singleplayer.onClick.Invoke();
            yield return null;
            var character = FindButton(RangeCharacterSelection.DisplayName(selection), "prepare");
            var creator = FindFirstObjectByType<RoomCreator>();
            var maps = creator ? creator.Maps : null;
            var arenaMap = maps?.FirstOrDefault(map => map && string.Equals(
                System.IO.Path.GetFileNameWithoutExtension(map.Scene), arena, System.StringComparison.OrdinalIgnoreCase));
            if (!arenaMap)
            {
                string configured = maps == null ? "<missing>" :
                    string.Join(", ", maps.Select(map => map ? map.Scene : "<missing slot>"));
                string creators = string.Join(", ", FindObjectsByType<RoomCreator>(
                    FindObjectsInactive.Include, FindObjectsSortMode.None).Select(candidate =>
                        $"{candidate.name} (active={candidate.gameObject.activeInHierarchy}, maps={candidate.Maps?.Length ?? -1})"));
                Fail($"Formal validation could not find the arena in {SceneManager.GetActiveScene().path}. " +
                     $"Maps: {configured}. Room creators: {creators}");
                yield break;
            }
            if (!character || !character.interactable)
            {
                Fail($"Formal validation could not select {RangeCharacterSelection.DisplayName(selection)}. " +
                     $"Prepare page={GameObject.Find("Page prepare") != null}, button={character != null}, " +
                     $"resource={RangeCharacterSelection.IsAvailable(selection)}.");
                yield break;
            }

            character.onClick.Invoke();
            yield return null;
            for (int i = 0; i < creator.Maps.Length && !FindButton(arenaMap.Name, "prepare"); i++)
            {
                var mapButton = creator.Maps.Select(map => FindButton(map.Name, "prepare")).FirstOrDefault(button => button);
                if (!mapButton || !mapButton.interactable)
                    break;
                mapButton.onClick.Invoke();
                yield return null;
            }

            for (int i = 0; i < (int)expectedClass; i++)
            {
                var raidClass = GameObject.Find("Page prepare")?.GetComponentsInChildren<Button>().FirstOrDefault(button =>
                    button.name.StartsWith("巨人杀手职业 · ") || button.name.StartsWith("Jotunnslayer class · "));
                if (!raidClass)
                    break;
                raidClass.onClick.Invoke();
                yield return null;
            }
            var deploy = FindButton("部署 · 巨人杀手", "prepare") ?? FindButton("Deploy · Jotunnslayer", "prepare");
            if (!deploy || !FindButton(arenaMap.Name, "prepare"))
            {
                Fail("Formal validation could not select the arena and deploy Jotunnslayer.");
                yield break;
            }

            if (missile || ranger || wardRank > 0 || damageRank > 0 || chainReaction || gourmetRank > 0 || militia || lichborne || divineDamage || shadowImages)
            {
                var progress = matchProfile = new SkillProgress(SkillCatalog.Load(), null, 10);
                for (int rank = 0; rank < gourmetRank; rank++)
                {
                    if (progress.TryLearn($"virtue_warden_01_{(char)('a' + rank)}")) continue;
                    Fail("Could not unlock Gourmet in the validation profile.");
                    yield break;
                }
                if (chainReaction && !progress.TryLearn("virtue_rogue_04_a"))
                {
                    Fail("Could not unlock Chain Reaction in the validation profile.");
                    yield break;
                }
                if ((missile || heartseeker) &&
                    !progress.TryLearn(missile ? SkillBattlePreview.MissileUnlock : SkillBattlePreview.HeartUnlock))
                {
                    Fail($"Could not unlock {scenario} in the in-memory validation profile.");
                    yield break;
                }
                if ((missileEvolution || heartEvolution) &&
                    !progress.TryLearn(heartEvolution ? SkillBattlePreview.HeartEvolution : SkillBattlePreview.MissileEvolution))
                {
                    Fail($"Could not unlock {scenario} in the validation profile.");
                    yield break;
                }
                string virtuePrefix = damageVirtue switch
                {
                    "DEEPER_CUTS" => "virtue_berserker_02_",
                    "PAIN_VESSEL" => "virtue_berserker_01_",
                    "WILD_HEART" => "virtue_ranger_04_",
                    "BRAVERY" => "virtue_odin_01_",
                    _ => "virtue_seer_02_"
                };
                for (int rank = 0; rank < wardRank + damageRank; rank++)
                {
                    string id = $"{virtuePrefix}{(char)('a' + rank)}";
                    if (progress.TryLearn(id))
                        continue;

                    Fail($"Could not unlock {id} in the validation profile.");
                    yield break;
                }
                SkillPlayground.ValidationMatchProgress = progress;
            }
            var manager = DNNetworkManager.Instance;
            if (!manager)
            {
                Fail("Formal validation could not find the network manager.");
                yield break;
            }
            manager.SetTransportPort(27963);
            try
            {
                deploy.onClick.Invoke();
                if (gourmetRank > 0 || shadowImages) RoomSetup.Properties.P_FillEmptySlotsWithBots = false;
            }
            catch (System.Exception error)
            {
                Fail($"Formal deployment failed on validation port 27963: {error}");
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 45f;
            SkillBattlePreview battle = null;
            CharacterInstance owner = null;
            bool requestedTeam = false;
            int? teamResponse = null;
            PlayerInstance requestedPlayer = null;
            void OnTeamResponse(int team, int permission) => teamResponse = permission;
            while (Time.realtimeSinceStartup < deadline)
            {
                var local = NetworkClient.localPlayer;
                if (local && local.TryGetComponent<PlayerInstance>(out var player))
                {
                    if (!requestedTeam && player.Team == -1 && SceneManager.GetActiveScene().name == arena &&
                        GameManager.Gamemode is TeamDeathmatch)
                    {
                        requestedTeam = true;
                        requestedPlayer = player;
                        player.PlayerEvent_OnReceivedTeamResponse += OnTeamResponse;
                        int blue = GameManager.Players.Values.Count(candidate => candidate.Team == 0);
                        int orange = GameManager.Players.Values.Count(candidate => candidate.Team == 1);
                        player.ClientRequestJoiningTeam(blue <= orange ? 0 : 1);
                    }
                    owner = player.MyCharacter;
                }
                battle = FindFirstObjectByType<SkillBattlePreview>();
                if (owner && battle && battle.HasLiveCaster)
                    break;
                yield return null;
            }
            if (requestedPlayer)
                requestedPlayer.PlayerEvent_OnReceivedTeamResponse -= OnTeamResponse;

            if (!owner || !battle || !battle.LiveMatch || !battle.HasLiveCaster ||
                SceneManager.GetActiveScene().name != arena)
            {
                Fail($"Formal arena binding failed. Scene={SceneManager.GetActiveScene().path}, " +
                     $"host={NetworkServer.active}, client={NetworkClient.active}, owner={owner != null}, " +
                     $"runtime={battle != null}, live match={battle && battle.LiveMatch}, caster={battle && battle.HasLiveCaster}, " +
                     $"team requested={requestedTeam}, response={teamResponse}.");
                yield break;
            }
            if (battle.SelectedRaidClass != expectedClass ||
                (expectedClass == RaidClass.Berserker ? battle.TryChooseMind() : battle.TryChooseAssault()))
            {
                Fail($"Formal arena class or reward gate was incorrect. Class={battle.SelectedRaidClass}.");
                yield break;
            }
            if (!owner.TryGetComponent<RangeRobotVisual>(out var visual) || visual.Selection != selection)
            {
                Fail($"Formal arena character selection was incorrect. " +
                     $"Expected={selection}, selected={RangeCharacterSelection.Selected}, visual={visual?.Selection}.");
                yield break;
            }

            // The original forward fixture sent elevated missiles into arena geometry.
            if (shadowImages)
            {
                yield return ValidateMatchShadowImages(owner, battle, matchProfile);
                if (!shadowImagesVerified) yield break;
            }
            if (divineDamage)
            {
                yield return ValidateMatchDivineDamage(owner, battle, matchProfile);
                if (!divineDamageVerified) yield break;
            }
            if (militia || lichborne)
            {
                yield return ValidateMatchClassDamage(owner, battle, matchProfile, militia);
                if (!classDamageVerified) yield break;
            }
            Vector3 targetOffset = owner.transform.forward * (trailName != null ? -3f : 3f);
            var dummy = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + targetOffset, Quaternion.identity)
                .GetComponent<CharacterInstance>();
            foreach (var slot in dummy.CharacterItemManager.Slots)
                slot.ItemOnSpawn = null;
            dummy.Team = owner.Team == 0 ? 1 : 0;
            dummy.CurrentHealth = dummy.MaxHealth = scenario switch
            {
                ValidationScenario.Soulmate => 20,
                ValidationScenario.Missile => 8,
                ValidationScenario.MissileEvolution => 8,
                ValidationScenario.Heartseeker => 15,
                ValidationScenario.HeartEvolution => 15,
                ValidationScenario.FocusFire => 8,
                ValidationScenario.ChainReaction => 1000,
                _ => 17
            };
            NetworkServer.Spawn(dummy.gameObject);
            dummy.GetComponent<CharacterMotor>().enabled = false;
            if (chainReaction)
            {
                // Health.Start registers the spawned target for skill selection.
                yield return null;
                dummy.transform.position = owner.transform.position + targetOffset;
                const int killHealth = 17;
                dummy.MaxHealth = dummy.CurrentHealth = 1000;
                owner.CurrentHealth = owner.MaxHealth - 80;
                int healthBefore = owner.CurrentHealth;
                var originalRandom = Random.state;
                Random.State repeatState;
                float roll;
                do
                {
                    repeatState = Random.state;
                    Random.Range(0, 1);
                    Random.Range(4f, 16f);
                    roll = Random.value;
                }
                while (roll >= 0.3f);
                Random.state = repeatState;
                battle.Tick(Time.time + 20f);
                Random.state = originalRandom;
                if (dummy.CurrentHealth != 948 || owner.CurrentHealth != healthBefore + 40)
                {
                    Fail($"Formal Chain Reaction damage={1000 - dummy.CurrentHealth}, healing={owner.CurrentHealth - healthBefore}, " +
                         $"class={battle.SelectedRaidClass}, live={battle.HasLiveCaster}, target={dummy.transform.position}, " +
                         $"owner={owner.transform.position}, invulnerable={dummy.RangeInvulnerable}, server={dummy.isServer}.");
                    yield break;
                }
                dummy.MaxHealth = dummy.CurrentHealth = killHealth;
            }
            if (damageRank > 0)
            {
                var victim = incomingHit ? owner : dummy;
                var attacker = incomingHit ? dummy : owner;
                int originalMaxHealth = victim.MaxHealth;
                int originalHealth = victim.CurrentHealth;
                victim.MaxHealth = victim.CurrentHealth = 1000;
                victim.Server_ChangeHealthState(100, CharacterPart.body, AttackType.melee, attacker, 0);
                int damage = victim.MaxHealth - victim.CurrentHealth;
                if (damage != expectedDamage)
                {
                    Fail($"Formal arena {scenario} dealt {damage} damage, expected {expectedDamage}.");
                    yield break;
                }
                if (combatRefund)
                {
                    int levelBefore = battle.RaidLevel, rewardsBefore = battle.PendingRewards;
                    matchProfile.Reset();
                    victim.CurrentHealth = 1000;
                    victim.Server_ChangeHealthState(100, CharacterPart.body, AttackType.melee, attacker, 0);
                    int refundedDamage = 1000 - victim.CurrentHealth;
                    string prefix = incomingHit ? "virtue_berserker_01_" : "virtue_berserker_02_";
                    for (int rank = 0; rank < 4; rank++)
                    {
                        if (matchProfile.TryLearn($"{prefix}{(char)('a' + rank)}")) continue;
                        Fail($"Formal {scenario} could not relearn permanent rank {rank + 1}.");
                        yield break;
                    }
                    victim.CurrentHealth = 1000;
                    victim.Server_ChangeHealthState(100, CharacterPart.body, AttackType.melee, attacker, 0);
                    int relearnedDamage = 1000 - victim.CurrentHealth;
                    if (refundedDamage != 100 || relearnedDamage != expectedDamage ||
                        battle.RaidLevel != levelBefore || battle.PendingRewards != rewardsBefore)
                    {
                        Fail($"Formal {scenario} refund={refundedDamage}, relearn={relearnedDamage}, expected={expectedDamage}, raid state preserved={battle.RaidLevel == levelBefore && battle.PendingRewards == rewardsBefore}.");
                        yield break;
                    }
                    Debug.Log($"JOTUNNSLAYER_FORMAL_COMBAT_REFUND_HIT_OK scenario={scenario} learned={damage} refund={refundedDamage} relearn={relearnedDamage} raidPreserved=True sameCharacter=True");
                }
                victim.MaxHealth = originalMaxHealth;
                victim.CurrentHealth = originalHealth;
            }
            if (wardRank > 0 && !ValidateMatchWardRestoring(owner, dummy, wardInterval))
            {
                Fail($"Formal arena Ward Restoring rank {wardRank} failed damage protection or the {wardInterval}s recharge boundary.");
                yield break;
            }
            if (gourmetRank > 0)
            {
                yield return ValidateMatchGourmet(owner, gourmetRank);
                if (!gourmetVerified) yield break;
            }
            if (trailName != null)
            {
                battle.ValidationProjectileTrace = message => Debug.Log($"JOTUNNSLAYER_FORMAL_PROJECTILE_TRACE {scenario} {message}");
                string nearby = string.Join(", ", GameManager.HealthInstances.Values
                    .Where(candidate => candidate && candidate.Team != owner.Team &&
                        (candidate.transform.position - owner.transform.position).sqrMagnitude <= 100f)
                    .Select(candidate => $"{candidate.netId}:{candidate.name}@{candidate.transform.position}"));
                Debug.Log($"JOTUNNSLAYER_FORMAL_PROJECTILE_FIXTURE {scenario} owner={owner.netId}@{owner.transform.position} " +
                          $"target={dummy.netId}@{dummy.transform.position} nearby={nearby}");
            }
            bool selectedSkill = scenario switch
            {
                ValidationScenario.Soulmate => true,
                ValidationScenario.Missile => battle.TryChoose(true),
                ValidationScenario.MissileEvolution => battle.TryChoose(true),
                ValidationScenario.Heartseeker => battle.TryChooseHeartseeker(),
                ValidationScenario.HeartEvolution => battle.TryChooseHeartseeker(),
                ValidationScenario.FocusFire => battle.TryChooseFocus(),
                _ => battle.TryChooseLightning()
            };
            if (!selectedSkill)
            {
                Fail($"The formal arena runtime did not offer {scenario} at level one.");
                yield break;
            }

            deadline = Time.realtimeSinceStartup + 8f;
            bool trailSeen = false, trailPathSeen = false;
            bool orbSeen = false, windupSeen = false, tetherSeen = false;
            while (battle && Time.realtimeSinceStartup < deadline)
            {
                if (trailName != null && !trailPathSeen)
                {
                    foreach (var trail in owner.GetComponentsInChildren<TrailRenderer>())
                    {
                        if (trail.name != trailName)
                            continue;
                        trailSeen = true;
                        trailPathSeen |= trail.positionCount >= 2;
                    }
                }
                if (soulmate)
                {
                    var orb = owner.transform.Find("Raid Soulmate");
                    if (orb && orb.gameObject.activeInHierarchy)
                    {
                        orbSeen = true;
                        windupSeen |= orb.localScale.x > 0.29f;
                        tetherSeen |= orb.TryGetComponent<LineRenderer>(out var tether) && tether.enabled;
                    }
                }
                if (battle.RaidLevel > 1 && (trailName == null || trailPathSeen) && (!soulmate || tetherSeen))
                    break;
                yield return null;
            }

            if (!battle || battle.RaidLevel != 2 || (dummy && dummy.CurrentHealth > 0) ||
                (trailName != null && !trailPathSeen) || (soulmate && !(orbSeen && windupSeen && tetherSeen)))
            {
                Fail($"Formal arena attack failed. Scenario={scenario}, trail={trailSeen}, path={trailPathSeen}, " +
                     $"orb={orbSeen}, windup={windupSeen}, tether={tetherSeen}, " +
                     $"target health={(dummy ? dummy.CurrentHealth : 0)}, level={(battle ? battle.RaidLevel : 0)}.");
                yield break;
            }

            int offers = battle.RefreshLiveOffer();
            if (offers == 0)
            {
                Fail("The formal arena did not offer a reward after level two.");
                yield break;
            }
            string reward = battle.LiveRewardLabel(0);
            int pending = battle.PendingRewards;
            if (!battle.TryAcceptLiveReward(0) || battle.TryAcceptLiveReward(0) ||
                battle.PendingRewards != pending - 1)
            {
                Fail($"The formal arena reward was not accepted exactly once. Reward={reward}.");
                yield break;
            }
            Debug.Log($"JOTUNNSLAYER_FORMAL_REWARD_OK offered={offers}, accepted={reward}");

            if (missileEvolution || heartEvolution)
            {
                battle.SetRaidLevel(22);
                while (heartEvolution ? battle.TryChooseHeartseeker() : battle.TryChoose(true)) { }
                var evolvedTarget = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position - (heartEvolution ? Vector3.forward : owner.transform.forward) * 3f, Quaternion.identity)
                    .GetComponent<CharacterInstance>();
                foreach (var slot in evolvedTarget.CharacterItemManager.Slots)
                    slot.ItemOnSpawn = null;
                evolvedTarget.Team = owner.Team == 0 ? 1 : 0;
                evolvedTarget.MaxHealth = evolvedTarget.CurrentHealth = 1000;
                NetworkServer.Spawn(evolvedTarget.gameObject);
                evolvedTarget.GetComponent<CharacterMotor>().enabled = false;
                if (heartEvolution)
                {
                    for (int i = 1; i <= 3; i++)
                    {
                        var bounceTarget = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                            evolvedTarget.transform.position - Vector3.forward * (i * 1.5f), Quaternion.identity)
                            .GetComponent<CharacterInstance>();
                        foreach (var slot in bounceTarget.CharacterItemManager.Slots)
                            slot.ItemOnSpawn = null;
                        bounceTarget.Team = evolvedTarget.Team;
                        bounceTarget.MaxHealth = bounceTarget.CurrentHealth = 1000;
                        NetworkServer.Spawn(bounceTarget.gameObject);
                        bounceTarget.GetComponent<CharacterMotor>().enabled = false;
                    }
                }
                var arrowHits = new System.Collections.Generic.Dictionary<uint, System.Collections.Generic.HashSet<uint>>();
                bool duplicateHit = false, excessiveHits = false, wrongHitDamage = false;
                int maximumHits = 0;
                int bleedTicks = 0, completedBleeds = 0;
                bool wrongBleedDamage = false;
                float firstArrowAt = float.PositiveInfinity;
                battle.ValidationBleedTick = (remaining, damage) =>
                {
                    if (!heartEvolution)
                        return;
                    bleedTicks++;
                    if (remaining == 1)
                        completedBleeds++;
                    wrongBleedDamage |= damage != 2;
                };
                battle.ValidationRangerHit = (arrow, target, damage) =>
                {
                    if (!heartEvolution)
                        return;
                    if (!arrowHits.TryGetValue(arrow, out var hits))
                    {
                        hits = new System.Collections.Generic.HashSet<uint>();
                        arrowHits.Add(arrow, hits);
                    }
                    duplicateHit |= !hits.Add(target);
                    maximumHits = Mathf.Max(maximumHits, hits.Count);
                    excessiveHits |= hits.Count > 3;
                    wrongHitDamage |= damage != 20;
                };
                int queuedShards = 0;
                bool evolvedParent = false;
                int radialArrows = 0;
                battle.ValidationProjectileTrace = message =>
                {
                    if (heartEvolution && message.StartsWith("arrow launch ") && message.Contains("damage=20 limit=3"))
                    {
                        radialArrows++;
                        firstArrowAt = Mathf.Min(firstArrowAt, Time.realtimeSinceStartup);
                    }
                    if (!message.StartsWith("queued "))
                        return;
                    evolvedParent |= message.Contains("damage=30 shatters=True");
                    if (message.Contains("damage=12 shatters=False"))
                        queuedShards++;
                };
                deadline = Time.realtimeSinceStartup + 10f;
                bool evolvedTrail = false;
                while (battle && evolvedTarget && Time.realtimeSinceStartup < deadline)
                {
                    foreach (var trail in owner.GetComponentsInChildren<TrailRenderer>())
                        evolvedTrail |= trail.name == trailName && trail.positionCount >= 2;
                    bool evolvedCast = heartEvolution ? radialArrows >= 6 : evolvedParent && queuedShards >= 17;
                    bool bounceVerified = !heartEvolution || maximumHits == 3 && completedBleeds > 0 &&
                        Time.realtimeSinceStartup >= firstArrowAt + 4.1f;
                    if (evolvedCast && evolvedTarget.CurrentHealth <= (heartEvolution ? 980 : 958) && evolvedTrail && bounceVerified)
                        break;
                    yield return null;
                }
                battle.ValidationProjectileTrace = null;
                battle.ValidationRangerHit = null;
                battle.ValidationBleedTick = null;
                bool castVerified = heartEvolution ? radialArrows >= 6 : evolvedParent && queuedShards >= 17;
                if (heartEvolution && (maximumHits != 3 || duplicateHit || excessiveHits || wrongHitDamage))
                {
                    Fail($"Formal Heart evolution hit cap failed. max={maximumHits}, duplicate={duplicateHit}, excessive={excessiveHits}, damage={wrongHitDamage}.");
                    yield break;
                }
                if (!evolvedTarget || !castVerified || evolvedTarget.CurrentHealth > (heartEvolution ? 980 : 958) || !evolvedTrail)
                {
                    Fail($"Formal {scenario} failed. parent={evolvedParent}, shards={queuedShards}, arrows={radialArrows}, health={evolvedTarget?.CurrentHealth}, trail={evolvedTrail}.");
                    yield break;
                }
                Debug.Log($"JOTUNNSLAYER_FORMAL_EVOLUTION_HIT_OK scenario={scenario} parent={evolvedParent} shards={queuedShards} arrows={radialArrows} damage={1000 - evolvedTarget.CurrentHealth} trail={evolvedTrail}");
                if (heartEvolution)
                    Debug.Log($"JOTUNNSLAYER_FORMAL_HEART_BOUNCE_OK max={maximumHits} duplicate={duplicateHit} excessive={excessiveHits} wrongDamage={wrongHitDamage}");
                if (heartEvolution)
                {
                    if (completedBleeds == 0 || bleedTicks < 4 || wrongBleedDamage)
                    {
                        Fail($"Formal Heart bleed failed. ticks={bleedTicks}, completed={completedBleeds}, wrongDamage={wrongBleedDamage}.");
                        yield break;
                    }
                    Debug.Log($"JOTUNNSLAYER_FORMAL_HEART_BLEED_OK ticks={bleedTicks} completed={completedBleeds} damagePerTick=2");
                }
            }

            DNNetworkManager.Instance.StopHost();
            deadline = Time.realtimeSinceStartup + 20f;
            while (!(FindButton("单人游戏") ?? FindButton("Singleplayer")) && Time.realtimeSinceStartup < deadline)
                yield return null;

            if (NetworkServer.active || NetworkClient.active || FindFirstObjectByType<SkillPlayground>() ||
                !(FindButton("单人游戏") ?? FindButton("Singleplayer")))
            {
                Fail("The formal match did not return to the main menu and release its skill runtime.");
                yield break;
            }

            if (shadowImages)
                Debug.Log("JOTUNNSLAYER_FORMAL_SHADOW_IMAGES_OK four ranks on actual Valkyrie body, server damage, lifetime and cleanup; fixture clock/profile, original visuals/network pending; kill XP, reward and menu return");
            else if (militia)
                Debug.Log("JOTUNNSLAYER_FORMAL_MILITIA_OK four independent profiles, impact/delay, refund/relearn, god isolation, kill XP, reward and menu return");
            else if (lichborne)
                Debug.Log("JOTUNNSLAYER_FORMAL_LICHBORNE_OK four independent profiles, normal-frame cast, refund/relearn, god isolation, kill XP, reward and menu return");
            else if (divineDamage)
                Debug.Log("JOTUNNSLAYER_FORMAL_DIVINE_DAMAGE_OK Nidhogg three and Thor four profiles, weapon/class/god scopes, refund/relearn, fall isolation, kill XP, reward and menu return");
            else if (gourmetRank > 0)
                Debug.Log($"JOTUNNSLAYER_FORMAL_GOURMET_OK rank={gourmetRank}, real health pickup trigger, ordinary heal isolation, no duplicate pickup, kill XP, reward and menu return");
            else if (combatRefund)
                Debug.Log($"JOTUNNSLAYER_FORMAL_COMBAT_REFUND_OK scenario={scenario}, learned/refunded/relearned server damage, same character, raid preservation, kill XP, reward and menu return");
            else if (damageRank > 0)
            {
                Debug.Log($"JOTUNNSLAYER_FORMAL_{damageVirtue}_OK selected {expectedClass}, rank {damageRank} weapon damage {expectedDamage}, kill XP and menu return");
            }
            else if (chainReaction)
                Debug.Log("JOTUNNSLAYER_FORMAL_CHAIN_REACTION_OK selected Rogue, double explosion 52, healing 40, kill XP and menu return");
            else
                Debug.Log(wardRank > 0
                ? $"JOTUNNSLAYER_FORMAL_WARD_RESTORING_OK selected Seeress, server damage protection, rank {wardRank} recharge {wardInterval}s and menu return"
                : scenario switch
                {
                    ValidationScenario.Missile => "JOTUNNSLAYER_FORMAL_MISSILE_OK selected character, automatic Magic Missile, host trail, kill XP and menu return",
                    ValidationScenario.MissileEvolution => "JOTUNNSLAYER_FORMAL_MISSILE_EVOLUTION_OK base kill XP, evolved projectile, seventeen shards, host trail and menu return",
                    ValidationScenario.HeartEvolution => "JOTUNNSLAYER_FORMAL_HEART_EVOLUTION_OK base kill XP, six radial arrows, evolved damage, host trail and menu return",
                    ValidationScenario.Soulmate => "JOTUNNSLAYER_FORMAL_SOULMATE_OK selected character, orb, windup, tether, kill XP and menu return",
                    ValidationScenario.Heartseeker => "JOTUNNSLAYER_FORMAL_HEART_OK selected character, automatic Heart Seeker, host trail, kill XP and menu return",
                    ValidationScenario.FocusFire => "JOTUNNSLAYER_FORMAL_FOCUS_OK selected character, automatic Focus Fire, host trail, kill XP and menu return",
                    _ => "JOTUNNSLAYER_FORMAL_MATCH_OK selected character, arena, automatic Lightning Strike, kill XP and menu return"
                });
            EditorApplication.Exit(0);
        }

        private static bool gourmetVerified;

        private static bool divineDamageVerified;

        private static IEnumerator ValidateMatchDivineDamage(CharacterInstance owner, SkillBattlePreview battle, SkillProgress restore)
        {
            divineDamageVerified = false;
            var playground = FindFirstObjectByType<SkillPlayground>();
            bool playgroundEnabled = playground && playground.enabled;
            if (playground) playground.enabled = false;
            int originalLevel = battle.RaidLevel, originalChoices = battle.PendingRewards;
            battle.enabled = false;
            battle.Bind(null, restore, null);
            for (int kind = 0; kind < 2; kind++)
            {
                bool thor = kind == 1;
                string prefix = thor ? "virtue_thor_02_" : "virtue_nidhogg_02_";
                int[] gods = thor ? new[] { 18, 18, 19, 21 } : new[] { 18, 19, 21 };
                int[] classes = thor ? new[] { 21, 21, 23, 25 } : new[] { 20, 20, 20 };
                int[] weapons = thor ? new[] { 207, 214, 228, 250 } : new[] { 200, 200, 200 };
                for (int rank = 0; rank < gods.Length; rank++)
                {
                    var profile = new SkillProgress(SkillCatalog.Load(), null, 20);
                    for (int learned = 0; learned <= rank; learned++)
                        if (!profile.TryLearn(prefix + (char)('a' + learned)))
                        { Fail("Formal divine damage profile unavailable"); yield break; }
                    var runtime = new GameObject("Formal divine damage rank " + rank).AddComponent<SkillBattlePreview>();
                    runtime.LiveMatch = true;
                    runtime.SelectedRaidClass = RaidClass.Ranger;
                    runtime.Bind(owner, profile, null);
                    var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        owner.transform.position + Vector3.forward * 2, Quaternion.identity).GetComponent<CharacterInstance>();
                    var god = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        owner.transform.position + Vector3.right * 3, Quaternion.identity).GetComponent<CharacterInstance>();
                    foreach (var body in new[] { target, god })
                    {
                        foreach (var slot in body.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                        body.Team = owner.Team == 0 ? 1 : 0;
                        body.MaxHealth = body.CurrentHealth = 10000;
                        NetworkServer.Spawn(body.gameObject);
                        body.GetComponent<CharacterMotor>().enabled = false;
                    }
                    yield return null;
                    runtime.SetRaidLevel(2);
                    if (!runtime.TryChooseSkull() || !runtime.TryChooseLightning())
                    { Fail("Formal divine damage skills unavailable"); yield break; }
                    int level = runtime.RaidLevel, choices = runtime.PendingRewards;
                    for (int phase = 0; phase < 3; phase++)
                    {
                        if (phase == 1) profile.Reset();
                        if (phase == 2)
                            for (int learned = 0; learned <= rank; learned++)
                                if (!profile.TryLearn(prefix + (char)('a' + learned)))
                                { Fail("Formal divine damage relearn failed"); yield break; }
                        target.CurrentHealth = target.MaxHealth;
                        god.CurrentHealth = god.MaxHealth;
                        target.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, owner, 0);
                        int weaponHit = target.MaxHealth - target.CurrentHealth;
                        target.CurrentHealth = target.MaxHealth;
                        runtime.Cast(Time.time + 10 + phase * 10);
                        int classHit = target.MaxHealth - target.CurrentHealth;
                        int godHit = god.MaxHealth - god.CurrentHealth;
                        int health = owner.CurrentHealth;
                        owner.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.falldamage, owner, 0);
                        int fallHit = health - owner.CurrentHealth;
                        owner.CurrentHealth = health;
                        if (weaponHit != (phase == 1 ? 200 : weapons[rank]) ||
                            classHit != (phase == 1 ? 20 : classes[rank]) || godHit != (phase == 1 ? 17 : gods[rank]) ||
                            fallHit != 10 || runtime.RaidLevel != level || runtime.PendingRewards != choices)
                        { Fail($"Formal divine damage node={prefix}{(char)('a' + rank)} phase={phase} weapon={weaponHit} class={classHit} god={godHit} fall={fallHit}"); yield break; }
                        Debug.Log($"JOTUNNSLAYER_FORMAL_DIVINE_DAMAGE_HIT_OK node={prefix}{(char)('a' + rank)} phase={phase} weapon={weaponHit} class={classHit} god={godHit} fall={fallHit} sameCharacter=True raidPreserved=True");
                    }
                    runtime.Bind(null, profile, null);
                    Destroy(runtime.gameObject);
                    target.Team = god.Team = owner.Team;
                    NetworkServer.Destroy(target.gameObject);
                    NetworkServer.Destroy(god.gameObject);
                    yield return null;
                }
            }
            battle.Bind(owner, restore, null);
            battle.enabled = true;
            if (playground) playground.enabled = playgroundEnabled;
            if (battle.RaidLevel != originalLevel || battle.PendingRewards != originalChoices)
            { Fail("Formal divine damage changed original raid progression"); yield break; }
            divineDamageVerified = true;
        }

        private static bool classDamageVerified;

        private static IEnumerator ValidateMatchClassDamage(CharacterInstance owner, SkillBattlePreview battle, SkillProgress restore, bool militia)
        {
            classDamageVerified = false;
            string label = militia ? "MILITIA" : "LICHBORNE";
            string nodePrefix = militia ? "virtue_warden_02_" : "virtue_ranger_02_";
            RaidClass raidClass = militia ? RaidClass.Warden : RaidClass.Ranger;
            var playground = FindFirstObjectByType<SkillPlayground>();
            bool playgroundEnabled = playground && playground.enabled;
            if (playground) playground.enabled = false;
            int originalLevel = battle.RaidLevel, originalChoices = battle.PendingRewards;
            battle.enabled = false;
            battle.Bind(null, restore, null);
            int[] impacts = militia ? new[] { 25, 26, 29, 32 } : new[] { 21, 22, 24, 27 };
            int[] delayed = { 11, 11, 12, 14 };
            for (int rank = 1; rank <= 4; rank++)
            {
                var profile = new SkillProgress(SkillCatalog.Load(), null, 20);
                for (int learned = 0; learned < rank; learned++)
                    if (!profile.TryLearn($"{nodePrefix}{(char)('a' + learned)}"))
                    { Fail($"Formal {label} profile unavailable"); yield break; }
                // Bind preserves an existing live raid, so each rank needs a fresh runtime.
                var runtime = new GameObject("Formal " + label + " rank " + rank).AddComponent<SkillBattlePreview>();
                runtime.enabled = false; // Explicit validation clock; no automatic frame casts.
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = raidClass;
                runtime.Bind(owner, profile, null);
                runtime.SetRaidLevel(2);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position + Vector3.forward * 2f, Quaternion.identity).GetComponent<CharacterInstance>();
                var god = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position + Vector3.right * 3f, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var body in new[] { target, god })
                {
                    foreach (var slot in body.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    body.Team = owner.Team == 0 ? 1 : 0;
                    body.MaxHealth = body.CurrentHealth = 10000;
                    NetworkServer.Spawn(body.gameObject);
                    body.GetComponent<CharacterMotor>().enabled = false;
                }
                yield return null;
                if (!(militia ? runtime.TryChooseAnvil() : runtime.TryChooseSkull()) || !runtime.TryChooseLightning())
                { Fail($"Formal {label} skills unavailable"); yield break; }
                int level = runtime.RaidLevel, choices = runtime.PendingRewards;
                for (int phase = 0; phase < 3; phase++)
                {
                    if (phase == 1) profile.Reset();
                    else if (phase == 2)
                        for (int learned = 0; learned < rank; learned++)
                            if (!profile.TryLearn($"{nodePrefix}{(char)('a' + learned)}"))
                            { Fail($"Formal {label} relearn unavailable"); yield break; }
                    target.CurrentHealth = target.MaxHealth;
                    god.CurrentHealth = god.MaxHealth;
                    float now = Time.time + 10 + phase * 10;
                    runtime.Cast(now);
                    int impact = target.MaxHealth - target.CurrentHealth;
                    int godHit = god.MaxHealth - god.CurrentHealth;
                    target.CurrentHealth = target.MaxHealth;
                    runtime.Tick(now + 1);
                    int followUp = target.MaxHealth - target.CurrentHealth;
                    int expectedImpact = phase == 1 ? (militia ? 24 : 20) : impacts[rank - 1];
                    int expectedFollowUp = militia ? (phase == 1 ? 10 : delayed[rank - 1]) : 0;
                    if (impact != expectedImpact || followUp != expectedFollowUp || godHit != 17 ||
                        runtime.RaidLevel != level || runtime.PendingRewards != choices)
                    { Fail($"Formal {label} rank={rank} phase={phase} impact={impact}/{expectedImpact} delayed={followUp}/{expectedFollowUp} god={godHit}/17"); yield break; }
                    Debug.Log($"JOTUNNSLAYER_FORMAL_{label}_HIT_OK rank={rank} phase={phase} impact={impact} delayed={followUp} god={godHit} sameCharacter=True raidPreserved=True");
                }
                runtime.Bind(null, profile, null);
                Destroy(runtime.gameObject);
                if (!militia)
                {
                    // Use an independent cooldown clock to prove the production Update path too.
                    runtime = new GameObject("Formal Lichborne normal-frame rank " + rank).AddComponent<SkillBattlePreview>();
                    runtime.LiveMatch = true;
                    runtime.SelectedRaidClass = raidClass;
                    runtime.Bind(owner, profile, null);
                    runtime.SetRaidLevel(2);
                    target.CurrentHealth = target.MaxHealth;
                    god.CurrentHealth = god.MaxHealth;
                    if (!runtime.TryChooseSkull() || !runtime.TryChooseLightning())
                    { Fail("Formal Lichborne normal-frame skill selection failed"); yield break; }
                    float deadline = Time.realtimeSinceStartup + 2;
                    while (target.CurrentHealth == target.MaxHealth && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    int normalHit = target.MaxHealth - target.CurrentHealth;
                    int normalGodHit = god.MaxHealth - god.CurrentHealth;
                    if (normalHit != impacts[rank - 1] || normalGodHit != 17)
                    { Fail($"Formal Lichborne normal-frame rank={rank} damage={normalHit} god={normalGodHit}"); yield break; }
                    Debug.Log($"JOTUNNSLAYER_FORMAL_LICHBORNE_FRAME_OK rank={rank} damage={normalHit} god={normalGodHit} productionUpdate=True");
                    runtime.enabled = false;
                    runtime.Bind(null, profile, null);
                    Destroy(runtime.gameObject);
                }
                target.Team = god.Team = owner.Team;
                NetworkServer.Destroy(target.gameObject);
                NetworkServer.Destroy(god.gameObject);
                yield return null;
            }
            battle.Bind(owner, restore, null);
            battle.enabled = true;
            if (playground) playground.enabled = playgroundEnabled;
            if (battle.RaidLevel != originalLevel || battle.PendingRewards != originalChoices)
            { Fail($"Formal {label} changed the original raid progression"); yield break; }
            classDamageVerified = true;
        }

        private static IEnumerator ValidateMatchGourmet(CharacterInstance owner, int rank)
        {
            gourmetVerified = false;
            const string prefabPath = "Assets/AssetStore/desNetware/Multiplayer TPS KIT/Levels/Prefabs/Gameplay/prop_healthpack.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (!prefab || !prefab.GetComponent<HealthPickup>())
            { Fail("Gourmet health pickup prefab missing"); yield break; }
            int originalMax = owner.MaxHealth, originalHealth = owner.CurrentHealth;
            var motor = owner.GetComponent<CharacterMotor>();
            bool moving = motor && motor.enabled;
            if (motor) motor.enabled = false;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 400;
            int ordinary = owner.ServerHeal(50, owner.netId);
            var pickup = Instantiate(prefab, owner.transform.position, Quaternion.identity).GetComponent<HealthPickup>();
            pickup.TimeToReplenish = 30;
            var rigidbody = pickup.GetComponent<Rigidbody>();
            if (!rigidbody) rigidbody = pickup.gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            NetworkServer.Spawn(pickup.gameObject);
            Physics.SyncTransforms();
            int expected = rank switch { 1 => 54, 2 => 58, 3 => 66, _ => 78 };
            float deadline = Time.realtimeSinceStartup + 3;
            while (owner && owner.CurrentHealth == 450 && Time.realtimeSinceStartup < deadline)
                yield return new WaitForFixedUpdate();
            int afterPickup = owner.CurrentHealth;
            yield return new WaitForSeconds(.25f);
            bool noDuplicate = owner.CurrentHealth == afterPickup;
            NetworkServer.Destroy(pickup.gameObject);
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            if (motor) motor.enabled = moving;
            if (ordinary != 50 || afterPickup != 450 + expected || !noDuplicate)
            {
                Fail($"Gourmet physical pickup failed rank={rank} ordinary={ordinary} gained={afterPickup - 450} expected={expected} noDuplicate={noDuplicate}");
                yield break;
            }
            gourmetVerified = true;
            Debug.Log($"JOTUNNSLAYER_FORMAL_GOURMET_PICKUP_OK rank={rank} ordinary=50 pickup={expected} noDuplicate=True");
        }

        private static bool ValidateMatchWardRestoring(CharacterInstance owner, CharacterInstance attacker, float rechargeInterval)
        {
            float hitTime = Time.time;
            int before = owner.CurrentHealth;
            owner.Server_ChangeHealthStateRaw(5, CharacterPart.body, AttackType.hitscan, attacker, 0);
            int after = owner.CurrentHealth;
            if (after >= before || !SkillBattlePreview.IsProtectedByEldritchWard(owner, attacker, hitTime))
                return false;

            owner.Server_ChangeHealthStateRaw(5, CharacterPart.body, AttackType.hitscan, attacker, 0);
            if (owner.CurrentHealth != after)
                return false;

            SkillBattlePreview.TriggerEldritchWard(owner, attacker, hitTime + 2f);
            SkillBattlePreview.TriggerEldritchWard(owner, attacker, hitTime + 4f);
            float readyAt = hitTime + rechargeInterval;
            SkillBattlePreview.TriggerEldritchWard(owner, attacker, readyAt - 0.01f);
            if (SkillBattlePreview.IsProtectedByEldritchWard(owner, attacker, readyAt - 0.01f))
                return false;

            SkillBattlePreview.TriggerEldritchWard(owner, attacker, readyAt);
            return SkillBattlePreview.IsProtectedByEldritchWard(owner, attacker, readyAt);
        }

        private static void Fail(string reason)
        {
            Debug.LogError(reason);
            EditorApplication.Exit(1);
        }
    }
}
#endif
