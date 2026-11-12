using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using Mirror;
using MTPSKIT.RainImported;
namespace MTPSKIT.Gameplay
{
    // Explicit development-build integration test; never runs in ordinary sessions.
    public sealed partial class RangeGmSmokeTest : MonoBehaviour
    {
        bool failed;
        bool rainCoop;
        bool rainClient, waitForRainClient;
        bool menuReturn, monsterGm;
        bool itemPlaytest;
        bool monsterRecoveryPlaytest;
        int loggedErrors;

#if UNITY_EDITOR
        public static void RunItemPlaytestBatch()
        {
            if (!Application.isBatchMode) throw new InvalidOperationException("Item playtest requires batch mode.");
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Game/Baseline/Scenes/Startup.unity");
            UnityEditor.EditorApplication.EnterPlaymode();
        }

        public static void RunMonsterRecoveryBatch() => RunItemPlaytestBatch();
#endif
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            bool client = args.Contains("--rain-coop-client-smoke");
            bool rain = client || args.Contains("--rain-coop-smoke");
            bool menu = args.Contains("--range-menu-smoke");
            bool monsterGm = args.Contains("--rain-monster-gm-smoke");
            bool itemPlaytest = args.Contains("-rainItemPlaytest");
            bool monsterRecovery = args.Contains("-rainMonsterRecovery") ||
                Array.Exists(args, arg => arg.StartsWith("-rainMonsterRecovery=", StringComparison.Ordinal));
            if(!Debug.isDebugBuild||(!rain&&!menu&&!monsterGm&&!itemPlaytest&&!monsterRecovery&&!args.Contains("--range-gm-smoke")))return;
            var go=new GameObject("Range GM integration test");DontDestroyOnLoad(go);
            var smoke=go.AddComponent<RangeGmSmokeTest>();
            smoke.rainCoop=rain;
            smoke.menuReturn=menu;
            smoke.monsterGm=monsterGm;
            smoke.itemPlaytest=itemPlaytest;
            smoke.monsterRecoveryPlaytest=monsterRecovery;
            smoke.rainClient=client;
            smoke.waitForRainClient=args.Contains("--rain-coop-wait-client");
        }
        void OnLog(string message,string stack,LogType type)
        {
            if (type is not (LogType.Error or LogType.Exception or LogType.Assert)) return;
            failed = true;
            loggedErrors++;
        }
        bool HitAndKillSmokeTarget(CharacterInstance owner, RangeResearchItems items)
        {
            var victim = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 15f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in victim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            victim.Team = owner.Team == 0 ? 1 : 0;
            victim.MaxHealth = victim.CurrentHealth = 2;
            NetworkServer.Spawn(victim.gameObject);

            float beforeHit = items.FruitCooldownRemaining;
            victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            bool nonlethalIgnored = victim && victim.CurrentHealth == 1 && items.FruitCooldownRemaining == beforeHit;
            if (victim && victim.CurrentHealth > 0)
                victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            bool killed = !victim || victim.CurrentHealth <= 0;
            if (victim) NetworkServer.Destroy(victim.gameObject);
            return nonlethalIgnored && killed;
        }

        IEnumerator VerifyKnurlAndFruit(CharacterInstance owner, RangeResearchItems items)
        {
            int maximum = owner.MaxHealth;
            int originalHealth = owner.CurrentHealth;
            int knurl = RangeResearchItems.CatalogIndex("Knurl");
            int pearl = RangeResearchItems.CatalogIndex("Pearl");
            items.Grant(pearl);
            items.Grant(pearl);
            items.Grant(knurl);
            int pearlBase = maximum + 40;
            if (owner.MaxHealth != (int)Math.Round(pearlBase * 1.2d) ||
                !items.Remove(pearl) || owner.MaxHealth != (int)Math.Round(pearlBase * 1.1d))
                Debug.LogError("RANGE_SMOKE_PEARL_FLAT_HEALTH");
            items.Clear();
            if (owner.MaxHealth != maximum) Debug.LogError("RANGE_SMOKE_PEARL_CLEAR");
            int glass = RangeResearchItems.CatalogIndex("LunarDagger");
            items.Grant(glass);
            items.Grant(glass);
            if (owner.MaxHealth != Mathf.Max(1, Mathf.RoundToInt(maximum * .25f)) || items.ModifyDamage(20) != 60 ||
                !items.Remove(glass) || owner.MaxHealth != Mathf.Max(1, Mathf.RoundToInt(maximum * .5f)))
                Debug.LogError("RANGE_SMOKE_SHAPED_GLASS_STACKS");
            items.Clear();
            if (owner.MaxHealth != maximum) Debug.LogError("RANGE_SMOKE_SHAPED_GLASS_CLEAR");
            items.Grant(knurl);
            items.Grant(knurl);
            owner.CurrentHealth = maximum / 2;
            int injuredHealth = owner.CurrentHealth;
            float start = Time.time;
            yield return new WaitForSeconds(2.5f);
            int recovered = owner.CurrentHealth - injuredHealth;
            if (owner.MaxHealth != maximum + 80 || recovered < 7 ||
                recovered > Mathf.CeilToInt((Time.time - start) * 3.2f) + 1)
                Debug.LogError("RANGE_SMOKE_KNURL_STACK_REGEN");
            if (!items.Remove(knurl) || owner.MaxHealth != maximum + 40)
                Debug.LogError("RANGE_SMOKE_KNURL_REMOVE");
            items.Clear();
            if (owner.MaxHealth != maximum || items.Remove(knurl))
                Debug.LogError("RANGE_SMOKE_KNURL_CLEAR");

            owner.CurrentHealth = 1;
            if (items.TryUseEquipment() || !items.Equip(RangeResearchItems.Equipment.Fruit) || !items.TryUseEquipment() ||
                owner.CurrentHealth != Mathf.Min(maximum, 1 + Mathf.CeilToInt(maximum * .5f)))
                Debug.LogError("RANGE_SMOKE_FRUIT_HEAL");
            float cooldown = items.FruitCooldownRemaining;
            bool blockedDuringCooldown = !items.TryUseEquipment();
            bool unequipped = items.Remove(RangeResearchItems.Equipment.Fruit);
            bool blockedWhileUnequipped = !items.TryUseEquipment();
            bool reequipped = items.Equip(RangeResearchItems.Equipment.Fruit);
            float cooldownAfterSwap = items.FruitCooldownRemaining;
            bool blockedAfterSwap = !items.TryUseEquipment();
            if (Mathf.Abs(cooldown - 45f) > .001f || Mathf.Abs(cooldownAfterSwap - cooldown) > .001f ||
                !blockedDuringCooldown || !unequipped || !blockedWhileUnequipped || !reequipped || !blockedAfterSwap)
                Debug.LogError($"RANGE_SMOKE_FRUIT_COOLDOWN before={cooldown:R} after={cooldownAfterSwap:R} " +
                    $"blockedBefore={blockedDuringCooldown} unequipped={unequipped} blockedWhileUnequipped={blockedWhileUnequipped} " +
                    $"reequipped={reequipped} blockedAfter={blockedAfterSwap}");
            else Debug.Log("RANGE_SMOKE_FRUIT_COOLDOWN_CHECKED");
            int fuelCell = RangeResearchItems.CatalogIndex("EquipmentMagazine");
            items.Grant(fuelCell);
            if (items.FruitCooldownRemaining != 0f || !items.TryUseEquipment() ||
                Mathf.Abs(items.FruitCooldownRemaining - 38.25f) > .05f || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_FUEL_CELL_CHARGE");
            if (!items.Remove(fuelCell) || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_FUEL_CELL_REMOVE");
            items.Grant(fuelCell);
            if (items.TryUseEquipment()) Debug.LogError("RANGE_SMOKE_FUEL_CELL_REEQUIP");
            int catalyst = RangeResearchItems.CatalogIndex("Talisman");
            items.Grant(catalyst);
            items.Grant(catalyst);
            float beforeCatalyst = items.FruitCooldownRemaining;
            items.OnKill(owner.transform.position, false);
            bool ignoredFriendly = items.FruitCooldownRemaining == beforeCatalyst;
            bool twoStacks = HitAndKillSmokeTarget(owner, items) && Mathf.Abs(items.FruitCooldownRemaining - (beforeCatalyst - 6f)) < .05f;
            bool removed = items.Remove(catalyst);
            bool oneStack = HitAndKillSmokeTarget(owner, items) && Mathf.Abs(items.FruitCooldownRemaining - (beforeCatalyst - 10f)) < .05f;
            if (!ignoredFriendly || !twoStacks || !removed || !oneStack || !items.Remove(catalyst))
                Debug.LogError("RANGE_SMOKE_SOULBOUND_CATALYST");
            else Debug.Log("RANGE_SMOKE_SOULBOUND_CATALYST_CHECKED");
            // The two kills advanced the pending recharge by ten seconds.
            const int rechargeTestCells = 20;
            for (int i = 1; i < rechargeTestCells; i++) items.Grant(fuelCell);
            // Two charges have already been spent. Spend all remaining charges to start the wait.
            for (int i = 2; i <= rechargeTestCells; i++)
                if (!items.TryUseEquipment()) Debug.LogError("RANGE_SMOKE_FUEL_CELL_EXTRA_CHARGE " + i);
            float rechargeTime = 45f * Mathf.Pow(.85f, rechargeTestCells);
            if (Mathf.Abs(items.FruitCooldownRemaining - rechargeTime * (1f - 10f / 38.25f)) > .05f || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_FUEL_CELL_STACK_RECHARGE");
            float rechargeDeadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => items.FruitCooldownRemaining <= 0f ||
                Time.realtimeSinceStartup >= rechargeDeadline);
            if (!items.TryUseEquipment() || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_FUEL_CELL_NATURAL_RECHARGE");
            else Debug.Log("RANGE_SMOKE_FUEL_CELL_NATURAL_RECHARGE_CHECKED");
            items.Grant(catalyst);
            if (!HitAndKillSmokeTarget(owner, items) || !items.TryUseEquipment() || !items.TryUseEquipment() || items.TryUseEquipment() ||
                Mathf.Abs(items.FruitCooldownRemaining - (3f * rechargeTime - 4f)) > .1f)
                Debug.LogError("RANGE_SMOKE_SOULBOUND_CATALYST_SERIAL");
            else Debug.Log("RANGE_SMOKE_SOULBOUND_CATALYST_SERIAL_CHECKED");
            items.Clear();
            if (items.FruitEquipped || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_FRUIT_CLEAR");
            var shower = RangeResearchItems.Equipment.BlastShower;
            var dots = owner.GetComponent<RangeBleeding>();
            if (!dots) dots = owner.gameObject.AddComponent<RangeBleeding>();
            dots.AddBurn(owner, 10, 5);
            dots.AddCollapse(owner, 10);
            owner.ResearchTarUntil = owner.ResearchRootUntil = owner.ResearchIceSlowUntil = NetworkTime.time + 3d;
            bool showerUsed = items.Equip(shower) && items.TryUseEquipment();
            float showerCooldown = items.EquipmentCooldownRemaining;
            bool showerReequipped = items.Remove(shower) && items.Equip(shower);
            if (!showerUsed || dots.ActiveDebuffCount() != 0 || owner.ResearchRooted ||
                owner.ResearchTarUntil != 0d || owner.ResearchIceSlowUntil != 0d ||
                Mathf.Abs(showerCooldown - 20f) > .05f || !showerReequipped ||
                items.EquipmentCooldownRemaining != showerCooldown || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_BLAST_SHOWER");
            else Debug.Log("RANGE_SMOKE_BLAST_SHOWER_CHECKED");
            items.Remove(shower);
            owner.ServerHeal(Mathf.Max(0, originalHealth - owner.CurrentHealth), owner.netId, allowOverhealBarrier: false);
            owner.CurrentHealth = originalHealth;
            Debug.Log("RANGE_SMOKE_KNURL_FRUIT_CHECKED");
        }

        IEnumerator Start()
        {
            Application.logMessageReceived+=OnLog;
            yield return new WaitForSeconds(3);
            const string portOption = "--rain-coop-port=";
            var portArgument = rainCoop || monsterGm ? Array.Find(Environment.GetCommandLineArgs(),
                arg => arg.StartsWith(portOption, StringComparison.Ordinal)) : null;
            if (portArgument != null)
            {
                if (!ushort.TryParse(portArgument.Substring(portOption.Length), out ushort port) || port == 0 ||
                    !(DNNetworkManager.Instance.transport is PortTransport transport))
                {
                    Debug.LogError("RAIN_COOP_INVALID_SMOKE_PORT " + portArgument);
                    Application.Quit(1);
                    yield break;
                }
                transport.Port = port;
            }
            if (rainClient)
            {
                yield return VerifyRainClient();
                Debug.Log(failed ? "RAIN_CLIENT_SMOKE_FAILED" : "RAIN_CLIENT_SMOKE_PASSED");
                Application.Quit(failed ? 1 : 0);
                yield break;
            }
            var creator=FindFirstObjectByType<RoomCreator>();
            int index=Array.FindIndex(creator.Maps,m=>rainCoop?RoomCreator.IsRainScene(m.Scene):MTPSKIT.RoomCreator.IsTrainingScene(m.Scene));
            if(index<0){Debug.LogError("RANGE_SMOKE_NO_MAP");FinishPlaytest(1);yield break;}
            creator.LaunchFromFrontEnd(index,rainCoop?1:0,false);
            float deadline=Time.realtimeSinceStartup+30;
            while((!NetworkClient.localPlayer||!NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter)&&Time.realtimeSinceStartup<deadline)yield return null;
            var player=NetworkClient.localPlayer?NetworkClient.localPlayer.GetComponent<PlayerInstance>():null;
            if(!player||!player.MyCharacter){Debug.LogError("RANGE_SMOKE_NO_PLAYER");FinishPlaytest(1);yield break;}
            var owner=player.MyCharacter;
            yield return new WaitForSeconds(2);
            if (monsterRecoveryPlaytest)
            {
                const string filterOption = "-rainMonsterRecovery=";
                string filter = Array.Find(Environment.GetCommandLineArgs(), arg => arg.StartsWith(filterOption, StringComparison.Ordinal));
                string selectedId = filter?.Substring(filterOption.Length);
                int tested = 0;
                foreach (string id in new[] { "HERMIT_CRAB_BODY", "GREATERWISP_BODY", "LEMURIANBRUISER_BODY" })
                {
                    if (selectedId != null && selectedId != id) continue;
                    tested++;
                    yield return VerifyNewMonsterGm(owner, id);
                }
                if (tested == 0) Debug.LogError("RAIN_MONSTER_RECOVERY_UNKNOWN_ID " + selectedId);
                FinishPlaytest(failed ? 1 : 0);
                yield break;
            }
            if (itemPlaytest)
            {
                yield return VerifyPriorityItems(owner);
                FinishPlaytest(failed ? 1 : 0);
                yield break;
            }
            if (monsterGm)
            {
                if (Environment.GetCommandLineArgs().Contains("--rain-parent-gm-smoke"))
                {
                    yield return VerifyParentGm(FindFirstObjectByType<RangeResearchLab>(), owner);
                    if (Environment.GetCommandLineArgs().Contains("--rain-parent-impulse-smoke"))
                        yield return VerifyParentImpulseGm(FindFirstObjectByType<RangeResearchLab>(), owner);
                }
                else yield return VerifyNewMonsterGm(owner);
                Debug.Log(failed ? "RAIN_MONSTER_GM_SMOKE_FAILED" : "RAIN_MONSTER_GM_SMOKE_PASSED");
                Application.Quit(failed ? 1 : 0);
                yield break;
            }
            if (menuReturn)
            {
                DNNetworkManager.Instance.StopHost();
                float menuDeadline = Time.realtimeSinceStartup + 15f;
                while (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene &&
                    Time.realtimeSinceStartup < menuDeadline) yield return null;
                yield return new WaitForSeconds(.5f);
                if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene ||
                    NetworkClient.active || NetworkServer.active || !DNNetworkManager.Instance ||
                    !GameObject.Find("Page home"))
                    Debug.LogError("RANGE_MENU_RETURN_FAILED");
                Debug.Log(failed ? "RANGE_MENU_SMOKE_FAILED" : "RANGE_MENU_SMOKE_PASSED");
                Application.Quit(failed ? 1 : 0);
                yield break;
            }
            if (rainCoop)
            {
                yield return VerifyRainCoop(player, owner);
                Debug.Log(failed ? "RAIN_COOP_SMOKE_FAILED" : "RAIN_COOP_SMOKE_PASSED");
                Application.Quit(failed ? 1 : 0);
                yield break;
            }
            if(FindObjectsByType<ItemSpawner>(FindObjectsSortMode.None).Any(s=>s.ItemPrefab&&s.ItemPrefab.GetComponent<Item>()))Debug.LogError("RANGE_SMOKE_WEAPON_SPAWNER_REMAINS");
            int weapons=0;
            foreach(var prefab in DNNetworkManager.Instance.spawnPrefabs)
            {
                if(!prefab||!prefab.GetComponent<Item>())continue;
                if(!owner.CharacterItemManager.ServerEquipTestWeapon(prefab))Debug.LogError("RANGE_SMOKE_WEAPON_FAILED "+prefab.name);
                else weapons++;
                yield return new WaitForSeconds(.25f);
                if(!owner.CharacterItemManager.CurrentlyUsedItem||owner.CharacterItemManager.CurrentlyUsedItem.ItemName!=prefab.GetComponent<Item>().ItemName)Debug.LogError("RANGE_SMOKE_WEAPON_NOT_SELECTED "+prefab.name);
            }
            Debug.Log("RANGE_SMOKE_WEAPONS="+weapons);
            var dummy=Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),owner.transform.position+Vector3.right*4,Quaternion.identity).GetComponent<CharacterInstance>();
            foreach(var slot in dummy.CharacterItemManager.Slots)slot.ItemOnSpawn=null;
            dummy.Team=owner.Team==0?1:0;dummy.MaxHealth=dummy.CurrentHealth=100000;
            NetworkServer.Spawn(dummy.gameObject);
            dummy.GetComponent<CharacterMotor>().enabled=false;
            yield return VerifyBeetleHeadbutt(owner,dummy);
            yield return VerifyTitanBeam(owner,dummy);
            foreach (string id in new[] { "GM_SNOW_GOLEM", "GM_NATURE_GOLEM", "GM_SANDY_GOLEM" })
            {
                string model=RangeResearchLab.MonsterVariant(id);
                var golem=Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),owner.transform.position+Vector3.right*8,Quaternion.identity).GetComponent<CharacterInstance>();
                foreach(var slot in golem.CharacterItemManager.Slots)slot.ItemOnSpawn=null;
                golem.Team=dummy.Team;
                NetworkServer.Spawn(golem.gameObject);
                var variant=golem.gameObject.AddComponent<RangeResearchEnemy>();
                variant.Configure(owner,1,model);
                variant.OwnsCharacter=true;
                bool sandy = model == RangeResearchEnemy.SandyGolemVariant;
                var instance=golem.transform.Find(sandy ? "mdlGolem" : model);
                var renderer=instance?instance.GetComponentInChildren<SkinnedMeshRenderer>():null;
                bool hasGraphics = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
                if(!renderer || !renderer.sharedMesh ||
                    (hasGraphics && (!renderer.sharedMaterial || !renderer.sharedMaterial.mainTexture ||
                        (sandy && renderer.sharedMaterial.mainTexture.name != "texSandyGolemDiffuse"))))
                    Debug.LogError($"RANGE_SMOKE_GOLEM_VARIANT_MISSING {id} model={!!instance} renderer={!!renderer} mesh={renderer && renderer.sharedMesh} texture={renderer && renderer.sharedMaterial && renderer.sharedMaterial.mainTexture}");
                else Debug.Log($"RANGE_SMOKE_GOLEM_VARIANT_READY {id} textureChecked={hasGraphics}");
                variant.RemoveFromRange();
                yield return null;
            }
            var items=owner.GetComponent<RangeResearchItems>();
            yield return VerifyKnurlAndFruit(owner, items);
            var elephant = RangeResearchItems.Equipment.Elephant;
            int warHorn = RangeResearchItems.CatalogIndex("EnergizedOnEquipmentUse");
            items.Grant(warHorn);
            if (player.ServerTryUseRangeEquipment() || owner.ResearchWarHornActive)
                Debug.LogError("RANGE_SMOKE_WAR_HORN_REJECTED_USE");
            int gesture = RangeResearchItems.CatalogIndex("AutoCastEquipment");
            items.Grant(gesture);
            if (!items.Equip(elephant) || player.ServerTryUseRangeEquipment())
                Debug.LogError("RANGE_SMOKE_GESTURE_MANUAL_USE");
            float gestureDeadline = Time.realtimeSinceStartup + 2f;
            yield return new WaitUntil(() => items.EquipmentCooldownRemaining > 0f ||
                Time.realtimeSinceStartup >= gestureDeadline);
            float gestureCooldown = items.EquipmentCooldownRemaining;
            bool gestureRemoved = items.Remove(gesture);
            float restoredCooldown = items.EquipmentCooldownRemaining;
            if (gestureCooldown < 22.5f - Time.maximumDeltaTime - .01f || gestureCooldown > 22.51f ||
                !gestureRemoved || Mathf.Abs(restoredCooldown - 2f * gestureCooldown) > .01f ||
                player.ServerTryUseRangeEquipment())
                Debug.LogError($"RANGE_SMOKE_GESTURE_AUTO_USE active={gestureCooldown} restored={restoredCooldown} removed={gestureRemoved}");
            if (!Mathf.Approximately(owner.ResearchAttackSpeed, 1.7f))
                Debug.LogError("RANGE_SMOKE_WAR_HORN_SPEED");
            int elephantHealth = owner.CurrentHealth;
            owner.Server_ChangeHealthStateRaw(60, CharacterPart.body, AttackType.melee, dummy, 0);
            if (owner.CurrentHealth != elephantHealth - 10 || !items.Remove(elephant) ||
                items.TryUseEquipment() || !items.Equip(elephant) || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_ELEPHANT_ARMOR_OR_SWAP");
            yield return new WaitForSeconds(5.1f);
            if (items.ReduceIncomingDamage(60) != 60 || items.EquipmentCooldownRemaining <= 0f)
                Debug.LogError("RANGE_SMOKE_ELEPHANT_ARMOR_EXPIRES");
            items.Clear();
            if (owner.ResearchWarHornActive) Debug.LogError("RANGE_SMOKE_WAR_HORN_CLEAR");
            owner.CurrentHealth = elephantHealth;
            Debug.Log("RANGE_SMOKE_ELEPHANT_CHECKED");
            var ocularHud = RangeResearchItems.Equipment.OcularHud;
            int ocularTargetHealth = dummy.CurrentHealth;
            if (!items.Equip(ocularHud) || !player.ServerTryUseRangeEquipment() ||
                items.EquipmentCooldownRemaining < 59.9f || player.ServerTryUseRangeEquipment())
                Debug.LogError("RANGE_SMOKE_OCULAR_HUD_ACTIVATE");
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != ocularTargetHealth - 40)
                Debug.LogError("RANGE_SMOKE_OCULAR_HUD_CRIT");
            yield return new WaitForSeconds(8.1f);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != ocularTargetHealth - 60 || items.EquipmentCooldownRemaining <= 0f)
                Debug.LogError("RANGE_SMOKE_OCULAR_HUD_EXPIRES");
            items.Clear();
            dummy.CurrentHealth = ocularTargetHealth;
            Debug.Log("RANGE_SMOKE_OCULAR_HUD_CHECKED");
            var leech = RangeResearchItems.Equipment.Leech;
            int leechOwnerHealth = owner.CurrentHealth;
            int leechTargetHealth = dummy.CurrentHealth;
            owner.CurrentHealth = 1;
            if (!items.Equip(leech) || !player.ServerTryUseRangeEquipment() ||
                items.EquipmentCooldownRemaining < 59.9f || player.ServerTryUseRangeEquipment())
                Debug.LogError("RANGE_SMOKE_LEECH_ACTIVATE");
            dummy.Server_ChangeHealthStateRaw(3, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            if (owner.CurrentHealth != 1) Debug.LogError("RANGE_SMOKE_LEECH_FRACTION");
            dummy.Server_ChangeHealthStateRaw(2, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            dummy.Server_ChangeHealthStateRaw(50, CharacterPart.body, AttackType.melee, owner, 0);
            if (owner.CurrentHealth != 12) Debug.LogError("RANGE_SMOKE_LEECH_HEAL");
            items.Clear();
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0);
            if (owner.CurrentHealth != 12) Debug.LogError("RANGE_SMOKE_LEECH_CLEAR");
            owner.CurrentHealth = leechOwnerHealth;
            dummy.CurrentHealth = leechTargetHealth;
            Debug.Log("RANGE_SMOKE_LEECH_CHECKED");
            int mocha = RangeResearchItems.CatalogIndex("AttackSpeedAndMoveSpeed");
            var mochaMotor = owner.GetComponent<CharacterMotor>();
            float baseWalk = mochaMotor.GetMoveSpeed(false);
            float baseRun = mochaMotor.GetMoveSpeed(true);
            items.Grant(mocha);
            items.Grant(mocha);
            float mochaWalk = mochaMotor.GetMoveSpeed(false);
            float mochaRun = mochaMotor.GetMoveSpeed(true);
            mochaMotor.RangeItems = null;
            bool replicaSpeed = Mathf.Approximately(mochaMotor.GetMoveSpeed(false), mochaWalk) &&
                Mathf.Approximately(mochaMotor.GetMoveSpeed(true), mochaRun);
            mochaMotor.RangeItems = items;
            if (!Mathf.Approximately(owner.ResearchAttackSpeed, 1.15f) ||
                !Mathf.Approximately(mochaWalk, baseWalk * 1.14f) || !Mathf.Approximately(mochaRun, baseRun * 1.14f) ||
                !replicaSpeed || !items.Remove(mocha) || !Mathf.Approximately(owner.ResearchAttackSpeed, 1.075f) ||
                !Mathf.Approximately(mochaMotor.GetMoveSpeed(false), baseWalk * 1.07f))
                Debug.LogError("RANGE_SMOKE_MOCHA");
            else Debug.Log("RANGE_SMOKE_MOCHA_CHECKED");
            items.Clear();
            int stoneFlux = RangeResearchItems.CatalogIndex("HalfSpeedDoubleHealth");
            int stoneBaseHealth = owner.MaxHealth;
            items.Grant(stoneFlux);
            bool firstStone = owner.MaxHealth == 2 * stoneBaseHealth &&
                Mathf.Approximately(mochaMotor.GetMoveSpeed(false), baseWalk / 2f);
            items.Grant(stoneFlux);
            bool secondStone = owner.MaxHealth == 3 * stoneBaseHealth &&
                Mathf.Approximately(mochaMotor.GetMoveSpeed(true), baseRun / 3f);
            if (!firstStone || !secondStone || !items.Remove(stoneFlux) || owner.MaxHealth != 2 * stoneBaseHealth ||
                !items.Remove(stoneFlux) || owner.MaxHealth != stoneBaseHealth ||
                !Mathf.Approximately(mochaMotor.GetMoveSpeed(false), baseWalk))
                Debug.LogError("RANGE_SMOKE_STONE_FLUX");
            else Debug.Log("RANGE_SMOKE_STONE_FLUX_CHECKED");
            items.Clear();
            int expansion = RangeResearchItems.CatalogIndex("IncreaseDamageOnMultiKill");
            items.Grant(expansion);
            items.Grant(expansion);
            items.OnKill(dummy.transform.position, false);
            bool ignoredAlly = items.ModifyDamage(200) == 200;
            for (int i = 0; i < 16; i++) items.OnKill(dummy.transform.position, true);
            bool capped = items.ModifyDamage(200) == 335;
            bool removed = items.Remove(expansion) && items.ModifyDamage(200) == 270;
            yield return new WaitForSeconds(4f);
            int beforeRefreshHit = dummy.CurrentHealth;
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0);
            dummy.CurrentHealth = beforeRefreshHit;
            yield return new WaitForSeconds(3.3f);
            bool refreshed = items.ModifyDamage(200) == 270;
            yield return new WaitForSeconds(4f);
            if (ignoredAlly && capped && removed && refreshed && items.ModifyDamage(200) == 200)
                Debug.Log("RANGE_SMOKE_CHRONIC_EXPANSION_CHECKED");
            else Debug.LogError("RANGE_SMOKE_CHRONIC_EXPANSION");
            items.Clear();
            int famine = RangeResearchItems.CatalogIndex("LowerHealing");
            int beforeFamineHealth = owner.CurrentHealth;
            owner.CurrentHealth = 1;
            items.Grant(famine);
            items.Grant(famine);
            int famineHealing = 0;
            for (int i = 0; i < 9; i++) famineHealing += owner.ServerHeal(1, owner.netId);
            bool famineStacked = famineHealing == 5 && owner.CurrentHealth == 6;
            bool famineRemoved = items.Remove(famine) && owner.ServerHeal(7, owner.netId) == 5 &&
                items.Remove(famine) && owner.ServerHeal(7, owner.netId) == 7;
            if (famineStacked && famineRemoved) Debug.Log("RANGE_SMOKE_HEX_OF_FAMINE_CHECKED");
            else Debug.LogError("RANGE_SMOKE_HEX_OF_FAMINE");
            int pain = RangeResearchItems.CatalogIndex("HexCurse");
            int painBaseHealth = owner.MaxHealth;
            int painShield = RangeResearchItems.CatalogIndex("PersonalShield");
            items.Grant(painShield);
            int originalPainShield = owner.ResearchShield;
            items.Grant(pain);
            bool firstPain = owner.MaxHealth == (int)Math.Round(painBaseHealth / 1.3d) &&
                owner.CurrentHealth <= owner.MaxHealth && owner.ResearchShield == Mathf.RoundToInt(owner.MaxHealth * .08f);
            items.Grant(pain);
            bool secondPain = owner.MaxHealth == (int)Math.Round(painBaseHealth / 1.6d) &&
                owner.ResearchShield == Mathf.RoundToInt(owner.MaxHealth * .08f);
            int glass = RangeResearchItems.CatalogIndex("LunarDagger");
            items.Grant(glass);
            bool combinedPain = owner.MaxHealth == (int)Math.Round(painBaseHealth / 2.6d);
            if (firstPain && secondPain && combinedPain && items.Remove(glass) &&
                items.Remove(pain) && items.Remove(pain) && owner.MaxHealth == painBaseHealth &&
                originalPainShield > 0 && owner.ResearchShield == originalPainShield && items.Remove(painShield))
                Debug.Log("RANGE_SMOKE_HEX_OF_PAIN_CHECKED");
            else Debug.LogError("RANGE_SMOKE_HEX_OF_PAIN");
            owner.CurrentHealth = beforeFamineHealth;
            items.Clear();
            yield return new WaitForSecondsRealtime(5.1f);
            int whip = RangeResearchItems.CatalogIndex("SprintOutOfCombat");
            int whipTargetHealth = dummy.CurrentHealth;
            items.Grant(whip);
            items.Grant(whip);
            if (!Mathf.Approximately(items.MoveSpeed(5f, 10f, false), 8f))
                Debug.LogError("RANGE_SMOKE_RED_WHIP_STACK");
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, owner, 0);
            if (!Mathf.Approximately(items.MoveSpeed(5f, 10f, true), 10f))
                Debug.LogError("RANGE_SMOKE_RED_WHIP_COMBAT");
            yield return new WaitForSecondsRealtime(5.1f);
            if (!items.Remove(whip) || !Mathf.Approximately(items.MoveSpeed(5f, 10f, true), 13f))
                Debug.LogError("RANGE_SMOKE_RED_WHIP_RECOVERY_REMOVE");
            var whipMotor = owner.GetComponent<CharacterMotor>();
            float hostWhipSpeed = whipMotor.GetMoveSpeed(true);
            whipMotor.RangeItems = null;
            float replicatedWhipSpeed = whipMotor.GetMoveSpeed(true);
            whipMotor.RangeItems = items;
            if (!Mathf.Approximately(hostWhipSpeed, replicatedWhipSpeed))
                Debug.LogError("RANGE_SMOKE_RED_WHIP_REPLICATED_SPEED");
            items.Clear();
            dummy.CurrentHealth = whipTargetHealth;
            Debug.Log("RANGE_SMOKE_RED_WHIP_CHECKED");
            items.Grant(RangeResearchItems.CatalogIndex("WarCryOnMultiKill"));
            for (int i = 0; i < 3; i++) items.OnKill(dummy.transform.position, true);
            items.OnKill(dummy.transform.position, false);
            if (owner.ResearchBerserkerFrenzy) Debug.LogError("RANGE_SMOKE_BERSERKER_EARLY");
            items.OnKill(dummy.transform.position, true);
            if (!Mathf.Approximately(owner.ResearchAttackSpeed, 2f) ||
                !Mathf.Approximately(items.MoveSpeed(4f, 8f, false), 6f))
                Debug.LogError("RANGE_SMOKE_BERSERKER_FRENZY");
            var frenzyMotor = owner.GetComponent<CharacterMotor>();
            float hostFrenzySpeed = frenzyMotor.GetMoveSpeed(false);
            frenzyMotor.RangeItems = null;
            float replicatedFrenzySpeed = frenzyMotor.GetMoveSpeed(false);
            frenzyMotor.RangeItems = items;
            if (!Mathf.Approximately(hostFrenzySpeed, frenzyMotor.WalkSpeed * 1.5f) ||
                !Mathf.Approximately(hostFrenzySpeed, replicatedFrenzySpeed))
                Debug.LogError("RANGE_SMOKE_BERSERKER_REPLICATED_SPEED");
            items.Clear();
            if (owner.ResearchBerserkerFrenzy) Debug.LogError("RANGE_SMOKE_BERSERKER_CLEAR");
            int enemyTeam = dummy.Team;
            dummy.Team = owner.Team;
            if (!items.Equip(RangeResearchItems.Equipment.TeamWarCry) || !player.ServerTryUseRangeEquipment() ||
                items.EquipmentCooldownRemaining < 44.9f || !owner.ResearchBerserkerFrenzy ||
                !dummy.ResearchBerserkerFrenzy)
                Debug.LogError("RANGE_SMOKE_TEAM_WAR_CRY");
            dummy.Team = enemyTeam;
            if (dummy.TryGetComponent<RangeResearchItems>(out var teamWarCryItems)) teamWarCryItems.Clear();
            items.Clear();
            yield return VerifyCapacitor(player, owner, dummy, items);
            int perforator = RangeResearchItems.CatalogIndex("LightningStrikeOnHit");
            int beforePerforator = dummy.CurrentHealth;
            items.Grant(perforator);
            items.Grant(perforator);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 20f);
            yield return new WaitForSeconds(.1f);
            if (dummy.CurrentHealth != beforePerforator - 240)
                Debug.LogError("RANGE_SMOKE_CHARGED_PERFORATOR");
            else Debug.Log("RANGE_SMOKE_CHARGED_PERFORATOR_CHECKED");
            items.Clear();
            dummy.CurrentHealth = beforePerforator;
            int scope = RangeResearchItems.CatalogIndex("CritDamage");
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            items.Grant(scope);
            items.Grant(scope);
            if (items.ModifyDamage(20) != 20) Debug.LogError("RANGE_SMOKE_SCOPE_GRANTS_CRIT_CHANCE");
            for (int i = 0; i < 10; i++) items.Grant(glasses);
            int scopeTargetHealth = dummy.CurrentHealth;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != scopeTargetHealth - 80) Debug.LogError("RANGE_SMOKE_SCOPE_STACK_DAMAGE");
            if (!items.Remove(scope) || items.ModifyDamage(20) != 60)
                Debug.LogError("RANGE_SMOKE_SCOPE_REMOVE");
            items.Clear();
            if (items.Remove(scope)) Debug.LogError("RANGE_SMOKE_SCOPE_CLEAR");
            dummy.CurrentHealth = scopeTargetHealth;
            Debug.Log("RANGE_SMOKE_SCOPE_CHECKED");
            int clover = RangeResearchItems.CatalogIndex("Clover");
            items.Grant(glasses);
            var originalLuckRandom = UnityEngine.Random.state;
            UnityEngine.Random.State luckyRoll;
            do
            {
                luckyRoll = UnityEngine.Random.state;
            } while (UnityEngine.Random.value < .1f || UnityEngine.Random.value >= .1f);
            UnityEngine.Random.state = luckyRoll;
            if (items.ModifyDamage(20) != 20) Debug.LogError("RANGE_SMOKE_CLOVER_BASE_ROLL");
            items.Grant(clover);
            UnityEngine.Random.state = luckyRoll;
            if (items.ModifyDamage(20) != 40 || !items.Remove(clover))
                Debug.LogError("RANGE_SMOKE_CLOVER_REROLL");
            int ultimateMeal = RangeResearchItems.CatalogIndex("UltimateMeal");
            int beforeMealHealth = owner.CurrentHealth;
            UnityEngine.Random.State mealRoll;
            do
            {
                mealRoll = UnityEngine.Random.state;
            } while (UnityEngine.Random.value < .1f || UnityEngine.Random.value < .1f ||
                UnityEngine.Random.value < .1f || UnityEngine.Random.value < .1f || UnityEngine.Random.value >= .1f);
            items.Grant(ultimateMeal);
            items.Grant(ultimateMeal);
            owner.CurrentHealth = owner.MaxHealth;
            UnityEngine.Random.state = mealRoll;
            bool fullMeal = items.ModifyDamage(20) == 40;
            owner.CurrentHealth--;
            UnityEngine.Random.state = mealRoll;
            bool injuredMeal = items.ModifyDamage(20) == 20;
            owner.CurrentHealth = owner.MaxHealth;
            UnityEngine.Random.state = mealRoll;
            bool removedMeal = items.Remove(ultimateMeal) && items.ModifyDamage(20) == 20;
            if (fullMeal && injuredMeal && removedMeal)
                Debug.Log("RANGE_SMOKE_ULTIMATE_MEAL_CHECKED");
            else Debug.LogError("RANGE_SMOKE_ULTIMATE_MEAL");
            owner.CurrentHealth = beforeMealHealth;
            UnityEngine.Random.state = originalLuckRandom;
            items.Clear();
            int stew = RangeResearchItems.CatalogIndex("Stew");
            int beforeStewHealth = owner.CurrentHealth;
            float beforeStewRegen = owner.RainHealthRegen;
            float beforeStewAttack = owner.EffectiveRainAttackPower;
            float stewRegen = 2.5f * beforeStewAttack / CharacterInstance.RainBaseAttackPower;
            items.Grant(stew);
            items.Grant(stew);
            owner.CurrentHealth = owner.MaxHealth;
            bool fullStew = owner.ResearchStews == 2 &&
                Mathf.Approximately(owner.EffectiveRainHealthRegen, beforeStewRegen + 2f * stewRegen) &&
                Mathf.Approximately(owner.EffectiveRainAttackPower, beforeStewAttack + beforeStewRegen + 2f * stewRegen) &&
                owner.ScaleRainWeaponDamage(24) == Mathf.RoundToInt(2f * owner.EffectiveRainAttackPower);
            owner.CurrentHealth--;
            bool injuredStew = Mathf.Approximately(owner.EffectiveRainAttackPower, beforeStewAttack);
            bool removedStew = items.Remove(stew) && owner.ResearchStews == 1 &&
                Mathf.Approximately(owner.EffectiveRainHealthRegen, beforeStewRegen + stewRegen) &&
                items.Remove(stew) && owner.ResearchStews == 0 &&
                Mathf.Approximately(owner.EffectiveRainHealthRegen, beforeStewRegen);
            if (fullStew && injuredStew && removedStew) Debug.Log("RANGE_SMOKE_STEW_CHECKED");
            else Debug.LogError("RANGE_SMOKE_STEW");
            int stewKnurl = RangeResearchItems.CatalogIndex("Knurl");
            items.Grant(stewKnurl);
            items.Grant(stew);
            owner.CurrentHealth = owner.MaxHealth;
            float knurlRegen = RangeResearchItems.KnurlRegenerationPerStack * beforeStewAttack / CharacterInstance.RainBaseAttackPower;
            bool combinedStew = Mathf.Approximately(owner.EffectiveRainHealthRegen, beforeStewRegen + stewRegen + knurlRegen) &&
                Mathf.Approximately(owner.EffectiveRainAttackPower, beforeStewAttack + beforeStewRegen + stewRegen + knurlRegen);
            bool removedKnurl = items.Remove(stewKnurl) && owner.ResearchKnurls == 0 &&
                Mathf.Approximately(owner.EffectiveRainAttackPower, beforeStewAttack + beforeStewRegen + stewRegen);
            if (combinedStew && removedKnurl && items.Remove(stew)) Debug.Log("RANGE_SMOKE_STEW_KNURL_CHECKED");
            else Debug.LogError("RANGE_SMOKE_STEW_KNURL");
            int daisy = RangeResearchItems.CatalogIndex("TPHealingNova");
            int seed = RangeResearchItems.CatalogIndex("Seed");
            items.Grant(daisy);
            bool incompleteStew = !items.TryCookMeal("Stew") && owner.ResearchLeptonDaisies == 1;
            items.Grant(seed);
            bool cookedStew = items.TryCookMeal("Stew") && !items.TryCookMeal("Stew") &&
                owner.ResearchStews == 1 && owner.ResearchLeptonDaisies == 0 && owner.ResearchSeeds == 0;
            if (incompleteStew && cookedStew && items.Remove(stew)) Debug.Log("RANGE_SMOKE_STEW_RECIPE_CHECKED");
            else Debug.LogError("RANGE_SMOKE_STEW_RECIPE");
            items.Grant(daisy);
            bool incompleteDoubleDaisy = !items.TryCookMeal("StewDoubleDaisy") && owner.ResearchLeptonDaisies == 1;
            items.Grant(daisy);
            bool doubleDaisyStew = items.TryCookMeal("StewDoubleDaisy") && !items.TryCookMeal("StewDoubleDaisy") &&
                owner.ResearchStews == 1 && owner.ResearchLeptonDaisies == 0 && items.Remove(stew);
            if (incompleteDoubleDaisy && doubleDaisyStew) Debug.Log("RANGE_SMOKE_STEW_DOUBLE_DAISY_CHECKED");
            else Debug.LogError("RANGE_SMOKE_STEW_DOUBLE_DAISY");
            owner.CurrentHealth = beforeStewHealth;
            yield return VerifyStewCombat(owner, dummy, items, stew);
            VerifyQuickFix(owner, items);
            VerifyWhitePrinterPayments(owner, items);
            VerifyWhiteScrapperBatch(owner, items);
            yield return VerifyBolsteringLantern(owner, items);
            yield return VerifyElusiveAntlers(owner, items);
            yield return VerifyAntlerPickupCallbacks(owner, items);
            yield return VerifyCollectorPickups(owner, items);
            yield return VerifyAntlerNaturalGeneration(owner, items);
            yield return VerifyAntlerAttraction(owner, items);
            yield return VerifyAntlerParticleLifecycle();
            yield return VerifyAntlerDistanceLifetime(owner, items);
            yield return VerifyAntlerImmediateReplacement(owner, items);
            yield return VerifyHikingBoots(owner, dummy, items);
            int lostSeers = RangeResearchItems.CatalogIndex("CritGlassesVoid");
            for (int i = 0; i < 11; i++) items.Grant(glasses);
            items.Grant(lostSeers);
            if (items.Glasses != 0 || items.ModifyDamage(20) != 20 ||
                items.RollLostSeersExecution(dummy, 0f) || !items.RollLostSeersExecution(dummy, 17f))
                Debug.LogError("RANGE_SMOKE_LOST_SEERS_CORRUPTION_OR_PROC");
            items.Clear();
            Debug.Log("RANGE_SMOKE_LOST_SEERS_CHECKED");
            items.Grant(RangeResearchItems.CatalogIndex("ArmorReductionOnHit"));
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            for (int i = 0; i < 5; i++)
                dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != scopeTargetHealth - 240)
                Debug.LogError("RANGE_SMOKE_JUSTICE_BUILDUP");
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != scopeTargetHealth - 295)
                Debug.LogError("RANGE_SMOKE_JUSTICE_ARMOR");
            if (dummy.TryGetComponent<RangeResearchItems>(out var targetJusticeItems)) targetJusticeItems.Clear();
            dummy.CurrentHealth = scopeTargetHealth;
            items.Clear();
            int scorpion = RangeResearchItems.CatalogIndex("PermanentDebuffOnHit");
            for (int i = 0; i < 10; i++) items.Grant(scorpion);
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != scopeTargetHealth - 80)
                Debug.LogError("RANGE_SMOKE_SCORPION_INITIAL");
            items.Clear();
            if (items.Remove(scorpion)) Debug.LogError("RANGE_SMOKE_SCORPION_CLEAR");
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != scopeTargetHealth - 127)
                Debug.LogError("RANGE_SMOKE_SCORPION_ARMOR_PERSISTS");
            items.Grant(RangeResearchItems.CatalogIndex("ArmorReductionOnHit"));
            for (int i = 0; i < 5; i++)
                dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("DeathMark"));
            dummy.ResearchIceSlowUntil = NetworkTime.time + 3;
            int beforeMark = dummy.CurrentHealth;
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            dummy.ResearchBaubleSlowUntil = NetworkTime.time + 3;
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0);
            if (dummy.CurrentHealth != beforeMark - 116)
                Debug.LogError("RANGE_SMOKE_DEATH_MARK_THRESHOLD");
            items.Clear();
            dummy.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            if (dummy.CurrentHealth != beforeMark - 203)
                Debug.LogError("RANGE_SMOKE_DEATH_MARK_AMPLIFICATION");
            dummy.ResearchIceSlowUntil = dummy.ResearchBaubleSlowUntil = 0;
            if (targetJusticeItems) targetJusticeItems.Clear();
            dummy.CurrentHealth = scopeTargetHealth;
            int scythe = RangeResearchItems.CatalogIndex("HealOnCrit");
            items.Grant(scythe);
            items.Grant(scythe);
            for (int i = 0; i < 10; i++) items.Grant(glasses);
            int healthBeforeScythe = owner.CurrentHealth;
            owner.CurrentHealth = 1;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0, .5f);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            if (owner.CurrentHealth != 7 || !items.Remove(scythe))
                Debug.LogError("RANGE_SMOKE_SCYTHE_PROC");
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0);
            if (owner.CurrentHealth != 15) Debug.LogError("RANGE_SMOKE_SCYTHE_REMOVE");
            items.Clear();
            owner.CurrentHealth = healthBeforeScythe;
            dummy.CurrentHealth = scopeTargetHealth;
            Debug.Log("RANGE_SMOKE_SCYTHE_CHECKED");
            int predatory = RangeResearchItems.CatalogIndex("AttackSpeedOnCrit");
            items.Grant(predatory);
            items.Grant(predatory);
            for (int i = 0; i < 10; i++) items.Grant(glasses);
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            if (owner.ResearchPredatoryBuffs != 0) Debug.LogError("RANGE_SMOKE_PREDATORY_ZERO_PROC");
            for (int i = 0; i < 6; i++)
                dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, owner, 0, .5f);
            if (!Mathf.Approximately(owner.ResearchAttackSpeed, 1.6f) || !items.Remove(predatory) ||
                !Mathf.Approximately(owner.ResearchAttackSpeed, 1.36f))
                Debug.LogError("RANGE_SMOKE_PREDATORY_STACK_CAP");
            yield return new WaitForSeconds(3.1f);
            if (!Mathf.Approximately(owner.ResearchAttackSpeed, 1f))
                Debug.LogError("RANGE_SMOKE_PREDATORY_EXPIRES");
            items.Clear();
            dummy.CurrentHealth = scopeTargetHealth;
            Debug.Log("RANGE_SMOKE_PREDATORY_CHECKED");
            int extraLife = RangeResearchItems.CatalogIndex("ExtraLife");
            int spentLife = RangeResearchItems.CatalogIndex("ExtraLifeConsumed");
            items.Grant(extraLife);
            owner.Server_ChangeHealthStateRaw(owner.MaxHealth + 1, CharacterPart.body, AttackType.melee, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth || !items.HasExtraLifeProtection ||
                items.ConsumedExtraLives != 1 || items.Remove(extraLife))
                Debug.LogError("RANGE_SMOKE_DIO_REVIVE");
            owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth || !items.Remove(spentLife) || items.ConsumedExtraLives != 0)
                Debug.LogError("RANGE_SMOKE_DIO_PROTECTION_OR_CONSUMED");
            yield return new WaitForSeconds(3.1f);
            owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, dummy, 0);
            if (items.HasExtraLifeProtection || owner.CurrentHealth != owner.MaxHealth - 20)
                Debug.LogError("RANGE_SMOKE_DIO_PROTECTION_EXPIRES");
            items.Clear();
            owner.ServerHeal(20, owner.netId, allowOverhealBarrier: false);
            Debug.Log("RANGE_SMOKE_DIO_CHECKED");
            int voidLife = RangeResearchItems.CatalogIndex("ExtraLifeVoid");
            int spentVoidLife = RangeResearchItems.CatalogIndex("ExtraLifeVoidConsumed");
            items.Grant(extraLife);
            items.Grant(voidLife);
            items.Grant(extraLife);
            if (items.Remove(extraLife)) Debug.LogError("RANGE_SMOKE_VOID_LIFE_CORRUPTION");
            owner.Server_ChangeHealthStateRaw(owner.MaxHealth + 1, CharacterPart.body, AttackType.melee, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth || !items.HasExtraLifeProtection ||
                items.ConsumedExtraLives != 0 || items.ConsumedVoidExtraLives != 1 ||
                !items.Remove(voidLife) || !items.Remove(voidLife) || items.Remove(voidLife))
                Debug.LogError("RANGE_SMOKE_VOID_LIFE_REVIVE");
            items.Grant(extraLife);
            if (items.Remove(extraLife) || !items.Remove(voidLife) || !items.Remove(spentVoidLife))
                Debug.LogError("RANGE_SMOKE_VOID_LIFE_CONSUMED_CORRUPTION");
            items.Clear();
            Debug.Log("RANGE_SMOKE_VOID_LIFE_CHECKED");
            int safer = RangeResearchItems.CatalogIndex("BearVoid");
            int bear = RangeResearchItems.CatalogIndex("Bear");
            items.Grant(bear);
            items.Grant(safer);
            items.Grant(bear);
            int saferHealth = owner.CurrentHealth;
            owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, dummy, 0);
            if (owner.CurrentHealth != saferHealth || items.Bears != 0)
                Debug.LogError("RANGE_SMOKE_SAFER_SPACES_BLOCK_CORRUPTION");
            owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, dummy, 0);
            if (owner.CurrentHealth != saferHealth - 20 || !items.Remove(safer) ||
                !items.Remove(safer) || !items.Remove(safer) || items.Remove(safer))
                Debug.LogError("RANGE_SMOKE_SAFER_SPACES_COOLDOWN_STACKS");
            items.Grant(safer);
            if (items.RollDamageBlock()) Debug.LogError("RANGE_SMOKE_SAFER_SPACES_REEQUIP");
            items.Clear();
            owner.ServerHeal(20, owner.netId, allowOverhealBarrier: false);
            Debug.Log("RANGE_SMOKE_SAFER_SPACES_CHECKED");
            for (int i = 0; i < 10; i++) items.Grant(RangeResearchItems.CatalogIndex("BleedOnHit"));
            int beforeBleed = dummy.CurrentHealth;
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            if (dummy.CurrentHealth != beforeBleed - 40) Debug.LogError("RANGE_SMOKE_BLEED_INITIAL_HITS");
            yield return new WaitForSeconds(3.25f);
            int fullDaggerBleed = Mathf.RoundToInt(items.ItemBaseAttack * 2.4f);
            if (dummy.CurrentHealth != beforeBleed - 40 - 2 * fullDaggerBleed ||
                !dummy.TryGetComponent<RangeBleeding>(out var bleed) || bleed.enabled)
                Debug.LogError("RANGE_SMOKE_BLEED_STACKS_OR_CLEANUP");
            dummy.CurrentHealth = beforeBleed;
            items.Clear();
            Debug.Log("RANGE_SMOKE_BLEED_CHECKED");
            int dagger = RangeResearchItems.CatalogIndex("BleedOnHit");
            for (int i = 0; i < 19; i++) items.Grant(dagger);
            items.Grant(RangeResearchItems.CatalogIndex("BleedOnHitVoid"));
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0, 0f);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0, .5f);
            if (items.Daggers != 0 || dummy.CurrentHealth != beforeBleed - 40)
                Debug.LogError("RANGE_SMOKE_NEEDLETICK_IMMEDIATE");
            yield return new WaitForSeconds(3.1f);
            float needleAttack = owner.RainAttackPower > 0f ? owner.RainAttackPower : CharacterInstance.RainBaseAttackPower;
            if (dummy.CurrentHealth != beforeBleed - 40 - Mathf.RoundToInt(needleAttack * 4f))
                Debug.LogError("RANGE_SMOKE_NEEDLETICK_COLLAPSE");
            items.Clear();
            dummy.CurrentHealth = beforeBleed;
            Debug.Log("RANGE_SMOKE_NEEDLETICK_CHECKED");
            yield return VerifyShatterspleen(owner, dummy, items);
            yield return VerifyHealingSkull(owner, dummy, items);
            yield return VerifyLittleDisciple(owner, dummy, items);
            int genesis = RangeResearchItems.CatalogIndex("NovaOnLowHealth");
            int genesisHealth = owner.CurrentHealth;
            int genesisTargetHealth = dummy.CurrentHealth;
            Vector3 genesisTargetPosition = dummy.transform.position;
            dummy.transform.position = owner.transform.position + Vector3.right * 6f;
            items.Grant(genesis);
            owner.CurrentHealth = owner.MaxHealth / 4 + 1;
            owner.Server_ChangeHealthStateRaw(2, CharacterPart.body, AttackType.melee, dummy, 0);
            if (!items.Remove(genesis)) Debug.LogError("RANGE_SMOKE_GENESIS_REMOVE");
            items.Grant(genesis);
            owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, dummy, 0);
            yield return new WaitForSeconds(.1f);
            float genesisAttack = owner.RainAttackPower > 0f ? owner.RainAttackPower : CharacterInstance.RainBaseAttackPower;
            if (dummy.CurrentHealth != genesisTargetHealth - Mathf.RoundToInt(60f * genesisAttack))
                Debug.LogError("RANGE_SMOKE_GENESIS_DAMAGE_OR_COOLDOWN");
            else Debug.Log("RANGE_SMOKE_GENESIS_CHECKED");
            dummy.transform.position = genesisTargetPosition;
            dummy.CurrentHealth = genesisTargetHealth;
            owner.CurrentHealth = genesisHealth;
            items.Clear();
            int stickyIndex = RangeResearchItems.CatalogIndex("StickyBomb");
            for (int i = 0; i < 20; i++) items.Grant(stickyIndex);
            var splash = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                dummy.transform.position + Vector3.forward * 2f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in splash.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            splash.Team = dummy.Team;
            NetworkServer.Spawn(splash.gameObject);
            splash.GetComponent<CharacterMotor>().enabled = false;
            int splashHealth = splash.CurrentHealth;
            int ownerHealth = owner.CurrentHealth;
            var stickyRandom = UnityEngine.Random.state;
            UnityEngine.Random.State partialHitRandom;
            do
            {
                partialHitRandom = UnityEngine.Random.state;
                _ = UnityEngine.Random.value;
            } while (UnityEngine.Random.value < .125f);
            UnityEngine.Random.state = partialHitRandom;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, .125f);
            UnityEngine.Random.state = stickyRandom;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            yield return new WaitForSeconds(1.15f);
            if (dummy.CurrentHealth != beforeBleed - 40 || splash.CurrentHealth != splashHealth)
                Debug.LogError("RANGE_SMOKE_PROC_COEFFICIENT_FAILED");
            else Debug.Log("RANGE_SMOKE_PROC_COEFFICIENT_CHECKED");
            dummy.CurrentHealth = beforeBleed;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (dummy.CurrentHealth != beforeBleed - 20 || splash.CurrentHealth != splashHealth)
                Debug.LogError("RANGE_SMOKE_STICKY_EARLY_BLAST");
            yield return new WaitForSeconds(2.15f);
            if (dummy.CurrentHealth != beforeBleed - 56 || splash.CurrentHealth != splashHealth - 36 ||
                owner.CurrentHealth != ownerHealth)
                Debug.LogError($"RANGE_SMOKE_STICKY_DAMAGE primary={beforeBleed - dummy.CurrentHealth} splash={splashHealth - splash.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_STICKY_BOMB_CHECKED");
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("Behemoth"));
            dummy.CurrentHealth = beforeBleed;
            splash.CurrentHealth = splashHealth;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, .125f);
            if (dummy.CurrentHealth != beforeBleed - 32 || splash.CurrentHealth != splashHealth)
                Debug.LogError("RANGE_SMOKE_BEHEMOTH_PELLET_RADIUS");
            dummy.CurrentHealth = beforeBleed;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (dummy.CurrentHealth != beforeBleed - 32 || splash.CurrentHealth != splashHealth - 12 ||
                owner.CurrentHealth != ownerHealth)
                Debug.LogError("RANGE_SMOKE_BEHEMOTH_DAMAGE");
            else Debug.Log("RANGE_SMOKE_BEHEMOTH_CHECKED");
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("ChainLightning"));
            dummy.CurrentHealth = beforeBleed;
            splash.CurrentHealth = splashHealth;
            var beforeUkuleleRandom = UnityEngine.Random.state;
            UnityEngine.Random.State ukuleleRoll;
            do
            {
                ukuleleRoll = UnityEngine.Random.state;
                _ = UnityEngine.Random.value;
            } while (UnityEngine.Random.value >= .25f);
            UnityEngine.Random.state = ukuleleRoll;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            UnityEngine.Random.state = beforeUkuleleRandom;
            yield return new WaitForSeconds(.45f);
            if (dummy.CurrentHealth != beforeBleed - 20 || splash.CurrentHealth != splashHealth - 16 ||
                owner.CurrentHealth != ownerHealth)
                Debug.LogError("RANGE_SMOKE_UKULELE_CHAIN_FAILED");
            else Debug.Log("RANGE_SMOKE_UKULELE_CHECKED");
            items.Grant(RangeResearchItems.CatalogIndex("ChainLightningVoid"));
            dummy.CurrentHealth = beforeBleed;
            splash.CurrentHealth = splashHealth;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 4f);
            yield return new WaitForSeconds(.9f);
            if (owner.ResearchUkuleles != 0 || dummy.CurrentHealth != beforeBleed - 40 - 6 * 12 ||
                splash.CurrentHealth != splashHealth)
                Debug.LogError("RANGE_SMOKE_POLYLUTE_DAMAGE_OR_CORRUPTION");
            else Debug.Log("RANGE_SMOKE_POLYLUTE_CHECKED");
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("ExplodeOnDeath"));
            items.Grant(RangeResearchItems.CatalogIndex("ExplodeOnDeathVoid"));
            dummy.CurrentHealth = dummy.MaxHealth;
            splash.CurrentHealth = splashHealth;
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            dummy.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            yield return new WaitForSeconds(.1f);
            float flameAttack = owner.RainAttackPower > 0f ? owner.RainAttackPower : CharacterInstance.RainBaseAttackPower;
            int flameDamage = Mathf.RoundToInt(flameAttack * 4.16f);
            if (owner.ResearchWisps != 0 || dummy.CurrentHealth != dummy.MaxHealth - 40 - flameDamage ||
                splash.CurrentHealth != splashHealth - flameDamage)
                Debug.LogError("RANGE_SMOKE_VOIDSENT_FLAME");
            else Debug.Log("RANGE_SMOKE_VOIDSENT_FLAME_CHECKED");
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("ExplodeOnDeath"));
            dummy.CurrentHealth = beforeBleed;
            splash.CurrentHealth = 1;
            int infusion = RangeResearchItems.CatalogIndex("Infusion");
            int infusionBaseHealth = owner.MaxHealth;
            items.Grant(RangeResearchItems.CatalogIndex("MoveSpeedOnKill"));
            items.Grant(infusion);
            items.Grant(infusion);
            var wispCenter = splash.transform.position;
            var nearPosition = dummy.transform.position;
            dummy.transform.position = wispCenter + Vector3.right * 8f;
            splash.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (Mathf.Abs(items.MoveSpeed(4f, 8f, false) - 9f) > .05f)
                Debug.LogError("RANGE_SMOKE_HARPOON_KILL");
            if (owner.MaxHealth != infusionBaseHealth + 2 || !items.Remove(infusion) ||
                !items.Remove(infusion) || owner.MaxHealth != infusionBaseHealth)
                Debug.LogError("RANGE_SMOKE_INFUSION_KILL_OR_REMOVE");
            items.Grant(infusion);
            if (owner.MaxHealth != infusionBaseHealth + 2)
                Debug.LogError("RANGE_SMOKE_INFUSION_REEQUIP");
            if (dummy.CurrentHealth != beforeBleed) Debug.LogError("RANGE_SMOKE_WISP_EARLY_BLAST");
            yield return new WaitForSeconds(.6f);
            float harpoonSpeed = items.MoveSpeed(4f, 8f, false);
            if (harpoonSpeed <= 4f || harpoonSpeed >= 9f)
                Debug.LogError("RANGE_SMOKE_HARPOON_DECAY");
            var harpoonMotor = owner.GetComponent<CharacterMotor>();
            float hostHarpoonSpeed = harpoonMotor.GetMoveSpeed(false);
            harpoonMotor.RangeItems = null;
            float remoteHarpoonSpeed = harpoonMotor.GetMoveSpeed(false);
            harpoonMotor.RangeItems = items;
            if (Mathf.Abs(hostHarpoonSpeed - remoteHarpoonSpeed) > .01f)
                Debug.LogError("RANGE_SMOKE_HARPOON_REPLICATED_SPEED");
            if (dummy.CurrentHealth != beforeBleed - 10 || owner.CurrentHealth != ownerHealth)
                Debug.LogError($"RANGE_SMOKE_WISP_FALLOFF hp={dummy.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_WISP_CHECKED");
            dummy.transform.position = nearPosition;
            if (splash) NetworkServer.Destroy(splash.gameObject);
            dummy.CurrentHealth = beforeBleed;
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("IgniteOnKill"));
            items.Grant(RangeResearchItems.CatalogIndex("Tooth"));
            var fuelVictim = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                dummy.transform.position + Vector3.forward * 2f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in fuelVictim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            fuelVictim.Team = dummy.Team;
            fuelVictim.MaxHealth = fuelVictim.CurrentHealth = 1;
            NetworkServer.Spawn(fuelVictim.gameObject);
            fuelVictim.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.hitscan, owner, 0);
            yield return new WaitForSeconds(.1f);
            if (dummy.CurrentHealth != beforeBleed - 18) Debug.LogError("RANGE_SMOKE_GASOLINE_BLAST_FAILED");
            yield return new WaitForSeconds(3.1f);
            if (dummy.CurrentHealth != beforeBleed - 36 || owner.CurrentHealth != ownerHealth)
                Debug.LogError($"RANGE_SMOKE_GASOLINE_BURN_FAILED hp={dummy.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_GASOLINE_CHECKED");
            var toothPickup = GameManager.Gamemode.transform.Find("Monster tooth healing pickup");
            if (!toothPickup) Debug.LogError("RANGE_SMOKE_TOOTH_DROP_MISSING");
            else
            {
                var previousPosition = owner.transform.position;
                owner.CurrentHealth = owner.MaxHealth - 20;
                int expectedHealth = owner.CurrentHealth + 8 + Mathf.RoundToInt(owner.MaxHealth * .02f);
                owner.transform.position = toothPickup.position;
                yield return null;
                yield return null;
                if (owner.CurrentHealth != expectedHealth || toothPickup)
                    Debug.LogError("RANGE_SMOKE_TOOTH_PICKUP_FAILED");
                else Debug.Log("RANGE_SMOKE_TOOTH_PICKUP_CHECKED");
                owner.transform.position = previousPosition;
                owner.CurrentHealth = ownerHealth;
            }
            if (fuelVictim) NetworkServer.Destroy(fuelVictim.gameObject);
            dummy.CurrentHealth = beforeBleed;
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("IceRing"));
            items.Grant(RangeResearchItems.CatalogIndex("FireRing"));
            dummy.Server_ChangeHealthStateRaw(47, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (dummy.CurrentHealth != beforeBleed - 47) Debug.LogError("RANGE_SMOKE_ICE_RING_THRESHOLD");
            dummy.Server_ChangeHealthStateRaw(48, CharacterPart.body, AttackType.hitscan, owner, 0);
            var chilledMotor = dummy.GetComponent<CharacterMotor>();
            if (dummy.CurrentHealth != beforeBleed - 215 ||
                !Mathf.Approximately(chilledMotor.GetMoveSpeed(false), chilledMotor.WalkSpeed / 1.8f))
                Debug.LogError($"RANGE_SMOKE_ICE_RING_DAMAGE_OR_SLOW hp={dummy.CurrentHealth} speed={chilledMotor.GetMoveSpeed(false)}");
            dummy.Server_ChangeHealthStateRaw(48, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (dummy.CurrentHealth != beforeBleed - 263) Debug.LogError("RANGE_SMOKE_ICE_RING_COOLDOWN");
            else Debug.Log("RANGE_SMOKE_ICE_RING_CHECKED");
            yield return new WaitForSeconds(.6f);
            if (dummy.CurrentHealth != beforeBleed - 287) Debug.LogError("RANGE_SMOKE_FIRE_RING_FIRST_TICK");
            yield return new WaitForSeconds(2.65f);
            if (dummy.CurrentHealth != beforeBleed - 407 || owner.CurrentHealth != ownerHealth)
                Debug.LogError($"RANGE_SMOKE_FIRE_RING_DAMAGE_OR_COOLDOWN hp={dummy.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_FIRE_RING_CHECKED");
            dummy.CurrentHealth = beforeBleed;
            dummy.ResearchIceSlowUntil = 0;
            items.Clear();
            int baubleIndex = RangeResearchItems.CatalogIndex("SlowOnHit");
            items.Grant(baubleIndex);
            items.Grant(baubleIndex);
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            if (dummy.ResearchBaubleSlowUntil > NetworkTime.time)
                Debug.LogError("RANGE_SMOKE_BAUBLE_ZERO_PROC");
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, .125f);
            double baubleDuration = dummy.ResearchBaubleSlowUntil - NetworkTime.time;
            if (baubleDuration < 3.9 || baubleDuration > 4.01 ||
                !Mathf.Approximately(chilledMotor.GetMoveSpeed(false), chilledMotor.WalkSpeed / 1.6f))
                Debug.LogError("RANGE_SMOKE_BAUBLE_STACK_DURATION");
            dummy.ResearchIceSlowUntil = NetworkTime.time + 2;
            if (!Mathf.Approximately(chilledMotor.GetMoveSpeed(false), chilledMotor.WalkSpeed / 2.4f))
                Debug.LogError("RANGE_SMOKE_BAUBLE_ICE_COMBINATION");
            yield return new WaitForSeconds(4.15f);
            if (!Mathf.Approximately(chilledMotor.GetMoveSpeed(false), chilledMotor.WalkSpeed))
                Debug.LogError("RANGE_SMOKE_BAUBLE_EXPIRY");
            else Debug.Log("RANGE_SMOKE_BAUBLE_CHECKED");
            dummy.CurrentHealth = beforeBleed;
            items.Clear();
            items.Grant(baubleIndex);
            items.Grant(RangeResearchItems.CatalogIndex("SlowOnHitVoid"));
            dummy.ResearchBaubleSlowUntil = dummy.ResearchIceSlowUntil = 0d;
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            if (dummy.ResearchRooted) Debug.LogError("RANGE_SMOKE_TENTABAUBLE_ZERO_PROC");
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 20f);
            double rootDuration = dummy.ResearchRootUntil - NetworkTime.time;
            if (owner.ResearchBaubles != 0 || rootDuration < 1.9 || rootDuration > 2.01 ||
                dummy.GetComponent<CharacterMotor>().GetMoveSpeed(false) != 0f)
                Debug.LogError("RANGE_SMOKE_TENTABAUBLE_ROOT");
            dummy.ResearchRootUntil = 0d;
            dummy.CurrentHealth = beforeBleed;
            items.Clear();
            int seedIndex = RangeResearchItems.CatalogIndex("Seed");
            items.Grant(seedIndex);
            items.Grant(seedIndex);
            owner.CurrentHealth = owner.MaxHealth - 10;
            int beforeSeed = owner.CurrentHealth;
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, .25f);
            if (owner.CurrentHealth != beforeSeed) Debug.LogError("RANGE_SMOKE_SEED_EARLY_HEAL");
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, .25f);
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (owner.CurrentHealth != beforeSeed + 3) Debug.LogError("RANGE_SMOKE_SEED_STACK_OR_PROC");
            items.Remove(seedIndex);
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0);
            items.Remove(seedIndex);
            dummy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (owner.CurrentHealth != beforeSeed + 4) Debug.LogError("RANGE_SMOKE_SEED_REMOVE");
            else Debug.Log("RANGE_SMOKE_SEED_CHECKED");
            owner.CurrentHealth = ownerHealth;
            dummy.CurrentHealth = beforeBleed;
            int shieldIndex = RangeResearchItems.CatalogIndex("PersonalShield");
            items.Grant(shieldIndex);
            items.Grant(shieldIndex);
            int shieldCapacity = Mathf.RoundToInt(owner.MaxHealth * .16f);
            int healthBeforeShield = owner.CurrentHealth;
            if (owner.ResearchShield != shieldCapacity) Debug.LogError("RANGE_SMOKE_PERSONAL_SHIELD_CAPACITY");
            owner.Server_ChangeHealthStateRaw(shieldCapacity + 2, CharacterPart.body, AttackType.hitscan, dummy, 0);
            if (owner.ResearchShield != 0 || owner.CurrentHealth != healthBeforeShield - 2)
                Debug.LogError("RANGE_SMOKE_PERSONAL_SHIELD_ABSORPTION");
            yield return new WaitForSeconds(6.8f);
            if (owner.ResearchShield != 0) Debug.LogError("RANGE_SMOKE_PERSONAL_SHIELD_RECHARGED_EARLY");
            yield return new WaitForSeconds(2.45f);
            if (owner.ResearchShield != shieldCapacity || !items.Remove(shieldIndex) ||
                owner.ResearchShield != Mathf.RoundToInt(owner.MaxHealth * .08f))
                Debug.LogError("RANGE_SMOKE_PERSONAL_SHIELD_RECHARGE_OR_REMOVAL");
            else Debug.Log("RANGE_SMOKE_PERSONAL_SHIELD_CHECKED");
            owner.CurrentHealth = healthBeforeShield;
            items.Clear();
            int bucklerIndex = RangeResearchItems.CatalogIndex("SprintArmor");
            items.Grant(bucklerIndex);
            items.Grant(bucklerIndex);
            owner.CurrentHealth = owner.MaxHealth;
            owner.IsRunning = false;
            owner.Server_ChangeHealthStateRaw(80, CharacterPart.body, AttackType.hitscan, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth - 80) Debug.LogError("RANGE_SMOKE_BUCKLER_NOT_SPRINTING");
            owner.CurrentHealth = owner.MaxHealth;
            owner.IsRunning = true;
            items.Grant(RangeResearchItems.CatalogIndex("ArmorPlate"));
            owner.Server_ChangeHealthStateRaw(80, CharacterPart.body, AttackType.hitscan, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth - 45) Debug.LogError("RANGE_SMOKE_BUCKLER_ARMOR_ORDER");
            else Debug.Log("RANGE_SMOKE_BUCKLER_CHECKED");
            owner.IsRunning = false;
            owner.CurrentHealth = healthBeforeShield;
            items.Clear();
            int opalIndex = RangeResearchItems.CatalogIndex("OutOfCombatArmor");
            items.Grant(opalIndex);
            items.Grant(opalIndex);
            owner.CurrentHealth = owner.MaxHealth;
            owner.Server_ChangeHealthStateRaw(60, CharacterPart.body, AttackType.hitscan, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth - 20) Debug.LogError("RANGE_SMOKE_OPAL_FIRST_HIT");
            owner.Server_ChangeHealthStateRaw(60, CharacterPart.body, AttackType.hitscan, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth - 80) Debug.LogError("RANGE_SMOKE_OPAL_FOLLOWUP");
            yield return new WaitForSeconds(7.1f);
            owner.CurrentHealth = owner.MaxHealth;
            owner.Server_ChangeHealthStateRaw(60, CharacterPart.body, AttackType.hitscan, dummy, 0);
            if (owner.CurrentHealth != owner.MaxHealth - 20) Debug.LogError("RANGE_SMOKE_OPAL_RECOVERY");
            else Debug.Log("RANGE_SMOKE_OPAL_CHECKED");
            owner.CurrentHealth = healthBeforeShield;
            items.Clear();
            items.Grant(RangeResearchItems.CatalogIndex("BarrierOnOverHeal"));
            owner.CurrentHealth = owner.MaxHealth - 10;
            if (owner.ServerHeal(30, owner.netId) != 10 || !Mathf.Approximately(items.Barrier, 10f))
                Debug.LogError("RANGE_SMOKE_AEGIS_PARTIAL_OVERHEAL");
            owner.ServerHeal(20, owner.netId, allowOverhealBarrier: false);
            if (!Mathf.Approximately(items.Barrier, 10f)) Debug.LogError("RANGE_SMOKE_AEGIS_REGEN");
            items.Grant(RangeResearchItems.CatalogIndex("IncreaseHealing"));
            if (owner.ServerHeal(10, owner.netId) != 0 || !Mathf.Approximately(items.Barrier, 20f))
                Debug.LogError("RANGE_SMOKE_AEGIS_FULL_HEALTH");
            items.Clear();
            Debug.Log("RANGE_SMOKE_AEGIS_CHECKED");
            int potionIndex = RangeResearchItems.CatalogIndex("HealingPotion");
            items.Grant(potionIndex);
            items.Grant(potionIndex);
            items.Grant(RangeResearchItems.CatalogIndex("FragileDamageBonus"));
            owner.CurrentHealth = 30;
            owner.Server_ChangeHealthStateRaw(5,CharacterPart.body,AttackType.melee,dummy,0);
            if (owner.CurrentHealth != 100 || items.HealingPotions != 1 ||
                items.EmptyBottles != 1 || items.Watches != 1)
                Debug.LogError("RANGE_SMOKE_POTION_TRANSFORM_OR_WATCH_ORDER");
            owner.Server_ChangeHealthStateRaw(5,CharacterPart.body,AttackType.melee,dummy,0);
            if (owner.CurrentHealth != 95 || items.HealingPotions != 1)
                Debug.LogError("RANGE_SMOKE_POTION_HIGH_HEALTH");
            items.Clear();
            Debug.Log("RANGE_SMOKE_POTION_CHECKED");
            items.Grant(RangeResearchItems.CatalogIndex("IncreaseHealing"));
            items.Grant(RangeResearchItems.CatalogIndex("HealWhileSafe"));
            owner.CurrentHealth = 40;
            if (owner.ServerHeal(5, owner.netId) != 10 || owner.CurrentHealth != 50)
                Debug.LogError("RANGE_SMOKE_HEALING_RACK");
            owner.Server_ChangeHealthStateRaw(5,CharacterPart.body,AttackType.melee,dummy,0);
            yield return new WaitForSeconds(7.2f);
            if (owner.CurrentHealth != 45) Debug.LogError("RANGE_SMOKE_SLUG_EARLY_TICK");
            yield return new WaitForSeconds(1.05f);
            if (owner.CurrentHealth != 51) Debug.LogError("RANGE_SMOKE_SLUG_HEAL");
            int stewForSlug = RangeResearchItems.CatalogIndex("Stew");
            items.Grant(stewForSlug);
            owner.CurrentHealth = owner.MaxHealth;
            float safeAttack = owner.EffectiveRainAttackPower;
            items.OnHealthDamaged(1);
            float unsafeAttack = owner.EffectiveRainAttackPower;
            float regenLevelScale = owner.RainAttackPower > 0f ? owner.RainAttackPower / CharacterInstance.RainBaseAttackPower : 1f;
            if (Mathf.Approximately(safeAttack - unsafeAttack, 3f * regenLevelScale) && owner.ResearchActiveSlugs == 0)
                Debug.Log("RANGE_SMOKE_STEW_SLUG_CHECKED");
            else Debug.LogError("RANGE_SMOKE_STEW_SLUG");
            items.Clear();
            Debug.Log("RANGE_SMOKE_RACK_SLUG_CHECKED");
            items.Grant(RangeResearchItems.CatalogIndex("ParentEgg"));
            owner.CurrentHealth = 10;
            owner.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,dummy,0);
            if (owner.CurrentHealth != 5) Debug.LogError("RANGE_SMOKE_PARENT_EGG_PREHEAL");
            items.Clear();
            int watchIndex = RangeResearchItems.CatalogIndex("FragileDamageBonus");
            items.Grant(watchIndex);
            items.Grant(watchIndex);
            int dummyHealth = dummy.CurrentHealth;
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            if (dummy.CurrentHealth != dummyHealth - 28) Debug.LogError("RANGE_SMOKE_WATCH_DAMAGE");
            owner.CurrentHealth = Mathf.CeilToInt(owner.MaxHealth * .25f) + 5;
            owner.Server_ChangeHealthStateRaw(10,CharacterPart.body,AttackType.melee,dummy,0);
            if (items.Watches != 0 || items.BrokenWatches != 2) Debug.LogError("RANGE_SMOKE_WATCH_TRANSFORM");
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            if (dummy.CurrentHealth != dummyHealth - 48) Debug.LogError("RANGE_SMOKE_BROKEN_WATCH_DAMAGE");
            dummy.CurrentHealth = dummyHealth;
            items.Clear();
            Debug.Log("RANGE_SMOKE_EGG_WATCH_CHECKED");
            var motor=owner.GetComponent<CharacterMotor>();
            int hoof=RangeResearchItems.CatalogIndex("Hoof");
            int soda=RangeResearchItems.CatalogIndex("SprintBonus");
            items.Grant(hoof);
            items.Grant(soda);
            items.Grant(soda);
            if (!Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.14f) ||
                !Mathf.Approximately(motor.GetMoveSpeed(true), motor.RunSpeed * 1.14f + motor.WalkSpeed * .5f))
                Debug.LogError("RANGE_SMOKE_MOVEMENT_ITEM_SCALING");
            if (!items.Remove(hoof) || !items.Remove(soda) ||
                !Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed) ||
                !Mathf.Approximately(motor.GetMoveSpeed(true), motor.RunSpeed + motor.WalkSpeed * .25f))
                Debug.LogError("RANGE_SMOKE_MOVEMENT_ITEM_REMOVAL");
            items.Clear();
            if (!Mathf.Approximately(motor.GetMoveSpeed(true), motor.RunSpeed))
                Debug.LogError("RANGE_SMOKE_MOVEMENT_ITEM_CLEAR");
            Debug.Log("RANGE_SMOKE_MOVEMENT_ITEMS_CHECKED");
            int crystalIndex = RangeResearchItems.CatalogIndex("NearbyDamageBonus");
            items.Grant(crystalIndex);
            items.Grant(crystalIndex);
            var crystalTargetPosition = dummy.transform.position;
            dummy.transform.position = owner.transform.position + Vector3.right * 12.9f;
            dummy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (dummy.CurrentHealth != 99860) Debug.LogError("RANGE_SMOKE_FOCUS_CRYSTAL_NEAR");
            dummy.CurrentHealth = 100000;
            dummy.transform.position = owner.transform.position + Vector3.right * 13.1f;
            dummy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (dummy.CurrentHealth != 99900) Debug.LogError("RANGE_SMOKE_FOCUS_CRYSTAL_FAR");
            dummy.CurrentHealth = 100000;
            dummy.transform.position = crystalTargetPosition;
            items.Clear();
            Debug.Log("RANGE_SMOKE_FOCUS_CRYSTAL_CHECKED");
            items.Grant(RangeResearchItems.CatalogIndex("Crowbar"));
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            if(dummy.CurrentHealth!=99965)Debug.LogError("RANGE_SMOKE_CROWBAR_HIGH_HP");
            dummy.CurrentHealth=89999;
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            if(dummy.CurrentHealth!=89979)Debug.LogError("RANGE_SMOKE_CROWBAR_LOW_HP");
            dummy.CurrentHealth=100000;
            items.Clear();
            for(int i=0;i<10;i++)items.Grant(RangeResearchItems.CatalogIndex("Bear"));
            var randomState=UnityEngine.Random.state;
            UnityEngine.Random.InitState(17);
            int blocked=0;
            for(int i=0;i<32;i++)
            {
                owner.CurrentHealth=60;
                owner.Server_ChangeHealthStateRaw(1,CharacterPart.body,AttackType.melee,dummy,0);
                if(owner.CurrentHealth==60)blocked++;
            }
            UnityEngine.Random.state=randomState;
            if(blocked==0||blocked==32)Debug.LogError("RANGE_SMOKE_BEAR_BLOCK "+blocked);
            items.Clear();
            Debug.Log("RANGE_SMOKE_CROWBAR_BEAR_CHECKED "+blocked);
            items.Grant(RangeResearchItems.CatalogIndex("ArmorPlate"));
            items.Grant(RangeResearchItems.CatalogIndex("Medkit"));
            owner.CurrentHealth=60;
            owner.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,dummy,0);
            if(owner.CurrentHealth!=45)Debug.LogError("RANGE_SMOKE_ARMOR_DAMAGE "+owner.CurrentHealth);
            yield return new WaitForSeconds(2.2f);
            if(owner.CurrentHealth!=45+20+Mathf.RoundToInt(owner.MaxHealth*.05f))
                Debug.LogError("RANGE_SMOKE_MEDKIT_HEAL "+owner.CurrentHealth);
            items.Clear();
            var buffs=owner.gameObject.AddComponent<RangeTestBuffs>();
            owner.CurrentHealth=40;
            buffs.Apply(RangeTestBuffs.Lifesteal);
            dummy.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,owner,0);
            if(owner.CurrentHealth!=44)Debug.LogError("RANGE_SMOKE_LIFESTEAL "+owner.CurrentHealth);
            buffs.Remove(RangeTestBuffs.Lifesteal);
            buffs.Apply(RangeTestBuffs.Regeneration);
            yield return new WaitForSeconds(1.1f);
            if(owner.CurrentHealth!=49)Debug.LogError("RANGE_SMOKE_REGEN "+owner.CurrentHealth);
            buffs.Remove(RangeTestBuffs.Regeneration);
            buffs.Apply(RangeTestBuffs.DamageReduction);
            buffs.Apply(RangeTestBuffs.DamageReduction);
            items.Grant(RangeResearchItems.CatalogIndex("ArmorPlate"));
            owner.CurrentHealth = 60;
            owner.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,dummy,0);
            if (owner.CurrentHealth != 50) Debug.LogError("RANGE_SMOKE_REDUCTION_ARMOR_ORDER");
            buffs.Remove(RangeTestBuffs.DamageReduction);
            owner.Server_ChangeHealthStateRaw(20,CharacterPart.body,AttackType.melee,dummy,0);
            if (owner.CurrentHealth != 35) Debug.LogError("RANGE_SMOKE_REDUCTION_REMOVAL");
            items.Clear();
            Debug.Log("RANGE_SMOKE_REDUCTION_CHECKED");
            var titan=Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),owner.transform.position+Vector3.right*8,Quaternion.identity).GetComponent<CharacterInstance>();
            foreach(var slot in titan.CharacterItemManager.Slots)slot.ItemOnSpawn=null;
            titan.Team=dummy.Team;
            NetworkServer.Spawn(titan.gameObject);
            titan.SetAsBOT(true);
            var actor=titan.gameObject.AddComponent<RangeResearchEnemy>();
            actor.Configure(owner,2);
            actor.OwnsCharacter=true;
            yield return null;
            items.Grant(RangeResearchItems.CatalogIndex("BossDamageBonus"));
            titan.Server_ChangeHealthStateRaw(100,CharacterPart.body,AttackType.melee,owner,0);
            if(titan.CurrentHealth!=1380)Debug.LogError("RANGE_SMOKE_BOSS_DAMAGE "+titan.CurrentHealth);
            uint titanId=titan.netId;
            items.Grant(RangeResearchItems.CatalogIndex("BarrierOnKill"));
            items.Grant(RangeResearchItems.CatalogIndex("BarrierOnKill"));
            titan.Server_ChangeHealthStateRaw(1500,CharacterPart.body,AttackType.melee,owner,0);
            if (!Mathf.Approximately(items.Barrier, 30f)) Debug.LogError("RANGE_SMOKE_BROOCH_GRANT");
            int healthBeforeBarrierHit = owner.CurrentHealth;
            owner.Server_ChangeHealthStateRaw(10,CharacterPart.body,AttackType.melee,dummy,0);
            if (owner.CurrentHealth != healthBeforeBarrierHit || !Mathf.Approximately(items.Barrier, 20f))
                Debug.LogError("RANGE_SMOKE_BROOCH_ABSORB");
            yield return new WaitForSeconds(.25f);
            if (items.Barrier >= 20f || items.Barrier <= 0f) Debug.LogError("RANGE_SMOKE_BROOCH_DECAY");
            Debug.Log("RANGE_SMOKE_BROOCH_CHECKED");
            if(titan||NetworkServer.spawned.ContainsKey(titanId))Debug.LogError("RANGE_SMOKE_SUMMON_DEATH_LEAK");
            items.Clear();
            Debug.Log("RANGE_SMOKE_ITEM_BUFF_DEATH_CHECKED");
            var healer = RangeDrone.FindSpec("Drone2");
            RangeDrone.Recruit(owner, healer);
            var healerActor = FindFirstObjectByType<RangeDrone>();
            var healerBody = healerActor ? healerActor.GetComponent<CharacterInstance>() : null;
            if (!healerBody || healerBody.RainDroneOwner != owner.netId || healerBody.Team != owner.Team ||
                healerBody.MaxHealth != 150 || !healerBody.transform.Find("Drone hitbox") ||
                RangeDrone.PreferCloserAlly(healerBody.transform.position, owner) != healerBody)
                Debug.LogError("RANGE_SMOKE_DRONE_ALLY_SETUP");
            if (healerBody)
            {
                healerBody.Server_ChangeHealthStateRaw(35, CharacterPart.body, AttackType.hitscan, owner, 0);
                if (healerBody.CurrentHealth != 150) Debug.LogError("RANGE_SMOKE_DRONE_FRIENDLY_DAMAGE");
                healerBody.Server_ChangeHealthStateRaw(35, CharacterPart.body, AttackType.hitscan, dummy, 0);
                if (healerBody.CurrentHealth != 115) Debug.LogError("RANGE_SMOKE_DRONE_HOSTILE_DAMAGE");
                yield return new WaitForSeconds(.3f);
                var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
                var allies = overlay ? overlay.Find("Allied drones") : null;
                if (!allies || !allies.GetComponentsInChildren<TMPro.TMP_Text>().Any(t => t.text.Contains("115/150")))
                    Debug.LogError("RANGE_SMOKE_DRONE_ALLY_HUD");
                uint healerId = healerBody.netId;
                healerBody.Server_ChangeHealthStateRaw(115, CharacterPart.body, AttackType.hitscan, dummy, 0);
                yield return new WaitForSeconds(.3f);
                if (NetworkServer.spawned.ContainsKey(healerId) || (allies && allies.gameObject.activeSelf))
                    Debug.LogError("RANGE_SMOKE_DRONE_DEATH_CLEANUP");
            }
            Debug.Log("RANGE_SMOKE_DRONE_ALLY_CHECKED");
            var backup = RangeResearchItems.Equipment.DroneBackup;
            bool backupActivated = items.Equip(backup) && player.ServerTryUseRangeEquipment();
            var backupDrones = FindObjectsByType<RangeDrone>(FindObjectsSortMode.None);
            if (!backupActivated || backupDrones.Length != 4 ||
                backupDrones.Any(drone => drone.Id != "BackupDrone" ||
                    drone.GetComponent<CharacterInstance>().RainDroneOwner != owner.netId) ||
                items.EquipmentCooldownRemaining < 99.9f || !items.Remove(backup) ||
                !items.Equip(backup) || player.ServerTryUseRangeEquipment())
                Debug.LogError("RANGE_SMOKE_DRONE_BACKUP_DEPLOYMENT");
            yield return new WaitForSeconds(25.1f);
            if (backupDrones.Any(drone => drone) || items.EquipmentCooldownRemaining <= 0f)
                Debug.LogError("RANGE_SMOKE_DRONE_BACKUP_EXPIRY");
            else if (backupActivated && backupDrones.Length == 4)
                Debug.Log("RANGE_SMOKE_DRONE_BACKUP_CHECKED");
            items.Remove(backup);
            owner.CurrentHealth=40;
            foreach(var spec in DroneCatalog.Load().units)
            {
                Debug.Log("RANGE_SMOKE_RECRUIT "+spec.id+" "+RangeDrone.Recruit(owner,spec));
                yield return new WaitForSeconds(.15f);
            }
            if(FindObjectsByType<RangeDrone>(FindObjectsSortMode.None).Length!=21)Debug.LogError("RANGE_SMOKE_FLEET_COUNT");
            yield return new WaitForSeconds(10);
            RangeDrone.CommandAll();
            yield return new WaitForSeconds(5);
            if(dummy.CurrentHealth>=100000)Debug.LogError("RANGE_SMOKE_NO_DAMAGE");
            if(owner.CurrentHealth<=40)Debug.LogError("RANGE_SMOKE_NO_HEAL");
            Debug.Log("RANGE_SMOKE_DAMAGE="+(100000-dummy.CurrentHealth)+" HEALED_TO="+owner.CurrentHealth);
            foreach(var spec in DroneCatalog.Load().units){RangeDrone.Recall(spec.id);yield return null;}
            yield return new WaitForSeconds(1);
            if(FindObjectsByType<RangeDrone>(FindObjectsSortMode.None).Length!=0)Debug.LogError("RANGE_SMOKE_RECALL_LEAK");
            NetworkServer.Destroy(dummy.gameObject);
            Debug.Log(failed?"RANGE_SMOKE_FAILED":"RANGE_SMOKE_PASSED");
            Application.Quit(failed?1:0);
        }
        void FinishPlaytest(int exitCode)
        {
#if UNITY_EDITOR
            if ((itemPlaytest || monsterRecoveryPlaytest) && Application.isBatchMode)
            {
                UnityEditor.EditorApplication.Exit(exitCode);
                return;
            }
#endif
            Application.Quit(exitCode);
        }

        IEnumerator VerifyPriorityItems(CharacterInstance owner)
        {
            var lab = GameManager.Gamemode.GetComponent<RangeResearchLab>();
            var items = owner.GetComponent<RangeResearchItems>();
            if (!lab || !items)
            {
                Debug.LogError("RAIN_ITEM_PLAYTEST_MISSING_GM_OR_INVENTORY");
                yield break;
            }

            const string filterOption = "-rainItemId=";
            string filter = Array.Find(Environment.GetCommandLineArgs(), arg => arg.StartsWith(filterOption, StringComparison.Ordinal));
            string selectedId = filter?.Substring(filterOption.Length);
            int tested = 0;
            foreach (var (id, test) in new (string, Func<IEnumerator>)[]
            {
                ("AlienHead", () => VerifyCooldownItem(lab, owner, items, lightFlux: false)),
                ("HalfAttackSpeedHalfCooldowns", () => VerifyCooldownItem(lab, owner, items, lightFlux: true)),
                ("PowerOrbSphere", () => VerifyPowerOrb(lab, owner, items)),
                ("PowerCube", () => VerifyPowerCube(lab, owner, items)),
                ("ArmorPlate", () => VerifyArmorPlate(lab, owner, items)),
                ("ArmorReductionOnHit", () => VerifyShatteringJustice(lab, owner, items)),
                ("AttackSpeedOnCrit", () => VerifyPredatoryInstincts(lab, owner, items)),
                ("AutoCastEquipment", () => VerifyGestureOfTheDrowned(lab, owner, items)),
                ("BarrierOnKill", () => VerifyTopazBrooch(lab, owner, items)),
                ("BarrierOnOverHeal", () => VerifyAegis(lab, owner, items)),
                ("Bear", () => VerifyTougherTimes(lab, owner, items)),
                ("BearVoid", () => VerifySaferSpaces(lab, owner, items)),
                ("MushroomVoid", () => VerifyWeepingFungus(lab, owner, items)),
                ("Mushroom", () => VerifyBustlingFungus(lab, owner, items)),
                ("Medkit", () => VerifyMedkit(lab, owner, items)),
                ("HealWhileSafe", () => VerifyCautiousSlug(lab, owner, items)),
                ("IncreaseHealing", () => VerifyRejuvenationRack(lab, owner, items)),
                ("ParentEgg", () => VerifyParentEgg(lab, owner, items)),
                ("OutOfCombatArmor", () => VerifyOddlyShapedOpal(lab, owner, items)),
                ("SprintArmor", () => VerifyRoseBuckler(lab, owner, items)),
                ("PersonalShield", () => VerifyPersonalShield(lab, owner, items)),
                ("MissileVoid", () => VerifyPlasmaShrimp(lab, owner, items)),
                ("Missile", () => VerifyAtg(lab, owner, items)),
                ("MoreMissile", () => VerifyPocketIcbm(lab, owner, items)),
                ("Tooth", () => VerifyMonsterTooth(lab, owner, items)),
                ("MoveSpeedOnKill", () => VerifyHarpoon(lab, owner, items)),
                ("Infusion", () => VerifyInfusion(lab, owner, items)),
                ("Behemoth", () => VerifyBrilliantBehemoth(lab, owner, items)),
                ("BleedOnHit", () => VerifyTriTipDagger(lab, owner, items)),
                ("BleedOnHitVoid", () => VerifyNeedletick(lab, owner, items)),
                ("BleedOnHitAndExplode", () => VerifyShatterspleen(lab, owner, items)),
                ("BonusGoldPackOnKill", () => VerifyGhorTomePlaytest(lab, owner, items)),
                ("BossDamageBonus", () => VerifyArmorPiercingRounds(lab, owner, items)),
                ("ChainLightning", () => VerifyUkulele(lab, owner, items)),
                ("ChainLightningVoid", () => VerifyPolylute(lab, owner, items)),
                ("DeathMark", () => VerifyDeathMark(lab, owner, items)),
                ("CritGlasses", () => VerifyLensMakerGlasses(lab, owner, items)),
                ("CritGlassesVoid", () => VerifyLostSeers(lab, owner, items)),
                ("SlowOnHitVoid", () => VerifyTentabauble(lab, owner, items)),
                ("SlowOnHit", () => VerifyChronobauble(lab, owner, items)),
                ("HealOnCrit", () => VerifyHarvestersScythe(lab, owner, items)),
                ("Seed", () => VerifyLeechingSeed(lab, owner, items)),
                ("IncreaseDamageOnMultiKill", () => VerifyChronicExpansion(lab, owner, items)),
                ("PermanentDebuffOnHit", () => VerifySymbioticScorpion(lab, owner, items)),
                ("Crowbar", () => VerifyCrowbar(lab, owner, items)),
                ("Clover", () => VerifyClover(lab, owner, items)),
                ("EnergizedOnEquipmentUse", () => VerifyWarHorn(lab, owner, items)),
                ("EquipmentMagazine", () => VerifyFuelCells(lab, owner, items)),
                ("Talisman", () => VerifySoulboundCatalyst(lab, owner, items)),
                ("Fruit", () => VerifyForeignFruit(lab, owner, items)),
                ("GainArmor", () => VerifyJadeElephant(lab, owner, items)),
                ("CritOnUse", () => VerifyOcularHud(lab, owner, items)),
                ("IceRing", () => VerifyIceRing(lab, owner, items)),
                ("FireRing", () => VerifyFireRing(lab, owner, items)),
                ("Dagger", () => VerifyCeremonialDagger(lab, owner, items)),
                ("ExplodeOnDeath", () => VerifyWispItem(lab, owner, items)),
                ("IgniteOnKill", () => VerifyGasoline(lab, owner, items)),
                ("StickyBomb", () => VerifyStickyBomb(lab, owner, items)),
                ("StunChanceOnHit", () => VerifyStunGrenade(lab, owner, items)),
                ("Syringe", () => VerifyFireRateItem(lab, owner, items, false)),
                ("AttackSpeedAndMoveSpeed", () => VerifyMocha(lab, owner, items)),
                ("NearbyDamageBonus", () => VerifyFocusCrystal(lab, owner, items)),
                ("SprintBonus", () => VerifyMovementItem(lab, owner, items, "SprintBonus")),
                ("Hoof", () => VerifyMovementItem(lab, owner, items, "Hoof")),
                ("HalfSpeedDoubleHealth", () => VerifyMovementItem(lab, owner, items, "HalfSpeedDoubleHealth")),
                ("FlatHealth", () => VerifyMaximumHealthItem(lab, owner, items, "FlatHealth")),
                ("Pearl", () => VerifyMaximumHealthItem(lab, owner, items, "Pearl")),
                ("LunarDagger", () => VerifyShapedGlass(lab, owner, items)),
                ("FragileDamageBonus", () => VerifyDelicateWatch(lab, owner, items)),
                ("HealingPotion", () => VerifyHealingPotion(lab, owner, items)),
                ("CookedSteak", () => VerifyMaximumHealthItem(lab, owner, items, "CookedSteak")),
                ("ExplodeOnDeathVoid", () => VerifyVoidsentFlame(lab, owner, items)),
                ("AttackSpeedPerNearbyAllyOrEnemy", () => VerifyBolsteringLantern(owner, items)),
                ("SpeedBoostPickup", () => VerifyElusiveAntlers(owner, items)),
                ("SpeedOnPickup", () => VerifyCollectorPickups(owner, items)),
                ("Lightning", () => VerifyCapacitorPlaytest(lab, owner, items))
            })
            {
                if (selectedId != null && selectedId != id) continue;
                tested++;
                Debug.Log("RAIN_ITEM_PLAYTEST_BEGIN " + id);
                int before = loggedErrors;
                yield return test();
                Debug.Log((loggedErrors == before ? "RAIN_ITEM_PLAYTEST_PASS " : "RAIN_ITEM_PLAYTEST_SKIP ") + id);
            }
            if (tested == 0) Debug.LogError("RAIN_ITEM_PLAYTEST_UNKNOWN_ID " + selectedId);
        }

        static IEnumerator VerifyCooldownItem(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items, bool lightFlux)
        {
            string id = lightFlux ? "HalfAttackSpeedHalfCooldowns" : "AlienHead";
            var entry = new BrowserEntry { id = id, name = lightFlux ? "光通量肩甲" : "外星脑袋" };
            items.Clear();
            var victim = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, "skeleton_1h_sur");
            if (!victim)
            {
                Debug.LogError("RAIN_ITEM_COOLDOWN_NO_TARGET " + id);
                yield break;
            }
            victim.transform.position = owner.transform.position + owner.transform.forward * 3f;
            victim.MaxHealth = victim.CurrentHealth = 1000;
            Physics.SyncTransforms();
            var intervals = new float[2];
            float baselineSpeed = owner.ResearchAttackSpeed;
            for (int i = 0; i < intervals.Length; i++)
            {
                if (i == 1)
                {
                    lab.ExecuteCatalog(entry);
                    lab.ExecuteCatalog(entry);
                }
                int health = victim.CurrentHealth;
                float first = -1f;
                Jotunnslayer.GodSkillGm.Activate(owner, Jotunnslayer.SkillBattlePreview.LightningEvolution, false);
                float deadline = Time.realtimeSinceStartup + 7f;
                while (victim && Time.realtimeSinceStartup < deadline)
                {
                    if (victim.CurrentHealth < health)
                    {
                        health = victim.CurrentHealth;
                        if (first >= 0f)
                        {
                            intervals[i] = Time.time - first;
                            break;
                        }
                        first = Time.time;
                    }
                    yield return null;
                }
                Jotunnslayer.GodSkillGm.Stop(owner);
                yield return null;
            }
            var saved = new RangeResearchItems.PersistentItems(items);
            items.Clear();
            saved.Restore(items);
            int StackCount() => lightFlux ? owner.ResearchLightFlux : owner.ResearchAlienHeads;
            bool restored = StackCount() == 2;
            float stackedSpeed = owner.ResearchAttackSpeed;
            lab.RemoveCatalog(entry);
            bool oneLeft = StackCount() == 1;
            float singleSpeed = owner.ResearchAttackSpeed;
            lab.RemoveCatalog(entry);
            bool removed = StackCount() == 0;
            bool speed = Mathf.Approximately(stackedSpeed, baselineSpeed / (lightFlux ? 3f : 1f)) &&
                Mathf.Approximately(singleSpeed, baselineSpeed / (lightFlux ? 2f : 1f)) &&
                Mathf.Approximately(owner.ResearchAttackSpeed, baselineSpeed);
            Jotunnslayer.JotunnMonsterGm.Clear(owner);
            if (intervals[0] > 0f && Mathf.Abs(intervals[1] - intervals[0] * (lightFlux ? .25f : .5625f)) <= .12f &&
                restored && oneLeft && removed && speed)
                Debug.Log($"RAIN_ITEM_COOLDOWN_CHECKED {id} castInterval={intervals[0]:F2}->{intervals[1]:F2} speed={baselineSpeed:F2}->{stackedSpeed:F2} restore=True removal=True");
            else Debug.LogError($"RAIN_ITEM_COOLDOWN_FAILED {id} castInterval={intervals[0]:F2}->{intervals[1]:F2} speed={speed} restored={restored} oneLeft={oneLeft} removed={removed}");
        }

        static IEnumerator VerifyPowerOrb(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            float baseline = owner.ResearchAttackSpeed;
            var entry = new BrowserEntry { id = "PowerOrbSphere", name = "视觉电池" };
            lab.ExecuteCatalog(entry);
            yield return null;
            bool first = Mathf.Approximately(owner.ResearchAttackSpeed, baseline + .15f);
            lab.ExecuteCatalog(entry);
            bool nonStacking = Mathf.Approximately(owner.ResearchAttackSpeed, baseline + .15f);
            var saved = new RangeResearchItems.PersistentItems(items);
            items.Clear();
            bool cleared = Mathf.Approximately(owner.ResearchAttackSpeed, baseline);
            saved.Restore(items);
            bool restored = saved.NeedsRestore && Mathf.Approximately(owner.ResearchAttackSpeed, baseline + .15f);
            items.enabled = false;
            bool disabled = Mathf.Approximately(owner.ResearchAttackSpeed, baseline);
            items.enabled = true;
            bool reenabled = Mathf.Approximately(owner.ResearchAttackSpeed, baseline + .15f);
            lab.RemoveCatalog(entry);
            bool kept = Mathf.Approximately(owner.ResearchAttackSpeed, baseline + .15f);
            lab.RemoveCatalog(entry);
            bool removed = Mathf.Approximately(owner.ResearchAttackSpeed, baseline);
            if (first && nonStacking && cleared && restored && disabled && reenabled && kept && removed)
                Debug.Log("RAIN_ITEM_POWER_ORB_CHECKED GM grant, +15% non-stacking, inventory restore, disable, final removal");
            else Debug.LogError($"RAIN_ITEM_POWER_ORB_FAILED first={first} nonStacking={nonStacking} cleared={cleared} restored={restored} disabled={disabled} reenabled={reenabled} kept={kept} removed={removed}");
            items.Clear();
        }

        static IEnumerator VerifyPowerCube(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var source = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 20f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in source.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            source.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(source.gameObject);
            yield return null;
            int originalHealth = owner.CurrentHealth;
            bool originalRunning = owner.IsRunning;
            var entry = new BrowserEntry { id = "PowerCube", name = "监狱矩阵" };
            int TakeHit()
            {
                owner.CurrentHealth = owner.MaxHealth;
                owner.IsRunning = true;
                owner.Server_ChangeHealthStateRaw(80, CharacterPart.body, AttackType.hitscan, source, 0);
                return owner.MaxHealth - owner.CurrentHealth;
            }

            items.Grant(RangeResearchItems.CatalogIndex("SprintArmor"));
            int baseline = TakeHit();
            lab.ExecuteCatalog(entry);
            int active = TakeHit();
            lab.ExecuteCatalog(entry);
            bool nonStacking = TakeHit() == active;
            bool signedArmor = items.ReduceIncomingDamage(100, -50f) == 123;
            var saved = new RangeResearchItems.PersistentItems(items);
            items.Clear();
            saved.Restore(items);
            // Recreate the test's external armor source after resetting the inventory.
            items.Grant(RangeResearchItems.CatalogIndex("SprintArmor"));
            bool restored = TakeHit() == active;
            lab.RemoveCatalog(entry);
            bool kept = TakeHit() == active;
            lab.RemoveCatalog(entry);
            bool removed = TakeHit() == baseline;
            if (baseline == 62 && active == 55 && nonStacking && signedArmor && restored && kept && removed)
                Debug.Log($"RAIN_ITEM_POWER_CUBE_CHECKED damage={baseline}->{active} nonStacking=True signedArmor=True restore=True removal=True");
            else Debug.LogError($"RAIN_ITEM_POWER_CUBE_FAILED baseline={baseline} active={active} nonStacking={nonStacking} signedArmor={signedArmor} restored={restored} kept={kept} removed={removed}");
            owner.IsRunning = originalRunning;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(source.gameObject);
            items.Clear();
        }

        static IEnumerator VerifyArmorPlate(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var source = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 20f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in source.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            source.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(source.gameObject);
            yield return null;

            var entry = new BrowserEntry { id = "ArmorPlate", name = "推斥护甲板" };
            int originalHealth = owner.CurrentHealth;
            int TakeHit(int damage)
            {
                owner.CurrentHealth = owner.MaxHealth;
                owner.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, source, 0);
                return owner.MaxHealth - owner.CurrentHealth;
            }

            int baseline = TakeHit(20);
            lab.ExecuteCatalog(entry);
            int oneStack = TakeHit(20);
            lab.ExecuteCatalog(entry);
            int twoStacks = TakeHit(20);
            int minimum = TakeHit(2);
            lab.RemoveCatalog(entry);
            int remaining = TakeHit(20);
            lab.RemoveCatalog(entry);
            int removed = TakeHit(20);

            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(source.gameObject);
            items.Clear();
            string result = $"baseline={baseline} one={oneStack} two={twoStacks} min={minimum} remaining={remaining} removed={removed}";
            if (baseline == 20 && oneStack == 15 && twoStacks == 10 && minimum == 1 && remaining == 15 && removed == 20)
                Debug.Log("RAIN_ITEM_ARMOR_PLATE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_ARMOR_PLATE_FAILED " + result);
        }

        static IEnumerator VerifyShatteringJustice(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var victim = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.right * 8f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in victim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            victim.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(victim.gameObject);
            yield return null;
            victim.MaxHealth = victim.CurrentHealth = 2000;

            int Hit(float proc = 1f)
            {
                int health = victim.CurrentHealth;
                victim.Server_ChangeHealthStateRaw(40, CharacterPart.body, AttackType.melee, owner, 0, proc);
                return health - victim.CurrentHealth;
            }

            var entry = new BrowserEntry { id = "ArmorReductionOnHit", name = "粉碎之正义" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int zeroProc = Hit(0f);
            int buildup = 0;
            for (int i = 0; i < 5; i++) buildup += Hit();
            int active = Hit(0f);
            yield return new WaitForSeconds(8.3f);
            int secondLayer = Hit(0f);
            for (int i = 0; i < 5; i++) Hit();
            yield return new WaitForSeconds(8f);
            int expired = Hit();
            int noBufferedHits = Hit(0f);
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            int removed = 0;
            for (int i = 0; i < 6; i++) removed += Hit();
            NetworkServer.Destroy(victim.gameObject);
            items.Clear();

            string result = $"zeroProc={zeroProc} buildup={buildup} active={active} secondLayer={secondLayer} expired={expired} afterExpiry={noBufferedHits} removed={removed}";
            if (zeroProc == 40 && buildup == 200 && active == 55 && secondLayer == 55 &&
                expired == 40 && noBufferedHits == 40 && removed == 240)
                Debug.Log("RAIN_ITEM_JUSTICE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_JUSTICE_FAILED " + result);
        }

        static IEnumerator VerifyPredatoryInstincts(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var victim = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.right * 8f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in victim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            victim.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(victim.gameObject);
            yield return null;
            victim.MaxHealth = victim.CurrentHealth = 2000;
            void Hit(float proc) => victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, owner, 0, proc);

            var entry = new BrowserEntry { id = "AttackSpeedOnCrit", name = "捕食本能" };
            for (int i = 0; i < 10; i++) items.Grant(RangeResearchItems.CatalogIndex("CritGlasses"));
            Hit(1f);
            bool noItem = owner.ResearchPredatoryBuffs == 0;
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            Hit(0f);
            bool zeroProc = owner.ResearchPredatoryBuffs == 0;
            Hit(.25f);
            for (int i = 0; i < 5; i++) Hit(1f);
            bool cap = Mathf.Approximately(owner.ResearchAttackSpeed, 1.6f);
            Hit(.25f); // At cap, replace the earliest stack with the shorter proc.
            yield return null;
            int sounds = 0;
            foreach (var source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (!source.isPlaying || !source.clip) continue;
                sounds |= source.clip.name switch { "PredatoryProc1" => 1, "PredatoryProc2" => 2, "PredatoryProc3" => 4, _ => 0 };
            }
            yield return new WaitForSeconds(.85f);
            bool staggered = owner.ResearchPredatoryBuffs == 4;
            lab.RemoveCatalog(entry);
            bool oneStack = Mathf.Approximately(owner.ResearchAttackSpeed, 1.36f);
            yield return new WaitForSeconds(2.25f);
            bool expired = Mathf.Approximately(owner.ResearchAttackSpeed, 1f);
            Hit(1f);
            bool retriggered = owner.ResearchPredatoryBuffs == 1;
            lab.RemoveCatalog(entry);
            Hit(1f);
            bool removed = owner.ResearchPredatoryBuffs == 0;
            NetworkServer.Destroy(victim.gameObject);
            items.Clear();

            string result = $"noItem={noItem} zeroProc={zeroProc} cap={cap} sounds={sounds} staggered={staggered} oneStack={oneStack} expired={expired} retriggered={retriggered} removed={removed}";
            if (noItem && zeroProc && cap && sounds == 7 && staggered && oneStack && expired && retriggered && removed)
                Debug.Log("RAIN_ITEM_PREDATORY_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_PREDATORY_FAILED " + result);
        }

        static IEnumerator VerifyGestureOfTheDrowned(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            var player = GameManager.myPlayerInstance;
            if (!player)
            {
                Debug.LogError("RAIN_ITEM_GESTURE_NO_PLAYER");
                yield break;
            }
            items.Clear();
            var gesture = new BrowserEntry { id = "AutoCastEquipment", name = "溺亡者的心意" };
            var fruit = new BrowserEntry { id = "Fruit", name = "外来水果" };
            var jade = new BrowserEntry { id = "GainArmor", name = "玉象" };
            int originalHealth = owner.CurrentHealth;
            int injured = Mathf.Max(1, owner.MaxHealth / 4);
            int healed = injured + Mathf.CeilToInt(owner.MaxHealth * .5f);
            owner.CurrentHealth = injured;
            lab.ExecuteCatalog(gesture);
            lab.ExecuteCatalog(gesture);
            lab.ExecuteCatalog(fruit);
            bool manualSuppressed = !player.ServerTryUseRangeEquipment();
            yield return new WaitUntil(() => items.EquipmentCooldownRemaining > 0f || !owner,
                TimeSpan.FromSeconds(2), () => Debug.LogError("RAIN_ITEM_GESTURE_FIRST_CAST_TIMEOUT"));
            bool firstHeal = owner && owner.CurrentHealth == healed;
            float firstCooldown = items.EquipmentCooldownRemaining;
            owner.CurrentHealth = injured;
            float started = Time.time;
            float deadline = started + 21f;
            while (owner.CurrentHealth == injured && Time.time < deadline) yield return null;
            float interval = Time.time - started;
            bool recast = owner.CurrentHealth == healed;
            float twoStacks = items.EquipmentCooldownRemaining;
            lab.RemoveCatalog(gesture);
            float oneStack = items.EquipmentCooldownRemaining;
            lab.RemoveCatalog(gesture);
            float zeroStacks = items.EquipmentCooldownRemaining;
            lab.RemoveCatalog(fruit);
            lab.ExecuteCatalog(jade);
            yield return new WaitForSeconds(.15f);
            bool manualRestored = !items.EquipmentAutoActivates && items.EquipmentCooldownRemaining == 0f &&
                player.ServerTryUseRangeEquipment() && items.EquipmentCooldownRemaining > 44.9f;
            lab.RemoveCatalog(jade);
            bool empty = items.Equipped == RangeResearchItems.Equipment.None;
            owner.CurrentHealth = originalHealth;
            items.Clear();

            string result = $"firstHeal={firstHeal} firstCooldown={firstCooldown:F2} interval={interval:F2} recast={recast} cooldown={twoStacks:F2}->{oneStack:F2}->{zeroStacks:F2} manualSuppressed={manualSuppressed} manualRestored={manualRestored} empty={empty}";
            if (firstHeal && Mathf.Abs(firstCooldown - 19.125f) < .25f && Mathf.Abs(interval - firstCooldown) < .25f &&
                recast && Mathf.Abs(oneStack * .85f - twoStacks) < .02f && Mathf.Abs(zeroStacks - 2f * oneStack) < .02f &&
                manualSuppressed && manualRestored && empty)
                Debug.Log("RAIN_ITEM_GESTURE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_GESTURE_FAILED " + result);
        }

        static IEnumerator VerifyTopazBrooch(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            const string targetId = "skeleton_1h_sur";
            items.Clear();
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            var attacker = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!target || !attacker)
            {
                Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
                Debug.LogError("RAIN_ITEM_BROOCH_NO_TARGET");
                yield break;
            }
            yield return null;
            int health = owner.CurrentHealth;
            var entry = new BrowserEntry { id = "BarrierOnKill", name = "黄晶胸针" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            target.CurrentHealth = target.MaxHealth;
            target.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, owner, 0);
            bool nonLethal = target.CurrentHealth > 0 && items.Barrier == 0f;
            target.Server_ChangeHealthStateRaw(target.MaxHealth * 2, CharacterPart.body, AttackType.melee, owner, 0);
            float granted = items.Barrier;
            owner.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.melee, attacker, 0);
            float afterHit = items.Barrier;
            bool absorbed = owner.CurrentHealth == health;
            yield return new WaitForSeconds(.25f);
            float afterDecay = items.Barrier;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            attacker.Server_ChangeHealthStateRaw(attacker.MaxHealth * 2, CharacterPart.body, AttackType.melee, owner, 0);
            bool removed = Mathf.Approximately(items.Barrier, afterDecay);
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            owner.CurrentHealth = health;
            items.Clear();
            string result = $"nonLethal={nonLethal} granted={granted:F2} afterHit={afterHit:F2} afterDecay={afterDecay:F2} absorbed={absorbed} removed={removed}";
            if (nonLethal && Mathf.Approximately(granted, 30f) && Mathf.Approximately(afterHit, 20f) &&
                absorbed && afterDecay > 0f && afterDecay < 20f && removed)
                Debug.Log("RAIN_ITEM_BROOCH_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_BROOCH_FAILED " + result);
        }

        static IEnumerator VerifyAegis(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            const string targetId = "skeleton_1h_sur";
            var attacker = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!attacker)
            {
                Debug.LogError("RAIN_ITEM_AEGIS_NO_TARGET");
                yield break;
            }
            yield return null;
            int originalHealth = owner.CurrentHealth;
            var entry = new BrowserEntry { id = "BarrierOnOverHeal", name = "庇护" };
            lab.ExecuteCatalog(entry);
            owner.CurrentHealth = owner.MaxHealth - 10;
            int healed = owner.ServerHeal(30, owner.netId);
            float first = items.Barrier;
            owner.ServerHeal(20, owner.netId, allowOverhealBarrier: false);
            bool excluded = Mathf.Approximately(items.Barrier, first);
            lab.ExecuteCatalog(entry);
            owner.ServerHeal(20, owner.netId);
            float second = items.Barrier;
            owner.Server_ChangeHealthStateRaw(12, CharacterPart.body, AttackType.melee, attacker, 0);
            float afterHit = items.Barrier;
            bool absorbed = owner.CurrentHealth == owner.MaxHealth;
            owner.ServerHeal(2 * owner.MaxHealth, owner.netId);
            bool capped = Mathf.Approximately(items.Barrier, owner.MaxHealth);
            owner.Server_ChangeHealthStateRaw(owner.MaxHealth, CharacterPart.body, AttackType.melee, attacker, 0);
            lab.RemoveCatalog(entry);
            owner.ServerHeal(20, owner.netId);
            float oneLeft = items.Barrier;
            lab.RemoveCatalog(entry);
            owner.ServerHeal(20, owner.netId);
            bool removed = Mathf.Approximately(items.Barrier, oneLeft);
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            owner.CurrentHealth = originalHealth;
            items.Clear();

            string result = $"healed={healed} one={first:F2} two={second:F2} afterHit={afterHit:F2} excluded={excluded} absorbed={absorbed} capped={capped} oneLeft={oneLeft:F2} removed={removed}";
            if (healed == 10 && Mathf.Approximately(first, 10f) && Mathf.Approximately(second, 30f) &&
                Mathf.Approximately(afterHit, 18f) && excluded && absorbed && capped &&
                Mathf.Approximately(oneLeft, 10f) && removed)
                Debug.Log("RAIN_ITEM_AEGIS_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_AEGIS_FAILED " + result);
        }

        static IEnumerator VerifyInfusion(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[3];
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 16f + Vector3.right * (i * 3f);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 100;
            var entry = new BrowserEntry { id = "Infusion", name = "Infusion" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int baseline = owner.MaxHealth;
            void Hit(int index, int damage) => targets[index].Server_ChangeHealthStateRaw(damage,
                CharacterPart.body, AttackType.hitscan, owner, 0, 0f);

            Hit(0, 20);
            bool nonLethal = owner.MaxHealth == baseline && !GameObject.Find("Rain infusion link");
            Hit(0, 150);
            int first = owner.MaxHealth - baseline;
            yield return null;
            bool cue = GameObject.Find("Rain infusion link") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "InfusionOriginal");
            lab.RemoveCatalog(entry);
            bool retained = owner.MaxHealth == baseline + first;
            Hit(1, 150);
            int single = owner.MaxHealth - baseline - first;
            lab.RemoveCatalog(entry);
            bool cleared = owner.MaxHealth == baseline;
            yield return new WaitForSeconds(.25f);
            Hit(2, 150);
            yield return null;
            bool removed = owner.MaxHealth == baseline && !GameObject.Find("Rain infusion link");
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"nonLethal={nonLethal} first={first} single={single} retained={retained} cleared={cleared} removed={removed} cue={cue} audio={audio}";
            if (nonLethal && first == 2 && single == 1 && retained && cleared && removed && cue && audio)
                Debug.Log("RAIN_ITEM_INFUSION_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_INFUSION_FAILED " + result);
        }

        static IEnumerator VerifyHarpoon(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[3];
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 16f + Vector3.right * (i * 3f);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 100;
            var entry = new BrowserEntry { id = "MoveSpeedOnKill", name = "Hunter's Harpoon" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(int index, int damage) => targets[index].Server_ChangeHealthStateRaw(damage,
                CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            float Speed() => items.MoveSpeed(4f, 8f, false);

            float baseline = Speed();
            Hit(0, 20);
            bool nonLethal = Mathf.Approximately(Speed(), baseline);
            Hit(0, 150);
            float first = Speed();
            float duration = owner.ResearchHarpoonDuration;
            yield return null;
            bool cue = GameObject.Find("Rain harpoon speed cue") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "HarpoonOriginal");
            yield return new WaitForSeconds(.65f);
            float decayed = Speed();
            Hit(1, 150);
            float refreshed = Speed();
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            bool retained = Speed() > baseline;
            yield return new WaitForSeconds(1.6f);
            bool expired = Mathf.Approximately(Speed(), baseline) && !GameObject.Find("Rain harpoon speed cue");
            Hit(2, 150);
            yield return null;
            bool removed = Mathf.Approximately(Speed(), baseline) && !GameObject.Find("Rain harpoon speed cue");
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"nonLethal={nonLethal} base={baseline:F2} first={first:F2} duration={duration:F2} decayed={decayed:F2} refreshed={refreshed:F2} retained={retained} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (nonLethal && Mathf.Abs(first - 9f) < .1f && Mathf.Approximately(duration, 1.5f) &&
                decayed > baseline && decayed < first - 1f && Mathf.Abs(refreshed - first) < .1f &&
                retained && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_HARPOON_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_HARPOON_FAILED " + result);
        }

        static IEnumerator VerifyMonsterTooth(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            Vector3 origin = owner.transform.position;
            Vector3 drop = origin + Vector3.up * 16f;
            var targets = new CharacterInstance[3];
            for (int i = 0; i < targets.Length; i++)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    drop + Vector3.right * (i == 2 ? 5f : 0f), Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets)
            {
                target.MaxHealth = 500;
                target.CurrentHealth = 100;
            }

            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "Tooth", name = "Monster Tooth" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            var mode = GameManager.Gamemode.transform;
            Transform Orb() => mode.Find("Monster tooth healing pickup");
            bool Playing(string clip) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == clip);
            void Hit(CharacterInstance target, int damage) =>
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);

            Hit(targets[0], 20);
            yield return null;
            bool nonLethal = !Orb() && owner.CurrentHealth == 500;
            Hit(targets[0], 150);
            yield return new WaitForSeconds(.1f);
            var orb = Orb();
            bool dropped = orb && owner.CurrentHealth == 500 && targets[1].CurrentHealth == 100;
            bool spawnAudio = Playing("MonsterToothSpawn");
            if (orb) owner.transform.position = orb.position;
            yield return new WaitForSeconds(.15f);
            int healed = owner.CurrentHealth - 500;
            bool collected = !Orb() && Playing("MonsterToothPickup");
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            Hit(targets[2], 150);
            yield return new WaitForSeconds(.15f);
            bool removed = !Orb() && owner.CurrentHealth == 500 + healed;
            owner.transform.position = origin;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"nonLethal={nonLethal} dropped={dropped} healed={healed} collected={collected} spawnAudio={spawnAudio} removed={removed}";
            if (nonLethal && dropped && healed == 48 && collected && spawnAudio && removed)
                Debug.Log("RAIN_ITEM_MONSTER_TOOTH_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_MONSTER_TOOTH_FAILED " + result);
        }

        static IEnumerator VerifyPocketIcbm(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 8f + Vector3.right * 4f,
                Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            target.MaxHealth = 500;

            var atg = new BrowserEntry { id = "Missile", name = "AtG Missile Mk. 1" };
            var icbm = new BrowserEntry { id = "MoreMissile", name = "Pocket I.C.B.M." };
            var damage = new int[6];
            var trails = new int[6];
            for (int sample = 0; sample < damage.Length; sample++)
            {
                switch (sample)
                {
                    case 0: lab.ExecuteCatalog(icbm); break;
                    case 1: lab.ExecuteCatalog(atg); break;
                    case 2: lab.ExecuteCatalog(icbm); break;
                    case 3: lab.RemoveCatalog(icbm); break;
                    case 4: lab.RemoveCatalog(icbm); break;
                    case 5: lab.RemoveCatalog(atg); break;
                }

                target.CurrentHealth = 500;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 10f);
                yield return null;
                trails[sample] = FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None)
                    .Count(trail => trail.name == "Rain homing item trail");
                yield return new WaitForSeconds(.65f);
                damage[sample] = 500 - target.CurrentHealth;
            }
            bool cleared = !GameObject.Find("Rain homing item trail");
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"damage={string.Join("/", damage)} trails={string.Join("/", trails)} cleared={cleared}";
            if (damage.SequenceEqual(new[] { 20, 200, 290, 200, 80, 20 }) &&
                trails.SequenceEqual(new[] { 0, 3, 3, 3, 1, 0 }) && cleared)
                Debug.Log("RAIN_ITEM_POCKET_ICBM_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_POCKET_ICBM_FAILED " + result);
        }

        static IEnumerator VerifyAtg(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 8f + Vector3.right * 4f,
                Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            target.MaxHealth = target.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "Missile", name = "AtG Missile Mk. 1" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(float coefficient) => target.Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);
            bool Playing(string clip) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == clip);

            Hit(0f);
            yield return new WaitForSeconds(.5f);
            bool zeroProc = target.CurrentHealth == 480 && !GameObject.Find("Rain homing item trail");
            target.CurrentHealth = 500;
            Hit(10f);
            yield return null;
            bool launch = GameObject.Find("Rain homing item trail") && Playing("AtgFire");
            yield return new WaitForSeconds(.55f);
            int stacked = 500 - target.CurrentHealth;
            bool impact = Playing("AtgImpact");
            lab.RemoveCatalog(entry);
            target.CurrentHealth = 500;
            Hit(10f);
            yield return new WaitForSeconds(.6f);
            int single = 500 - target.CurrentHealth;
            lab.RemoveCatalog(entry);
            target.CurrentHealth = 500;
            Hit(10f);
            yield return new WaitForSeconds(.65f);
            int removed = 500 - target.CurrentHealth;
            bool cleared = !GameObject.Find("Rain homing item trail");
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"zeroProc={zeroProc} launch={launch} two={stacked} impact={impact} one={single} removed={removed} cleared={cleared}";
            if (zeroProc && launch && stacked == 140 && impact && single == 80 && removed == 20 && cleared)
                Debug.Log("RAIN_ITEM_ATG_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_ATG_FAILED " + result);
        }

        static IEnumerator VerifyPlasmaShrimp(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 8f + Vector3.right * 4f,
                Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            target.MaxHealth = target.CurrentHealth = 500;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            var missile = new BrowserEntry { id = "Missile", name = "AtG Missile Mk. 1" };
            var shrimp = new BrowserEntry { id = "MissileVoid", name = "Plasma Shrimp" };
            lab.ExecuteCatalog(missile);
            lab.ExecuteCatalog(shrimp);
            bool converted = owner.ResearchPlasmaShrimps == 2 && owner.ResearchShield == 100;
            void Hit(float coefficient) => target.Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);

            Hit(0f);
            yield return new WaitForSeconds(.5f);
            bool zeroProc = target.CurrentHealth == 480 && !GameObject.Find("Rain plasma shrimp trail");
            Hit(1f);
            yield return null;
            bool cue = GameObject.Find("Rain plasma shrimp trail") != null;
            yield return new WaitForSeconds(.65f);
            int shielded = 480 - target.CurrentHealth;
            owner.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
            bool broken = owner.ResearchShield == 0 && owner.CurrentHealth == 500;
            Hit(1f);
            yield return new WaitForSeconds(.6f);
            int unshielded = 480 - shielded - target.CurrentHealth;
            lab.RemoveCatalog(shrimp);
            lab.RemoveCatalog(shrimp);
            Hit(1f);
            yield return new WaitForSeconds(.6f);
            int removedDamage = 480 - shielded - unshielded - target.CurrentHealth;
            bool cleared = owner.ResearchPlasmaShrimps == 0 && owner.ResearchShieldCapacity == 0 &&
                !GameObject.Find("Rain plasma shrimp trail");
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            string result = $"converted={converted} zeroProc={zeroProc} cue={cue} shielded={shielded} broken={broken} unshielded={unshielded} removed={removedDamage} cleared={cleared}";
            if (converted && zeroProc && cue && shielded == 38 && broken && unshielded == 20 &&
                removedDamage == 20 && cleared)
                Debug.Log("RAIN_ITEM_PLASMA_SHRIMP_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_PLASMA_SHRIMP_FAILED " + result);
        }

        static IEnumerator VerifyPersonalShield(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "PersonalShield", name = "Personal Shield Generator" };
            lab.ExecuteCatalog(entry);
            int one = owner.ResearchShield;
            lab.ExecuteCatalog(entry);
            int two = owner.ResearchShield;
            void Hit(int damage) => owner.Server_ChangeHealthStateRaw(damage, CharacterPart.body,
                AttackType.hitscan, attacker, 0, 0f);

            Hit(120);
            bool absorbed = owner.CurrentHealth == 500 && owner.ResearchShield == 40;
            Hit(60);
            float brokenAt = Time.time;
            bool spill = owner.CurrentHealth == 480 && owner.ResearchShield == 0;
            yield return new WaitForSeconds(6.75f);
            bool early = owner.ResearchShield == 0 && !GameObject.Find("Rain personal shield recharge");
            while (Time.time < brokenAt + 7.4f && !GameObject.Find("Rain personal shield recharge")) yield return null;
            bool cue = GameObject.Find("Rain personal shield recharge") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "PersonalShieldRecharge");
            yield return new WaitUntil(() => Time.time >= brokenAt + 9.25f);
            int charged = owner.ResearchShield;
            lab.RemoveCatalog(entry);
            int single = owner.ResearchShield;
            lab.RemoveCatalog(entry);
            int before = owner.CurrentHealth;
            Hit(20);
            bool removed = owner.ResearchShield == 0 && owner.CurrentHealth == before - 20;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"one={one} two={two} absorbed={absorbed} spill={spill} early={early} charged={charged} single={single} removed={removed} cue={cue} audio={audio}";
            if (one == 80 && two == 160 && absorbed && spill && early && charged == 160 && single == 80 &&
                removed && cue && audio)
                Debug.Log("RAIN_ITEM_PERSONAL_SHIELD_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_PERSONAL_SHIELD_FAILED " + result);
        }

        static IEnumerator VerifyRoseBuckler(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            bool running = owner.IsRunning;
            motor.enabled = false;
            owner.IsRunning = false;
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            var entry = new BrowserEntry { id = "SprintArmor", name = "Rose Buckler" };
            int Hit()
            {
                owner.CurrentHealth = 500;
                owner.Server_ChangeHealthStateRaw(104, CharacterPart.body, AttackType.hitscan, attacker, 0, 0f);
                return 500 - owner.CurrentHealth;
            }

            int baseline = Hit();
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int idle = Hit();
            owner.IsRunning = true;
            int stacked = Hit();
            float deadline = Time.time + .3f;
            while (Time.time < deadline && !GameObject.Find("Rain rose buckler active")) yield return null;
            bool cue = GameObject.Find("Rain rose buckler active") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "RoseBucklerOriginal");
            lab.RemoveCatalog(entry);
            int single = Hit();
            owner.IsRunning = false;
            int stopped = Hit();
            lab.RemoveCatalog(entry);
            owner.IsRunning = true;
            yield return new WaitForSeconds(.7f);
            int removed = Hit();
            bool cleared = !GameObject.Find("Rain rose buckler active");
            owner.IsRunning = running;
            motor.enabled = motorEnabled;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"base={baseline} idle={idle} two={stacked} one={single} stopped={stopped} removed={removed} cue={cue} audio={audio} cleared={cleared}";
            if (baseline == 104 && idle == baseline && stacked == 65 && single == 80 &&
                stopped == baseline && removed == baseline && cue && audio && cleared)
                Debug.Log("RAIN_ITEM_ROSE_BUCKLER_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_ROSE_BUCKLER_FAILED " + result);
        }

        static IEnumerator VerifyOddlyShapedOpal(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            var entry = new BrowserEntry { id = "OutOfCombatArmor", name = "Oddly-shaped Opal" };
            int Hit()
            {
                owner.CurrentHealth = 500;
                owner.Server_ChangeHealthStateRaw(120, CharacterPart.body, AttackType.hitscan, attacker, 0, 0f);
                return 500 - owner.CurrentHealth;
            }

            int baseline = Hit();
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int stacked = Hit();
            yield return null;
            bool cue = GameObject.Find("Rain opal guard") != null;
            int followup = Hit();
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(6.75f);
            int early = Hit();
            yield return new WaitForSeconds(7.25f);
            int recovered = Hit();
            lab.RemoveCatalog(entry);
            int removed = Hit();
            yield return new WaitForSeconds(.75f);
            bool cleared = !GameObject.Find("Rain opal guard");
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"base={baseline} two={stacked} followup={followup} early={early} oneRecovered={recovered} removed={removed} cue={cue} cleared={cleared}";
            if (baseline == 120 && stacked == 40 && followup == baseline && early == baseline &&
                recovered == 60 && removed == baseline && cue && cleared)
                Debug.Log("RAIN_ITEM_OPAL_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_OPAL_FAILED " + result);
        }

        static IEnumerator VerifyParentEgg(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            var entry = new BrowserEntry { id = "ParentEgg", name = "Planula" };
            int HitFrom(int health, int damage)
            {
                owner.CurrentHealth = health;
                owner.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, attacker, 0, 0f);
                return owner.CurrentHealth;
            }

            int baseline = HitFrom(500, 40);
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int full = HitFrom(1000, 40);
            yield return null;
            bool noFullCue = !GameObject.Find("Rain parent egg heal");
            int stacked = HitFrom(500, 40);
            yield return null;
            bool cue = GameObject.Find("Rain parent egg heal") != null;
            lab.RemoveCatalog(entry);
            int single = HitFrom(500, 40);
            int survived = HitFrom(10, 20);
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(.7f);
            int removed = HitFrom(500, 40);
            yield return null;
            bool cleared = !GameObject.Find("Rain parent egg heal");

            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"base={baseline} full={full} fullQuiet={noFullCue} two={stacked} one={single} survived={survived} removed={removed} cleared={cleared} cue={cue}";
            if (baseline == 460 && full == 960 && noFullCue && stacked == 490 && single == 475 &&
                survived == 5 && removed == baseline && cleared && cue)
                Debug.Log("RAIN_ITEM_PARENT_EGG_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_PARENT_EGG_FAILED " + result);
        }

        static IEnumerator VerifyRejuvenationRack(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            var rack = new BrowserEntry { id = "IncreaseHealing", name = "Rejuvenation Rack" };
            var aegis = new BrowserEntry { id = "BarrierOnOverHeal", name = "Aegis" };

            int HealFrom(int start)
            {
                owner.CurrentHealth = start;
                owner.ServerHeal(20, owner.netId);
                return owner.CurrentHealth - start;
            }

            int baseline = HealFrom(500);
            lab.ExecuteCatalog(rack);
            int first = HealFrom(500);
            lab.ExecuteCatalog(rack);
            int stacked = HealFrom(500);
            lab.ExecuteCatalog(aegis);
            int capped = HealFrom(980);
            float barrier = items.Barrier;
            lab.RemoveCatalog(aegis);
            lab.RemoveCatalog(rack);
            int single = HealFrom(500);
            lab.RemoveCatalog(rack);
            int removed = HealFrom(500);
            yield return null;

            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            items.Clear();
            string result = $"base={baseline} one={first} two={stacked} capped={capped} barrier={barrier:F1} single={single} removed={removed}";
            if (baseline == 20 && first == 40 && stacked == 60 && capped == 20 &&
                Mathf.Approximately(barrier, 20f) && single == first && removed == baseline)
                Debug.Log("RAIN_ITEM_REJUVENATION_RACK_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_REJUVENATION_RACK_FAILED " + result);
        }

        static IEnumerator VerifyCautiousSlug(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            float originalRegen = owner.RainHealthRegen;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            owner.RainHealthRegen = 0f;
            float baseline = owner.EffectiveRainHealthRegen;
            var entry = new BrowserEntry { id = "HealWhileSafe", name = "Cautious Slug" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, attacker, 0, 0f);
            int damaged = owner.CurrentHealth;
            yield return null;
            bool Playing(string name) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == name);
            bool hide = Playing("SlugHide");
            yield return new WaitForSeconds(6.75f);
            bool unsafePeriod = owner.ResearchActiveSlugs == 0 && Mathf.Approximately(owner.EffectiveRainHealthRegen, baseline);
            int early = owner.CurrentHealth - damaged;
            yield return new WaitForSeconds(.5f);
            bool activated = owner.ResearchActiveSlugs == 2;
            bool emerge = Playing("SlugEmerge");
            bool cue = GameObject.Find("Rain cautious slug active") != null;
            float stackedRate = owner.EffectiveRainHealthRegen - baseline;
            int before = owner.CurrentHealth;
            yield return new WaitForSeconds(2f);
            int healed = owner.CurrentHealth - before;
            lab.RemoveCatalog(entry);
            float singleRate = owner.EffectiveRainHealthRegen - baseline;
            lab.RemoveCatalog(entry);
            bool removed = owner.ResearchActiveSlugs == 0 && Mathf.Approximately(owner.EffectiveRainHealthRegen, baseline);
            owner.RainHealthRegen = originalRegen;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"damaged={damaged} unsafe={unsafePeriod} early={early} activated={activated} stackedRate={stackedRate:F2} singleRate={singleRate:F2} healed={healed} removed={removed}";
            if (damaged == 480 && unsafePeriod && early == 0 && activated && stackedRate > 0 &&
                Mathf.Approximately(stackedRate, 2f * singleRate) && healed > 0 && removed && hide && emerge && cue)
                Debug.Log("RAIN_ITEM_CAUTIOUS_SLUG_CHECKED " + result + $" hide={hide} emerge={emerge} cue={cue}");
            else Debug.LogError("RAIN_ITEM_CAUTIOUS_SLUG_FAILED " + result + $" hide={hide} emerge={emerge} cue={cue}");
        }

        static IEnumerator VerifyMedkit(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "Medkit", name = "Medkit" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit() => owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, attacker, 0, 0f);

            Hit();
            bool direct = owner.CurrentHealth == 480;
            yield return new WaitForSeconds(1f);
            Hit();
            float restarted = Time.time;
            yield return new WaitForSeconds(1.3f);
            bool delayed = owner.CurrentHealth == 460 && !GameObject.Find("Rain medkit heal");
            while (Time.time < restarted + 2.4f && !GameObject.Find("Rain medkit heal")) yield return null;
            int healed = owner.CurrentHealth - 460;
            bool cue = GameObject.Find("Rain medkit heal") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "MedkitOriginal");
            Hit();
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(2.25f);
            bool removed = owner.CurrentHealth == 440 + healed && !GameObject.Find("Rain medkit heal");
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"direct={direct} delayed={delayed} healed={healed} cue={cue} audio={audio} removed={removed}";
            if (direct && delayed && healed == 120 && cue && audio && removed)
                Debug.Log("RAIN_ITEM_MEDKIT_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_MEDKIT_FAILED " + result);
        }

        static IEnumerator VerifyBustlingFungus(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            bool grounded = owner.IsGrounded;
            bool running = owner.IsRunning;
            Vector3 origin = owner.transform.position;
            Vector2 input = owner.movementInput;
            int maxHealth = owner.MaxHealth;
            int health = owner.CurrentHealth;
            motor.enabled = false;
            owner.IsGrounded = true;
            owner.IsRunning = false;
            owner.SetMovementInput(Vector2.zero);
            var targets = new CharacterInstance[3];
            float[] offsets = { 4f, 5f, 2f };
            for (int i = 0; i < targets.Length; i++)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    origin + Vector3.right * offsets[i], Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = i == 2 ? (owner.Team == 0 ? 1 : 0) : owner.Team;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 1000;
            foreach (var target in targets) target.CurrentHealth = 500;

            var entry = new BrowserEntry { id = "Mushroom", name = "Bustling Fungus" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            float started = Time.time;
            yield return new WaitForSeconds(.5f);
            bool delayed = owner.CurrentHealth == 500 && targets[0].CurrentHealth == 500;
            while (Time.time < started + 1.4f && !GameObject.Find("Rain bustling fungus heal")) yield return null;
            bool cue = GameObject.Find("Rain bustling fungus heal") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "BustlingFungusStart");
            int self = owner.CurrentHealth - 500;
            int ally = targets[0].CurrentHealth - 500;
            int far = targets[1].CurrentHealth - 500;
            int enemy = targets[2].CurrentHealth - 500;
            float movementEnds = Time.time + 1.2f;
            while (Time.time < movementEnds)
            {
                owner.transform.position += Vector3.forward * (2f * Time.deltaTime);
                yield return null;
            }
            bool moving = owner.CurrentHealth == 500 + self && targets[0].CurrentHealth == 500 + ally &&
                !GameObject.Find("Rain bustling fungus heal");
            owner.SetMovementInput(Vector2.zero);
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(1.2f);
            bool removed = owner.CurrentHealth == 500 + self && targets[0].CurrentHealth == 500 + ally &&
                !GameObject.Find("Rain bustling fungus heal");

            foreach (var target in targets) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            owner.transform.position = origin;
            owner.SetMovementInput(input);
            owner.IsGrounded = grounded;
            owner.IsRunning = running;
            motor.enabled = motorEnabled;
            owner.MaxHealth = maxHealth;
            owner.CurrentHealth = health;
            int expected = Mathf.RoundToInt(1000f * .0675f);
            string result = $"delayed={delayed} self={self} ally={ally} far={far} enemy={enemy} moving={moving} removed={removed} cue={cue} audio={audio}";
            if (delayed && self == expected && ally == expected && far == 0 && enemy == 0 && moving && removed && cue && audio)
                Debug.Log("RAIN_ITEM_BUSTLING_FUNGUS_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_BUSTLING_FUNGUS_FAILED " + result);
        }

        static IEnumerator VerifyWeepingFungus(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var motor = owner.GetComponent<CharacterMotor>();
            bool originalMotorEnabled = motor.enabled;
            bool originalRunning = owner.IsRunning;
            // Hold the server sprint state without hardware input in batch mode.
            motor.enabled = false;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.IsRunning = false;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            var original = new BrowserEntry { id = "Mushroom", name = "Bustling Fungus" };
            var entry = new BrowserEntry { id = "MushroomVoid", name = "Weeping Fungus" };
            lab.ExecuteCatalog(original);
            lab.ExecuteCatalog(entry);
            yield return new WaitForSeconds(1.25f);
            bool idle = owner.CurrentHealth == 500;
            owner.IsRunning = true;
            float started = Time.time;
            float cueDeadline = started + .3f;
            while (Time.time < cueDeadline && !GameObject.Find("Rain weeping fungus heal")) yield return null;
            bool cue = GameObject.Find("Rain weeping fungus heal") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "WeepingFungusStart");
            yield return new WaitUntil(() => Time.time >= started + 1f);
            int healed = owner.CurrentHealth - 500;
            owner.IsRunning = false;
            int stoppedHealth = owner.CurrentHealth;
            yield return new WaitForSeconds(.5f);
            bool stopped = owner.CurrentHealth == stoppedHealth;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            owner.IsRunning = true;
            yield return new WaitForSeconds(.85f);
            bool removed = owner.CurrentHealth == stoppedHealth && !GameObject.Find("Rain weeping fungus heal");
            items.Clear();
            owner.IsRunning = originalRunning;
            motor.enabled = originalMotorEnabled;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            string result = $"idle={idle} healed={healed} stopped={stopped} removed={removed} cue={cue} audio={audio}";
            if (idle && healed >= 38 && healed <= 44 && stopped && removed && cue && audio)
                Debug.Log("RAIN_ITEM_WEEPING_FUNGUS_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_WEEPING_FUNGUS_FAILED " + result);
        }

        static IEnumerator VerifySaferSpaces(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;

            int originalHealth = owner.CurrentHealth;
            var bear = new BrowserEntry { id = "Bear", name = "Tougher Times" };
            var entry = new BrowserEntry { id = "BearVoid", name = "Safer Spaces" };
            int Hit()
            {
                owner.CurrentHealth = owner.MaxHealth;
                owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, attacker, 0, 0f);
                return owner.MaxHealth - owner.CurrentHealth;
            }

            int baseline = Hit();
            lab.ExecuteCatalog(bear);
            bool granted = items.Bears == 1;
            lab.ExecuteCatalog(entry);
            bool corrupted = granted && items.Bears == 0;
            float started = Time.time;
            int first = Hit();
            float cueDeadline = Time.time + .25f;
            while (Time.time < cueDeadline && !GameObject.Find("Rain safer spaces shield")) yield return null;
            bool cue = GameObject.Find("Rain safer spaces shield") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "SaferSpacesBlock");
            int cooling = Hit();
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int reequipped = Hit();
            yield return new WaitUntil(() => Time.time >= started + 13.25f);
            int beforeReady = Hit();
            yield return new WaitUntil(() => Time.time >= started + 13.75f);
            int recharged = Hit();
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            int removed = Hit();
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"corrupted={corrupted} baseline={baseline} first={first} cooling={cooling} reequipped={reequipped} beforeReady={beforeReady} recharged={recharged} removed={removed} cue={cue} audio={audio}";
            if (corrupted && baseline > 0 && first == 0 && cooling == baseline && reequipped == baseline &&
                beforeReady == baseline && recharged == 0 && removed == baseline && cue && audio)
                Debug.Log("RAIN_ITEM_SAFER_SPACES_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_SAFER_SPACES_FAILED " + result);
        }

        static IEnumerator VerifyTougherTimes(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            const string targetId = "skeleton_1h_sur";
            items.Clear();
            var attacker = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!attacker)
            {
                Debug.LogError("RAIN_ITEM_BEAR_NO_TARGET");
                yield break;
            }
            yield return null;
            int originalHealth = owner.CurrentHealth;
            var bear = new BrowserEntry { id = "Bear", name = "更艰难的时光" };
            var clover = new BrowserEntry { id = "Clover", name = "57 叶草" };

            ulong TakeHits(out int blocked)
            {
                var saved = UnityEngine.Random.state;
                UnityEngine.Random.InitState(17);
                ulong mask = 0;
                blocked = 0;
                for (int i = 0; i < 64; i++)
                {
                    owner.CurrentHealth = owner.MaxHealth;
                    owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.melee, attacker, 0, 0f);
                    if (owner.CurrentHealth != owner.MaxHealth) continue;
                    mask |= 1UL << i;
                    blocked++;
                }
                UnityEngine.Random.state = saved;
                return mask;
            }

            TakeHits(out int baseline);
            lab.ExecuteCatalog(bear);
            ulong one = TakeHits(out int oneCount);
            lab.ExecuteCatalog(bear);
            ulong two = TakeHits(out int twoCount);
            lab.ExecuteCatalog(clover);
            bool unaffectedByLuck = TakeHits(out _) == two;
            lab.RemoveCatalog(clover);
            lab.RemoveCatalog(bear);
            bool oneRemaining = TakeHits(out _) == one;
            lab.RemoveCatalog(bear);
            TakeHits(out int removed);
            yield return null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "BearBlockOriginal");
            owner.CurrentHealth = originalHealth;
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            items.Clear();

            string result = $"baseline={baseline} one={oneCount} two={twoCount} luckIndependent={unaffectedByLuck} oneRemaining={oneRemaining} removed={removed} audio={audio}";
            if (baseline == 0 && oneCount > 0 && twoCount > oneCount && (two & one) == one &&
                unaffectedByLuck && oneRemaining && removed == 0 && audio)
                Debug.Log("RAIN_ITEM_BEAR_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_BEAR_FAILED " + result);
        }

        static IEnumerator VerifyBrilliantBehemoth(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[5];
            float[] offsets = { 0f, 3f, 5.5f, 7.5f, -3f };
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 16f + Vector3.right * offsets[i];
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), position,
                    Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = i == 4 ? owner.Team : owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                targets[i] = target;
            }
            yield return null;

            string Hit(float proc)
            {
                foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 500;
                targets[0].Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, proc);
                return string.Join("/", targets.Select(target => 500 - target.CurrentHealth));
            }

            var entry = new BrowserEntry { id = "Behemoth", name = "完美巨兽" };
            lab.ExecuteCatalog(entry);
            string zeroProc = Hit(0f);
            string pellet = Hit(.125f);
            string one = Hit(1f);
            lab.ExecuteCatalog(entry);
            string two = Hit(1f);
            lab.RemoveCatalog(entry);
            string remaining = Hit(1f);
            lab.RemoveCatalog(entry);
            string removed = Hit(1f);
            yield return null;
            bool cue = GameObject.Find("Rain Behemoth blast ring") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "BehemothBlastOriginal");
            foreach (var target in targets) NetworkServer.Destroy(target.gameObject);
            items.Clear();

            string result = $"zero={zeroProc} pellet={pellet} one={one} two={two} remaining={remaining} removed={removed} cue={cue} audio={audio}";
            if (zeroProc == "20/0/0/0/0" && pellet == "32/0/0/0/0" && one == "32/12/0/0/0" &&
                two == "32/12/12/0/0" && remaining == one && removed == zeroProc && cue && audio)
                Debug.Log("RAIN_ITEM_BEHEMOTH_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_BEHEMOTH_FAILED " + result);
        }

        static IEnumerator VerifyTriTipDagger(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            const string targetId = "skeleton_1h_sur";
            items.Clear();
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!target)
            {
                Debug.LogError("RAIN_ITEM_DAGGER_NO_TARGET");
                yield break;
            }
            yield return null;
            target.MaxHealth = target.CurrentHealth = 1000;
            var entry = new BrowserEntry { id = "BleedOnHit", name = "三尖匕首" };
            for (int i = 0; i < 20; i++) lab.ExecuteCatalog(entry);
            void Hit(float proc) => target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.melee, owner, 0, proc);
            Hit(0f);
            bool zero = !target.TryGetComponent<RangeBleeding>(out _);
            Hit(.5f);
            Hit(1f);
            int immediate = 1000 - target.CurrentHealth;
            yield return null;
            bool cue = GameObject.Find("Rain dagger bleed cue") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "DaggerBleedOriginal");
            int halfExpected = Mathf.RoundToInt(items.ItemBaseAttack * 1.2f);
            int fullExpected = Mathf.RoundToInt(items.ItemBaseAttack * 2.4f);
            for (int i = 0; i < 20; i++) lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(1.65f);
            bool halfExpired = target.TryGetComponent<RangeBleeding>(out var bleed) && bleed.enabled &&
                1000 - target.CurrentHealth - immediate > halfExpected &&
                1000 - target.CurrentHealth - immediate < halfExpected + fullExpected;
            yield return new WaitForSeconds(1.55f);
            int dot = 1000 - target.CurrentHealth - immediate;
            bool expired = bleed && !bleed.enabled;
            Hit(1f);
            bool removed = 1000 - target.CurrentHealth == immediate + dot + 20 && bleed && !bleed.enabled && items.Daggers == 0;
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            items.Clear();

            string result = $"zero={zero} direct={immediate} dot={dot} expected={halfExpected}+{fullExpected} halfExpired={halfExpired} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (zero && immediate == 60 && dot == halfExpected + fullExpected && halfExpired && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_DAGGER_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_DAGGER_FAILED " + result);
        }

        static IEnumerator VerifyShatterspleen(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[5];
            float[] offsets = { 0f, 6f, 12f, 17f, -4f };
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 16f + Vector3.right * offsets[i];
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), position,
                    Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 1000;
            var victim = targets[0];
            victim.MaxHealth = victim.CurrentHealth = 100;
            var entry = new BrowserEntry { id = "BleedOnHitAndExplode", name = "易爆的脾脏" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            for (int i = 0; i < 10; i++) items.Grant(RangeResearchItems.CatalogIndex("CritGlasses"));
            void Hit(CharacterInstance target, float proc) =>
                target.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, proc);
            bool Playing(string clip) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == clip);

            Hit(victim, 0f);
            bool zero = !victim.TryGetComponent<RangeBleeding>(out _);
            Hit(victim, 1f);
            bool bleeding = victim.TryGetComponent<RangeBleeding>(out var dot) && dot.HasBleed();
            yield return null;
            bool impactAudio = Playing("ShatterspleenImpact");
            int expected = Mathf.RoundToInt((4f * items.ItemBaseAttack + 15f) * 4f);
            victim.CurrentHealth = 1;
            yield return new WaitForSeconds(.55f);
            int near = 1000 - targets[1].CurrentHealth;
            int mid = 1000 - targets[2].CurrentHealth;
            int far = 1000 - targets[3].CurrentHealth;
            bool died = !victim || victim.CurrentHealth <= 0;
            bool cue = GameObject.Find("Rain Shatterspleen blast") != null;
            bool blastAudio = Playing("ShatterspleenBlast");
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            Hit(targets[4], 1f);
            bool removed = !targets[4].TryGetComponent<RangeBleeding>(out _);
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"zero={zero} bleed={bleeding} died={died} near={near} mid={mid} far={far} expected={expected} cue={cue} impact={impactAudio} blast={blastAudio} removed={removed}";
            if (zero && bleeding && died && near == expected && mid == Mathf.RoundToInt(expected * .25f) &&
                far == 0 && cue && impactAudio && blastAudio && removed)
                Debug.Log("RAIN_ITEM_SHATTERSPLEEN_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_SHATTERSPLEEN_FAILED " + result);
        }

        static IEnumerator VerifyGhorTomePlaytest(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            var player = GameManager.myPlayerInstance;
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            if (!player || !mode)
            {
                Debug.LogError("RAIN_ITEM_GHOR_NO_PLAYER_OR_MODE");
                yield break;
            }
            items.Clear();
            var entry = new BrowserEntry { id = "BonusGoldPackOnKill", name = "古尔的巨著" };
            int originalGold = player.RainGold;
            Vector3 originalPosition = owner.transform.position;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            motor.enabled = false;

            CharacterInstance SpawnVictim(Vector3 position)
            {
                var victim = Instantiate(mode.GetPlayerCharacterPrefab(), position, Quaternion.identity)
                    .GetComponent<CharacterInstance>();
                foreach (var slot in victim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                victim.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(victim.gameObject);
                victim.GetComponent<CharacterMotor>().enabled = false;
                victim.MaxHealth = victim.CurrentHealth = 100;
                return victim;
            }

            for (int i = 0; i < 25; i++) lab.ExecuteCatalog(entry);
            var first = SpawnVictim(originalPosition + Vector3.up * 15f);
            var second = SpawnVictim(originalPosition + Vector3.up * 15f + Vector3.right * 5f);
            yield return null;
            first.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            bool nonLethal = !mode.transform.Find("Ghor gold pickup");
            first.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            yield return null;
            var pickup = mode.transform.Find("Ghor gold pickup");
            bool dropped = pickup && player.RainGold == originalGold;
            if (pickup)
            {
                owner.transform.position = pickup.position - Vector3.up * .4f;
                Physics.SyncTransforms();
                float deadline = Time.realtimeSinceStartup + 1f;
                while (pickup && Time.realtimeSinceStartup < deadline) yield return null;
            }
            int gold = player.RainGold - originalGold;
            bool collected = !pickup && gold == 25;
            for (int i = 0; i < 25; i++) lab.RemoveCatalog(entry);
            second.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            yield return null;
            bool removed = !mode.transform.Find("Ghor gold pickup") && player.RainGold == originalGold + 25;
            owner.transform.position = originalPosition;
            motor.enabled = motorEnabled;
            Physics.SyncTransforms();
            player.RainGold = originalGold;
            if (first) NetworkServer.Destroy(first.gameObject);
            if (second) NetworkServer.Destroy(second.gameObject);
            items.Clear();

            string result = $"nonLethal={nonLethal} dropped={dropped} gold={gold} collected={collected} removed={removed}";
            if (nonLethal && dropped && collected && removed) Debug.Log("RAIN_ITEM_GHOR_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_GHOR_FAILED " + result);
        }

        static IEnumerator VerifyArmorPiercingRounds(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var rounds = new BrowserEntry { id = "BossDamageBonus", name = "穿甲弹" };
            foreach (string id in new[] { "TITAN_BODY", "BEETLEQUEEN_BODY", "BEETLE_BODY" })
            {
                var monster = new BrowserEntry { id = id, category = 1 };
                lab.ExecuteCatalog(monster);
                yield return null;
                var actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None).FirstOrDefault(enemy => enemy.IsLabSummon);
                var target = actor ? actor.GetComponent<CharacterInstance>() : null;
                if (!target)
                {
                    Debug.LogError("RAIN_ITEM_AP_ROUNDS_NO_TARGET " + id);
                    continue;
                }
                int Hit()
                {
                    target.MaxHealth = target.CurrentHealth = 1000;
                    target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                    return 1000 - target.CurrentHealth;
                }

                int baseline = Hit();
                lab.ExecuteCatalog(rounds);
                int one = Hit();
                lab.ExecuteCatalog(rounds);
                int two = Hit();
                lab.RemoveCatalog(rounds);
                int remaining = Hit();
                lab.RemoveCatalog(rounds);
                int removed = Hit();
                lab.RemoveCatalog(monster);
                yield return null;
                int oneExpected = id == "BEETLE_BODY" ? 100 : 120;
                int twoExpected = id == "BEETLE_BODY" ? 100 : 140;
                string result = $"{id} damage={baseline}/{one}/{two}/{remaining}/{removed}";
                if (baseline == 100 && one == oneExpected && two == twoExpected && remaining == one && removed == baseline)
                    Debug.Log("RAIN_ITEM_AP_ROUNDS_CHECKED " + result);
                else Debug.LogError("RAIN_ITEM_AP_ROUNDS_FAILED " + result);
            }
            items.Clear();
        }

        static IEnumerator VerifyNeedletick(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var position = owner.transform.position + Vector3.up * 16f;
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                position, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            target.MaxHealth = target.CurrentHealth = 1000;
            var original = new BrowserEntry { id = "BleedOnHit", name = "Tri-Tip Dagger" };
            var entry = new BrowserEntry { id = "BleedOnHitVoid", name = "Needletick" };
            lab.ExecuteCatalog(original);
            bool granted = items.Daggers == 1;
            lab.ExecuteCatalog(entry);
            bool corrupted = granted && items.Daggers == 0;
            void Hit(float coefficient) => target.Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);
            bool Playing(string clip) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == clip);

            Hit(0f);
            yield return null;
            bool zeroProc = target.CurrentHealth == 980 && !GameObject.Find("Rain needletick mark");
            target.CurrentHealth = 1000;
            float started = Time.time;
            Hit(5f);
            yield return null;
            bool marked = GameObject.Find("Rain needletick mark") && Playing("NeedletickStart") &&
                target.TryGetComponent<RangeBleeding>(out var dot) && !dot.HasBleed() && dot.ActiveDebuffCount() == 1;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            Hit(10f);
            yield return new WaitForSeconds(2.5f);
            bool delayed = target.CurrentHealth == 960;
            while (Time.time < started + 3.5f && target.CurrentHealth == 960) yield return null;
            float elapsed = Time.time - started;
            int burst = 960 - target.CurrentHealth;
            float cueDeadline = Time.time + .25f;
            while (Time.time < cueDeadline && !GameObject.Find("Rain needletick burst")) yield return null;
            bool burstCue = GameObject.Find("Rain needletick burst") != null;
            bool burstAudio = Playing("NeedletickBurst");
            yield return new WaitForSeconds(.85f);
            bool cleared = target.CurrentHealth == 960 - burst && target.GetComponent<RangeBleeding>().ActiveDebuffCount() == 0 &&
                !GameObject.Find("Rain needletick burst");
            int expected = Mathf.RoundToInt(items.ItemBaseAttack * 4f);
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"corrupted={corrupted} zeroProc={zeroProc} marked={marked} delayed={delayed} elapsed={elapsed:F2} burst={burst} cue={burstCue} audio={burstAudio} cleared={cleared}";
            if (corrupted && zeroProc && marked && delayed && elapsed >= 3f && burst == expected && burstCue && burstAudio && cleared)
                Debug.Log("RAIN_ITEM_NEEDLETICK_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_NEEDLETICK_FAILED " + result);
        }

        static IEnumerator VerifyPolylute(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[2];
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 35f + Vector3.right * (i * 4f);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 1000;
            var original = new BrowserEntry { id = "ChainLightning", name = "Ukulele" };
            var entry = new BrowserEntry { id = "ChainLightningVoid", name = "Polylute" };
            lab.ExecuteCatalog(original);
            bool grantedOriginal = owner.ResearchUkuleles == 1;
            lab.ExecuteCatalog(entry);
            bool corrupted = grantedOriginal && owner.ResearchUkuleles == 0;
            void Hit(float coefficient) => targets[0].Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);

            Hit(0f);
            yield return new WaitForSeconds(.2f);
            bool zeroProc = targets[0].CurrentHealth == 980 && !GameObject.Find("Rain Polylute strike");
            targets[0].CurrentHealth = 1000;
            Hit(4f);
            float timeout = Time.time + 1f;
            while (Time.time < timeout && !GameObject.Find("Rain Polylute strike")) yield return null;
            bool cue = GameObject.Find("Rain Polylute strike") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "PolyluteOriginal");
            yield return new WaitForSeconds(1f);
            int main = 1000 - targets[0].CurrentHealth;
            int near = 1000 - targets[1].CurrentHealth;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            targets[0].CurrentHealth = 1000;
            Hit(4f);
            yield return new WaitForSeconds(.85f);
            bool removed = targets[0].CurrentHealth == 980 && targets[1].CurrentHealth == 1000 &&
                !GameObject.Find("Rain Polylute strike");
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"corrupted={corrupted} zeroProc={zeroProc} main={main} near={near} cue={cue} audio={audio} removed={removed}";
            if (corrupted && zeroProc && main == 20 + 6 * 12 && near == 0 && cue && audio && removed)
                Debug.Log("RAIN_ITEM_POLYLUTE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_POLYLUTE_FAILED " + result);
        }

        static IEnumerator VerifyUkulele(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[7];
            var origin = owner.transform.position + Vector3.up * 35f;
            for (int i = 0; i < targets.Length; i++)
            {
                float offset = i == 0 ? 0f : 23f + 3f * (i - 1);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    origin + Vector3.right * offset, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;

            void Hit(float coefficient)
            {
                foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 1000;
                // 25% × 4 guarantees a chain so spacing and limits can be tested without random retries.
                targets[0].Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, coefficient);
            }
            string Damage() => string.Join("/", targets.Select(target => 1000 - target.CurrentHealth));

            const string noChain = "20/0/0/0/0/0/0";
            const string three = "20/16/16/16/0/0/0";
            var entry = new BrowserEntry { id = "ChainLightning", name = "尤克里里" };
            lab.ExecuteCatalog(entry);
            Hit(4f);
            yield return new WaitForSeconds(.25f);
            string outOfRange = Damage();
            targets[1].transform.position = origin + Vector3.right * 21f;
            Hit(4f);
            yield return null;
            bool cue = GameObject.Find("Rain Ukulele arc") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "UkuleleArcOriginal");
            yield return new WaitForSeconds(.5f);
            string one = Damage();
            targets[1].transform.position = origin + Vector3.right * 23f;
            lab.ExecuteCatalog(entry);
            Hit(4f);
            yield return new WaitForSeconds(.75f);
            string two = Damage();
            Hit(0f);
            yield return new WaitForSeconds(.25f);
            string zero = Damage();
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            targets[1].transform.position = origin + Vector3.right * 21f;
            Hit(4f);
            yield return new WaitForSeconds(.25f);
            string removed = Damage();
            foreach (var target in targets) NetworkServer.Destroy(target.gameObject);
            items.Clear();

            string result = $"outOfRange={outOfRange} one={one} two={two} zero={zero} removed={removed} cue={cue} audio={audio}";
            if (outOfRange == noChain && one == three && two == "20/16/16/16/16/16/0" &&
                zero == noChain && removed == noChain && cue && audio)
                Debug.Log("RAIN_ITEM_UKULELE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_UKULELE_FAILED " + result);
        }

        static IEnumerator VerifyDeathMark(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            const string targetId = "skeleton_1h_sur";
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!target)
            {
                Debug.LogError("RAIN_ITEM_DEATH_MARK_NO_TARGET");
                yield break;
            }
            yield return null;
            target.MaxHealth = target.CurrentHealth = 1000;
            var entry = new BrowserEntry { id = "DeathMark", name = "湮灭标记" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            double statusEnd = NetworkTime.time + 20;
            target.ResearchBaubleSlowUntil = target.ResearchIceSlowUntil = target.ResearchTarUntil = statusEnd;
            int Hit(float proc)
            {
                int health = target.CurrentHealth;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, proc);
                return health - target.CurrentHealth;
            }

            bool threeOnly = Hit(1f) == 20 && Hit(0f) == 20;
            target.ResearchRootUntil = statusEnd;
            bool zeroProc = Hit(0f) == 20 && Hit(0f) == 20;
            bool trigger = Hit(1f) == 20 && Hit(0f) == 30;
            yield return null;
            bool cue = GameObject.Find("Rain death mark cue") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "DeathMarkOriginal");
            yield return new WaitForSeconds(7.2f);
            bool twoStackDuration = Hit(1f) == 30;
            yield return new WaitForSeconds(7.05f);
            bool expired = Hit(0f) == 20;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            bool removed = Hit(1f) == 20 && Hit(0f) == 20;
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            items.Clear();

            string result = $"threeOnly={threeOnly} zeroProc={zeroProc} trigger={trigger} twoStackDuration={twoStackDuration} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (threeOnly && zeroProc && trigger && twoStackDuration && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_DEATH_MARK_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_DEATH_MARK_FAILED " + result);
        }

        static IEnumerator VerifySymbioticScorpion(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[2];
            for (int i = 0; i < targets.Length; i++)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position + Vector3.up * 16f + Vector3.right * (i * 3f),
                    Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = 5000;
            int Hit(int index, float coefficient)
            {
                var target = targets[index];
                target.CurrentHealth = 5000;
                target.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, owner, 0, coefficient);
                return 5000 - target.CurrentHealth;
            }

            var entry = new BrowserEntry { id = "PermanentDebuffOnHit", name = "Symbiotic Scorpion" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int zero = Hit(0, 0f);
            int first = Hit(0, 1f);
            yield return null;
            bool cue = GameObject.Find("Rain scorpion armor loss") != null;
            int second = Hit(0, 1f);
            int stacked = Hit(0, 0f);
            lab.RemoveCatalog(entry);
            int one = Hit(0, 1f);
            int afterOne = Hit(0, 0f);
            lab.RemoveCatalog(entry);
            int retained = Hit(0, 1f);
            int removed = Hit(1, 1f);
            int cleanTarget = Hit(1, 0f);
            yield return new WaitForSeconds(.7f);
            bool cleared = !GameObject.Find("Rain scorpion armor loss");
            foreach (var target in targets) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"zero={zero} first={first} second={second} stacked={stacked} one={one}/{afterOne} retained={retained} clean={removed}/{cleanTarget} cue={cue} cleared={cleared}";
            if (zero == 1000 && first == 1000 && second == 1038 && stacked == 1074 &&
                one == stacked && afterOne == 1091 && retained == afterOne && removed == 1000 &&
                cleanTarget == 1000 && cue && cleared)
                Debug.Log("RAIN_ITEM_SCORPION_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_SCORPION_FAILED " + result);
        }

        static IEnumerator VerifyChronicExpansion(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            Vector3 position = owner.transform.position + Vector3.up * 16f;
            CharacterInstance SpawnTarget(int maximum)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), position,
                    Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                target.MaxHealth = target.CurrentHealth = maximum;
                return target;
            }
            var probe = SpawnTarget(5000);
            yield return null;
            int Hit()
            {
                probe.MaxHealth = probe.CurrentHealth = 5000;
                probe.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                return 5000 - probe.CurrentHealth;
            }
            var entry = new BrowserEntry { id = "IncreaseDamageOnMultiKill", name = "Chronic Expansion" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int baseline = Hit();
            int first = 0;
            bool cue = false;
            for (int kill = 1; kill <= 16; kill++)
            {
                var target = SpawnTarget(100);
                target.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                if (target) NetworkServer.Destroy(target.gameObject);
                if (kill != 1) continue;
                yield return null;
                cue = GameObject.Find("Rain chronic expansion") != null;
                first = Hit();
            }
            int capped = Hit();
            lab.RemoveCatalog(entry);
            int reduced = Hit();
            yield return new WaitForSeconds(5f);
            int refresh = Hit();
            yield return new WaitForSeconds(3f);
            int refreshed = Hit();
            yield return new WaitForSeconds(7.25f);
            int expired = Hit();
            lab.RemoveCatalog(entry);
            int removed = Hit();
            NetworkServer.Destroy(probe.gameObject);
            items.Clear();
            string result = $"base={baseline} first={first} capped={capped} reduced={reduced} refresh={refresh} refreshed={refreshed} expired={expired} removed={removed} cue={cue}";
            if (baseline == 1000 && first == 1045 && capped == 1675 && reduced == 1350 &&
                refresh == reduced && refreshed == reduced && expired == baseline && removed == baseline && cue)
                Debug.Log("RAIN_ITEM_CHRONIC_EXPANSION_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_CHRONIC_EXPANSION_FAILED " + result);
        }

        static IEnumerator VerifyLeechingSeed(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 500;
            target.MaxHealth = target.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "Seed", name = "Leeching Seed" };
            int Hit(float coefficient)
            {
                int before = owner.CurrentHealth;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, coefficient);
                return owner.CurrentHealth - before;
            }

            int baseline = Hit(1f);
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int zero = Hit(0f);
            int firstQuarter = Hit(.25f);
            int secondQuarter = Hit(.25f);
            int stacked = Hit(1f);
            lab.RemoveCatalog(entry);
            int single = Hit(1f);
            int pending = Hit(.5f);
            lab.RemoveCatalog(entry);
            int removed = Hit(1f);
            lab.ExecuteCatalog(entry);
            int regranted = Hit(.5f);
            lab.RemoveCatalog(entry);
            int damage = 500 - target.CurrentHealth;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"base={baseline} zero={zero} quarters={firstQuarter}/{secondQuarter} two={stacked} one={single} pending={pending} removed={removed} regranted={regranted} damage={damage}";
            if (baseline == 0 && zero == 0 && firstQuarter == 0 && secondQuarter == 1 && stacked == 2 &&
                single == 1 && pending == 0 && removed == 0 && regranted == 0 && damage == 180)
                Debug.Log("RAIN_ITEM_LEECHING_SEED_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_LEECHING_SEED_FAILED " + result);
        }

        static IEnumerator VerifyHarvestersScythe(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            target.MaxHealth = 500;
            var entry = new BrowserEntry { id = "HealOnCrit", name = "Harvester's Scythe" };
            (int damage, int healing) Hit(float coefficient)
            {
                owner.CurrentHealth = 500;
                target.CurrentHealth = 500;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, coefficient);
                return (500 - target.CurrentHealth, owner.CurrentHealth - 500);
            }

            float baselineCrit = items.CriticalChance;
            lab.ExecuteCatalog(entry);
            float firstCrit = items.CriticalChance;
            lab.ExecuteCatalog(entry);
            float twoCrit = items.CriticalChance;
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            for (int i = 0; i < 10; i++) items.Grant(glasses);
            var zero = Hit(0f);
            var half = Hit(.5f);
            var full = Hit(1f);
            yield return null;
            bool cue = GameObject.Find("Rain scythe heal") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "ScytheHealOriginal");
            lab.RemoveCatalog(entry);
            var single = Hit(1f);
            lab.RemoveCatalog(entry);
            var removed = Hit(1f);
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"crit={baselineCrit:F2}/{firstCrit:F2}/{twoCrit:F2} zero={zero} half={half} full={full} single={single} removed={removed} cue={cue} audio={audio}";
            if (Mathf.Approximately(firstCrit - baselineCrit, .05f) && Mathf.Approximately(twoCrit, firstCrit) &&
                zero == (40, 0) && half == (40, 6) && full == (40, 12) &&
                single == (40, 8) && removed == (40, 0) && cue && audio)
                Debug.Log("RAIN_ITEM_SCYTHE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_SCYTHE_FAILED " + result);
        }

        static IEnumerator VerifyChronobauble(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            var motor = target.GetComponent<CharacterMotor>();
            motor.enabled = false;
            yield return null;
            target.MaxHealth = target.CurrentHealth = 500;
            float baseline = motor.GetMoveSpeed(false);
            var entry = new BrowserEntry { id = "SlowOnHit", name = "Chronobauble" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(float coefficient) => target.Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);

            Hit(0f);
            bool zero = target.ResearchBaubleSlowUntil <= NetworkTime.time;
            Hit(1f);
            double duration = target.ResearchBaubleSlowUntil - NetworkTime.time;
            float slowed = motor.GetMoveSpeed(false);
            yield return null;
            bool cue = GameObject.Find("Rain chronobauble slow") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "ChronobaubleOriginal");
            lab.RemoveCatalog(entry);
            double previousExpiry = target.ResearchBaubleSlowUntil;
            Hit(1f);
            bool preserved = target.ResearchBaubleSlowUntil == previousExpiry;
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(4.2f);
            bool expired = Mathf.Approximately(motor.GetMoveSpeed(false), baseline);
            Hit(1f);
            yield return null;
            bool removed = target.ResearchBaubleSlowUntil <= NetworkTime.time &&
                !GameObject.Find("Rain chronobauble slow") && target.CurrentHealth == 420;
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"zero={zero} base={baseline:F2} slowed={slowed:F2} duration={duration:F2} preserved={preserved} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (zero && baseline > 0 && Mathf.Approximately(slowed, baseline / 1.6f) &&
                duration > 3.95 && duration <= 4.01 && preserved && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_CHRONOBAUBLE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_CHRONOBAUBLE_FAILED " + result);
        }

        static IEnumerator VerifyTentabauble(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            var motor = target.GetComponent<CharacterMotor>();
            motor.enabled = false;
            yield return null;
            target.MaxHealth = target.CurrentHealth = 500;
            float speed = motor.GetMoveSpeed(false);
            var chrono = new BrowserEntry { id = "SlowOnHit", name = "Chronobauble" };
            var tenta = new BrowserEntry { id = "SlowOnHitVoid", name = "Tentabauble" };
            lab.ExecuteCatalog(chrono);
            bool acquired = owner.ResearchBaubles == 1;
            lab.ExecuteCatalog(tenta);
            bool corrupted = acquired && owner.ResearchBaubles == 0;
            void Hit(float coefficient) => target.Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);

            Hit(0f);
            bool zero = !target.ResearchRooted;
            Hit(11f);
            double duration = target.ResearchRootUntil - NetworkTime.time;
            bool rooted = target.ResearchRooted && motor.GetMoveSpeed(false) == 0f &&
                target.ResearchBaubleSlowUntil <= NetworkTime.time;
            yield return null;
            bool cue = GameObject.Find("Rain tentabauble root") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "TentabaubleOriginal");
            lab.RemoveCatalog(tenta);
            lab.RemoveCatalog(tenta);
            bool retained = target.ResearchRooted;
            yield return new WaitForSeconds(2.2f);
            bool expired = !target.ResearchRooted && Mathf.Approximately(motor.GetMoveSpeed(false), speed);
            Hit(21f);
            yield return null;
            bool removed = !target.ResearchRooted && !GameObject.Find("Rain tentabauble root") &&
                target.CurrentHealth == 440;
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"corrupted={corrupted} zero={zero} rooted={rooted} duration={duration:F2} retained={retained} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (corrupted && zero && rooted && duration > 1.95 && duration <= 2.01 &&
                retained && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_TENTABAUBLE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_TENTABAUBLE_FAILED " + result);
        }

        static IEnumerator VerifyLostSeers(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var boss = new BrowserEntry { id = "BEETLEQUEEN_BODY", category = 1 };
            lab.ExecuteCatalog(boss);
            yield return null;
            var actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                .FirstOrDefault(enemy => enemy.IsLabSummon && enemy.IsBoss);
            var queen = actor ? actor.GetComponent<CharacterInstance>() : null;
            if (!queen)
            {
                lab.RemoveCatalog(boss);
                Debug.LogError("RAIN_ITEM_LOST_SEERS_NO_QUEEN");
                yield break;
            }

            var targets = new CharacterInstance[2];
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 16f + Vector3.right * (i * 3f);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), position,
                    Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 500;
            queen.MaxHealth = queen.CurrentHealth = 500;
            var glasses = new BrowserEntry { id = "CritGlasses", name = "Lens-Maker's Glasses" };
            var seers = new BrowserEntry { id = "CritGlassesVoid", name = "Lost Seer's Lenses" };
            lab.ExecuteCatalog(glasses);
            bool acquired = items.Glasses == 1;
            lab.ExecuteCatalog(seers);
            bool corrupted = acquired && items.Glasses == 0 && Mathf.Approximately(items.CriticalChance, 0f);
            void Hit(CharacterInstance target, float coefficient) =>
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, coefficient);

            Hit(queen, 110f);
            int bossDamage = 500 - queen.CurrentHealth;
            Hit(targets[0], 0f);
            bool zeroProc = targets[0].CurrentHealth == 480;
            Hit(targets[0], 110f);
            bool executed = !targets[0] || targets[0].CurrentHealth == 0;
            yield return null;
            bool cue = GameObject.Find("Rain lost seers execution") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "LostSeersExecution");
            lab.RemoveCatalog(seers);
            lab.RemoveCatalog(seers);
            Hit(targets[1], 110f);
            int removedDamage = 500 - targets[1].CurrentHealth;
            lab.RemoveCatalog(boss);
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"corrupted={corrupted} bossDamage={bossDamage} zeroProc={zeroProc} executed={executed} removedDamage={removedDamage} cue={cue} audio={audio}";
            if (corrupted && bossDamage == 20 && zeroProc && executed && removedDamage == 20 && cue && audio)
                Debug.Log("RAIN_ITEM_LOST_SEERS_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_LOST_SEERS_FAILED " + result);
        }

        static IEnumerator VerifyLensMakerGlasses(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            const string targetId = "skeleton_1h_sur";
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!target)
            {
                Debug.LogError("RAIN_ITEM_GLASSES_NO_TARGET");
                yield break;
            }
            yield return null;
            target.MaxHealth = 1000;
            int Hit()
            {
                target.CurrentHealth = 1000;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
                return 1000 - target.CurrentHealth;
            }

            var entry = new BrowserEntry { id = "CritGlasses", name = "透镜制作者的眼镜" };
            int baseline = Hit();
            lab.ExecuteCatalog(entry);
            float one = items.CriticalChance;
            for (int i = 0; i < 10; i++) lab.ExecuteCatalog(entry);
            float capped = items.CriticalChance;
            int critical = Hit();
            for (int i = 0; i < 11; i++) lab.RemoveCatalog(entry);
            int removed = Hit();
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            items.Clear();
            string result = $"baseline={baseline} chanceOne={one:F2} chanceCapped={capped:F2} critical={critical} removed={removed}";
            if (baseline == 20 && Mathf.Approximately(one, .1f) && Mathf.Approximately(capped, 1f) && critical == 40 && removed == 20)
                Debug.Log("RAIN_ITEM_GLASSES_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_GLASSES_FAILED " + result);
        }

        static IEnumerator VerifyCrowbar(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            const string targetId = "skeleton_1h_sur";
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!target)
            {
                Debug.LogError("RAIN_ITEM_CROWBAR_NO_TARGET");
                yield break;
            }
            yield return null;
            target.MaxHealth = 1000;
            int Hit(int health)
            {
                target.CurrentHealth = health;
                int shield = target.ResearchShield;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
                return health - target.CurrentHealth + shield - target.ResearchShield;
            }

            var entry = new BrowserEntry { id = "Crowbar", name = "撬棍" };
            lab.ExecuteCatalog(entry);
            int below = Hit(899);
            int threshold = Hit(900);
            lab.ExecuteCatalog(entry);
            int two = Hit(1000);
            lab.RemoveCatalog(entry);
            var defender = target.gameObject.AddComponent<RangeResearchItems>();
            defender.Grant(RangeResearchItems.CatalogIndex("PersonalShield"));
            int shieldDepleted = Hit(1000);
            bool initialShield = target.ResearchShield == 45;
            target.AbsorbResearchShield(45);
            int afterShield = Hit(970);
            lab.RemoveCatalog(entry);
            int removed = Hit(1000);
            yield return null;
            bool cue = GameObject.Find("Rain crowbar impact") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "CrowbarOriginal");
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            items.Clear();
            string result = $"below={below} threshold={threshold} two={two} fullShield={shieldDepleted} shieldConsumed={initialShield} emptyShield={afterShield} removed={removed} cue={cue} audio={audio}";
            if (below == 20 && threshold == 35 && two == 50 && shieldDepleted == 35 && initialShield &&
                afterShield == 20 && removed == 20 && cue && audio)
                Debug.Log("RAIN_ITEM_CROWBAR_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_CROWBAR_FAILED " + result);
        }

        static IEnumerator VerifyClover(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[2];
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 25f + Vector3.right * (4f * i);
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;

            var entry = new BrowserEntry { id = "Clover", name = "57片叶子的三叶草" };
            lab.ExecuteCatalog(new BrowserEntry { id = "ChainLightning", name = "尤克里里" });
            var savedRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(17);
            UnityEngine.Random.State roll = default;
            bool found = false;
            for (int attempt = 0; attempt < 1000 && !found; attempt++)
            {
                roll = UnityEngine.Random.state;
                _ = UnityEngine.Random.value; // Crit check.
                float first = UnityEngine.Random.value;
                float second = UnityEngine.Random.value;
                float third = UnityEngine.Random.value;
                found = first >= .25f && second >= .25f && third < .25f;
            }
            UnityEngine.Random.state = savedRandom;

            void Hit()
            {
                foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 1000;
                var saved = UnityEngine.Random.state;
                UnityEngine.Random.state = roll;
                targets[0].Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 1f);
                UnityEngine.Random.state = saved;
            }
            int Damage() => 1000 - targets[1].CurrentHealth;
            Hit();
            yield return new WaitForSeconds(.22f);
            int none = Damage();
            lab.ExecuteCatalog(entry);
            Hit();
            yield return new WaitForSeconds(.22f);
            int one = Damage();
            lab.ExecuteCatalog(entry);
            Hit();
            yield return new WaitForSeconds(.22f);
            int two = Damage();
            bool cue = GameObject.Find("Rain clover reroll cue") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "CloverRerollOriginal");
            lab.RemoveCatalog(entry);
            Hit();
            yield return new WaitForSeconds(.22f);
            int remaining = Damage();
            lab.RemoveCatalog(entry);
            Hit();
            yield return new WaitForSeconds(.22f);
            int removed = Damage();
            foreach (var target in targets) NetworkServer.Destroy(target.gameObject);
            items.Clear();

            string result = $"sequence={found} damage={none}/{one}/{two}/{remaining}/{removed} cue={cue} audio={audio}";
            if (found && none == 0 && one == 0 && two == 16 && remaining == 0 && removed == 0 && cue && audio)
                Debug.Log("RAIN_ITEM_CLOVER_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_CLOVER_FAILED " + result);
        }

        static IEnumerator VerifyWarHorn(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            var player = GameManager.myPlayerInstance;
            if (!player)
            {
                Debug.LogError("RAIN_ITEM_WAR_HORN_NO_PLAYER");
                yield break;
            }
            items.Clear();
            var horn = new BrowserEntry { id = "EnergizedOnEquipmentUse", name = "战争号角" };
            var jade = new BrowserEntry { id = "GainArmor", name = "玉象" };
            var fruit = new BrowserEntry { id = "Fruit", name = "外来水果" };
            int initialHealth = owner.CurrentHealth;
            lab.ExecuteCatalog(horn);
            lab.ExecuteCatalog(horn);
            bool noEquipment = !player.ServerTryUseRangeEquipment() && !owner.ResearchWarHornActive;
            lab.ExecuteCatalog(jade);
            bool activated = player.ServerTryUseRangeEquipment() && Mathf.Approximately(owner.ResearchAttackSpeed, 1.7f);
            bool cooldown = !player.ServerTryUseRangeEquipment();
            yield return null;
            bool cue = GameObject.Find("Rain war horn cue") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "WarHornOriginal");
            lab.RemoveCatalog(horn);
            lab.RemoveCatalog(horn);
            yield return new WaitForSeconds(8.2f);
            bool twoStackDuration = Mathf.Approximately(owner.ResearchAttackSpeed, 1.7f);
            yield return new WaitForSeconds(4.1f);
            bool expired = Mathf.Approximately(owner.ResearchAttackSpeed, 1f);
            lab.ExecuteCatalog(fruit);
            bool removed = player.ServerTryUseRangeEquipment() && !owner.ResearchWarHornActive;
            owner.CurrentHealth = initialHealth;
            lab.RemoveCatalog(fruit);
            items.Clear();
            string result = $"noEquipment={noEquipment} activated={activated} cooldown={cooldown} twoStackDuration={twoStackDuration} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (noEquipment && activated && cooldown && twoStackDuration && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_WAR_HORN_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_WAR_HORN_FAILED " + result);
        }

        static IEnumerator VerifyOcularHud(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var player = GameManager.myPlayerInstance;
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            target.MaxHealth = 500;
            int Hit()
            {
                target.CurrentHealth = 500;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                return 500 - target.CurrentHealth;
            }
            bool Playing(string clip) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == clip);

            var entry = new BrowserEntry { id = "CritOnUse", name = "Ocular HUD" };
            lab.ExecuteCatalog(entry);
            int baseline = Hit();
            bool used = player && player.ServerTryUseRangeEquipment();
            float started = Time.time;
            float cooldown = items.EquipmentCooldownRemaining;
            int active = Hit();
            bool blocked = player && !player.ServerTryUseRangeEquipment();
            yield return null;
            bool cue = GameObject.Find("Rain ocular hud active") != null;
            bool startAudio = Playing("OcularHudStart");
            yield return new WaitUntil(() => Time.time >= started + 7.75f);
            int beforeExpiry = Hit();
            yield return new WaitUntil(() => Time.time >= started + 8.2f);
            int expired = Hit();
            bool endAudio = Playing("OcularHudEnd");
            lab.RemoveCatalog(entry);
            bool removed = items.Equipped == RangeResearchItems.Equipment.None && player &&
                !player.ServerTryUseRangeEquipment() && !GameObject.Find("Rain ocular hud active");
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"used={used} damage={baseline}/{active}/{beforeExpiry}/{expired} cooldown={cooldown:F2} blocked={blocked} removed={removed} cue={cue} audio={startAudio}/{endAudio}";
            if (used && baseline == 20 && active == 40 && beforeExpiry == 40 && expired == baseline &&
                Mathf.Abs(cooldown - 60f) < .05f && blocked && removed && cue && startAudio && endAudio)
                Debug.Log("RAIN_ITEM_OCULAR_HUD_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_OCULAR_HUD_FAILED " + result);
        }

        static IEnumerator VerifyJadeElephant(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var player = GameManager.myPlayerInstance;
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            var entry = new BrowserEntry { id = "GainArmor", name = "Jade Elephant" };
            var fruit = new BrowserEntry { id = "Fruit", name = "Foreign Fruit" };
            int Hit()
            {
                owner.CurrentHealth = 500;
                owner.Server_ChangeHealthStateRaw(120, CharacterPart.body, AttackType.hitscan, attacker, 0, 0f);
                return 500 - owner.CurrentHealth;
            }

            lab.ExecuteCatalog(entry);
            int baseline = Hit();
            bool used = player && player.ServerTryUseRangeEquipment();
            float started = Time.time;
            float cooldown = items.EquipmentCooldownRemaining;
            int armored = Hit();
            bool blocked = player && !player.ServerTryUseRangeEquipment();
            yield return null;
            bool cue = GameObject.Find("Rain jade elephant armor") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "JadeElephantOriginal");
            lab.ExecuteCatalog(fruit);
            lab.ExecuteCatalog(entry);
            bool kept = items.EquipmentCooldownRemaining > 44f && player && !player.ServerTryUseRangeEquipment();
            yield return new WaitUntil(() => Time.time >= started + 4.75f);
            int beforeExpiry = Hit();
            yield return new WaitUntil(() => Time.time >= started + 5.25f);
            int expired = Hit();
            lab.RemoveCatalog(entry);
            bool removed = items.Equipped == RangeResearchItems.Equipment.None && player &&
                !player.ServerTryUseRangeEquipment() && !GameObject.Find("Rain jade elephant armor");
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"used={used} base={baseline} armored={armored} beforeExpiry={beforeExpiry} expired={expired} cooldown={cooldown:F2} blocked={blocked} kept={kept} removed={removed} cue={cue} audio={audio}";
            if (used && baseline == 120 && armored == 20 && beforeExpiry == 20 && expired == baseline &&
                Mathf.Abs(cooldown - 45f) < .05f && blocked && kept && removed && cue && audio)
                Debug.Log("RAIN_ITEM_JADE_ELEPHANT_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_JADE_ELEPHANT_FAILED " + result);
        }

        static IEnumerator VerifyForeignFruit(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var player = GameManager.myPlayerInstance;
            if (!player)
            {
                Debug.LogError("RAIN_ITEM_FRUIT_NO_PLAYER");
                yield break;
            }
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            float originalRegen = owner.RainHealthRegen;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 250;
            owner.RainHealthRegen = 0f;
            var fruit = new BrowserEntry { id = "Fruit", name = "Foreign Fruit" };
            var elephant = new BrowserEntry { id = "GainArmor", name = "Jade Elephant" };
            lab.ExecuteCatalog(fruit);
            bool used = player.ServerTryUseRangeEquipment();
            int healed = owner.CurrentHealth - 250;
            float cooldown = items.FruitCooldownRemaining;
            bool blocked = !player.ServerTryUseRangeEquipment() && owner.CurrentHealth == 750;
            yield return null;
            bool cue = GameObject.Find("Rain foreign fruit heal") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "ForeignFruitOriginal");
            lab.ExecuteCatalog(elephant);
            lab.ExecuteCatalog(fruit);
            bool kept = items.FruitCooldownRemaining > 44f && !player.ServerTryUseRangeEquipment();
            lab.RemoveCatalog(fruit);
            bool removed = items.Equipped == RangeResearchItems.Equipment.None && !player.ServerTryUseRangeEquipment();
            yield return new WaitForSeconds(.75f);
            bool cleared = !GameObject.Find("Rain foreign fruit heal");
            owner.RainHealthRegen = originalRegen;
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            items.Clear();
            string result = $"used={used} healed={healed} cooldown={cooldown:F2} blocked={blocked} kept={kept} removed={removed} cue={cue} audio={audio} cleared={cleared}";
            if (used && healed == 500 && Mathf.Abs(cooldown - 45f) < .05f &&
                blocked && kept && removed && cue && audio && cleared)
                Debug.Log("RAIN_ITEM_FRUIT_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_FRUIT_FAILED " + result);
        }

        IEnumerator VerifySoulboundCatalyst(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var player = GameManager.myPlayerInstance;
            var fruit = new BrowserEntry { id = "Fruit", name = "Foreign Fruit" };
            var catalyst = new BrowserEntry { id = "Talisman", name = "Soulbound Catalyst" };
            int originalHealth = owner.CurrentHealth;
            owner.CurrentHealth = Mathf.Max(1, owner.MaxHealth / 4);
            lab.ExecuteCatalog(fruit);
            lab.ExecuteCatalog(catalyst);
            lab.ExecuteCatalog(catalyst);
            bool used = player && player.ServerTryUseRangeEquipment();
            float initial = items.FruitCooldownRemaining;
            bool twoKills = HitAndKillSmokeTarget(owner, items);
            float two = initial - items.FruitCooldownRemaining;
            yield return null;
            bool cue = GameObject.Find("Rain soulbound catalyst") != null;
            lab.RemoveCatalog(catalyst);
            float previous = items.FruitCooldownRemaining;
            bool oneKill = HitAndKillSmokeTarget(owner, items);
            float one = previous - items.FruitCooldownRemaining;
            lab.RemoveCatalog(catalyst);
            yield return new WaitForSeconds(.7f);
            previous = items.FruitCooldownRemaining;
            bool removedKill = HitAndKillSmokeTarget(owner, items);
            float removed = previous - items.FruitCooldownRemaining;
            yield return null;
            bool cleared = !GameObject.Find("Rain soulbound catalyst");
            owner.CurrentHealth = originalHealth;
            items.Clear();

            string result = $"used={used} initial={initial:F2} kills={twoKills}/{oneKill}/{removedKill} reductions={two:F2}/{one:F2}/{removed:F2} cue={cue} cleared={cleared}";
            if (used && initial > 44.9f && twoKills && oneKill && removedKill && Mathf.Abs(two - 6f) < .05f &&
                Mathf.Abs(one - 4f) < .05f && Mathf.Abs(removed) < .05f && cue && cleared)
                Debug.Log("RAIN_ITEM_SOULBOUND_CATALYST_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_SOULBOUND_CATALYST_FAILED " + result);
        }

        static IEnumerator VerifyFuelCells(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            var player = GameManager.myPlayerInstance;
            if (!player)
            {
                Debug.LogError("RAIN_ITEM_FUEL_CELL_NO_PLAYER");
                yield break;
            }
            items.Clear();
            var cell = new BrowserEntry { id = "EquipmentMagazine", name = "燃料电池" };
            var fruit = new BrowserEntry { id = "Fruit", name = "外来水果" };
            var jade = new BrowserEntry { id = "GainArmor", name = "玉象" };
            int originalHealth = owner.CurrentHealth;
            int injured = Mathf.Max(1, owner.MaxHealth / 4);
            int healed = injured + Mathf.CeilToInt(owner.MaxHealth * .5f);
            bool UseFruit()
            {
                owner.CurrentHealth = injured;
                return player.ServerTryUseRangeEquipment() && owner.CurrentHealth == healed;
            }

            lab.ExecuteCatalog(cell);
            lab.ExecuteCatalog(cell);
            lab.ExecuteCatalog(fruit);
            int uses = 0;
            for (int i = 0; i < 3; i++)
                if (UseFruit()) uses++;
            bool blocked = !UseFruit();
            float cooldown = items.EquipmentCooldownRemaining;
            lab.ExecuteCatalog(jade);
            bool independent = player.ServerTryUseRangeEquipment();
            lab.ExecuteCatalog(fruit);
            bool debtKept = Mathf.Abs(cooldown - items.EquipmentCooldownRemaining) < .02f && !UseFruit();
            float started = Time.time;
            while (items.EquipmentCooldownRemaining > 0f && Time.time < started + 34f) yield return null;
            float rechargedAt = Time.time - started;
            bool recharged = UseFruit() && !UseFruit();
            lab.RemoveCatalog(cell);
            float one = items.EquipmentCooldownRemaining;
            lab.RemoveCatalog(cell);
            float zero = items.EquipmentCooldownRemaining;
            bool stillBlocked = !UseFruit();
            lab.RemoveCatalog(fruit);
            owner.CurrentHealth = originalHealth;
            items.Clear();

            string result = $"uses={uses} blocked={blocked} cooldown={cooldown:F2} swap={independent && debtKept} recharge={rechargedAt:F2} recharged={recharged} removedCooldown={one:F2}/{zero:F2} stillBlocked={stillBlocked}";
            if (uses == 3 && blocked && Mathf.Abs(cooldown - 32.5125f) < .05f && independent && debtKept &&
                Mathf.Abs(rechargedAt - cooldown) < .3f && recharged && stillBlocked &&
                Mathf.Abs(one - 76.5f) < .3f && Mathf.Abs(zero - 135f) < .3f)
                Debug.Log("RAIN_ITEM_FUEL_CELL_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_FUEL_CELL_FAILED " + result);
        }

        static IEnumerator VerifyIceRing(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            const string targetId = "skeleton_1h_sur";
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, targetId);
            if (!target)
            {
                Debug.LogError("RAIN_ITEM_ICE_RING_NO_TARGET");
                yield break;
            }
            yield return null;
            target.MaxHealth = 1000;
            int Hit(int damage, float proc = 1f)
            {
                target.CurrentHealth = 1000;
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, owner, 0, proc);
                return 1000 - target.CurrentHealth;
            }
            var entry = new BrowserEntry { id = "IceRing", name = "鲁纳德的手环" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int threshold = Mathf.CeilToInt(4f * items.ItemBaseAttack);
            int below = Hit(threshold - 1);
            int zeroProc = Hit(threshold, 0f);
            int two = Hit(threshold);
            var motor = target.GetComponent<CharacterMotor>();
            bool slow = target.ResearchIceSlowUntil - NetworkTime.time > 5.9d &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed / 1.8f);
            int cooldown = Hit(threshold);
            yield return null;
            bool cue = GameObject.Find("Rain ice ring cue") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "IceRingOriginal");
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(6.15f);
            bool slowExpired = target.ResearchIceSlowUntil <= NetworkTime.time;
            bool stillCooling = Hit(threshold) == threshold;
            yield return new WaitForSeconds(4.05f);
            int one = Hit(threshold);
            lab.RemoveCatalog(entry);
            bool removed = Hit(threshold) == threshold && owner.ResearchIceRings == 0;
            Jotunnslayer.JotunnMonsterGm.Remove(owner, targetId);
            items.Clear();
            string result = $"threshold={threshold} below={below} zero={zeroProc} two={two} one={one} cooldown={cooldown} slow={slow} expired={slowExpired} stillCooling={stillCooling} removed={removed} cue={cue} audio={audio}";
            if (below == threshold - 1 && zeroProc == threshold && two == threshold * 6 &&
                one == Mathf.RoundToInt(threshold * 3.5f) && cooldown == threshold && slow && slowExpired &&
                stillCooling && removed && cue && audio)
                Debug.Log("RAIN_ITEM_ICE_RING_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_ICE_RING_FAILED " + result);
        }

        static IEnumerator VerifyFireRing(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[2];
            Vector3 origin = owner.transform.position + Vector3.up * 16f + Vector3.right * 6f;
            for (int i = 0; i < targets.Length; i++)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    origin + Vector3.right * (11.5f * i), Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 1000;
            var entry = new BrowserEntry { id = "FireRing", name = "贾罗的手环" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int threshold = Mathf.CeilToInt(4f * items.ItemBaseAttack);
            int Hit(int damage, float proc = 1f)
            {
                int before = targets[0].CurrentHealth;
                targets[0].Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, owner, 0, proc);
                return before - targets[0].CurrentHealth;
            }

            bool noEarly = Hit(threshold - 1) == threshold - 1 && Hit(threshold, 0f) == threshold &&
                !GameObject.Find("Rain fire tornado cue");
            targets[0].CurrentHealth = 1000;
            bool immediate = Hit(threshold) == threshold && Hit(threshold) == threshold;
            yield return null;
            var cue = GameObject.Find("Rain fire tornado cue");
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "FireRingOriginal");
            yield return new WaitForSeconds(.55f);
            bool firstTick = targets[0].CurrentHealth == 1000 - threshold * 3 && targets[1].CurrentHealth == 1000;
            bool advanced = cue && cue.transform.position.x > origin.x + .8f;
            yield return new WaitForSeconds(2.7f);
            int primary = 1000 - targets[0].CurrentHealth;
            int reached = 1000 - targets[1].CurrentHealth;
            bool cleared = !cue;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(7f);
            bool removed = Hit(threshold) == threshold && !GameObject.Find("Rain fire tornado cue") &&
                owner.ResearchFireRings == 0;
            foreach (var target in targets) NetworkServer.Destroy(target.gameObject);
            items.Clear();

            string result = $"threshold={threshold} noEarly={noEarly} immediate={immediate} firstTick={firstTick} advanced={advanced} primary={primary} reached={reached} cleared={cleared} removed={removed} audio={audio}";
            if (noEarly && immediate && firstTick && advanced && primary == threshold * 8 &&
                reached == threshold * 3 && cleared && removed && audio)
                Debug.Log("RAIN_ITEM_FIRE_RING_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_FIRE_RING_FAILED " + result);
        }

        static IEnumerator VerifyCeremonialDagger(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var bodies = new CharacterInstance[3];
            Vector3 origin = owner.transform.position + Vector3.up * 16f;
            for (int i = 0; i < bodies.Length; i++)
            {
                float offset = i == 0 ? 0f : i == 1 ? 10f : 24f;
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    origin + Vector3.right * offset, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                bodies[i] = target;
            }
            yield return null;
            foreach (var target in bodies) target.MaxHealth = target.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "Dagger", name = "仪式用匕首" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            bool Playing(string clip) => FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == clip);
            void Hit(CharacterInstance target, int damage) =>
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);

            Hit(bodies[0], 20);
            bool nonLethal = !GameObject.Find("Rain ceremonial dagger blade");
            Hit(bodies[0], 600);
            bool delayed = bodies[1].CurrentHealth == 500;
            yield return null;
            int trails = FindObjectsByType<LineRenderer>(FindObjectsSortMode.None)
                .Count(line => line.name == "Rain ceremonial dagger blade");
            bool spawnAudio = Playing("CeremonialDaggerSpawn");
            bodies[1].transform.position += Vector3.forward * 6f;
            yield return new WaitForSeconds(.65f);
            int damage = 500 - bodies[1].CurrentHealth;
            bool impactAudio = Playing("CeremonialDaggerImpact");
            int expected = 3 * Mathf.RoundToInt(3f * items.ItemBaseAttack);
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            Hit(bodies[2], 600);
            yield return new WaitForSeconds(.8f);
            bool removed = bodies[1].CurrentHealth == 500 - damage && !GameObject.Find("Rain ceremonial dagger blade");
            foreach (var target in bodies)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"nonLethal={nonLethal} delayed={delayed} trails={trails} damage={damage} expected={expected} spawnAudio={spawnAudio} impactAudio={impactAudio} removed={removed}";
            if (nonLethal && delayed && trails == 3 && damage == expected && spawnAudio && impactAudio && removed)
                Debug.Log("RAIN_ITEM_CEREMONIAL_DAGGER_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_CEREMONIAL_DAGGER_FAILED " + result);
        }

        static IEnumerator VerifyWispItem(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[5];
            float[] offsets = { 0f, 6f, 10f, 15f, 18f };
            for (int i = 0; i < targets.Length; i++)
            {
                var position = owner.transform.position + Vector3.up * 16f + Vector3.right * offsets[i];
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), position,
                    Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "ExplodeOnDeath", name = "鬼火" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(CharacterInstance target, int damage) =>
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            Hit(targets[0], 20);
            bool nonLethal = !GameObject.Find("Rain wisp item warning");
            Hit(targets[0], 600);
            yield return new WaitForSeconds(.25f);
            bool delayed = targets[1].CurrentHealth == 500 && targets[2].CurrentHealth == 500;
            bool warning = GameObject.Find("Rain wisp item warning") != null;
            yield return new WaitForSeconds(.4f);
            int near = 500 - targets[1].CurrentHealth;
            int outer = 500 - targets[2].CurrentHealth;
            int far = 500 - targets[3].CurrentHealth;
            bool blast = GameObject.Find("Rain wisp item blast") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "WispItemExplosion");
            int expected = Mathf.RoundToInt(items.ItemBaseAttack * 3.5f * 1.8f);
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            Hit(targets[4], 600);
            yield return new WaitForSeconds(.65f);
            bool removed = targets[3].CurrentHealth == 500 && !GameObject.Find("Rain wisp item blast");
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"nonLethal={nonLethal} delayed={delayed} near={near} outer={outer} far={far} warning={warning} blast={blast} audio={audio} removed={removed}";
            if (nonLethal && delayed && near == expected && outer == Mathf.RoundToInt(expected * .25f) &&
                far == 0 && warning && blast && audio && removed)
                Debug.Log("RAIN_ITEM_WISP_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_WISP_FAILED " + result);
        }

        static IEnumerator VerifyVoidsentFlame(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[3];
            float[] offsets = { 0f, 13f, 16f };
            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 position = owner.transform.position + Vector3.up * 16f + Vector3.right * offsets[i];
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 500;
            var wisp = new BrowserEntry { id = "ExplodeOnDeath", name = "Will-o'-the-wisp" };
            var entry = new BrowserEntry { id = "ExplodeOnDeathVoid", name = "Voidsent Flame" };
            lab.ExecuteCatalog(wisp);
            bool grantedWisp = owner.ResearchWisps == 1;
            lab.ExecuteCatalog(entry);
            bool corrupted = grantedWisp && owner.ResearchWisps == 0;
            void Hit(float proc) => targets[0].Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, proc);

            Hit(0f);
            yield return new WaitForSeconds(.1f);
            bool zeroProc = targets[0].CurrentHealth == 480 && targets[1].CurrentHealth == 500 &&
                !GameObject.Find("Rain voidsent flame blast");
            targets[0].CurrentHealth = 500;
            Hit(1f);
            yield return new WaitForSeconds(.1f);
            int main = 500 - targets[0].CurrentHealth;
            int near = 500 - targets[1].CurrentHealth;
            int far = 500 - targets[2].CurrentHealth;
            bool cue = GameObject.Find("Rain voidsent flame blast") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "VoidsentFlameOriginal");
            Hit(1f);
            yield return new WaitForSeconds(.1f);
            bool noRepeat = targets[0].CurrentHealth == 500 - main - 20 && targets[1].CurrentHealth == 500 - near;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            targets[0].CurrentHealth = 500;
            Hit(1f);
            yield return new WaitForSeconds(1f);
            bool removed = targets[0].CurrentHealth == 480 && targets[1].CurrentHealth == 500 - near &&
                targets[2].CurrentHealth == 500 && !GameObject.Find("Rain voidsent flame blast");
            int expected = Mathf.RoundToInt(items.ItemBaseAttack * 4.16f);
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"corrupted={corrupted} zeroProc={zeroProc} main={main} near={near} far={far} cue={cue} audio={audio} noRepeat={noRepeat} removed={removed}";
            if (corrupted && zeroProc && main == expected + 20 && near == expected && far == 0 && cue && audio && noRepeat && removed)
                Debug.Log("RAIN_ITEM_VOIDSENT_FLAME_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_VOIDSENT_FLAME_FAILED " + result);
        }

        static IEnumerator VerifyHealingPotion(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var attacker = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in attacker.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            attacker.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(attacker.gameObject);
            attacker.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 270;
            var potion = new BrowserEntry { id = "HealingPotion", name = "Power Elixir" };
            var bottle = new BrowserEntry { id = "HealingPotionConsumed", name = "Empty Bottle" };
            void Hit(int damage) => owner.Server_ChangeHealthStateRaw(damage, CharacterPart.body,
                AttackType.hitscan, attacker, 0, 0f);

            lab.ExecuteCatalog(potion);
            lab.ExecuteCatalog(potion);
            Hit(19);
            bool above = owner.CurrentHealth == 251 && items.HealingPotions == 2 && items.EmptyBottles == 0;
            Hit(1);
            bool first = owner.CurrentHealth == 1000 && items.HealingPotions == 1 && items.EmptyBottles == 1;
            yield return null;
            bool cue = GameObject.Find("Rain healing potion") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "HealingPotionOriginal");
            owner.CurrentHealth = 270;
            Hit(40);
            bool second = owner.CurrentHealth == 980 && items.HealingPotions == 0 && items.EmptyBottles == 2;
            owner.CurrentHealth = 270;
            Hit(40);
            bool spent = owner.CurrentHealth == 230 && items.EmptyBottles == 2;
            lab.RemoveCatalog(bottle);
            lab.RemoveCatalog(bottle);
            lab.ExecuteCatalog(bottle);
            owner.CurrentHealth = 270;
            Hit(40);
            bool inert = owner.CurrentHealth == 230 && items.EmptyBottles == 1;
            lab.RemoveCatalog(bottle);
            lab.ExecuteCatalog(potion);
            lab.RemoveCatalog(potion);
            owner.CurrentHealth = 270;
            Hit(40);
            yield return new WaitForSeconds(.8f);
            bool removed = owner.CurrentHealth == 230 && items.HealingPotions == 0 && items.EmptyBottles == 0 &&
                !GameObject.Find("Rain healing potion");

            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(attacker.gameObject);
            items.Clear();
            string result = $"above={above} first={first} second={second} spent={spent} inert={inert} removed={removed} cue={cue} audio={audio}";
            if (above && first && second && spent && inert && removed && cue && audio)
                Debug.Log("RAIN_ITEM_HEALING_POTION_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_HEALING_POTION_FAILED " + result);
        }

        static IEnumerator VerifyDelicateWatch(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            owner.MaxHealth = 1000;
            owner.CurrentHealth = 270;
            target.MaxHealth = 500;
            var intact = new BrowserEntry { id = "FragileDamageBonus", name = "Delicate Watch" };
            var broken = new BrowserEntry { id = "FragileDamageBonusConsumed", name = "Broken Delicate Watch" };

            int Outgoing()
            {
                target.CurrentHealth = 500;
                target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                return 500 - target.CurrentHealth;
            }
            void Incoming(int damage) =>
                owner.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, target, 0, 0f);

            int baseline = Outgoing();
            lab.ExecuteCatalog(intact);
            lab.ExecuteCatalog(intact);
            int stacked = Outgoing();
            Incoming(19);
            yield return null;
            bool above = owner.CurrentHealth == 251 && items.Watches == 2 && !GameObject.Find("Rain watch break");
            Incoming(1);
            yield return null;
            bool threshold = owner.CurrentHealth == 250 && items.Watches == 0 && items.BrokenWatches == 2;
            bool cue = GameObject.Find("Rain watch break") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "DelicateWatchBreak");
            int afterBreak = Outgoing();
            owner.ServerHeal(500, owner.netId);
            int afterHeal = Outgoing();
            lab.RemoveCatalog(broken);
            lab.RemoveCatalog(broken);
            bool cleaned = items.BrokenWatches == 0;
            lab.ExecuteCatalog(broken);
            bool brokenGrant = items.BrokenWatches == 1 && Outgoing() == baseline;
            lab.RemoveCatalog(broken);
            bool brokenRemoved = items.BrokenWatches == 0;
            lab.ExecuteCatalog(intact);
            int single = Outgoing();
            lab.RemoveCatalog(intact);
            int removed = Outgoing();

            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"base={baseline} stacked={stacked} above={above} threshold={threshold} cue={cue} audio={audio} broken={afterBreak} healed={afterHeal} cleaned={cleaned} brokenGrant={brokenGrant} brokenRemoved={brokenRemoved} single={single} removed={removed}";
            if (baseline == 100 && stacked == 140 && above && threshold && cue && audio && afterBreak == baseline &&
                afterHeal == baseline && cleaned && brokenGrant && brokenRemoved && single == 120 && removed == baseline)
                Debug.Log("RAIN_ITEM_WATCH_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_WATCH_FAILED " + result);
        }

        static IEnumerator VerifyShapedGlass(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.up * 16f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            int originalHealth = owner.CurrentHealth;
            int baseline = owner.MaxHealth;
            owner.CurrentHealth = baseline;
            target.MaxHealth = 1000;
            var entry = new BrowserEntry { id = "LunarDagger", name = "Shaped Glass" };
            int Hit()
            {
                target.CurrentHealth = 1000;
                target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                return 1000 - target.CurrentHealth;
            }

            int baseDamage = Hit();
            lab.ExecuteCatalog(entry);
            int firstMax = owner.MaxHealth;
            int firstDamage = Hit();
            lab.ExecuteCatalog(entry);
            int secondMax = owner.MaxHealth;
            int secondDamage = Hit();
            int clamped = owner.CurrentHealth;
            lab.RemoveCatalog(entry);
            int singleMax = owner.MaxHealth;
            int singleDamage = Hit();
            lab.RemoveCatalog(entry);
            int removedMax = owner.MaxHealth;
            int removedDamage = Hit();
            int afterRemoval = owner.CurrentHealth;
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            owner.CurrentHealth = originalHealth;
            string result = $"max={baseline}/{firstMax}/{secondMax}/{singleMax}/{removedMax} damage={baseDamage}/{firstDamage}/{secondDamage}/{singleDamage}/{removedDamage} clamped={clamped} afterRemoval={afterRemoval}";
            if (firstMax == Math.Round(baseline / 2d) && secondMax == Math.Round(baseline / 4d) &&
                singleMax == firstMax && removedMax == baseline && clamped == secondMax && afterRemoval == clamped &&
                baseDamage == 100 && firstDamage == 200 && secondDamage == 300 &&
                singleDamage == firstDamage && removedDamage == baseDamage)
                Debug.Log("RAIN_ITEM_SHAPED_GLASS_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_SHAPED_GLASS_FAILED " + result);
        }

        static IEnumerator VerifyMaximumHealthItem(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items, string id)
        {
            items.Clear();
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            int baseline = owner.MaxHealth;
            owner.CurrentHealth = Mathf.Max(1, baseline - 60);
            int startingHealth = owner.CurrentHealth;
            var entry = new BrowserEntry
            {
                id = id,
                name = id == "Pearl" ? "Pearl" : id == "CookedSteak" ? "Cooked Steak" : "Bison Steak"
            };
            int Expected(int stacks) => id switch
            {
                "Pearl" => (int)Math.Round(baseline * (1d + .1d * stacks)),
                "CookedSteak" => (int)Math.Round((baseline + 50d * stacks) * (1d + .05d * stacks)),
                _ => baseline + 25 * stacks
            };

            lab.ExecuteCatalog(entry);
            yield return null;
            int first = owner.MaxHealth;
            int firstHealth = owner.CurrentHealth;
            lab.ExecuteCatalog(entry);
            yield return null;
            int second = owner.MaxHealth;
            int secondHealth = owner.CurrentHealth;
            owner.CurrentHealth = second;
            lab.RemoveCatalog(entry);
            yield return null;
            int single = owner.MaxHealth;
            int singleHealth = owner.CurrentHealth;
            lab.RemoveCatalog(entry);
            yield return null;
            int removed = owner.MaxHealth;
            int removedHealth = owner.CurrentHealth;
            items.Clear();
            owner.MaxHealth = originalMax;
            owner.CurrentHealth = originalHealth;
            string result = $"base={baseline} max={first}/{second}/{single}/{removed} health={startingHealth}/{firstHealth}/{secondHealth}/{singleHealth}/{removedHealth}";
            bool pickupHealth = id == "FlatHealth"
                ? firstHealth == startingHealth + 25 && secondHealth == startingHealth + 50
                : firstHealth == startingHealth && secondHealth == startingHealth;
            if (first == Expected(1) && second == Expected(2) && single == first && removed == baseline &&
                singleHealth == first && removedHealth == baseline && pickupHealth)
                Debug.Log("RAIN_ITEM_MAX_HEALTH_CHECKED " + entry.id + " " + result);
            else Debug.LogError("RAIN_ITEM_MAX_HEALTH_FAILED " + entry.id + " " + result);
        }

        static IEnumerator VerifyMovementItem(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items, string id)
        {
            items.Clear();
            var motor = owner.GetComponent<CharacterMotor>();
            var controller = owner.GetComponent<CharacterController>();
            bool motorEnabled = motor.enabled;
            bool wasRunning = owner.IsRunning;
            Vector2 previousInput = owner.movementInput;
            Vector3 origin = owner.transform.position;
            bool hoof = id == "Hoof";
            bool stoneFlux = id == "HalfSpeedDoubleHealth";
            bool mocha = id == "AttackSpeedAndMoveSpeed";
            var entry = new BrowserEntry
            {
                id = id,
                name = mocha ? "Mocha" : stoneFlux ? "Stone Flux Pauldron" : hoof ? "Paul's Goat Hoof" : "Energy Drink"
            };
            motor.enabled = false;
            var sprintSpeeds = new float[4];
            var walkSpeeds = new float[4];
            var distances = new float[4];
            var walkingDistances = new float[4];
            var maximumHealth = new int[4];
            bool grounded = true;
            bool hoofAudio = !hoof;
            for (int sample = 0; sample < sprintSpeeds.Length; sample++)
            {
                if (sample == 1)
                {
                    lab.ExecuteCatalog(entry);
                    lab.ExecuteCatalog(entry);
                }
                else if (sample > 1) lab.RemoveCatalog(entry);
                yield return null;
                maximumHealth[sample] = owner.MaxHealth;
                sprintSpeeds[sample] = motor.GetMoveSpeed(true);
                walkSpeeds[sample] = motor.GetMoveSpeed(false);
                distances[sample] = MeasureSpeed(true);
                walkingDistances[sample] = MeasureSpeed(false);
                if (!hoof || sample != 1) continue;
                float deadline = Time.time + 1f;
                while (Time.time < deadline && !hoofAudio)
                {
                    yield return null;
                    MeasureSpeed(true);
                    hoofAudio = owner.GetComponents<AudioSource>().Any(source =>
                        source.isPlaying && source.clip && source.clip.name == "GoatHoofStep");
                }
            }

            owner.transform.position = origin;
            owner.IsRunning = wasRunning;
            owner.SetMovementInput(previousInput);
            motor.enabled = motorEnabled;
            items.Clear();
            float walkBonus = walkSpeeds[0] * (mocha ? .07f : hoof ? .14f : 0f);
            float sprintBonus = mocha || hoof ? sprintSpeeds[0] * (mocha ? .07f : .14f) : walkSpeeds[0] * .25f;
            bool scaled = true;
            for (int sample = 0; sample < sprintSpeeds.Length; sample++)
            {
                int stacks = sample == 1 ? 2 : sample == 2 ? 1 : 0;
                float expectedWalk = stoneFlux ? walkSpeeds[0] / (1 + stacks) : walkSpeeds[0] + stacks * walkBonus;
                float expectedSprint = stoneFlux ? sprintSpeeds[0] / (1 + stacks) : sprintSpeeds[0] + stacks * sprintBonus;
                scaled &= Mathf.Abs(walkSpeeds[sample] - expectedWalk) < .01f &&
                    Mathf.Abs(sprintSpeeds[sample] - expectedSprint) < .01f &&
                    (!stoneFlux || maximumHealth[sample] == maximumHealth[0] * (1 + stacks)) &&
                    Mathf.Abs(distances[sample] - sprintSpeeds[sample]) < .1f &&
                    Mathf.Abs(walkingDistances[sample] - walkSpeeds[sample]) < .1f;
            }
            string result = $"grounded={grounded} walk={string.Join("/", walkingDistances)} sprint={string.Join("/", distances)}";
            if (stoneFlux) result += $" maxHealth={string.Join("/", maximumHealth)}";
            string marker = mocha ? "RAIN_ITEM_MOCHA_MOVEMENT_" : stoneFlux ? "RAIN_ITEM_STONE_FLUX_" : hoof ? "RAIN_ITEM_HOOF_" : "RAIN_ITEM_ENERGY_DRINK_";
            if (grounded && scaled && hoofAudio) Debug.Log(marker + "CHECKED " + result + $" audio={hoofAudio}");
            else Debug.LogError(marker + "FAILED " + result);

            float MeasureSpeed(bool sprinting)
            {
                owner.transform.position = origin;
                controller.Move(Vector3.down * .35f);
                grounded &= controller.isGrounded;
                owner.IsRunning = sprinting;
                owner.SetMovementInput(Vector2.up);
                Vector3 before = owner.transform.position;
                motor.MovementTick();
                return Vector3.ProjectOnPlane(owner.transform.position - before, Vector3.up).magnitude /
                    Mathf.Max(.0001f, Time.deltaTime);
            }
        }

        static IEnumerator VerifyFocusCrystal(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.right * 12.9f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            yield return null;
            target.MaxHealth = 500;
            int Hit(float distance)
            {
                target.transform.position += owner.CombatAimPoint + Vector3.right * distance - target.CombatAimPoint;
                target.CurrentHealth = 500;
                target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                return 500 - target.CurrentHealth;
            }
            var entry = new BrowserEntry { id = "NearbyDamageBonus", name = "Focus Crystal" };
            int baseline = Hit(12.9f);
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            int outside = Hit(13.1f);
            yield return null;
            bool outsideQuiet = !GameObject.Find("Rain focus crystal hit");
            int inside = Hit(12.9f);
            yield return null;
            bool cue = GameObject.Find("Rain focus crystal hit") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "FocusCrystalOriginal");
            lab.RemoveCatalog(entry);
            int single = Hit(12.9f);
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(.7f);
            int removed = Hit(12.9f);
            yield return null;
            bool cleared = !GameObject.Find("Rain focus crystal hit");
            NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"baseline={baseline} outside={outside} outsideQuiet={outsideQuiet} inside={inside} single={single} removed={removed} cleared={cleared} cue={cue} audio={audio}";
            if (baseline == 100 && outside == baseline && outsideQuiet && inside == 140 && single == 120 &&
                removed == baseline && cleared && cue && audio)
                Debug.Log("RAIN_ITEM_FOCUS_CRYSTAL_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_FOCUS_CRYSTAL_FAILED " + result);
        }

        static IEnumerator VerifyMocha(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            yield return VerifyMovementItem(lab, owner, items, "AttackSpeedAndMoveSpeed");
            yield return VerifyFireRateItem(lab, owner, items, true);
        }

        static IEnumerator VerifyFireRateItem(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items, bool mocha)
        {
            items.Clear();
            var prefab = DNNetworkManager.Instance.spawnPrefabs.FirstOrDefault(p => p && p.name == "item_sniperRifle");
            var manager = owner.CharacterItemManager;
            if (!prefab || !manager.ServerEquipTestWeapon(prefab))
            {
                Debug.LogError("RAIN_ITEM_SYRINGE_NO_RIFLE");
                yield break;
            }
            yield return new WaitForSeconds(.65f);
            if (manager.CurrentlyUsedItem is not Gun gun || !gun || gun.ItemName != prefab.GetComponent<Item>().ItemName)
            {
                Debug.LogError("RAIN_ITEM_SYRINGE_RIFLE_NOT_EQUIPPED");
                yield break;
            }
            var ammo = typeof(Item).GetField("CurrentAmmo",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            var entry = new BrowserEntry
            {
                id = mocha ? "AttackSpeedAndMoveSpeed" : "Syringe",
                name = mocha ? "Mocha" : "Soldier's Syringe"
            };
            var intervals = new float[4];
            var speeds = new float[4];
            for (int sample = 0; sample < intervals.Length; sample++)
            {
                if (sample == 1)
                {
                    lab.ExecuteCatalog(entry);
                    lab.ExecuteCatalog(entry);
                }
                else if (sample > 1) lab.RemoveCatalog(entry);
                speeds[sample] = owner.ResearchAttackSpeed;
                ammo.SetValue(gun, 5);
                float first = -1f;
                float deadline = Time.time + 2f;
                while (Time.time < deadline)
                {
                    int before = (int)ammo.GetValue(gun);
                    gun.PushLeftTrigger();
                    if ((int)ammo.GetValue(gun) == before - 1)
                    {
                        if (first >= 0f)
                        {
                            intervals[sample] = Time.time - first;
                            break;
                        }
                        first = Time.time;
                    }
                    yield return null;
                }
                yield return new WaitForSeconds(.65f);
            }
            manager.ServerRemoveTestWeapon(prefab);
            items.Clear();
            string result = $"base={intervals[0]:F3} stacked={intervals[1]:F3} single={intervals[2]:F3} removed={intervals[3]:F3} speed={string.Join("/", speeds)}";
            float oneStack = mocha ? 1.075f : 1.15f;
            float twoStacks = mocha ? 1.15f : 1.3f;
            string marker = mocha ? "RAIN_ITEM_MOCHA_FIRE_RATE_" : "RAIN_ITEM_SYRINGE_";
            if (Mathf.Abs(intervals[0] - .6f) < .05f && Mathf.Abs(intervals[1] - .6f / twoStacks) < .035f &&
                Mathf.Abs(intervals[2] - .6f / oneStack) < .035f && Mathf.Abs(intervals[3] - intervals[0]) < .05f &&
                intervals[1] < intervals[2] && intervals[2] < intervals[0] &&
                Mathf.Approximately(speeds[1], twoStacks) && Mathf.Approximately(speeds[2], oneStack) &&
                Mathf.Approximately(speeds[3], speeds[0]))
                Debug.Log(marker + "CHECKED " + result);
            else Debug.LogError(marker + "FAILED " + result);
        }

        static IEnumerator VerifyStunGrenade(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            Vector3 origin = owner.transform.position;
            int originalHealth = owner.CurrentHealth;
            Vector3 arena = origin + Vector3.up * 12f;
            var beetle = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), arena,
                Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in beetle.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            beetle.Team = owner.Team == 0 ? 1 : 0;
            NetworkServer.Spawn(beetle.gameObject);
            beetle.GetComponent<CharacterMotor>().enabled = false;
            var actor = beetle.gameObject.AddComponent<RangeResearchEnemy>();
            actor.Configure(owner, 0);
            motor.enabled = false;
            owner.transform.position = arena + Vector3.forward * 2f;
            yield return null;

            var entry = new BrowserEntry { id = "StunChanceOnHit", name = "Stun Grenade" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(float coefficient) => beetle.Server_ChangeHealthStateRaw(1, CharacterPart.body,
                AttackType.hitscan, owner, 0, coefficient);
            Hit(0f);
            bool zeroProc = !actor.IsStunned;
            actor.SetCombat(true);
            yield return new WaitForSeconds(.25f);
            int healthBefore = owner.CurrentHealth;
            var random = UnityEngine.Random.state;
            UnityEngine.Random.InitState(17);
            Hit(1000f);
            UnityEngine.Random.state = random;
            yield return null;
            bool cue = GameObject.Find("Rain stun grenade cue") != null;
            yield return new WaitForSeconds(1f);
            bool interrupted = actor.IsStunned && owner.CurrentHealth == healthBefore;
            yield return new WaitForSeconds(2f);
            bool recovered = !actor.IsStunned && owner.CurrentHealth < healthBefore;
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            Hit(1000f);
            yield return null;
            bool removed = !actor.IsStunned && !GameObject.Find("Rain stun grenade cue");
            NetworkServer.Destroy(beetle.gameObject);
            owner.transform.position = origin;
            owner.CurrentHealth = originalHealth;
            motor.enabled = motorEnabled;
            items.Clear();
            string result = $"zeroProc={zeroProc} interrupted={interrupted} recovered={recovered} removed={removed} cue={cue}";
            if (zeroProc && interrupted && recovered && removed && cue)
                Debug.Log("RAIN_ITEM_STUN_GRENADE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_STUN_GRENADE_FAILED " + result);
        }

        static IEnumerator VerifyStickyBomb(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[4];
            Vector3 origin = owner.transform.position + Vector3.up * 16f;
            float[] offsets = { 0f, 11f, 13f, 1f };
            for (int i = 0; i < targets.Length; i++)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    origin + Vector3.right * offsets[i], Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "StickyBomb", name = "Sticky Bomb" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(float proc) => targets[0].Server_ChangeHealthStateRaw(20, CharacterPart.body,
                AttackType.hitscan, owner, 0, proc);

            Hit(0f);
            yield return null;
            bool zeroProc = targets[0].CurrentHealth == 480 && !GameObject.Find("Rain sticky bomb attached");
            targets[0].CurrentHealth = 500;
            Hit(10f);
            yield return null;
            bool attached = GameObject.Find("Rain sticky bomb attached") != null;
            var playing = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            bool attachAudio = playing.Any(source => source.isPlaying && source.clip && source.clip.name == "StickyBombAttach");
            bool countdown = playing.Any(source => source.isPlaying && source.clip && source.clip.name == "StickyBombCountdown");
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            targets[0].transform.position = origin + Vector3.right * 8f;
            yield return new WaitForSeconds(.65f);
            bool delayed = targets[0].CurrentHealth == 480 && targets[1].CurrentHealth == 500;
            float deadline = Time.time + .65f;
            while (Time.time < deadline && !GameObject.Find("Rain sticky bomb blast")) yield return null;
            bool cue = GameObject.Find("Rain sticky bomb blast") != null;
            int main = 500 - targets[0].CurrentHealth;
            int near = 500 - targets[1].CurrentHealth;
            int far = 500 - targets[2].CurrentHealth;
            int old = 500 - targets[3].CurrentHealth;
            Hit(10f);
            yield return new WaitForSeconds(1.2f);
            bool removed = targets[0].CurrentHealth == 500 - main - 20 && targets[1].CurrentHealth == 500 - near &&
                !GameObject.Find("Rain sticky bomb blast");
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"zeroProc={zeroProc} attached={attached} delayed={delayed} main={main} near={near} far={far} old={old} cue={cue} attachAudio={attachAudio} countdown={countdown} removed={removed}";
            if (zeroProc && attached && delayed && main == 56 && near == 36 && far == 0 && old == 0 &&
                cue && attachAudio && countdown && removed)
                Debug.Log("RAIN_ITEM_STICKY_BOMB_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_STICKY_BOMB_FAILED " + result);
        }

        static IEnumerator VerifyGasoline(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var targets = new CharacterInstance[4];
            float[] offsets = { 0f, 15f, 17f, 20f };
            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 position = owner.transform.position + Vector3.up * 16f + Vector3.right * offsets[i];
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    position, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                targets[i] = target;
            }
            yield return null;
            foreach (var target in targets) target.MaxHealth = target.CurrentHealth = 500;
            var entry = new BrowserEntry { id = "IgniteOnKill", name = "汽油" };
            lab.ExecuteCatalog(entry);
            lab.ExecuteCatalog(entry);
            void Hit(CharacterInstance target, int damage) =>
                target.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);

            Hit(targets[0], 20);
            yield return null;
            bool nonLethal = targets[1].CurrentHealth == 500 && !GameObject.Find("Rain gasoline blast");
            Hit(targets[0], 600);
            yield return new WaitForSeconds(.1f);
            int blast = 500 - targets[1].CurrentHealth;
            bool burning = targets[1].TryGetComponent<RangeBleeding>(out var dot) && dot.HasBurn();
            bool cue = GameObject.Find("Rain gasoline blast") != null;
            bool audio = FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                source.isPlaying && source.clip && source.clip.name == "GasolineOriginal");
            lab.RemoveCatalog(entry);
            lab.RemoveCatalog(entry);
            yield return new WaitForSeconds(4.7f);
            int burn = 500 - targets[1].CurrentHealth - blast;
            int far = 500 - targets[2].CurrentHealth;
            bool expired = dot && !dot.HasBurn() && !GameObject.Find("Rain gasoline blast");
            Hit(targets[3], 600);
            yield return new WaitForSeconds(.25f);
            bool removed = targets[2].CurrentHealth == 500 && targets[1].CurrentHealth == 500 - blast - burn &&
                !GameObject.Find("Rain gasoline blast");
            int expectedBlast = Mathf.RoundToInt(items.ItemBaseAttack * 1.5f);
            int expectedBurn = Mathf.RoundToInt(items.ItemBaseAttack * 2.25f);
            foreach (var target in targets)
                if (target) NetworkServer.Destroy(target.gameObject);
            items.Clear();
            string result = $"nonLethal={nonLethal} blast={blast} burn={burn} far={far} burning={burning} expired={expired} removed={removed} cue={cue} audio={audio}";
            if (nonLethal && blast == expectedBlast && burn == expectedBurn && far == 0 &&
                burning && expired && removed && cue && audio)
                Debug.Log("RAIN_ITEM_GASOLINE_CHECKED " + result);
            else Debug.LogError("RAIN_ITEM_GASOLINE_FAILED " + result);
        }

        static IEnumerator VerifyBolsteringLantern(CharacterInstance owner, RangeResearchItems items)
        {
            int lantern = RangeResearchItems.CatalogIndex("AttackSpeedPerNearbyAllyOrEnemy");
            var targets = new CharacterInstance[5];
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            int health = owner.CurrentHealth;
            Vector3 originalPosition = owner.transform.position;
            try
            {
                items.Clear();
                motor.enabled = false;
                owner.transform.position += Vector3.up * 200f;
                Vector3 center = owner.transform.position + owner.centerPosition;
                items.Grant(lantern);
                items.RefreshLanternBodies();
                bool selfExcluded = owner.ResearchLanternBodies == 0 && Mathf.Approximately(owner.ResearchAttackSpeed, 1f);
                for (int i = 0; i < targets.Length; i++)
                {
                    var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        center + Vector3.right * (i + 1), Quaternion.identity).GetComponent<CharacterInstance>();
                    targets[i] = target;
                    foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    target.Team = i % 2 == 0 ? owner.Team : owner.Team == 0 ? 1 : 0;
                    target.MaxHealth = target.CurrentHealth = 1000;
                    target.RangeInvulnerable = true;
                    NetworkServer.Spawn(target.gameObject);
                    target.GetComponent<CharacterMotor>().enabled = false;
                    var ai = target.GetComponent<BotAI>();
                    if (ai) { ai.enabled = false; ai.StopAllCoroutines(); }
                }
                yield return null; // Register the bodies through their normal Start path.
                items.RefreshLanternBodies();
                bool firstCap = owner.ResearchLanternBodies == 3 && Mathf.Approximately(owner.ResearchAttackSpeed, 1.6f);
                items.Grant(lantern);
                items.RefreshLanternBodies();
                bool secondCap = owner.ResearchLanternBodies == 4 && Mathf.Approximately(owner.ResearchAttackSpeed, 1.94f);
                for (int i = 2; i < targets.Length; i++) targets[i].CurrentHealth = 0;
                items.RefreshLanternBodies();
                bool factions = owner.ResearchLanternBodies == 2 && Mathf.Approximately(owner.ResearchAttackSpeed, 1.47f);
                targets[1].CurrentHealth = 0;
                targets[0].transform.position = center + Vector3.right * 20.001f;
                items.RefreshLanternBodies();
                bool outside = owner.ResearchLanternBodies == 0;
                targets[0].transform.position = center + Vector3.right * 20f;
                items.RefreshLanternBodies();
                bool boundary = owner.ResearchLanternBodies == 1;
                var snapshot = new RangeResearchItems.PersistentItems(items);
                items.Clear();
                snapshot.Restore(items);
                bool restoredInventoryOnly = owner.ResearchLanterns == 2 && owner.ResearchLanternBodies == 0;
                items.RefreshLanternBodies();
                bool removed = items.Remove(lantern) && owner.ResearchLanterns == 1 &&
                    Mathf.Approximately(owner.ResearchAttackSpeed, 1.2f);
                owner.CurrentHealth = 0;
                items.RefreshLanternBodies();
                bool dead = owner.ResearchLanternBodies == 0 && owner.ResearchLanterns == 1;
                owner.CurrentHealth = health;
                items.RefreshLanternBodies();
                bool resumed = owner.ResearchLanternBodies == 1;
                items.enabled = false;
                bool disabled = owner.ResearchLanternBodies == 0 && owner.ResearchLanterns == 1 &&
                    Mathf.Approximately(owner.ResearchAttackSpeed, 1f);
                items.RefreshLanternBodies();
                disabled &= owner.ResearchLanternBodies == 0;
                items.enabled = true;
                bool reenabled = owner.ResearchLanternBodies == 1 &&
                    Mathf.Approximately(owner.ResearchAttackSpeed, 1.2f);
                bool last = items.Remove(lantern) && owner.ResearchLanterns == 0 &&
                    owner.ResearchLanternBodies == 0 && Mathf.Approximately(owner.ResearchAttackSpeed, 1f);
                if (selfExcluded && firstCap && secondCap && factions && outside && boundary &&
                    restoredInventoryOnly && removed && dead && resumed && disabled && reenabled && last)
                    Debug.Log("RANGE_SMOKE_BOLSTERING_LANTERN_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_BOLSTERING_LANTERN self={selfExcluded} cap1={firstCap} cap2={secondCap} teams={factions} outside={outside} edge={boundary} restore={restoredInventoryOnly} remove={removed} dead={dead} resume={resumed} disabled={disabled} enabled={reenabled} last={last}");
            }
            finally
            {
                owner.CurrentHealth = health;
                items.Clear();
                foreach (var target in targets) if (target) NetworkServer.Destroy(target.gameObject);
                owner.transform.position = originalPosition;
                motor.enabled = motorEnabled;
            }
        }

        static void VerifyWhiteScrapperBatch(CharacterInstance owner, RangeResearchItems items)
        {
            int maximum = owner.MaxHealth;
            int health = owner.CurrentHealth;
            int scrap = RangeResearchItems.CatalogIndex("ScrapWhite");
            int[] selections = { 0, 1, 2, 8, 9, 10, 11, 12, 13, 14, 16, 19, 20, 22, 24,
                25, 26, 27, 29, 31, 33, 36, 38, 52, 86, 90, 91, 93, 100, 103, 104 };
            bool selectionsPassed = true;
            try
            {
                items.Clear();
                foreach (int selected in selections)
                {
                    items.Grant(selected);
                    items.Grant(selected);
                    bool converted = items.TryScrapWhiteItem(selected) == 2 &&
                        items.WhitePrinterPaymentCount(selected) == 0 && owner.ResearchWhiteScrap == 2 &&
                        items.TryScrapWhiteItem(selected) == 0;
                    if (!converted) Debug.LogError($"RANGE_SMOKE_WHITE_SCRAPPER_SELECTION item={selected}");
                    selectionsPassed &= converted;
                    items.Clear();
                }
                for (int i = 0; i < 12; i++) items.Grant(0);
                bool capped = items.TryUseWhiteItemStation("ScrapBisonSteak") && items.Steak == 2 &&
                    owner.ResearchWhiteScrap == 10 && owner.MaxHealth == maximum + 50;
                bool remaining = items.TryUseWhiteItemStation("ScrapBisonSteak") && items.Steak == 0 &&
                    owner.ResearchWhiteScrap == 12 && owner.MaxHealth == maximum &&
                    !items.TryUseWhiteItemStation("ScrapBisonSteak");
                items.Grant(17);
                items.Grant(21);
                items.Grant(40);
                bool rejected = items.TryScrapWhiteItem(scrap) == 0 && items.TryScrapWhiteItem(17) == 0 &&
                    items.TryScrapWhiteItem(21) == 0 && items.TryScrapWhiteItem(40) == 0 &&
                    items.TryScrapWhiteItem(-1) == 0 && items.BrokenWatches == 1 &&
                    items.EmptyBottles == 1 && owner.ResearchSeeds == 1 && owner.ResearchWhiteScrap == 12;
                items.Grant(1);
                owner.CurrentHealth = 0;
                bool deadRejected = items.TryScrapWhiteItem(1) == 0 && items.Glasses == 1;
                owner.CurrentHealth = health;
                if (selectionsPassed && capped && remaining && rejected && deadRejected)
                    Debug.Log("RANGE_SMOKE_WHITE_SCRAPPER_BATCH_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_WHITE_SCRAPPER_BATCH selections={selectionsPassed} capped={capped} remaining={remaining} rejected={rejected} dead={deadRejected}");
            }
            finally
            {
                items.Clear();
                owner.CurrentHealth = health;
            }
            if (owner.MaxHealth != maximum) Debug.LogError("RANGE_SMOKE_WHITE_SCRAPPER_HEALTH_RESTORE");
        }

        static void VerifyWhitePrinterPayments(CharacterInstance owner, RangeResearchItems items)
        {
            int health = owner.CurrentHealth;
            int maximum = owner.MaxHealth;
            int syringe = RangeResearchItems.CatalogIndex("Syringe");
            int[] payments = { 0, 1, 2, 8, 9, 10, 11, 12, 13, 14, 16, 19, 20, 22, 24,
                26, 27, 29, 31, 33, 36, 38, 52, 86, 90, 91, 93, 100, 103, 104 };
            bool allPayments = true;
            try
            {
                items.Clear();
                foreach (int payment in payments)
                {
                    items.Grant(payment);
                    bool converted = items.WhitePrinterPaymentCount(payment) == 1 &&
                        items.SelectWhitePrinterPayment(0f) == payment &&
                        items.SelectWhitePrinterPayment(1f) == payment &&
                        items.TryUseWhiteItemStation("PrintSyringe") &&
                        items.WhitePrinterPaymentCount(payment) == 0 && owner.ResearchSyringes == 1;
                    if (!converted) Debug.LogError($"RANGE_SMOKE_WHITE_PRINTER_PAYMENT item={payment}");
                    allPayments &= converted;
                    items.Clear();
                }
                items.Grant(syringe);
                items.Grant(0);
                items.Grant(0);
                items.Grant(1);
                bool weighted = items.SelectWhitePrinterPayment(0f) == 0 &&
                    items.SelectWhitePrinterPayment(.666f) == 0 &&
                    items.SelectWhitePrinterPayment(.667f) == 1 &&
                    items.SelectWhitePrinterPayment(1f) == 1;
                items.Grant(RangeResearchItems.CatalogIndex("ScrapWhite"));
                bool scrapPriority = items.TryUseWhiteItemStation("PrintSyringe") &&
                    owner.ResearchWhiteScrap == 0 && items.Steak == 2 && items.Glasses == 1 &&
                    owner.ResearchSyringes == 2;
                items.Clear();
                items.Grant(syringe);
                bool outputFallback = items.SelectWhitePrinterPayment(.5f) == syringe &&
                    items.TryUseWhiteItemStation("PrintSyringe") && owner.ResearchSyringes == 1;
                items.Clear();
                items.Grant(17); // Broken watch and empty bottle are consumed, unprintable items.
                items.Grant(21);
                items.Grant(40); // Leeching Seed is Tier2.
                bool protectedItems = items.SelectWhitePrinterPayment(.5f) == -1 &&
                    !items.TryUseWhiteItemStation("PrintSyringe") && owner.ResearchSyringes == 0 &&
                    items.BrokenWatches == 1 && items.EmptyBottles == 1 && owner.ResearchSeeds == 1;
                items.Clear();
                bool empty = !items.TryUseWhiteItemStation("PrintSyringe");
                bool alive = owner.CurrentHealth > 0;
                items.Grant(1);
                owner.CurrentHealth = 0;
                bool deadRejected = !items.TryUseWhiteItemStation("PrintSyringe") && items.Glasses == 1;
                owner.CurrentHealth = health;
                if (allPayments && weighted && scrapPriority && outputFallback && protectedItems && empty && alive && deadRejected)
                    Debug.Log("RANGE_SMOKE_WHITE_PRINTER_PAYMENTS_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_WHITE_PRINTER_PAYMENTS all={allPayments} weighted={weighted} scrap={scrapPriority} output={outputFallback} protected={protectedItems} empty={empty} alive={alive} dead={deadRejected}");
            }
            finally
            {
                items.Clear();
                owner.CurrentHealth = health;
            }
            if (owner.MaxHealth != maximum) Debug.LogError("RANGE_SMOKE_WHITE_PRINTER_HEALTH_RESTORE");
        }

        static void VerifyQuickFix(CharacterInstance owner, RangeResearchItems items)
        {
            int quickFix = RangeResearchItems.CatalogIndex("BonusHealthBoost");
            int scrap = RangeResearchItems.CatalogIndex("ScrapWhite");
            int mocha = RangeResearchItems.CatalogIndex("AttackSpeedAndMoveSpeed");
            int steak = RangeResearchItems.CatalogIndex("FlatHealth");
            int stew = RangeResearchItems.CatalogIndex("Stew");
            int maximum = owner.MaxHealth;
            int health = owner.CurrentHealth;
            float baseRegen = owner.EffectiveRainHealthRegen;
            try
            {
                items.Grant(scrap);
                bool missingMocha = !items.TryCookMeal("BonusHealthBoost") && owner.ResearchWhiteScrap == 1;
                items.Grant(mocha);
                bool crafted = items.TryCookMeal("BonusHealthBoost") && !items.TryCookMeal("BonusHealthBoost") &&
                    owner.ResearchQuickFixes == 1 && owner.ResearchWhiteScrap == 0 && owner.ResearchMochas == 0;
                bool noBaseScaling = owner.MaxHealth == maximum && Mathf.Approximately(owner.EffectiveRainHealthRegen, baseRegen);

                items.Grant(steak);
                items.Grant(stew);
                float stewRegen = 2.5f * (owner.RainAttackPower > 0f ? owner.RainAttackPower / CharacterInstance.RainBaseAttackPower : 1f);
                bool firstStack = owner.MaxHealth == (int)Math.Round(maximum + 25d * 1.5d) &&
                    Mathf.Approximately(owner.EffectiveRainHealthRegen, baseRegen + stewRegen * 1.5f);
                items.Grant(quickFix);
                bool secondStack = owner.MaxHealth == (int)Math.Round(maximum + 25d * 1.65d) &&
                    Mathf.Approximately(owner.EffectiveRainHealthRegen, baseRegen + stewRegen * 1.65f);
                bool removed = items.Remove(quickFix) && items.Remove(quickFix) && owner.MaxHealth == maximum + 25 &&
                    Mathf.Approximately(owner.EffectiveRainHealthRegen, baseRegen + stewRegen);
                if (missingMocha && crafted && noBaseScaling && firstStack && secondStack && removed)
                    Debug.Log("RANGE_SMOKE_QUICK_FIX_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_QUICK_FIX missing={missingMocha} crafted={crafted} base={noBaseScaling} first={firstStack} second={secondStack} removed={removed}");

                items.Clear();
                int pearl = RangeResearchItems.CatalogIndex("Pearl");
                int stoneFlux = RangeResearchItems.CatalogIndex("HalfSpeedDoubleHealth");
                int cookedSteak = RangeResearchItems.CatalogIndex("CookedSteak");
                int knurl = RangeResearchItems.CatalogIndex("Knurl");
                items.Grant(pearl);
                items.Grant(stoneFlux);
                items.Grant(cookedSteak);
                items.Grant(steak);
                items.Grant(knurl);
                items.Grant(quickFix);
                items.Grant(quickFix);
                // Original CharacterBody applies Quick Fix to both flat item health and additive health multipliers.
                bool percentStack = owner.MaxHealth == (int)Math.Round((maximum + 115d * 1.65d) * (1d + 1.15d * 1.65d));
                bool removeQuickFix = items.Remove(quickFix) &&
                    owner.MaxHealth == (int)Math.Round((maximum + 115d * 1.5d) * (1d + 1.15d * 1.5d));
                bool removePercentItem = items.Remove(stoneFlux) &&
                    owner.MaxHealth == (int)Math.Round((maximum + 115d * 1.5d) * (1d + .15d * 1.5d));
                bool removeMixedItem = items.Remove(cookedSteak) &&
                    owner.MaxHealth == (int)Math.Round((maximum + 65d * 1.5d) * (1d + .1d * 1.5d));
                bool removeLastQuickFix = items.Remove(quickFix) &&
                    owner.MaxHealth == (int)Math.Round((maximum + 65d) * 1.1d);
                items.Clear();
                bool clearRestored = owner.MaxHealth == maximum && Mathf.Approximately(owner.EffectiveRainHealthRegen, baseRegen);
                if (percentStack && removeQuickFix && removePercentItem && removeMixedItem && removeLastQuickFix && clearRestored)
                    Debug.Log("RANGE_SMOKE_QUICK_FIX_PERCENT_HEALTH_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_QUICK_FIX_PERCENT_HEALTH stack={percentStack} quickFix={removeQuickFix} percentItem={removePercentItem} mixedItem={removeMixedItem} lastQuickFix={removeLastQuickFix} clear={clearRestored}");
            }
            finally
            {
                items.Clear();
                owner.CurrentHealth = health;
            }
        }

        static IEnumerator VerifyHikingBoots(CharacterInstance owner, Health target, RangeResearchItems items)
        {
            VerifyHikingBootsRadius(owner, target, items);
            int boots = RangeResearchItems.CatalogIndex("CritAtLowerElevation");
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            Vector3 originalPosition = owner.transform.position;
            int targetHealth = target.CurrentHealth;
            try
            {
                items.Grant(boots);
                items.Grant(boots);
                for (int i = 0; i < 10; i++) items.Grant(glasses);
                owner.transform.position = target.transform.position + Vector3.up * 3f;
                target.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                bool zeroProc = items.ModifyDamage(100) == 200;
                target.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.hitscan, owner, 0, .1f);
                bool firstHit = items.ModifyDamage(100) == 202;
                target.ServerApplyRangeAreaProc(10, owner, 1f, RangeProcSource.HikingBoots, false, false);
                bool chainBlocked = items.ModifyDamage(100) == 202;
                for (int i = 0; i < 12; i++)
                    target.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.hitscan, owner, 0);
                bool capped = owner.ResearchHikingBoots == 2 && items.ModifyDamage(100) == 220;
                owner.transform.position = originalPosition;
                yield return new WaitForSeconds(8f);
                owner.transform.position = target.transform.position + Vector3.down * 3f;
                target.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.hitscan, owner, 0);
                owner.transform.position = originalPosition;
                yield return new WaitForSeconds(3f);
                bool refreshed = items.ModifyDamage(100) == 220;
                bool removedOne = items.Remove(boots) && items.ModifyDamage(100) == 210;
                yield return new WaitForSeconds(7.3f);
                bool expired = owner.ResearchHikingBoots == 1 && items.ModifyDamage(100) == 200;
                owner.transform.position = target.transform.position + Vector3.up * 3f;
                target.Server_ChangeHealthStateRaw(10, CharacterPart.body, AttackType.hitscan, owner, 0);
                bool removed = removedOne && items.ModifyDamage(100) == 201 &&
                    items.Remove(boots) && items.ModifyDamage(100) == 200;
                if (zeroProc && firstHit && chainBlocked && capped && refreshed && expired && removed && owner.ResearchHikingBoots == 0)
                    Debug.Log("RANGE_SMOKE_HIKING_BOOTS_CHECKED");
                else Debug.LogError("RANGE_SMOKE_HIKING_BOOTS");
            }
            finally
            {
                items.Clear();
                owner.transform.position = originalPosition;
                target.CurrentHealth = targetHealth;
            }
        }

        static void VerifyHikingBootsRadius(CharacterInstance owner, Health target, RangeResearchItems items)
        {
            if (!owner.TryGetComponent<CharacterController>(out var controller))
            {
                Debug.LogError("RANGE_SMOKE_HIKING_BOOTS_RADIUS_NO_CONTROLLER");
                return;
            }
            Vector3 position = owner.transform.position;
            Vector3 scale = owner.transform.localScale;
            float radius = controller.radius;
            float height = controller.height;
            try
            {
                controller.height = 2f;
                controller.radius = .4f;
                int boots = RangeResearchItems.CatalogIndex("CritAtLowerElevation");
                items.Grant(boots);
                bool normal = CheckBoundary(Vector3.one, .4f);
                bool uniformScale = CheckBoundary(Vector3.one * 2f, .8f);
                bool wideX = CheckBoundary(new Vector3(2f, 1f, .5f), .8f);
                bool wideZ = CheckBoundary(new Vector3(.5f, 1f, 2f), .8f);
                if (normal && uniformScale && wideX && wideZ)
                    Debug.Log("RANGE_SMOKE_HIKING_BOOTS_RADIUS_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_HIKING_BOOTS_RADIUS normal={normal} uniform={uniformScale} wideX={wideX} wideZ={wideZ}");
            }
            finally
            {
                items.Clear();
                controller.radius = radius;
                controller.height = height;
                owner.transform.localScale = scale;
                owner.transform.position = position;
            }

            bool CheckBoundary(Vector3 localScale, float worldRadius)
            {
                owner.transform.localScale = localScale;
                float targetCenter = target is CharacterInstance character ? character.CombatAimPoint.y : target.transform.position.y + 1f;
                if (target.TryGetComponent<CharacterController>(out var targetController) && targetController.enabled)
                    targetCenter = targetController.transform.TransformPoint(targetController.center).y;
                SetHeight(worldRadius - .001f);
                bool below = !items.TryHikingBootsHit(target, RangeProcSource.None);
                SetHeight(worldRadius + .001f);
                bool above = items.TryHikingBootsHit(target, RangeProcSource.None);
                return below && above;

                void SetHeight(float difference) => owner.transform.position += Vector3.up *
                    (targetCenter + difference - controller.transform.TransformPoint(controller.center).y);
            }
        }

        static IEnumerator VerifyStewCombat(CharacterInstance owner, Health target, RangeResearchItems items, int stew)
        {
            int ownerHealth = owner.CurrentHealth;
            int targetHealth = target.CurrentHealth;
            float originalRegen = owner.RainHealthRegen;
            try
            {
                owner.RainHealthRegen = 0f;
                owner.CurrentHealth = owner.MaxHealth;
                // Give the server a frame to clear prior fractional regeneration.
                yield return null;
                items.Grant(stew);

                int injuredHealth = Mathf.Max(1, owner.MaxHealth - 20);
                owner.CurrentHealth = injuredHealth;
                float deadline = Time.realtimeSinceStartup + 2f;
                yield return new WaitUntil(() => owner.CurrentHealth > injuredHealth || Time.realtimeSinceStartup >= deadline);
                bool regenerated = owner.CurrentHealth > injuredHealth && owner.CurrentHealth < owner.MaxHealth;

                target.Server_ChangeHealthState(24, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                int injuredDamage = targetHealth - target.CurrentHealth;
                target.CurrentHealth = targetHealth;
                owner.CurrentHealth = owner.MaxHealth;
                target.Server_ChangeHealthState(24, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                int fullDamage = targetHealth - target.CurrentHealth;

                if (regenerated && injuredDamage > 0 && fullDamage > injuredDamage)
                    Debug.Log($"RANGE_SMOKE_STEW_COMBAT_CHECKED injured={injuredDamage} full={fullDamage}");
                else Debug.LogError($"RANGE_SMOKE_STEW_COMBAT regenerated={regenerated} injured={injuredDamage} full={fullDamage}");
            }
            finally
            {
                items.Remove(stew);
                owner.RainHealthRegen = originalRegen;
                owner.CurrentHealth = ownerHealth;
                target.CurrentHealth = targetHealth;
            }
        }

        IEnumerator VerifyDeskPlantLifetime(Gamemodes.RangeGamemode mode, CharacterInstance owner)
        {
            if (!owner.TryGetComponent<RangeResearchItems>(out var items))
                items = owner.gameObject.AddComponent<RangeResearchItems>();
            var motor = owner.GetComponent<CharacterMotor>();
            Vector3 originalPosition = owner.transform.position;
            int originalHealth = owner.CurrentHealth;
            int originalMax = owner.MaxHealth;
            float originalRegen = owner.RainHealthRegen;
            bool motorWasEnabled = motor.enabled;
            int plant = RangeResearchItems.CatalogIndex("Plant");
            items.Grant(plant);
            try
            {
                motor.enabled = false;
                owner.transform.position += Vector3.up * 150f;
                owner.ServerSetMaximumHealth(100);
                owner.RainHealthRegen = 0f;
                owner.CurrentHealth = 1;
                float plantedAt = Time.time;
                bool killed = HitAndKillSmokeTarget(owner, items);
                owner.transform.position += Vector3.up * 12f;
                float growthDeadline = Time.time + 6.5f;
                yield return new WaitUntil(() => mode.ActiveRainHealingPlantCount > 0 || Time.time >= growthDeadline);
                if (!killed || mode.ActiveRainHealingPlantCount != 1 || Time.time < plantedAt + 5f ||
                    owner.CurrentHealth != 1)
                {
                    Debug.LogError("RAIN_COOP_DESK_PLANT_GROWTH");
                    yield break;
                }

                Debug.Log("RAIN_COOP_DESK_PLANT_ACTIVE_BEFORE_JOIN");
                float activeAt = Time.time;
                int pulses = 0;
                int previousHealth = owner.CurrentHealth;
                float expiryDeadline = Time.time + 13f;
                while (mode.ActiveRainHealingPlantCount > 0 && Time.time < expiryDeadline)
                {
                    yield return null;
                    if (owner.CurrentHealth == previousHealth) continue;
                    pulses++;
                    previousHealth = owner.CurrentHealth;
                }
                float activeDuration = Time.time - activeAt;
                if (pulses != 20 || owner.CurrentHealth != 100 || mode.ActiveRainHealingPlantCount != 0 ||
                    activeDuration < 9.9f || activeDuration > 12.5f)
                    Debug.LogError($"RAIN_COOP_DESK_PLANT_LIFETIME pulses={pulses} hp={owner.CurrentHealth} seconds={activeDuration:F2}");
                else Debug.Log($"RAIN_COOP_DESK_PLANT_LIFETIME_CHECKED pulses={pulses} seconds={activeDuration:F2}");
                yield return new WaitForSeconds(1f);
            }
            finally
            {
                items.Remove(plant);
                owner.ServerSetMaximumHealth(originalMax);
                owner.CurrentHealth = originalHealth;
                owner.RainHealthRegen = originalRegen;
                owner.transform.position = originalPosition;
                motor.enabled = motorWasEnabled;
            }
        }

        IEnumerator VerifyRainCoop(PlayerInstance player, CharacterInstance owner)
        {
            var mode = GameManager.Gamemode as Gamemodes.RangeGamemode;
            if (!mode || !mode.IsRainCoop || mode.FriendyFire)
            {
                Debug.LogError("RAIN_COOP_WRONG_MODE");
                yield break;
            }
            mode.RainBossKind = Environment.GetCommandLineArgs().Contains("--rain-coop-queen-boss")
                ? RangeResearchEnemy.QueenKind : 2;
            var arena = GameObject.Find("Chapter One");
            if (!arena || !arena.transform.Find("Sector 2-2") || GameObject.Find("map_range"))
                Debug.LogError("RAIN_COOP_WRONG_ARENA");
            if (!owner.TryGetComponent<RangeRobotVisual>(out var visual) || visual.Selection != 1)
                Debug.LogError("RAIN_COOP_FAF_NOT_SELECTED");
            if (!player.ServerSetRangeHero(0)) Debug.LogError("RAIN_COOP_SOLDIER_SWITCH");
            yield return null;
            if (owner.GetComponent<RangeRobotVisual>()) Debug.LogError("RAIN_COOP_SOLDIER_VISUAL");
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-desk-plant-smoke"))
            {
                yield return VerifyDeskPlantLifetime(mode, owner);
                yield break;
            }
            owner.RangeInvulnerable = true;
            int baseHealth = owner.MaxHealth;
            PlayerInstance guest = null;
            if (waitForRainClient)
            {
                Debug.Log("RAIN_COOP_HOST_READY");
                float joinDeadline=Time.realtimeSinceStartup+24f;
                while (GameManager.Players.Values.Count(p=>p&&!p.BOT&&p.MyCharacter)<2 &&
                    Time.realtimeSinceStartup<joinDeadline) yield return null;
                if (GameManager.Players.Values.Count(p=>p&&!p.BOT&&p.MyCharacter)<2)
                    Debug.LogError("RAIN_COOP_SECOND_PLAYER_MISSING");
                yield return new WaitForSeconds(2f);
                guest = GameManager.Players.Values.FirstOrDefault(p => p && p != player && !p.BOT && p.MyCharacter);
                if (Environment.GetCommandLineArgs().Contains("--rain-parent-impulse-coop"))
                {
                    if (guest) yield return VerifyParentImpulseHost(FindFirstObjectByType<RangeResearchLab>(), guest);
                    yield break;
                }
                if (Environment.GetCommandLineArgs().Contains("--rain-parent-coop-smoke"))
                {
                    owner.RangeInvulnerable = false;
                    if (guest) yield return VerifyParentGm(FindFirstObjectByType<RangeResearchLab>(), owner);
                    owner.RangeInvulnerable = true;
                    yield return new WaitForSeconds(2f);
                    yield break;
                }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
                if (Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-smoke"))
                {
                    if (guest) yield return VerifyMonsterLogHost(mode, player, guest, owner);
                    yield break;
                }
#endif
                if (Environment.GetCommandLineArgs().Contains("--rain-coop-drone-backup-only"))
                {
                    if (guest) yield return VerifyDroneBackupHost(mode, guest);
                    yield break;
                }
                bool projectileSmoke = Environment.GetCommandLineArgs().Contains("--rain-coop-projectile-smoke");
                if (projectileSmoke || Environment.GetCommandLineArgs().Contains("--rain-coop-firework-only"))
                {
                    if (!guest) yield break;
                    yield return VerifyFireworkChest(mode, guest);
                    var beacon = mode.transform.Find("Rain task beacon 0");
                    if (!beacon) Debug.LogError("RAIN_COOP_MISSING_BEACON");
                    else yield return VerifyFireworkBeacon(mode, player, beacon, 0, projectileSmoke);
                    yield return new WaitForSeconds(.3f);
                    yield break;
                }
                if (Environment.GetCommandLineArgs().Contains("--rain-coop-scanner-only"))
                {
                    if (guest) yield return VerifyRainScannerHost(mode, guest);
                    yield break;
                }
                if (guest) yield return VerifyFruitFromRemotePlayer(guest);
            }
            var deadline = Time.realtimeSinceStartup + 76f;
            var kinds = new System.Collections.Generic.HashSet<int>();
            bool guestJoined = guest != null;
            int expectedHostExperience = player.RainExperience;
            bool bought = false, activated = false;
            bool weaponScalingChecked = false;
            while (!mode.RainVictory && Time.realtimeSinceStartup < deadline)
            {
                foreach (var actor in FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None))
                {
                    if (!actor) continue;
                    var enemy = actor.GetComponent<CharacterInstance>();
                    if (!enemy || enemy.CurrentHealth <= 0 || enemy.RainEnemyKind == 0) continue;
                    if (actor.Archetype == 2 && waitForRainClient && !kinds.Contains(2))
                    {
                        owner.transform.position = enemy.transform.position + Vector3.forward * 4f;
                        Physics.SyncTransforms();
                        yield return new WaitForSeconds(2.8f);
                    }
                    if (actor.Archetype == RangeResearchEnemy.QueenKind && waitForRainClient && !kinds.Contains(actor.Archetype))
                    {
                        owner.transform.position = enemy.transform.position + enemy.transform.forward * 6f;
                        Physics.SyncTransforms();
                        enemy.CurrentHealth = enemy.MaxHealth * 3 / 4;
                        float cueDeadline = Time.realtimeSinceStartup + 8f;
                        while (enemy && enemy.RainEnemyAnimation != 23 &&
                            Time.realtimeSinceStartup < cueDeadline) yield return null;
                        if (!enemy || enemy.RainEnemyAnimation != 23)
                            Debug.LogError("RAIN_COOP_QUEEN_CUE_FAILED");
                        else yield return new WaitForSeconds(2.5f);
                    }
                    if (waitForRainClient && actor.UsesGroundNavigation &&
                        actor.Archetype is not (RangeResearchEnemy.QueenKind or RangeResearchEnemy.HermitCrabKind or
                            RangeResearchEnemy.BisonKind or RangeResearchEnemy.AcidLarvaKind or
                            RangeResearchEnemy.LunarExploderKind) &&
                        !kinds.Contains(actor.Archetype))
                    {
                        Vector3 originalPosition = owner.transform.position;
                        owner.transform.position = enemy.transform.position + enemy.transform.forward * 2f;
                        Physics.SyncTransforms();
                        byte attackAnimation = actor.Archetype switch
                        {
                            RangeResearchEnemy.BeetleGuardKind => 5,
                            RangeResearchEnemy.BruiserKind => 16,
                            RangeResearchEnemy.ImpKind => 26,
                            RangeResearchEnemy.VerminKind => RangeResearchEnemy.VerminFirstBiteAnimation,
                            RangeResearchEnemy.ParentKind => RangeResearchEnemy.ParentSlamAnimation,
                            _ => 9
                        };
                        float cueDeadline = Time.realtimeSinceStartup + 4f;
                        while (enemy && !MatchesMonsterSmokeAttack(kind: actor.Archetype, enemy.RainEnemyAnimation, attackAnimation) &&
                            Time.realtimeSinceStartup < cueDeadline) yield return null;
                        if (!enemy || !MatchesMonsterSmokeAttack(actor.Archetype, enemy.RainEnemyAnimation, attackAnimation))
                            Debug.LogError($"RAIN_COOP_NEW_MONSTER_CUE_FAILED kind={actor.Archetype}");
                        else yield return new WaitForSeconds(.35f);
                        owner.transform.position = originalPosition;
                        Physics.SyncTransforms();
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.BisonKind &&
                        !kinds.Contains(actor.Archetype))
                    {
                        yield return VerifyBisonCharge(lab: null, owner, enemy, owner.CurrentHealth);
                        if (!enemy) continue;
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.WispKind &&
                        !kinds.Contains(RangeResearchEnemy.WispKind))
                    {
                        Vector3 originalPosition = owner.transform.position;
                        owner.transform.position = enemy.transform.position + enemy.transform.forward * 8f;
                        Physics.SyncTransforms();
                        float fireDeadline = Time.realtimeSinceStartup + 5f;
                        while (enemy && enemy.RainEnemyAnimation != 13 && Time.realtimeSinceStartup < fireDeadline)
                            yield return null;
                        if (!enemy || enemy.RainEnemyAnimation != 13)
                            Debug.LogError("RAIN_COOP_WISP_FIRE_FAILED");
                        else yield return new WaitForSeconds(.35f);
                        owner.transform.position = originalPosition;
                        Physics.SyncTransforms();
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.JellyfishKind &&
                        !kinds.Contains(RangeResearchEnemy.JellyfishKind))
                    {
                        Vector3 originalPosition = owner.transform.position;
                        int goldBeforeNova = player.RainGold;
                        int experienceBeforeNova = player.RainExperience;
                        int killsBeforeNova = mode.RainKills;
                        uint jellyfishId = enemy.netId;
                        owner.transform.position = enemy.transform.position + enemy.transform.forward * 3f;
                        Physics.SyncTransforms();
                        float novaDeadline = Time.realtimeSinceStartup + 5f;
                        while (NetworkServer.spawned.ContainsKey(jellyfishId) &&
                            Time.realtimeSinceStartup < novaDeadline) yield return null;
                        owner.transform.position = originalPosition;
                        Physics.SyncTransforms();
                        if (!NetworkServer.spawned.ContainsKey(jellyfishId) &&
                            mode.RainKills == killsBeforeNova + 1 && player.RainGold == goldBeforeNova &&
                            player.RainExperience == experienceBeforeNova)
                        {
                            kinds.Add(RangeResearchEnemy.JellyfishKind);
                            Debug.Log("RAIN_COOP_JELLYFISH_NOVA_REWARD_CHECKED");
                            continue;
                        }
                        Debug.LogError("RAIN_COOP_JELLYFISH_NOVA_REWARD_FAILED");
                        if (!enemy) continue;
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.GreaterWispKind &&
                        !kinds.Contains(actor.Archetype))
                    {
                        Vector3 originalPosition = owner.transform.position;
                        var motor = owner.GetComponent<CharacterMotor>();
                        bool motorEnabled = motor.enabled;
                        motor.enabled = false;
                        enemy.transform.position += Vector3.up * 12f;
                        owner.transform.position = enemy.transform.position + enemy.transform.forward * 22f;
                        Physics.SyncTransforms();
                        float fireDeadline = Time.realtimeSinceStartup + 8f;
                        while (enemy && enemy.RainEnemyAnimation != 25 && Time.realtimeSinceStartup < fireDeadline)
                            yield return null;
                        if (!enemy || enemy.RainEnemyAnimation != 25)
                            Debug.LogError("RAIN_COOP_GREATER_WISP_FIRE_FAILED");
                        else yield return new WaitForSeconds(.35f);
                        owner.transform.position = originalPosition;
                        motor.enabled = motorEnabled;
                        Physics.SyncTransforms();
                        if (!enemy) continue;
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.FlyingVerminKind &&
                        !kinds.Contains(actor.Archetype))
                    {
                        Vector3 originalPosition = owner.transform.position;
                        var motor = owner.GetComponent<CharacterMotor>();
                        bool motorEnabled = motor.enabled;
                        motor.enabled = false;
                        enemy.transform.position += Vector3.up * 12f;
                        owner.transform.position = enemy.transform.position + enemy.transform.forward * 24f;
                        Physics.SyncTransforms();
                        float shotDeadline = Time.realtimeSinceStartup + 6f;
                        while (enemy && enemy.RainFlyingVerminShots.Count == 0 &&
                            Time.realtimeSinceStartup < shotDeadline) yield return null;
                        owner.transform.position = originalPosition;
                        motor.enabled = motorEnabled;
                        Physics.SyncTransforms();
                        if (!enemy) continue;
                        if (enemy.RainFlyingVerminShots.Count == 0)
                            Debug.LogError("RAIN_COOP_FLYING_VERMIN_SHOT_FAILED");
                        else yield return new WaitForSeconds(.5f);
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.HermitCrabKind &&
                        !kinds.Contains(actor.Archetype))
                    {
                        yield return VerifyHermitCoopMortar(owner, enemy);
                        if (!enemy) continue;
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.AcidLarvaKind &&
                        !kinds.Contains(actor.Archetype))
                    {
                        yield return VerifyAcidLarvaLeap(null, owner, actor, enemy);
                        if (!enemy) continue;
                    }
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.LunarExploderKind &&
                        !kinds.Contains(actor.Archetype))
                    {
                        yield return VerifyLunarExploderCoopShard(owner, actor, enemy);
                        if (!enemy) continue;
                    }
                    kinds.Add(actor.Archetype);
                    if (bought && !weaponScalingChecked)
                    {
                        int beforeHit = enemy.CurrentHealth;
                        enemy.Server_ChangeHealthState(25, CharacterPart.body, AttackType.hitscan, owner, 0);
                        int expectedHit = enemy.CountDamage((byte)CharacterPart.body, 25 + 5 * (player.RainLevel - 1));
                        if (beforeHit - enemy.CurrentHealth != expectedHit) Debug.LogError("RAIN_COOP_SCALED_HIT_FAILED");
                        else Debug.Log($"RAIN_COOP_SCALED_HIT={expectedHit}");
                        weaponScalingChecked = true;
                    }
                    var killer = guest && guest.MyCharacter && guest.RainExperience == 0 && actor.Archetype <= 1
                        ? guest.MyCharacter : owner;
                    int experience = actor.Archetype switch
                    {
                        2 => 100,
                        RangeResearchEnemy.QueenKind => 120,
                        RangeResearchEnemy.BeetleGuardKind or RangeResearchEnemy.BisonKind => 8,
                        RangeResearchEnemy.BruiserKind => 23,
                        RangeResearchEnemy.GreaterWispKind => 40,
                        RangeResearchEnemy.ImpKind => 5,
                        RangeResearchEnemy.VerminKind => 2,
                        RangeResearchEnemy.FlyingVerminKind => 3,
                        RangeResearchEnemy.HermitCrabKind => 5,
                        RangeResearchEnemy.AcidLarvaKind => 2,
                        RangeResearchEnemy.LunarExploderKind => 16,
                        RangeResearchEnemy.ParentKind => 20,
                        RangeResearchEnemy.LemurianKind or RangeResearchEnemy.WispKind or RangeResearchEnemy.JellyfishKind => 2,
                        _ => 10
                    };
                    int gold = actor.Archetype switch
                    {
                        2 => 100,
                        RangeResearchEnemy.QueenKind => 240,
                        RangeResearchEnemy.BeetleGuardKind or RangeResearchEnemy.BisonKind => 16,
                        RangeResearchEnemy.BruiserKind => 46,
                        RangeResearchEnemy.GreaterWispKind => 80,
                        RangeResearchEnemy.ImpKind => 11,
                        RangeResearchEnemy.VerminKind => 5,
                        RangeResearchEnemy.FlyingVerminKind => 6,
                        RangeResearchEnemy.HermitCrabKind => 10,
                        RangeResearchEnemy.AcidLarvaKind => 4,
                        RangeResearchEnemy.LunarExploderKind => 32,
                        RangeResearchEnemy.ParentKind => 40,
                        RangeResearchEnemy.LemurianKind or RangeResearchEnemy.WispKind or RangeResearchEnemy.JellyfishKind => 4,
                        _ => 10
                    };
                    int goldBefore = player.RainGold;
                    int experienceBefore = killer == owner ? player.RainExperience : guest.RainExperience;
                    int killDamage = actor.Archetype == RangeResearchEnemy.QueenKind ? enemy.CurrentHealth * 4 : enemy.CurrentHealth;
                    enemy.Server_ChangeHealthStateRaw(killDamage,CharacterPart.body,AttackType.hitscan,killer,0);
                    int experienceAfter = killer == owner ? player.RainExperience : guest.RainExperience;
                    if (experienceAfter != experienceBefore + experience || player.RainGold != goldBefore + gold)
                        Debug.LogError($"RAIN_COOP_MONSTER_REWARD_FAILED kind={actor.Archetype}");
                    if (killer == owner) expectedHostExperience += experience;
                    if (waitForRainClient && actor.Archetype == RangeResearchEnemy.LunarExploderKind)
                        yield return new WaitForSeconds(RangeResearchEnemy.LunarExploderDeathDuration + .2f);
                }
                if (!bought && player.RainGold >= Gamemodes.RangeGamemode.RainChestPrice)
                {
                    bought = true;
                    var chest = mode.transform.Find("Rain supply chest 0");
                    if (!chest) { Debug.LogError("RAIN_COOP_MISSING_CHEST"); break; }
                    owner.transform.position = chest.position;
                    Physics.SyncTransforms();
                    int before = player.RainGold;
                    if (!player.TryInteractRainNode()) Debug.LogError("RAIN_COOP_INTERACT_CHEST");
                    yield return new WaitForSeconds(.25f);
                    mode.BuyRainChest(player, 0);
                    if (player.RainGold != before - Gamemodes.RangeGamemode.RainChestPrice ||
                        player.RainSteak != 1 || chest.gameObject.activeSelf)
                        Debug.LogError("RAIN_COOP_CHEST_TRANSACTION");
                    else Debug.Log("RAIN_COOP_CHEST_CHECKED");
                    var hud = UI.HUD.ReferenceBattleHud.MonsterOverlay;
                    var icon = hud ? hud.Find("Combat inventory/Inventory item 0") : null;
                    if (!icon || !icon.gameObject.activeInHierarchy ||
                        icon.GetComponent<UnityEngine.UI.Image>().sprite?.name != "texSteakIcon")
                        Debug.LogError("RAIN_COOP_ORIGINAL_ITEM_ICON_MISSING");
                    else Debug.Log("RAIN_COOP_ORIGINAL_ITEM_ICON_CHECKED");
                    int expectedHealth = baseHealth + Mathf.RoundToInt(baseHealth * .30f) * (player.RainLevel - 1) + 25;
                    if (owner.MaxHealth != expectedHealth)
                        Debug.LogError("RAIN_COOP_LEVEL_HEALTH_FAILED");
                    var researchItems = owner.GetComponent<RangeResearchItems>();
                    int syringeIndex = RangeResearchItems.CatalogIndex("Syringe");
                    researchItems.Grant(syringeIndex);
                    researchItems.Grant(syringeIndex);
                    if (!Mathf.Approximately(owner.ResearchAttackSpeed, 1.30f) || !researchItems.Remove(syringeIndex) ||
                        !Mathf.Approximately(owner.ResearchAttackSpeed, 1.15f))
                        Debug.LogError("RAIN_COOP_SYRINGE_STACKS_FAILED");
                    researchItems.Grant(RangeResearchItems.CatalogIndex("WardOnLevel"));
                    int knurl = RangeResearchItems.CatalogIndex("Knurl");
                    int scope = RangeResearchItems.CatalogIndex("CritDamage");
                    int scythe = RangeResearchItems.CatalogIndex("HealOnCrit");
                    int pearl = RangeResearchItems.CatalogIndex("Pearl");
                    researchItems.Grant(knurl);
                    researchItems.Grant(scope);
                    researchItems.Grant(scythe);
                    researchItems.Grant(pearl);
                    int ignitionTank = RangeResearchItems.CatalogIndex("StrengthenBurn");
                    researchItems.Grant(ignitionTank);
                    researchItems.Grant(ignitionTank);
                    int beforePearl = expectedHealth + 40;
                    int expectedPearlHealth = (int)Math.Round(beforePearl * 1.1d);
                    if (owner.MaxHealth != expectedPearlHealth)
                        Debug.LogError($"RAIN_COOP_PEARL_BEFORE_RESPAWN max={owner.MaxHealth} expected={expectedPearlHealth} unscaled={beforePearl}");
                    if (player.ServerTryUseRangeEquipment() || !researchItems.Equip(RangeResearchItems.Equipment.Fruit) ||
                        !player.ServerTryUseRangeEquipment())
                        Debug.LogError("RAIN_COOP_FRUIT_BEFORE_RESPAWN_FAILED");
                    float fruitReadyAt = Time.time + researchItems.FruitCooldownRemaining;
                    player.RecordRainChestItems(researchItems);
                    mode.PlayerSpawnCharacterRequest(player);
                    owner = player.MyCharacter;
                    owner.RangeInvulnerable = true;
                    if (owner.MaxHealth != expectedPearlHealth || owner.CurrentHealth != expectedPearlHealth ||
                        !owner.TryGetComponent<RangeResearchItems>(out var restoredItems) || restoredItems.Steak != 1)
                        Debug.LogError($"RAIN_COOP_RESPAWN_ITEMS_FAILED max={owner.MaxHealth} current={owner.CurrentHealth} expected={expectedPearlHealth} steak={owner.GetComponent<RangeResearchItems>()?.Steak}");
                    else Debug.Log("RAIN_COOP_RESPAWN_ITEMS_CHECKED");
                    if (owner.TryGetComponent<RangeResearchItems>(out var restoredExtras))
                    {
                        if (!restoredExtras.Remove(pearl) || owner.MaxHealth != beforePearl ||
                            owner.CurrentHealth != beforePearl)
                            Debug.LogError("RAIN_COOP_PEARL_RESPAWN_REMOVE");
                        else Debug.Log("RAIN_COOP_PEARL_RESPAWN_CHECKED");
                        if (restoredExtras.ScaleBurnDamage(10) != 70 || !restoredExtras.Remove(ignitionTank) ||
                            restoredExtras.ScaleBurnDamage(10) != 40 || !restoredExtras.Remove(ignitionTank) ||
                            restoredExtras.ScaleBurnDamage(10) != 10)
                            Debug.LogError("RAIN_COOP_IGNITION_TANK_RESPAWN");
                        else Debug.Log("RAIN_COOP_IGNITION_TANK_RESPAWN_CHECKED");
                        if (!restoredExtras.FruitEquipped || restoredExtras.TryUseEquipment() ||
                            Mathf.Abs(Time.time + restoredExtras.FruitCooldownRemaining - fruitReadyAt) > .05f ||
                            !restoredExtras.Remove(RangeResearchItems.Equipment.Fruit) || !restoredExtras.Remove(knurl) ||
                            !restoredExtras.Remove(scope) || !restoredExtras.Remove(scythe) ||
                            owner.MaxHealth != expectedHealth)
                            Debug.LogError("RAIN_COOP_EXTRA_ITEMS_RESPAWN_FAILED");
                        else Debug.Log("RAIN_COOP_EXTRA_ITEMS_RESPAWN_CHECKED");
                        player.RecordRainChestItems(restoredExtras);
                    }
                    if (owner.ResearchSyringes != 1 || !Mathf.Approximately(owner.ResearchAttackSpeed, 1.15f))
                        Debug.LogError("RAIN_COOP_SYRINGE_RESPAWN_FAILED");
                    else Debug.Log("RAIN_COOP_SYRINGE_CHECKED");
                    float expectedRegen = 1f + .20f * (player.RainLevel - 1);
                    owner.CurrentHealth = expectedHealth - 5;
                    yield return new WaitForSeconds(1.1f);
                    if (!Mathf.Approximately(owner.RainHealthRegen, expectedRegen) || owner.CurrentHealth <= expectedHealth - 5)
                        Debug.LogError("RAIN_COOP_REGEN_FAILED");
                    else Debug.Log($"RAIN_COOP_REGEN_CHECKED rate={owner.RainHealthRegen} healed={owner.CurrentHealth - expectedHealth + 5}");
                }
                if (bought && !activated && mode.RainKills >= Gamemodes.RangeGamemode.RainKillsForBoss)
                {
                    activated = true;
                    yield return new WaitForSeconds(2.1f);
                    if (mode.RainBossActive) Debug.LogError("RAIN_COOP_BOSS_BEFORE_BEACONS");
                    int before = player.RainGold;
                    for (int i = 0; i < Gamemodes.RangeGamemode.RainBeaconCount; i++)
                    {
                        var beacon = mode.transform.Find("Rain task beacon " + i);
                        if (!beacon) { Debug.LogError("RAIN_COOP_MISSING_BEACON"); break; }
                        if (i == 0)
                        {
                            yield return VerifyFireworkBeacon(mode, player, beacon, i);
                            continue;
                        }
                        if (i == 1)
                        {
                            yield return VerifyLeptonDaisyBeacon(mode, player, beacon, i);
                            continue;
                        }
                        owner.transform.position = beacon.position;
                        Physics.SyncTransforms();
                        if (!player.TryInteractRainNode()) Debug.LogError("RAIN_COOP_INTERACT_BEACON");
                        yield return new WaitForSeconds(.2f);
                        mode.ActivateRainBeacon(player, i);
                        if (mode.RainBeaconsActivated != i + 1) Debug.LogError("RAIN_COOP_BEACON_PROGRESS");
                    }
                    if (player.RainGold != before + 60) Debug.LogError("RAIN_COOP_BEACON_REWARD");
                    Debug.Log("RAIN_COOP_BEACONS_CHECKED");
                }
                yield return new WaitForSeconds(.25f);
            }
            if (!bought || !mode.RainVictory || mode.RainKills < Gamemodes.RangeGamemode.RainKillsForBoss ||
                kinds.Count != Gamemodes.RangeGamemode.RainKillsForBoss + 1 ||
                !kinds.Contains(RangeResearchEnemy.BeetleGuardKind) || !kinds.Contains(RangeResearchEnemy.LemurianKind) ||
                !kinds.Contains(RangeResearchEnemy.WispKind) || !kinds.Contains(RangeResearchEnemy.GreaterWispKind) ||
                !kinds.Contains(RangeResearchEnemy.ImpKind) || !kinds.Contains(RangeResearchEnemy.BisonKind) ||
                !kinds.Contains(RangeResearchEnemy.VerminKind) ||
                !kinds.Contains(RangeResearchEnemy.FlyingVerminKind) ||
                !kinds.Contains(RangeResearchEnemy.HermitCrabKind) ||
                !kinds.Contains(RangeResearchEnemy.AcidLarvaKind) ||
                !kinds.Contains(RangeResearchEnemy.LunarExploderKind) ||
                !kinds.Contains(RangeResearchEnemy.ParentKind))
                Debug.LogError($"RAIN_COOP_WAVE_FAILED kills={mode.RainKills} kinds={kinds.Count} victory={mode.RainVictory}");
            if (player.RainExperience != expectedHostExperience ||
                player.RainLevel <= 1 || (guest && (guest.RainExperience != 10 || guest.RainLevel != 1)))
                Debug.LogError($"RAIN_COOP_PLAYER_EXPERIENCE_FAILED xp={player.RainExperience} kills={mode.RainKills} guestJoined={guestJoined}");
            else Debug.Log($"RAIN_COOP_HOST_EXPERIENCE={player.RainExperience} LEVEL={player.RainLevel}");
            int finalHealth = baseHealth + Mathf.RoundToInt(baseHealth * .30f) * (player.RainLevel - 1) + 25;
            float expectedAttack = 12f + 2.4f * (player.RainLevel - 1);
            if (!weaponScalingChecked || owner.MaxHealth != finalHealth || !Mathf.Approximately(owner.RainAttackPower, expectedAttack) ||
                owner.ScaleRainWeaponDamage(25) != 25 + 5 * (player.RainLevel - 1))
                Debug.LogError("RAIN_COOP_CHARACTER_GROWTH_FAILED");
            else Debug.Log($"RAIN_COOP_CHARACTER_GROWTH hp={owner.MaxHealth} atk={owner.RainAttackPower}");
            var banner = mode.transform.Find("Rain warbanner");
            if (!banner || !banner.TryGetComponent<LineRenderer>(out var ring))
                Debug.LogError("RAIN_COOP_WARBANNER_MISSING");
            else
            {
                var center = (ring.GetPosition(0) + ring.GetPosition(ring.positionCount / 2)) * .5f;
                var positionBeforeBannerCheck = owner.transform.position;
                owner.transform.position = center + Vector3.up;
                yield return null;
                yield return null;
                var motor = owner.GetComponent<CharacterMotor>();
                if (!owner.ResearchBannerBuff || !Mathf.Approximately(owner.ResearchAttackSpeed, 1.45f) ||
                    !Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.3f))
                    Debug.LogError("RAIN_COOP_WARBANNER_BUFF_FAILED");
                float outsideX = ring.GetPosition(0).x;
                foreach (Transform child in mode.transform)
                    if (child.name == "Rain warbanner" && child.TryGetComponent<LineRenderer>(out var otherRing))
                        outsideX = Mathf.Max(outsideX, otherRing.GetPosition(0).x);
                bool motorEnabled = motor.enabled;
                motor.enabled = false;
                owner.transform.position = new Vector3(outsideX + 5f, center.y + 1f, center.z);
                Physics.SyncTransforms();
                yield return null;
                yield return null;
                if (owner.ResearchBannerBuff) Debug.LogError("RAIN_COOP_WARBANNER_EXIT_FAILED");
                else Debug.Log("RAIN_COOP_WARBANNER_CHECKED");
                owner.transform.position = positionBeforeBannerCheck;
                motor.enabled = motorEnabled;
                Physics.SyncTransforms();
            }
            if (waitForRainClient) yield return new WaitForSeconds(.6f);
            if (mode.RainVictory && mode.RainBossKind == RangeResearchEnemy.QueenKind)
            {
                var drops = mode.transform.Cast<Transform>()
                    .Where(child => child.name.StartsWith("Rain boss reward ", StringComparison.Ordinal)).ToArray();
                int expectedDrops = guestJoined ? 2 : 1;
                if (drops.Length != expectedDrops)
                    Debug.LogError($"RAIN_COOP_QUEEN_DROP_COUNT expected={expectedDrops} actual={drops.Length}");
                if (drops.Length > 0)
                {
                    Vector3 previousPosition = owner.transform.position;
                    owner.transform.position = drops[0].position;
                    Physics.SyncTransforms();
                    yield return null;
                    yield return null;
                    if (drops[0]) Debug.LogError("RAIN_COOP_QUEEN_DROP_NOT_COLLECTED");
                    owner.transform.position = previousPosition;
                    Physics.SyncTransforms();
                }
            }
        }
        IEnumerator VerifyShatterspleen(CharacterInstance owner, CharacterInstance bystander, RangeResearchItems items)
        {
            Vector3 previousPosition = bystander.transform.position;
            int previousHealth = bystander.CurrentHealth;
            Vector3 position = owner.transform.position + Vector3.up * 15f;
            bystander.transform.position = position + Vector3.right * 6f;
            var victim = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), position, Quaternion.identity)
                .GetComponent<CharacterInstance>();
            foreach (var slot in victim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            victim.Team = bystander.Team;
            victim.MaxHealth = victim.CurrentHealth = 100;
            NetworkServer.Spawn(victim.gameObject);
            victim.GetComponent<CharacterMotor>().enabled = false;

            int spleen = RangeResearchItems.CatalogIndex("BleedOnHitAndExplode");
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            items.Grant(spleen);
            items.Grant(spleen);
            for (int i = 0; i < 10; i++) items.Grant(glasses);
            victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            if (victim.TryGetComponent<RangeBleeding>(out var zeroProcBleed) && zeroProcBleed.HasBleed())
                Debug.LogError("RANGE_SMOKE_SHATTERSPLEEN_ZERO_PROC");
            victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0);
            if (!victim.TryGetComponent<RangeBleeding>(out var criticalBleed) || !criticalBleed.HasBleed())
                Debug.LogError("RANGE_SMOKE_SHATTERSPLEEN_CRIT_BLEED");
            victim.CurrentHealth = 1;
            yield return new WaitForSeconds(.85f);

            float attack = owner.RainAttackPower > 0f ? owner.RainAttackPower : CharacterInstance.RainBaseAttackPower;
            int explosion = Mathf.RoundToInt((4f * attack + 15f) * 4f);
            if ((victim && victim.CurrentHealth > 0) || bystander.CurrentHealth != previousHealth - explosion)
                Debug.LogError("RANGE_SMOKE_SHATTERSPLEEN_BLEED_KILL_EXPLOSION");
            else Debug.Log("RANGE_SMOKE_SHATTERSPLEEN_CHECKED");
            if (victim) NetworkServer.Destroy(victim.gameObject);
            bystander.transform.position = previousPosition;
            bystander.CurrentHealth = previousHealth;
            items.Clear();
        }

        IEnumerator VerifyHealingSkull(CharacterInstance owner, CharacterInstance target, RangeResearchItems items)
        {
            Vector3 ownerPosition = owner.transform.position;
            Vector3 targetPosition = target.transform.position;
            int ownerHealth = owner.CurrentHealth;
            int targetHealth = target.CurrentHealth;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            motor.enabled = false;
            owner.transform.position = ownerPosition + Vector3.up * 100f;
            target.transform.position = owner.transform.position + Vector3.forward * 12f;
            int skull = RangeResearchItems.CatalogIndex("NovaOnHeal");
            items.Grant(skull);
            items.Grant(skull);
            owner.CurrentHealth = owner.MaxHealth;
            int healing = Mathf.CeilToInt(owner.MaxHealth * .05f);
            owner.ServerHeal(healing, owner.netId);
            yield return new WaitForSeconds(.1f);

            var obstacle = new GameObject("Healing skull smoke obstacle");
            obstacle.transform.position = owner.transform.position + Vector3.forward * 6f + Vector3.up;
            var collider = obstacle.AddComponent<BoxCollider>();
            collider.size = new Vector3(3f, 3f, 1f);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.55f);
            if (target.CurrentHealth != targetHealth)
                Debug.LogError("RANGE_SMOKE_HEALING_SKULL_OCCLUDED");
            collider.enabled = false;
            Destroy(obstacle);
            Physics.SyncTransforms();

            owner.ServerHeal(healing, owner.netId);
            yield return new WaitForSeconds(.1f);
            target.transform.position += Vector3.right * 6f;
            yield return new WaitForSeconds(.75f);
            if (target.CurrentHealth != targetHealth - 5 * healing)
                Debug.LogError("RANGE_SMOKE_HEALING_SKULL_TRACKING");
            else Debug.Log("RANGE_SMOKE_HEALING_SKULL_CHECKED");

            owner.transform.position = ownerPosition;
            target.transform.position = targetPosition;
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;
            motor.enabled = motorEnabled;
            items.Clear();
            Physics.SyncTransforms();
        }

        IEnumerator VerifyAreaItemTargetLimits(CharacterInstance owner, RangeResearchItems items, int razorwire, int razorwireDamage, int tesla, int teslaDamage)
        {
            const int targetHealth = 1000;
            var enemies = new CharacterInstance[8];
            Vector3 originalPosition = owner.transform.position;
            int originalHealth = owner.CurrentHealth;
            int razorwireGranted = 0;
            int teslaGranted = 0;
            try
            {
                owner.transform.position = originalPosition + Vector3.up * 150f;
                for (int i = enemies.Length - 1; i >= 0; i--)
                {
                    var enemy = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        owner.transform.position + Vector3.right * (3f + i * 2f), Quaternion.identity)
                        .GetComponent<CharacterInstance>();
                    enemies[i] = enemy;
                    foreach (var slot in enemy.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    enemy.Team = owner.Team == 0 ? 1 : 0;
                    enemy.MaxHealth = enemy.CurrentHealth = targetHealth;
                    NetworkServer.Spawn(enemy.gameObject);
                    enemy.GetComponent<CharacterMotor>().enabled = false;
                }

                bool passed = true;
                for (int stacks = 1; stacks <= 2; stacks++)
                {
                    items.Grant(razorwire);
                    razorwireGranted++;
                    owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, enemies[7], 0, 0f);
                    yield return null;
                    yield return null;
                    int expectedHits = stacks == 1 ? 5 : 7;
                    for (int i = 0; i < enemies.Length; i++)
                    {
                        int expectedHealth = i < expectedHits ? targetHealth - razorwireDamage : targetHealth;
                        passed &= enemies[i].CurrentHealth == expectedHealth;
                        enemies[i].CurrentHealth = targetHealth;
                    }
                }
                if (passed && owner.CurrentHealth == originalHealth - 2)
                    Debug.Log("RANGE_SMOKE_RAZORWIRE_TARGET_LIMIT_CHECKED");
                else Debug.LogError("RANGE_SMOKE_RAZORWIRE_TARGET_LIMIT");

                while (razorwireGranted > 0)
                {
                    items.Remove(razorwire);
                    razorwireGranted--;
                }

                bool teslaPassed = true;
                for (int stacks = 1; stacks <= 2; stacks++)
                {
                    items.Grant(tesla);
                    teslaGranted++;
                    float deadline = Time.time + 1.5f;
                    yield return new WaitUntil(() => enemies[0].CurrentHealth != targetHealth || Time.time >= deadline);
                    int expectedHits = stacks == 1 ? 3 : 5;
                    for (int i = 0; i < enemies.Length; i++)
                    {
                        int expectedHealth = i < expectedHits ? targetHealth - teslaDamage : targetHealth;
                        teslaPassed &= enemies[i].CurrentHealth == expectedHealth;
                        enemies[i].CurrentHealth = targetHealth;
                    }
                }
                if (teslaPassed) Debug.Log("RANGE_SMOKE_TESLA_TARGET_LIMIT_CHECKED");
                else Debug.LogError("RANGE_SMOKE_TESLA_TARGET_LIMIT");
            }
            finally
            {
                foreach (var enemy in enemies)
                    if (enemy) NetworkServer.Destroy(enemy.gameObject);
                for (int i = 0; i < razorwireGranted; i++) items.Remove(razorwire);
                for (int i = 0; i < teslaGranted; i++) items.Remove(tesla);
                owner.transform.position = originalPosition;
                owner.CurrentHealth = originalHealth;
            }
        }

        static IEnumerator VerifyIgnitionTank(CharacterInstance owner, CharacterInstance target, RangeResearchItems items)
        {
            int health = target.CurrentHealth;
            int tank = RangeResearchItems.CatalogIndex("StrengthenBurn");
            if (!target.TryGetComponent<RangeBleeding>(out var dots))
                dots = target.gameObject.AddComponent<RangeBleeding>();
            items.Grant(tank);
            items.Grant(tank);
            dots.AddBurn(owner, 10, 1);
            bool removed = items.Remove(tank);
            dots.AddBurn(owner, 10, 1);
            removed &= items.Remove(tank);
            dots.AddBurn(owner, 10, 1);
            yield return new WaitForSeconds(.25f);
            if (removed && target.CurrentHealth == health - 120)
                Debug.Log("RANGE_SMOKE_IGNITION_TANK_CHECKED");
            else Debug.LogError($"RANGE_SMOKE_IGNITION_TANK damage={health - target.CurrentHealth} removed={removed}");
            target.CurrentHealth = health;
        }

        void VerifyEffigy(CharacterInstance owner, CharacterInstance target, RangeResearchItems items)
        {
            var mode = (Gamemodes.RangeGamemode)GameManager.Gamemode;
            Vector3 originalPosition = owner.transform.position;
            Vector3 originalTargetPosition = target.transform.position;
            Vector2 originalLook = owner.lookInput;
            int ownerHealth = owner.CurrentHealth;
            int targetHealth = target.CurrentHealth;
            var motor = owner.GetComponent<CharacterMotor>();
            float initialSpeed = motor.GetMoveSpeed(false);
            var equipment = RangeResearchItems.Equipment.Effigy;
            int fuel = RangeResearchItems.CatalogIndex("EquipmentMagazine");
            int fuelGranted = 0;
            Vector3 start = originalPosition + Vector3.up * 150f;
            var floor = new GameObject("Rain effigy smoke floor").AddComponent<BoxCollider>();
            try
            {
                floor.transform.position = start + new Vector3(100f, -4f, 0f);
                floor.size = new Vector3(240f, 1f, 20f);
                owner.transform.position = start;
                target.transform.position = start + Vector3.right * 3f;
                Physics.SyncTransforms();
                owner.lookInput = new Vector2(-89f, 0f);
                bool refusedMiss = items.Equip(equipment) && !items.TryUseEquipment() &&
                    items.EquipmentCooldownRemaining == 0f;
                owner.lookInput = new Vector2(89f, 0f);
                bool placed = items.TryUseEquipment() && Mathf.Abs(items.EquipmentCooldownRemaining - 15f) < .05f;
                target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
                owner.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
                bool affectsBothTeams = Mathf.Approximately(motor.GetMoveSpeed(false), initialSpeed * .5f) &&
                    target.CurrentHealth == targetHealth - 23 && owner.CurrentHealth == ownerHealth - 23;
                for (; fuelGranted < 5; fuelGranted++) items.Grant(fuel);
                bool laterPlacements = true;
                for (int i = 1; i <= 5; i++)
                {
                    owner.transform.position = start + Vector3.right * (40f * i);
                    Physics.SyncTransforms();
                    laterPlacements &= items.TryUseEquipment();
                }
                bool oldestRemoved = !mode.IsWithinRainEffigy(start) &&
                    mode.IsWithinRainEffigy(start + Vector3.right * 40f) && mode.IsWithinRainEffigy(owner.transform.position);
                if (refusedMiss && placed && affectsBothTeams && laterPlacements && oldestRemoved)
                    Debug.Log("RANGE_SMOKE_EFFIGY_CHECKED");
                else Debug.LogError($"RANGE_SMOKE_EFFIGY miss={refusedMiss} placed={placed} bothTeams={affectsBothTeams} later={laterPlacements} oldest={oldestRemoved}");
            }
            finally
            {
                for (int i = 0; i < fuelGranted; i++) items.Remove(fuel);
                items.Remove(equipment);
                owner.lookInput = originalLook;
                owner.transform.position = originalPosition;
                target.transform.position = originalTargetPosition;
                owner.CurrentHealth = ownerHealth;
                target.CurrentHealth = targetHealth;
                Destroy(floor.gameObject);
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyLittleDisciple(CharacterInstance owner, CharacterInstance target, RangeResearchItems items)
        {
            Vector3 ownerPosition = owner.transform.position;
            Vector3 targetPosition = target.transform.position;
            int targetHealth = target.CurrentHealth;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            bool previousRunning = owner.IsRunning;
            motor.enabled = false;
            owner.transform.position = ownerPosition + Vector3.up * 100f;
            target.transform.position = owner.transform.position + Vector3.forward * 3f;

            int disciple = RangeResearchItems.CatalogIndex("SprintWisp");
            items.Grant(disciple);
            items.Grant(disciple);
            owner.IsRunning = true;
            yield return new WaitForSeconds(.9f);
            owner.IsRunning = false;
            yield return new WaitForSeconds(.25f);
            if (target.CurrentHealth != targetHealth)
                Debug.LogError("RANGE_SMOKE_LITTLE_DISCIPLE_EARLY");

            int hoof = RangeResearchItems.CatalogIndex("Hoof");
            for (int i = 0; i < 8; i++) items.Grant(hoof);
            int glass = RangeResearchItems.CatalogIndex("LunarDagger");
            int ownerHealth = owner.CurrentHealth;
            items.Grant(glass);
            items.Grant(glass);
            owner.IsRunning = true;
            yield return new WaitForSeconds(1.1f);
            owner.IsRunning = false;
            float attack = owner.RainAttackPower > 0f ? owner.RainAttackPower : CharacterInstance.RainBaseAttackPower;
            if (target.CurrentHealth != targetHealth - Mathf.RoundToInt(18f * attack))
                Debug.LogError($"RANGE_SMOKE_LITTLE_DISCIPLE_SPEED_OR_DAMAGE loss={targetHealth - target.CurrentHealth} expected={Mathf.RoundToInt(18f * attack)} attack={attack}");
            else Debug.Log("RANGE_SMOKE_LITTLE_DISCIPLE_CHECKED");
            items.Clear();
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;
            int ceremonialDagger = RangeResearchItems.CatalogIndex("Dagger");
            items.Grant(ceremonialDagger);
            items.Grant(ceremonialDagger);
            items.Grant(RangeResearchItems.CatalogIndex("Seed"));
            owner.CurrentHealth = 1;
            bool daggerKill = HitAndKillSmokeTarget(owner, items);
            yield return new WaitForSeconds(1f);
            if (!daggerKill || target.CurrentHealth != targetHealth - 3 * Mathf.RoundToInt(3f * attack) ||
                owner.CurrentHealth != 4)
                Debug.LogError($"RANGE_SMOKE_CEREMONIAL_DAGGER kill={daggerKill} loss={targetHealth - target.CurrentHealth} expected={3 * Mathf.RoundToInt(3f * attack)} ownerHp={owner.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_CEREMONIAL_DAGGER_CHECKED");
            items.Clear();
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;
            int razorwire = RangeResearchItems.CatalogIndex("Thorns");
            var targetItems = target.GetComponent<RangeResearchItems>();
            if (!targetItems) targetItems = target.gameObject.AddComponent<RangeResearchItems>();
            items.Grant(razorwire);
            targetItems.Grant(razorwire);
            owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
            yield return null;
            yield return null;
            if (owner.CurrentHealth != ownerHealth - 1 ||
                target.CurrentHealth != targetHealth - Mathf.RoundToInt(1.6f * attack))
                Debug.LogError($"RANGE_SMOKE_RAZORWIRE ownerLoss={ownerHealth - owner.CurrentHealth} targetLoss={targetHealth - target.CurrentHealth} expected={Mathf.RoundToInt(1.6f * attack)}");
            else Debug.Log("RANGE_SMOKE_RAZORWIRE_CHECKED");
            targetItems.Remove(razorwire);
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;

            int shield = RangeResearchItems.CatalogIndex("PersonalShield");
            items.Grant(shield);
            int shieldBeforeRazorwire = owner.ResearchShield;
            Vector3 nearPosition = target.transform.position;
            target.transform.position = owner.transform.position + Vector3.forward * 30f;
            owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
            yield return null;
            yield return null;
            bool outsideSingleStack = target.CurrentHealth == targetHealth;

            items.Grant(razorwire);
            owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
            yield return null;
            yield return null;
            int expectedRazorwireHealth = targetHealth - Mathf.RoundToInt(1.6f * attack);
            bool hitWithTwoStacks = target.CurrentHealth == expectedRazorwireHealth;

            bool removedRazorwire = items.Remove(razorwire);
            owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
            yield return null;
            yield return null;
            if (!outsideSingleStack || !hitWithTwoStacks || !removedRazorwire ||
                target.CurrentHealth != expectedRazorwireHealth || owner.CurrentHealth != ownerHealth ||
                owner.ResearchShield != shieldBeforeRazorwire - 3)
                Debug.LogError("RANGE_SMOKE_RAZORWIRE_SHIELD_RADIUS");
            else Debug.Log("RANGE_SMOKE_RAZORWIRE_SHIELD_RADIUS_CHECKED");
            items.Remove(razorwire);
            items.Remove(shield);
            target.transform.position = nearPosition;
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;
            int tesla = RangeResearchItems.CatalogIndex("ShockNearby");
            int teslaDamage = Mathf.RoundToInt(2f * attack);
            yield return VerifyAreaItemTargetLimits(owner, items, razorwire, Mathf.RoundToInt(1.6f * attack), tesla, teslaDamage);
            float teslaStart = Time.time;
            items.Grant(tesla);
            yield return new WaitUntil(() => target.CurrentHealth != targetHealth || Time.time >= teslaStart + 1.5f);
            bool firstTeslaPulse = target.CurrentHealth == targetHealth - teslaDamage;
            yield return new WaitUntil(() => Time.time >= teslaStart + 10.25f);
            int teslaOffHealth = target.CurrentHealth;
            yield return new WaitUntil(() => target.CurrentHealth != teslaOffHealth || Time.time >= teslaStart + 19.75f);
            bool teslaStayedOff = target.CurrentHealth == teslaOffHealth;
            yield return new WaitUntil(() => target.CurrentHealth != teslaOffHealth || Time.time >= teslaStart + 21f);
            int afterTeslaRestart = target.CurrentHealth;
            bool teslaRestarted = Time.time >= teslaStart + 20f && afterTeslaRestart == teslaOffHealth - teslaDamage;
            bool teslaRemoved = items.Remove(tesla);
            yield return new WaitForSeconds(.65f);
            if (!firstTeslaPulse || !teslaStayedOff || !teslaRestarted || !teslaRemoved ||
                target.CurrentHealth != afterTeslaRestart)
                Debug.LogError($"RANGE_SMOKE_TESLA_COIL first={firstTeslaPulse} off={teslaStayedOff} restart={teslaRestarted} removed={teslaRemoved} restartLoss={teslaOffHealth - afterTeslaRestart} finalLoss={afterTeslaRestart - target.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_TESLA_COIL_CHECKED");
            target.CurrentHealth = targetHealth;
            yield return new WaitUntil(() => NetworkTime.time >= owner.ResearchRedWhipReadyAt);
            int urn = RangeResearchItems.CatalogIndex("SiphonOnLowHealth");
            var secondUrnTarget = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.right * 8f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in secondUrnTarget.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            secondUrnTarget.Team = target.Team;
            secondUrnTarget.MaxHealth = secondUrnTarget.CurrentHealth = targetHealth;
            NetworkServer.Spawn(secondUrnTarget.gameObject);
            secondUrnTarget.GetComponent<CharacterMotor>().enabled = false;

            items.Grant(urn);
            owner.CurrentHealth = 1;
            yield return new WaitForSeconds(.25f);
            bool urnStayedIdle = target.CurrentHealth == targetHealth && owner.CurrentHealth == 1;
            target.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            double combatExpiry = owner.ResearchRedWhipReadyAt;
            float urnDeadline = Time.time + 1.5f;
            yield return new WaitUntil(() => target.CurrentHealth < targetHealth - 1 || Time.time >= urnDeadline);
            int urnDamage = Mathf.RoundToInt(attack);
            bool singleUrn = target.CurrentHealth == targetHealth - 1 - urnDamage &&
                secondUrnTarget.CurrentHealth == targetHealth && owner.CurrentHealth == 1 + urnDamage &&
                target.ResearchTarUntil > NetworkTime.time;
            items.Grant(urn);
            urnDeadline = Time.time + 1.5f;
            yield return new WaitUntil(() => secondUrnTarget.CurrentHealth != targetHealth || Time.time >= urnDeadline);
            bool doubleUrn = target.CurrentHealth == targetHealth - 1 - 2 * urnDamage &&
                secondUrnTarget.CurrentHealth == targetHealth - urnDamage &&
                owner.CurrentHealth == Mathf.Min(owner.MaxHealth, 1 + 3 * urnDamage) &&
                secondUrnTarget.ResearchTarUntil > NetworkTime.time;
            if (!urnStayedIdle || !singleUrn || !doubleUrn || owner.ResearchRedWhipReadyAt != combatExpiry)
                Debug.LogError($"RANGE_SMOKE_MIRED_URN idle={urnStayedIdle} single={singleUrn} double={doubleUrn} firstLoss={targetHealth - target.CurrentHealth} secondLoss={targetHealth - secondUrnTarget.CurrentHealth} ownerHp={owner.CurrentHealth} combatExtended={owner.ResearchRedWhipReadyAt != combatExpiry}");
            else Debug.Log("RANGE_SMOKE_MIRED_URN_CHECKED");
            if (secondUrnTarget) NetworkServer.Destroy(secondUrnTarget.gameObject);
            items.Remove(urn);
            items.Remove(urn);
            target.ResearchTarUntil = 0d;
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;
            int frostRelic = RangeResearchItems.CatalogIndex("Icicle");
            items.Grant(frostRelic);
            float firstFrostKillAt = Time.time;
            bool frostKill = HitAndKillSmokeTarget(owner, items);
            float frostDeadline = Time.time + 1f;
            yield return new WaitUntil(() => target.CurrentHealth != targetHealth || Time.time >= frostDeadline);
            int afterFrostPulse = target.CurrentHealth;
            bool chilled = target.ResearchIceSlowUntil > NetworkTime.time && owner.ResearchFrostRelicRadius == 8f;
            Vector3 frostTargetPosition = target.transform.position;
            target.transform.position = owner.transform.position + Vector3.forward * 9f;
            yield return new WaitUntil(() => Time.time >= firstFrostKillAt + 1.5f);
            bool outsideFirstRadius = target.CurrentHealth == afterFrostPulse;

            bool secondFrostKill = HitAndKillSmokeTarget(owner, items);
            frostDeadline = Time.time + .65f;
            yield return new WaitUntil(() => target.CurrentHealth != afterFrostPulse || Time.time >= frostDeadline);
            bool expanded = target.CurrentHealth == afterFrostPulse - Mathf.RoundToInt(3f * attack) && owner.ResearchFrostRelicRadius == 10f;

            yield return new WaitUntil(() => Time.time >= firstFrostKillAt + 5.1f);
            int afterFirstExpiry = target.CurrentHealth;
            yield return new WaitForSeconds(.35f);
            bool shrank = target.CurrentHealth == afterFirstExpiry && owner.ResearchFrostRelicRadius == 8f;
            target.transform.position = frostTargetPosition;
            frostDeadline = Time.time + .5f;
            yield return new WaitUntil(() => target.CurrentHealth != afterFirstExpiry || Time.time >= frostDeadline);
            int beforeFrostRemove = target.CurrentHealth;
            bool secondKillStillActive = beforeFrostRemove == afterFirstExpiry - Mathf.RoundToInt(3f * attack);

            bool frostRemoved = items.Remove(frostRelic);
            yield return new WaitForSeconds(.35f);
            if (!frostKill || !chilled || afterFrostPulse != targetHealth - Mathf.RoundToInt(3f * attack) ||
                !outsideFirstRadius || !secondFrostKill || !expanded || !shrank || !secondKillStillActive ||
                !frostRemoved || owner.ResearchFrostRelicRadius != 0f || target.CurrentHealth != beforeFrostRemove)
                Debug.LogError($"RANGE_SMOKE_FROST_RELIC kill={frostKill}/{secondFrostKill} firstLoss={targetHealth - afterFrostPulse} chilled={chilled} outside={outsideFirstRadius} expanded={expanded} shrank={shrank} stillActive={secondKillStillActive} removed={frostRemoved} radius={owner.ResearchFrostRelicRadius}");
            else Debug.Log("RANGE_SMOKE_FROST_RELIC_CHECKED");
            target.ResearchIceSlowUntil = 0d;
            target.CurrentHealth = targetHealth;
            target.transform.position = owner.transform.position + Vector3.forward * 24f;
            items.Grant(frostRelic);
            bool frostCapKills = true;
            for (int i = 0; i < 13; i++) frostCapKills &= HitAndKillSmokeTarget(owner, items);
            yield return new WaitForSeconds(.55f);
            bool singleFrostCap = owner.ResearchFrostRelicRadius == 18f && target.CurrentHealth == targetHealth;

            items.Grant(frostRelic);
            frostDeadline = Time.time + .65f;
            yield return new WaitUntil(() => target.CurrentHealth != targetHealth || Time.time >= frostDeadline);
            int afterFrostCapPulse = target.CurrentHealth;
            bool doubleFrostCap = owner.ResearchFrostRelicRadius == 30f &&
                afterFrostCapPulse == targetHealth - Mathf.RoundToInt(3f * attack);

            bool removedFrostStack = items.Remove(frostRelic);
            yield return new WaitForSeconds(.55f);
            bool returnedToSingleCap = owner.ResearchFrostRelicRadius == 18f && target.CurrentHealth == afterFrostCapPulse;
            if (!frostCapKills || !singleFrostCap || !doubleFrostCap || !removedFrostStack || !returnedToSingleCap)
                Debug.LogError("RANGE_SMOKE_FROST_RELIC_STACK_CAP");
            else Debug.Log("RANGE_SMOKE_FROST_RELIC_STACK_CAP_CHECKED");
            items.Remove(frostRelic);
            target.transform.position = frostTargetPosition;
            target.ResearchIceSlowUntil = 0d;
            target.CurrentHealth = targetHealth;
            int fireworks = RangeResearchItems.CatalogIndex("Firework");
            int fireworkDamage = Mathf.RoundToInt(3f * attack);
            bool fireworkPassed = true;
            for (int stacks = 1; stacks <= 2; stacks++)
            {
                items.Grant(fireworks);
                items.OnRainInteraction(items.FireworkStacks);
                int expectedHealth = targetHealth - (4 + 4 * stacks) * fireworkDamage;
                float fireworkDeadline = Time.time + 3f;
                yield return new WaitUntil(() => target.CurrentHealth <= expectedHealth || Time.time >= fireworkDeadline);
                yield return new WaitForSeconds(.3f);
                fireworkPassed &= target.CurrentHealth == expectedHealth;
                target.CurrentHealth = targetHealth;
            }
            if (fireworkPassed) Debug.Log("RANGE_SMOKE_FIREWORKS_CHECKED");
            else Debug.LogError("RANGE_SMOKE_FIREWORKS");
            items.Remove(fireworks);
            items.Remove(fireworks);
            var fireworkCeiling = new GameObject("Rain smoke firework ceiling").AddComponent<BoxCollider>();
            fireworkCeiling.transform.position = owner.CombatAimPoint + Vector3.up * 1.5f;
            fireworkCeiling.size = new Vector3(10f, .5f, 10f);
            int fireworkGlasses = RangeResearchItems.CatalogIndex("CritGlasses");
            int fireworkScythe = RangeResearchItems.CatalogIndex("HealOnCrit");
            for (int i = 0; i < 10; i++) items.Grant(fireworkGlasses);
            items.Grant(fireworkScythe);
            owner.CurrentHealth = 1;
            Physics.SyncTransforms();
            items.Grant(fireworks);
            items.OnRainInteraction(items.FireworkStacks);
            int expectedCeilingHealth = targetHealth - 8 * Mathf.RoundToInt(6f * attack);
            float ceilingDeadline = Time.time + 3f;
            yield return new WaitUntil(() => target.CurrentHealth <= expectedCeilingHealth || Time.time >= ceilingDeadline);
            yield return new WaitForSeconds(.3f);
            if (target.CurrentHealth != expectedCeilingHealth || owner.CurrentHealth != 13)
                Debug.LogError($"RANGE_SMOKE_FIREWORK_TERRAIN_IMPACT loss={targetHealth - target.CurrentHealth} expected={targetHealth - expectedCeilingHealth} ownerHp={owner.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_FIREWORK_TERRAIN_IMPACT_CHECKED");
            for (int i = 0; i < 10; i++) items.Remove(fireworkGlasses);
            items.Remove(fireworkScythe);
            owner.CurrentHealth = ownerHealth;
            items.Remove(fireworks);
            target.CurrentHealth = targetHealth;
            fireworkCeiling.transform.position = target.transform.position + Vector3.up * 2f;
            fireworkCeiling.gameObject.layer = (int)GameLayers.hitbox;
            fireworkCeiling.isTrigger = true;
            fireworkCeiling.gameObject.AddComponent<HitBox>()._health = target;
            Physics.SyncTransforms();
            int molten = RangeResearchItems.CatalogIndex("FireballsOnHit");
            items.Grant(molten);
            items.Grant(molten);
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            yield return new WaitForSeconds(.5f);
            bool zeroProcIgnored = target.CurrentHealth == targetHealth - 20;
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 10f);
            int moltenExpectedHealth = targetHealth - 40 - 3 * 120;
            float moltenDeadline = Time.time + .8f;
            yield return new WaitUntil(() => target.CurrentHealth <= moltenExpectedHealth || Time.time >= moltenDeadline);
            bool threeMoltenBalls = target.CurrentHealth == moltenExpectedHealth;
            yield return new WaitForSeconds(3.2f);
            int moltenBurnEnd = moltenExpectedHealth - 3 * 60;
            bool burnStillActive = target.CurrentHealth < moltenExpectedHealth && target.CurrentHealth > moltenBurnEnd;
            moltenDeadline = Time.time + 12f;
            yield return new WaitUntil(() => target.CurrentHealth <= moltenBurnEnd || Time.time >= moltenDeadline);
            if (!zeroProcIgnored || !threeMoltenBalls || !burnStillActive || target.CurrentHealth != moltenBurnEnd)
                Debug.LogError($"RANGE_SMOKE_MOLTEN_PERFORATOR zeroProc={zeroProcIgnored} threeBalls={threeMoltenBalls} loss={targetHealth - target.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_MOLTEN_PERFORATOR_CHECKED");
            Destroy(fireworkCeiling.gameObject);
            items.Remove(molten);
            items.Remove(molten);
            target.CurrentHealth = targetHealth;
            int plant = RangeResearchItems.CatalogIndex("Plant");
            Vector3 plantOwnerPosition = owner.transform.position;
            Vector3 plantTargetPosition = target.transform.position;
            items.Grant(plant);
            bool firstPlantKill = HitAndKillSmokeTarget(owner, items);
            items.Grant(plant);
            bool secondPlantKill = HitAndKillSmokeTarget(owner, items);
            bool removedPlants = items.Remove(plant) && items.Remove(plant);
            owner.transform.position = plantOwnerPosition + Vector3.up * 3f;
            target.transform.position = owner.transform.position + Vector3.forward * 2f;
            owner.CurrentHealth = 1;
            target.CurrentHealth = targetHealth - 100;
            yield return new WaitForSeconds(4.8f);
            bool plantWaited = owner.CurrentHealth == 1;
            float plantDeadline = Time.time + 2f;
            yield return new WaitUntil(() => owner.CurrentHealth > 1 || Time.time >= plantDeadline);
            int plantHeal = Mathf.Max(1, Mathf.RoundToInt(.05f * owner.MaxHealth));
            if (!firstPlantKill || !secondPlantKill || !removedPlants || !plantWaited ||
                owner.CurrentHealth != 1 + plantHeal || target.CurrentHealth != targetHealth - 100)
                Debug.LogError("RANGE_SMOKE_DESK_PLANT");
            else Debug.Log("RANGE_SMOKE_DESK_PLANT_CHECKED");
            owner.transform.position = plantOwnerPosition;
            target.transform.position = plantTargetPosition;
            owner.CurrentHealth = ownerHealth;
            target.CurrentHealth = targetHealth;
            VerifyEffigy(owner, target, items);
            yield return VerifyIgnitionTank(owner, target, items);
            int missile = RangeResearchItems.CatalogIndex("Missile");
            items.Grant(missile);
            items.Grant(missile);
            items.Grant(RangeResearchItems.CatalogIndex("Seed"));
            int preMissileHealth = owner.CurrentHealth;
            owner.CurrentHealth = 1;
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 0f);
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 20f);
            float missileDeadline = Time.time + 4.1f;
            yield return new WaitUntil(() => target.CurrentHealth <= targetHealth - 160 || Time.time >= missileDeadline);
            if (target.CurrentHealth != targetHealth - 160 || owner.CurrentHealth != 22)
                Debug.LogError($"RANGE_SMOKE_ATG_MISSILE damage={targetHealth - target.CurrentHealth} ownerHealth={owner.CurrentHealth}");
            else Debug.Log("RANGE_SMOKE_ATG_MISSILE_CHECKED");
            owner.CurrentHealth = preMissileHealth;
            items.Grant(RangeResearchItems.CatalogIndex("MissileVoid"));
            target.CurrentHealth = targetHealth;
            if (owner.ResearchPlasmaShrimps != 3 || owner.ResearchShield != Mathf.RoundToInt(owner.MaxHealth * .1f) ||
                items.Remove(missile))
                Debug.LogError("RANGE_SMOKE_PLASMA_SHRIMP_CORRUPTION");
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            yield return new WaitForSeconds(.4f);
            int beforeShrimpShieldHit = owner.CurrentHealth;
            owner.Server_ChangeHealthStateRaw(owner.ResearchShield, CharacterPart.body, AttackType.hitscan, target, 0, 0f);
            bool shrimpShieldAbsorbed = owner.ResearchShield == 0 && owner.CurrentHealth == beforeShrimpShieldHit;
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0);
            yield return new WaitForSeconds(.4f);
            if (!shrimpShieldAbsorbed || target.CurrentHealth != targetHealth - 68)
                Debug.LogError("RANGE_SMOKE_PLASMA_SHRIMP_SHIELD_GATE");
            else Debug.Log("RANGE_SMOKE_PLASMA_SHRIMP_CHECKED");
            items.Clear();
            target.CurrentHealth = targetHealth;
            int icbm = RangeResearchItems.CatalogIndex("MoreMissile");
            items.Grant(missile);
            items.Grant(icbm);
            items.Grant(icbm);
            target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, owner, 0, 20f);
            yield return new WaitForSeconds(.4f);
            if (target.CurrentHealth != targetHealth - 290 || !items.Remove(icbm))
                Debug.LogError("RANGE_SMOKE_POCKET_ICBM_STACKS");
            else Debug.Log("RANGE_SMOKE_POCKET_ICBM_CHECKED");
            items.Clear();
            target.CurrentHealth = targetHealth;
            Vector3 launcherTargetPosition = target.transform.position;
            target.transform.position = owner.transform.position + Vector3.forward * 45f;
            if (!items.Equip(RangeResearchItems.Equipment.MissileLauncher) || items.TryUseEquipment() ||
                items.EquipmentCooldownRemaining != 0f)
                Debug.LogError("RANGE_SMOKE_MISSILE_LAUNCHER_NO_TARGET");
            target.transform.position = launcherTargetPosition;
            items.Grant(icbm);
            items.Grant(icbm);
            if (!items.TryUseEquipment() || items.EquipmentCooldownRemaining < 44.9f || items.TryUseEquipment())
                Debug.LogError("RANGE_SMOKE_MISSILE_LAUNCHER_COOLDOWN");
            yield return new WaitForSeconds(1.75f);
            int missileDamage = (int)System.Math.Round(4.5d * attack);
            if (target.CurrentHealth != targetHealth - 36 * missileDamage)
                Debug.LogError("RANGE_SMOKE_MISSILE_LAUNCHER_SALVO");
            else Debug.Log("RANGE_SMOKE_MISSILE_LAUNCHER_CHECKED");

            owner.IsRunning = previousRunning;
            owner.transform.position = ownerPosition;
            target.transform.position = targetPosition;
            target.CurrentHealth = targetHealth;
            motor.enabled = motorEnabled;
            items.Clear();
            Physics.SyncTransforms();
        }

        static IEnumerator VerifyCapacitorPlaytest(RangeResearchLab lab, CharacterInstance owner, RangeResearchItems items)
        {
            items.Clear();
            var target = Jotunnslayer.JotunnMonsterGm.TrySpawn(owner, "skeleton_1h_sur");
            if (!target)
            {
                Debug.LogError("RAIN_ITEM_CAPACITOR_NO_DUMMY");
                yield break;
            }
            target.MaxHealth = target.CurrentHealth = 1000;
            yield return null;
            var player = GameManager.myPlayerInstance;
            if (player) yield return VerifyCapacitor(player, owner, target, items, lab);
            else Debug.LogError("RAIN_ITEM_CAPACITOR_NO_PLAYER");
            bool cue = false, audio = false;
            float deadline = Time.realtimeSinceStartup + 1f;
            while (Time.realtimeSinceStartup < deadline && (!cue || !audio))
            {
                cue |= GameObject.Find("Rain lightning strike") != null;
                audio |= FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Any(source =>
                    source.isPlaying && source.clip && source.clip.name == "RoyalCapacitorOriginal");
                if (!cue || !audio) yield return null;
            }
            if (!cue || !audio) Debug.LogError($"RAIN_ITEM_CAPACITOR_PRESENTATION_FAILED cue={cue} audio={audio}");
            else Debug.Log("RAIN_ITEM_CAPACITOR_PRESENTATION_CHECKED cue=True audio=True");
            Jotunnslayer.JotunnMonsterGm.Clear(owner);
        }

        static IEnumerator VerifyCapacitor(PlayerInstance player, CharacterInstance owner, CharacterInstance target,
            RangeResearchItems items, RangeResearchLab lab = null)
        {
            Vector3 ownerPosition = owner.transform.position;
            Vector3 targetPosition = target.transform.position;
            Vector2 previousLook = owner.lookInput;
            int previousHealth = target.CurrentHealth;
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var targetMotor = target.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled, targetMotorEnabled = targetMotor.enabled;
            ownerMotor.enabled = targetMotor.enabled = false;
            owner.transform.position = ownerPosition + Vector3.up * 100f;
            target.transform.position = owner.transform.position + Vector3.forward * 5f;
            var entry = new BrowserEntry { id = "Lightning", name = "皇家电容器" };
            if (lab) lab.ExecuteCatalog(entry);
            else items.Equip(RangeResearchItems.Equipment.Capacitor);
            if (items.Equipped != RangeResearchItems.Equipment.Capacitor)
                Debug.LogError("RAIN_ITEM_CAPACITOR_EQUIP_FAILED");
            owner.lookInput = new Vector2(0f, 180f);
            if (player.ServerTryUseRangeEquipment() || items.EquipmentCooldownRemaining != 0f)
                Debug.LogError("RANGE_SMOKE_CAPACITOR_NO_TARGET");
            owner.lookInput = Vector2.zero;

            int environmentLayer = 0;
            int layerMask = GameManager.environmentLayer;
            while (environmentLayer < 32 && (layerMask & (1 << environmentLayer)) == 0) environmentLayer++;
            if (environmentLayer < 32)
            {
                var obstacle = new GameObject("Capacitor smoke obstacle");
                obstacle.layer = environmentLayer;
                obstacle.transform.position = Vector3.Lerp(owner.CombatAimPoint, target.CombatAimPoint, .5f);
                var collider = obstacle.AddComponent<BoxCollider>();
                Physics.SyncTransforms();
                if (player.ServerTryUseRangeEquipment() || items.EquipmentCooldownRemaining != 0f)
                    Debug.LogError("RANGE_SMOKE_CAPACITOR_OCCLUDED");
                collider.enabled = false;
                Destroy(obstacle);
                Physics.SyncTransforms();
            }
            else Debug.LogError("RANGE_SMOKE_CAPACITOR_ENVIRONMENT_LAYER");

            float attack = owner.RainAttackPower > 0f ? owner.RainAttackPower : CharacterInstance.RainBaseAttackPower;
            CharacterInstance near = null, far = null;
            if (lab)
            {
                CharacterInstance SpawnProbe(float offset)
                {
                    var probe = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        target.transform.position + Vector3.right * offset, Quaternion.identity).GetComponent<CharacterInstance>();
                    foreach (var slot in probe.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    probe.Team = target.Team;
                    probe.MaxHealth = probe.CurrentHealth = 1000;
                    NetworkServer.Spawn(probe.gameObject);
                    probe.GetComponent<CharacterMotor>().enabled = false;
                    return probe;
                }
                near = SpawnProbe(2f);
                far = SpawnProbe(4f);
                Physics.SyncTransforms();
            }
            bool activated = player.ServerTryUseRangeEquipment();
            bool cooldown = items.EquipmentCooldownRemaining >= 19.9f && !player.ServerTryUseRangeEquipment();
            yield return new WaitForSeconds(.3f);
            bool delayed = target.CurrentHealth == previousHealth && (!near || near.CurrentHealth == 1000);
            float deadline = Time.realtimeSinceStartup + 1.5f;
            while (target.CurrentHealth == previousHealth && Time.realtimeSinceStartup < deadline) yield return null;
            int expected = Mathf.RoundToInt(30f * attack);
            if (!activated || !cooldown || !delayed || target.CurrentHealth != previousHealth - expected ||
                (near && near.CurrentHealth != 1000 - expected) || (far && far.CurrentHealth != 1000))
                Debug.LogError("RANGE_SMOKE_CAPACITOR_HIT_OR_COOLDOWN");
            else Debug.Log("RANGE_SMOKE_CAPACITOR_CHECKED");

            if (lab)
            {
                int damage = previousHealth - target.CurrentHealth;
                lab.RemoveCatalog(entry);
                bool removed = items.Equipped == RangeResearchItems.Equipment.None && !player.ServerTryUseRangeEquipment();
                if (!removed) Debug.LogError("RAIN_ITEM_CAPACITOR_REMOVE_FAILED");
                else Debug.Log($"RAIN_ITEM_CAPACITOR_CHECKED damage={damage} near={1000 - near.CurrentHealth} far={1000 - far.CurrentHealth} delayed={delayed} removed=True");
            }

            if (near) NetworkServer.Destroy(near.gameObject);
            if (far) NetworkServer.Destroy(far.gameObject);
            target.CurrentHealth = previousHealth;
            target.transform.position = targetPosition;
            owner.transform.position = ownerPosition;
            owner.lookInput = previousLook;
            ownerMotor.enabled = ownerMotorEnabled;
            targetMotor.enabled = targetMotorEnabled;
            items.Clear();
            Physics.SyncTransforms();
        }

        IEnumerator VerifyFruitFromRemotePlayer(PlayerInstance guest)
        {
            var character = guest.MyCharacter;
            var mode = (Gamemodes.RangeGamemode)GameManager.Gamemode;
            const int highChestIndex = 32;
            if (!mode.TryGetRainMapNode(highChestIndex, false, out var highChestPosition))
                Debug.LogError("RAIN_COOP_HIGH_CHEST_MISSING");
            else
            {
                Vector3 position = character.transform.position;
                int gold = guest.RainGold;
                int price = Gamemodes.RangeGamemode.RainChestPrice;
                guest.RainGold = 2 * price;
                character.transform.position = highChestPosition;
                mode.BuyRainChest(guest, highChestIndex);
                mode.BuyRainChest(guest, highChestIndex);
                if (guest.RainGold != price || character.ResearchRedWhipStacks != 1 ||
                    mode.TryGetRainMapNode(highChestIndex, false, out _) || !mode.TryGetRainMapNode(0, false, out _))
                    Debug.LogError("RAIN_COOP_HIGH_CHEST_MASK");
                else Debug.Log("RAIN_COOP_HIGH_CHEST_MASK_CHECKED");

                const int chestBeyondMask = 64;
                if (!mode.TryGetRainMapNode(chestBeyondMask, false, out var beyondMaskPosition))
                    Debug.LogError("RAIN_COOP_CHEST_BEYOND_MASK_MISSING");
                else
                {
                    guest.RainGold = 2 * price;
                    character.transform.position = beyondMaskPosition;
                    mode.BuyRainChest(guest, chestBeyondMask);
                    mode.BuyRainChest(guest, chestBeyondMask);
                    int voidLife = RangeResearchItems.CatalogIndex("ExtraLifeVoid");
                    if (guest.RainGold != price || mode.TryGetRainMapNode(chestBeyondMask, false, out _) ||
                        !mode.TryGetRainMapNode(0, false, out _) ||
                        !character.TryGetComponent<RangeResearchItems>(out var voidItems) ||
                        !voidItems.Remove(voidLife) || voidItems.Remove(voidLife))
                        Debug.LogError("RAIN_COOP_CHEST_BEYOND_MASK_TRANSACTION");
                    else Debug.Log("RAIN_COOP_CHEST_BEYOND_MASK_CHECKED");
                }
                character.transform.position = position;
                guest.RainGold = gold;
                if (character.TryGetComponent<RangeResearchItems>(out var whipItems) &&
                    whipItems.Remove(RangeResearchItems.CatalogIndex("SprintOutOfCombat")))
                    guest.RecordRainChestItems(whipItems);
            }
            int itemIndex = Gamemodes.RangeGamemode.FirstExtraRainItemChest;
            var itemChest = mode.transform.Find("Rain supply chest " + itemIndex);
            if (!itemChest)
                Debug.LogError("RAIN_COOP_EXTRA_ITEM_CHEST_MISSING");
            else
            {
                Vector3 position = character.transform.position;
                int gold = guest.RainGold;
                int maximum = character.MaxHealth;
                guest.RainGold = Gamemodes.RangeGamemode.RainChestPrice;
                character.transform.position = itemChest.position;
                mode.BuyRainChest(guest, itemIndex);
                bool granted = guest.RainGold == 0 && character.MaxHealth == maximum + 40;
                character.transform.position = position;
                guest.RainGold = gold;
                if (character.TryGetComponent<RangeResearchItems>(out var chestItems) &&
                    chestItems.Remove(RangeResearchItems.CatalogIndex("Knurl")))
                    guest.RecordRainChestItems(chestItems);
                if (!granted || character.MaxHealth != maximum)
                    Debug.LogError("RAIN_COOP_EXTRA_ITEM_CHEST_TRANSACTION");
                else Debug.Log("RAIN_COOP_EXTRA_ITEM_CHEST_CHECKED");
            }
            int first = Gamemodes.RangeGamemode.FirstRainEquipmentChest;
            bool otherChestsPassed = VerifyEquipmentChest(mode, guest, first + 1, RangeResearchItems.Equipment.Elephant);
            otherChestsPassed &= VerifyEquipmentChest(mode, guest, first + 2, RangeResearchItems.Equipment.OcularHud);
            otherChestsPassed &= VerifyEquipmentChest(mode, guest, first + 3, RangeResearchItems.Equipment.Leech);
            if (!VerifyEquipmentChest(mode, guest, Gamemodes.RangeGamemode.RainScannerChest,
                    RangeResearchItems.Equipment.Scanner) || !guest.ServerTryUseRangeEquipment() ||
                guest.ServerTryUseRangeEquipment() || character.RainScannerUntil - NetworkTime.time < 9.9d)
                Debug.LogError("RAIN_COOP_SCANNER_ACTIVATION");
            else
            {
                Debug.Log("RAIN_COOP_SCANNER_ACTIVATION_CHECKED");
                double scanStartedAt = mode.RainScannerTime;
                Vector3 origin = character.CombatAimPoint;
                Vector3 edge = origin + Vector3.right * 499f;
                Vector3 outside = origin + Vector3.right * 501f;
                if (!mode.IsRainScannerPointRevealed(origin, scanStartedAt + .1d) ||
                    mode.IsRainScannerPointRevealed(edge, scanStartedAt + 3.9d) ||
                    !mode.IsRainScannerPointRevealed(edge, scanStartedAt + 4.1d) ||
                    mode.IsRainScannerPointRevealed(outside, scanStartedAt + 4.1d) ||
                    mode.IsRainScannerPointRevealed(origin, scanStartedAt + 10.1d) ||
                    !mode.IsRainScannerPointRevealed(edge, scanStartedAt + 10.1d) ||
                    mode.IsRainScannerPointRevealed(edge, scanStartedAt + 14.1d))
                    Debug.LogError("RAIN_COOP_SCANNER_PULSE_TIMING");
                else Debug.Log("RAIN_COOP_SCANNER_PULSE_TIMING_CHECKED");
            }
            VerifyExecutiveCard(mode, guest);
            VerifyBrittleCrown(guest);
            yield return VerifyFireworkChest(mode, guest);
            VerifyPearlLevelUp(guest);
            yield return VerifyGhorGoldPack(mode, guest);
            if (otherChestsPassed) Debug.Log("RAIN_COOP_EQUIPMENT_CHESTS_CHECKED");

            character.RangeInvulnerable = true;
            int originalHealth = character.CurrentHealth;
            character.CurrentHealth = 1;
            character.ServerSetMaximumHealth(character.MaxHealth);
            if (!VerifyEquipmentChest(mode, guest, first, RangeResearchItems.Equipment.Fruit) ||
                !character.TryGetComponent<RangeResearchItems>(out var items))
            {
                character.CurrentHealth = originalHealth;
                character.ServerSetMaximumHealth(character.MaxHealth);
                character.RangeInvulnerable = false;
                Debug.LogError("RAIN_COOP_REMOTE_FRUIT_EQUIP");
                yield break;
            }
            Debug.Log("RAIN_COOP_REMOTE_FRUIT_CHEST_CHECKED");
            float deadline = Time.realtimeSinceStartup + 5f;
            while (items.FruitCooldownRemaining <= 0f && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (items.FruitCooldownRemaining < 43f ||
                character.CurrentHealth < 1 + Mathf.CeilToInt(character.MaxHealth * .5f))
                Debug.LogError("RAIN_COOP_REMOTE_FRUIT_SERVER");
            else Debug.Log("RAIN_COOP_REMOTE_FRUIT_SERVER_CHECKED");
            yield return new WaitForSecondsRealtime(.5f);
            items.Remove(RangeResearchItems.Equipment.Fruit);
            guest.RecordRainChestItems(items);
            character.CurrentHealth = character.MaxHealth;
            character.ServerSetMaximumHealth(character.MaxHealth);
            character.RangeInvulnerable = false;
        }

        static void VerifyPearlLevelUp(PlayerInstance player)
        {
            var character = player.MyCharacter;
            if (player.RainLevel != 1 || !character.TryGetComponent<RangeResearchItems>(out var items))
            {
                Debug.LogError("RAIN_COOP_PEARL_LEVEL_SETUP");
                return;
            }

            int maximum = character.MaxHealth;
            int health = character.CurrentHealth;
            int experience = player.RainExperience;
            float attack = character.RainAttackPower;
            float regeneration = character.RainHealthRegen;
            int pearl = RangeResearchItems.CatalogIndex("Pearl");
            items.Grant(pearl);
            int levelCost = (int)RunLedger.Experience(2) - experience;
            player.GrantRainExperience(levelCost);
            int upgradedBase = maximum + Mathf.RoundToInt(maximum * .30f);
            if (player.RainLevel != 2 || character.MaxHealth != upgradedBase + Mathf.RoundToInt(upgradedBase * .10f))
                Debug.LogError("RAIN_COOP_PEARL_LEVEL_HEALTH");
            else Debug.Log("RAIN_COOP_PEARL_LEVEL_CHECKED");

            items.Remove(pearl);
            player.RainExperience = experience;
            character.RainAttackPower = attack;
            character.RainHealthRegen = regeneration;
            character.CurrentHealth = health;
            character.ServerSetMaximumHealth(maximum);
            player.RecordRainChestItems(items);
        }

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        IEnumerator VerifyMonsterLogExpiryHost(Gamemodes.RangeGamemode mode, PlayerInstance host,
            PlayerInstance guest, CharacterInstance owner)
        {
            string logId = Gamemodes.RangeGamemode.HermitCrabLogId;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPSSHOOT_PROFILE_TEST_DIR")) ||
                !SteamRoomSession.Instance || SteamRoomSession.Instance.HasRainMonsterLog(logId) ||
                !mode.TryDropMonsterLog(owner.transform.position + Vector3.forward * 8f,
                    RangeResearchEnemy.HermitCrabKind, 0f))
            {
                Debug.LogError("RAIN_COOP_MONSTER_LOG_EXPIRY_SETUP_FAILED");
                yield break;
            }
            var pickup = GameObject.Find("Rain monster log " + logId);
            var physicsObject = GameObject.Find("Rain monster log body");
            if (!pickup || !physicsObject || !physicsObject.TryGetComponent<Rigidbody>(out var body))
            {
                Debug.LogError("RAIN_COOP_MONSTER_LOG_EXPIRY_BODY_MISSING");
                yield break;
            }
            float started = Time.time;
            yield return new WaitForSeconds(2.5f);
            var pickupLight = pickup ? pickup.GetComponentInChildren<Light>() : null;
            if (!pickupLight || Mathf.Abs(pickupLight.intensity - 5.173f) > .15f ||
                Mathf.Abs(pickupLight.range - 3f) > .01f ||
                Vector4.Distance(pickupLight.color, new Color(.55707115f, .22004759f, .80882353f)) > .01f)
                Debug.LogError("RAIN_COOP_MONSTER_LOG_LIGHT_CURVE_FAILED");
            else Debug.Log("RAIN_COOP_MONSTER_LOG_LIGHT_CURVE_CHECKED");
            yield return new WaitForSeconds(2.5f);
            bool foundGround = false;
            RaycastHit ground = default;
            if (body)
            {
                var contacts = Physics.RaycastAll(body.position + Vector3.up * 2f, Vector3.down,
                    10f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore);
                foreach (var contact in contacts)
                {
                    if (contact.rigidbody == body || foundGround && contact.distance >= ground.distance) continue;
                    foundGround = true;
                    ground = contact;
                }
            }
            if (!body || !foundGround ||
                body.position.y < ground.point.y - .1f || body.position.y > ground.point.y + 2f ||
                body.linearVelocity.sqrMagnitude > .25f)
                Debug.LogError($"RAIN_COOP_MONSTER_LOG_GROUND_CONTACT_FAILED body={body} " +
                    $"ground={foundGround} position={(body ? body.position : Vector3.zero)} " +
                    $"surface={ground.point} velocity={(body ? body.linearVelocity : Vector3.zero)}");
            else Debug.Log("RAIN_COOP_MONSTER_LOG_GROUND_CONTACT_CHECKED");

            float deadline = Time.realtimeSinceStartup + 320f;
            while (pickup && Time.realtimeSinceStartup < deadline) yield return null;
            float elapsed = Time.time - started;
            yield return null;
            if (pickup || physicsObject || elapsed < 299.8f || elapsed > 302f ||
                host.ServerHasRainMonsterLog(logId) || guest.ServerHasRainMonsterLog(logId) ||
                SteamRoomSession.Instance.HasRainMonsterLog(logId))
                Debug.LogError($"RAIN_COOP_MONSTER_LOG_EXPIRY_FAILED seconds={elapsed:F2}");
            else Debug.Log($"RAIN_COOP_MONSTER_LOG_EXPIRY_CHECKED seconds={elapsed:F2}");
            yield return new WaitForSeconds(2f);
        }

        IEnumerator VerifyMonsterLogExpiryClient()
        {
            string logId = Gamemodes.RangeGamemode.HermitCrabLogId;
            float deadline = Time.realtimeSinceStartup + 30f;
            GameObject pickup = null;
            while (!(pickup = GameObject.Find("Rain monster log " + logId)) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (!pickup || !SteamRoomSession.Instance)
            {
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_EXPIRY_MODEL_MISSING");
                yield break;
            }
            float started = Time.time;
            var pickupLight = pickup.GetComponentInChildren<Light>();
            if (!pickupLight || Mathf.Abs(pickupLight.range - 3f) > .01f ||
                Vector4.Distance(pickupLight.color, new Color(.55707115f, .22004759f, .80882353f)) > .01f)
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_LIGHT_MISSING");
            else Debug.Log("RAIN_CLIENT_MONSTER_LOG_LIGHT_CHECKED");
            deadline = Time.realtimeSinceStartup + 320f;
            while (pickup && NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
                yield return null;
            float elapsed = Time.time - started;
            if (pickup || !NetworkClient.isConnected || elapsed < 298f || elapsed > 302f ||
                SteamRoomSession.Instance.HasRainMonsterLog(logId))
                Debug.LogError($"RAIN_CLIENT_MONSTER_LOG_EXPIRY_FAILED seconds={elapsed:F2}");
            else Debug.Log($"RAIN_CLIENT_MONSTER_LOG_EXPIRY_CHECKED seconds={elapsed:F2}");
        }

        IEnumerator VerifyMonsterLogHost(Gamemodes.RangeGamemode mode, PlayerInstance host,
            PlayerInstance guest, CharacterInstance owner)
        {
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-death"))
            {
                var items = owner.GetComponent<RangeResearchItems>();
                if (!items) items = owner.gameObject.AddComponent<RangeResearchItems>();
                items.Clear();
                try
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var entry = RainMonsterLogs.Entries[i];
                        if (i == 1) items.Grant(RangeResearchItems.CatalogIndex("Clover"));
                        yield return VerifyMonsterLogHost(mode, host, guest, owner,
                            entry.Archetype, entry.UnlockableId, entry.DropChance, true, i == 1);
                    }
                }
                finally { items.Clear(); }
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-preloaded"))
            {
                yield return VerifyPreloadedMonsterLogsHost(mode, host, guest, owner);
                yield break;
            }
            yield return VerifyMonsterLogExpiryHost(mode, host, guest, owner);
            foreach (var entry in RainMonsterLogs.Entries)
                if (entry.Archetype == 0 &&
                    Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-allies"))
                    yield return VerifyMonsterLogAllyHost(mode, host, guest, owner, entry);
                else yield return VerifyMonsterLogHost(mode, host, guest, owner,
                        entry.Archetype, entry.UnlockableId, entry.DropChance);
        }

        IEnumerator VerifyMonsterLogAllyHost(Gamemodes.RangeGamemode mode, PlayerInstance host,
            PlayerInstance guest, CharacterInstance owner, RainMonsterLogs.Entry entry)
        {
            var ally = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position + Vector3.right * 30f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in ally.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            ally.Team = owner.Team;
            ally.RangeInvulnerable = true;
            NetworkServer.Spawn(ally.gameObject);
            ally.SetAsBOT(true);
            var ai = ally.GetComponent<BotAI>();
            if (ai) { ai.enabled = false; ai.StopAllCoroutines(); }
            ally.GetComponent<CharacterMotor>().enabled = false;
            try
            {
                if (ally.connectionToClient != null)
                {
                    Debug.LogError("RAIN_COOP_MONSTER_LOG_ALLY_HAS_PLAYER_CONNECTION");
                    yield break;
                }
                yield return VerifyMonsterLogHost(mode, host, guest, ally,
                    entry.Archetype, entry.UnlockableId, entry.DropChance);
                if (host.ServerHasRainMonsterLog(entry.UnlockableId) && guest.ServerHasRainMonsterLog(entry.UnlockableId))
                    Debug.Log("RAIN_COOP_MONSTER_LOG_ALLY_CHECKED " + entry.UnlockableId);
                else Debug.LogError("RAIN_COOP_MONSTER_LOG_ALLY_GRANT_FAILED");
            }
            finally
            {
                if (ally) NetworkServer.Destroy(ally.gameObject);
            }
        }

        IEnumerator VerifyMonsterLogHost(Gamemodes.RangeGamemode mode, PlayerInstance host,
            PlayerInstance guest, CharacterInstance owner, int kind, string logId, float dropChance,
            bool fromDeath = false, bool luckyReroll = false)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPSSHOOT_PROFILE_TEST_DIR")) ||
                !SteamRoomSession.Instance || SteamRoomSession.Instance.HasRainMonsterLog(logId))
            {
                Debug.LogError("RAIN_COOP_MONSTER_LOG_FRESH_TEST_PROFILE_REQUIRED");
                yield break;
            }

            Vector3 position = owner.transform.position;
            Vector3 drop = position + Vector3.forward * 8f;
            if (fromDeath)
            {
                RangeResearchEnemy actor = null;
                float spawnDeadline = Time.realtimeSinceStartup + 20f;
                while (!actor && Time.realtimeSinceStartup < spawnDeadline)
                {
                    foreach (var candidate in FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None))
                        if (candidate.Archetype == kind && candidate.TryGetComponent<CharacterInstance>(out var body) &&
                            body.CurrentHealth > 0 && body.RainEnemyKind == kind + 1)
                        { actor = candidate; break; }
                    if (!actor) yield return null;
                }
                if (!actor)
                {
                    Debug.LogError("RAIN_COOP_MONSTER_LOG_DEATH_SETUP_FAILED " + logId);
                    yield break;
                }
                actor.SetCombat(false);
                var victim = actor.GetComponent<CharacterInstance>();
                victim.GetComponent<CharacterMotor>().enabled = false;
                victim.transform.position = drop;
                Physics.SyncTransforms();
                int kills = mode.RainKills;
                var randomState = UnityEngine.Random.state;
                bool seedFound = false;
                try
                {
                    for (int seed = 0; seed < 100000; seed++)
                    {
                        UnityEngine.Random.InitState(seed);
                        float first = UnityEngine.Random.value;
                        float second = UnityEngine.Random.value;
                        if (luckyReroll ? first >= dropChance && second < dropChance : first < dropChance)
                        {
                            UnityEngine.Random.InitState(seed);
                            seedFound = true;
                            // Periodic damage avoids an unrelated weapon critical roll before DeathRewards.
                            victim.ServerApplyRangeDot(100000, AttackType.explosion, owner);
                            victim.ServerApplyRangeDot(100000, AttackType.explosion, owner);
                            break;
                        }
                    }
                }
                finally { UnityEngine.Random.state = randomState; }
                if (!seedFound || victim.CurrentHealth > 0 || mode.RainKills != kills + 1 ||
                    !GameObject.Find("Rain monster log " + logId))
                {
                    Debug.LogError($"RAIN_COOP_MONSTER_LOG_DEATH_FAILED id={logId} " +
                        $"seed={seedFound} hp={victim.CurrentHealth} kills={mode.RainKills - kills}");
                    yield break;
                }
                Debug.Log((luckyReroll ? "RAIN_COOP_MONSTER_LOG_LUCK_DEATH_CHECKED " :
                    "RAIN_COOP_MONSTER_LOG_DEATH_CHECKED ") + logId);
            }
            else if (mode.TryDropMonsterLog(drop, kind, dropChance) || !mode.TryDropMonsterLog(drop, kind, 0f))
            {
                Debug.LogError("RAIN_COOP_MONSTER_LOG_ROLL_FAILED");
                yield break;
            }
            var pickup = GameObject.Find("Rain monster log " + logId);
            if (!pickup)
            {
                Debug.LogError("RAIN_COOP_MONSTER_LOG_MODEL_MISSING");
                yield break;
            }

            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            try
            {
                Vector3 initial = pickup.transform.position;
                bool rose = false;
                float launchDeadline = Time.realtimeSinceStartup + .8f;
                while (pickup && Time.realtimeSinceStartup < launchDeadline)
                {
                    rose |= pickup.transform.position.y > initial.y + .25f;
                    yield return null;
                }
                yield return new WaitForSeconds(1f);
                if (!rose || !pickup)
                {
                    Debug.LogError("RAIN_COOP_MONSTER_LOG_LAUNCH_FAILED " + logId);
                    yield break;
                }
                motor.enabled = false;
                owner.transform.position = pickup.transform.position + Vector3.right * 2.6f;
                Physics.SyncTransforms();
                float deadline = Time.realtimeSinceStartup + 3f;
                while (pickup && Time.realtimeSinceStartup < deadline) yield return null;
                if (pickup || !host.ServerHasRainMonsterLog(logId) || !guest.ServerHasRainMonsterLog(logId) ||
                    !SteamRoomSession.Instance.HasRainMonsterLog(logId) || mode.TryDropMonsterLog(drop, kind, 0f))
                {
                    var physicsObject = GameObject.Find("Rain monster log body");
                    var body = physicsObject ? physicsObject.GetComponent<Rigidbody>() : null;
                    Debug.LogError($"RAIN_COOP_MONSTER_LOG_PICKUP_OR_SUPPRESSION_FAILED id={logId} " +
                        $"pickup={(pickup ? pickup.transform.position : Vector3.zero)} " +
                        $"velocity={(body ? body.linearVelocity : Vector3.zero)} " +
                        $"owner={owner.transform.position} aim={owner.CombatAimPoint} hp={owner.CurrentHealth} " +
                        $"guest={(guest.MyCharacter ? guest.MyCharacter.transform.position : Vector3.zero)} " +
                        $"hostGranted={host.ServerHasRainMonsterLog(logId)} " +
                        $"guestGranted={guest.ServerHasRainMonsterLog(logId)} " +
                        $"localGranted={SteamRoomSession.Instance.HasRainMonsterLog(logId)}");
                }
                else Debug.Log("RAIN_COOP_MONSTER_LOG_CHECKED " + logId);
                yield return new WaitForSeconds(1f);
            }
            finally
            {
                owner.transform.position = position;
                motor.enabled = motorEnabled;
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyMonsterLogClient()
        {
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-death"))
            {
                for (int i = 0; i < 2; i++)
                    yield return VerifyMonsterLogClient(RainMonsterLogs.Entries[i].UnlockableId);
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-preloaded"))
            {
                yield return VerifyPreloadedMonsterLogsClient();
                yield break;
            }
            yield return VerifyMonsterLogExpiryClient();
            foreach (var entry in RainMonsterLogs.Entries)
                yield return VerifyMonsterLogClient(entry.UnlockableId);
        }

        IEnumerator VerifyPreloadedMonsterLogsHost(Gamemodes.RangeGamemode mode, PlayerInstance host,
            PlayerInstance guest, CharacterInstance owner)
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPSSHOOT_PROFILE_TEST_DIR")) ||
                !SteamRoomSession.Instance)
            {
                Debug.LogError("RAIN_COOP_MONSTER_LOG_PRELOADED_PROFILE_REQUIRED");
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 15f;
            bool reported = false;
            while (!reported && host && guest && Time.realtimeSinceStartup < deadline)
            {
                reported = true;
                foreach (var entry in RainMonsterLogs.Entries)
                    reported &= host.ServerHasRainMonsterLog(entry.UnlockableId) &&
                        guest.ServerHasRainMonsterLog(entry.UnlockableId);
                if (!reported) yield return null;
            }
            if (!reported)
            {
                foreach (var entry in RainMonsterLogs.Entries)
                    Debug.LogError($"RAIN_COOP_MONSTER_LOG_PRELOADED_REPORT_FAILED id={entry.UnlockableId} " +
                        $"host={(host && host.ServerHasRainMonsterLog(entry.UnlockableId))} " +
                        $"guest={(guest && guest.ServerHasRainMonsterLog(entry.UnlockableId))}");
                yield break;
            }
            foreach (var entry in RainMonsterLogs.Entries)
            {
                if (!SteamRoomSession.Instance.HasRainMonsterLog(entry.UnlockableId) ||
                    mode.TryDropMonsterLog(owner.transform.position + Vector3.forward * 8f, entry.Archetype, 0f) ||
                    GameObject.Find("Rain monster log " + entry.UnlockableId))
                    Debug.LogError("RAIN_COOP_MONSTER_LOG_PRELOADED_SUPPRESSION_FAILED " + entry.UnlockableId);
                else Debug.Log("RAIN_COOP_MONSTER_LOG_PRELOADED_CHECKED " + entry.UnlockableId);
            }
            // Keep the server alive while the remote process checks no pickup was replicated.
            yield return new WaitForSecondsRealtime(8f);
        }

        IEnumerator VerifyPreloadedMonsterLogsClient()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPSSHOOT_PROFILE_TEST_DIR")))
            {
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_PRELOADED_PROFILE_REQUIRED");
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 30f;
            PlayerInstance player = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                player = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerInstance>() : null;
                if (player && player.MyCharacter && GameManager.Gamemode is Gamemodes.RangeGamemode mode &&
                    mode.IsRainCoop && SteamRoomSession.Instance) break;
                yield return null;
            }
            if (!player || !player.MyCharacter || !SteamRoomSession.Instance || !NetworkClient.isConnected)
            {
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_PRELOADED_JOIN_FAILED");
                yield break;
            }
            foreach (var entry in RainMonsterLogs.Entries)
                if (!SteamRoomSession.Instance.HasRainMonsterLog(entry.UnlockableId))
                    Debug.LogError("RAIN_CLIENT_MONSTER_LOG_PRELOADED_UNLOCK_MISSING " + entry.UnlockableId);
            deadline = Time.realtimeSinceStartup + 5f;
            while (NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
            {
                foreach (var entry in RainMonsterLogs.Entries)
                    if (GameObject.Find("Rain monster log " + entry.UnlockableId))
                    {
                        Debug.LogError("RAIN_CLIENT_MONSTER_LOG_PRELOADED_UNEXPECTED_PICKUP " + entry.UnlockableId);
                        yield break;
                    }
                yield return null;
            }
            if (!NetworkClient.isConnected)
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_PRELOADED_DISCONNECTED");
            else foreach (var entry in RainMonsterLogs.Entries)
                Debug.Log("RAIN_CLIENT_MONSTER_LOG_PRELOADED_CHECKED " + entry.UnlockableId);
        }

        IEnumerator VerifyMonsterLogClient(string logId)
        {
            string modelName = "Rain monster log " + logId;
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("TPSSHOOT_PROFILE_TEST_DIR")) ||
                !SteamRoomSession.Instance || SteamRoomSession.Instance.HasRainMonsterLog(logId))
            {
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_FRESH_TEST_PROFILE_REQUIRED");
                yield break;
            }
            float deadline = Time.realtimeSinceStartup + 30f;
            GameObject pickup = null;
            while (!(pickup = GameObject.Find(modelName)) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!pickup)
            {
                Debug.LogError("RAIN_CLIENT_MONSTER_LOG_MODEL_MISSING");
                yield break;
            }
            Vector3 initial = pickup.transform.position;
            bool moved = false;
            while (NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
            {
                moved |= pickup && (pickup.transform.position - initial).sqrMagnitude > .04f;
                if (SteamRoomSession.Instance.HasRainMonsterLog(logId) && !pickup)
                {
                    if (moved) Debug.Log("RAIN_CLIENT_MONSTER_LOG_CHECKED " + logId);
                    else Debug.LogError("RAIN_CLIENT_MONSTER_LOG_MOTION_MISSING " + logId);
                    yield break;
                }
                yield return null;
            }
            Debug.LogError("RAIN_CLIENT_MONSTER_LOG_GRANT_OR_REMOVAL_FAILED");
        }
#endif

        IEnumerator VerifyGhorGoldPack(Gamemodes.RangeGamemode mode, PlayerInstance player)
        {
            var character = player.MyCharacter;
            var items = character.GetComponent<RangeResearchItems>();
            Vector3 originalPosition = character.transform.position;
            Vector3 dropPosition = originalPosition + Vector3.up * 15f;
            int originalGold = player.RainGold;
            int tome = RangeResearchItems.CatalogIndex("BonusGoldPackOnKill");
            for (int i = 0; i < 25; i++) items.Grant(tome);
            var victim = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), dropPosition, Quaternion.identity)
                .GetComponent<CharacterInstance>();
            foreach (var slot in victim.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            victim.Team = character.Team == 0 ? 1 : 0;
            victim.MaxHealth = victim.CurrentHealth = 1;
            NetworkServer.Spawn(victim.gameObject);
            victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, character, 0, 0f);
            var pickup = mode.transform.Find("Ghor gold pickup");
            if (!pickup || player.RainGold != originalGold)
                Debug.LogError("RAIN_COOP_GHOR_DROP");
            if (pickup)
            {
                var motor = character.GetComponent<CharacterMotor>();
                var networkTransform = character.GetComponent<NetworkTransformBase>();
                var originalSyncDirection = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
                bool motorEnabled = motor.enabled;
                motor.enabled = false;
                try
                {
                    if (networkTransform)
                    {
                        networkTransform.syncDirection = SyncDirection.ServerToClient;
                        networkTransform.ResetState();
                    }
                    character.transform.position = pickup.position - Vector3.up * .4f;
                    Physics.SyncTransforms();
                    float pickupDeadline = Time.realtimeSinceStartup + 1f;
                    while (pickup && Time.realtimeSinceStartup < pickupDeadline) yield return null;
                    if (player.RainGold != originalGold + 25 || pickup)
                        Debug.LogError($"RAIN_COOP_GHOR_PICKUP goldDelta={player.RainGold - originalGold} pickupPresent={(bool)pickup}");
                    else Debug.Log("RAIN_COOP_GHOR_PICKUP_CHECKED");
                }
                finally
                {
                    character.transform.position = originalPosition;
                    if (networkTransform)
                    {
                        networkTransform.RpcTeleport(originalPosition);
                        networkTransform.ResetState();
                        networkTransform.syncDirection = originalSyncDirection;
                    }
                    motor.enabled = motorEnabled;
                    Physics.SyncTransforms();
                }
            }

            character.transform.position = originalPosition;
            player.RainGold = originalGold;
            for (int i = 0; i < 25; i++) items.Remove(tome);
            player.RecordRainChestItems(items);
            if (victim) NetworkServer.Destroy(victim.gameObject);
        }

        static IEnumerator VerifyLeptonDaisyBeacon(Gamemodes.RangeGamemode mode, PlayerInstance player, Transform beacon, int index)
        {
            var owner = player.MyCharacter;
            var items = owner.GetComponent<RangeResearchItems>();
            int daisy = RangeResearchItems.CatalogIndex("TPHealingNova");
            int previousHealth = owner.CurrentHealth;
            Vector3 previousPosition = owner.transform.position;
            var nearby = SpawnNovaTarget(beacon.position + Vector3.right * 3f, owner.Team);
            var outside = SpawnNovaTarget(beacon.position + Vector3.right * 21f, owner.Team);
            var enemy = SpawnNovaTarget(beacon.position + Vector3.left * 3f, owner.Team == 0 ? 1 : 0);
            bool originalProtection = owner.RangeInvulnerable;
            owner.RangeInvulnerable = true;
            try
            {
                // Health registers with GameManager in Start, after the spawn call.
                yield return null;
                nearby.CurrentHealth = outside.CurrentHealth = enemy.CurrentHealth = 1;
                owner.transform.position = beacon.position;
                items.Grant(daisy);
                owner.CurrentHealth = 1;
                mode.ActivateRainBeacon(player, index);
                int healedOwner = owner.CurrentHealth;
                bool healed = healedOwner == 1 + Mathf.CeilToInt(owner.MaxHealth * .5f) &&
                    nearby.CurrentHealth == 51 && outside.CurrentHealth == 1 && enemy.CurrentHealth == 1;
                owner.CurrentHealth = 1;
                mode.ActivateRainBeacon(player, index);
                bool rejectedReplay = owner.CurrentHealth == 1 && nearby.CurrentHealth == 51 &&
                    mode.RainBeaconsActivated == index + 1;
                bool removed = items.Remove(daisy) && owner.ResearchLeptonDaisies == 0;
                if (healed && rejectedReplay && removed) Debug.Log("RAIN_COOP_LEPTON_DAISY_CHECKED");
                else Debug.LogError($"RAIN_COOP_LEPTON_DAISY healed={healed} repeat={rejectedReplay} removed={removed} progress={mode.RainBeaconsActivated} owner={healedOwner}/{owner.MaxHealth} near={nearby.CurrentHealth} outside={outside.CurrentHealth} enemy={enemy.CurrentHealth}");
            }
            finally
            {
                owner.RangeInvulnerable = originalProtection;
                owner.CurrentHealth = previousHealth;
                owner.transform.position = previousPosition;
                NetworkServer.Destroy(nearby.gameObject);
                NetworkServer.Destroy(outside.gameObject);
                NetworkServer.Destroy(enemy.gameObject);
            }

            CharacterInstance SpawnNovaTarget(Vector3 position, int team)
            {
                var target = Instantiate(mode.GetPlayerCharacterPrefab(), position, Quaternion.identity)
                    .GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = team;
                target.MaxHealth = 100;
                target.CurrentHealth = 1;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                return target;
            }
        }

        IEnumerator VerifyFireworkBeacon(Gamemodes.RangeGamemode mode, PlayerInstance player, Transform beacon, int index, bool checkMolten = false)
        {
            var character = player.MyCharacter;
            if (!character.TryGetComponent<RangeResearchItems>(out var items))
                items = character.gameObject.AddComponent<RangeResearchItems>();
            var motor = character.GetComponent<CharacterMotor>();
            Vector3 originalBeaconPosition = beacon.position;
            Vector3 originalPosition = character.transform.position;
            bool motorWasEnabled = motor.enabled;
            int firework = RangeResearchItems.CatalogIndex("Firework");
            const int targetHealth = 10000;
            CharacterInstance target = null;
            GameObject ceiling = null;
            items.Grant(firework);
            try
            {
                // Isolate the test from natural spawns and the wave's XP assertions.
                beacon.position += Vector3.up * 150f;
                motor.enabled = false;
                character.transform.position = beacon.position;
                target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    beacon.position + Vector3.right * 2f, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = character.Team == 0 ? 1 : 0;
                target.MaxHealth = target.CurrentHealth = targetHealth;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                ceiling = new GameObject("Rain beacon smoke firework ceiling");
                ceiling.transform.position = character.CombatAimPoint + Vector3.up * 1.5f;
                ceiling.AddComponent<BoxCollider>().size = new Vector3(8f, .5f, 8f);
                Physics.SyncTransforms();

                int goldBefore = player.RainGold;
                bool interacted = player.TryInteractRainNode();
                yield return new WaitForSeconds(.2f);
                mode.ActivateRainBeacon(player, index);

                float attack = character.RainAttackPower > 0f ? character.RainAttackPower : CharacterInstance.RainBaseAttackPower;
                int expectedHealth = targetHealth - 8 * Mathf.RoundToInt(3f * attack);
                float deadline = Time.time + 3f;
                yield return new WaitUntil(() => target.CurrentHealth <= expectedHealth || Time.time >= deadline);
                yield return new WaitForSeconds(.55f);
                if (!interacted || target.CurrentHealth != expectedHealth || mode.RainBeaconsActivated != index + 1 ||
                    player.RainGold != goldBefore + 20)
                    Debug.LogError("RAIN_COOP_FIREWORK_BEACON");
                else Debug.Log("RAIN_COOP_FIREWORK_BEACON_CHECKED");
                if (checkMolten)
                {
                    var hitbox = ceiling.AddComponent<HitBox>();
                    hitbox._health = target;
                    ceiling.layer = (int)GameLayers.hitbox;
                    ceiling.GetComponent<BoxCollider>().isTrigger = true;
                    ceiling.transform.position = target.transform.position + Vector3.up * 2f;
                    Physics.SyncTransforms();
                    target.CurrentHealth = targetHealth;
                    int molten = RangeResearchItems.CatalogIndex("FireballsOnHit");
                    items.Grant(molten);
                    items.Grant(molten);
                    target.Server_ChangeHealthStateRaw(20, CharacterPart.body, AttackType.hitscan, character, 0, 10f);
                    int expectedMoltenHealth = targetHealth - 20 - 3 * (120 + 60);
                    float moltenDeadline = Time.time + 13f;
                    yield return new WaitUntil(() => target.CurrentHealth <= expectedMoltenHealth || Time.time >= moltenDeadline);
                    if (target.CurrentHealth == expectedMoltenHealth)
                        Debug.Log("RAIN_COOP_MOLTEN_DAMAGE_CHECKED");
                    else Debug.LogError($"RAIN_COOP_MOLTEN_DAMAGE loss={targetHealth - target.CurrentHealth}");
                    target.ServerSetMaximumHealth(100);
                    target.CurrentHealth = 50;
                    target.RainHealthRegen = 10f;
                    var ignite = target.GetComponent<RangeBleeding>();
                    if (!ignite) ignite = target.gameObject.AddComponent<RangeBleeding>();
                    ignite.AddIgnite(character, 20, 24f);
                    yield return new WaitForSeconds(.85f);
                    int duringBurn = target.CurrentHealth;
                    bool cappedAndSuppressed = ignite.HasBurn() && duringBurn >= 44 && duringBurn <= 48;
                    float burnDeadline = Time.time + 2f;
                    yield return new WaitUntil(() => !ignite.HasBurn() || Time.time >= burnDeadline);
                    bool finishedAtExpectedHealth = !ignite.HasBurn() && target.CurrentHealth == 40;
                    yield return new WaitForSeconds(.45f);
                    if (cappedAndSuppressed && finishedAtExpectedHealth && target.CurrentHealth > 40)
                        Debug.Log("RAIN_COOP_MOLTEN_IGNITE_CAP_AND_REGEN_CHECKED");
                    else Debug.LogError($"RAIN_COOP_MOLTEN_IGNITE_CAP_AND_REGEN during={duringBurn} completed={finishedAtExpectedHealth} after={target.CurrentHealth}");
                    items.Remove(molten);
                    items.Remove(molten);
                    Vector3 wispPosition = character.transform.position + Vector3.forward * 24f;
                    var wisp = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        wispPosition, Quaternion.identity).GetComponent<CharacterInstance>();
                    foreach (var slot in wisp.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    wisp.Team = target.Team;
                    wisp.RainEnemyKind = RangeResearchEnemy.GreaterWispKind + 1;
                    wisp.MaxHealth = wisp.CurrentHealth = 1000;
                    NetworkServer.Spawn(wisp.gameObject);
                    var wispActor = wisp.gameObject.AddComponent<RangeResearchEnemy>();
                    wispActor.Configure(character, RangeResearchEnemy.GreaterWispKind);
                    wispActor.SetCombat(true);
                    float shotDeadline = Time.realtimeSinceStartup + 8f;
                    yield return new WaitUntil(() => wisp.RainGreaterWispShots.Count >= 2 ||
                        Time.realtimeSinceStartup >= shotDeadline);
                    bool farShotsPreserved = false;
                    bool allyShotsPreserved = false;
                    if (wisp.RainGreaterWispShots.Count == 2)
                    {
                        RangeResearchEnemy.ClearHostileProjectiles(character, character.CombatAimPoint, 5f);
                        farShotsPreserved = wisp.RainGreaterWispShots.Count == 2;
                        Vector3 shotOrigin = wisp.RainGreaterWispShots.Values.First().Origin;
                        int enemyTeam = wisp.Team;
                        wisp.Team = character.Team;
                        RangeResearchEnemy.ClearHostileProjectiles(character, shotOrigin, 5f);
                        allyShotsPreserved = wisp.RainGreaterWispShots.Count == 2;
                        wisp.Team = enemyTeam;
                        character.transform.position = shotOrigin - Vector3.up;
                        Physics.SyncTransforms();
                    }

                    var shower = RangeResearchItems.Equipment.BlastShower;
                    var ownerDots = character.GetComponent<RangeBleeding>();
                    if (!ownerDots) ownerDots = character.gameObject.AddComponent<RangeBleeding>();
                    ownerDots.AddBurn(target, 10, 5);
                    character.ResearchRootUntil = NetworkTime.time + 3d;
                    bool showerUsed = items.Equip(shower) && items.TryUseEquipment();
                    if (!showerUsed || ownerDots.HasBurn() || character.ResearchRooted ||
                        Mathf.Abs(items.EquipmentCooldownRemaining - 20f) > .05f || items.TryUseEquipment())
                        Debug.LogError("RAIN_COOP_BLAST_SHOWER_CLEANSE");
                    else Debug.Log("RAIN_COOP_BLAST_SHOWER_CLEANSE_CHECKED");
                    if (!farShotsPreserved || !allyShotsPreserved || wisp.RainGreaterWispShots.Count != 0)
                        Debug.LogError($"RAIN_COOP_BLAST_SHOWER_PROJECTILES far={farShotsPreserved} ally={allyShotsPreserved} remaining={wisp.RainGreaterWispShots.Count}");
                    else Debug.Log("RAIN_COOP_BLAST_SHOWER_PROJECTILES_CHECKED");
                    if (wisp) NetworkServer.Destroy(wisp.gameObject);
                    var bruiser = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        character.transform.position + Vector3.right * 24f, Quaternion.identity).GetComponent<CharacterInstance>();
                    foreach (var slot in bruiser.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    bruiser.Team = target.Team;
                    bruiser.RainEnemyKind = RangeResearchEnemy.BruiserKind + 1;
                    bruiser.MaxHealth = bruiser.CurrentHealth = 1000;
                    NetworkServer.Spawn(bruiser.gameObject);
                    var bruiserActor = bruiser.gameObject.AddComponent<RangeResearchEnemy>();
                    bruiserActor.Configure(character, RangeResearchEnemy.BruiserKind);
                    bruiser.GetComponent<CharacterMotor>().enabled = false;
                    bruiserActor.SetCombat(true);
                    int fuel = RangeResearchItems.CatalogIndex("EquipmentMagazine");
                    items.Grant(fuel);
                    shotDeadline = Time.realtimeSinceStartup + 8f;
                    yield return new WaitUntil(() => bruiser.RainBruiserFireballs.Count > 0 ||
                        Time.realtimeSinceStartup >= shotDeadline);
                    bool bruiserCleared = false;
                    if (bruiser.RainBruiserFireballs.Count > 0)
                    {
                        character.transform.position = bruiser.RainBruiserFireballs.Values.First().Origin - Vector3.up;
                        Physics.SyncTransforms();
                        bruiserCleared = items.TryUseEquipment() && bruiser.RainBruiserFireballs.Count == 0;
                    }
                    if (bruiserCleared) Debug.Log("RAIN_COOP_BLAST_SHOWER_BRUISER_CHECKED");
                    else Debug.LogError($"RAIN_COOP_BLAST_SHOWER_BRUISER shots={bruiser.RainBruiserFireballs.Count} cooldown={items.EquipmentCooldownRemaining}");
                    if (bruiser) NetworkServer.Destroy(bruiser.gameObject);
                    items.Grant(fuel);
                    yield return VerifyBlastShowerQueen(character, items, target.Team);
                    items.Remove(fuel);
                    items.Remove(fuel);
                    float beforeShowerSwap = items.EquipmentCooldownRemaining;
                    if (!items.Remove(shower) || !items.Equip(shower) || beforeShowerSwap <= 0f ||
                        items.EquipmentCooldownRemaining != beforeShowerSwap || items.TryUseEquipment())
                        Debug.LogError("RAIN_COOP_BLAST_SHOWER_REEQUIP");
                    else Debug.Log("RAIN_COOP_BLAST_SHOWER_REEQUIP_CHECKED");
                    items.Remove(shower);
                }
            }
            finally
            {
                if (target) NetworkServer.Destroy(target.gameObject);
                if (ceiling) Destroy(ceiling);
                items.Remove(firework);
                beacon.position = originalBeaconPosition;
                character.transform.position = originalPosition;
                motor.enabled = motorWasEnabled;
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyBlastShowerQueen(CharacterInstance owner, RangeResearchItems items, int enemyTeam)
        {
            Vector3 originalPosition = owner.transform.position;
            bool originalInvulnerability = owner.RangeInvulnerable;
            var queen = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                originalPosition + Vector3.forward * 7f, Quaternion.identity).GetComponent<CharacterInstance>();
            GameObject backstop = null;
            try
            {
                owner.RangeInvulnerable = true;
                foreach (var slot in queen.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                queen.Team = enemyTeam;
                queen.RainEnemyKind = RangeResearchEnemy.QueenKind + 1;
                queen.MaxHealth = queen.CurrentHealth = 1000;
                NetworkServer.Spawn(queen.gameObject);
                var actor = queen.gameObject.AddComponent<RangeResearchEnemy>();
                actor.Configure(owner, RangeResearchEnemy.QueenKind);
                queen.GetComponent<CharacterMotor>().enabled = false;
                backstop = new GameObject("Rain acid cleanup smoke backstop");
                backstop.transform.position = originalPosition + Vector3.back * 2f + Vector3.up;
                backstop.AddComponent<BoxCollider>().size = new Vector3(16f, 10f, .5f);
                Physics.SyncTransforms();
                actor.SetCombat(true);

                float deadline = Time.realtimeSinceStartup + 30f;
                yield return new WaitUntil(() => (queen.RainQueenAcidPools.Count > 0 && queen.RainQueenSpit.Count == 0) ||
                    Time.realtimeSinceStartup >= deadline);
                bool formedPool = queen.RainQueenAcidPools.Count > 0;
                yield return new WaitUntil(() => queen.RainQueenSpit.Count > 0 || Time.realtimeSinceStartup >= deadline);
                int poolsBefore = queen.RainQueenAcidPools.Count;
                bool cleared = false;
                if (formedPool && poolsBefore > 0 && queen.RainQueenSpit.Count > 0)
                {
                    owner.transform.position = queen.RainQueenSpit.Values.First().Origin - Vector3.up;
                    Physics.SyncTransforms();
                    cleared = items.TryUseEquipment() && queen.RainQueenSpit.Count == 0 &&
                        queen.RainQueenAcidPools.Count == poolsBefore;
                }

                if (cleared) Debug.Log("RAIN_COOP_BLAST_SHOWER_QUEEN_CHECKED");
                else Debug.LogError($"RAIN_COOP_BLAST_SHOWER_QUEEN formedPool={formedPool} pools={poolsBefore} shots={queen.RainQueenSpit.Count}");
            }
            finally
            {
                if (queen) NetworkServer.Destroy(queen.gameObject);
                if (backstop) Destroy(backstop);
                owner.transform.position = originalPosition;
                owner.RangeInvulnerable = originalInvulnerability;
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyFireworkChest(Gamemodes.RangeGamemode mode, PlayerInstance player)
        {
            int index = -1;
            for (int i = 0; i < Gamemodes.RangeGamemode.RainChestCount; i++)
            {
                if (Gamemodes.RangeGamemode.RainChestRewardId(i) != "Firework") continue;
                index = i;
                break;
            }
            if (index < 0 || !mode.TryGetRainMapNode(index, false, out var chest))
            {
                Debug.LogError("RAIN_COOP_FIREWORK_CHEST_MISSING");
                yield break;
            }

            var chestNode = mode.transform.Find("Rain supply chest " + index);
            if (!chestNode)
            {
                Debug.LogError("RAIN_COOP_FIREWORK_CHEST_NODE_MISSING");
                yield break;
            }

            var character = player.MyCharacter;
            var networkTransform = character.GetComponent<NetworkTransformBase>();
            var originalSyncDirection = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            if (!character.TryGetComponent<RangeResearchItems>(out var items))
                items = character.gameObject.AddComponent<RangeResearchItems>();
            var motor = character.GetComponent<CharacterMotor>();
            Vector3 originalPosition = character.transform.position;
            int originalGold = player.RainGold;
            bool motorWasEnabled = motor.enabled;
            int firework = RangeResearchItems.CatalogIndex("Firework");
            int price = Gamemodes.RangeGamemode.RainChestPrice;
            const int targetHealth = 10000;
            CharacterInstance target = null;
            GameObject ceilingObject = null;
            items.Grant(firework);
            try
            {
                motor.enabled = false;
                chestNode.position = chest + Vector3.up * 150f;
                character.transform.position = chestNode.position;
                if (networkTransform)
                {
                    networkTransform.syncDirection = SyncDirection.ServerToClient;
                    networkTransform.ResetState();
                }
                player.RainGold = 2 * price;
                target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    chestNode.position + Vector3.right * 2f, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = character.Team == 0 ? 1 : 0;
                target.MaxHealth = target.CurrentHealth = targetHealth;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;

                ceilingObject = new GameObject("Rain coop firework ceiling");
                ceilingObject.transform.position = character.CombatAimPoint + Vector3.up * 1.5f;
                ceilingObject.AddComponent<BoxCollider>().size = new Vector3(8f, .5f, 8f);
                Physics.SyncTransforms();

                mode.BuyRainChest(player, index);
                mode.BuyRainChest(player, index);
                float attack = character.RainAttackPower > 0f ? character.RainAttackPower : CharacterInstance.RainBaseAttackPower;
                int expectedHealth = targetHealth - 8 * Mathf.RoundToInt(3f * attack);
                float deadline = Time.time + 3f;
                yield return new WaitUntil(() => target.CurrentHealth <= expectedHealth || Time.time >= deadline);
                yield return new WaitForSeconds(.55f);
                bool chestStillOpen = mode.TryGetRainMapNode(index, false, out _);
                if (target.CurrentHealth != expectedHealth || items.FireworkStacks != 2 || player.RainGold != price || chestStillOpen)
                    Debug.LogError($"RAIN_COOP_FIREWORK_CHEST_TRANSACTION loss={targetHealth - target.CurrentHealth} expected={targetHealth - expectedHealth} stacks={items.FireworkStacks} gold={player.RainGold} price={price} available={chestStillOpen}");
                else Debug.Log("RAIN_COOP_FIREWORK_CHEST_CHECKED");
            }
            finally
            {
                if (target) NetworkServer.Destroy(target.gameObject);
                if (ceilingObject) Destroy(ceilingObject);
                items.Remove(firework);
                items.Remove(firework);
                chestNode.position = chest;
                character.transform.position = originalPosition;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(originalPosition);
                    networkTransform.ResetState();
                    networkTransform.syncDirection = originalSyncDirection;
                }
                motor.enabled = motorWasEnabled;
                player.RainGold = originalGold;
            }
        }

        void VerifyBrittleCrown(PlayerInstance player)
        {
            var character = player.MyCharacter;
            if (!character.TryGetComponent<RangeResearchItems>(out var items))
                items = character.gameObject.AddComponent<RangeResearchItems>();
            var enemy = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                character.transform.position + Vector3.up * 10f, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in enemy.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            enemy.Team = character.Team == 0 ? 1 : 0;
            enemy.MaxHealth = enemy.CurrentHealth = 100;
            NetworkServer.Spawn(enemy.gameObject);
            enemy.GetComponent<CharacterMotor>().enabled = false;

            int originalGold = player.RainGold;
            int originalHealth = character.CurrentHealth;
            int crown = RangeResearchItems.CatalogIndex("GoldOnHit");
            int shield = RangeResearchItems.CatalogIndex("PersonalShield");
            items.Grant(crown);
            items.Grant(crown);
            player.RainGold = 100;
            enemy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, character, 0, 0f);
            bool zeroProc = player.RainGold == 100;
            enemy.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, character, 0, 4f);
            bool earned = player.RainGold == 104;

            items.Grant(shield);
            character.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, enemy, 0, 0f);
            bool shieldProtected = player.RainGold == 104 && character.CurrentHealth == originalHealth;
            items.Remove(shield);
            int damage = Mathf.Max(1, character.MaxHealth / 10);
            character.Server_ChangeHealthStateRaw(damage, CharacterPart.body, AttackType.hitscan, enemy, 0, 0f);
            int expectedGold = 104 - (int)(104d * damage / character.MaxHealth * 2d);
            bool lost = player.RainGold == expectedGold && character.CurrentHealth == originalHealth - damage;

            items.Remove(crown);
            items.Remove(crown);
            player.RainGold = originalGold;
            player.RecordRainChestItems(items);
            character.CurrentHealth = originalHealth;
            character.ServerSetMaximumHealth(character.MaxHealth);
            NetworkServer.Destroy(enemy.gameObject);
            if (!zeroProc || !earned || !shieldProtected || !lost)
                Debug.LogError($"RAIN_COOP_BRITTLE_CROWN zeroProc={zeroProc} earned={earned} shield={shieldProtected} lost={lost}");
            else Debug.Log("RAIN_COOP_BRITTLE_CROWN_CHECKED");
        }

        static void VerifyExecutiveCard(Gamemodes.RangeGamemode mode, PlayerInstance player)
        {
            if (!VerifyEquipmentChest(mode, player, Gamemodes.RangeGamemode.RainCardChest,
                    RangeResearchItems.Equipment.ExecutiveCard)) return;

            int first = Gamemodes.RangeGamemode.FirstRainMultishopChest;
            var character = player.MyCharacter;
            var items = character.GetComponent<RangeResearchItems>();
            if (!mode.TryGetRainMapNode(first, false, out var firstPosition) ||
                !mode.TryGetRainMapNode(first + 1, false, out var secondPosition))
            {
                Debug.LogError("RAIN_COOP_MULTISHOP_MISSING");
                items.Remove(RangeResearchItems.Equipment.ExecutiveCard);
                player.RecordRainChestItems(items);
                return;
            }

            Vector3 originalPosition = character.transform.position;
            int originalGold = player.RainGold;
            int price = Gamemodes.RangeGamemode.RainChestPrice;
            character.transform.position = firstPosition;
            player.RainGold = price - 1;
            mode.BuyRainChest(player, first);
            bool rejected = player.RainGold == price - 1 && mode.TryGetRainMapNode(first, false, out _);
            player.RainGold = price;
            mode.BuyRainChest(player, first);
            bool cardPurchase = player.RainGold == price / 10 &&
                !mode.TryGetRainMapNode(first, false, out _) &&
                mode.TryGetRainMapNode(first + 1, false, out _) && mode.TryGetRainMapNode(first + 2, false, out _) &&
                items.Remove(RangeResearchItems.CatalogIndex("Hoof"));

            items.Remove(RangeResearchItems.Equipment.ExecutiveCard);
            character.transform.position = secondPosition;
            player.RainGold = price;
            mode.BuyRainChest(player, first + 1);
            bool regularPurchase = player.RainGold == 0 && !mode.TryGetRainMapNode(first + 1, false, out _) &&
                !mode.TryGetRainMapNode(first + 2, false, out _) &&
                items.Remove(RangeResearchItems.CatalogIndex("Syringe"));
            character.transform.position = originalPosition;
            player.RainGold = originalGold;
            player.RecordRainChestItems(items);
            if (!rejected || !cardPurchase || !regularPurchase)
                Debug.LogError($"RAIN_COOP_MULTISHOP_TRANSACTION rejected={rejected} card={cardPurchase} regular={regularPurchase}");
            else Debug.Log("RAIN_COOP_MULTISHOP_CHECKED");
        }

        static bool VerifyEquipmentChest(Gamemodes.RangeGamemode mode, PlayerInstance player, int index,
            RangeResearchItems.Equipment expected)
        {
            var chest = mode.transform.Find("Rain supply chest " + index);
            if (!chest)
            {
                Debug.LogError("RAIN_COOP_EQUIPMENT_CHEST_MISSING " + index);
                return false;
            }

            var character = player.MyCharacter;
            Vector3 originalPosition = character.transform.position;
            int originalGold = player.RainGold;
            int price = Gamemodes.RangeGamemode.RainChestPrice;
            player.RainGold = 2 * price;
            character.transform.position = chest.position + Vector3.up * 10f;
            mode.BuyRainChest(player, index);
            bool refusedFromDistance = player.RainGold == 2 * price;
            character.transform.position = chest.position;
            mode.BuyRainChest(player, index);
            bool bought = player.RainGold == price && player.RainEquipment == expected &&
                character.TryGetComponent<RangeResearchItems>(out var items) && items.Equipped == expected;
            mode.BuyRainChest(player, index);
            bool refusedDuplicate = player.RainGold == price;
            character.transform.position = originalPosition;
            player.RainGold = originalGold;
            if (refusedFromDistance && bought && refusedDuplicate) return true;
            Debug.LogError($"RAIN_COOP_EQUIPMENT_CHEST_TRANSACTION index={index} distance={refusedFromDistance} bought={bought} duplicate={refusedDuplicate}");
            return false;
        }

        static int CountBackupDrones(System.Collections.Generic.IEnumerable<NetworkIdentity> spawned, uint ownerId)
        {
            return spawned.Count(identity => identity &&
                identity.TryGetComponent<CharacterInstance>(out var character) &&
                character.RainDroneOwner == ownerId && character.RainDroneId == "BackupDrone");
        }

        static void VerifyRollOfPennies(Gamemodes.RangeGamemode mode, PlayerInstance player,
            CharacterInstance enemy, RangeResearchItems items)
        {
            var owner = player.MyCharacter;
            int gold = player.RainGold;
            int health = owner.CurrentHealth;
            int pennies = RangeResearchItems.CatalogIndex("GoldOnHurt");
            items.Grant(pennies);
            int doubleReward = (int)Math.Floor(6d * mode.RainDifficultyCoefficient());
            owner.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, enemy, 0, 0f);
            bool direct = player.RainGold == gold + doubleReward;
            owner.ServerApplyRangeDot(1, AttackType.explosion, enemy);
            bool periodic = player.RainGold == gold + 2 * doubleReward;
            bool removed = items.Remove(pennies);
            int singleReward = (int)Math.Floor(3d * mode.RainDifficultyCoefficient());
            owner.ServerApplyRangeDot(1, AttackType.explosion, owner);
            bool excludedSelf = player.RainGold == gold + 2 * doubleReward;
            owner.ServerApplyRangeDot(1, AttackType.explosion, enemy);
            bool single = player.RainGold == gold + 2 * doubleReward + singleReward;
            removed &= items.Remove(pennies);
            owner.ServerApplyRangeDot(1, AttackType.explosion, enemy);
            if (direct && periodic && excludedSelf && single && removed &&
                player.RainGold == gold + 2 * doubleReward + singleReward)
                Debug.Log("RAIN_COOP_ROLL_OF_PENNIES_CHECKED");
            else Debug.LogError("RAIN_COOP_ROLL_OF_PENNIES");
            player.RainGold = gold;
            owner.CurrentHealth = health;
        }

        static bool VerifyPassiveItemChest(Gamemodes.RangeGamemode mode, PlayerInstance guest, string reward)
        {
            int chest = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Where(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == reward && mode.TryGetRainMapNode(index, false, out _))
                .DefaultIfEmpty(-1).First();
            if (!mode.TryGetRainMapNode(chest, false, out var position))
            {
                Debug.LogError($"RAIN_COOP_PASSIVE_ITEM_CHEST_MISSING reward={reward}");
                return false;
            }

            var owner = guest.MyCharacter;
            Vector3 originalPosition = owner.transform.position;
            int originalGold = guest.RainGold;
            int price = Gamemodes.RangeGamemode.RainChestPrice;
            try
            {
                guest.RainGold = 2 * price;
                owner.transform.position = position + Vector3.up * 10f;
                mode.BuyRainChest(guest, chest);
                bool refusedFromDistance = guest.RainGold == 2 * price &&
                    mode.TryGetRainMapNode(chest, false, out _);
                owner.transform.position = position;
                mode.BuyRainChest(guest, chest);
                mode.BuyRainChest(guest, chest);
                if (refusedFromDistance && guest.RainGold == price && !mode.TryGetRainMapNode(chest, false, out _))
                    return true;
                Debug.LogError($"RAIN_COOP_PASSIVE_ITEM_CHEST_TRANSACTION reward={reward}");
                return false;
            }
            finally
            {
                owner.transform.position = originalPosition;
                guest.RainGold = originalGold;
            }
        }

        static IEnumerator VerifyDroneBackupHost(Gamemodes.RangeGamemode mode, PlayerInstance guest)
        {
            if (!VerifyPassiveItemChest(mode, guest, "StrengthenBurn") ||
                !VerifyPassiveItemChest(mode, guest, "GoldOnHurt") ||
                !VerifyPassiveItemChest(mode, guest, "AttackSpeedAndMoveSpeed")) yield break;
            int originalMaxHealth = guest.MyCharacter.MaxHealth;
            if (!VerifyPassiveItemChest(mode, guest, "HalfSpeedDoubleHealth") ||
                !VerifyPassiveItemChest(mode, guest, "IncreaseDamageOnMultiKill")) yield break;
            var originalOwner = guest.MyCharacter;
            var originalItems = originalOwner.GetComponent<RangeResearchItems>();
            int famine = RangeResearchItems.CatalogIndex("LowerHealing");
            int pain = RangeResearchItems.CatalogIndex("HexCurse");
            int personalShield = RangeResearchItems.CatalogIndex("PersonalShield");
            originalItems.Grant(personalShield);
            originalItems.Grant(pain);
            originalItems.Grant(pain);
            int cursedMaximum = (int)Math.Round(2d * originalMaxHealth / 1.6d);
            bool painBeforeRespawn = originalOwner.MaxHealth == cursedMaximum &&
                originalOwner.ResearchShield == Mathf.RoundToInt(cursedMaximum * .08f);
            originalItems.Grant(famine);
            originalItems.Grant(famine);
            originalOwner.CurrentHealth = 1;
            bool famineBeforeRespawn = originalOwner.ServerHeal(1, originalOwner.netId) == 0;
            originalItems.OnKill(originalOwner.transform.position, true);
            bool chronicBeforeRespawn = originalItems.ModifyDamage(200) == 207;
            int lantern = RangeResearchItems.CatalogIndex("AttackSpeedPerNearbyAllyOrEnemy");
            originalItems.Grant(lantern);
            originalItems.Grant(lantern);
            bool lanternBeforeRespawn = originalOwner.ResearchLanterns == 2;
            guest.RecordRainChestItems(originalItems);
            uint originalOwnerId = originalOwner.netId;
            mode.PlayerSpawnCharacterRequest(guest);
            var owner = guest.MyCharacter;
            if (!owner || owner.netId == originalOwnerId ||
                !owner.TryGetComponent<RangeResearchItems>(out var chestItems))
            {
                Debug.LogError("RAIN_COOP_PASSIVE_ITEMS_RESPAWN");
                yield break;
            }
            // The real PlayerSpawnCharacterRequest path restores inventory but never the old body's nearby count.
            bool lanternRestored = owner.ResearchLanterns == 2 && owner.ResearchLanternBodies == 0;
            bool lanternRemoved = chestItems.Remove(lantern) && chestItems.Remove(lantern) &&
                owner.ResearchLanterns == 0 && owner.ResearchLanternBodies == 0;
            if (lanternBeforeRespawn && lanternRestored && lanternRemoved)
                Debug.Log("RAIN_COOP_LANTERN_RESPAWN_CHECKED");
            else Debug.LogError($"RAIN_COOP_LANTERN_RESPAWN before={lanternBeforeRespawn} restore={lanternRestored} remove={lanternRemoved}");
            var motor = owner.GetComponent<CharacterMotor>();
            if (owner.ResearchMochas == 1 && Mathf.Approximately(owner.ResearchAttackSpeed, 1.075f) &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.07f / 2f))
                Debug.Log("RAIN_COOP_MOCHA_RESPAWN_CHECKED");
            else Debug.LogError("RAIN_COOP_MOCHA_RESPAWN");
            bool painRestored = owner.MaxHealth == cursedMaximum && owner.CurrentHealth == cursedMaximum &&
                owner.ResearchShield == Mathf.RoundToInt(cursedMaximum * .08f);
            owner.RangeInvulnerable = true;
            yield return new WaitForSeconds(3f);
            owner.RangeInvulnerable = false;
            bool painRemoved = chestItems.Remove(pain) &&
                owner.MaxHealth == (int)Math.Round(2d * originalMaxHealth / 1.3d) &&
                chestItems.Remove(pain) && owner.MaxHealth == 2 * originalMaxHealth &&
                owner.CurrentHealth == cursedMaximum &&
                owner.ResearchShield == Mathf.RoundToInt(owner.MaxHealth * .08f) &&
                chestItems.Remove(personalShield);
            if (painBeforeRespawn && painRestored && painRemoved)
                Debug.Log("RAIN_COOP_HEX_OF_PAIN_RESPAWN_CHECKED");
            else Debug.LogError($"RAIN_COOP_HEX_OF_PAIN_RESPAWN before={painBeforeRespawn} restored={painRestored} removed={painRemoved}");
            owner.CurrentHealth = owner.MaxHealth;
            if (owner.ResearchStoneFlux == 1 && owner.MaxHealth == 2 * originalMaxHealth &&
                owner.CurrentHealth == owner.MaxHealth)
                Debug.Log("RAIN_COOP_STONE_FLUX_RESPAWN_CHECKED");
            else Debug.LogError("RAIN_COOP_STONE_FLUX_RESPAWN");
            int fullHealth = owner.CurrentHealth;
            owner.CurrentHealth = 1;
            bool famineRestored = owner.ServerHeal(1, owner.netId) == 0 &&
                owner.ServerHeal(1, owner.netId) == 1;
            bool famineRemoved = chestItems.Remove(famine) && owner.ServerHeal(7, owner.netId) == 5 &&
                chestItems.Remove(famine) && owner.ServerHeal(7, owner.netId) == 7;
            owner.CurrentHealth = fullHealth;
            if (famineBeforeRespawn && famineRestored && famineRemoved)
                Debug.Log("RAIN_COOP_HEX_OF_FAMINE_RESPAWN_CHECKED");
            else Debug.LogError($"RAIN_COOP_HEX_OF_FAMINE_RESPAWN before={famineBeforeRespawn} restored={famineRestored} removed={famineRemoved}");
            int cookedSteak = RangeResearchItems.CatalogIndex("CookedSteak");
            chestItems.Grant(cookedSteak);
            bool firstSteak = owner.MaxHealth == (int)Math.Round((originalMaxHealth + 50d) * 2.05d);
            chestItems.Grant(cookedSteak);
            bool secondSteak = owner.MaxHealth == (int)Math.Round((originalMaxHealth + 100d) * 2.10d);
            if (firstSteak && secondSteak && chestItems.Remove(cookedSteak) && chestItems.Remove(cookedSteak) &&
                owner.MaxHealth == fullHealth && owner.CurrentHealth == fullHealth)
                Debug.Log("RAIN_COOP_COOKED_STEAK_CHECKED");
            else Debug.LogError("RAIN_COOP_COOKED_STEAK");
            yield return VerifyUltimateMealCooking(mode, guest, chestItems);
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "CookedSteak");
            if (!mode.TryGetRainMapNode(station, false, out var stationPosition))
                Debug.LogError("RAIN_COOP_COOKING_STATION_MISSING");
            else
            {
                int gold = guest.RainGold;
                Vector3 position = owner.transform.position;
                guest.RainGold = 0;
                owner.transform.position = stationPosition;
                mode.BuyRainChest(guest, station);
                bool refusedWithoutIngredients = owner.MaxHealth == fullHealth && guest.RainSteak == 0;
                guest.RainGold = 2 * Gamemodes.RangeGamemode.RainChestPrice;
                for (int ingredient = 0; ingredient < Gamemodes.RangeGamemode.RainChestCount && guest.RainSteak < 2; ingredient++)
                {
                    if (Gamemodes.RangeGamemode.RainChestRewardId(ingredient) != "FlatHealth" ||
                        !mode.TryGetRainMapNode(ingredient, false, out var ingredientPosition)) continue;
                    owner.transform.position = ingredientPosition;
                    mode.BuyRainChest(guest, ingredient);
                }
                int withIngredients = owner.MaxHealth;
                owner.transform.position = stationPosition + Vector3.up * 10f;
                mode.BuyRainChest(guest, station);
                bool refusedFromDistance = guest.RainSteak == 2 && chestItems.Steak == 2 &&
                    owner.MaxHealth == withIngredients;
                var stationTransform = owner.GetComponent<NetworkTransformBase>();
                var stationDirection = stationTransform ? stationTransform.syncDirection : SyncDirection.ServerToClient;
                motor.enabled = false;
                owner.RangeInvulnerable = true;
                owner.transform.position = stationPosition;
                if (stationTransform)
                {
                    stationTransform.syncDirection = SyncDirection.ServerToClient;
                    stationTransform.RpcTeleport(stationPosition);
                    stationTransform.ResetState();
                }
                float cookingDeadline = Time.realtimeSinceStartup + 10f;
                yield return new WaitUntil(() => chestItems.Steak == 0 || Time.realtimeSinceStartup >= cookingDeadline);
                yield return new WaitForSeconds(1f);
                bool cooked = chestItems.Steak == 0 && guest.RainSteak == 0 && guest.RainGold == 0 &&
                    owner.MaxHealth == (int)Math.Round((originalMaxHealth + 50d) * 2.05d);
                int stationAtSettlement = mode.NearbyRainChest(owner);
                int cookedMaximum = owner.MaxHealth;
                owner.transform.position = position;
                if (stationTransform)
                {
                    stationTransform.RpcTeleport(position);
                    stationTransform.syncDirection = stationDirection;
                    stationTransform.ResetState();
                }
                motor.enabled = true;
                owner.RangeInvulnerable = false;
                guest.RainGold = gold;
                bool mealRestored = false;
                bool removedMeal = false;
                if (cooked)
                {
                    uint cookedOwnerId = owner.netId;
                    mode.PlayerSpawnCharacterRequest(guest);
                    owner = guest.MyCharacter;
                    if (!owner || owner.netId == cookedOwnerId || !owner.TryGetComponent(out chestItems))
                    {
                        Debug.LogError("RAIN_COOP_COOKED_STEAK_RESPAWN_FIXTURE");
                        yield break;
                    }
                    motor = owner.GetComponent<CharacterMotor>();
                    mealRestored = guest.RainSteak == 0 && chestItems.Steak == 0 &&
                        owner.MaxHealth == cookedMaximum && owner.CurrentHealth == cookedMaximum;
                    removedMeal = chestItems.Remove(cookedSteak) && owner.MaxHealth == fullHealth &&
                        owner.CurrentHealth == fullHealth;
                    if (mealRestored && removedMeal) Debug.Log("RAIN_COOP_COOKED_STEAK_RESPAWN_CHECKED");
                    else Debug.LogError($"RAIN_COOP_COOKED_STEAK_RESPAWN restored={mealRestored} removed={removedMeal}");
                }
                if (refusedWithoutIngredients && refusedFromDistance && cooked && mealRestored && removedMeal)
                    Debug.Log("RAIN_COOP_COOKING_STATION_CHECKED");
                else Debug.LogError($"RAIN_COOP_COOKING_STATION noIngredients={refusedWithoutIngredients} distance={refusedFromDistance} cooked={cooked} removed={removedMeal} nearest={stationAtSettlement} station={station} point={stationPosition}");
            }
            bool chronicReset = chestItems.ModifyDamage(200) == 200;
            chestItems.OnKill(owner.transform.position, true);
            bool chronicRestored = chestItems.ModifyDamage(200) == 207;
            int expansion = RangeResearchItems.CatalogIndex("IncreaseDamageOnMultiKill");
            bool chronicRemoved = chestItems.Remove(expansion) && chestItems.ModifyDamage(200) == 200;
            if (chronicBeforeRespawn && chronicReset && chronicRestored && chronicRemoved)
                Debug.Log("RAIN_COOP_CHRONIC_EXPANSION_RESPAWN_CHECKED");
            else Debug.LogError("RAIN_COOP_CHRONIC_EXPANSION_RESPAWN");
            int tank = RangeResearchItems.CatalogIndex("StrengthenBurn");
            if (chestItems.ScaleBurnDamage(10) == 40 && chestItems.Remove(tank) && chestItems.ScaleBurnDamage(10) == 10)
                Debug.Log("RAIN_COOP_IGNITION_TANK_CHEST_CHECKED");
            else Debug.LogError("RAIN_COOP_IGNITION_TANK_CHEST_GRANT");
            int chest = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "DroneBackup");
            uint ownerId = owner.netId;
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            var originalDirection = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            Vector3 originalPosition = owner.transform.position;
            bool motorEnabled = motor.enabled;
            CharacterInstance target = null;
            const int targetHealth = 10000;
            const int expectedShots = 16;
            const int expectedDamage = 56;
            int hits = 0;
            var shotsByDrone = new System.Collections.Generic.Dictionary<uint, int>();
            int healthAfterLastShot = targetHealth;
            bool wrongShotDamage = false;
            float deadline;
            try
            {
                motor.enabled = false;
                owner.transform.position += Vector3.up * 150f;
                if (networkTransform)
                {
                    networkTransform.syncDirection = SyncDirection.ServerToClient;
                    networkTransform.RpcTeleport(owner.transform.position);
                    networkTransform.ResetState();
                }
                target = Instantiate(mode.GetPlayerCharacterPrefab(),
                    owner.transform.position + Vector3.forward * 8f, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                target.MaxHealth = target.CurrentHealth = targetHealth;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                if (owner.TryGetComponent<RangeResearchItems>(out var burnItems))
                {
                    yield return VerifyIgnitionTank(owner, target, burnItems);
                    VerifyRollOfPennies(mode, guest, target, burnItems);
                }
                target.Server_OnDamaged += (health, part, type, attacker, force) =>
                {
                    if (type != AttackType.hitscan || hits >= expectedShots ||
                        attacker is not CharacterInstance drone || drone.RainDroneOwner != ownerId ||
                        drone.RainDroneId != "BackupDrone") return;
                    shotsByDrone.TryGetValue(drone.netId, out int shots);
                    shotsByDrone[drone.netId] = shots + 1;
                    int damage = healthAfterLastShot - health;
                    wrongShotDamage |= damage != 3 && damage != 4;
                    healthAfterLastShot = health;
                    hits++;
                };
                if (!VerifyEquipmentChest(mode, guest, chest, RangeResearchItems.Equipment.DroneBackup)) yield break;
                Physics.SyncTransforms();
                deadline = Time.realtimeSinceStartup + 12f;
                yield return new WaitUntil(() => hits >= expectedShots || Time.realtimeSinceStartup >= deadline);
                if (hits == expectedShots && shotsByDrone.Count == 4 && shotsByDrone.Values.All(shots => shots == 4) &&
                    !wrongShotDamage && targetHealth - healthAfterLastShot == expectedDamage)
                    Debug.Log("RAIN_COOP_DRONE_BACKUP_BURST_CHECKED");
                else
                    Debug.LogError($"RAIN_COOP_DRONE_BACKUP_BURST hits={hits} damage={targetHealth - healthAfterLastShot} wrongShot={wrongShotDamage} drones={shotsByDrone.Count}");
            }
            finally
            {
                if (target) NetworkServer.Destroy(target.gameObject);
                if (owner) owner.transform.position = originalPosition;
                if (motor) motor.enabled = motorEnabled;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(originalPosition);
                    networkTransform.syncDirection = originalDirection;
                    networkTransform.ResetState();
                }
            }
            var items = guest.MyCharacter.GetComponent<RangeResearchItems>();
            if (CountBackupDrones(NetworkServer.spawned.Values, ownerId) != 4 ||
                items.EquipmentCooldownRemaining < 95f)
            {
                Debug.LogError("RAIN_COOP_DRONE_BACKUP_REMOTE_ACTIVATION");
                yield break;
            }
            Debug.Log("RAIN_COOP_DRONE_BACKUP_REMOTE_ACTIVATION_CHECKED");
            int stoneFlux = RangeResearchItems.CatalogIndex("HalfSpeedDoubleHealth");
            if (items.Remove(stoneFlux) && owner.MaxHealth == originalMaxHealth && owner.CurrentHealth == originalMaxHealth &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.07f))
                Debug.Log("RAIN_COOP_STONE_FLUX_REMOVE_CHECKED");
            else Debug.LogError("RAIN_COOP_STONE_FLUX_REMOVE");
            var beetle = GameManager.HealthInstances.Values.OfType<CharacterInstance>()
                .FirstOrDefault(enemy => enemy && enemy.RainEnemyKind == 1 && enemy.CurrentHealth > 0);
            var strikeDrone = NetworkServer.spawned.Values
                .Select(identity => identity ? identity.GetComponent<RangeDrone>() : null)
                .FirstOrDefault(drone => drone && drone.EquipmentSummoner == owner);
            if (!beetle || !strikeDrone)
                Debug.LogError("RAIN_COOP_DRONE_BACKUP_KILL_FIXTURE");
            else
            {
                int experience = guest.RainExperience;
                beetle.Server_ChangeHealthStateRaw(10000, CharacterPart.body, AttackType.hitscan,
                    strikeDrone.GetComponent<CharacterInstance>(), 0, 1f);
                if (guest.RainExperience == experience + 10)
                    Debug.Log("RAIN_COOP_DRONE_BACKUP_KILL_CREDIT_CHECKED");
                else Debug.LogError($"RAIN_COOP_DRONE_BACKUP_KILL_CREDIT gained={guest.RainExperience - experience}");
            }
            bool wasInvulnerable = owner.RangeInvulnerable;
            owner.RangeInvulnerable = true;
            try
            {
                // Let the client observe the removal before the level-up changes maximum health again.
                yield return new WaitForSeconds(1f);
                guest.GrantRainExperience(Mathf.Max(0, (int)RunLedger.Experience(2) - guest.RainExperience));
                yield return new WaitForSeconds(.65f);
                int upgraded = NetworkServer.spawned.Values.Count(identity => identity &&
                    identity.TryGetComponent<CharacterInstance>(out var drone) && drone.RainDroneOwner == ownerId &&
                    drone.RainDroneId == "BackupDrone" && drone.MaxHealth == 293 && drone.RainHealthRegen == 9f);
                if (upgraded == 4) Debug.Log("RAIN_COOP_DRONE_BACKUP_LEVEL_CHECKED");
                else Debug.LogError($"RAIN_COOP_DRONE_BACKUP_LEVEL upgraded={upgraded}");
                deadline = Time.realtimeSinceStartup + 30f;
                yield return new WaitUntil(() => CountBackupDrones(NetworkServer.spawned.Values, ownerId) == 0 ||
                    Time.realtimeSinceStartup >= deadline);
                if (CountBackupDrones(NetworkServer.spawned.Values, ownerId) == 0)
                    Debug.Log("RAIN_COOP_DRONE_BACKUP_EXPIRY_CHECKED");
                else Debug.LogError("RAIN_COOP_DRONE_BACKUP_EXPIRY");
                yield return VerifyMochaSteakCooking(mode, guest, items);
                yield return VerifyStewCooking(mode, guest, items);
                VerifyWhiteScrapStations(mode, guest, items);
                yield return VerifyRemoteWhiteScrapStations(mode, guest, items);
                yield return VerifyQuickFixStation(mode, guest, items);
                yield return VerifyHikingBootsInventoryHost(mode, guest, items);
                yield return VerifyScrapperSelectionHost(mode, guest, items);
                yield return VerifyLanternInventoryHost(mode, guest, items);
                yield return VerifyAntlersInventoryHost(mode, guest, items);
                yield return new WaitForSeconds(1f);
            }
            finally
            {
                if (owner) owner.RangeInvulnerable = wasInvulnerable;
            }
        }

        static IEnumerator VerifyMochaSteakCooking(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "CookedSteakMocha");
            if (!mode.TryGetRainMapNode(station, false, out var stationPosition))
            {
                Debug.LogError("RAIN_COOP_MOCHA_STEAK_STATION");
                yield break;
            }

            var owner = player.MyCharacter;
            Vector3 originalPosition = owner.transform.position;
            int originalGold = player.RainGold;
            int originalMaximum = owner.MaxHealth;
            int originalHealth = owner.CurrentHealth;
            owner.transform.position = stationPosition;
            mode.BuyRainChest(player, station);
            bool missingSteak = owner.ResearchMochas == 1 && owner.ResearchCookedSteaks == 0;
            if (!VerifyPassiveItemChest(mode, player, "FlatHealth"))
            {
                owner.transform.position = originalPosition;
                Debug.LogError($"RAIN_COOP_MOCHA_STEAK_MATERIAL_CHEST hp={owner.CurrentHealth}");
                yield break;
            }
            bool boughtRawSteak = items.Steak == 1 && player.RainSteak == 1;
            mode.BuyRainChest(player, station);
            mode.BuyRainChest(player, station);
            var motor = owner.GetComponent<CharacterMotor>();
            bool cooked = owner.ResearchMochas == 0 && items.Steak == 0 && player.RainSteak == 0 &&
                owner.ResearchCookedSteaks == 1 && owner.MaxHealth == (int)Math.Round((originalMaximum + 50d) * 1.05d) &&
                player.RainGold == originalGold && Mathf.Approximately(owner.ResearchAttackSpeed, 1f) &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed);
            string cookedState = $"mocha={owner.ResearchMochas} raw={items.Steak}/{player.RainSteak} cooked={owner.ResearchCookedSteaks} hp={owner.CurrentHealth}/{owner.MaxHealth}/{originalMaximum} gold={player.RainGold}/{originalGold} attack={owner.ResearchAttackSpeed:R} walk={motor.GetMoveSpeed(false):R}/{motor.WalkSpeed:R}";
            yield return new WaitForSeconds(1.5f);
            bool removed = items.Remove(RangeResearchItems.CatalogIndex("CookedSteak")) && owner.MaxHealth == originalMaximum;
            owner.transform.position = originalPosition;
            owner.CurrentHealth = originalHealth;
            if (missingSteak && boughtRawSteak && cooked && removed) Debug.Log("RAIN_COOP_MOCHA_STEAK_RECIPE_CHECKED");
            else Debug.LogError($"RAIN_COOP_MOCHA_STEAK_RECIPE missingSteak={missingSteak} boughtRawSteak={boughtRawSteak} cooked={cooked} removed={removed} {cookedState}");
        }

        static IEnumerator VerifyStewCooking(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "Stew");
            if (!mode.TryGetRainMapNode(station, false, out var stationPosition) ||
                !VerifyPassiveItemChest(mode, player, "TPHealingNova"))
            {
                Debug.LogError("RAIN_COOP_STEW_STATION_OR_DAISY_CHEST");
                yield break;
            }

            var owner = player.MyCharacter;
            var motor = owner.GetComponent<CharacterMotor>();
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            var direction = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            int originalGold = player.RainGold;
            try
            {
                motor.enabled = false;
                owner.transform.position = stationPosition;
                mode.BuyRainChest(player, station);
                bool refusedWithoutSeed = owner.ResearchLeptonDaisies == 1 &&
                    owner.ResearchStews == 0 && player.RainGold == originalGold;
                if (networkTransform)
                {
                    networkTransform.syncDirection = SyncDirection.ServerToClient;
                    networkTransform.RpcTeleport(stationPosition);
                    networkTransform.ResetState();
                }

                if (!VerifyPassiveItemChest(mode, player, "Seed")) yield break;
                float deadline = Time.realtimeSinceStartup + 8f;
                yield return new WaitUntil(() => owner.ResearchStews > 0 || Time.realtimeSinceStartup >= deadline);
                // Leave time for the client to observe the crafted item before removing it.
                yield return new WaitForSeconds(1.5f);
                bool cooked = owner.ResearchStews == 1 && owner.ResearchLeptonDaisies == 0 &&
                    owner.ResearchSeeds == 0 && player.RainGold == originalGold;
                bool removed = items.Remove(RangeResearchItems.CatalogIndex("Stew")) &&
                    owner.ResearchStews == 0 && Mathf.Approximately(owner.EffectiveRainHealthRegen, owner.RainHealthRegen);
                if (refusedWithoutSeed && cooked && removed) Debug.Log("RAIN_COOP_STEW_TRANSACTION_CHECKED");
                else Debug.LogError($"RAIN_COOP_STEW_TRANSACTION refused={refusedWithoutSeed} cooked={cooked} removed={removed} daisy={owner.ResearchLeptonDaisies} seed={owner.ResearchSeeds} stew={owner.ResearchStews} gold={player.RainGold}/{originalGold}");
                if (removed) VerifyDoubleDaisyStation(mode, player, items);
            }
            finally
            {
                if (owner) owner.transform.position = originalPosition;
                if (motor) motor.enabled = motorEnabled;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(originalPosition);
                    networkTransform.ResetState();
                    networkTransform.syncDirection = direction;
                }
            }
        }

        static void VerifyWhiteScrapStations(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int FindNode(string reward) => Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == reward);
            int scrapper = FindNode("ScrapBisonSteak");
            int printer = FindNode("PrintSyringe");
            if (!mode.TryGetRainMapNode(scrapper, false, out var scrapPosition) ||
                !mode.TryGetRainMapNode(printer, false, out var printPosition))
            {
                Debug.LogError("RAIN_COOP_WHITE_SCRAP_NODES");
                return;
            }

            var owner = player.MyCharacter;
            int gold = player.RainGold;
            int maxHealth = owner.MaxHealth;
            int health = owner.CurrentHealth;
            Vector3 originalPosition = owner.transform.position;
            int steak = RangeResearchItems.CatalogIndex("FlatHealth");
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            int syringe = RangeResearchItems.CatalogIndex("Syringe");
            try
            {
                items.Grant(steak);
                items.Grant(glasses);
                owner.transform.position = scrapPosition + Vector3.up * 10f;
                mode.BuyRainChest(player, scrapper);
                bool distanceRejected = items.Steak == 1 && owner.ResearchWhiteScrap == 0;
                owner.transform.position = scrapPosition;
                mode.BuyRainChest(player, scrapper);
                mode.BuyRainChest(player, scrapper);
                bool scrapped = items.Steak == 0 && player.RainSteak == 0 &&
                    owner.ResearchWhiteScrap == 1 && owner.MaxHealth == maxHealth;

                owner.transform.position = printPosition;
                mode.BuyRainChest(player, printer);
                bool priority = owner.ResearchWhiteScrap == 0 && owner.ResearchSyringes == 1 && items.Glasses == 1;
                items.Remove(syringe);
                mode.BuyRainChest(player, printer);
                bool fallback = owner.ResearchSyringes == 1 && items.Glasses == 0 && player.RainGold == gold;
                items.Remove(syringe);
                mode.BuyRainChest(player, printer);
                bool emptyRejected = owner.ResearchSyringes == 0 && player.RainGold == gold;
                if (distanceRejected && scrapped && priority && fallback && emptyRejected)
                    Debug.Log("RAIN_COOP_WHITE_SCRAP_STATIONS_CHECKED");
                else Debug.LogError($"RAIN_COOP_WHITE_SCRAP_STATIONS distance={distanceRejected} scrapped={scrapped} priority={priority} fallback={fallback} empty={emptyRejected}");
            }
            finally
            {
                owner.transform.position = originalPosition;
                owner.CurrentHealth = health;
                player.RecordRainChestItems(items);
            }
        }

        static IEnumerator VerifyRemoteWhiteScrapStations(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int scrapper = WhiteStationIndex("ScrapBisonSteak");
            int printer = WhiteStationIndex("PrintSyringe");
            if (!mode.TryGetRainMapNode(scrapper, false, out var scrapPosition) ||
                !mode.TryGetRainMapNode(printer, false, out var printPosition))
            {
                Debug.LogError("RAIN_COOP_REMOTE_WHITE_SCRAP_NODES");
                yield break;
            }

            var owner = player.MyCharacter;
            var motor = owner.GetComponent<CharacterMotor>();
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            var direction = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            int originalGold = player.RainGold;
            int originalHealth = owner.CurrentHealth;
            int originalMaximum = owner.MaxHealth;
            int steak = RangeResearchItems.CatalogIndex("FlatHealth");
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            int syringe = RangeResearchItems.CatalogIndex("Syringe");
            try
            {
                motor.enabled = false;
                if (networkTransform) networkTransform.syncDirection = SyncDirection.ServerToClient;
                MoveOwner(scrapPosition);
                items.Grant(steak);
                items.Grant(glasses);
                player.RecordRainChestItems(items);
                float deadline = Time.realtimeSinceStartup + 8f;
                yield return new WaitUntil(() => !owner || owner.ResearchWhiteScrap == 1 || Time.realtimeSinceStartup >= deadline);
                if (!owner || owner.ResearchWhiteScrap != 1 || player.RainSteak != 0 || owner.MaxHealth != originalMaximum)
                {
                    Debug.LogError("RAIN_COOP_REMOTE_WHITE_SCRAP_CONVERSION");
                    yield break;
                }

                MoveOwner(printPosition);
                deadline = Time.realtimeSinceStartup + 8f;
                yield return new WaitUntil(() => !owner || owner.ResearchSyringes > 0 || Time.realtimeSinceStartup >= deadline);
                if (owner && owner.ResearchSyringes == 1 && owner.ResearchWhiteScrap == 0 &&
                    items.Glasses == 1 && player.RainGold == originalGold)
                    Debug.Log("RAIN_COOP_REMOTE_WHITE_SCRAP_STATIONS_CHECKED");
                else Debug.LogError("RAIN_COOP_REMOTE_WHITE_SCRAP_PRINT");
                yield return new WaitForSeconds(1f);
            }
            finally
            {
                if (owner)
                {
                    items.Remove(steak);
                    items.Remove(glasses);
                    items.Remove(syringe);
                    items.Remove(RangeResearchItems.CatalogIndex("ScrapWhite"));
                    player.RecordRainChestItems(items);
                    owner.CurrentHealth = originalHealth;
                    MoveOwner(originalPosition);
                }
                if (motor) motor.enabled = motorEnabled;
                if (networkTransform) networkTransform.syncDirection = direction;
            }

            void MoveOwner(Vector3 position)
            {
                owner.transform.position = position;
                if (!networkTransform) return;
                networkTransform.RpcTeleport(position);
                networkTransform.ResetState();
            }
        }

        static IEnumerator VerifyHikingBootsInventoryHost(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            var owner = player.MyCharacter;
            if (!owner || owner.ResearchHikingBoots != 0)
            {
                Debug.LogError("RAIN_COOP_HIKING_BOOTS_INVENTORY_NOT_EMPTY");
                yield break;
            }
            int boots = RangeResearchItems.CatalogIndex("CritAtLowerElevation");
            try
            {
                bool bought = VerifyPassiveItemChest(mode, player, "CritAtLowerElevation") && owner.ResearchHikingBoots == 1;
                yield return new WaitForSeconds(2f);
                items.Grant(boots);
                player.RecordRainChestItems(items);
                bool stacked = owner && owner.ResearchHikingBoots == 2;
                yield return new WaitForSeconds(2f);
                bool removedOne = owner && items.Remove(boots) && owner.ResearchHikingBoots == 1;
                yield return new WaitForSeconds(2f);
                bool removedLast = owner && items.Remove(boots) && owner.ResearchHikingBoots == 0;
                yield return new WaitForSeconds(2f);
                if (bought && stacked && removedOne && removedLast)
                    Debug.Log("RAIN_COOP_HIKING_BOOTS_INVENTORY_CHECKED");
                else Debug.LogError($"RAIN_COOP_HIKING_BOOTS_INVENTORY bought={bought} stacked={stacked} one={removedOne} last={removedLast}");
            }
            finally
            {
                if (owner)
                {
                    while (owner.ResearchHikingBoots > 0 && items.Remove(boots)) { }
                    player.RecordRainChestItems(items);
                }
            }
        }

        static int WhiteStationIndex(string reward) => Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
            .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == reward);

        static IEnumerator VerifyQuickFixStation(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "BonusHealthBoost");
            int scrapper = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "ScrapBisonSteak");
            if (!mode.TryGetRainMapNode(station, false, out var position) ||
                !mode.TryGetRainMapNode(scrapper, false, out var scrapPosition))
            {
                Debug.LogError("RAIN_COOP_QUICK_FIX_STATION_OR_SCRAP");
                yield break;
            }

            var owner = player.MyCharacter;
            Vector3 originalPosition = owner.transform.position;
            int originalGold = player.RainGold;
            int maximum = owner.MaxHealth;
            var motor = owner.GetComponent<CharacterMotor>();
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            bool motorEnabled = motor.enabled;
            var direction = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            try
            {
                motor.enabled = false;
                if (!VerifyPassiveItemChest(mode, player, "FlatHealth")) yield break;
                owner.transform.position = scrapPosition;
                mode.BuyRainChest(player, scrapper);
                bool scrapProduced = owner.ResearchWhiteScrap == 1 && player.RainSteak == 0 &&
                    owner.MaxHealth == maximum && player.RainGold == originalGold;
                owner.transform.position = position;
                mode.BuyRainChest(player, station);
                bool refused = scrapProduced && owner.ResearchQuickFixes == 0 && owner.ResearchWhiteScrap == 1;
                if (networkTransform)
                {
                    networkTransform.syncDirection = SyncDirection.ServerToClient;
                    networkTransform.RpcTeleport(position);
                    networkTransform.ResetState();
                }
                if (!VerifyPassiveItemChest(mode, player, "AttackSpeedAndMoveSpeed")) yield break;
                bool boughtMocha = owner.ResearchMochas == 1;
                float deadline = Time.realtimeSinceStartup + 8f;
                yield return new WaitUntil(() => !owner || owner.ResearchQuickFixes > 0 || Time.realtimeSinceStartup >= deadline);
                if (!owner)
                {
                    Debug.LogError("RAIN_COOP_QUICK_FIX_REMOTE_OWNER_LOST");
                    yield break;
                }
                bool cooked = owner.ResearchQuickFixes == 1 && owner.ResearchWhiteScrap == 0 &&
                    owner.ResearchMochas == 0 && owner.MaxHealth == maximum && player.RainGold == originalGold;
                yield return new WaitForSeconds(1.5f);
                bool removed = items.Remove(RangeResearchItems.CatalogIndex("BonusHealthBoost")) &&
                    owner.ResearchQuickFixes == 0 && owner.MaxHealth == maximum;
                if (refused && boughtMocha && cooked && removed) Debug.Log("RAIN_COOP_QUICK_FIX_TRANSACTION_CHECKED");
                else Debug.LogError($"RAIN_COOP_QUICK_FIX_TRANSACTION refused={refused} boughtMocha={boughtMocha} cooked={cooked} removed={removed}");
            }
            finally
            {
                if (owner) owner.transform.position = originalPosition;
                if (motor) motor.enabled = motorEnabled;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(originalPosition);
                    networkTransform.ResetState();
                    networkTransform.syncDirection = direction;
                }
            }
        }

        static void VerifyDoubleDaisyStation(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "StewDoubleDaisy");
            if (!mode.TryGetRainMapNode(station, false, out var position) ||
                !VerifyPassiveItemChest(mode, player, "TPHealingNova"))
            {
                Debug.LogError("RAIN_COOP_STEW_DOUBLE_DAISY_STATION");
                return;
            }

            var owner = player.MyCharacter;
            int gold = player.RainGold;
            owner.transform.position = position;
            mode.BuyRainChest(player, station);
            bool refused = owner.ResearchLeptonDaisies == 1 && owner.ResearchStews == 0;
            items.Grant(RangeResearchItems.CatalogIndex("TPHealingNova"));
            mode.BuyRainChest(player, station);
            mode.BuyRainChest(player, station);
            bool cooked = owner.ResearchLeptonDaisies == 0 && owner.ResearchStews == 1 &&
                player.RainGold == gold && mode.TryGetRainMapNode(station, false, out _);
            if (refused && cooked && items.Remove(RangeResearchItems.CatalogIndex("Stew")))
                Debug.Log("RAIN_COOP_STEW_DOUBLE_DAISY_CHECKED");
            else Debug.LogError("RAIN_COOP_STEW_DOUBLE_DAISY");
        }

        static bool HexHudShowsStacks()
        {
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            return overlay && ShowsStack(42, "texHexCurseIcon") && ShowsStack(43, "texLowerHealingIcon");

            bool ShowsStack(int slot, string icon)
            {
                var item = overlay.Find("Combat inventory/Inventory item " + slot);
                return item && item.gameObject.activeInHierarchy &&
                    item.GetComponent<UnityEngine.UI.Image>().sprite?.name == icon &&
                    item.GetComponentInChildren<TMPro.TMP_Text>().text == "2";
            }
        }

        static IEnumerator VerifyUltimateMealCooking(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "UltimateMeal");
            if (!mode.TryGetRainMapNode(station, false, out var stationPosition))
            {
                Debug.LogError("RAIN_COOP_ULTIMATE_MEAL_STATION_MISSING");
                yield break;
            }

            var owner = player.MyCharacter;
            int originalGold = player.RainGold;
            Vector3 originalPosition = owner.transform.position;
            int meal = RangeResearchItems.CatalogIndex("UltimateMeal");
            int clover = RangeResearchItems.CatalogIndex("Clover");
            player.RainGold = 2 * Gamemodes.RangeGamemode.RainChestPrice;
            owner.transform.position = stationPosition;
            mode.BuyRainChest(player, station);
            bool refusedEmpty = !items.Remove(meal);
            for (int index = 0; index < Gamemodes.RangeGamemode.RainChestCount; index++)
            {
                if (Gamemodes.RangeGamemode.RainChestRewardId(index) != "Clover" ||
                    !mode.TryGetRainMapNode(index, false, out var position)) continue;
                owner.transform.position = position;
                mode.BuyRainChest(player, index);
            }

            var motor = owner.GetComponent<CharacterMotor>();
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            bool motorEnabled = motor.enabled;
            var direction = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            motor.enabled = false;
            owner.transform.position = stationPosition;
            if (networkTransform)
            {
                networkTransform.syncDirection = SyncDirection.ServerToClient;
                networkTransform.RpcTeleport(stationPosition);
                networkTransform.ResetState();
            }
            float deadline = Time.realtimeSinceStartup + 10f;
            yield return new WaitUntil(() => owner.ResearchUltimateMeals > 0 || Time.realtimeSinceStartup >= deadline);
            yield return new WaitForSeconds(2f);
            int glasses = RangeResearchItems.CatalogIndex("CritGlasses");
            int originalHealth = owner.CurrentHealth;
            var originalRandom = UnityEngine.Random.state;
            UnityEngine.Random.State mealRoll;
            do
            {
                mealRoll = UnityEngine.Random.state;
            } while (UnityEngine.Random.value < .1f || UnityEngine.Random.value < .1f || UnityEngine.Random.value >= .1f);

            items.Grant(glasses);
            owner.CurrentHealth = owner.MaxHealth;
            UnityEngine.Random.state = mealRoll;
            bool fullHealthLuck = items.ModifyDamage(20) == 40;
            owner.CurrentHealth--;
            UnityEngine.Random.state = mealRoll;
            bool injuredLuck = items.ModifyDamage(20) == 20;
            owner.CurrentHealth = owner.MaxHealth;
            UnityEngine.Random.State stackedRoll;
            do
            {
                stackedRoll = UnityEngine.Random.state;
            } while (UnityEngine.Random.value < .1f || UnityEngine.Random.value < .1f ||
                UnityEngine.Random.value < .1f || UnityEngine.Random.value < .1f || UnityEngine.Random.value >= .1f);
            items.Grant(meal);
            UnityEngine.Random.state = stackedRoll;
            bool stackedLuck = items.ModifyDamage(20) == 40;
            bool removedExtraMeal = items.Remove(meal);
            UnityEngine.Random.state = stackedRoll;
            bool reducedLuck = items.ModifyDamage(20) == 20;
            if (stackedLuck && removedExtraMeal && reducedLuck)
                Debug.Log("RAIN_COOP_ULTIMATE_MEAL_STACKS_CHECKED");
            else Debug.LogError("RAIN_COOP_ULTIMATE_MEAL_STACKS");
            bool crafted = items.Remove(meal) && !items.Remove(meal) && !items.Remove(clover) && player.RainGold == 0;
            UnityEngine.Random.state = mealRoll;
            bool removedLuck = items.ModifyDamage(20) == 20;
            items.Remove(glasses);
            owner.CurrentHealth = originalHealth;
            UnityEngine.Random.state = originalRandom;
            if (fullHealthLuck && injuredLuck && removedLuck)
                Debug.Log("RAIN_COOP_ULTIMATE_MEAL_LUCK_CHECKED");
            else Debug.LogError($"RAIN_COOP_ULTIMATE_MEAL_LUCK full={fullHealthLuck} injured={injuredLuck} removed={removedLuck}");
            owner.transform.position = originalPosition;
            if (networkTransform)
            {
                networkTransform.RpcTeleport(originalPosition);
                networkTransform.syncDirection = direction;
                networkTransform.ResetState();
            }
            motor.enabled = motorEnabled;
            player.RainGold = originalGold;
            if (refusedEmpty && crafted) Debug.Log("RAIN_COOP_ULTIMATE_MEAL_COOKING_CHECKED");
            else Debug.LogError("RAIN_COOP_ULTIMATE_MEAL_COOKING");

            // Keep this timed server recipe fixture authoritative until the transaction finishes.
            motor.enabled = false;
            if (networkTransform) networkTransform.syncDirection = SyncDirection.ServerToClient;
            try
            {
                int mixedStation = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                    .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "UltimateMealFrostRelic");
                if (!mode.TryGetRainMapNode(mixedStation, false, out var mixedPosition))
                {
                    Debug.LogError("RAIN_COOP_ULTIMATE_MEAL_FROST_STATION");
                    yield break;
                }
                int relic = RangeResearchItems.CatalogIndex("Icicle");
                items.Grant(clover);
                owner.transform.position = mixedPosition;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(mixedPosition);
                    networkTransform.ResetState();
                }
                mode.BuyRainChest(player, mixedStation);
                bool missingRelic = owner.ResearchClovers == 1 && owner.ResearchUltimateMeals == 0;
                if (!VerifyPassiveItemChest(mode, player, "Icicle")) yield break;
                items.OnKill(mixedPosition, true);
                float relicDeadline = Time.time + 2f;
                yield return new WaitUntil(() => owner.ResearchFrostRelicRadius > 0f || Time.time >= relicDeadline);
                bool activeRelic = owner.ResearchFrostRelicRadius > 0f;
                owner.transform.position = mixedPosition;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(mixedPosition);
                    networkTransform.ResetState();
                }
                int nearestMixedStation = mode.NearbyRainChest(owner);
                mode.BuyRainChest(player, mixedStation);
                mode.BuyRainChest(player, mixedStation);
                int cloversAfterMixed = owner.ResearchClovers;
                int mealsAfterMixed = owner.ResearchUltimateMeals;
                int goldAfterMixed = player.RainGold;
                bool relicLeft = items.Remove(relic);
                bool mealRemoved = items.Remove(meal);
                bool mixedMeal = cloversAfterMixed == 0 && mealsAfterMixed == 1 &&
                    !relicLeft && mealRemoved && goldAfterMixed == originalGold;
                owner.transform.position = originalPosition;
                if (missingRelic && activeRelic && mixedMeal && owner.ResearchFrostRelicRadius == 0f)
                    Debug.Log("RAIN_COOP_ULTIMATE_MEAL_FROST_RECIPE_CHECKED");
                else Debug.LogError($"RAIN_COOP_ULTIMATE_MEAL_FROST_RECIPE missing={missingRelic} active={activeRelic} crafted={mixedMeal} radius={owner.ResearchFrostRelicRadius} nearest={nearestMixedStation} expected={mixedStation} clovers={cloversAfterMixed} meals={mealsAfterMixed} relicLeft={relicLeft} mealRemoved={mealRemoved} gold={goldAfterMixed} expectedGold={originalGold}");
            }
            finally
            {
                if (owner) owner.transform.position = originalPosition;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(originalPosition);
                    networkTransform.syncDirection = direction;
                    networkTransform.ResetState();
                }
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static IEnumerator VerifyRemoteCooking()
        {
            PlayerInstance local = null;
            float deadline = Time.realtimeSinceStartup + 12f;
            yield return new WaitUntil(() =>
            {
                if (NetworkClient.localPlayer) local = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
                return (local && local.MyCharacter && local.RainSteak == 2) ||
                    Time.realtimeSinceStartup >= deadline;
            });
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "CookedSteak");
            if (!local || !local.MyCharacter || local.RainSteak != 2 ||
                GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                !mode.TryGetRainMapNode(station, false, out var position))
            {
                Debug.LogError("RAIN_CLIENT_COOKING_STATION_NOT_READY");
                yield break;
            }

            var owner = local.MyCharacter;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            int ingredientMaximum = owner.MaxHealth;
            try
            {
                // The server arranges its own fixture. Put the locally controlled character at the node for E selection.
                motor.enabled = false;
                owner.transform.position = position;
                Physics.SyncTransforms();
                bool submitted = local.TryInteractRainNode() && local.TryInteractRainNode();
                deadline = Time.realtimeSinceStartup + 5f;
                yield return new WaitUntil(() => (local.RainSteak == 0 && owner.MaxHealth > ingredientMaximum && CookedSteakHudVisible()) ||
                    Time.realtimeSinceStartup >= deadline);
                if (submitted && local.RainSteak == 0 && owner.MaxHealth > ingredientMaximum && local.RainGold == 0 &&
                    owner.ResearchCookedSteaks == 1 && CookedSteakHudVisible())
                    Debug.Log("RAIN_CLIENT_COOKING_STATION_CHECKED");
                else Debug.LogError($"RAIN_CLIENT_COOKING_STATION submitted={submitted} steak={local.RainSteak} max={owner.MaxHealth} before={ingredientMaximum}");
            }
            finally
            {
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static bool CookedSteakHudVisible()
        {
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            var icon = overlay ? overlay.Find("Combat inventory/Inventory item 44") : null;
            return icon && icon.gameObject.activeInHierarchy &&
                icon.GetComponent<UnityEngine.UI.Image>().sprite?.name == "texCookedSteakIcon";
        }

        static IEnumerator VerifyRemoteUltimateMeal()
        {
            PlayerInstance local = null;
            float deadline = Time.realtimeSinceStartup + 10f;
            yield return new WaitUntil(() =>
            {
                if (NetworkClient.localPlayer) local = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
                return (local && local.MyCharacter && local.MyCharacter.ResearchClovers == 2) ||
                    Time.realtimeSinceStartup >= deadline;
            });
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "UltimateMeal");
            if (!local || !local.MyCharacter || local.MyCharacter.ResearchClovers != 2 ||
                GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                !mode.TryGetRainMapNode(station, false, out var position))
            {
                Debug.LogError("RAIN_CLIENT_ULTIMATE_MEAL_NOT_READY");
                yield break;
            }

            var owner = local.MyCharacter;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            try
            {
                motor.enabled = false;
                owner.transform.position = position;
                Physics.SyncTransforms();
                bool submitted = local.TryInteractRainNode() && local.TryInteractRainNode();
                deadline = Time.realtimeSinceStartup + 5f;
                yield return new WaitUntil(() => (owner.ResearchClovers == 0 && UltimateMealHudVisible()) ||
                    Time.realtimeSinceStartup >= deadline);
                if (submitted && owner.ResearchClovers == 0 && owner.ResearchUltimateMeals == 1 &&
                    local.RainGold == 0 && UltimateMealHudVisible())
                    Debug.Log("RAIN_CLIENT_ULTIMATE_MEAL_INTERACTION_CHECKED");
                else Debug.LogError($"RAIN_CLIENT_ULTIMATE_MEAL_INTERACTION submitted={submitted} clovers={owner.ResearchClovers} meals={owner.ResearchUltimateMeals}");
            }
            finally
            {
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static bool UltimateMealHudVisible()
        {
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            var icon = overlay ? overlay.Find("Combat inventory/Inventory item 45") : null;
            return icon && icon.gameObject.activeInHierarchy &&
                icon.GetComponent<UnityEngine.UI.Image>().sprite?.name == "texUltimateMealIcon";
        }

        static IEnumerator VerifyDroneBackupClient()
        {
            PlayerInstance local = null;
            float hexDeadline = Time.realtimeSinceStartup + 12f;
            yield return new WaitUntil(() => HexHudShowsStacks() || Time.realtimeSinceStartup >= hexDeadline);
            if (HexHudShowsStacks()) Debug.Log("RAIN_CLIENT_HEX_INVENTORY_CHECKED");
            else Debug.LogError("RAIN_CLIENT_HEX_INVENTORY");
            yield return VerifyRemoteUltimateMeal();
            yield return VerifyRemoteCooking();
            if (UltimateMealHudVisible()) Debug.LogError("RAIN_CLIENT_ULTIMATE_MEAL_HUD_REMOVE");
            else Debug.Log("RAIN_CLIENT_ULTIMATE_MEAL_HUD_REMOVE_CHECKED");
            float deadline = Time.realtimeSinceStartup + 16f;
            yield return new WaitUntil(() =>
            {
                if (NetworkClient.localPlayer)
                    local = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
                return (local && local.MyCharacter && local.RainEquipment == RangeResearchItems.Equipment.DroneBackup) ||
                    Time.realtimeSinceStartup >= deadline;
            });
            if (!local || !local.MyCharacter || local.RainEquipment != RangeResearchItems.Equipment.DroneBackup)
            {
                Debug.LogError("RAIN_CLIENT_DRONE_BACKUP_EQUIPMENT_SYNC");
                yield break;
            }
            var owner = local.MyCharacter;
            var motor = owner.GetComponent<CharacterMotor>();
            deadline = Time.realtimeSinceStartup + 4f;
            yield return new WaitUntil(() => (owner && owner.ResearchCookedSteaks == 0 && !CookedSteakHudVisible()) ||
                Time.realtimeSinceStartup >= deadline);
            if (owner.ResearchCookedSteaks != 0 || CookedSteakHudVisible())
                Debug.LogError("RAIN_CLIENT_COOKED_STEAK_REMOVE_SYNC");
            else Debug.Log("RAIN_CLIENT_COOKED_STEAK_REMOVE_SYNC_CHECKED");
            if (owner.ResearchMochas == 1 && Mathf.Approximately(owner.ResearchAttackSpeed, 1.075f) &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.07f / 2f))
                Debug.Log("RAIN_CLIENT_MOCHA_RESPAWN_SYNC_CHECKED");
            else Debug.LogError("RAIN_CLIENT_MOCHA_RESPAWN_SYNC");
            int stoneMax = owner.MaxHealth;
            if (owner.ResearchStoneFlux != 1 || stoneMax <= 0)
                Debug.LogError("RAIN_CLIENT_STONE_FLUX_RESPAWN_SYNC");
            uint ownerId = owner.netId;
            local.RequestUseRangeEquipment();
            local.RequestUseRangeEquipment();
            deadline = Time.realtimeSinceStartup + 10f;
            yield return new WaitUntil(() => CountBackupDrones(NetworkClient.spawned.Values, ownerId) >= 4 ||
                Time.realtimeSinceStartup >= deadline);
            if (CountBackupDrones(NetworkClient.spawned.Values, ownerId) != 4)
            {
                Debug.LogError("RAIN_CLIENT_DRONE_BACKUP_SPAWN_SYNC");
                yield break;
            }
            Debug.Log("RAIN_CLIENT_DRONE_BACKUP_SPAWN_SYNC_CHECKED");
            deadline = Time.realtimeSinceStartup + 5f;
            yield return new WaitUntil(() => (owner && owner.ResearchStoneFlux == 0 && owner.MaxHealth * 2 == stoneMax) ||
                Time.realtimeSinceStartup >= deadline);
            if (owner && owner.ResearchStoneFlux == 0 && owner.MaxHealth * 2 == stoneMax &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed * 1.07f))
                Debug.Log("RAIN_CLIENT_STONE_FLUX_REMOVE_SYNC_CHECKED");
            else Debug.LogError($"RAIN_CLIENT_STONE_FLUX_REMOVE_SYNC before={stoneMax} after={owner.MaxHealth} stacks={owner.ResearchStoneFlux} speed={motor.GetMoveSpeed(false)}");
            deadline = Time.realtimeSinceStartup + 30f;
            yield return new WaitUntil(() => CountBackupDrones(NetworkClient.spawned.Values, ownerId) == 0 ||
                Time.realtimeSinceStartup >= deadline);
            if (NetworkClient.isConnected && CountBackupDrones(NetworkClient.spawned.Values, ownerId) == 0)
                Debug.Log("RAIN_CLIENT_DRONE_BACKUP_REMOVAL_SYNC_CHECKED");
            else Debug.LogError("RAIN_CLIENT_DRONE_BACKUP_REMOVAL_SYNC");
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => (owner && owner.ResearchMochas == 0 &&
                owner.ResearchCookedSteaks == 1 && CookedSteakHudVisible()) || Time.realtimeSinceStartup >= deadline);
            if (owner && owner.ResearchMochas == 0 && owner.ResearchCookedSteaks == 1 &&
                local.RainSteak == 0 && CookedSteakHudVisible() &&
                Mathf.Approximately(owner.ResearchAttackSpeed, 1f) &&
                Mathf.Approximately(motor.GetMoveSpeed(false), motor.WalkSpeed))
                Debug.Log("RAIN_CLIENT_MOCHA_STEAK_SYNC_CHECKED");
            else Debug.LogError($"RAIN_CLIENT_MOCHA_STEAK_SYNC mocha={owner.ResearchMochas} cooked={owner.ResearchCookedSteaks} raw={local.RainSteak} icon={CookedSteakHudVisible()}");
            yield return VerifyStewClient(local, owner);
            yield return VerifyWhiteScrapClient(local, owner);
            yield return VerifyQuickFixClient(local, owner);
            yield return VerifyHikingBootsInventoryClient(owner);
            yield return VerifyScrapperSelectionClient(local, owner);
            yield return VerifyLanternInventoryClient(owner);
            yield return VerifyAntlersInventoryClient(owner);
        }

        static IEnumerator VerifyLanternInventoryHost(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            var owner = player.MyCharacter;
            int lantern = RangeResearchItems.CatalogIndex("AttackSpeedPerNearbyAllyOrEnemy");
            Vector3 originalPosition = owner.transform.position;
            var targets = new CharacterInstance[5];
            var motor = owner.GetComponent<CharacterMotor>();
            var networkTransform = owner.GetComponent<NetworkTransformBase>();
            bool motorEnabled = motor.enabled;
            var direction = networkTransform ? networkTransform.syncDirection : SyncDirection.ServerToClient;
            try
            {
                motor.enabled = false;
                if (networkTransform) networkTransform.syncDirection = SyncDirection.ServerToClient;
                owner.transform.position += Vector3.up * 200f;
                bool purchased = VerifyPassiveItemChest(mode, player, "AttackSpeedPerNearbyAllyOrEnemy") && owner.ResearchLanterns == 1;
                if (networkTransform)
                {
                    networkTransform.RpcTeleport(owner.transform.position);
                    networkTransform.ResetState();
                }
                Vector3 center = owner.transform.position + owner.centerPosition;
                for (int i = 0; i < targets.Length; i++)
                {
                    var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        center + Vector3.right * (i + 1), Quaternion.identity).GetComponent<CharacterInstance>();
                    targets[i] = target;
                    foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    target.Team = i % 2 == 0 ? owner.Team : owner.Team == 0 ? 1 : 0;
                    target.MaxHealth = target.CurrentHealth = 1000;
                    target.RangeInvulnerable = true;
                    NetworkServer.Spawn(target.gameObject);
                    target.GetComponent<CharacterMotor>().enabled = false;
                    var ai = target.GetComponent<BotAI>();
                    if (ai) { ai.enabled = false; ai.StopAllCoroutines(); }
                }
                yield return null;
                items.RefreshLanternBodies();
                bool one = owner.ResearchLanternBodies == 3;
                yield return new WaitForSeconds(2f);
                items.Grant(lantern);
                player.RecordRainChestItems(items);
                int secondBodies = owner.ResearchLanternBodies;
                bool two = owner.ResearchLanterns == 2 && secondBodies == 4;
                yield return new WaitForSeconds(2f);
                foreach (var target in targets) target.transform.position += Vector3.up * 50f;
                items.RefreshLanternBodies();
                int awayBodies = owner.ResearchLanternBodies;
                bool away = awayBodies == 0;
                yield return new WaitForSeconds(2f);
                bool removed = items.Remove(lantern) && items.Remove(lantern) && owner.ResearchLanterns == 0;
                yield return new WaitForSeconds(2f);
                if (purchased && one && two && away && removed) Debug.Log("RAIN_COOP_LANTERN_INVENTORY_CHECKED");
                else Debug.LogError($"RAIN_COOP_LANTERN_INVENTORY purchased={purchased} one={one} two={two} away={away} removed={removed} secondBodies={secondBodies} awayBodies={awayBodies} ownerY={owner.transform.position.y} expectedY={center.y-owner.centerPosition.y}");
            }
            finally
            {
                if (owner)
                {
                    while (items.Remove(lantern)) { }
                    owner.transform.position = originalPosition;
                    if (networkTransform)
                    {
                        networkTransform.RpcTeleport(originalPosition);
                        networkTransform.syncDirection = direction;
                        networkTransform.ResetState();
                    }
                    motor.enabled = motorEnabled;
                    player.RecordRainChestItems(items);
                }
                foreach (var target in targets) if (target) NetworkServer.Destroy(target.gameObject);
            }
        }

        static IEnumerator VerifyLanternInventoryClient(CharacterInstance owner)
        {
            bool One() => owner && owner.ResearchLanterns == 1 && owner.ResearchLanternBodies == 3;
            float deadline = Time.realtimeSinceStartup + 12f;
            yield return new WaitUntil(() => One() || !owner || Time.realtimeSinceStartup >= deadline);
            yield return new WaitForSeconds(.3f);
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            var iconSlot = overlay ? overlay.Find("Combat inventory/Inventory item 51") : null;
            var expectedIcon = Resources.Load<Sprite>("RainImported/LogIcons/texRageCrystalIcon");
            bool iconMatches = false;
            if (iconSlot && expectedIcon)
                foreach (var image in iconSlot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (image.sprite == expectedIcon) { iconMatches = true; break; }
            bool one = One() && HasCount(1) && iconMatches;
            float firstSpeed = owner ? owner.ResearchAttackSpeed : 0f;
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => !owner || owner.ResearchLanterns == 2 || Time.realtimeSinceStartup >= deadline);
            yield return new WaitForSeconds(.3f); // Allow the HUD refresh interval to elapse.
            bool two = owner && owner.ResearchLanternBodies == 4 && HasCount(2) &&
                Mathf.Approximately(owner.ResearchAttackSpeed - firstSpeed, .34f);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => !owner || owner.ResearchLanternBodies == 0 || Time.realtimeSinceStartup >= deadline);
            bool away = owner && owner.ResearchLanterns == 2 &&
                Mathf.Approximately(firstSpeed - owner.ResearchAttackSpeed, .6f);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => !owner || owner.ResearchLanterns == 0 || Time.realtimeSinceStartup >= deadline);
            yield return new WaitForSeconds(.3f);
            bool removed = owner && HasCount(0) && owner.ResearchLanternBodies == 0;
            if (one && two && away && removed) Debug.Log("RAIN_CLIENT_LANTERN_INVENTORY_CHECKED");
            else Debug.LogError($"RAIN_CLIENT_LANTERN_INVENTORY one={one} two={two} away={away} removed={removed}");

            bool HasCount(int count)
            {
                var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
                var slot = overlay ? overlay.Find("Combat inventory/Inventory item 51") : null;
                var text = slot ? slot.GetComponentInChildren<TMPro.TMP_Text>() : null;
                return slot && slot.gameObject.activeSelf == (count > 0) &&
                    (count == 0 || (text && text.text == (count > 1 ? count.ToString() : "")));
            }
        }

        static IEnumerator VerifyScrapperSelectionHost(Gamemodes.RangeGamemode mode, PlayerInstance player, RangeResearchItems items)
        {
            int node = WhiteStationIndex("ScrapBisonSteak");
            if (!mode.TryGetRainMapNode(node, false, out var position))
            {
                Debug.LogError("RAIN_COOP_SCRAPPER_SELECTION_NODE");
                yield break;
            }
            var owner = player.MyCharacter;
            Vector3 originalPosition = owner.transform.position;
            int originalHooves = items.Hooves;
            int originalScrap = owner.ResearchWhiteScrap;
            int scrap = RangeResearchItems.CatalogIndex("ScrapWhite");
            int gold = player.RainGold;
            try
            {
                while (items.Remove(13)) { }
                while (items.Remove(scrap)) { }
                for (int i = 0; i < 12; i++) items.Grant(13);
                player.RecordRainChestItems(items);
                owner.transform.position = position;
                bool invalidRejected = player.ServerScrapRainSelection(node, owner.netId, -1) == 0 &&
                    player.ServerScrapRainSelection(node, owner.netId + 1, 13) == 0 && items.Hooves == 12;
                owner.transform.position = position + Vector3.up * 10f;
                bool distanceRejected = player.ServerScrapRainSelection(node, owner.netId, 13) == 0 && items.Hooves == 12;
                owner.transform.position = position;
                int health = owner.CurrentHealth;
                owner.CurrentHealth = 0;
                bool deadRejected = player.ServerScrapRainSelection(node, owner.netId, 13) == 0 && items.Hooves == 12;
                owner.CurrentHealth = health;
                float deadline = Time.realtimeSinceStartup + 30f;
                yield return new WaitUntil(() => !owner || owner.ResearchWhiteScrap >= 10 || Time.realtimeSinceStartup >= deadline);
                bool first = owner && items.Hooves == 2 && owner.ResearchWhiteScrap == 10;
                deadline = Time.realtimeSinceStartup + 10f;
                yield return new WaitUntil(() => !owner || owner.ResearchWhiteScrap == 12 || Time.realtimeSinceStartup >= deadline);
                bool second = owner && items.Hooves == 0 && owner.ResearchWhiteScrap == 12 && player.RainGold == gold;
                if (first && second && invalidRejected && distanceRejected && deadRejected)
                    Debug.Log("RAIN_COOP_SCRAPPER_SELECTION_CHECKED");
                else Debug.LogError($"RAIN_COOP_SCRAPPER_SELECTION first={first} second={second} invalid={invalidRejected} distance={distanceRejected} dead={deadRejected}");
                yield return new WaitForSeconds(2f);
            }
            finally
            {
                if (owner)
                {
                    while (items.Remove(13)) { }
                    while (items.Remove(scrap)) { }
                    for (int i = 0; i < originalHooves; i++) items.Grant(13);
                    for (int i = 0; i < originalScrap; i++) items.Grant(scrap);
                    owner.transform.position = originalPosition;
                    player.RecordRainChestItems(items);
                }
            }
        }

        static IEnumerator VerifyScrapperSelectionClient(PlayerInstance player, CharacterInstance owner)
        {
            if (GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                !mode.TryGetRainMapNode(WhiteStationIndex("ScrapBisonSteak"), false, out var position))
            {
                Debug.LogError("RAIN_CLIENT_SCRAPPER_SELECTION_NODE");
                yield break;
            }
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            Vector3 originalPosition = owner.transform.position;
            bool first = false, second = false;
            try
            {
                motor.enabled = false;
                owner.transform.position = position;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(3f);
                for (int pass = 0; pass < 2; pass++)
                {
                    float deadline = Time.realtimeSinceStartup + 8f;
                    bool offered = false;
                    bool requested = false, sawMenu = false;
                    string lastLabels = "";
                    while (owner && Time.realtimeSinceStartup < deadline)
                    {
                        var menu = UI.HUD.RainScrapperMenu.Current;
                        if (menu)
                        {
                            sawMenu = true;
                            lastLabels = string.Join(" | ", menu.GetComponentsInChildren<TMPro.TMP_Text>().Select(label => label.text));
                            int expected = pass == 0 ? 12 : 2;
                            offered = menu.GetComponentsInChildren<TMPro.TMP_Text>().Any(label =>
                                label.text.Contains("保罗的山羊蹄 ×" + expected));
                            if (offered) break;
                            menu.Close();
                        }
                        requested |= player.TryOpenRainScrapper();
                        yield return new WaitForSeconds(.2f);
                    }
                    if (!offered)
                        Debug.LogWarning($"RAIN_CLIENT_SCRAPPER_OFFER_DIAGNOSTIC pass={pass} requested={requested} sawMenu={sawMenu} focused={Application.isFocused} input={PlayerGameplayInput.Instance.enabled} gameplay={UI.ClientFrontend.GamePlayInput()} node={mode.NearbyRainChest(owner)} expectedNode={WhiteStationIndex("ScrapBisonSteak")} health={owner.CurrentHealth} labels={lastLabels}");
                    if (pass == 0 && offered) yield return VerifyScrapperMenuLifecycle(player, owner, position);
                    var selection = UI.HUD.RainScrapperMenu.Current;
                    bool submitted = offered && selection && selection.Select(13);
                    deadline = Time.realtimeSinceStartup + 6f;
                    int expectedScrap = pass == 0 ? 10 : 12;
                    yield return new WaitUntil(() => !owner || owner.ResearchWhiteScrap == expectedScrap || Time.realtimeSinceStartup >= deadline);
                    bool passed = submitted && owner && owner.ResearchWhiteScrap == expectedScrap &&
                        !UI.HUD.RainScrapperMenu.Current && PlayerGameplayInput.Instance.enabled;
                    if (pass == 0) first = passed;
                    else second = passed;
                    yield return new WaitForSeconds(.5f);
                }
                if (first && second) Debug.Log("RAIN_CLIENT_SCRAPPER_SELECTION_CHECKED");
                else Debug.LogError($"RAIN_CLIENT_SCRAPPER_SELECTION first={first} second={second}");
            }
            finally
            {
                if (UI.HUD.RainScrapperMenu.Current) UI.HUD.RainScrapperMenu.Current.Close();
                if (owner) owner.transform.position = originalPosition;
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static IEnumerator OpenScrapperMenuForSmoke(PlayerInstance player, CharacterInstance owner)
        {
            float deadline = Time.realtimeSinceStartup + 5f;
            while (owner && !UI.HUD.RainScrapperMenu.Current && Time.realtimeSinceStartup < deadline)
            {
                player.TryOpenRainScrapper();
                yield return new WaitForSeconds(.2f);
            }
        }

        static IEnumerator VerifyScrapperMenuLifecycle(PlayerInstance player, CharacterInstance owner, Vector3 position)
        {
            var menu = UI.HUD.RainScrapperMenu.Current;
            if (!menu) { Debug.LogError("RAIN_CLIENT_SCRAPPER_LIFECYCLE_NOT_OPEN"); yield break; }
            string screenshot = System.IO.Path.Combine(System.IO.Path.GetTempPath(),
                "TPSRainItems-ScrapperMenu-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".png");
            ScreenCapture.CaptureScreenshot(screenshot);
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(.2f);
            Debug.Log("RAIN_CLIENT_SCRAPPER_SCREENSHOT=" + screenshot);
            menu.Close();
            bool cancelled = !UI.HUD.RainScrapperMenu.Current && PlayerGameplayInput.Instance.enabled && UI.ClientFrontend.GamePlayInput();
            yield return OpenScrapperMenuForSmoke(player, owner);
            menu = UI.HUD.RainScrapperMenu.Current;
            bool openedForAway = menu;
            owner.transform.position = position + Vector3.up * 10f;
            yield return null;
            yield return null;
            bool away = openedForAway && !UI.HUD.RainScrapperMenu.Current && !menu && PlayerGameplayInput.Instance.enabled;
            owner.transform.position = position;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.3f);
            yield return OpenScrapperMenuForSmoke(player, owner);
            menu = UI.HUD.RainScrapperMenu.Current;
            bool openedForDisable = menu;
            if (menu) menu.gameObject.SetActive(false);
            yield return null;
            bool disabled = openedForDisable && !UI.HUD.RainScrapperMenu.Current && !menu && PlayerGameplayInput.Instance.enabled && UI.ClientFrontend.GamePlayInput();
            yield return OpenScrapperMenuForSmoke(player, owner);
            menu = UI.HUD.RainScrapperMenu.Current;
            bool openedForDeath = menu;
            int health = owner.CurrentHealth;
            try
            {
                owner.CurrentHealth = 0;
                yield return null;
                yield return null;
            }
            finally { if (owner) owner.CurrentHealth = health; }
            bool dead = openedForDeath && !UI.HUD.RainScrapperMenu.Current && !menu && PlayerGameplayInput.Instance.enabled && UI.ClientFrontend.GamePlayInput();
            if (cancelled && away && disabled && dead) Debug.Log("RAIN_CLIENT_SCRAPPER_LIFECYCLE_CHECKED");
            else Debug.LogError($"RAIN_CLIENT_SCRAPPER_LIFECYCLE cancel={cancelled} away={away} disable={disabled} dead={dead}");
            yield return OpenScrapperMenuForSmoke(player, owner);
        }

        static IEnumerator VerifyHikingBootsInventoryClient(CharacterInstance owner)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            yield return new WaitUntil(() => HasStack(1) || !owner || Time.realtimeSinceStartup >= deadline);
            bool purchased = HasStack(1);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => HasStack(2) || !owner || Time.realtimeSinceStartup >= deadline);
            bool stacked = HasStack(2);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => HasStack(1) || !owner || Time.realtimeSinceStartup >= deadline);
            bool removedOne = HasStack(1);
            deadline = Time.realtimeSinceStartup + 6f;
            yield return new WaitUntil(() => !owner ||
                (owner.ResearchHikingBoots == 0 && !InventoryIconVisible(48, "texCritAtLowerElevationIcon")) ||
                Time.realtimeSinceStartup >= deadline);
            bool removedLast = owner && owner.ResearchHikingBoots == 0 && !InventoryIconVisible(48, "texCritAtLowerElevationIcon");
            if (purchased && stacked && removedOne && removedLast)
                Debug.Log("RAIN_CLIENT_HIKING_BOOTS_INVENTORY_CHECKED");
            else Debug.LogError($"RAIN_CLIENT_HIKING_BOOTS_INVENTORY purchase={purchased} stacked={stacked} one={removedOne} last={removedLast}");

            bool HasStack(int count)
            {
                if (!owner || owner.ResearchHikingBoots != count || !InventoryIconVisible(48, "texCritAtLowerElevationIcon")) return false;
                var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
                var slot = overlay ? overlay.Find("Combat inventory/Inventory item 48") : null;
                var label = slot ? slot.GetComponentInChildren<TMPro.TMP_Text>() : null;
                return label && label.text == (count > 1 ? count.ToString() : "");
            }
        }

        static IEnumerator VerifyWhiteScrapClient(PlayerInstance player, CharacterInstance owner)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            yield return new WaitUntil(() => (owner && player.RainSteak == 1) || Time.realtimeSinceStartup >= deadline);
            if (!owner || player.RainSteak != 1 || GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                !mode.TryGetRainMapNode(WhiteStationIndex("ScrapBisonSteak"), false, out var scrapPosition) ||
                !mode.TryGetRainMapNode(WhiteStationIndex("PrintSyringe"), false, out var printPosition))
            {
                Debug.LogError("RAIN_CLIENT_REMOTE_WHITE_SCRAP_NOT_READY");
                yield break;
            }

            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            try
            {
                motor.enabled = false;
                owner.transform.position = scrapPosition;
                Physics.SyncTransforms();
                bool submittedScrap = player.TryInteractRainNode() && player.TryInteractRainNode();
                deadline = Time.realtimeSinceStartup + 6f;
                yield return new WaitUntil(() => (owner && owner.ResearchWhiteScrap == 1 &&
                    InventoryIconVisible(50, "texScrapWhiteIcon")) || Time.realtimeSinceStartup >= deadline);
                if (!submittedScrap || !owner || owner.ResearchWhiteScrap != 1 || player.RainSteak != 0 ||
                    !InventoryIconVisible(50, "texScrapWhiteIcon"))
                {
                    Debug.LogError("RAIN_CLIENT_REMOTE_WHITE_SCRAP_CONVERSION");
                    yield break;
                }

                deadline = Time.realtimeSinceStartup + 4f;
                yield return new WaitUntil(() => !owner ||
                    (owner.transform.position - printPosition).sqrMagnitude < 1f || Time.realtimeSinceStartup >= deadline);
                if (!owner || (owner.transform.position - printPosition).sqrMagnitude >= 1f)
                {
                    Debug.LogError("RAIN_CLIENT_REMOTE_WHITE_SCRAP_PRINTER_POSITION");
                    yield break;
                }
                owner.transform.position = printPosition;
                Physics.SyncTransforms();
                bool submittedPrint = player.TryInteractRainNode();
                deadline = Time.realtimeSinceStartup + 6f;
                yield return new WaitUntil(() => (owner && owner.ResearchSyringes == 1 &&
                    !InventoryIconVisible(50, "texScrapWhiteIcon")) || Time.realtimeSinceStartup >= deadline);
                if (submittedPrint && owner && owner.ResearchSyringes == 1 && owner.ResearchWhiteScrap == 0 &&
                    !InventoryIconVisible(50, "texScrapWhiteIcon"))
                    Debug.Log("RAIN_CLIENT_REMOTE_WHITE_SCRAP_STATIONS_CHECKED");
                else Debug.LogError("RAIN_CLIENT_REMOTE_WHITE_SCRAP_PRINT");
            }
            finally
            {
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static IEnumerator VerifyQuickFixClient(PlayerInstance player, CharacterInstance owner)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            yield return new WaitUntil(() => (owner && owner.ResearchWhiteScrap == 1 &&
                owner.ResearchMochas == 1 && InventoryIconVisible(50, "texScrapWhiteIcon")) || Time.realtimeSinceStartup >= deadline);
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "BonusHealthBoost");
            if (!owner || owner.ResearchWhiteScrap != 1 || owner.ResearchMochas != 1 ||
                !InventoryIconVisible(50, "texScrapWhiteIcon") ||
                GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                !mode.TryGetRainMapNode(station, false, out var position))
            {
                Debug.LogError("RAIN_CLIENT_QUICK_FIX_NOT_READY");
                yield break;
            }

            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            try
            {
                motor.enabled = false;
                owner.transform.position = position;
                Physics.SyncTransforms();
                bool submitted = player.TryInteractRainNode() && player.TryInteractRainNode();
                deadline = Time.realtimeSinceStartup + 6f;
                yield return new WaitUntil(() => (owner && owner.ResearchQuickFixes == 1 &&
                    InventoryIconVisible(49, "texQuickFixIcon")) || Time.realtimeSinceStartup >= deadline);
                bool cooked = submitted && owner && owner.ResearchQuickFixes == 1 &&
                    owner.ResearchWhiteScrap == 0 && owner.ResearchMochas == 0 &&
                    InventoryIconVisible(49, "texQuickFixIcon") && !InventoryIconVisible(50, "texScrapWhiteIcon");
                deadline = Time.realtimeSinceStartup + 6f;
                yield return new WaitUntil(() => (owner && owner.ResearchQuickFixes == 0 &&
                    !InventoryIconVisible(49, "texQuickFixIcon")) || Time.realtimeSinceStartup >= deadline);
                if (cooked && owner && owner.ResearchQuickFixes == 0 && !InventoryIconVisible(49, "texQuickFixIcon"))
                    Debug.Log("RAIN_CLIENT_QUICK_FIX_REMOTE_INTERACTION_CHECKED");
                else Debug.LogError($"RAIN_CLIENT_QUICK_FIX_REMOTE_INTERACTION submitted={submitted} cooked={cooked}");
            }
            finally
            {
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static IEnumerator VerifyStewClient(PlayerInstance player, CharacterInstance owner)
        {
            float deadline = Time.realtimeSinceStartup + 8f;
            yield return new WaitUntil(() => (owner && owner.ResearchLeptonDaisies == 1 &&
                owner.ResearchSeeds == 1 && InventoryIconVisible(47, "texGlowFlowerIcon")) || Time.realtimeSinceStartup >= deadline);
            int station = Enumerable.Range(0, Gamemodes.RangeGamemode.RainChestCount)
                .Single(index => Gamemodes.RangeGamemode.RainChestRewardId(index) == "Stew");
            if (!owner || owner.ResearchLeptonDaisies != 1 || owner.ResearchSeeds != 1 ||
                !InventoryIconVisible(47, "texGlowFlowerIcon") ||
                GameManager.Gamemode is not Gamemodes.RangeGamemode mode ||
                !mode.TryGetRainMapNode(station, false, out var position))
            {
                Debug.LogError($"RAIN_CLIENT_STEW_NOT_READY daisy={(owner ? owner.ResearchLeptonDaisies : -1)} seed={(owner ? owner.ResearchSeeds : -1)} icon={InventoryIconVisible(47, "texGlowFlowerIcon")}");
                yield break;
            }

            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            try
            {
                motor.enabled = false;
                owner.transform.position = position;
                Physics.SyncTransforms();
                bool submitted = player.TryInteractRainNode() && player.TryInteractRainNode();
                deadline = Time.realtimeSinceStartup + 5f;
                yield return new WaitUntil(() => (owner && owner.ResearchStews == 1 &&
                    InventoryIconVisible(46, "texStewIcon")) || Time.realtimeSinceStartup >= deadline);
                if (submitted && owner && owner.ResearchStews == 1 && owner.ResearchLeptonDaisies == 0 &&
                    owner.ResearchSeeds == 0 && InventoryIconVisible(46, "texStewIcon") &&
                    !InventoryIconVisible(47, "texGlowFlowerIcon"))
                    Debug.Log("RAIN_CLIENT_STEW_TRANSACTION_CHECKED");
                else Debug.LogError("RAIN_CLIENT_STEW_TRANSACTION");

                deadline = Time.realtimeSinceStartup + 6f;
                yield return new WaitUntil(() => (owner && owner.ResearchStews == 0 &&
                    !InventoryIconVisible(46, "texStewIcon")) || Time.realtimeSinceStartup >= deadline);
                if (owner && owner.ResearchStews == 0 && !InventoryIconVisible(46, "texStewIcon"))
                    Debug.Log("RAIN_CLIENT_STEW_REMOVE_SYNC_CHECKED");
                else Debug.LogError("RAIN_CLIENT_STEW_REMOVE_SYNC");
            }
            finally
            {
                if (motor) motor.enabled = motorEnabled;
            }
        }

        static bool InventoryIconVisible(int slot, string iconName)
        {
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            var icon = overlay ? overlay.Find("Combat inventory/Inventory item " + slot) : null;
            return icon && icon.gameObject.activeInHierarchy &&
                icon.GetComponent<UnityEngine.UI.Image>().sprite?.name == iconName;
        }

        static IEnumerator VerifyRainScannerHost(Gamemodes.RangeGamemode mode, PlayerInstance guest)
        {
            if (!VerifyEquipmentChest(mode, guest, Gamemodes.RangeGamemode.RainScannerChest,
                    RangeResearchItems.Equipment.Scanner))
                yield break;

            float deadline = Time.realtimeSinceStartup + 10f;
            while (!mode.HasRainScannerPulses && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!mode.HasRainScannerPulses || guest.ServerTryUseRangeEquipment())
            {
                Debug.LogError("RAIN_SCANNER_HOST_ACTIVATION");
                yield break;
            }
            Debug.Log("RAIN_SCANNER_HOST_ACTIVATION_CHECKED");
            double beforePause = mode.RainScannerTime;
            float previousScale = Time.timeScale;
            try
            {
                Time.timeScale = 0f;
                yield return new WaitForSecondsRealtime(.75f);
                if (mode.RainScannerTime != beforePause || !mode.HasRainScannerPulses)
                    Debug.LogError("RAIN_SCANNER_HOST_CLOCK_ADVANCED_DURING_PAUSE");
                else Debug.Log("RAIN_SCANNER_HOST_PAUSE_CHECKED");
            }
            finally
            {
                Time.timeScale = previousScale;
            }
            float expiryDeadline = Time.realtimeSinceStartup + 17f;
            while (mode.HasRainScannerPulses && Time.realtimeSinceStartup < expiryDeadline)
                yield return null;
            if (mode.HasRainScannerPulses) Debug.LogError("RAIN_SCANNER_HOST_PULSE_NOT_PRUNED");
            else Debug.Log("RAIN_SCANNER_HOST_PRUNE_CHECKED");
            yield return new WaitForSecondsRealtime(2f);
        }

        static IEnumerator VerifyRainScannerClient()
        {
            bool requested = false;
            bool sawPulse = false;
            bool sawReveal = false;
            float deadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < deadline)
            {
                var player = GameManager.myPlayerInstance;
                if (player && NetworkClient.localPlayer && GameManager.Gamemode is Gamemodes.RangeGamemode mode)
                {
                    if (!requested && player.RainEquipment == RangeResearchItems.Equipment.Scanner)
                    {
                        player.RequestUseRangeEquipment();
                        requested = true;
                    }
                    if (mode.HasRainScannerPulses)
                    {
                        sawPulse = true;
                        for (int i = 0; !sawReveal && i < Gamemodes.RangeGamemode.RainBeaconCount; i++)
                        {
                            if (mode.TryGetRainMapNode(i, true, out var beacon))
                                sawReveal = mode.IsRainScannerPointRevealed(beacon, mode.RainScannerTime);
                        }
                    }
                    else if (requested && sawPulse && sawReveal && NetworkClient.isConnected)
                    {
                        Debug.Log("RAIN_SCANNER_CLIENT_REVEAL_AND_PRUNE_CHECKED");
                        yield break;
                    }
                }
                yield return null;
            }
            Debug.LogError($"RAIN_SCANNER_CLIENT_LIFETIME requested={requested} pulse={sawPulse} reveal={sawReveal}");
        }

        IEnumerator VerifyRainClient()
        {
            var manager=DNNetworkManager.Instance;
            if(!manager || !manager.GetComponent<DNTransportImplementer>().RestoreLocalTransport())
            {
                Debug.LogError("RAIN_CLIENT_LOCAL_TRANSPORT");
                yield break;
            }
            RangeCharacterSelection.SetSelected(0);
            manager.networkAddress="127.0.0.1";
            manager.StartClient();
            if (Environment.GetCommandLineArgs().Contains("--rain-parent-impulse-coop"))
            {
                yield return VerifyParentImpulseClient();
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--rain-parent-coop-smoke"))
            {
                yield return VerifyParentReplica();
                yield break;
            }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-monster-log-smoke"))
            {
                yield return VerifyMonsterLogClient();
                yield break;
            }
#endif
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-drone-backup-only"))
            {
                yield return VerifyDroneBackupClient();
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-desk-plant-smoke"))
            {
                const string ringName = "Rain desk plant healing ring";
                float joinDeadline = Time.realtimeSinceStartup + 15f;
                yield return new WaitUntil(() => (NetworkClient.localPlayer && GameObject.Find(ringName)) ||
                    Time.realtimeSinceStartup >= joinDeadline);
                if (!NetworkClient.localPlayer || !GameObject.Find(ringName))
                {
                    Debug.LogError("RAIN_CLIENT_DESK_PLANT_LATE_JOIN_MISSING");
                    yield break;
                }
                Debug.Log("RAIN_CLIENT_DESK_PLANT_LATE_JOIN_CHECKED");
                float expiryDeadline = Time.realtimeSinceStartup + 15f;
                yield return new WaitUntil(() => !GameObject.Find(ringName) || Time.realtimeSinceStartup >= expiryDeadline);
                if (NetworkClient.isConnected && !GameObject.Find(ringName))
                    Debug.Log("RAIN_CLIENT_DESK_PLANT_REMOVAL_CHECKED");
                else Debug.LogError("RAIN_CLIENT_DESK_PLANT_REMOVAL");
                yield break;
            }
            if (Environment.GetCommandLineArgs().Contains("--rain-coop-scanner-only"))
            {
                yield return VerifyRainScannerClient();
                yield break;
            }
            bool projectileSmoke = Environment.GetCommandLineArgs().Contains("--rain-coop-projectile-smoke");
            if (projectileSmoke || Environment.GetCommandLineArgs().Contains("--rain-coop-firework-only"))
            {
                float fireworkDeadline = Time.realtimeSinceStartup + 25f;
                while (!GameObject.Find("Rain firework trail") && Time.realtimeSinceStartup < fireworkDeadline)
                    yield return null;
                if (NetworkClient.localPlayer && GameObject.Find("Rain firework trail"))
                    Debug.Log("RAIN_CLIENT_FIREWORK_TRAIL_CHECKED");
                else Debug.LogError("RAIN_CLIENT_FIREWORK_TRAIL_MISSING");
                if (projectileSmoke)
                {
                    float moltenDeadline = Time.realtimeSinceStartup + 15f;
                    while (!GameObject.Find("Rain molten ball trail") && NetworkClient.isConnected &&
                        Time.realtimeSinceStartup < moltenDeadline) yield return null;
                    if (GameObject.Find("Rain molten ball trail")) Debug.Log("RAIN_CLIENT_MOLTEN_TRAIL_CHECKED");
                    else Debug.LogError("RAIN_CLIENT_MOLTEN_TRAIL_MISSING");
                    float cleanseDeadline = Time.realtimeSinceStartup + 35f;
                    while (!GameObject.Find("Rain blast shower ring") && NetworkClient.isConnected &&
                        Time.realtimeSinceStartup < cleanseDeadline) yield return null;
                    if (GameObject.Find("Rain blast shower ring")) Debug.Log("RAIN_CLIENT_BLAST_SHOWER_RING_CHECKED");
                    else Debug.LogError("RAIN_CLIENT_BLAST_SHOWER_RING_MISSING");
                }
                float disconnectDeadline = Time.realtimeSinceStartup + 40f;
                while (NetworkClient.isConnected && Time.realtimeSinceStartup < disconnectDeadline) yield return null;
                yield break;
            }
            float deadline=Time.realtimeSinceStartup+84f;
            bool sawReplica=false, sawBossDot=false, sawBeaconDot=false;
            bool sawFist=false, sawFistRetract=false;
            bool sawQueenCast = false, sawQueenWard = false;
            bool sawGuardSlam = false, sawLemurianBite = false, sawImpSlash = false, sawBisonHeadbutt = false;
            bool sawVerminLash = false;
            bool sawFlyingVerminCue = false, sawFlyingVerminShot = false, sawFlyingVerminDeath = false;
            bool sawHermitBurrow = false, sawHermitMortar = false, sawHermitImpact = false;
            bool sawLarvaCue = false, sawLarvaFlight = false, sawLarvaLanding = false;
            bool sawLunarShard = false, sawLunarDeathCue = false, sawLunarZone = false;
            bool lunarGraphics = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            bool sawLunarImpact = !lunarGraphics;
            uint lunarId = 0;
            uint larvaLeapId = 0;
            Vector3 larvaLeapStart = default;
            bool sawBisonChargePrep = false, sawBisonChargeTravel = false, sawBisonWallStun = false;
            uint bisonChargeId = 0;
            Vector3 bisonChargeOrigin = default;
            bool sawWispCharge = false, sawWispFire = false, sawWispFlight = false, sawWispTraces = false;
            bool sawBruiserBreath = false;
            bool sawGreaterWispCharge = false, sawGreaterWispFire = false;
            bool sawGreaterWispVolley = false;
            bool sawJellyfishCue = false, sawJellyfishNova = false;
            uint observedWispId = 0;
            Vector3 firstWispPosition = default;
            bool requestedFruit = false;
            int fruitHeals = 0;
            bool sawExpectedFruitHeal = false;
            CharacterInstance fruitCharacter = null;
            void CountFruitHeal(int current, int added, uint healer)
            {
                if (healer != fruitCharacter.netId || added < fruitCharacter.MaxHealth * .25f) return;
                fruitHeals++;
                sawExpectedFruitHeal |= added == Mathf.CeilToInt(fruitCharacter.MaxHealth * .5f);
            }
            while (Time.realtimeSinceStartup<deadline)
            {
                if (GameManager.Gamemode is Gamemodes.RangeGamemode mode && mode.IsRainCoop &&
                    NetworkClient.localPlayer && NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter)
                {
                    var local = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
                    if (!requestedFruit && local.RainEquipment == RangeResearchItems.Equipment.Fruit)
                    {
                        requestedFruit = true;
                        fruitCharacter = local.MyCharacter;
                        fruitCharacter.Client_OnHealthAdded += CountFruitHeal;
                        local.RequestUseRangeEquipment();
                        local.RequestUseRangeEquipment();
                    }
                    sawReplica |= FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                        .Any(actor=>actor.GetComponent<CharacterInstance>().RainEnemyKind>0 &&
                            actor.GetComponent<RainActorVisual>());
                    if (!sawFlyingVerminDeath)
                        sawFlyingVerminDeath = GameObject.Find("VerminDeathExplosion(Clone)") != null;
                    if (!sawHermitImpact)
                        sawHermitImpact = GameObject.Find("Rain Hermit Crab mortar impact") != null;
                    if (!sawLunarImpact)
                        sawLunarImpact = GameObject.Find("Rain Lunar Exploder shard impact") != null;
                    foreach (var identity in NetworkClient.spawned.Values)
                    {
                        if (!identity || !identity.TryGetComponent<CharacterInstance>(out var enemy)) continue;
                        if (enemy.RainEnemyKind == RangeResearchEnemy.WispKind + 1)
                        {
                            if (observedWispId == 0)
                            {
                                observedWispId = enemy.netId;
                                firstWispPosition = enemy.transform.position;
                            }
                            sawWispFlight |= observedWispId == enemy.netId &&
                                (enemy.transform.position - firstWispPosition).sqrMagnitude >= .25f;
                            if (enemy.RainEnemyAnimation == 12 && enemy.RainTelegraph == 2)
                                sawWispCharge |= HasRainReplicaMesh(enemy, "mdlWisp1Mouth");
                            sawWispFire |= enemy.RainEnemyAnimation == 13 && enemy.RainTelegraph == 3;
                            sawWispTraces |= enemy.GetComponentsInChildren<LineRenderer>()
                                .Count(trace => trace.enabled && trace.name == "Wisp ember trace") == 3;
                        }
                        if (enemy.RainEnemyKind == RangeResearchEnemy.BruiserKind + 1 &&
                            enemy.RainEnemyAnimation == 16 && enemy.RainTelegraph == 3)
                            sawBruiserBreath |= HasRainReplicaMesh(enemy, "mdlLemurianBruiser");
                        if (enemy.RainEnemyKind == RangeResearchEnemy.GreaterWispKind + 1)
                        {
                            if (enemy.RainEnemyAnimation == 24 && enemy.RainTelegraph == 2)
                                sawGreaterWispCharge |= HasRainReplicaMesh(enemy, "mdlGreaterWisp");
                            sawGreaterWispFire |= enemy.RainEnemyAnimation == 25;
                            sawGreaterWispVolley |= enemy.RainGreaterWispShots.Count == 2;
                        }
                        if (enemy.RainEnemyKind == RangeResearchEnemy.BisonKind + 1)
                        {
                            if (enemy.RainEnemyAnimation == RangeResearchEnemy.BisonChargePrepAnimation &&
                                enemy.RainTelegraph == 2)
                            {
                                sawBisonChargePrep |= GameObject.Find("ChargeBisonCharge(Clone)") != null;
                                bisonChargeId = enemy.netId;
                                bisonChargeOrigin = enemy.transform.position;
                            }
                            if (enemy.netId == bisonChargeId)
                            {
                                if (enemy.RainEnemyAnimation == RangeResearchEnemy.BisonChargeAnimation)
                                    sawBisonChargeTravel |= (enemy.transform.position - bisonChargeOrigin).sqrMagnitude >= 4f;
                                if (enemy.RainEnemyAnimation == RangeResearchEnemy.BisonStunAnimation)
                                    sawBisonWallStun = true;
                            }
                        }
                        if (enemy.RainEnemyKind == RangeResearchEnemy.FlyingVerminKind + 1)
                        {
                            sawFlyingVerminCue |= enemy.RainTelegraph == 2 &&
                                enemy.RainEnemyAnimation == RangeResearchEnemy.FlyingVerminSpitAnimation &&
                                HasRainReplicaMesh(enemy, "mdlFlyingVermin");
                            sawFlyingVerminShot |= enemy.RainFlyingVerminShots.Count > 0 &&
                                GameObject.Find("VerminSpitGhost(Clone)") != null;
                        }
                        if (enemy.RainEnemyKind == RangeResearchEnemy.HermitCrabKind + 1)
                        {
                            sawHermitBurrow |= enemy.RainEnemyAnimation == RangeResearchEnemy.HermitBurrowInAnimation &&
                                HasRainReplicaMesh(enemy, "mdlHermitCrab");
                            sawHermitMortar |= enemy.RainTelegraph == 1 && enemy.RainHermitMortars.Count != 0 &&
                                GameObject.Find("Rain Hermit Crab mortar") != null;
                        }
                        if (enemy.RainEnemyKind == RangeResearchEnemy.AcidLarvaKind + 1)
                        {
                            if (larvaLeapId == 0 && enemy.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLeapStartAnimation &&
                                enemy.RainTelegraph == 1 && HasRainReplicaMesh(enemy, "mdlAcidLarva"))
                            {
                                sawLarvaCue = true;
                                larvaLeapId = enemy.netId;
                                larvaLeapStart = enemy.transform.position;
                            }
                            if (enemy.netId == larvaLeapId)
                            {
                                sawLarvaFlight |= enemy.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLeapLoopAnimation &&
                                    Vector3.ProjectOnPlane(enemy.transform.position - larvaLeapStart, Vector3.up).sqrMagnitude >= 4f;
                                sawLarvaLanding |= enemy.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLandingAnimation;
                            }
                        }
                        if (enemy.RainEnemyKind == RangeResearchEnemy.LunarExploderKind + 1)
                        {
                            bool hasMesh = HasRainReplicaMesh(enemy, "mdlLunarExploder");
                            if (lunarId == 0 && enemy.RainLunarExploderShards.Count != 0 && hasMesh &&
                                (!lunarGraphics || GameObject.Find("Rain Lunar Exploder shard") != null))
                            {
                                lunarId = enemy.netId;
                                sawLunarShard = true;
                            }
                            if (enemy.netId == lunarId && enemy.CurrentHealth <= 0)
                            {
                                sawLunarDeathCue |= enemy.RainTelegraph == 1 && hasMesh;
                                var model = enemy.transform.Find("mdlLunarExploder");
                                sawLunarZone |= enemy.RainTelegraph == RangeResearchEnemy.LunarZoneActiveCue &&
                                    model && !model.gameObject.activeSelf && sawLunarDeathCue &&
                                    enemy.RainLunarExploderShards.Count == 0;
                            }
                        }
                        if (enemy.RainTelegraph != 1) continue;
                        if (enemy.RainEnemyKind == RangeResearchEnemy.VerminKind + 1 &&
                            RangeResearchEnemy.IsVerminBiteAnimation(enemy.RainEnemyAnimation))
                            sawVerminLash |= HasRainReplicaMesh(enemy, "mdlVermin");
                        if (enemy.RainEnemyKind == RangeResearchEnemy.BisonKind + 1 &&
                            enemy.RainEnemyAnimation == RangeResearchEnemy.BisonHeadbuttAnimation)
                            sawBisonHeadbutt |= HasRainReplicaMesh(enemy, "mdlBison");
                        if (enemy.RainEnemyKind == RangeResearchEnemy.ImpKind + 1 && enemy.RainEnemyAnimation == 26)
                            sawImpSlash |= HasRainReplicaMesh(enemy, "mdlImp");
                        if (enemy.RainEnemyKind == RangeResearchEnemy.JellyfishKind + 1 &&
                            enemy.RainEnemyAnimation == 14)
                            sawJellyfishCue |= HasRainReplicaMesh(enemy, "mdlJellyfish");
                        if (enemy.RainEnemyKind == RangeResearchEnemy.BeetleGuardKind + 1 &&
                            enemy.RainEnemyAnimation == 5)
                            sawGuardSlam |= HasRainReplicaMesh(enemy, "mdlBeetleGuard");
                        if (enemy.RainEnemyKind == RangeResearchEnemy.LemurianKind + 1 &&
                            enemy.RainEnemyAnimation == 9)
                            sawLemurianBite |= HasRainReplicaMesh(enemy, "mdlLemurian");
                    }
                    if (sawJellyfishCue && !sawJellyfishNova)
                        sawJellyfishNova = GameObject.Find("JellyfishNova(Clone)") != null;
                    var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
                    var radar = overlay ? overlay.Find("Rain radar") : null;
                    var titanDot = radar ? radar.Find("Boss") : null;
                    var beaconDot = radar ? radar.Find("Beacon 0") : null;
                    sawBossDot |= titanDot && titanDot.gameObject.activeInHierarchy && mode.RainBossActive;
                    sawBeaconDot |= beaconDot && beaconDot.gameObject.activeInHierarchy && mode.RainBeaconsActivated == 0;
                    if (mode.RainBossActive)
                    {
                        foreach (var identity in NetworkClient.spawned.Values)
                        {
                            if (!identity) continue;
                            if (identity.TryGetComponent<RainQueenWardHealth>(out var ward))
                            {
                                var mesh = ward.GetComponentInChildren<SkinnedMeshRenderer>();
                                sawQueenWard |= mesh && mesh.sharedMesh;
                                continue;
                            }
                            if (!identity.TryGetComponent<CharacterInstance>(out var enemy)) continue;
                            if (enemy.RainEnemyKind == RangeResearchEnemy.QueenKind + 1)
                                sawQueenCast |= enemy.RainEnemyAnimation == 23 && HasRainReplicaMesh(enemy, "mdlBeetleQueen");
                            if (enemy.RainEnemyKind != 3) continue;
                            var fist = enemy.transform.Find("Titan ground fist");
                            if (fist && fist.gameObject.activeSelf) sawFist = true;
                            else if (fist && sawFist) sawFistRetract = true;
                        }
                    }
                    if(sawReplica && mode.RainVictory)
                    {
                        if (titanDot && titanDot.gameObject.activeSelf) { yield return null; continue; }
                        var chest = mode.transform.Find("Rain supply chest 0");
                        if (fruitCharacter) fruitCharacter.Client_OnHealthAdded -= CountFruitHeal;
                        if (fruitHeals != 1 || !sawExpectedFruitHeal)
                            Debug.LogError("RAIN_CLIENT_REMOTE_FRUIT_RPC count=" + fruitHeals);
                        else Debug.Log("RAIN_CLIENT_REMOTE_FRUIT_RPC_CHECKED");
                        if (!chest || chest.gameObject.activeSelf || local.RainGold < 60 ||
                            mode.RainBeaconsActivated != Gamemodes.RangeGamemode.RainBeaconCount ||
                            !GameManager.Players.Values.Any(p => p && p.RainSteak == 1))
                            Debug.LogError("RAIN_CLIENT_CHEST_OR_REWARD_SYNC");
                        else Debug.Log("RAIN_CLIENT_CHEST_SYNC_CHECKED");
                        var highChest = mode.transform.Find("Rain supply chest 64");
                        var unboughtChest = mode.transform.Find("Rain supply chest 1");
                        if (!highChest || highChest.gameObject.activeSelf ||
                            !unboughtChest || !unboughtChest.gameObject.activeSelf ||
                            mode.TryGetRainMapNode(64, false, out _))
                            Debug.LogError("RAIN_CLIENT_CHEST_BEYOND_MASK_SYNC");
                        else Debug.Log("RAIN_CLIENT_CHEST_BEYOND_MASK_SYNC_CHECKED");
                        int firstShop = Gamemodes.RangeGamemode.FirstRainMultishopChest;
                        if (Enumerable.Range(firstShop, Gamemodes.RangeGamemode.RainMultishopSize)
                            .Any(index => mode.TryGetRainMapNode(index, false, out _)))
                            Debug.LogError("RAIN_CLIENT_MULTISHOP_SYNC");
                        else Debug.Log("RAIN_CLIENT_MULTISHOP_SYNC_CHECKED");
                        if (!GameManager.Players.Values.Any(p => p && p != local && p.MyCharacter &&
                            p.MyCharacter.ResearchSyringes == 1))
                            Debug.LogError("RAIN_CLIENT_SYRINGE_SYNC_FAILED");
                        var chestDot = radar ? radar.Find("Chest 0") : null;
                        var levelLabel = overlay ? overlay.Find("Rain player level") : null;
                        if (local.RainExperience != 10 || local.RainLevel != 1 ||
                            !levelLabel || !levelLabel.GetComponent<TMPro.TMP_Text>().text.Contains("10/20"))
                            Debug.LogError("RAIN_CLIENT_PLAYER_EXPERIENCE_SYNC_FAILED");
                        else Debug.Log($"RAIN_CLIENT_PLAYER_EXPERIENCE={local.RainExperience} LEVEL={local.RainLevel}");
                        if (!radar || !radar.Find("Chapter One terrain/Sector 2-2"))
                            Debug.LogError("RAIN_CLIENT_TERRAIN_MAP_MISSING");
                        else Debug.Log("RAIN_CLIENT_TERRAIN_MAP_CHECKED");
                        if (!mode.transform.Find("Rain warbanner")) Debug.LogError("RAIN_CLIENT_WARBANNER_MISSING");
                        if (!sawBossDot || !sawBeaconDot || !chestDot || chestDot.gameObject.activeSelf ||
                            !beaconDot || beaconDot.gameObject.activeSelf)
                            Debug.LogError($"RAIN_CLIENT_RADAR_STATE boss={sawBossDot} beacon={sawBeaconDot}");
                        else Debug.Log("RAIN_CLIENT_RADAR_CHECKED");
                        if (mode.RainBossKind == RangeResearchEnemy.QueenKind)
                        {
                            if (!sawQueenCast || !sawQueenWard)
                                Debug.LogError($"RAIN_CLIENT_QUEEN cast={sawQueenCast} ward={sawQueenWard}");
                            else Debug.Log("RAIN_CLIENT_QUEEN_CHECKED");
                            if (!mode.transform.Cast<Transform>().Any(child =>
                                child.name.StartsWith("Rain boss reward ", StringComparison.Ordinal)))
                                Debug.LogError("RAIN_CLIENT_QUEEN_DROP_MISSING");
                        }
                        else if (!sawFist || !sawFistRetract)
                            Debug.LogError($"RAIN_CLIENT_TITAN_FIST active={sawFist} retract={sawFistRetract}");
                        else Debug.Log("RAIN_CLIENT_TITAN_FIST_CHECKED");
                        if (!sawGuardSlam || !sawLemurianBite || !sawImpSlash || !sawBisonHeadbutt)
                            Debug.LogError($"RAIN_CLIENT_NEW_MONSTER_CUE_FAILED guard={sawGuardSlam} lemurian={sawLemurianBite} imp={sawImpSlash} bison={sawBisonHeadbutt}");
                        else Debug.Log("RAIN_CLIENT_NEW_MONSTER_CUES_CHECKED");
                        if (!sawBisonChargePrep || !sawBisonChargeTravel || !sawBisonWallStun)
                            Debug.LogError($"RAIN_CLIENT_BISON_CHARGE_FAILED prep={sawBisonChargePrep} travel={sawBisonChargeTravel} stun={sawBisonWallStun}");
                        if (!sawVerminLash) Debug.LogError("RAIN_CLIENT_VERMIN_LASH_FAILED");
                        if (!sawFlyingVerminCue || !sawFlyingVerminShot || !sawFlyingVerminDeath)
                            Debug.LogError($"RAIN_CLIENT_FLYING_VERMIN_FAILED cue={sawFlyingVerminCue} shot={sawFlyingVerminShot} death={sawFlyingVerminDeath}");
                        if (!sawHermitBurrow || !sawHermitMortar || !sawHermitImpact)
                            Debug.LogError($"RAIN_CLIENT_HERMIT_FAILED burrow={sawHermitBurrow} mortar={sawHermitMortar} impact={sawHermitImpact}");
                        else Debug.Log("RAIN_CLIENT_HERMIT_MORTAR_CHECKED");
                        if (!sawLarvaCue || !sawLarvaFlight || !sawLarvaLanding)
                            Debug.LogError($"RAIN_CLIENT_LARVA_LEAP_FAILED cue={sawLarvaCue} flight={sawLarvaFlight} landing={sawLarvaLanding}");
                        else Debug.Log("RAIN_CLIENT_LARVA_LEAP_CHECKED");
                        if (!sawLunarShard || !sawLunarImpact || !sawLunarDeathCue || !sawLunarZone)
                            Debug.LogError($"RAIN_CLIENT_LUNAR_EXPLODER_FAILED shard={sawLunarShard} impact={sawLunarImpact} cue={sawLunarDeathCue} zone={sawLunarZone}");
                        else Debug.Log("RAIN_CLIENT_LUNAR_EXPLODER_CHECKED");
                        if (!sawWispCharge || !sawWispFire || !sawWispFlight || !sawWispTraces)
                            Debug.LogError($"RAIN_CLIENT_WISP_FAILED charge={sawWispCharge} fire={sawWispFire} flight={sawWispFlight} traces={sawWispTraces}");
                        else Debug.Log("RAIN_CLIENT_WISP_CHECKED");
                        if (!sawJellyfishCue || !sawJellyfishNova)
                            Debug.LogError($"RAIN_CLIENT_JELLYFISH_FAILED cue={sawJellyfishCue} nova={sawJellyfishNova}");
                        else Debug.Log("RAIN_CLIENT_JELLYFISH_CHECKED");
                        if (!sawBruiserBreath) Debug.LogError("RAIN_CLIENT_BRUISER_BREATH_FAILED");
                        if (!sawGreaterWispCharge || !sawGreaterWispFire || !sawGreaterWispVolley)
                            Debug.LogError($"RAIN_CLIENT_GREATER_WISP_FAILED charge={sawGreaterWispCharge} fire={sawGreaterWispFire} volley={sawGreaterWispVolley}");
                        else Debug.Log("RAIN_CLIENT_BRUISER_BREATH_CHECKED");
                        yield break;
                    }
                }
                yield return null;
            }
            if (fruitCharacter) fruitCharacter.Client_OnHealthAdded -= CountFruitHeal;
            Debug.LogError("RAIN_CLIENT_MISSING_REPLICA_OR_VICTORY replica="+sawReplica);
        }
        static bool MatchesMonsterSmokeAttack(int kind, byte actual, byte expected) =>
            kind == RangeResearchEnemy.VerminKind ? RangeResearchEnemy.IsVerminBiteAnimation(actual) : actual == expected;

        IEnumerator VerifyLunarExploderCoopShard(CharacterInstance owner, RangeResearchEnemy actor,
            CharacterInstance enemy)
        {
            Vector3 ownerPosition = owner.transform.position;
            Vector3 enemyPosition = enemy.transform.position;
            int ownerHealth = owner.CurrentHealth;
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            bool enemyMotorEnabled = enemyMotor.enabled;
            bool graphics = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
            var backstop = new GameObject("Lunar Exploder smoke backstop");
            try
            {
                actor.SetCombat(false);
                actor.SetTarget(owner);
                ownerMotor.enabled = enemyMotor.enabled = false;
                enemy.transform.position = enemyPosition + Vector3.up * 60f;
                owner.transform.position = enemy.transform.position + Vector3.forward * 30f;
                backstop.transform.position = owner.transform.position + Vector3.forward * 3f + Vector3.up;
                backstop.AddComponent<BoxCollider>().size = new Vector3(12f, 12f, 1f);
                Physics.SyncTransforms();
                actor.SetCombat(true);
                int firstShot = 0;
                float deadline = Time.realtimeSinceStartup + 4f;
                while (enemy && Time.realtimeSinceStartup < deadline)
                {
                    if (firstShot == 0)
                    {
                        foreach (var shot in enemy.RainLunarExploderShards)
                        {
                            firstShot = shot.Key;
                            break;
                        }
                    }
                    else if (!enemy.RainLunarExploderShards.ContainsKey(firstShot) &&
                        (!graphics || GameObject.Find("Rain Lunar Exploder shard impact")))
                    {
                        Debug.Log("RAIN_COOP_LUNAR_EXPLODER_SHARD_CHECKED");
                        yield break;
                    }
                    yield return null;
                }
                Debug.LogError($"RAIN_COOP_LUNAR_EXPLODER_SHARD_FAILED id={firstShot}");
            }
            finally
            {
                if (actor) actor.SetCombat(false);
                Destroy(backstop);
                if (enemy) enemy.transform.position = enemyPosition;
                if (owner)
                {
                    owner.transform.position = ownerPosition;
                    owner.CurrentHealth = ownerHealth;
                }
                if (enemyMotor) enemyMotor.enabled = enemyMotorEnabled;
                if (ownerMotor) ownerMotor.enabled = ownerMotorEnabled;
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyHermitCoopMortar(CharacterInstance owner, CharacterInstance enemy)
        {
            Vector3 ownerPosition = owner.transform.position;
            Vector3 enemyPosition = enemy.transform.position;
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            bool enemyMotorEnabled = enemyMotor.enabled;
            var landingPad = new GameObject("Hermit Crab smoke landing pad");
            try
            {
                ownerMotor.enabled = enemyMotor.enabled = false;
                enemy.transform.position = enemyPosition + Vector3.up * 60f;
                owner.transform.position = enemy.transform.position + Vector3.forward * 30f;
                landingPad.transform.position = owner.transform.position - Vector3.up * .5f;
                landingPad.AddComponent<BoxCollider>().size = new Vector3(12f, 1f, 12f);
                Physics.SyncTransforms();
                bool sawMortar = false;
                float deadline = Time.realtimeSinceStartup + 8f;
                while (enemy && Time.realtimeSinceStartup < deadline)
                {
                    sawMortar |= enemy.RainHermitMortars.Count != 0 && enemy.RainTelegraph == 1;
                    if (sawMortar && GameObject.Find("Rain Hermit Crab mortar impact"))
                    {
                        Debug.Log("RAIN_COOP_HERMIT_MORTAR_CHECKED");
                        yield break;
                    }
                    yield return null;
                }
                Debug.LogError($"RAIN_COOP_HERMIT_MORTAR_FAILED mortar={sawMortar}");
            }
            finally
            {
                Destroy(landingPad);
                if (enemy) enemy.transform.position = enemyPosition;
                if (owner) owner.transform.position = ownerPosition;
                if (enemyMotor) enemyMotor.enabled = enemyMotorEnabled;
                if (ownerMotor) ownerMotor.enabled = ownerMotorEnabled;
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyNewMonsterGm(CharacterInstance owner, string onlyId = null)
        {
            var lab = GameManager.Gamemode.GetComponent<RangeResearchLab>();
            if (!lab)
            {
                Debug.LogError("RAIN_MONSTER_GM_MISSING_LAB");
                yield break;
            }

            if (onlyId == "GREATERWISP_BODY")
            {
                int errors = loggedErrors;
                yield return VerifyGreaterWispGm(lab, owner);
                Debug.Log((loggedErrors == errors ? "RAIN_MONSTER_RECOVERY_PASS " : "RAIN_MONSTER_RECOVERY_SKIP ") + onlyId);
                yield break;
            }

            foreach (var (id, kind, attack, modelName, settleTime) in new (string, int, byte, string, float)[]
            {
                ("BEETLEGUARD_BODY", RangeResearchEnemy.BeetleGuardKind, 5, "mdlBeetleGuard", 1.75f),
                ("LEMURIAN_BODY", RangeResearchEnemy.LemurianKind, 9, "mdlLemurian", 1.75f),
                ("WISP_BODY", RangeResearchEnemy.WispKind, 12, "mdlWisp1Mouth", 1.75f),
                ("LEMURIANBRUISER_BODY", RangeResearchEnemy.BruiserKind, 15, "mdlLemurianBruiser", 2.1f),
                ("IMP_BODY", RangeResearchEnemy.ImpKind, 26, "mdlImp", 1.75f),
                ("BISON_BODY", RangeResearchEnemy.BisonKind, RangeResearchEnemy.BisonHeadbuttAnimation,
                    "mdlBison", RangeResearchEnemy.BisonSpawnDuration + .15f),
                ("VERMIN_BODY", RangeResearchEnemy.VerminKind, RangeResearchEnemy.VerminFirstBiteAnimation,
                    "mdlVermin", RangeResearchEnemy.VerminSpawnDuration + .15f),
                ("FLYINGVERMIN_BODY", RangeResearchEnemy.FlyingVerminKind, RangeResearchEnemy.FlyingVerminSpitAnimation,
                    "mdlFlyingVermin", RangeResearchEnemy.FlyingVerminSpawnDuration + .15f),
                ("HERMIT_CRAB_BODY", RangeResearchEnemy.HermitCrabKind, RangeResearchEnemy.HermitFireAnimation,
                    "mdlHermitCrab", RangeResearchEnemy.HermitCrabSpawnDuration + .15f)
            })
            {
                if (onlyId != null && id != onlyId) continue;
                int errors = loggedErrors;
                bool guard = kind == RangeResearchEnemy.BeetleGuardKind;
                bool wisp = kind == RangeResearchEnemy.WispKind;
                bool bruiser = kind == RangeResearchEnemy.BruiserKind;
                bool imp = kind == RangeResearchEnemy.ImpKind;
                bool bison = kind == RangeResearchEnemy.BisonKind;
                bool flyingVermin = kind == RangeResearchEnemy.FlyingVerminKind;
                bool hermit = kind == RangeResearchEnemy.HermitCrabKind;
                Vector3 ownerPosition = owner.transform.position;
                Vector2 ownerLook = owner.lookInput;
                var entry = new BrowserEntry { id = id, category = 1 };
                RangeResearchEnemy actor = null;
                for (int direction = 0; direction < 4 && !actor; direction++)
                {
                    owner.lookInput = new Vector2(0f, ownerLook.y + direction * 90f);
                    lab.ExecuteCatalog(entry);
                    actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                        .FirstOrDefault(candidate => candidate.IsLabSummon && candidate.Archetype == kind);
                }
                if (!actor)
                {
                    Debug.LogError("RAIN_MONSTER_GM_SPAWN_FAILED " + id);
                    continue;
                }

                var enemy = actor.GetComponent<CharacterInstance>();
                uint netId = enemy.netId;
                int originalMaxHealth = owner.MaxHealth;
                int originalHealth = owner.CurrentHealth;
                if (onlyId != null && bruiser) owner.MaxHealth = owner.CurrentHealth = 1000;
                Vector3 enemySpawnPosition = enemy.transform.position;
                if (!wisp && !flyingVermin && !hermit)
                    owner.transform.position = enemySpawnPosition + enemy.transform.forward * (bruiser ? 6f : 2f);
                Physics.SyncTransforms();
                int health = owner.CurrentHealth;
                yield return new WaitForSeconds(settleTime);
                bool idle = enemy.RainEnemyAnimation == 0 && enemy.RainTelegraph == 0 && owner.CurrentHealth == health;
                if (wisp || flyingVermin)
                    idle &= (enemy.transform.position - enemySpawnPosition).sqrMagnitude < .01f;
                if (!idle || !HasRainReplicaMesh(enemy, modelName))
                    Debug.LogError("RAIN_MONSTER_GM_IDLE_OR_MODEL_FAILED " + id);

                var ownerMotor = owner.GetComponent<CharacterMotor>();
                bool motorEnabled = ownerMotor.enabled;
                var enemyMotor = enemy.GetComponent<CharacterMotor>();
                bool enemyMotorEnabled = enemyMotor.enabled;
                if (flyingVermin || hermit)
                {
                    ownerMotor.enabled = false;
                    if (hermit) enemyMotor.enabled = false;
                    enemy.transform.position = enemySpawnPosition + Vector3.up * 60f;
                    owner.transform.position = enemy.transform.position + Vector3.forward * (hermit ? 30f : 16f);
                    Physics.SyncTransforms();
                }

                lab.SetMonsterCombat(true);
                float deadline = Time.realtimeSinceStartup + (hermit ? 7f : 3f);
                bool cue = false;
                bool spitObserved = false;
                bool burrowObserved = false;
                bool mortarObserved = false;
                bool attackAudioObserved = false;
                var attackVisual = hermit || bruiser ? enemy.GetComponent<RainActorVisual>() : null;
                while (owner.CurrentHealth == health && Time.realtimeSinceStartup < deadline)
                {
                    attackAudioObserved |= attackVisual && attackVisual.AttackAudioPlaying;
                    cue |= MatchesMonsterSmokeAttack(kind, enemy.RainEnemyAnimation, attack) &&
                        enemy.RainTelegraph == (wisp || bruiser || flyingVermin ? 2 : 1);
                    if (flyingVermin)
                        spitObserved |= enemy.RainFlyingVerminShots.Count == 1 &&
                            GameObject.Find("VerminSpitGhost(Clone)") != null;
                    if (hermit)
                    {
                        burrowObserved |= enemy.RainEnemyAnimation == RangeResearchEnemy.HermitBurrowInAnimation;
                        mortarObserved |= enemy.RainHermitMortars.Count != 0 &&
                            GameObject.Find("Rain Hermit Crab mortar") != null;
                    }
                    yield return null;
                }
                attackAudioObserved |= attackVisual && attackVisual.AttackAudioPlaying;
                if (!cue || owner.CurrentHealth >= health)
                    Debug.LogError($"RAIN_MONSTER_GM_HIT_FAILED {id} cue={cue} damage={health - owner.CurrentHealth}");
                if (hermit && (!burrowObserved || !mortarObserved))
                    Debug.LogError($"RAIN_MONSTER_GM_HERMIT_CUE_FAILED burrow={burrowObserved} mortar={mortarObserved}");
                if (onlyId != null && hermit)
                {
                    if (!attackAudioObserved) Debug.LogError("RAIN_MONSTER_RECOVERY_HERMIT_AUDIO_MISSING");
                    Debug.Log($"RAIN_MONSTER_RECOVERY_HERMIT damage={health - owner.CurrentHealth} burrow={burrowObserved} projectile={mortarObserved} audio={attackAudioObserved}");
                }
                if (hermit && !GameObject.Find("Rain Hermit Crab mortar impact"))
                    Debug.LogError("RAIN_MONSTER_GM_HERMIT_IMPACT_MISSING");
                if (imp && (!GameObject.Find("ImpSwipeEffect(Clone)") || !GameObject.Find("ImpactImpSwipe(Clone)")))
                    Debug.LogError("RAIN_MONSTER_GM_IMP_SWIPE_EFFECT_MISSING");
                if (kind == RangeResearchEnemy.VerminKind && !GameObject.Find("VerminBiteEffect(Clone)"))
                    Debug.LogError("RAIN_MONSTER_GM_VERMIN_LASH_EFFECT_MISSING");
                if (flyingVermin && (!spitObserved || !GameObject.Find("VerminSpitImpactEffect(Clone)")))
                    Debug.LogError("RAIN_MONSTER_GM_FLYING_VERMIN_SPIT_EFFECT_MISSING");
                if (flyingVermin)
                {
                    deadline = Time.realtimeSinceStartup + 4f;
                    while (enemy.RainFlyingVerminShots.Count == 0 && Time.realtimeSinceStartup < deadline)
                        yield return null;
                    if (enemy.RainFlyingVerminShots.Count == 0)
                        Debug.LogError("RAIN_MONSTER_GM_FLYING_VERMIN_SECOND_SHOT_MISSING");
                }

                if (wisp && (enemy.transform.position - enemySpawnPosition).sqrMagnitude < .25f)
                    Debug.LogError("RAIN_MONSTER_GM_WISP_FLIGHT_FAILED");
                lab.SetMonsterCombat(false);
                yield return null;
                if (enemy.RainTelegraph != 0 || enemy.RainEnemyAnimation != 0 ||
                    flyingVermin && (enemy.RainFlyingVerminShots.Count != 0 ||
                        GameObject.Find("VerminSpitGhost(Clone)") != null))
                    Debug.LogError("RAIN_MONSTER_GM_STOP_FAILED " + id);
                if (bruiser)
                {
                    int afterBreath = owner.CurrentHealth;
                    yield return new WaitForSeconds(.75f);
                    if (owner.CurrentHealth >= afterBreath)
                        Debug.LogError("RAIN_MONSTER_GM_BRUISER_BURN_FAILED");
                    if (onlyId != null)
                    {
                        if (!attackAudioObserved) Debug.LogError("RAIN_MONSTER_RECOVERY_BRUISER_BREATH_AUDIO_MISSING");
                        Debug.Log($"RAIN_MONSTER_RECOVERY_BRUISER_BREATH damage={health - afterBreath} burn={afterBreath - owner.CurrentHealth} cue={cue} audio={attackAudioObserved}");
                    }
                    yield return VerifyBruiserGmVolley(lab, owner, enemy, health);
                }
                else if (imp) yield return VerifyImpGmBlink(lab, owner, enemy, enemySpawnPosition);
                else if (bison) yield return VerifyBisonCharge(lab, owner, enemy, health);
                else if (!wisp && !flyingVermin && !hermit && kind != RangeResearchEnemy.VerminKind)
                    yield return VerifyNewMonsterGmProjectile(lab, owner, enemy, guard, health);
                lab.RemoveCatalog(entry);
                yield return null;
                if (NetworkServer.spawned.ContainsKey(netId))
                    Debug.LogError("RAIN_MONSTER_GM_REMOVE_FAILED " + id);
                owner.transform.position = ownerPosition;
                owner.lookInput = ownerLook;
                owner.MaxHealth = originalMaxHealth;
                owner.CurrentHealth = originalHealth;
                if (flyingVermin || hermit) ownerMotor.enabled = motorEnabled;
                if (hermit && enemyMotor) enemyMotor.enabled = enemyMotorEnabled;
                Physics.SyncTransforms();
                Debug.Log("RAIN_MONSTER_GM_CASE_FINISHED " + id);
                if (onlyId != null)
                    Debug.Log((loggedErrors == errors ? "RAIN_MONSTER_RECOVERY_PASS " : "RAIN_MONSTER_RECOVERY_SKIP ") + id);
            }
            if (onlyId != null) yield break;
            yield return VerifyJellyfishGm(lab, owner, evade: false);
            yield return VerifyJellyfishGm(lab, owner, evade: true);
            yield return VerifyQueenWardGm(lab, owner);
            yield return VerifyGreaterWispGm(lab, owner);
            yield return VerifyAcidLarvaGm(lab, owner);
            yield return VerifyLunarExploderGm(lab, owner);
            yield return VerifyParentGm(lab, owner);
        }

        IEnumerator VerifyParentGm(RangeResearchLab lab, CharacterInstance owner)
        {
            const string marker = "RAIN_MONSTER_GM_PARENT";
            var entry = new BrowserEntry { id = "PARENT_BODY", category = 1 };
            var originalLook = owner.lookInput;
            RangeResearchEnemy actor = null;
            for (int direction = 0; direction < 4 && !actor; direction++)
            {
                owner.lookInput = new Vector2(0, originalLook.y + direction * 90);
                lab.ExecuteCatalog(entry);
                actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(a => a.IsLabSummon && a.Archetype == RangeResearchEnemy.ParentKind);
            }
            owner.lookInput = originalLook;
            if (!actor) { Debug.LogError(marker + "_SPAWN_FAILED"); yield break; }
            var enemy = actor.GetComponent<CharacterInstance>();
            uint id = enemy.netId;
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            var enemyController = enemy.GetComponent<CharacterController>();
            bool ownerEnabled = ownerMotor.enabled, enemyEnabled = enemyMotor.enabled;
            Vector3 ownerPosition = owner.transform.position;
            int health = owner.CurrentHealth, maximum = owner.MaxHealth;
            float ownerRegen = owner.RainHealthRegen;
            bool ownerInvulnerable = owner.RangeInvulnerable;
            var floor = new GameObject("Parent smoke floor");
            var testTarget = new GameObject("Parent smoke hurtbox");
            Unity.AI.Navigation.NavMeshSurface navigation = null;
            try
            {
                // The coop map has baseline regeneration and unrelated natural enemies.
                // Protect setup, then disable regeneration only inside this exact-damage fixture.
                owner.RainHealthRegen = 0f;
                owner.RangeInvulnerable = true;
                yield return new WaitForSeconds(RangeResearchEnemy.ParentSpawnDuration + .2f);
                if (enemy.MaxHealth != 585 || !HasRainReplicaMesh(enemy, "mdlParent") ||
                    enemy.RainEnemyAnimation != 0 || owner.CurrentHealth != health)
                    Debug.LogError($"{marker}_IDLE_FAILED maximum={enemy.MaxHealth} animation={enemy.RainEnemyAnimation} " +
                        $"ownerHealth={owner.CurrentHealth} expected={health} mesh={HasRainReplicaMesh(enemy, "mdlParent")}");
                var parentModel = enemy.transform.Find("mdlParent");
                var originalBoxes = parentModel.GetComponentsInChildren<HitBox>(true);
                Physics.SyncTransforms();
                bool shape = originalBoxes.Length == 13 && originalBoxes.All(box => box._health == enemy &&
                    box.part == CharacterPart.body && box.gameObject.layer == 8 && box.GetComponent<Collider>().enabled) &&
                    enemy.GetComponentsInChildren<HitBox>(true).Where(box => !box.transform.IsChildOf(parentModel))
                        .All(box => !box.GetComponent<Collider>().enabled) &&
                    Mathf.Abs(enemyController.radius - 1.5f) < .001f && Mathf.Abs(enemyController.height - 8.6f) < .001f &&
                    Mathf.Abs(enemyController.center.y - 4.3f) < .001f &&
                    Mathf.Abs(enemyController.slopeLimit - 55f) < .001f && Mathf.Abs(enemyController.stepOffset - 4f) < .001f &&
                    Mathf.Abs(parentModel.localPosition.y + .19f) < .001f;
                var aimCollider = enemy.RainMonsterAimCollider;
                bool aim = aimCollider && aimCollider is SphereCollider &&
                    aimCollider.transform.parent.name == "Par_Spine_TopSHJnt" &&
                    Vector3.Distance(enemy.CombatAimPoint, aimCollider.bounds.center) < .001f &&
                    aimCollider.Raycast(new Ray(aimCollider.bounds.center + Vector3.forward * 20f, Vector3.back), out _, 40f);
                if (!shape || !aim) Debug.LogError($"{marker}_SHAPE_FAILED shape={shape} aim={aim}");
                else Debug.Log(marker + "_SHAPE_CHECKED");
                Vector3 origin = enemy.transform.position + Vector3.up * 60;
                floor.transform.position = origin + Vector3.down * .5f;
                floor.AddComponent<BoxCollider>().size = new Vector3(100, 1, 100);
                navigation = floor.AddComponent<Unity.AI.Navigation.NavMeshSurface>();
                navigation.collectObjects = Unity.AI.Navigation.CollectObjects.Children;
                navigation.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
                navigation.BuildNavMesh();
                ownerMotor.enabled = enemyMotor.enabled = false;
                owner.MaxHealth = owner.CurrentHealth = 1000;
                owner.RangeInvulnerable = false;
                enemy.transform.position = origin + Vector3.up * .15f;
                owner.transform.position = origin + Vector3.forward * 7 + Vector3.up * .15f;
                testTarget.layer = (int)GameLayers.character;
                testTarget.transform.SetParent(owner.transform, false);
                testTarget.AddComponent<SphereCollider>().radius = .6f;
                Physics.SyncTransforms();
                actor.SetTarget(owner);
                lab.SetMonsterCombat(true);
                float deadline = Time.realtimeSinceStartup + 2;
                while (enemy.RainEnemyAnimation != RangeResearchEnemy.ParentSlamAnimation &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                deadline = Time.realtimeSinceStartup + .3f;
                while (enemy.RainTelegraph == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                bool cue = enemy.RainTelegraph != 0;
                var visual = actor.GetComponent<RainActorVisual>();
                visual.EvaluateCurrentPose(RangeResearchEnemy.ParentSlamHitTime);
                owner.transform.position = visual.FindModelBone("SlamZone").position;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(1.5f);
                int damage = 1000 - owner.CurrentHealth;
                if (!cue || damage != 64) Debug.LogError($"{marker}_SLAM_FAILED cue={cue} damage={damage}");
                else Debug.Log(marker + "_SLAM_CHECKED");
                lab.SetMonsterCombat(false);
                int afterStop = owner.CurrentHealth;
                yield return new WaitForSeconds(.3f);
                if (owner.CurrentHealth != afterStop || enemy.RainTelegraph != 0)
                    Debug.LogError($"{marker}_STOP_FAILED health={owner.CurrentHealth} expected={afterStop} cue={enemy.RainTelegraph}");

                // Exercise BlinkMachine during a second Body slam, then cancel both safely.
                yield return new WaitForSeconds(.4f);
                enemy.transform.position = origin + Vector3.up * .15f;
                owner.transform.position = origin + Vector3.forward * 7 + Vector3.up * .15f;
                enemy.CurrentHealth = 400;
                enemyMotor.enabled = enemyEnabled;
                Physics.SyncTransforms();
                actor.SetTarget(owner);
                lab.SetMonsterCombat(true);
                deadline = Time.realtimeSinceStartup + 2;
                while (enemy.RainEnemyAnimation != RangeResearchEnemy.ParentSlamAnimation &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                Vector3 slamMoveStart = enemy.transform.position;
                yield return new WaitForSeconds(.25f);
                float slamTravel = Vector3.ProjectOnPlane(enemy.transform.position - slamMoveStart, Vector3.up).magnitude;
                if (slamTravel < .5f || enemy.RainEnemyAnimation != RangeResearchEnemy.ParentSlamAnimation)
                    Debug.LogError($"{marker}_SLAM_MOVE_FAILED travel={slamTravel} animation={enemy.RainEnemyAnimation}");
                else Debug.Log(marker + "_SLAM_MOVE_CHECKED");
                owner.transform.position = origin + Vector3.forward * 25 + Vector3.up * .15f;
                Physics.SyncTransforms();
                deadline = Time.realtimeSinceStartup + 2;
                while (enemy.RainParentBlinkUntil == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                var model = enemy.transform.Find("mdlParent");
                bool entered = enemy.RainParentBlinkUntil > 0 && model && model.gameObject.activeSelf && visual.ModelRenderingHidden &&
                    !enemyController.enabled && !enemyMotor.enabled && enemy.RangeInvulnerable &&
                    originalBoxes.All(box => !box.GetComponent<Collider>().enabled) &&
                    Vector3.Distance(enemy.CombatAimPoint, aimCollider.transform.TransformPoint(((SphereCollider)aimCollider).center)) < .001f &&
                    enemy.RainEnemyAnimation == RangeResearchEnemy.ParentSlamAnimation && model.GetComponent<Animator>().isActiveAndEnabled;
                if (entered && Environment.GetCommandLineArgs().Contains("--rain-parent-coop-smoke"))
                    yield return new WaitForSeconds(.075f);
                lab.SetMonsterCombat(false);
                bool restored = model && model.gameObject.activeSelf && !visual.ModelRenderingHidden && enemyController.enabled &&
                    originalBoxes.All(box => box.GetComponent<Collider>().enabled) &&
                    enemyMotor.enabled == enemyEnabled && !enemy.RangeInvulnerable && enemy.RainParentBlinkUntil == 0;
                if (!entered || !restored)
                    Debug.LogError($"{marker}_BLINK_CANCEL_FAILED entered={entered} restored={restored}");
                else Debug.Log(marker + "_BLINK_CANCEL_CHECKED");
                yield return new WaitForSeconds(4.1f);
                enemy.transform.position = origin + Vector3.up * .15f;
                owner.transform.position = origin + Vector3.forward * 25 + Vector3.up * .15f;
                Physics.SyncTransforms();
                actor.SetTarget(owner);
                lab.SetMonsterCombat(true);
                deadline = Time.realtimeSinceStartup + 2;
                while (enemy.RainParentBlinkUntil == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                bool enteredAgain = enemy.RainParentBlinkUntil > 0;
                deadline = Time.realtimeSinceStartup + 1;
                while (enemy.RainParentBlinkUntil != 0 && Time.realtimeSinceStartup < deadline) yield return null;
                float travel = Vector3.ProjectOnPlane(enemy.transform.position - origin, Vector3.up).magnitude;
                bool completed = enteredAgain && travel >= 25 && travel <= 35 && model.gameObject.activeSelf && !visual.ModelRenderingHidden &&
                    enemyController.enabled && enemyMotor.enabled == enemyEnabled && !enemy.RangeInvulnerable;
                if (!completed) Debug.LogError($"{marker}_BLINK_EXIT_FAILED entered={enteredAgain} travel={travel}");
                else Debug.Log(marker + "_BLINK_EXIT_CHECKED");
                lab.SetMonsterCombat(false);
                actor.enabled = false;
                bool disabled = originalBoxes.All(box => !box.GetComponent<Collider>().enabled);
                actor.enabled = true;
                bool reenabled = originalBoxes.All(box => box.GetComponent<Collider>().enabled);
                enemy.CurrentHealth = 0;
                yield return null;
                if (!disabled || !reenabled || originalBoxes.Any(box => box.GetComponent<Collider>().enabled))
                    Debug.LogError($"{marker}_SHAPE_LIFECYCLE_FAILED disabled={disabled} reenabled={reenabled}");
                else Debug.Log(marker + "_SHAPE_LIFECYCLE_CHECKED");
                // Allow remote replicas to observe the dead body's disabled hurtboxes before removal.
                yield return new WaitForSeconds(.5f);
            }
            finally
            {
                lab.SetMonsterCombat(false);
                lab.RemoveCatalog(entry);
                if (navigation) navigation.RemoveData();
                Destroy(testTarget);
                Destroy(floor);
                owner.transform.position = ownerPosition;
                owner.MaxHealth = maximum;
                owner.CurrentHealth = health;
                owner.RainHealthRegen = ownerRegen;
                owner.RangeInvulnerable = ownerInvulnerable;
                ownerMotor.enabled = ownerEnabled;
                if (enemyMotor) enemyMotor.enabled = enemyEnabled;
                Physics.SyncTransforms();
            }
            yield return null;
            if (NetworkServer.spawned.ContainsKey(id)) Debug.LogError(marker + "_REMOVE_FAILED");
        }

        IEnumerator VerifyLunarExploderGm(RangeResearchLab lab, CharacterInstance owner)
        {
            const string marker = "RAIN_MONSTER_GM_LUNAR_EXPLODER";
            var entry = new BrowserEntry { id = "LUNAREXPLODER_BODY", category = 1 };
            Vector2 look = owner.lookInput;
            RangeResearchEnemy actor = null;
            for (int direction = 0; direction < 4 && !actor; direction++)
            {
                owner.lookInput = new Vector2(0f, look.y + direction * 90f);
                lab.ExecuteCatalog(entry);
                actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.IsLabSummon && candidate.Archetype == RangeResearchEnemy.LunarExploderKind);
            }
            owner.lookInput = look;
            if (!actor)
            {
                Debug.LogError(marker + "_SPAWN_FAILED");
                yield break;
            }

            var enemy = actor.GetComponent<CharacterInstance>();
            uint enemyId = enemy.netId;
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            bool enemyMotorEnabled = enemyMotor.enabled;
            Vector3 ownerPosition = owner.transform.position;
            int ownerHealth = owner.CurrentHealth;
            var floor = new GameObject("Lunar Exploder smoke floor");
            try
            {
                yield return new WaitForSeconds(RangeResearchEnemy.LunarExploderSpawnDuration + .2f);
                if (enemy.MaxHealth != 180 || !HasRainReplicaMesh(enemy, "mdlLunarExploder") ||
                    enemy.RainEnemyAnimation != 0 || enemy.RainLunarExploderShards.Count != 0 ||
                    owner.CurrentHealth != ownerHealth)
                    Debug.LogError(marker + "_IDLE_FAILED");

                Vector3 start = enemy.transform.position + Vector3.up * 60f;
                floor.transform.position = start + Vector3.down * .5f;
                floor.AddComponent<BoxCollider>().size = new Vector3(64f, 1f, 64f);
                ownerMotor.enabled = enemyMotor.enabled = false;
                enemy.transform.position = start + Vector3.up * .05f;
                owner.transform.position = start + Vector3.forward * 7f + Vector3.up * .05f;
                Physics.SyncTransforms();
                actor.SetTarget(owner);
                lab.SetMonsterCombat(true);
                bool shot = false;
                float deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline && owner.CurrentHealth == ownerHealth)
                {
                    shot |= enemy.RainEnemyAnimation == 2 && enemy.RainLunarExploderShards.Count != 0;
                    yield return null;
                }
                lab.SetMonsterCombat(false);
                if (!shot || ownerHealth - owner.CurrentHealth != 11 || enemy.RainLunarExploderShards.Count != 0)
                    Debug.LogError($"{marker}_SHARD_FAILED shot={shot} damage={ownerHealth - owner.CurrentHealth}");
                else Debug.Log(marker + "_SHARD_CHECKED");

                owner.CurrentHealth = ownerHealth;
                enemy.Server_ChangeHealthStateRaw(enemy.CurrentHealth, CharacterPart.body, AttackType.hitscan, owner, 0);
                deadline = Time.realtimeSinceStartup + 1f;
                while (enemy && enemy.RainTelegraph == 0 && Time.realtimeSinceStartup < deadline) yield return null;
                bool warned = enemy && enemy.RainEnemyAnimation == 8 && enemy.RainTelegraph == 1 &&
                    HasRainReplicaMesh(enemy, "mdlLunarExploder") && owner.CurrentHealth == ownerHealth;
                deadline = Time.realtimeSinceStartup + 2f;
                while (Time.realtimeSinceStartup < deadline && ownerHealth - owner.CurrentHealth < 26) yield return null;
                int zoneDamage = ownerHealth - owner.CurrentHealth;
                bool zoneActive = enemy && enemy.RainTelegraph == RangeResearchEnemy.LunarZoneActiveCue;
                var model = enemy ? enemy.transform.Find("mdlLunarExploder") : null;
                bool hidden = model && !model.gameObject.activeSelf;
                owner.transform.position = start + Vector3.right * 20f + Vector3.up * .05f;
                Physics.SyncTransforms();
                yield return new WaitForSeconds(.45f);
                if (!warned || !zoneActive || !hidden || zoneDamage != 26 || ownerHealth - owner.CurrentHealth != zoneDamage)
                    Debug.LogError($"{marker}_ZONE_FAILED warned={warned} active={zoneActive} hidden={hidden} damage={zoneDamage} afterExit={ownerHealth - owner.CurrentHealth}");
                else Debug.Log(marker + "_ZONE_CHECKED");
            }
            finally
            {
                lab.SetMonsterCombat(false);
                lab.RemoveCatalog(entry);
                Destroy(floor);
                owner.transform.position = ownerPosition;
                owner.CurrentHealth = ownerHealth;
                ownerMotor.enabled = ownerMotorEnabled;
                if (enemyMotor) enemyMotor.enabled = enemyMotorEnabled;
                Physics.SyncTransforms();
            }
            yield return null;
            if (NetworkServer.spawned.ContainsKey(enemyId)) Debug.LogError(marker + "_REMOVE_FAILED");
        }

        IEnumerator VerifyAcidLarvaGm(RangeResearchLab lab, CharacterInstance owner)
        {
            var entry = new BrowserEntry { id = "ACIDLARVA_BODY", category = 1 };
            var ownerLook = owner.lookInput;
            RangeResearchEnemy actor = null;
            for (int direction = 0; direction < 4 && !actor; direction++)
            {
                owner.lookInput = new Vector2(0f, ownerLook.y + direction * 90f);
                lab.ExecuteCatalog(entry);
                actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.IsLabSummon && candidate.Archetype == RangeResearchEnemy.AcidLarvaKind);
            }
            owner.lookInput = ownerLook;
            if (!actor)
            {
                Debug.LogError("RAIN_MONSTER_GM_LARVA_SPAWN_FAILED");
                yield break;
            }

            var enemy = actor.GetComponent<CharacterInstance>();
            uint enemyId = enemy.netId;
            try
            {
                int ownerHealth = owner.CurrentHealth;
                yield return new WaitForSeconds(RangeResearchEnemy.AcidLarvaSpawnDuration + .2f);
                if (!HasRainReplicaMesh(enemy, "mdlAcidLarva") || enemy.RainEnemyAnimation != 0 ||
                    enemy.RainTelegraph != 0 || owner.CurrentHealth != ownerHealth)
                    Debug.LogError("RAIN_MONSTER_GM_LARVA_IDLE_FAILED");
                yield return VerifyAcidLarvaLeap(lab, owner, actor, enemy);
            }
            finally
            {
                lab.RemoveCatalog(entry);
            }
            yield return null;
            if (NetworkServer.spawned.ContainsKey(enemyId)) Debug.LogError("RAIN_MONSTER_GM_LARVA_REMOVE_FAILED");
        }

        IEnumerator VerifyAcidLarvaLeap(RangeResearchLab lab, CharacterInstance owner,
            RangeResearchEnemy actor, CharacterInstance enemy)
        {
            string marker = lab ? "RAIN_MONSTER_GM_LARVA_LEAP" : "RAIN_COOP_LARVA_LEAP";
            int ownerHealth = owner.CurrentHealth;
            int expectedEnemyHealth = enemy.CurrentHealth - Mathf.RoundToInt(enemy.MaxHealth * .25f);
            Vector3 ownerPosition = owner.transform.position;
            Vector3 enemyPosition = enemy.transform.position;
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            var runway = new GameObject("Acid Larva smoke runway");
            try
            {
                actor.SetCombat(false);
                actor.SetTarget(owner);
                Vector3 start = enemyPosition + Vector3.up * 60f;
                runway.transform.position = start + new Vector3(0f, -.5f, 13f);
                runway.AddComponent<BoxCollider>().size = new Vector3(16f, 1f, 36f);
                ownerMotor.enabled = false;
                enemy.transform.position = start + Vector3.up * .05f;
                owner.transform.position = start + Vector3.forward * 20f;
                Physics.SyncTransforms();
                if (lab) lab.SetMonsterCombat(true);
                else actor.SetCombat(true);
                bool cue = false, airborne = false, landed = false;
                float deadline = Time.realtimeSinceStartup + (lab ? 5f : 9f);
                while (enemy && Time.realtimeSinceStartup < deadline)
                {
                    cue |= enemy.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLeapStartAnimation && enemy.RainTelegraph == 1;
                    airborne |= enemy.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLeapLoopAnimation &&
                        enemy.transform.position.y > start.y + 2f;
                    landed |= enemy.RainEnemyAnimation == RangeResearchEnemy.AcidLarvaLandingAnimation;
                    if (landed) break;
                    yield return null;
                }
                if (!cue || !airborne || !landed || (lab && ownerHealth - owner.CurrentHealth != 35) ||
                    !enemy || enemy.CurrentHealth != expectedEnemyHealth)
                    Debug.LogError($"{marker}_FAILED cue={cue} airborne={airborne} landed={landed} damage={ownerHealth - owner.CurrentHealth}");
                else Debug.Log(marker + "_CHECKED");
                if (!lab && landed) yield return new WaitForSeconds(.3f);
            }
            finally
            {
                if (lab) lab.SetMonsterCombat(false);
                else if (actor) actor.SetCombat(false);
                Destroy(runway);
                if (enemy) enemy.transform.position = enemyPosition;
                owner.transform.position = ownerPosition;
                owner.CurrentHealth = ownerHealth;
                ownerMotor.enabled = ownerMotorEnabled;
                Physics.SyncTransforms();
            }
        }

        IEnumerator VerifyBisonCharge(RangeResearchLab lab, CharacterInstance owner,
            CharacterInstance enemy, int health)
        {
            var actor = enemy.GetComponent<RangeResearchEnemy>();
            actor.SetCombat(false);
            actor.SetTarget(owner);
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var ownerController = owner.GetComponent<CharacterController>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            bool ownerControllerEnabled = ownerController.enabled;
            bool enemyMotorEnabled = enemyMotor.enabled;
            float walkSpeed = enemyMotor.WalkSpeed;
            float runSpeed = enemyMotor.RunSpeed;
            Vector3 ownerPosition = owner.transform.position;
            ownerMotor.enabled = false;
            ownerController.enabled = false;
            owner.CurrentHealth = health;
            Vector3 start = enemy.transform.position + Vector3.up * 60f;
            enemy.transform.position = start;
            owner.transform.position = start + Vector3.forward * 14f;

            var runway = new GameObject("Bison smoke runway");
            runway.transform.position = start + new Vector3(0f, -.65f, 9f);
            runway.AddComponent<BoxCollider>().size = new Vector3(12f, 1f, 32f);
            var wall = new GameObject("Bison smoke wall");
            wall.transform.position = start + new Vector3(0f, 3f, 16f);
            wall.AddComponent<BoxCollider>().size = new Vector3(12f, 8f, .5f);
            var target = new GameObject("Bison smoke target");
            target.layer = (int)GameLayers.character;
            target.transform.SetParent(owner.transform, false);
            target.transform.localPosition = Vector3.up;
            var hurtbox = target.AddComponent<SphereCollider>();
            hurtbox.isTrigger = true;
            hurtbox.radius = .6f;
            Physics.SyncTransforms();

            if (lab) lab.SetMonsterCombat(true);
            else
            {
                owner.transform.position = start + Vector3.forward * 2f;
                Physics.SyncTransforms();
                actor.SetCombat(true);
                float headbuttDeadline = Time.realtimeSinceStartup + 6f;
                while (enemy.RainEnemyAnimation != RangeResearchEnemy.BisonHeadbuttAnimation &&
                    Time.realtimeSinceStartup < headbuttDeadline) yield return null;
                if (enemy.RainEnemyAnimation != RangeResearchEnemy.BisonHeadbuttAnimation)
                    Debug.LogError("RAIN_COOP_BISON_HEADBUTT_FAILED");
                else yield return new WaitForSeconds(.35f);
                actor.SetCombat(false);
                enemy.transform.position = start;
                owner.transform.position = start + Vector3.forward * 14f;
                Physics.SyncTransforms();
                actor.SetTarget(owner);
                // Hold the launch distance while an earlier natural charge cools down.
                enemyMotor.enabled = false;
                actor.SetCombat(true);
            }
            bool prep = false, charge = false, stun = false;
            float deadline = Time.realtimeSinceStartup + (lab ? 6f : 12f);
            while (Time.realtimeSinceStartup < deadline)
            {
                byte state = enemy.RainEnemyAnimation;
                prep |= state == RangeResearchEnemy.BisonChargePrepAnimation && enemy.RainTelegraph == 2;
                if (prep && !lab) enemyMotor.enabled = enemyMotorEnabled;
                charge |= state == RangeResearchEnemy.BisonChargeAnimation && enemyMotor.WalkSpeed > walkSpeed;
                stun |= state == RangeResearchEnemy.BisonStunAnimation;
                if (stun) break;
                yield return null;
            }
            if (!prep || !charge || !stun || enemy.transform.position.z - start.z < 5f ||
                (lab && owner.CurrentHealth >= health))
                Debug.LogError($"RAIN_MONSTER_GM_BISON_CHARGE_FAILED prep={prep} charge={charge} stun={stun} damage={health - owner.CurrentHealth}");
            if (!Mathf.Approximately(enemyMotor.WalkSpeed, walkSpeed) || !Mathf.Approximately(enemyMotor.RunSpeed, runSpeed))
                Debug.LogError("RAIN_MONSTER_GM_BISON_STUN_SPEED_FAILED");

            if (lab)
            {
                lab.SetMonsterCombat(false);
                yield return null;
                if (enemy.RainEnemyAnimation != 0 || enemy.RainTelegraph != 0 ||
                    !Mathf.Approximately(enemyMotor.WalkSpeed, walkSpeed) || !Mathf.Approximately(enemyMotor.RunSpeed, runSpeed))
                    Debug.LogError("RAIN_MONSTER_GM_BISON_STOP_FAILED");
            }
            else yield return new WaitForSeconds(.35f);
            enemyMotor.enabled = enemyMotorEnabled;
            Destroy(target);
            Destroy(wall);
            Destroy(runway);
            owner.transform.position = ownerPosition;
            ownerController.enabled = ownerControllerEnabled;
            ownerMotor.enabled = ownerMotorEnabled;
            Physics.SyncTransforms();
        }

        IEnumerator VerifyImpGmBlink(RangeResearchLab lab, CharacterInstance owner, CharacterInstance enemy,
            Vector3 spawnPosition)
        {
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            ownerMotor.enabled = false;
            enemy.transform.position = spawnPosition;
            Physics.SyncTransforms();
            Vector3 direction = Vector3.zero;
            for (int i = 0; i < 8; i++)
            {
                Vector3 candidate = Quaternion.Euler(0f, i * 45f, 0f) * Vector3.forward;
                if (!UnityEngine.AI.NavMesh.SamplePosition(spawnPosition + candidate * 9f, out var landing,
                    2f, UnityEngine.AI.NavMesh.AllAreas)) continue;
                Vector3 feet = landing.position + Vector3.up * .1f;
                if (Physics.CheckCapsule(feet + Vector3.up * .45f, feet + Vector3.up * 1.55f, .45f,
                    GameManager.characterLayer, QueryTriggerInteraction.Ignore)) continue;
                direction = candidate;
                break;
            }
            if (direction == Vector3.zero)
            {
                Debug.LogError("RAIN_MONSTER_GM_IMP_BLINK_NO_TEST_LANDING");
                ownerMotor.enabled = ownerMotorEnabled;
                yield break;
            }
            owner.transform.position = spawnPosition + direction * 18f;
            Physics.SyncTransforms();
            var controller = enemy.GetComponent<CharacterController>();
            bool controllerEnabled = controller.enabled;
            bool invulnerable = enemy.RangeInvulnerable;
            var hitboxes = enemy.GetComponentsInChildren<HitBox>()
                .Select(hitbox => hitbox.GetComponent<Collider>())
                .Where(collider => collider && collider.enabled).ToArray();
            lab.SetMonsterCombat(true);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (enemy.RainEnemyAnimation != RangeResearchEnemy.ImpBlinkAnimation &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (enemy.RainEnemyAnimation != RangeResearchEnemy.ImpBlinkAnimation ||
                !enemy.RangeInvulnerable || controller.enabled || hitboxes.Length == 0 ||
                hitboxes.Any(collider => collider.enabled))
                Debug.LogError("RAIN_MONSTER_GM_IMP_BLINK_ENTER_FAILED");

            lab.SetMonsterCombat(false);
            yield return null;
            if (enemy.RainEnemyAnimation != 0 || enemy.RangeInvulnerable != invulnerable ||
                controller.enabled != controllerEnabled || hitboxes.Any(collider => !collider.enabled))
                Debug.LogError("RAIN_MONSTER_GM_IMP_BLINK_STOP_FAILED");
            ownerMotor.enabled = ownerMotorEnabled;
        }

        IEnumerator VerifyGreaterWispGm(RangeResearchLab lab, CharacterInstance owner)
        {
            var entry = new BrowserEntry { id = "GREATERWISP_BODY", category = 1 };
            Vector3 originalPosition = owner.transform.position;
            Vector2 originalLook = owner.lookInput;
            int originalHealth = owner.CurrentHealth;
            RangeResearchEnemy actor = null;
            for (int direction = 0; direction < 4 && !actor; direction++)
            {
                owner.lookInput = new Vector2(0f, originalLook.y + direction * 90f);
                lab.ExecuteCatalog(entry);
                actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.IsLabSummon &&
                        candidate.Archetype == RangeResearchEnemy.GreaterWispKind);
            }
            owner.lookInput = originalLook;
            if (!actor)
            {
                Debug.LogError("RAIN_MONSTER_GM_GREATER_WISP_SPAWN_FAILED");
                yield break;
            }

            var enemy = actor.GetComponent<CharacterInstance>();
            uint enemyId = enemy.netId;
            Vector3 spawnPosition = enemy.transform.position;
            yield return new WaitForSeconds(RangeResearchEnemy.GreaterWispSpawnDuration + .15f);
            if (enemy.RainEnemyAnimation != 0 || enemy.RainTelegraph != 0 ||
                owner.CurrentHealth != originalHealth || (enemy.transform.position - spawnPosition).sqrMagnitude > .01f ||
                !HasRainReplicaMesh(enemy, "mdlGreaterWisp"))
                Debug.LogError("RAIN_MONSTER_GM_GREATER_WISP_IDLE_OR_MODEL_FAILED");

            var ownerMotor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = ownerMotor.enabled;
            ownerMotor.enabled = false;
            Vector3 firingPosition = spawnPosition + Vector3.up * 60f;
            enemy.transform.position = firingPosition;
            owner.transform.position = firingPosition + Vector3.forward * 22f;
            var backstop = new GameObject("Greater Wisp smoke backstop");
            backstop.transform.position = owner.CombatAimPoint + Vector3.forward * 2f;
            backstop.AddComponent<BoxCollider>().size = new Vector3(8f, 8f, .5f);
            Physics.SyncTransforms();

            lab.SetMonsterCombat(true);
            bool charged = false, fired = false, impact = false, attackAudioObserved = false;
            var attackVisual = enemy.GetComponent<RainActorVisual>();
            int peakShots = 0;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                charged |= enemy.RainEnemyAnimation == 24 && enemy.RainTelegraph == 2;
                fired |= enemy.RainEnemyAnimation == 25;
                attackAudioObserved |= attackVisual && attackVisual.AttackAudioPlaying;
                peakShots = Mathf.Max(peakShots, enemy.RainGreaterWispShots.Count);
                impact |= GameObject.Find("OmniExplosionVFXGreaterWisp(Clone)") != null;
                if (impact && owner.CurrentHealth < originalHealth && enemy.RainGreaterWispShots.Count == 0) break;
                yield return null;
            }
            if (!charged || !fired || peakShots != 2 || !impact || owner.CurrentHealth >= originalHealth)
                Debug.LogError($"RAIN_MONSTER_GM_GREATER_WISP_VOLLEY_FAILED charge={charged} fire={fired} shots={peakShots} impact={impact} damage={originalHealth - owner.CurrentHealth}");
            if (monsterRecoveryPlaytest)
            {
                if (!attackAudioObserved) Debug.LogError("RAIN_MONSTER_RECOVERY_WISP_AUDIO_MISSING");
                Debug.Log($"RAIN_MONSTER_RECOVERY_WISP damage={originalHealth - owner.CurrentHealth} shots={peakShots} impact={impact} audio={attackAudioObserved}");
            }

            deadline = Time.realtimeSinceStartup + 6f;
            while (enemy.RainGreaterWispShots.Count == 0 && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (enemy.RainGreaterWispShots.Count == 0)
                Debug.LogError("RAIN_MONSTER_GM_GREATER_WISP_SECOND_VOLLEY_MISSING");
            lab.SetMonsterCombat(false);
            yield return null;
            if (enemy.RainGreaterWispShots.Count != 0 || enemy.RainTelegraph != 0 || enemy.RainEnemyAnimation != 0)
                Debug.LogError("RAIN_MONSTER_GM_GREATER_WISP_STOP_FAILED");

            Destroy(backstop);
            lab.RemoveCatalog(entry);
            yield return null;
            if (NetworkServer.spawned.ContainsKey(enemyId)) Debug.LogError("RAIN_MONSTER_GM_GREATER_WISP_REMOVE_FAILED");
            owner.transform.position = originalPosition;
            owner.CurrentHealth = originalHealth;
            ownerMotor.enabled = motorEnabled;
            Physics.SyncTransforms();
            Debug.Log("RAIN_MONSTER_GM_CASE_FINISHED GREATERWISP_BODY");
        }

        IEnumerator VerifyQueenWardGm(RangeResearchLab lab, CharacterInstance owner)
        {
            var entry = new BrowserEntry { id = "BEETLEQUEEN_BODY", category = 1 };
            Vector3 originalPosition = owner.transform.position;
            Vector2 originalLook = owner.lookInput;
            int originalHealth = owner.CurrentHealth;
            RangeResearchEnemy queen = null;
            for (int direction = 0; direction < 4 && !queen; direction++)
            {
                owner.lookInput = new Vector2(0f, originalLook.y + direction * 90f);
                lab.ExecuteCatalog(entry);
                queen = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(actor => actor.IsLabSummon && actor.Archetype == RangeResearchEnemy.QueenKind);
            }
            owner.lookInput = originalLook;
            if (!queen)
            {
                Debug.LogError("RAIN_MONSTER_GM_QUEEN_SPAWN_FAILED");
                yield break;
            }

            var enemy = queen.GetComponent<CharacterInstance>();
            uint queenId = enemy.netId;
            yield return new WaitForSeconds(RangeResearchEnemy.QueenSpawnDuration + .15f);
            if (enemy.RainEnemyAnimation != 0 || enemy.RainTelegraph != 0 ||
                owner.CurrentHealth != originalHealth || !HasRainReplicaMesh(enemy, "mdlBeetleQueen"))
                Debug.LogError("RAIN_MONSTER_GM_QUEEN_IDLE_OR_MODEL_FAILED");

            var ownerMotor = owner.GetComponent<CharacterMotor>();
            bool motorWasEnabled = ownerMotor.enabled;
            ownerMotor.enabled = false;
            owner.transform.position = enemy.transform.position + enemy.transform.forward * 6f;
            Physics.SyncTransforms();
            enemy.CurrentHealth = enemy.MaxHealth * 3 / 4;
            lab.SetMonsterCombat(true);

            RainQueenWardHealth ward = null;
            bool sawCast = false;
            float deadline = Time.realtimeSinceStartup + 5f;
            while (Time.realtimeSinceStartup < deadline)
            {
                sawCast |= enemy.RainEnemyAnimation == 23;
                ward = FindObjectsByType<RainQueenWardHealth>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.Team == enemy.Team &&
                        (candidate.transform.position - owner.CombatAimPoint).sqrMagnitude < .25f);
                if (ward && owner.BeetleJuicePenalty > 0f) break;
                yield return null;
            }
            if (!sawCast || !ward || owner.BeetleJuicePenalty <= 0f)
                Debug.LogError($"RAIN_MONSTER_GM_QUEEN_WARD_FAILED cast={sawCast} ward={(bool)ward} juice={owner.BeetleJuicePenalty}");

            uint wardId = ward ? ward.netId : 0;
            lab.RemoveCatalog(entry);
            yield return null;
            if (NetworkServer.spawned.ContainsKey(queenId))
                Debug.LogError("RAIN_MONSTER_GM_QUEEN_REMOVE_FAILED");
            if (wardId != 0 && !NetworkServer.spawned.ContainsKey(wardId))
                Debug.LogError("RAIN_MONSTER_GM_QUEEN_WARD_REMOVED_WITH_QUEEN");
            if (ward)
            {
                var hitbox = ward.GetComponentInChildren<HitBox>();
                if (!hitbox || hitbox._health != ward || ward.CurrentHealth != 80)
                    Debug.LogError("RAIN_MONSTER_GM_QUEEN_WARD_HITBOX_FAILED");
                else
                {
                    hitbox._health.Server_ChangeHealthState(20, hitbox.part, AttackType.hitscan, owner, 0,
                        procCoefficient: 0f);
                    if (ward.CurrentHealth <= 0 || ward.CurrentHealth >= 80)
                        Debug.LogError("RAIN_MONSTER_GM_QUEEN_WARD_DAMAGE_FAILED");
                    hitbox._health.Server_ChangeHealthState(1000, hitbox.part, AttackType.hitscan, owner, 0,
                        procCoefficient: 0f);
                }

                deadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < deadline &&
                    (NetworkServer.spawned.ContainsKey(wardId) || owner.BeetleJuicePenalty > 0f))
                    yield return null;
                if (NetworkServer.spawned.ContainsKey(wardId) || owner.BeetleJuicePenalty > 0f)
                    Debug.LogError("RAIN_MONSTER_GM_QUEEN_WARD_CLEANUP_FAILED");
            }
            owner.transform.position = originalPosition;
            owner.CurrentHealth = originalHealth;
            ownerMotor.enabled = motorWasEnabled;
            Physics.SyncTransforms();
            Debug.Log("RAIN_MONSTER_GM_CASE_FINISHED BEETLEQUEEN_BODY");
        }

        IEnumerator VerifyJellyfishGm(RangeResearchLab lab, CharacterInstance owner, bool evade)
        {
            var entry = new BrowserEntry { id = "JELLYFISH_BODY", category = 1 };
            Vector3 originalPosition = owner.transform.position;
            Vector2 originalLook = owner.lookInput;
            int health = owner.CurrentHealth;
            RangeResearchEnemy actor = null;
            for (int direction = 0; direction < 4 && !actor; direction++)
            {
                owner.lookInput = new Vector2(0f, originalLook.y + direction * 90f);
                lab.ExecuteCatalog(entry);
                actor = FindObjectsByType<RangeResearchEnemy>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.IsLabSummon &&
                        candidate.Archetype == RangeResearchEnemy.JellyfishKind);
            }
            owner.lookInput = originalLook;
            if (!actor)
            {
                Debug.LogError("RAIN_MONSTER_GM_JELLYFISH_SPAWN_FAILED");
                yield break;
            }

            var enemy = actor.GetComponent<CharacterInstance>();
            uint netId = enemy.netId;
            Vector3 spawn = enemy.transform.position;
            var motor = owner.GetComponent<CharacterMotor>();
            bool motorEnabled = motor.enabled;
            motor.enabled = false;
            owner.transform.position = spawn + enemy.transform.forward * 3f;
            Physics.SyncTransforms();
            yield return new WaitForSeconds(1.75f);
            if (!HasRainReplicaMesh(enemy, "mdlJellyfish") || enemy.RainEnemyAnimation != 0 ||
                enemy.RainTelegraph != 0 || owner.CurrentHealth != health ||
                (enemy.transform.position - spawn).sqrMagnitude > .01f)
                Debug.LogError("RAIN_MONSTER_GM_JELLYFISH_IDLE_FAILED");

            lab.SetMonsterCombat(true);
            float deadline = Time.realtimeSinceStartup + 2f;
            while (enemy && (enemy.RainEnemyAnimation != 14 || enemy.RainTelegraph != 1) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            bool cue = enemy && enemy.RainEnemyAnimation == 14 && enemy.RainTelegraph == 1;
            if (evade && cue)
            {
                owner.transform.position += Vector3.up * 25f;
                Physics.SyncTransforms();
            }

            deadline = Time.realtimeSinceStartup + 2f;
            while (NetworkServer.spawned.ContainsKey(netId) && Time.realtimeSinceStartup < deadline)
                yield return null;
            bool removed = !NetworkServer.spawned.ContainsKey(netId);
            int damage = health - owner.CurrentHealth;
            if (!cue || !removed || (evade ? damage != 0 : damage <= 0))
                Debug.LogError($"RAIN_MONSTER_GM_JELLYFISH_FAILED evade={evade} cue={cue} removed={removed} damage={damage}");
            else Debug.Log($"RAIN_MONSTER_GM_JELLYFISH_CHECKED evade={evade} damage={damage}");

            lab.SetMonsterCombat(false);
            if (!removed) lab.RemoveCatalog(entry);
            owner.transform.position = originalPosition;
            owner.CurrentHealth = health;
            motor.enabled = motorEnabled;
            Physics.SyncTransforms();
        }

        IEnumerator VerifyBruiserGmVolley(RangeResearchLab lab, CharacterInstance owner,
            CharacterInstance enemy, int health)
        {
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            bool enemyMotorEnabled = enemyMotor.enabled;
            ownerMotor.enabled = false;
            enemyMotor.enabled = false;
            Vector3 arena = enemy.transform.position + Vector3.up * 12f;
            GameObject floor = null;
            if (monsterRecoveryPlaytest)
            {
                floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
                floor.transform.position = arena - Vector3.up * .5f + Vector3.forward * 16f;
                floor.transform.localScale = new Vector3(12f, 1f, 40f);
                arena += Vector3.up * .15f;
            }
            enemy.transform.position = arena;
            owner.transform.position = arena + Vector3.forward * 32f;
            owner.CurrentHealth = health;
            Physics.SyncTransforms();
            if (floor) enemyMotor.enabled = enemyMotorEnabled;

            lab.SetMonsterCombat(true);
            float deadline = Time.realtimeSinceStartup + 4f;
            bool cue = false, impact = false, audio = false;
            var attackVisual = enemy.GetComponent<RainActorVisual>();
            int peakShots = 0;
            while (Time.realtimeSinceStartup < deadline)
            {
                cue |= enemy.RainEnemyAnimation == 18 && enemy.RainTelegraph == 2;
                peakShots = Mathf.Max(peakShots, enemy.RainBruiserFireballs.Count);
                impact |= GameObject.Find("OmniExplosionVFXLemurianBruiserFireballImpact(Clone)") != null;
                audio |= attackVisual && attackVisual.AttackAudioPlaying &&
                    attackVisual.SelectedAttackSound && attackVisual.SelectedAttackSound.name == "OriginalFireball";
                if (peakShots == 5 && impact && owner.CurrentHealth < health) break;
                yield return null;
            }
            int damage = health - owner.CurrentHealth;
            if (!cue || peakShots != 5 || !impact || damage <= 0)
                Debug.LogError($"RAIN_MONSTER_GM_BRUISER_VOLLEY_FAILED cue={cue} shots={peakShots} impact={impact} damage={damage}");
            else Debug.Log($"RAIN_MONSTER_GM_BRUISER_VOLLEY_CHECKED shots={peakShots} damage={damage}");
            if (floor)
            {
                Vector3 displacement = enemy.transform.position - arena;
                bool moved = displacement.x * displacement.x + displacement.z * displacement.z >= .04f;
                if (!moved || !audio) Debug.LogError($"RAIN_MONSTER_RECOVERY_BRUISER_VOLLEY_MISSING moved={moved} audio={audio}");
                Debug.Log($"RAIN_MONSTER_RECOVERY_BRUISER_VOLLEY moved={moved} shots={peakShots} damage={damage} cue={cue} impact={impact} audio={audio}");
                Destroy(floor);
            }

            lab.SetMonsterCombat(false);
            if (enemy.RainBruiserFireballs.Count != 0)
                Debug.LogError("RAIN_MONSTER_GM_BRUISER_PROJECTILES_NOT_CLEARED");
            ownerMotor.enabled = ownerMotorEnabled;
            enemyMotor.enabled = enemyMotorEnabled;
        }

        IEnumerator VerifyNewMonsterGmProjectile(RangeResearchLab lab, CharacterInstance owner,
            CharacterInstance enemy, bool guard, int health)
        {
            var ownerMotor = owner.GetComponent<CharacterMotor>();
            var enemyMotor = enemy.GetComponent<CharacterMotor>();
            bool ownerMotorEnabled = ownerMotor.enabled;
            bool enemyMotorEnabled = enemyMotor.enabled;
            ownerMotor.enabled = false;
            enemyMotor.enabled = false;
            Vector3 arena = enemy.transform.position + Vector3.up * 12f;
            enemy.transform.position = arena;
            owner.transform.position = arena + Vector3.forward * 20f;
            owner.CurrentHealth = health;
            Physics.SyncTransforms();

            lab.SetMonsterCombat(true);
            float deadline = Time.realtimeSinceStartup + 4f;
            bool warning = false, projectile = false;
            while (owner.CurrentHealth == health && Time.realtimeSinceStartup < deadline)
            {
                warning |= enemy.RainTelegraph == 2 && enemy.RainEnemyAnimation == (guard ? 6 : 10);
                projectile |= enemy.RainTelegraph == 3;
                yield return null;
            }
            int damage = health - owner.CurrentHealth;
            if (!warning || !projectile || damage <= 0)
                Debug.LogError($"RAIN_MONSTER_GM_PROJECTILE_FAILED guard={guard} warning={warning} projectile={projectile} damage={damage}");
            else Debug.Log($"RAIN_MONSTER_GM_PROJECTILE_CHECKED guard={guard} damage={damage}");

            lab.SetMonsterCombat(false);
            ownerMotor.enabled = ownerMotorEnabled;
            enemyMotor.enabled = enemyMotorEnabled;
        }

        static bool HasRainReplicaMesh(CharacterInstance enemy, string modelName)
        {
            var model = enemy.transform.Find(modelName);
            var renderer = model ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            return renderer && renderer.sharedMesh;
        }

        IEnumerator VerifyBeetleHeadbutt(CharacterInstance owner, CharacterInstance dummy)
        {
            // Isolate the attack from arena props and bot movement.
            Vector3 originalPosition = dummy.transform.position;
            Vector3 arena = originalPosition + Vector3.up * 12f;
            var beetle = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), arena,
                Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in beetle.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            beetle.Team = owner.Team;
            NetworkServer.Spawn(beetle.gameObject);
            beetle.GetComponent<CharacterMotor>().enabled = false;
            var actor = beetle.gameObject.AddComponent<RangeResearchEnemy>();
            actor.Configure(dummy, 0);
            actor.OwnsCharacter = true;
            dummy.transform.position = arena + Vector3.forward * 2f;
            yield return null;

            int initialHealth = dummy.CurrentHealth;
            actor.SetCombat(true);
            yield return new WaitForSeconds(1.1f);
            if (dummy.CurrentHealth != initialHealth - 12)
                Debug.LogError("RANGE_SMOKE_BEETLE_FRONT_DAMAGE " + (initialHealth - dummy.CurrentHealth));

            actor.SetCombat(true);
            yield return new WaitForSeconds(.25f);
            dummy.transform.position = arena - Vector3.forward * 2f;
            yield return new WaitForSeconds(.85f);
            if (dummy.CurrentHealth != initialHealth - 12)
                Debug.LogError("RANGE_SMOKE_BEETLE_REAR_DODGE");

            dummy.transform.position = arena + Vector3.forward * 2f;
            actor.SetCombat(true);
            yield return new WaitForSeconds(.25f);
            var dummyItems = dummy.gameObject.AddComponent<RangeResearchItems>();
            int stunIndex = RangeResearchItems.CatalogIndex("StunChanceOnHit");
            for (int i = 0; i < 20; i++) dummyItems.Grant(stunIndex);
            var previousRandom = UnityEngine.Random.state;
            UnityEngine.Random.State hitRandom;
            do
            {
                hitRandom = UnityEngine.Random.state;
                _ = UnityEngine.Random.value; // The damage calculation rolls crit before stun.
            } while (UnityEngine.Random.value >= .5f);
            UnityEngine.Random.state = hitRandom;
            beetle.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, dummy, 0);
            UnityEngine.Random.state = previousRandom;
            yield return new WaitForSeconds(1f);
            if (!actor.IsStunned || dummy.CurrentHealth != initialHealth - 12)
                Debug.LogError("RANGE_SMOKE_STUN_INTERRUPT_FAILED");
            yield return new WaitForSeconds(2f);
            if (actor.IsStunned || dummy.CurrentHealth >= initialHealth - 12)
                Debug.LogError("RANGE_SMOKE_STUN_RECOVERY_FAILED");
            else Debug.Log("RANGE_SMOKE_STUN_GRENADE_CHECKED");
            Destroy(dummyItems);

            uint beetleId = beetle.netId;
            var overlay = UI.HUD.ReferenceBattleHud.MonsterOverlay;
            string barName = "Summon health " + beetleId;
            var bar = overlay ? overlay.Find(barName) as RectTransform : null;
            var fill = bar ? bar.Find("Health fill") as RectTransform : null;
            beetle.Server_ChangeHealthStateRaw(beetle.CurrentHealth / 2, CharacterPart.body, AttackType.melee, dummy, 0);
            yield return null;
            yield return null;
            float expected = bar ? (bar.rect.width - 2) * beetle.CurrentHealth / beetle.MaxHealth : -1f;
            if (!fill || Mathf.Abs(fill.rect.width - expected) > .01f)
                Debug.LogError("RANGE_SMOKE_BEETLE_HEALTH_BAR");
            beetle.Server_ChangeHealthStateRaw(beetle.CurrentHealth, CharacterPart.body, AttackType.melee, dummy, 0);
            yield return null;
            yield return null;
            if (beetle || NetworkServer.spawned.ContainsKey(beetleId) || (overlay && overlay.Find(barName)))
                Debug.LogError("RANGE_SMOKE_BEETLE_DEATH_LEAK");
            dummy.transform.position = originalPosition;
            dummy.CurrentHealth = initialHealth;
            Debug.Log("RANGE_SMOKE_BEETLE_HEADBUTT_CHECKED");
        }
        IEnumerator VerifyTitanBeam(CharacterInstance owner, CharacterInstance dummy)
        {
            Vector3 originalPosition = dummy.transform.position;
            int initialHealth = dummy.CurrentHealth;
            Vector3 arena = originalPosition + Vector3.up * 18f;
            var titan = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(), arena,
                Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in titan.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            titan.Team = owner.Team;
            NetworkServer.Spawn(titan.gameObject);
            titan.GetComponent<CharacterMotor>().enabled = false;
            var actor = titan.gameObject.AddComponent<RangeResearchEnemy>();
            actor.Configure(dummy, 2, RangeResearchLab.MonsterVariant("TITANGOLD_BODY"));
            var model = titan.transform.Find("mdlTitan");
            var goldRenderer = model ? model.GetComponentInChildren<SkinnedMeshRenderer>() : null;
            if (!goldRenderer || !goldRenderer.sharedMesh || goldRenderer.sharedMesh.name != RangeResearchEnemy.GoldTitanVariant)
                Debug.LogError("RANGE_SMOKE_GOLD_TITAN_MESH");
            else Debug.Log("RANGE_SMOKE_GOLD_TITAN_READY");
            var sword = model ? Array.Find(model.GetComponentsInChildren<MeshFilter>(),
                filter => filter.name == "Titan gold sword") : null;
            if (!sword || !sword.sharedMesh || sword.sharedMesh.name != "TitanGoldSwordMesh" ||
                sword.transform.parent.name != "hand.r")
                Debug.LogError("RANGE_SMOKE_GOLD_TITAN_SWORD");
            else Debug.Log("RANGE_SMOKE_GOLD_TITAN_SWORD_READY");
            actor.OwnsCharacter = true;
            dummy.transform.position = arena + Vector3.forward * 8f;
            Physics.SyncTransforms();
            actor.SetCombat(true);

            float deadline = Time.time + 8f;
            while (Time.time < deadline && dummy.CurrentHealth == initialHealth) yield return null;
            var fist = titan.transform.Find("Titan ground fist");
            if (dummy.CurrentHealth != initialHealth - 40 || !fist || !fist.gameObject.activeSelf ||
                !fist.TryGetComponent<MeshFilter>(out var fistMesh) || !fistMesh.sharedMesh)
                Debug.LogError("RANGE_SMOKE_TITAN_FIST_IMPACT");
            while (Time.time < deadline && dummy.CurrentHealth > initialHealth - 50) yield return null;
            if (fist && fist.gameObject.activeSelf) Debug.LogError("RANGE_SMOKE_TITAN_FIST_CLEANUP");
            int beamHealth = dummy.CurrentHealth;
            if (beamHealth != initialHealth - 50) Debug.LogError("RANGE_SMOKE_TITAN_BEAM_TICKS " + (initialHealth - beamHealth));

            var cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cover.transform.position = arena + Vector3.forward * 4f + Vector3.up * 2.5f;
            cover.transform.localScale = new Vector3(3, 5, 1);
            Physics.SyncTransforms();
            yield return new WaitForSeconds(.4f);
            if (dummy.CurrentHealth != beamHealth) Debug.LogError("RANGE_SMOKE_TITAN_BEAM_COVER");

            actor.SetCombat(false);
            cover.SetActive(false);
            Destroy(cover);
            yield return new WaitForSeconds(.4f);
            if (dummy.CurrentHealth != beamHealth) Debug.LogError("RANGE_SMOKE_TITAN_BEAM_STOP");
            actor.RemoveFromRange();
            dummy.transform.position = originalPosition;
            dummy.CurrentHealth = initialHealth;
            Physics.SyncTransforms();
            Debug.Log("RANGE_SMOKE_TITAN_BEAM_CHECKED");
        }
        void OnDestroy(){Application.logMessageReceived-=OnLog;}
    }
}
