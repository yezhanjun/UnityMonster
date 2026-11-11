#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.IO;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jotunnslayer
{
    internal sealed partial class CoopSkillValidation
    {
        bool RewardManagement => scenario == "remote-rewards";
        RaidClass RemoteClass => RewardManagement ? RaidClass.Seeress : RaidClass.Warden;
        int RemoteFood => RewardManagement ? 50 : 54;
        int managementPending;
        static readonly string[] AttackSpeedNodes = { "virtue_warden_01_a", "virtue_freya_01_a", "virtue_freya_01_b",
            "virtue_freya_01_c", "virtue_freya_01_d", "virtue_warden_03_a", "virtue_warden_03_b", "virtue_warden_03_c" };

        IEnumerator RunRemoteOwnerHost(DNNetworkManager manager)
        {
            var creator = FindFirstObjectByType<RoomCreator>();
            int map = creator ? Array.FindIndex(creator.Maps, candidate => candidate &&
                Path.GetFileNameWithoutExtension(candidate.Scene) == "map_arena") : -1;
            if (map < 0) { Fail("Remote owner arena unavailable"); yield break; }
            var hostProfile = new SkillProgress(SkillCatalog.Load(), null, 10, 321);
            SkillPlayground.ValidationMatchProgress = hostProfile;
            SkillPlayground.LaunchMatch(creator, map, 0, RaidClass.Berserker);
            RoomSetup.Properties.P_FillEmptySlotsWithBots = false;
            yield return JoinCharacter();
            if (failed) yield break;
            var hostPlayer = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
            var hostBattle = FindFirstObjectByType<SkillBattlePreview>();
            if (!hostBattle || !SkillMatchSession.Active || GameManager.Players.Count != 1 ||
                !TryFindOpenArenaPosition(hostPlayer.MyCharacter.transform.position, out var position))
            { Fail("Remote owner host fixture unavailable"); manager.StopHost(); yield break; }
            hostPlayer.MyCharacter.GetComponent<CharacterMotor>().enabled = false;
            hostPlayer.MyCharacter.transform.position = position;
            int hostPending = hostBattle.PendingRewards;
            WriteSignal("host-ready", "remote-owner");
            float deadline = Time.realtimeSinceStartup + 60;
            while (!File.Exists(Path.Combine(signalDirectory, "client-ready")) && Time.realtimeSinceStartup < deadline) yield return null;
            PlayerInstance remote = null;
            foreach (var player in GameManager.Players.Values)
                if (player != hostPlayer && player.connectionToClient != null) remote = player;
            var remoteBattle = remote ? SkillMatchSession.Active.ValidationRuntime(remote) : null;
            if (!remote || !remote.MyCharacter || !remoteBattle || remoteBattle == hostBattle ||
                remoteBattle.SelectedRaidClass != RemoteClass ||
                SkillBattlePreview.ScaleFoodHealing(remote.MyCharacter, 50) != RemoteFood ||
                SkillBattlePreview.ScaleFoodHealing(hostPlayer.MyCharacter, 50) != 50)
            { Fail("Remote profile, class or owner isolation failed"); manager.StopHost(); yield break; }
            var owner = remote.MyCharacter;
            hostPlayer.MyCharacter.Team = owner.Team; // Keep the two test owners out of each other's target lists.
            owner.GetComponent<CharacterMotor>().enabled = false;
            owner.transform.SetPositionAndRotation(position + Vector3.right * 3, Quaternion.identity);
            if (scenario == "remote-attack-speed")
            {
                yield return RunRemoteAttackSpeedHost(remote, remoteBattle, owner, hostPlayer.MyCharacter);
                if (failed) yield break;
            }
            if (scenario == "remote-dash")
            {
                yield return RunRemoteDashHost(remoteBattle, owner, hostPlayer.MyCharacter);
                if (failed) { manager.StopHost(); yield break; }
            }
            // Real server death -> kill attribution -> crystal collection. No SetRaidLevel or direct XP grant.
            for (int i = 0; i < 2; i++)
            {
                var target = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position + Vector3.forward, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in target.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                target.Team = owner.Team == 0 ? 1 : 0;
                target.MaxHealth = target.CurrentHealth = 1;
                NetworkServer.Spawn(target.gameObject);
                target.GetComponent<CharacterMotor>().enabled = false;
                yield return null;
                target.ServerApplyRaidSkillDamage(1, owner);
            }
            deadline = Time.realtimeSinceStartup + 10;
            while (remoteBattle.RaidLevel != 2 && Time.realtimeSinceStartup < deadline) yield return null;
            int remotePending = remoteBattle.PendingRewards;
            if (remoteBattle.RaidLevel != 2 || remotePending < 2 || remotePending > (RewardManagement ? 3 : 2) ||
                hostBattle.PendingRewards != hostPending)
            { Fail("Remote kill XP leaked or did not advance independently"); manager.StopHost(); yield break; }
            WriteSignal("remote-reward-ready", "ready");
            deadline = Time.realtimeSinceStartup + 55;
            while (!File.Exists(Path.Combine(signalDirectory, "client-observed")) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(Path.Combine(signalDirectory, "client-observed")) || remoteBattle.PendingRewards != remotePending - 1 ||
                hostBattle.PendingRewards != hostPending || hostProfile.Gold != 321 ||
                !SkillMatchSession.Active.ValidationGrantGold(remote, 3))
            { Fail("Remote reward replay or owner currency isolation failed"); manager.StopHost(); yield break; }
            if (RewardManagement)
            {
                var rules = remoteBattle.BuildRemoteSnapshot();
                if (rules.banishes != 0 || rules.rerolls != 0 || rules.sealCapacity != 3 || rules.sealedLabels.Length != 2)
                { Fail("Server reward management budgets did not match the actual commands"); manager.StopHost(); yield break; }
                Debug.Log("JOTUNN_REMOTE_REWARD_RULES_SERVER_OK banishes=0 rerolls=0 sealed=2 capacity=3 pending=" + (remotePending - 1));
            }
            deadline = Time.realtimeSinceStartup + 15;
            while (!File.Exists(Path.Combine(signalDirectory, "client-synced")) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!File.Exists(Path.Combine(signalDirectory, "client-synced")) || hostProfile.Gold != 321)
                Fail("Remote gold delivery acknowledgment missing or host save changed");
            if (!failed)
            {
                var enemy = Instantiate(GameManager.Gamemode.GetPlayerCharacterPrefab(),
                    owner.transform.position + Vector3.forward * 40, Quaternion.identity).GetComponent<CharacterInstance>();
                foreach (var slot in enemy.CharacterItemManager.Slots) slot.ItemOnSpawn = null;
                enemy.Team = owner.Team == 0 ? 1 : 0;
                enemy.MaxHealth = enemy.CurrentHealth = 10000;
                NetworkServer.Spawn(enemy.gameObject);
                enemy.GetComponent<CharacterMotor>().enabled = false;
                yield return null;
                for (int hit = 0; owner.CurrentHealth > 0 && hit < 32; hit++) owner.ServerApplyRaidSkillDamage(100000, enemy);
                if (owner.CurrentHealth > 0) Fail("Could not enter actual remote death lifecycle");
                deadline = Time.realtimeSinceStartup + 20;
                while ((remote.MyCharacter == owner || !remote.MyCharacter ||
                        remoteBattle.BuildRemoteSnapshot().character != remote.MyCharacter.netId) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                if (!remote.MyCharacter || remote.MyCharacter == owner || !remoteBattle.HasLiveCaster ||
                    remoteBattle.RaidLevel != 2 || remoteBattle.PendingRewards != remotePending - 1 ||
                    SkillBattlePreview.ScaleFoodHealing(remote.MyCharacter, 50) != RemoteFood ||
                    SkillBattlePreview.ScaleFoodHealing(owner, 50) != 50)
                    Fail("Remote respawn failed to transfer stats and retain raid progression");
                deadline = Time.realtimeSinceStartup + 15;
                while (!File.Exists(Path.Combine(signalDirectory, "client-respawned")) && Time.realtimeSinceStartup < deadline) yield return null;
                if (!File.Exists(Path.Combine(signalDirectory, "client-respawned"))) Fail("Remote respawn snapshot acknowledgment missing");
                NetworkServer.Destroy(enemy.gameObject);
            }
            if (!failed) Debug.Log("JOTUNN_REMOTE_OWNER_SERVER_OK class=" + RemoteClass + " food=" + RemoteFood + " hostFood=50 level=2 pending=" + (remotePending - 1) + " hostPending=" +
                hostPending + " hostGold=321 remoteGold=3 duplicateAndInvalidRequestRejected=True");
            if (!failed) Debug.Log("JOTUNN_REMOTE_OWNER_RESPAWN_SERVER_OK newCharacter=" + remote.MyCharacter.netId +
                " level=2 pending=" + (remotePending - 1) + " newFood=" + RemoteFood + " oldFood=50");
            manager.StopHost();
            deadline = Time.realtimeSinceStartup + 20;
            while ((NetworkServer.active || SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (NetworkServer.active || NetworkClient.active || SkillMatchSession.Active || FindFirstObjectByType<SkillBattlePreview>())
                Fail("Remote owner host teardown left session or skill runtime");
        }

        IEnumerator RunRemoteOwnerClient(DNNetworkManager manager)
        {
            var localProfile = new SkillProgress(SkillCatalog.Load(), null, RewardManagement || scenario == "remote-attack-speed" ? 100 : 10, 77);
            var nodes = RewardManagement ? new[] { "virtue_skadi_02_a", "virtue_skadi_02_b", "virtue_skadi_02_c",
                "virtue_seer_03_a", "virtue_seer_03_b", "virtue_skadi_04_a", "virtue_skadi_04_b", "virtue_odin_04_a" }
                : scenario == "remote-attack-speed" ? AttackSpeedNodes : new[] { "virtue_warden_01_a" };
            foreach (string node in nodes)
                if (!localProfile.TryLearn(node)) { Fail("Remote validation loadout unavailable: " + node); yield break; }
            RemoteSkillPanel.ValidationProfile = localProfile;
            RemoteSkillPanel.ValidationEnglish = false; // Isolate the fixture from the user's saved UI language.
            manager.networkAddress = "127.0.0.1";
            RangeCharacterSelection.SetSelected(0);
            manager.StartClient();
            yield return JoinCharacter();
            if (failed) yield break;
            var player = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
            float deadline = Time.realtimeSinceStartup + 20;
            RemoteSkillPanel panel = null;
            while (!(panel = player.GetComponent<RemoteSkillPanel>()) && Time.realtimeSinceStartup < deadline) yield return null;
            if (!panel || NetworkServer.active) { Fail("Remote owner UI or client authority invalid"); yield break; }
            Screen.SetResolution(640, 360, FullScreenMode.Windowed);
            deadline = Time.realtimeSinceStartup + 5;
            while ((Screen.width != 640 || Screen.height != 360) && Time.realtimeSinceStartup < deadline) yield return null;
            if (Screen.width != 640 || Screen.height != 360) { Fail("Remote UI initial resolution change failed"); yield break; }
            panel.MoveFocus(-1);
            if (panel.FocusedControl != 0) { Fail("Remote class navigation crossed the first boundary"); yield break; }
            for (int i = 0; i < 5; i++) panel.MoveFocus(1);
            if (panel.FocusedControl != 5) { Fail("Remote class navigation could not reach Valkyrie"); yield break; }
            yield return CaptureRemoteUi("remote-classes-640");
            if (failed) yield break;
            for (int i = 5; i > (int)RemoteClass; i--) panel.MoveFocus(-1);
            if (panel.FocusedControl != (int)RemoteClass) { Fail("Remote class reverse focus failed"); yield break; }
            panel.ActivateFocused();
            deadline = Time.realtimeSinceStartup + 20;
            while ((!panel.Snapshot.alive || panel.Snapshot.character != player.MyCharacter.netId) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            if (!panel.Snapshot.alive) { Fail("Remote server runtime handshake failed"); yield break; }
            WriteSignal("client-ready", player.netId.ToString());
            if (scenario == "remote-attack-speed")
            {
                yield return RunRemoteAttackSpeedClient(panel, player);
                if (failed) yield break;
            }
            if (scenario == "remote-dash")
            {
                yield return RunRemoteDashClient(panel, player);
                if (failed) yield break;
            }
            deadline = Time.realtimeSinceStartup + 20;
            while ((!File.Exists(Path.Combine(signalDirectory, "remote-reward-ready")) || panel.Snapshot.pending < 2) &&
                Time.realtimeSinceStartup < deadline) yield return null;
            var before = panel.Snapshot;
            if (RewardManagement)
            {
                managementPending = before.pending; // Preserve Seeress's genuine random bonus choice.
                yield return RunRemoteRewardActions(panel, player);
                if (failed) yield break;
                before = panel.Snapshot;
            }
            int index = -1;
            for (int i = 0; before.actions != null && i < before.actions.Length; i++)
                if ((before.actions[i] & 1) != 0) { index = i; break; }
            if (before.pending < 2 || before.pending > (RewardManagement ? 3 : 2) || index < 0)
            { Fail("Remote level two reward missing"); yield break; }
            int details = 0;
            foreach (string effect in before.details)
            {
                if (string.IsNullOrEmpty(effect)) continue;
                if (System.Text.RegularExpressions.Regex.IsMatch(effect, "[A-Za-z]") ||
                    !System.Text.RegularExpressions.Regex.IsMatch(effect, "[\\u4e00-\\u9fff]"))
                { Fail("Remote Chinese reward effect was not localized"); yield break; }
                details++;
            }
            // Management actions can preserve a still-valid secondary focus; navigate to the actual reward.
            if (RewardManagement)
                for (int i = 0; i < 32 && panel.FocusedControl != 100 + index; i++) panel.MoveFocus(-1);
            if (details == 0 || before.details.Length != before.labels.Length || panel.FocusedControl != 100 + index)
            { Fail("Remote reward details or focused selection missing"); yield break; }
            Debug.Log("JOTUNN_REMOTE_UI_LOCALIZATION_OK language=zh-CN details=" + details);
            yield return CaptureRemoteUi("remote-rewards-640");
            Screen.SetResolution(1280, 720, FullScreenMode.Windowed);
            deadline = Time.realtimeSinceStartup + 5;
            while ((Screen.width != 1280 || Screen.height != 720) && Time.realtimeSinceStartup < deadline) yield return null;
            if (Screen.width != 1280 || Screen.height != 720) { Fail("Remote UI resolution change failed"); yield break; }
            yield return CaptureRemoteUi("remote-rewards-1280");
            if (failed) yield break;
            Debug.Log("JOTUNN_REMOTE_UI_NAVIGATION_OK classBoundary=True classLast=True reverse=True focusedReward=" + index + " details=" + details);
            panel.ActivateFocused();
            player.CmdJotunnSkillAction(before.session, before.revision, JotunnSkillAction.Accept, index);
            yield return new WaitForSecondsRealtime(.75f);
            if (panel.Snapshot.pending != before.pending - 1 || panel.Snapshot.revision <= before.revision)
            { Fail("Remote duplicate packet consumed more than one reward"); yield break; }
            var accepted = panel.Snapshot;
            player.CmdJotunnSkillAction(accepted.session, accepted.revision, JotunnSkillAction.Accept, int.MaxValue);
            yield return new WaitForSecondsRealtime(.5f);
            if (panel.Snapshot.pending != before.pending - 1 || panel.Snapshot.revision <= accepted.revision || localProfile.Gold != 77)
            { Fail("Remote invalid request locked UI, consumed a reward or changed local currency"); yield break; }
            Debug.Log("JOTUNN_REMOTE_OWNER_CLIENT_REWARD_OK character=" + before.character + " level=2 choices=" + before.pending + "->" +
                (before.pending - 1) + " duplicateAndInvalidRejected=True");
            WriteSignal("client-observed", "reward");
            deadline = Time.realtimeSinceStartup + 15;
            while (localProfile.Gold != 80 && Time.realtimeSinceStartup < deadline) yield return null;
            if (localProfile.Gold != 80 || panel.Snapshot.gold != 3)
            { Fail("Remote earned gold did not reach only the owner's local profile"); yield break; }
            var goldState = panel.Snapshot;
            player.CmdJotunnSkillAction(goldState.session, goldState.revision, JotunnSkillAction.Accept, -1);
            deadline = Time.realtimeSinceStartup + 10;
            while (panel.Snapshot.revision <= goldState.revision && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(.5f);
            if (localProfile.Gold != 80 || panel.Snapshot.revision <= goldState.revision)
            { Fail("Remote repeated snapshot missing or duplicated gold"); yield break; }
            Debug.Log("JOTUNN_REMOTE_OWNER_CLIENT_GOLD_OK local=77->80 serverEarned=3");
            WriteSignal("client-synced", "gold");
            var statsBodyBeforeDeath = player.MyCharacter;
            bool sawDead = false;
            deadline = Time.realtimeSinceStartup + 30;
            while (Time.realtimeSinceStartup < deadline)
            {
                sawDead |= !panel.Snapshot.alive;
                if (sawDead && panel.Snapshot.alive && panel.Snapshot.character != before.character) break;
                yield return null;
            }
            if (!sawDead || !panel.Snapshot.alive || panel.Snapshot.character == before.character ||
                panel.Snapshot.level != 2 || panel.Snapshot.pending != before.pending - 1 || localProfile.Gold != 80)
            { Fail("Remote actual death/respawn state did not retain owner progression"); yield break; }
            if (RewardManagement && (panel.Snapshot.banishes != 0 || panel.Snapshot.rerolls != 0 ||
                panel.Snapshot.sealCapacity != 3 || panel.Snapshot.sealedLabels.Length != 2))
            { Fail("Remote respawn replenished or lost reward management budgets"); yield break; }
            if (scenario == "remote-attack-speed")
            {
                deadline = Time.realtimeSinceStartup + 5;
                while ((!player.MyCharacter || player.MyCharacter.netId != panel.Snapshot.character ||
                        !Mathf.Approximately(SkillBattlePreview.RaidBaseAttackSpeedMultiplier(player.MyCharacter), panel.Snapshot.attackSpeed)) &&
                    Time.realtimeSinceStartup < deadline) yield return null;
                if (!player.MyCharacter || player.MyCharacter.netId != panel.Snapshot.character ||
                    panel.Snapshot.attackSpeed < 1.57f - .001f ||
                    !Mathf.Approximately(SkillBattlePreview.RaidBaseAttackSpeedMultiplier(player.MyCharacter), panel.Snapshot.attackSpeed) ||
                    (statsBodyBeforeDeath && statsBodyBeforeDeath.TryGetComponent<RemoteRaidStats>(out var previous) && previous.HasState))
                { Fail("Remote attack speed did not transfer to the respawn body or retained old body stats"); yield break; }
                Debug.Log("JOTUNN_REMOTE_ATTACK_SPEED_RESPAWN_OK speed=" + panel.Snapshot.attackSpeed + " oldBodyReleased=True");
            }
            Debug.Log("JOTUNN_REMOTE_OWNER_RESPAWN_CLIENT_OK old=" + before.character + " new=" + panel.Snapshot.character +
                " sawDead=True level=2 pending=" + (before.pending - 1) + " localGold=80 repeatedGoldSnapshot=True");
            WriteSignal("client-respawned", "respawn");
            deadline = Time.realtimeSinceStartup + 25;
            while (NetworkClient.active && Time.realtimeSinceStartup < deadline) yield return null;
            yield return new WaitForSecondsRealtime(1);
            if (NetworkClient.active || FindFirstObjectByType<RemoteSkillPanel>() || SceneManager.GetActiveScene().path != DNNetworkManager.MainMenuScene)
                Fail("Remote owner teardown left UI or network");
            else Debug.Log("JOTUNN_REMOTE_OWNER_CLIENT_TEARDOWN_OK");
        }

        IEnumerator SendRemoteRewardAction(RemoteSkillPanel panel, PlayerInstance player, JotunnSkillAction action, int index)
        {
            var previous = panel.Snapshot;
            panel.Request(action, index);
            // A real duplicate wire request uses the stale version and must not repeat the action.
            player.CmdJotunnSkillAction(previous.session, previous.revision, action, index);
            float deadline = Time.realtimeSinceStartup + 5;
            while (panel.Snapshot.revision < previous.revision + 2 && Time.realtimeSinceStartup < deadline) yield return null;
            if (panel.Snapshot.revision < previous.revision + 2 || panel.Snapshot.pending != managementPending)
                Fail("Remote management acknowledgment or reward preservation failed: " + action);
        }

        static int FindRemoteAction(JotunnSkillSnapshot state, byte mask)
        {
            for (int i = 0; i < state.actions.Length; i++) if ((state.actions[i] & mask) != 0) return i;
            return -1;
        }

        IEnumerator RunRemoteRewardActions(RemoteSkillPanel panel, PlayerInstance player)
        {
            if (panel.Snapshot.rerolls != 13 || panel.Snapshot.banishes != 2 || panel.Snapshot.sealCapacity != 3)
            { Fail("Remote Seeress reward budgets incorrect"); yield break; }
            for (int count = 1; count <= 3; count++)
            {
                int index = FindRemoteAction(panel.Snapshot, 4);
                string label = index >= 0 ? panel.Snapshot.labels[index] : null;
                if (index < 0) { Fail("Remote seal candidate missing"); yield break; }
                yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Seal, index);
                if (failed) yield break;
                if (panel.Snapshot.sealedLabels.Length != count || Array.IndexOf(panel.Snapshot.sealedLabels, label) < 0 ||
                    Array.IndexOf(panel.Snapshot.labels, label) >= 0)
                { Fail("Remote seal state mismatch expected=" + count + " actual=" + panel.Snapshot.sealedLabels.Length +
                    " selected=" + label + " sealed=" + string.Join("|", panel.Snapshot.sealedLabels)); yield break; }
            }
            for (int i = 0; i < 32 && panel.FocusedControl != 600; i++) panel.MoveFocus(1);
            if (panel.FocusedControl != 600) { Fail("Remote seal management could not focus the first unseal action"); yield break; }
            yield return CaptureRemoteUi("remote-seals-full-640");
            if (failed) yield break;
            for (int i = 0; i < 32; i++) panel.MoveFocus(-1);
            yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Seal, 0);
            if (failed) yield break;
            if (panel.Snapshot.sealedLabels.Length != 3 || FindRemoteAction(panel.Snapshot, 4) >= 0)
            { Fail("Remote full seal capacity accepted another entry"); yield break; }
            yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Unseal, 0);
            if (failed) yield break;
            if (panel.Snapshot.sealedLabels.Length != 2 || FindRemoteAction(panel.Snapshot, 4) < 0)
            { Fail("Remote unseal removed two entries or did not restore capacity"); yield break; }
            var banned = new string[2];
            for (int count = 0; count < 2; count++)
            {
                int index = FindRemoteAction(panel.Snapshot, 2);
                if (index < 0) { Fail("Remote banish candidate missing"); yield break; }
                banned[count] = panel.Snapshot.labels[index];
                yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Banish, index);
                if (failed) yield break;
                if (panel.Snapshot.banishes != 1 - count || Array.IndexOf(panel.Snapshot.labels, banned[count]) >= 0)
                { Fail("Remote banish consumed the wrong budget or retained its offer"); yield break; }
            }
            yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Banish, 0);
            if (failed) yield break;
            if (panel.Snapshot.banishes != 0 || FindRemoteAction(panel.Snapshot, 2) >= 0)
            { Fail("Remote exhausted banish budget was reusable"); yield break; }
            int godIndex;
            while ((godIndex = FindRemoteAction(panel.Snapshot, 8)) < 0 && panel.Snapshot.rerolls > 0)
            {
                int budget = panel.Snapshot.rerolls;
                yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Reroll, -1);
                if (failed) yield break;
                if (panel.Snapshot.rerolls != budget - 1) { Fail("Remote reroll replay consumed twice"); yield break; }
            }
            if (godIndex < 0) { Fail("No god exclusion candidate appeared within the actual reroll budget"); yield break; }
            string godLabel = panel.Snapshot.labels[godIndex];
            int separator = godLabel.IndexOf('·');
            if (separator <= 0) { Fail("God exclusion candidate lacks its catalog god prefix"); yield break; }
            string godPrefix = godLabel.Substring(0, separator).Trim();
            yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.ExcludeGod, godIndex);
            if (failed) yield break;
            yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.ExcludeGod, 0);
            if (failed) yield break;
            do
            {
                var state = panel.Snapshot;
                if (FindRemoteAction(state, 8) >= 0) { Fail("Remote god exclusion could be used again"); yield break; }
                foreach (string label in state.labels)
                    if (Array.IndexOf(banned, label) >= 0 || Array.IndexOf(state.sealedLabels, label) >= 0 ||
                        label.StartsWith(godPrefix + " ·", StringComparison.Ordinal))
                    { Fail("Excluded god, banished or sealed family returned to the offer"); yield break; }
                if (state.rerolls == 0) break;
                yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Reroll, -1);
                if (failed) yield break;
                if (panel.Snapshot.rerolls != state.rerolls - 1) { Fail("Remote reroll budget mismatch"); yield break; }
                bool changed = false;
                foreach (string label in panel.Snapshot.labels) changed |= Array.IndexOf(state.labels, label) < 0;
                if (!changed) { Fail("Remote reroll did not introduce a new candidate"); yield break; }
            } while (!failed);
            yield return SendRemoteRewardAction(panel, player, JotunnSkillAction.Reroll, -1);
            if (failed) yield break;
            if (panel.Snapshot.rerolls != 0 || panel.Snapshot.sealedLabels.Length != 2)
            { Fail("Remote exhausted reroll changed budgets"); yield break; }
            Debug.Log("JOTUNN_REMOTE_REWARD_RULES_CLIENT_OK rerolls=13 banishes=2 seals=3 unseals=1 excluded=" + godPrefix +
                " duplicateAndExhaustedRejected=True pending=" + managementPending);
        }

        IEnumerator CaptureRemoteUi(string name)
        {
            string path = Path.Combine(signalDirectory, name + ".png");
            // Capture the real Player frame after layout/focus has settled, including IMGUI.
            yield return null;
            yield return null;
            yield return new WaitForEndOfFrame();
            var capture = ScreenCapture.CaptureScreenshotAsTexture();
            bool nonBlack = false;
            for (int y = 1; y < 8 && !nonBlack; y++)
                for (int x = 1; x < 8 && !nonBlack; x++)
                {
                    Color pixel = capture.GetPixel(capture.width * x / 8, capture.height * y / 8);
                    nonBlack = Mathf.Max(pixel.r, Mathf.Max(pixel.g, pixel.b)) > .03f;
                }
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
            if (!nonBlack) Fail("Remote UI screenshot is black: " + name);
            else Debug.Log("JOTUNN_REMOTE_UI_CAPTURE width=" + Screen.width + " height=" + Screen.height + " path=" + path);
        }
    }
}
#endif
