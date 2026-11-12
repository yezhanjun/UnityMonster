#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

namespace Jotunnslayer
{
    // Explicit development-player switches only. Files coordinate readiness, never replicate gameplay.
    internal sealed partial class CoopSkillValidation : MonoBehaviour
    {
        const string TargetName = "Jotunn skill coop validation target";
        const int TargetHealth = 12345;
        bool client, failed;
        string signalDirectory;
        string scenario = "missile";
        bool Heart => scenario == "heart-evolution";
        bool Evolved => scenario != "missile";
        string TrailName => Heart ? "Raid Heart Seeker trail" : "Raid Magic Missile trail";
        readonly HashSet<uint> remoteProjectiles = new HashSet<uint>();
        float watchdog;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var args = Environment.GetCommandLineArgs();
            bool isClient = Array.IndexOf(args, "--jotunn-skill-client") >= 0;
            if (!Debug.isDebugBuild || (!isClient && Array.IndexOf(args, "--jotunn-skill-host") < 0)) return;
            Application.runInBackground = true;
            var root = new GameObject("Jotunn skill coop validation");
            DontDestroyOnLoad(root);
            var runner = root.AddComponent<CoopSkillValidation>();
            runner.client = isClient;
            const string option = "--jotunn-skill-signals=";
            string argument = Array.Find(args, value => value.StartsWith(option, StringComparison.Ordinal));
            runner.signalDirectory = argument?.Substring(option.Length);
            const string scenarioOption = "--jotunn-skill-scenario=";
            string scenarioArgument = Array.Find(args, value => value.StartsWith(scenarioOption, StringComparison.Ordinal));
            if (scenarioArgument != null) runner.scenario = scenarioArgument.Substring(scenarioOption.Length);
        }

        void OnDestroy() => MagicMissileTrails.ValidationReceived -= OnRemoteProjectile;

        void OnRemoteProjectile(uint id, bool finished, bool heartArrow)
        {
            if (!finished && heartArrow == Heart) remoteProjectiles.Add(id);
        }

        void Update()
        {
            if (watchdog > 0 && Time.realtimeSinceStartup > watchdog)
            {
                watchdog = 0;
                Fail("Overall validation timeout");
                if (NetworkServer.active) DNNetworkManager.Instance.StopHost();
                else if (NetworkClient.active) DNNetworkManager.Instance.StopClient();
                Application.Quit(1);
            }
        }

        void Fail(string reason)
        {
            failed = true;
            Debug.LogError("JOTUNN_SKILL_COOP_FAILED " + reason);
        }

        void WriteSignal(string name, string value)
        {
            string path = Path.Combine(signalDirectory, name);
            File.WriteAllText(path + ".tmp", value);
            File.Move(path + ".tmp", path);
        }

        static bool TryReadSignal(string path, out string value)
        {
            value = null;
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                    FileShare.ReadWrite | FileShare.Delete);
                using var reader = new StreamReader(stream);
                value = reader.ReadToEnd();
                return true;
            }
            catch (IOException) { return false; } // Creation/rename sharing windows: retry on the next frame.
        }

        static bool TryFindOpenArenaPosition(Vector3 fallback, out Vector3 position)
        {
            var mesh = NavMesh.CalculateTriangulation();
            for (int i = -3; i < mesh.indices.Length; i += 3)
            {
                Vector3 candidate = i < 0 ? fallback : (mesh.vertices[mesh.indices[i]] +
                    mesh.vertices[mesh.indices[i + 1]] + mesh.vertices[mesh.indices[i + 2]]) / 3f;
                Vector3 origin = candidate + Vector3.up;
                if (Physics.CheckSphere(origin, .6f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore) ||
                    Physics.SphereCast(origin, .6f, Vector3.up, out _, 7f,
                        GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) continue;
                bool open = true;
                for (int direction = 0; direction < 6; direction++)
                    if (Physics.SphereCast(origin, .6f, Quaternion.AngleAxis(direction * 60, Vector3.up) * Vector3.forward,
                        out _, 8f, GameManager.environmentLayer, QueryTriggerInteraction.Ignore)) { open = false; break; }
                if (!open) continue;
                position = candidate;
                return true;
            }
            position = fallback;
            return false;
        }

        IEnumerator Start()
        {
            watchdog = Time.realtimeSinceStartup + 180;
            if (scenario != "missile" && scenario != "missile-evolution" && scenario != "heart-evolution" &&
                scenario != "remote-owner" && scenario != "remote-rewards" && scenario != "remote-dash" && scenario != "remote-attack-speed")
            {
                Fail("Unknown scenario " + scenario);
                Application.Quit(1);
                yield break;
            }
            if (string.IsNullOrEmpty(signalDirectory))
            {
                Fail("Missing unique signal directory");
                Application.Quit(1);
                yield break;
            }
            Directory.CreateDirectory(signalDirectory);
            yield return new WaitForSecondsRealtime(3);
            var manager = DNNetworkManager.Instance;
            if (!manager || !manager.GetComponent<DNTransportImplementer>().RestoreLocalTransport())
            {
                Fail("Local transport unavailable");
                Application.Quit(1);
                yield break;
            }
            manager.SetTransportPort(27964);
            yield return scenario == "remote-owner" || scenario == "remote-rewards" || scenario == "remote-dash" || scenario == "remote-attack-speed"
                ? (client ? RunRemoteOwnerClient(manager) : RunRemoteOwnerHost(manager))
                : (client ? RunClient(manager) : RunHost(manager));
            Debug.Log(failed ? "JOTUNN_SKILL_COOP_FAILED" : client ?
                "JOTUNN_SKILL_COOP_CLIENT_OK" : "JOTUNN_SKILL_COOP_HOST_OK");
            Application.Quit(failed ? 1 : 0);
        }

        IEnumerator JoinCharacter()
        {
            float deadline = Time.realtimeSinceStartup + 45;
            bool requested = false;
            while (Time.realtimeSinceStartup < deadline)
            {
                var player = NetworkClient.localPlayer ? NetworkClient.localPlayer.GetComponent<PlayerInstance>() : null;
                if (player)
                {
                    if (player.MyCharacter) yield break;
                    if (!requested && player.Team == -1 && GameManager.Gamemode is TeamDeathmatch)
                    {
                        requested = true;
                        int blue = 0, orange = 0;
                        foreach (var candidate in GameManager.Players.Values)
                        {
                            if (candidate.Team == 0) blue++;
                            if (candidate.Team == 1) orange++;
                        }
                        player.ClientRequestJoiningTeam(blue <= orange ? 0 : 1);
                    }
                }
                yield return null;
            }
            Fail("Timed out joining a formal character");
        }

        IEnumerator RunHost(DNNetworkManager manager)
        {
            var creator = FindFirstObjectByType<RoomCreator>();
            int map = creator ? Array.FindIndex(creator.Maps, candidate => candidate &&
                Path.GetFileNameWithoutExtension(candidate.Scene) == "map_arena") : -1;
            if (map < 0) { Fail("Formal arena unavailable"); yield break; }
            if (creator.BotsToggle) creator.BotsToggle.isOn = false;
            var profile = new SkillProgress(SkillCatalog.Load(), null, 10);
            if (!profile.TryLearn(Heart ? SkillBattlePreview.HeartUnlock : SkillBattlePreview.MissileUnlock) ||
                Evolved && !profile.TryLearn(Heart ? SkillBattlePreview.HeartEvolution : SkillBattlePreview.MissileEvolution))
            { Fail("In-memory skill unlock failed"); yield break; }
            SkillPlayground.ValidationMatchProgress = profile;
            var raidClass = Heart ? RaidClass.Ranger : RaidClass.Seeress;
            SkillPlayground.LaunchMatch(creator, map, 0, raidClass);
            // The new frontend has no legacy BotsToggle; ConfigureRoom otherwise defaults to filling bots.
            // Scene loading is asynchronous, so set the test room policy before RoomManager starts.
            RoomSetup.Properties.P_FillEmptySlotsWithBots = false;
            yield return JoinCharacter();
            if (failed) yield break;
            var owner = NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter;
            var battle = FindFirstObjectByType<SkillBattlePreview>();
            if (!battle || !battle.LiveMatch || battle.SelectedRaidClass != raidClass)
            { Fail("Formal skill runtime not bound"); yield break; }
            if (RoomSetup.Properties.P_FillEmptySlotsWithBots || GameManager.Players.Count != 1)
            { Fail("Fixture requires exactly one host and no bots before client joins"); manager.StopHost(); yield break; }
            if (!TryFindOpenArenaPosition(owner.transform.position, out var openPosition))
            { Fail("No unobstructed real arena floor for projectile fixture"); manager.StopHost(); yield break; }
            owner.GetComponent<CharacterMotor>().enabled = false;
            owner.transform.SetPositionAndRotation(openPosition, Quaternion.identity);
            Debug.Log("JOTUNN_SKILL_COOP_OPEN_ARENA position=" + openPosition);
            var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                owner.transform.position - (Heart ? Vector3.forward : owner.transform.forward) * 3, Quaternion.identity).GetComponent<CharacterInstance>();
            foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
            target.CharacterName = TargetName;
            target.Team = owner.Team == 0 ? 1 : 0;
            target.MaxHealth = target.CurrentHealth = TargetHealth;
            NetworkServer.Spawn(target.gameObject);
            target.GetComponent<CharacterMotor>().enabled = false;
            bool foreignDamage = false;
            int previousHealth = TargetHealth, targetHits = 0;
            target.Server_OnDamaged += (health, part, type, attacker, force) =>
            {
                int damage = previousHealth - health;
                previousHealth = health;
                targetHits++;
                foreignDamage |= attacker != owner || type != AttackType.explosion ||
                    (Heart ? damage != 20 && damage != 2 : Evolved ? damage != 30 && damage != 12 : damage != 8);
                Debug.Log($"JOTUNN_SKILL_COOP_TARGET_DAMAGED hp={health} damage={damage} type={type} " +
                    $"owner={attacker == owner} foreign={foreignDamage}");
            };
            if (Heart)
            {
                for (int i = 1; i <= 3; i++)
                {
                    var bounce = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                        target.transform.position - Vector3.forward * (i * 1.5f), Quaternion.identity).GetComponent<CharacterInstance>();
                    foreach (var slot in bounce.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                    bounce.Team = target.Team;
                    bounce.MaxHealth = bounce.CurrentHealth = TargetHealth;
                    NetworkServer.Spawn(bounce.gameObject);
                    bounce.GetComponent<CharacterMotor>().enabled = false;
                }
            }
            WriteSignal("host-ready", target.netId.ToString());
            Debug.Log($"JOTUNN_SKILL_COOP_HOST_READY target={target.netId} hp={target.CurrentHealth} max={target.MaxHealth} " +
                $"bots={RoomSetup.Properties.P_FillEmptySlotsWithBots} players={GameManager.Players.Count}");
            float deadline = Time.realtimeSinceStartup + 60;
            while (!File.Exists(Path.Combine(signalDirectory, "client-ready")) && Time.realtimeSinceStartup < deadline)
                yield return null;
            if (!File.Exists(Path.Combine(signalDirectory, "client-ready")))
            { Fail("Remote client readiness timeout"); manager.StopHost(); yield break; }
            int parents = 0, shards = 0, arrows = 0, maxHits = 0, bleedTicks = 0, completedBleeds = 0;
            bool invalidHit = false;
            var hitsByArrow = new Dictionary<uint, HashSet<uint>>();
            battle.ValidationProjectileTrace = message =>
            {
                Debug.Log("JOTUNN_SKILL_COOP_PROJECTILE " + message);
                if (message.StartsWith("queued "))
                {
                    if (message.Contains("damage=30 shatters=True")) parents++;
                    if (message.Contains("damage=12 shatters=False")) shards++;
                }
                if (message.StartsWith("arrow launch ") && message.Contains("damage=20 limit=3")) arrows++;
            };
            battle.ValidationRangerHit = (arrow, victim, damage) =>
            {
                if (!hitsByArrow.TryGetValue(arrow, out var hits))
                { hits = new HashSet<uint>(); hitsByArrow.Add(arrow, hits); }
                invalidHit |= !hits.Add(victim) || damage != 20;
                maxHits = Mathf.Max(maxHits, hits.Count);
                invalidHit |= hits.Count > 3;
            };
            battle.ValidationBleedTick = (remaining, damage) =>
            {
                bleedTicks++;
                if (remaining == 1) completedBleeds++;
                invalidHit |= damage != 2;
            };
            if (Evolved) battle.SetRaidLevel(22);
            int chosen = 0;
            while (Heart ? battle.TryChooseHeartseeker() : battle.TryChoose(true)) chosen++;
            if (chosen != (Evolved ? 4 : 1))
            { Fail("Formal skill ranks unavailable: " + chosen); manager.StopHost(); yield break; }
            deadline = Time.realtimeSinceStartup + 20;
            bool ServerProof() => !Evolved || (Heart ? arrows >= 6 && maxHits == 3 &&
                completedBleeds > 0 && bleedTicks >= 4 : parents > 0 && shards >= 17 && target.CurrentHealth <= TargetHealth - 42);
            while ((!ServerProof() || !File.Exists(Path.Combine(signalDirectory, "client-observed"))) && Time.realtimeSinceStartup < deadline)
                yield return null;
            battle.ValidationProjectileTrace = null;
            battle.ValidationRangerHit = null;
            battle.ValidationBleedTick = null;
            battle.enabled = false;
            if (!ServerProof() || invalidHit || foreignDamage || !File.Exists(Path.Combine(signalDirectory, "client-observed")) || target.CurrentHealth >= TargetHealth)
                Fail("Remote observations or authoritative damage missing");
            else Debug.Log($"JOTUNN_SKILL_COOP_SERVER_DAMAGE_OK scenario={scenario} damage={TargetHealth - target.CurrentHealth} " +
                $"parents={parents} shards={shards} arrows={arrows} maxHits={maxHits} bleedTicks={bleedTicks} completedBleeds={completedBleeds} " +
                $"targetHits={targetHits} foreign={foreignDamage}");
            WriteSignal("server-health", target.CurrentHealth.ToString());
            deadline = Time.realtimeSinceStartup + 10;
            string synchronizedHealth = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                if (TryReadSignal(Path.Combine(signalDirectory, "client-synced"), out synchronizedHealth)) break;
                yield return null;
            }
            if (synchronizedHealth != target.CurrentHealth.ToString())
                Fail("Final remote health differs from authoritative server");
            else Debug.Log("JOTUNN_SKILL_COOP_HEALTH_EQUAL_OK hp=" + target.CurrentHealth);
            manager.StopHost();
            deadline = Time.realtimeSinceStartup + 20;
            while ((NetworkServer.active || SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (NetworkServer.active || NetworkClient.active || FindFirstObjectByType<SkillBattlePreview>() ||
                SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene)
                Fail("Host teardown left network or skill runtime");
        }

        IEnumerator RunClient(DNNetworkManager manager)
        {
            manager.networkAddress = "127.0.0.1";
            RangeCharacterSelection.SetSelected(0);
            manager.StartClient();
            yield return JoinCharacter();
            if (failed) yield break;
            float deadline = Time.realtimeSinceStartup + 25;
            Health target = null;
            while (!target && Time.realtimeSinceStartup < deadline)
            {
                foreach (var identity in NetworkClient.spawned.Values)
                    if (identity && identity.TryGetComponent<Health>(out var health) && health.CharacterName == TargetName)
                    { target = health; break; }
                yield return null;
            }
            if (!target || target.CurrentHealth != TargetHealth || NetworkServer.active)
            {
                Fail($"Client baseline target or authority invalid: target={target != null} hp={(target ? target.CurrentHealth : -1)} " +
                    $"max={(target ? target.MaxHealth : -1)} server={NetworkServer.active} scene={SceneManager.GetActiveScene().name}");
                yield break;
            }
            MagicMissileTrails.ValidationReceived += OnRemoteProjectile;
            WriteSignal("client-ready", target.netId.ToString());
            bool sawTrail = false, sawDamage = false;
            deadline = Time.realtimeSinceStartup + 25;
            int requiredProjectiles = Heart ? 6 : Evolved ? 18 : 1;
            while ((!sawTrail || !sawDamage || remoteProjectiles.Count < requiredProjectiles) && NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
            {
                foreach (var trail in FindObjectsByType<TrailRenderer>(FindObjectsSortMode.None))
                    if (trail.name == TrailName && trail.positionCount >= 2) sawTrail = true;
                sawDamage |= target && target.CurrentHealth <= TargetHealth - (Heart ? 20 : Evolved ? 42 : 1);
                yield return null;
            }
            if (!sawTrail || !sawDamage || remoteProjectiles.Count < requiredProjectiles)
            { Fail("Remote trail=" + sawTrail + " damage=" + sawDamage + " projectiles=" + remoteProjectiles.Count); yield break; }
            Debug.Log($"JOTUNN_SKILL_COOP_REMOTE_OBSERVED scenario={scenario} trail=True damage={TargetHealth - target.CurrentHealth} projectiles={remoteProjectiles.Count}");
            WriteSignal("client-observed", "observed");
            deadline = Time.realtimeSinceStartup + 25;
            string finalHealthPath = Path.Combine(signalDirectory, "server-health");
            int expectedHealth = -1;
            while (NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
            {
                if (TryReadSignal(finalHealthPath, out var healthText) && int.TryParse(healthText, out expectedHealth) &&
                    target && target.CurrentHealth == expectedHealth) break;
                yield return null;
            }
            if (!target || expectedHealth < 0 || target.CurrentHealth != expectedHealth)
            { Fail("Final replicated health did not converge"); yield break; }
            WriteSignal("client-synced", target.CurrentHealth.ToString());
            Debug.Log("JOTUNN_SKILL_COOP_REMOTE_HEALTH_EQUAL_OK hp=" + target.CurrentHealth);
            deadline = Time.realtimeSinceStartup + 25;
            while (NetworkClient.active && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1);
            MagicMissileTrails.ValidationReceived -= OnRemoteProjectile;
            if (NetworkClient.active || target || GameObject.Find(TrailName) ||
                SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene)
                Fail("Client teardown left network, target or trail");
            else Debug.Log("JOTUNN_SKILL_COOP_REMOTE_TEARDOWN_OK");
        }
    }
}
#endif
