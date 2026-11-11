#if UNITY_EDITOR
using System;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.Gameplay.Gamemodes;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jotunnslayer
{
    [InitializeOnLoad]
    internal static class SkillBattleValidation
    {
        private const string Pending = "Jotunnslayer.BattleValidation";
        private const string Focused = "Jotunnslayer.BattleValidation.Focused";

        static SkillBattleValidation() => EditorApplication.playModeStateChanged += OnPlayModeChanged;

        public static void Run() => EnterValidation(false);

        public static void RunProjectileAndFollower() => EnterValidation(true);

        private static void EnterValidation(bool focused)
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetBool(Pending, true);
            SessionState.SetBool(Focused, focused);
            EditorApplication.EnterPlaymode();
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false))
                return;

            SessionState.EraseBool(Pending);
            bool focused = SessionState.GetBool(Focused, false);
            SessionState.EraseBool(Focused);
            int exitCode = 0;
            try
            {
                if (focused)
                {
                    StartCombatValidation();
                    ValidateMagicMissileInterception();
                    ValidateMagicMissileTerrain();
                    ValidateHeartseekerPath();
                    ValidateFocusFirePath();
                    ValidateSoulmate();
                    Debug.Log("JOTUNNSLAYER_PROJECTILE_FOLLOWER_VALIDATION_OK");
                }
                else
                    Validate();
            }
            catch (Exception error)
            {
                Debug.LogException(error);
                exitCode = 1;
            }
            finally
            {
                if (NetworkServer.active)
                    NetworkServer.Shutdown();

                EditorApplication.Exit(exitCode);
            }
        }

        private static void ValidateTreeUnlock()
        {
            const int startingPoints = 10;
            var catalog = SkillCatalog.Load();
            var progress = new SkillProgress(catalog, null, startingPoints);
            var panel = new GameObject("Validation skill tree").AddComponent<SkillTreePanel>();
            panel.Open(catalog, progress, null);
            var node = Array.Find(catalog.skills, entry => entry.id == SkillBattlePreview.MissileUnlock);
            var evolution = Array.Find(catalog.skills, entry => entry.id == SkillBattlePreview.MissileEvolution);

            panel.SelectNode(evolution);
            panel.BeginUnlock(false, 0);
            panel.UpdateUnlock(true, 1);
            panel.SelectNode(node);
            panel.BeginUnlock(true, 2);
            panel.UpdateUnlock(true, 2.99f);
            if (progress.Points != startingPoints || progress.IsLearned(node.id) || progress.IsLearned(evolution.id))
                throw new InvalidOperationException("Selecting, holding briefly, or submitting a locked node spent skill points.");

            panel.UpdateUnlock(false, 3);
            panel.UpdateUnlock(true, 4);
            panel.BeginUnlock(false, 5);
            panel.SelectNode(evolution);
            panel.UpdateUnlock(true, 6);
            panel.SelectNode(node);
            panel.BeginUnlock(false, 7);
            panel.enabled = false;
            panel.enabled = true;
            panel.UpdateUnlock(true, 8);
            if (progress.Points != startingPoints || progress.IsLearned(node.id))
                throw new InvalidOperationException("Releasing, changing selection, or closing the panel did not cancel the hold.");

            panel.BeginUnlock(false, 9);
            panel.UpdateUnlock(true, 10);
            panel.BeginUnlock(false, 11);
            panel.UpdateUnlock(true, 12);
            if (!progress.IsLearned(node.id) || progress.Points != startingPoints - node.cost)
                throw new InvalidOperationException("Completed hold did not purchase exactly one unlock.");

            var treeNode = Array.Find(catalog.skills, entry => entry.group == "Berserker [Class]");
            panel.SelectNode(treeNode);
            panel.SetArchive(true);
            panel.SelectArchiveTab(1);
            panel.MoveArchiveSelection(-1);
            if (panel.SelectedArchiveRow() != 0 || panel.ArchiveScrollOffset != 0f)
                throw new InvalidOperationException("Archive navigation should clamp at the first trophy.");
            for (int i = 0; i < catalog.awards.Length; i++)
                panel.MoveArchiveSelection(1);
            if (panel.SelectedArchiveRow() != catalog.awards.Length - 1 || panel.ArchiveScrollOffset <= 0f)
                throw new InvalidOperationException("Archive navigation should clamp and scroll to the final trophy.");
            panel.SelectArchiveTab(2);
            if (panel.SelectedArchiveRow() != -1 || panel.ArchiveScrollOffset != 0f)
                throw new InvalidOperationException("Switching archive tabs should reset selection and scroll.");
            panel.MoveArchiveSelection(1);
            if (panel.SelectedArchiveRow() != 0)
                throw new InvalidOperationException("Archive navigation should select the first item after changing tabs.");
            panel.SetArchive(false);
            if (panel.SelectedNode != treeNode)
                throw new InvalidOperationException("Returning from the archive should restore the tree selection.");
            Debug.Log("JOTUNNSLAYER_ARCHIVE_NAVIGATION_OK row selection, boundaries, scroll, tab reset and tree return");

            UnityEngine.Object.Destroy(panel.gameObject);
            Debug.Log("JOTUNNSLAYER_TREE_HOLD_OK locked, short, released, selection changed, disabled and committed once");
        }

        private static void StartCombatValidation()
        {
            var transport = new GameObject("Validation transport").AddComponent<kcp2k.KcpTransport>();
            transport.port = 0;
            Transport.active = transport;
            NetworkServer.Listen(1);
            GameManager.SetGamemode(new GameObject("Validation gamemode", typeof(NetworkIdentity)).AddComponent<Gamemode>());
        }

        private static void Validate()
        {
            ValidateTreeUnlock();
            StartCombatValidation();
            ValidateMagicMissileInterception();
            ValidateMagicMissileTerrain();
            ValidateHeartseekerPath();
            ValidateFocusFirePath();
            ValidateGoldPickup();
            var owner = Spawn("Caster", 0, Vector3.zero);
            var target = Spawn("Target", 1, Vector3.forward * 2);
            var ally = Spawn("Ally", 0, Vector3.forward);
            var skill = new GameObject("Skill runtime").AddComponent<SkillBattlePreview>();
            var progress = new SkillProgress(SkillCatalog.Load(), null, 10);
            skill.Bind(owner, progress, null);

            skill.Cast(0);
            ExpectDamage(target, 0);
            if (!progress.TryLearn(SkillBattlePreview.MissileUnlock))
                throw new InvalidOperationException("Missile unlock failed.");

            skill.Cast(0);
            ExpectDamage(target, 0);
            Choose(skill, true);
            var secondMissileTarget = Spawn("Second ordinary missile target", 1, Vector3.right * 2f);
            skill.Cast(0);
            Vector3 casterOrigin = owner.transform.position;
            owner.transform.position -= (Vector3.right + Vector3.forward) * 6f;
            skill.Tick(0.1f);
            ExpectDamage(target, 0);
            ExpectDamage(secondMissileTarget, 0);
            skill.Tick(0.8f);
            int firstMissileDamage = target.MaxHealth - target.CurrentHealth +
                secondMissileTarget.MaxHealth - secondMissileTarget.CurrentHealth;
            if (firstMissileDamage != 8)
                throw new InvalidOperationException("Only the first ordinary missile should have reached a target.");
            skill.Tick(1.4f);
            ExpectDamage(target, 8);
            ExpectDamage(secondMissileTarget, 8);
            owner.transform.position = casterOrigin;
            secondMissileTarget.Team = owner.Team;
            NetworkServer.Destroy(secondMissileTarget.gameObject);
            skill.Cast(2);
            ExpectDamage(target, 0);
            skill.SetRaidLevel(6);
            Choose(skill, true);
            skill.Cast(4);
            skill.Tick(5f);
            ExpectDamage(target, 8);
            skill.SetRaidLevel(12);
            Choose(skill, true);
            skill.Cast(8);
            skill.Tick(9f);
            ExpectDamage(target, 8);

            if (!progress.TryLearn(SkillBattlePreview.MissileEvolution))
                throw new InvalidOperationException("Missile evolution unlock failed.");
            skill.SetRaidLevel(17);
            if (skill.TryChoose(true))
                throw new InvalidOperationException("Evolution was selectable before raid level 18.");
            skill.Cast(12);
            skill.Tick(13f);
            ExpectDamage(target, 8);
            skill.SetRaidLevel(18);
            Choose(skill, true);
            var missileRandom = UnityEngine.Random.state;
            UnityEngine.Random.InitState(408);
            skill.Cast(16);
            skill.Tick(18f);
            UnityEngine.Random.state = missileRandom;
            ExpectDamage(target, 30);

            progress.Reset();
            if (!progress.TryLearn(SkillBattlePreview.StormUnlock))
                throw new InvalidOperationException("Storm unlock failed.");

            skill.SetRaidLevel(5);
            Choose(skill, false);
            skill.Cast(16);
            skill.Tick(16);
            ExpectDamage(target, 30);
            skill.Tick(16.6f);
            ExpectDamage(target, 0);
            skill.Tick(16.9f);
            ExpectDamage(target, 30);
            skill.SetRaidLevel(12);
            Choose(skill, false);
            Choose(skill, false);
            skill.Cast(25);
            skill.Tick(25);
            ExpectDamage(target, 30);
            skill.Tick(25.6f);
            ExpectDamage(target, 30);
            skill.Tick(28);
            ExpectDamage(target, 0);
            if (!progress.TryLearn(SkillBattlePreview.StormEvolution))
                throw new InvalidOperationException("Storm evolution unlock failed.");

            skill.SetRaidLevel(17);
            if (skill.TryChoose(true))
                throw new InvalidOperationException("Evolution was selectable before raid level 18.");
            skill.Cast(34);
            skill.Tick(34);
            ExpectDamage(target, 30);

            skill.SetRaidLevel(18);
            Choose(skill, false);
            skill.Cast(43);
            skill.Tick(43);
            ExpectDamage(target, 35);
            skill.Cast(43.4f);
            ExpectDamage(target, 0);
            skill.Cast(43.6f);
            ExpectDamage(target, 35);
            owner.transform.position = Vector3.back * 10;
            skill.Cast(44.2f);
            ExpectDamage(target, 0);
            owner.transform.position = Vector3.zero;
            skill.Cast(44.2f);
            ExpectDamage(target, 35);
            ExpectDamage(ally, 0);

            skill.SetRaidLevel(1);
            var xpTarget = Spawn("Experience target", 1, Vector3.forward * 3);
            xpTarget.Server_OnHealthDepleted += (_, _, killer, _) => killer.Server_KilledCharacter?.Invoke(xpTarget);
            xpTarget.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.explosion, owner, 0);
            xpTarget.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.explosion, owner, 0);
            if (skill.RaidLevel != 2)
                throw new InvalidOperationException("The server kill did not reach the original 35 XP threshold.");

            progress.Reset();
            skill.SetRaidLevel(12);
            for (int rank = 0; rank < 3; rank++)
            {
                if (!skill.TryChooseThunder())
                    throw new InvalidOperationException("Base Thunderstorm rank is not selectable.");
            }

            skill.Cast(50);
            for (int hit = 0; hit < 10; hit++)
                skill.Tick(50 + hit * 0.25f);
            ExpectDamage(target, 200);

            skill.SetRaidLevel(18);
            if (skill.TryChooseThunder() || !progress.TryLearn(SkillBattlePreview.ThunderEvolution) ||
                !skill.TryChooseThunder())
                throw new InvalidOperationException("Thunderstorm evolution gate failed.");

            var splash = Spawn("Splash target", 1, Vector3.forward * 5);
            skill.Cast(56);
            skill.Tick(56);
            ExpectDamage(target, 45);
            ExpectDamage(splash, 45);
            ExpectDamage(ally, 0);
            Debug.Log("JOTUNNSLAYER_THUNDER_OK 10 x 20 base; 45 evolved area damage");

            var lightningCaster = Spawn("Lightning caster", 0, Vector3.right * 40);
            skill.Bind(lightningCaster, progress, null);
            var front = Spawn("Front strike", 1, lightningCaster.transform.position + Vector3.forward * 3);
            var side = Spawn("Side strike", 1, lightningCaster.transform.position + Vector3.right * 3);
            var edge = Spawn("Evolution edge", 1, side.transform.position + Vector3.forward * 0.8f);
            if (!skill.TryChooseLightning())
                throw new InvalidOperationException("Lightning Strike was not offered at level 1.");

            skill.Cast(60);
            ExpectDamage(front, 17);
            ExpectDamage(side, 17);
            ExpectDamage(edge, 0);
            skill.Cast(64);
            ExpectDamage(front, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseLightning() || !skill.TryChooseLightning() || skill.TryChooseLightning() ||
                !progress.TryLearn(SkillBattlePreview.LightningEvolution) || !skill.TryChooseLightning())
                throw new InvalidOperationException("Lightning Strike evolution selection failed.");

            skill.Cast(65);
            ExpectDamage(front, 28);
            ExpectDamage(side, 28);
            ExpectDamage(edge, 28);
            skill.Cast(65.2f);
            ExpectDamage(front, 0);
            skill.Cast(65.4f);
            ExpectDamage(front, 28);
            Debug.Log("JOTUNNSLAYER_LIGHTNING_OK 17 base, 28 evolved, four-point targeting, range and cooldown");

            var hammerCaster = Spawn("Hammer caster", 0, Vector3.right * 80);
            skill.Bind(hammerCaster, progress, null);
            var path = Spawn("Hammer path", 1, hammerCaster.transform.position + Vector3.forward * 4);
            var nova = Spawn("Far nova", 1, hammerCaster.transform.position + Vector3.forward * 9 + Vector3.right * 3);
            var returnNova = Spawn("Return nova", 1, hammerCaster.transform.position + Vector3.right * 3);
            if (!skill.TryChooseHammer())
                throw new InvalidOperationException("Mjolnir was not offered at level 1.");

            skill.Cast(70);
            skill.Tick(72.5f);
            ExpectDamage(path, 30);
            skill.Tick(75);
            ExpectDamage(path, 30);
            ExpectDamage(nova, 0);
            ExpectDamage(returnNova, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseHammer() || !skill.TryChooseHammer() || skill.TryChooseHammer() ||
                !progress.TryLearn(SkillBattlePreview.HammerEvolution) || !skill.TryChooseHammer())
                throw new InvalidOperationException("Mjolnir evolution selection failed.");

            skill.Cast(80);
            skill.Tick(82.5f);
            ExpectDamage(nova, 40);
            ExpectDamage(path, 80);
            skill.Tick(85);
            ExpectDamage(returnNova, 40);
            ExpectDamage(path, 80);
            Debug.Log("JOTUNNSLAYER_HAMMER_OK two 30 damage passes and two evolved 40 damage novas");

            progress.Reset();
            var orbCaster = Spawn("Orb caster", 0, Vector3.right * 120);
            skill.Bind(orbCaster, progress, null);
            var randomState = UnityEngine.Random.state;
            float angle = UnityEngine.Random.Range(0f, 360f);
            UnityEngine.Random.state = randomState;
            Vector3 direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var orbTarget = Spawn("Orb path", 1, orbCaster.transform.position + direction * 3);
            if (!skill.TryChooseOrb())
                throw new InvalidOperationException("Ball Lightning was not offered at level 1.");

            skill.Cast(90);
            skill.Tick(91);
            ExpectDamage(orbTarget, 14);
            skill.Tick(92);
            ExpectDamage(orbTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseOrb() || !skill.TryChooseOrb() || skill.TryChooseOrb() ||
                !progress.TryLearn(SkillBattlePreview.OrbEvolution) || !skill.TryChooseOrb())
                throw new InvalidOperationException("Ball Lightning evolution selection failed.");

            orbTarget.transform.position = orbCaster.transform.position;
            var chain = Spawn("Chain target", 1, orbCaster.transform.position + Vector3.right * 3);
            skill.Cast(95);
            skill.Tick(95);
            ExpectDamage(orbTarget, 25);
            ExpectDamage(chain, 10);
            skill.Tick(95.1f);
            ExpectDamage(orbTarget, 0);
            ExpectDamage(chain, 0);
            Debug.Log("JOTUNNSLAYER_ORB_OK swept base damage, evolved 25 + 10 chain and pulse cooldown");

            progress.Reset();
            var mindCaster = Spawn("Mind caster", 0, Vector3.right * 170);
            skill.Bind(mindCaster, progress, null);
            var mindFront = Spawn("Mind front", 1, mindCaster.transform.position + Vector3.forward * 6);
            if (!skill.TryChooseMind())
                throw new InvalidOperationException("Mind Blast was not offered at level 1.");

            skill.Cast(100);
            ExpectDamage(mindFront, 16);
            skill.Cast(102);
            ExpectDamage(mindFront, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseMind() || !skill.TryChooseMind() || skill.TryChooseMind() ||
                !progress.TryLearn(SkillBattlePreview.MindEvolution) || !skill.TryChooseMind())
                throw new InvalidOperationException("Mind Blast evolution selection failed.");

            var mindSide = Spawn("Mind side", 1, mindCaster.transform.position + Vector3.right * 6);
            var mindGap = Spawn("Mind gap", 1, mindCaster.transform.position + new Vector3(2, 0, 6));
            skill.Cast(103);
            ExpectDamage(mindFront, 20);
            ExpectDamage(mindSide, 20);
            ExpectDamage(mindGap, 0);
            Debug.Log("JOTUNNSLAYER_MIND_OK random-target base and radial evolution");

            progress.Reset();
            var briarCaster = Spawn("Briar caster", 0, Vector3.right * 220);
            skill.Bind(briarCaster, progress, null);
            randomState = UnityEngine.Random.state;
            angle = UnityEngine.Random.Range(0f, 360f);
            UnityEngine.Random.state = randomState;
            direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var briarFront = Spawn("Briar front", 1, briarCaster.transform.position + direction * 5);
            var briarBack = Spawn("Briar back", 1, briarCaster.transform.position - direction * 5);
            if (!skill.TryChooseBriar())
                throw new InvalidOperationException("Briar was not offered at level 1.");

            skill.Cast(110);
            skill.Tick(110);
            ExpectDamage(briarFront, 24);
            ExpectDamage(briarBack, 24);
            skill.Tick(111);
            ExpectDamage(briarFront, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseBriar() || !skill.TryChooseBriar() || skill.TryChooseBriar() ||
                !progress.TryLearn(SkillBattlePreview.BriarEvolution) || !skill.TryChooseBriar())
                throw new InvalidOperationException("Briar evolution selection failed.");

            do
            {
                randomState = UnityEngine.Random.state;
                UnityEngine.Random.Range(0.15f, 0.25f);
            }
            while (UnityEngine.Random.value >= 0.35f);
            UnityEngine.Random.state = randomState;
            var briarGround = new GameObject("Briar validation ground").AddComponent<BoxCollider>();
            briarGround.transform.position = briarCaster.transform.position - Vector3.up * 0.5f;
            briarGround.size = new Vector3(28f, 1f, 28f);
            Physics.SyncTransforms();
            RaidClass previousBriarClass = skill.SelectedRaidClass;
            skill.SelectedRaidClass = RaidClass.Seeress;
            skill.LiveMatch = true;
            briarFront.transform.position = briarCaster.transform.position + Vector3.forward * 4f;
            briarBack.transform.position = briarCaster.transform.position + Vector3.back * 14f;
            skill.Cast(115);
            skill.Tick(115);
            ExpectDamage(briarFront, 0);
            skill.Tick(115.25f);
            ExpectDamage(briarFront, 30);
            ExpectDamage(briarBack, 0);
            skill.LiveMatch = false;
            skill.SelectedRaidClass = previousBriarClass;
            UnityEngine.Object.Destroy(briarGround.gameObject);
            Debug.Log("JOTUNNSLAYER_BRIAR_OK opposite base branches, contact deduplication and evolved area");

            progress.Reset();
            var trapCaster = Spawn("Trap caster", 0, Vector3.right * 260);
            skill.Bind(trapCaster, progress, null);
            var trapVictim = Spawn("Trap victim", 1, trapCaster.transform.position + Vector3.right * 2);
            if (!skill.TryChooseTrap())
                throw new InvalidOperationException("Time Trap was not offered at level 1.");

            skill.Cast(120);
            Vector3 arrowStart = trapCaster.transform.position + Vector3.back * 5f;
            float trapContact = SkillBattlePreview.TimeTrapProjectileContact(trapVictim, arrowStart, Vector3.forward, 10f, 120f);
            if (!Mathf.Approximately(trapContact, 1.8f) ||
                !float.IsPositiveInfinity(SkillBattlePreview.TimeTrapProjectileContact(
                    trapCaster, arrowStart, Vector3.forward, 10f, 120f)) ||
                !float.IsPositiveInfinity(SkillBattlePreview.TimeTrapProjectileContact(
                    trapVictim, arrowStart, Vector3.forward, 10f, 123.5f)))
                throw new InvalidOperationException("Time Trap projectile entry, team or expiry check failed.");
            if (!SkillBattlePreview.IsStoppedByTimeTrap(trapVictim, 120f) ||
                SkillBattlePreview.IsStoppedByTimeTrap(trapCaster, 120f) ||
                SkillBattlePreview.IsStoppedByTimeTrap(trapVictim, 123.5f))
                throw new InvalidOperationException("Time Trap did not stop only the enemy.");
            trapVictim.transform.position += Vector3.right * 2;
            if (SkillBattlePreview.IsStoppedByTimeTrap(trapVictim, 120f))
                throw new InvalidOperationException("Time Trap stop survived exiting the zone.");
            trapVictim.transform.position -= Vector3.right * 2;
            trapVictim.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, trapCaster, 0);
            ExpectDamage(trapVictim, 112);
            skill.Tick(123.5f);
            if (SkillBattlePreview.IsStoppedByTimeTrap(trapVictim))
                throw new InvalidOperationException("Time Trap stop survived expiry.");
            trapVictim.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, trapCaster, 0);
            ExpectDamage(trapVictim, 100);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseTrap() || !skill.TryChooseTrap() || skill.TryChooseTrap() ||
                !progress.TryLearn(SkillBattlePreview.TrapEvolution) || !skill.TryChooseTrap())
                throw new InvalidOperationException("Time Trap evolution selection failed.");

            skill.Cast(129);
            trapVictim.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, trapCaster, 0);
            ExpectDamage(trapVictim, 145);
            skill.Tick(130);
            ExpectDamage(trapVictim, 14);
            skill.Tick(135);
            ExpectDamage(trapVictim, 70);
            skill.Tick(135);
            ExpectDamage(trapVictim, 0);
            if (!skill.TryChooseEssenceExpansion())
                throw new InvalidOperationException("Could not select Essence Expansion for the evolved trap.");
            skill.Cast(136);
            skill.Tick(146);
            ExpectDamage(trapVictim, 153);
            if (SkillBattlePreview.IsStoppedByTimeTrap(trapVictim))
                throw new InvalidOperationException("The extended trap survived its expiry.");
            Debug.Log("JOTUNNSLAYER_TRAP_OK vulnerability, expiry, catch-up and nine extended damage ticks");

            progress.Reset();
            var wispCaster = Spawn("Wisp caster", 0, Vector3.right * 300);
            skill.Bind(wispCaster, progress, null);
            var wispTarget = Spawn("Wisp contact", 1, wispCaster.transform.position + Vector3.forward * 5);
            var wispArcTarget = Spawn("Wisp swept arc", 1, wispCaster.transform.position + Vector3.right * 5);
            if (!skill.TryChooseWisps())
                throw new InvalidOperationException("Dark Wisps was not offered at level 1.");

            skill.Cast(140);
            skill.Tick(140);
            ExpectDamage(wispTarget, 12);
            skill.Tick(140.1f);
            ExpectDamage(wispTarget, 0);
            skill.Tick(141);
            ExpectDamage(wispArcTarget, 12);
            ExpectDamage(wispTarget, 0);
            skill.Tick(142.67f);
            ExpectDamage(wispTarget, 24);
            skill.Tick(144);
            ExpectDamage(wispTarget, 12);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseWisps() || !skill.TryChooseWisps() || skill.TryChooseWisps() ||
                !progress.TryLearn(SkillBattlePreview.WispEvolution) || !skill.TryChooseWisps())
                throw new InvalidOperationException("Dark Wisps evolution selection failed.");

            skill.Cast(148);
            wispTarget.transform.position = wispCaster.transform.position + Vector3.forward * 3f;
            skill.Tick(148);
            ExpectDamage(wispTarget, 12);
            wispTarget.transform.position = wispCaster.transform.position + Vector3.forward * 10f;
            skill.Tick(152.49f);
            wispTarget.transform.position = wispCaster.transform.position +
                Quaternion.Euler(0f, 4.5f * 135f, 0f) * Vector3.forward * 3f;
            var bonusWispTarget = Spawn("Wisp bonus expiry blast", 1, wispCaster.transform.position +
                Quaternion.Euler(0f, 4.5f * 135f + 40f, 0f) * Vector3.forward * 4.5f);
            skill.Tick(152.5f);
            ExpectDamage(wispTarget, 42);
            ExpectDamage(bonusWispTarget, 30);
            skill.Tick(153);
            ExpectDamage(wispTarget, 0);
            ExpectDamage(bonusWispTarget, 0);
            wispTarget.transform.position = wispCaster.transform.position + new Vector3(2f, 0f, 3f);
            skill.Cast(156);
            skill.Tick(156);
            ExpectDamage(wispTarget, 0);
            wispCaster.transform.position += Vector3.right * 4f;
            skill.Tick(156.01f);
            ExpectDamage(wispTarget, 12);
            Debug.Log("JOTUNNSLAYER_WISPS_OK swept contact, rotation resets and primary and bonus expiry explosions");

            progress.Reset();
            var cuttingCaster = Spawn("Cutting caster", 0, Vector3.right * 340);
            skill.Bind(cuttingCaster, progress, null);
            var cuttingFront = Spawn("Cutting front", 1, cuttingCaster.transform.position + Vector3.forward * 1.9f);
            var cuttingSide = Spawn("Cutting diagonal", 1, cuttingCaster.transform.position + new Vector3(1.4f, 0, 1.4f));
            var cuttingOutside = Spawn("Cutting outside", 1, cuttingCaster.transform.position + Vector3.back * 3);
            if (!skill.TryChooseCuttingEdge())
                throw new InvalidOperationException("Cutting Edge was not offered at level 1.");

            skill.Cast(160);
            ExpectDamage(cuttingFront, 12);
            ExpectDamage(cuttingSide, 12);
            ExpectDamage(cuttingOutside, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseCuttingEdge() || !skill.TryChooseCuttingEdge() || skill.TryChooseCuttingEdge() ||
                !progress.TryLearn(SkillBattlePreview.CuttingEvolution) || !skill.TryChooseCuttingEdge())
                throw new InvalidOperationException("Cutting Edge evolution selection failed.");

            skill.Cast(165);
            ExpectDamage(cuttingFront, 14);
            ExpectDamage(cuttingSide, 0);
            skill.Cast(165.25f);
            ExpectDamage(cuttingSide, 0);
            skill.Cast(165.5f);
            ExpectDamage(cuttingSide, 14);
            Debug.Log("JOTUNNSLAYER_CUTTING_OK base radius and evolved moving hit point");

            progress.Reset();
            var assaultCaster = Spawn("Assault caster", 0, Vector3.right * 380);
            skill.Bind(assaultCaster, progress, null);
            var assaultNear = Spawn("Assault near", 1, assaultCaster.transform.position + Vector3.forward * 4);
            var assaultFar = Spawn("Assault far", 1, assaultCaster.transform.position + Vector3.forward * 6.75f);
            var assaultSide = Spawn("Assault side", 1, assaultNear.transform.position + Vector3.right);
            if (!skill.TryChooseAssault())
                throw new InvalidOperationException("Assault was not offered at level 1.");

            skill.Cast(170);
            ExpectDamage(assaultNear, 20);
            ExpectDamage(assaultFar, 0);
            ExpectDamage(assaultSide, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseAssault() || !skill.TryChooseAssault())
                throw new InvalidOperationException("Assault base upgrades were not selectable.");
            skill.Cast(173);
            ExpectDamage(assaultNear, 28);
            ExpectDamage(assaultFar, 28);

            if (skill.TryChooseAssault() || !progress.TryLearn(SkillBattlePreview.AssaultEvolution) ||
                !skill.TryChooseAssault())
                throw new InvalidOperationException("Assault evolution selection failed.");

            skill.Cast(176);
            ExpectDamage(assaultNear, 120);
            ExpectDamage(assaultFar, 0);
            ExpectDamage(assaultSide, 0);
            skill.Cast(178);
            ExpectDamage(assaultNear, 0);
            Debug.Log("JOTUNNSLAYER_ASSAULT_OK base range growth and four evolved hits");

            progress.Reset();
            var swirlCaster = Spawn("Swirling caster", 0, Vector3.right * 420);
            skill.Bind(swirlCaster, progress, null);
            var swirlFront = Spawn("Swirling front", 1, swirlCaster.transform.position + Vector3.forward);
            var swirlSecond = Spawn("Swirling second", 1, swirlCaster.transform.position +
                Quaternion.Euler(0, 120, 0) * Vector3.forward);
            if (!skill.TryChooseSwirling())
                throw new InvalidOperationException("Swirling Axe was not offered at level 1.");

            skill.Cast(180);
            skill.Tick(180);
            ExpectDamage(swirlFront, 14);
            ExpectDamage(swirlSecond, 0);
            skill.Tick(180.05f);
            ExpectDamage(swirlFront, 0);
            skill.Tick(181.5f);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseSwirling() || !skill.TryChooseSwirling())
                throw new InvalidOperationException("Swirling Axe base upgrades were not selectable.");
            skill.Cast(183);
            skill.Tick(183);
            ExpectDamage(swirlFront, 18);
            ExpectDamage(swirlSecond, 18);
            skill.Tick(184.5f);

            if (skill.TryChooseSwirling() || !progress.TryLearn(SkillBattlePreview.SwirlingEvolution) ||
                !skill.TryChooseSwirling())
                throw new InvalidOperationException("Swirling Axe evolution selection failed.");

            skill.Cast(187);
            skill.Tick(187);
            ExpectDamage(swirlFront, 30);
            ExpectDamage(swirlSecond, 0);
            Debug.Log("JOTUNNSLAYER_SWIRLING_OK base growth, hit deduplication and one evolved axe");

            progress.Reset();
            var throwingCaster = Spawn("Throwing caster", 0, Vector3.right * 460);
            skill.Bind(throwingCaster, progress, null);
            var throwingTarget = Spawn("Throwing target", 1, throwingCaster.transform.position + Vector3.forward * 9.8f);
            var throwingClose = Spawn("Too close", 1, throwingCaster.transform.position + Vector3.forward);
            var throwingSplit = Spawn("Split target", 1, throwingTarget.transform.position + Vector3.right * 2f);
            if (!skill.TryChooseThrowingAxes())
                throw new InvalidOperationException("Throwing Axes was not offered at level 1.");

            skill.Cast(190);
            ExpectDamage(throwingTarget, 20);
            ExpectDamage(throwingClose, 0);
            ExpectDamage(throwingSplit, 0);
            skill.Cast(191);
            ExpectDamage(throwingTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseThrowingAxes() || !skill.TryChooseThrowingAxes() ||
                skill.TryChooseThrowingAxes() || !progress.TryLearn(SkillBattlePreview.ThrowingEvolution) ||
                !skill.TryChooseThrowingAxes())
                throw new InvalidOperationException("Throwing Axes evolution selection failed.");

            skill.Cast(192);
            ExpectDamage(throwingTarget, 40);
            ExpectDamage(throwingSplit, 60);
            ExpectDamage(throwingClose, 0);
            Debug.Log("JOTUNNSLAYER_THROWING_OK throw range, impact radius and evolved split damage");

            progress.Reset();
            var totemCaster = Spawn("Totem caster", 0, Vector3.right * 500);
            skill.Bind(totemCaster, progress, null);
            var totemTarget = Spawn("Totem target", 1, totemCaster.transform.position + Vector3.forward * 5);
            if (skill.TryChooseTotem() || !progress.TryLearn(SkillBattlePreview.TotemUnlock) || !skill.TryChooseTotem())
                throw new InvalidOperationException("Totem unlock gate failed.");

            skill.Cast(200);
            skill.Tick(201);
            ExpectDamage(totemTarget, 10);
            if (Vector3.Distance(totemCaster.transform.position, totemTarget.transform.position) > 1.01f)
                throw new InvalidOperationException("Totem did not pull the enemy.");
            skill.Tick(202);
            ExpectDamage(totemTarget, 10);
            skill.Tick(203);
            ExpectDamage(totemTarget, 10);
            skill.Tick(204);
            ExpectDamage(totemTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseTotem() || !skill.TryChooseTotem() || skill.TryChooseTotem() ||
                !progress.TryLearn(SkillBattlePreview.TotemEvolution) || !skill.TryChooseTotem())
                throw new InvalidOperationException("Totem evolution selection failed.");

            skill.Cast(210);
            skill.Tick(211);
            ExpectDamage(totemTarget, 14);
            skill.Cast(212);
            skill.Tick(212);
            ExpectDamage(totemTarget, 14);
            skill.Tick(213);
            ExpectDamage(totemTarget, 28);
            Debug.Log("JOTUNNSLAYER_TOTEM_OK pull, 10/14 damage ticks, expiry and evolved overlap");

            progress.Reset();
            var heartCaster = Spawn("Heart caster", 0, Vector3.right * 540);
            skill.Bind(heartCaster, progress, null);
            var heartTargets = new Health[4];
            for (int i = 0; i < heartTargets.Length; i++)
                heartTargets[i] = Spawn("Heart target " + i, 1, heartCaster.transform.position + Vector3.forward * (3 + i));

            if (skill.TryChooseHeartseeker() || !progress.TryLearn(SkillBattlePreview.HeartUnlock) ||
                !skill.TryChooseHeartseeker())
                throw new InvalidOperationException("Heart Seeker unlock gate failed.");

            Vector3 heartOrigin = heartCaster.transform.position;
            heartCaster.transform.position -= Vector3.forward * 8f;
            skill.Cast(219);
            skill.Tick(219);
            foreach (var victim in heartTargets)
                ExpectDamage(victim, 0);
            heartCaster.transform.position = heartOrigin;

            var beforeHeartRandom = UnityEngine.Random.state;
            void SetHeartRolls(int count, float firstMin, float firstMax)
            {
                for (int seed = 0; ; seed++)
                {
                    UnityEngine.Random.InitState(seed);
                    float first = UnityEngine.Random.value;
                    if (first < firstMin || first >= firstMax)
                        continue;

                    bool remainingMissed = true;
                    for (int i = 1; i < count; i++)
                        remainingMissed &= UnityEngine.Random.value >= 0.5f;
                    if (!remainingMissed)
                        continue;

                    UnityEngine.Random.InitState(seed);
                    return;
                }
            }

            skill.Cast(220);
            skill.Tick(220.1f);
            foreach (var victim in heartTargets)
                ExpectDamage(victim, 0);
            SetHeartRolls(3, 0f, 0.25f);
            skill.Tick(220.5f);
            for (int i = 0; i < heartTargets.Length; i++)
                ExpectDamage(heartTargets[i], i < 3 ? 15 : 0);
            skill.Tick(220.9f);
            ExpectDamage(heartTargets[0], 0);
            skill.Tick(224.5f);
            ExpectDamage(heartTargets[0], 8);
            skill.Tick(225);
            ExpectDamage(heartTargets[0], 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseHeartseeker() || !skill.TryChooseHeartseeker())
                throw new InvalidOperationException("Heart Seeker base upgrades were not selectable.");

            skill.Cast(225);
            SetHeartRolls(4, 0.5f, 1f);
            skill.Tick(226);
            foreach (var victim in heartTargets)
                ExpectDamage(victim, 15);

            if (skill.TryChooseHeartseeker() || !progress.TryLearn(SkillBattlePreview.HeartEvolution) ||
                !skill.TryChooseHeartseeker())
                throw new InvalidOperationException("Heart Seeker evolution selection failed.");

            skill.Cast(229);
            SetHeartRolls(3, 0.25f, 0.5f);
            skill.Tick(230);
            for (int i = 0; i < heartTargets.Length; i++)
                ExpectDamage(heartTargets[i], i < 3 ? 20 : 0);
            skill.Tick(233.5f);
            UnityEngine.Random.state = beforeHeartRandom;
            for (int i = 0; i < heartTargets.Length; i++)
                ExpectDamage(heartTargets[i], i == 0 ? 8 : 0);
            Debug.Log("JOTUNNSLAYER_HEART_OK travel, bounce limit, radial evolution and four bleed ticks");

            progress.Reset();
            var rainCaster = Spawn("Rain caster", 0, Vector3.right * 580);
            skill.Bind(rainCaster, progress, null);
            var rainTarget = Spawn("Rain target", 1, rainCaster.transform.position + Vector3.forward * 4);
            if (!skill.TryChooseArrowRain())
                throw new InvalidOperationException("Arrow Rain was not offered at level 1.");

            skill.Cast(240);
            for (int wave = 0; wave < 4; wave++)
                skill.Tick(240 + wave * 0.65f);
            ExpectDamage(rainTarget, 42);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseArrowRain() || !skill.TryChooseArrowRain())
                throw new InvalidOperationException("Arrow Rain upgrades were not selectable.");
            skill.Cast(245);
            for (int wave = 0; wave < 6; wave++)
                skill.Tick(245 + wave * 0.65f);
            ExpectDamage(rainTarget, 70);

            if (skill.TryChooseArrowRain() || !progress.TryLearn(SkillBattlePreview.RainEvolution) ||
                !skill.TryChooseArrowRain())
                throw new InvalidOperationException("Arrow Rain evolution selection failed.");

            skill.Cast(249);
            ExpectDamage(rainTarget, 24);
            bool rainCursed = SkillBattlePreview.HasRaidCurse(rainTarget, Time.time);
            skill.Cast(249.6f);
            ExpectDamage(rainTarget, 0);
            skill.Cast(249.75f);
            ExpectDamage(rainTarget, rainCursed ? 31 : 24);
            Debug.Log("JOTUNNSLAYER_RAIN_OK three/five waves and 0.7s evolved cadence");

            progress.Reset();
            var skullCaster = Spawn("Skull caster", 0, Vector3.right * 620);
            skill.Bind(skullCaster, progress, null);
            var skullPrimary = Spawn("Skull primary", 1, skullCaster.transform.position + Vector3.forward * 4);
            var skullFront = Spawn("Skull front", 1, skullPrimary.transform.position + Vector3.forward * 3);
            var skullSide = Spawn("Skull side", 1, skullPrimary.transform.position + Vector3.right * 4.8f);
            if (!skill.TryChooseSkull())
                throw new InvalidOperationException("Exploding Skull was not offered at level 1.");

            skill.Cast(260);
            ExpectDamage(skullPrimary, 20);
            ExpectDamage(skullFront, 18);
            ExpectDamage(skullSide, 0);
            if (SkillBattlePreview.IsStoppedBySkill(skullPrimary) || SkillBattlePreview.IsStoppedBySkill(skullFront))
                throw new InvalidOperationException("Exploding Skull stunned before evolution.");
            skill.Cast(261);
            ExpectDamage(skullPrimary, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseSkull() || !skill.TryChooseSkull() || skill.TryChooseSkull() ||
                !progress.TryLearn(SkillBattlePreview.SkullEvolution) || !skill.TryChooseSkull())
                throw new InvalidOperationException("Exploding Skull evolution selection failed.");

            skill.Cast(263);
            ExpectDamage(skullPrimary, 32);
            ExpectDamage(skullFront, 26);
            ExpectDamage(skullSide, 26);
            skill.Tick(264.9f);
            if (!SkillBattlePreview.IsStoppedBySkill(skullPrimary) ||
                !SkillBattlePreview.IsStoppedBySkill(skullFront) ||
                !SkillBattlePreview.IsStoppedBySkill(skullSide))
                throw new InvalidOperationException("Exploding Skull did not stun all evolved targets.");
            skill.Tick(265);
            if (SkillBattlePreview.IsStoppedBySkill(skullPrimary) ||
                SkillBattlePreview.IsStoppedBySkill(skullFront) ||
                SkillBattlePreview.IsStoppedBySkill(skullSide))
                throw new InvalidOperationException("Exploding Skull stun did not expire.");
            Debug.Log("JOTUNNSLAYER_SKULL_OK 90-degree base, 360-degree evolved fragments and stun expiry");

            progress.Reset();
            var focusCaster = Spawn("Focus caster", 0, Vector3.right * 660);
            skill.Bind(focusCaster, progress, null);
            var focusTarget = Spawn("Focus target", 1, focusCaster.transform.position + Vector3.forward * 5);
            if (!skill.TryChooseFocus())
                throw new InvalidOperationException("Focus Fire base selection failed.");
            Vector3 focusOrigin = focusTarget.transform.position;
            focusTarget.transform.position += Vector3.forward * 6f;
            skill.Cast(269);
            skill.Tick(269);
            ExpectDamage(focusTarget, 0);
            focusTarget.transform.position = focusOrigin;
            skill.Cast(270);
            skill.Tick(270.3f);
            ExpectDamage(focusTarget, 0);
            skill.Tick(270.45f);
            ExpectDamage(focusTarget, 8);
            skill.Tick(270.5f);
            ExpectDamage(focusTarget, 8);
            skill.Tick(272);
            ExpectDamage(focusTarget, 48);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseFocus() || !skill.TryChooseFocus())
                throw new InvalidOperationException("Focus Fire upgrades failed.");
            skill.Cast(274);
            skill.Tick(274.46f);
            ExpectDamage(focusTarget, 16);
            skill.Tick(277);
            ExpectDamage(focusTarget, 112);

            if (skill.TryChooseFocus() || !progress.TryLearn(SkillBattlePreview.FocusEvolution) ||
                !skill.TryChooseFocus())
                throw new InvalidOperationException("Focus Fire evolution gate failed.");
            skill.Cast(278);
            ExpectDamage(focusTarget, 0);
            skill.Cast(278.1f);
            ExpectDamage(focusTarget, 0);
            skill.Cast(278.13f);
            ExpectDamage(focusTarget, 0);
            skill.Cast(278.16f);
            skill.Tick(278.45f);
            ExpectDamage(focusTarget, 10);
            skill.Tick(278.6f);
            ExpectDamage(focusTarget, 10);
            var previousFocusClass = skill.SelectedRaidClass;
            skill.SelectedRaidClass = RaidClass.Ranger;
            skill.LiveMatch = true;
            skill.Tick(279.5f);
            ExpectDamage(focusTarget, 40);
            skill.Tick(279.5f);
            ExpectDamage(focusTarget, 0);
            Vector3 focusPosition = focusTarget.transform.position;
            focusTarget.transform.position += Vector3.up * 100f;
            skill.Tick(280);
            focusTarget.transform.position = focusPosition;
            skill.Tick(280.5f);
            ExpectDamage(focusTarget, 0);
            skill.Tick(280.91f);
            ExpectDamage(focusTarget, 10);
            skill.LiveMatch = false;
            skill.SelectedRaidClass = previousFocusClass;
            Debug.Log("JOTUNNSLAYER_FOCUS_OK range, rank-specific bursts, evolved cadence and bounded catch-up");

            progress.Reset();
            var netCaster = Spawn("Net caster", 0, Vector3.right * 700);
            skill.Bind(netCaster, progress, null);
            var netTarget = Spawn("Net target", 1, netCaster.transform.position + Vector3.forward * 4);
            var netOffAxis = Spawn("Net diagonal", 1, netCaster.transform.position +
                Quaternion.Euler(0, 60, 0) * Vector3.forward * 4);
            if (!skill.TryChooseNet())
                throw new InvalidOperationException("Net base selection failed.");
            skill.Cast(290);
            var netAlly = Spawn("Net ally", 0, netTarget.transform.position);
            var beforeNetRandom = UnityEngine.Random.state;
            int netSeed = 0;
            do { UnityEngine.Random.InitState(netSeed++); }
            while (UnityEngine.Random.value < 0.75f);
            UnityEngine.Random.InitState(netSeed - 1);
            if (SkillBattlePreview.NetMoveMultiplier(netTarget, 290) != 0.75f ||
                SkillBattlePreview.NetMoveMultiplier(netOffAxis, 290) != 1f ||
                SkillBattlePreview.NetMoveMultiplier(netAlly, 290) != 1f ||
                SkillBattlePreview.NetMoveMultiplier(netTarget, 292) != 1f)
                throw new InvalidOperationException("Net slow, ally exclusion or expiry failed.");
            UnityEngine.Random.state = beforeNetRandom;
            skill.Tick(291);
            ExpectDamage(netTarget, 10);
            ExpectDamage(netOffAxis, 0);
            skill.Tick(292);
            ExpectDamage(netTarget, 10);
            skill.Tick(293);
            ExpectDamage(netTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseNet() || !skill.TryChooseNet() || skill.TryChooseNet() ||
                !progress.TryLearn(SkillBattlePreview.NetEvolution) || !skill.TryChooseNet())
                throw new InvalidOperationException("Net evolution selection failed.");
            skill.Cast(294);
            netSeed = 0;
            do { UnityEngine.Random.InitState(netSeed++); }
            while (UnityEngine.Random.value >= 0.75f);
            UnityEngine.Random.InitState(netSeed - 1);
            if (SkillBattlePreview.NetMoveMultiplier(netOffAxis, 294) != 0f)
                throw new InvalidOperationException("Evolved net did not root an enemy.");
            UnityEngine.Random.state = beforeNetRandom;
            skill.Tick(296);
            ExpectDamage(netTarget, 30);
            ExpectDamage(netOffAxis, 30);
            ExpectDamage(netAlly, 0);
            Debug.Log("JOTUNNSLAYER_NET_OK base/evolved zones, slow, root, damage catch-up and expiry");

            progress.Reset();
            var auraCaster = Spawn("Aura caster", 0, Vector3.right * 740);
            skill.Bind(auraCaster, progress, null);
            var auraNear = Spawn("Aura near", 1, auraCaster.transform.position + Vector3.forward * 5.5f);
            var auraFar = Spawn("Aura far", 1, auraCaster.transform.position + Vector3.right * 9f);
            if (!skill.TryChooseAura())
                throw new InvalidOperationException("Divine Aura base selection failed.");
            skill.Cast(300);
            ExpectDamage(auraNear, 6);
            ExpectDamage(auraFar, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseAura() || !skill.TryChooseAura())
                throw new InvalidOperationException("Divine Aura upgrades failed.");
            skill.Cast(305);
            ExpectDamage(auraNear, 10);
            ExpectDamage(auraFar, 10);

            if (skill.TryChooseAura() || !progress.TryLearn(SkillBattlePreview.AuraEvolution) || !skill.TryChooseAura())
                throw new InvalidOperationException("Divine Aura evolution gate failed.");
            skill.Cast(309);
            ExpectDamage(auraNear, 7);
            ExpectDamage(auraFar, 0);
            skill.Cast(309.25f);
            ExpectDamage(auraNear, 0);
            skill.Cast(309.5f);
            ExpectDamage(auraNear, 7);
            Debug.Log("JOTUNNSLAYER_AURA_OK radius growth and evolved 0.5s cadence");

            progress.Reset();
            var swordCaster = Spawn("Sword caster", 0, Vector3.right * 780);
            skill.Bind(swordCaster, progress, null);
            var swordNear = Spawn("Sword near", 1, swordCaster.transform.position + Vector3.forward * 2);
            var swordFar = Spawn("Sword far", 1, swordCaster.transform.position + Vector3.forward * 7);
            if (!skill.TryChooseMagicSword())
                throw new InvalidOperationException("Magic Sword base selection failed.");
            skill.Cast(320);
            skill.Tick(320);
            ExpectDamage(swordNear, 20);
            skill.Tick(320.25f);
            ExpectDamage(swordNear, 20);
            ExpectDamage(swordFar, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseMagicSword() || !skill.TryChooseMagicSword())
                throw new InvalidOperationException("Magic Sword upgrades failed.");
            skill.Cast(323);
            for (int swing = 0; swing < 4; swing++)
                skill.Tick(323 + swing * 0.25f);
            ExpectDamage(swordNear, 80);

            if (skill.TryChooseMagicSword() || !progress.TryLearn(SkillBattlePreview.SwordEvolution) ||
                !skill.TryChooseMagicSword())
                throw new InvalidOperationException("Magic Sword evolution gate failed.");
            skill.Cast(328);
            ExpectDamage(swordNear, 45);
            ExpectDamage(swordFar, 45);
            Debug.Log("JOTUNNSLAYER_SWORD_OK two/four slashes and evolved fire wave");

            progress.Reset();
            var blessingCaster = Spawn("Blessing caster", 0, Vector3.right * 820);
            skill.Bind(blessingCaster, progress, null);
            var blessingAlly = Spawn("Blessing ally", 0, blessingCaster.transform.position + Vector3.forward * 2);
            var blessingEnemy = Spawn("Blessing enemy", 1, blessingCaster.transform.position + Vector3.forward * 2.8f);
            if (skill.TryChooseBlessing() || !progress.TryLearn(SkillBattlePreview.BlessingUnlock) ||
                !skill.TryChooseBlessing())
                throw new InvalidOperationException("Blessing base gate failed.");
            blessingAlly.CurrentHealth = 700;
            skill.Cast(330);
            for (int second = 1; second <= 5; second++)
                skill.Tick(330 + second);
            ExpectDamage(blessingAlly, 100);
            ExpectDamage(blessingEnemy, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseBlessing() || !skill.TryChooseBlessing() || skill.TryChooseBlessing() ||
                !progress.TryLearn(SkillBattlePreview.BlessingEvolution) || !skill.TryChooseBlessing())
                throw new InvalidOperationException("Blessing evolution gate failed.");
            blessingAlly.CurrentHealth = 700;
            skill.Cast(360);
            skill.Tick(361);
            ExpectDamage(blessingAlly, 250);
            ExpectDamage(blessingEnemy, 8);
            Debug.Log("JOTUNNSLAYER_BLESSING_OK 5-percent regeneration and evolved damage");

            progress.Reset();
            var discCaster = Spawn("Disc caster", 0, Vector3.right * 860);
            skill.Bind(discCaster, progress, null);
            var discMain = Spawn("Disc main", 1, discCaster.transform.position + Vector3.forward * 7);
            var discEdge = Spawn("Disc edge", 1, discMain.transform.position + Vector3.right * 0.8f);
            if (!skill.TryChooseDiscs())
                throw new InvalidOperationException("Discs base selection failed.");
            skill.Cast(370);
            skill.Tick(370.5f);
            ExpectDamage(discMain, 23);
            ExpectDamage(discEdge, 0);
            skill.Tick(371);
            ExpectDamage(discMain, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseDiscs() || !skill.TryChooseDiscs() || skill.TryChooseDiscs() ||
                !progress.TryLearn(SkillBattlePreview.DiscEvolution) || !skill.TryChooseDiscs())
                throw new InvalidOperationException("Discs evolution gate failed.");
            skill.Cast(374);
            skill.Tick(374.5f);
            ExpectDamage(discMain, 23);
            ExpectDamage(discEdge, 23);
            skill.Tick(375);
            ExpectDamage(discMain, 20);
            ExpectDamage(discEdge, 20);
            Debug.Log("JOTUNNSLAYER_DISCS_OK outbound and widened return damage");

            progress.Reset();
            var valkyrieCaster = Spawn("Valkyrie caster", 0, Vector3.right * 900);
            skill.Bind(valkyrieCaster, progress, null);
            var valkyrieTarget = Spawn("Valkyrie target", 1, valkyrieCaster.transform.position + Vector3.forward * 4);
            if (!skill.TryChooseValkyrie())
                throw new InvalidOperationException("Valkyrie base selection failed.");
            skill.Cast(380);
            ExpectDamage(valkyrieTarget, 35);
            skill.Tick(384);
            ExpectDamage(valkyrieTarget, 18);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseValkyrie() || !skill.TryChooseValkyrie() || skill.TryChooseValkyrie() ||
                !progress.TryLearn(SkillBattlePreview.ValkyrieEvolution) || !skill.TryChooseValkyrie())
                throw new InvalidOperationException("Valkyrie evolution gate failed.");
            skill.Cast(385);
            ExpectDamage(valkyrieTarget, 35);
            var valkyrieEdge = Spawn("Valkyrie outer zone", 1, valkyrieTarget.transform.position + Vector3.right * 2.75f);
            skill.Tick(386);
            ExpectDamage(valkyrieTarget, 6);
            ExpectDamage(valkyrieEdge, 6);
            skill.Tick(392);
            ExpectDamage(valkyrieTarget, 30);
            ExpectDamage(valkyrieEdge, 30);
            Debug.Log("JOTUNNSLAYER_VALKYRIE_OK impact, evolved outer area and expiry");

            progress.Reset();
            var plagueCaster = Spawn("Plague caster", 0, Vector3.right * 940);
            skill.Bind(plagueCaster, progress, null);
            var plagueHost = Spawn("Plague host", 1, plagueCaster.transform.position + Vector3.forward * 4);
            if (skill.TryChoosePlague() || !progress.TryLearn(SkillBattlePreview.PlagueUnlock) ||
                !skill.TryChoosePlague())
                throw new InvalidOperationException("Plague base gate failed.");
            skill.Cast(400);
            skill.Tick(402);
            ExpectDamage(plagueHost, 60);

            skill.SetRaidLevel(18);
            if (!skill.TryChoosePlague() || !skill.TryChoosePlague() || skill.TryChoosePlague() ||
                !progress.TryLearn(SkillBattlePreview.PlagueEvolution) || !skill.TryChoosePlague())
                throw new InvalidOperationException("Plague evolution gate failed.");
            skill.Cast(403);
            skill.Tick(406);
            ExpectDamage(plagueHost, 180);

            plagueHost.CurrentHealth = 15;
            skill.Cast(407);
            var plagueNeighbor = Spawn("Plague neighbor", 1, plagueHost.transform.position + Vector3.right);
            skill.Tick(407);
            if (plagueHost.CurrentHealth != 0)
                throw new InvalidOperationException("Infection did not kill the host.");
            ExpectDamage(plagueNeighbor, 25);
            Debug.Log("JOTUNNSLAYER_PLAGUE_OK base/evolved ticks and host-death explosion");

            progress.Reset();
            var barrierCaster = Spawn("Barrier caster", 0, Vector3.right * 980);
            skill.Bind(barrierCaster, progress, null);
            var barrierInner = Spawn("Barrier inner", 1, barrierCaster.transform.position + Vector3.forward * 3);
            var barrierEdge = Spawn("Barrier edge", 1, barrierInner.transform.position + Vector3.forward * 2);
            if (!skill.TryChooseFireBarrier())
                throw new InvalidOperationException("Fire Barrier selection failed.");
            skill.Cast(410);
            skill.Tick(411);
            ExpectDamage(barrierInner, 16);
            ExpectDamage(barrierEdge, 0);
            skill.Tick(412);
            ExpectDamage(barrierInner, 36);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseFireBarrier() || !skill.TryChooseFireBarrier() || skill.TryChooseFireBarrier() ||
                !progress.TryLearn(SkillBattlePreview.BarrierEvolution) || !skill.TryChooseFireBarrier())
                throw new InvalidOperationException("Fire Barrier evolution gate failed.");
            skill.Cast(415);
            skill.Tick(416);
            ExpectDamage(barrierInner, 36);
            ExpectDamage(barrierEdge, 36);
            skill.Tick(417);
            ExpectDamage(barrierInner, 76);
            ExpectDamage(barrierEdge, 76);
            skill.Tick(418);
            ExpectDamage(barrierInner, 0);
            Debug.Log("JOTUNNSLAYER_FIRE_BARRIER_OK narrow/wide zones, ticks and one explosion");

            progress.Reset();
            var serpentCaster = Spawn("Serpent caster", 0, Vector3.right * 1020);
            skill.Bind(serpentCaster, progress, null);
            var serpentFront = Spawn("Serpent front", 1, serpentCaster.transform.position + Vector3.forward * 6);
            var serpentBack = Spawn("Serpent back", 1, serpentCaster.transform.position + Vector3.back * 6);
            var serpentOutside = Spawn("Serpent outside", 1, serpentCaster.transform.position + Vector3.forward * 8);
            if (!skill.TryChooseSerpent())
                throw new InvalidOperationException("Jormungandr selection failed.");
            skill.Cast(420);
            ExpectDamage(serpentFront, 20);
            ExpectDamage(serpentBack, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseSerpent() || !skill.TryChooseSerpent())
                throw new InvalidOperationException("Jormungandr upgrades failed.");
            skill.Cast(423);
            ExpectDamage(serpentFront, 32);

            if (skill.TryChooseSerpent() || !progress.TryLearn(SkillBattlePreview.SerpentEvolution) || !skill.TryChooseSerpent())
                throw new InvalidOperationException("Jormungandr evolution gate failed.");
            skill.Cast(426);
            ExpectDamage(serpentFront, 35);
            ExpectDamage(serpentBack, 35);
            ExpectDamage(serpentOutside, 0);
            skill.Cast(428);
            ExpectDamage(serpentFront, 0);
            Debug.Log("JOTUNNSLAYER_SERPENT_OK cone growth, evolved full circle, range and cooldown");

            progress.Reset();
            var firestarCaster = Spawn("Firestar caster", 0, Vector3.right * 1060);
            skill.Bind(firestarCaster, progress, null);
            var firestarFirst = Spawn("Firestar first", 1, firestarCaster.transform.position + Vector3.forward * 4);
            var firestarSecond = Spawn("Firestar second", 1, firestarFirst.transform.position + Vector3.right * 3);
            if (!skill.TryChooseFirestar())
                throw new InvalidOperationException("Firestar selection failed.");
            skill.Cast(430);
            skill.Tick(430);
            ExpectDamage(firestarFirst, 16);
            skill.Tick(430.4f);
            ExpectDamage(firestarSecond, 16);

            var evolvedCaster = Spawn("Evolved firestar caster", 0, Vector3.right * 1100);
            skill.Bind(evolvedCaster, progress, null);
            var firestarEvolved = Spawn("Evolved firestar target", 1, evolvedCaster.transform.position + Vector3.forward * 4);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseFirestar() || !skill.TryChooseFirestar() || !skill.TryChooseFirestar() ||
                skill.TryChooseFirestar() || !progress.TryLearn(SkillBattlePreview.FirestarEvolution) || !skill.TryChooseFirestar())
                throw new InvalidOperationException("Firestar evolution gate failed.");
            skill.Cast(435);
            skill.Tick(435);
            ExpectDamage(firestarEvolved, 24);
            skill.Tick(435.5f);
            ExpectDamage(firestarEvolved, 4);
            skill.Tick(436);
            ExpectDamage(firestarEvolved, 28);
            Debug.Log("JOTUNNSLAYER_FIRESTAR_OK base bounce, evolved repeated explosion and burn");

            progress.Reset();
            var viperCaster = Spawn("Viper caster", 0, Vector3.right * 1140);
            skill.Bind(viperCaster, progress, null);
            var viperTarget = Spawn("Viper target", 1, viperCaster.transform.position + Vector3.forward * 5);
            if (!skill.TryChooseVipers())
                throw new InvalidOperationException("Viper selection failed.");
            skill.Cast(450);
            skill.Tick(450.1f);
            ExpectDamage(viperTarget, 0);
            skill.Tick(450.4f);
            ExpectDamage(viperTarget, 30);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseVipers() || !skill.TryChooseVipers() || skill.TryChooseVipers() ||
                !progress.TryLearn(SkillBattlePreview.ViperEvolution) || !skill.TryChooseVipers())
                throw new InvalidOperationException("Viper evolution gate failed.");
            skill.Cast(454);
            skill.Tick(454.4f);
            ExpectDamage(viperTarget, 60);
            skill.Tick(454.8f);
            ExpectDamage(viperTarget, 240);
            skill.Tick(455);
            ExpectDamage(viperTarget, 0);
            Debug.Log("JOTUNNSLAYER_VIPERS_OK delayed contact and 4 + 16 evolved vipers");

            progress.Reset();
            var cometCaster = Spawn("Comet caster", 0, Vector3.right * 1180);
            skill.Bind(cometCaster, progress, null);
            var cometTarget = Spawn("Comet target", 1, cometCaster.transform.position + Vector3.forward * 5);
            if (!skill.TryChooseComet())
                throw new InvalidOperationException("Comet selection failed.");
            skill.Cast(460);
            skill.Tick(460.25f);
            ExpectDamage(cometTarget, 0);
            skill.Tick(460.5f);
            ExpectDamage(cometTarget, 20);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseComet() || !skill.TryChooseComet() || skill.TryChooseComet() ||
                !progress.TryLearn(SkillBattlePreview.CometEvolution) || !skill.TryChooseComet())
                throw new InvalidOperationException("Comet evolution gate failed.");
            skill.Cast(464);
            var cometSpike = Spawn("Comet spike", 1, cometTarget.transform.position + Vector3.right * 4.5f);
            skill.Tick(464.5f);
            ExpectDamage(cometTarget, 0);
            skill.Tick(465);
            ExpectDamage(cometTarget, 40);
            ExpectDamage(cometSpike, 15);
            Debug.Log("JOTUNNSLAYER_COMET_OK delayed impact and evolved spike damage");

            progress.Reset();
            var ringCaster = Spawn("Ring caster", 0, Vector3.right * 1220);
            skill.Bind(ringCaster, progress, null);
            var ringNear = Spawn("Ring near", 1, ringCaster.transform.position + Vector3.forward * 4);
            var ringMiddle = Spawn("Ring middle", 1, ringCaster.transform.position + Vector3.forward * 6);
            var ringFar = Spawn("Ring far", 1, ringCaster.transform.position + Vector3.forward * 8);
            if (!skill.TryChooseFrostRing())
                throw new InvalidOperationException("Frost Ring selection failed.");
            var beforeRingRandom = UnityEngine.Random.state;
            UnityEngine.Random.State winningRoll;
            do
            {
                winningRoll = UnityEngine.Random.state;
            }
            while (UnityEngine.Random.value >= 0.15f);

            UnityEngine.Random.state = winningRoll;
            skill.Cast(470);
            UnityEngine.Random.state = beforeRingRandom;
            if (!SkillBattlePreview.IsStoppedBySkill(ringNear) || SkillBattlePreview.IsStoppedBySkill(ringFar))
                throw new InvalidOperationException("Frost Ring did not freeze only within its radius.");
            skill.Tick(471.79f);
            if (!SkillBattlePreview.IsStoppedBySkill(ringNear))
                throw new InvalidOperationException("Frost Ring freeze expired early.");
            skill.Tick(471.81f);
            if (SkillBattlePreview.IsStoppedBySkill(ringNear))
                throw new InvalidOperationException("Frost Ring freeze did not expire.");
            ExpectDamage(ringNear, 6);
            ExpectDamage(ringMiddle, 6);
            ExpectDamage(ringFar, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseFrostRing() || !skill.TryChooseFrostRing() || skill.TryChooseFrostRing() ||
                !progress.TryLearn(SkillBattlePreview.RingEvolution) || !skill.TryChooseFrostRing())
                throw new InvalidOperationException("Frost Ring evolution gate failed.");
            skill.Cast(475);
            skill.Tick(475);
            ExpectDamage(ringNear, 12);
            ExpectDamage(ringMiddle, 0);
            skill.Tick(475.25f);
            ExpectDamage(ringMiddle, 12);
            ExpectDamage(ringFar, 0);
            skill.Tick(475.5f);
            ExpectDamage(ringNear, 24);
            ExpectDamage(ringMiddle, 12);
            ExpectDamage(ringFar, 12);
            Debug.Log("JOTUNNSLAYER_FROST_RING_OK base hit and three expanding evolved waves");

            progress.Reset();
            var frostArrowCaster = Spawn("Frost Arrow caster", 0, Vector3.right * 1260);
            skill.Bind(frostArrowCaster, progress, null);
            var frostPrimary = Spawn("Frost Arrow primary", 1, frostArrowCaster.transform.position + Vector3.forward * 5);
            var frostSide = Spawn("Frost Arrow side", 1, frostPrimary.transform.position + Vector3.right * 3);
            var frostForward = Spawn("Frost Arrow forward", 1, frostPrimary.transform.position + Vector3.forward * 3);
            if (!skill.TryChooseFrostArrow())
                throw new InvalidOperationException("Frost Arrow selection failed.");
            skill.Cast(480);
            ExpectDamage(frostPrimary, 30);
            ExpectDamage(frostSide, 12);
            ExpectDamage(frostForward, 0);
            if (!SkillBattlePreview.IsStoppedBySkill(frostPrimary) || SkillBattlePreview.IsStoppedBySkill(frostSide))
                throw new InvalidOperationException("Frost Arrow freeze did not target the primary victim.");
            skill.Tick(483);
            if (SkillBattlePreview.IsStoppedBySkill(frostPrimary))
                throw new InvalidOperationException("Frost Arrow freeze did not expire.");

            skill.SetRaidLevel(18);
            if (!skill.TryChooseFrostArrow() || !skill.TryChooseFrostArrow() || skill.TryChooseFrostArrow() ||
                !progress.TryLearn(SkillBattlePreview.FrostArrowEvolution) || !skill.TryChooseFrostArrow())
                throw new InvalidOperationException("Frost Arrow evolution gate failed.");
            skill.Cast(484);
            ExpectDamage(frostPrimary, 50);
            ExpectDamage(frostSide, 20);
            ExpectDamage(frostForward, 20);
            Debug.Log("JOTUNNSLAYER_FROST_ARROW_OK base/evolved splits and three-second freeze state");

            progress.Reset();
            var snowCaster = Spawn("Snow caster", 0, Vector3.right * 1300);
            skill.Bind(snowCaster, progress, null);
            var snowTarget = Spawn("Snow target", 1, snowCaster.transform.position + Vector3.forward * 2);
            if (!skill.TryChooseSnowflake())
                throw new InvalidOperationException("Snowflake selection failed.");
            skill.Cast(490);
            skill.Tick(494);
            ExpectDamage(snowTarget, 4 * 16 + 6 * 8);
            skill.Tick(495);
            ExpectDamage(snowTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseSnowflake() || !skill.TryChooseSnowflake() || skill.TryChooseSnowflake() ||
                !progress.TryLearn(SkillBattlePreview.SnowEvolution) || !skill.TryChooseSnowflake())
                throw new InvalidOperationException("Snowflake evolution gate failed.");
            skill.Cast(498);
            skill.Tick(505.01f);
            ExpectDamage(snowTarget, 7 * 25 + 36 * 8);
            skill.Tick(506);
            ExpectDamage(snowTarget, 0);
            Debug.Log("JOTUNNSLAYER_SNOWFLAKE_OK primary and secondary hit counts and expiry");

            progress.Reset();
            var iceCaster = Spawn("Ice caster", 0, Vector3.right * 1340);
            skill.Bind(iceCaster, progress, null);
            var iceVictim = Spawn("Ice victim", 1, iceCaster.transform.position + Vector3.forward * 4);
            if (skill.TryChooseIceField() || !progress.TryLearn(SkillBattlePreview.IceUnlock) || !skill.TryChooseIceField())
                throw new InvalidOperationException("Ice Field selection failed.");
            skill.Cast(510);
            if (SkillBattlePreview.IceMoveMultiplier(iceCaster) != 1.35f ||
                SkillBattlePreview.IceMoveMultiplier(iceVictim) != 1f)
                throw new InvalidOperationException("Ice Field movement modifier affected the wrong team.");
            iceCaster.transform.position += Vector3.right * 6;
            if (SkillBattlePreview.IceMoveMultiplier(iceCaster) != 1f)
                throw new InvalidOperationException("Ice Field movement modifier survived leaving the field.");
            iceCaster.transform.position -= Vector3.right * 6;
            skill.Tick(515);
            ExpectDamage(iceVictim, 0);
            if (SkillBattlePreview.IceMoveMultiplier(iceCaster) != 1f)
                throw new InvalidOperationException("Ice Field movement modifier survived expiry.");

            skill.SetRaidLevel(18);
            if (!skill.TryChooseIceField() || !skill.TryChooseIceField() || skill.TryChooseIceField() ||
                !progress.TryLearn(SkillBattlePreview.IceEvolution) || !skill.TryChooseIceField())
                throw new InvalidOperationException("Ice Field evolution gate failed.");
            skill.Cast(522);
            skill.Tick(526.9f);
            ExpectDamage(iceVictim, 0);
            skill.Tick(527);
            ExpectDamage(iceVictim, 50);
            skill.Tick(528);
            ExpectDamage(iceVictim, 0);
            Debug.Log("JOTUNNSLAYER_ICE_FIELD_OK team movement modifier, expiry and evolved blast");

            progress.Reset();
            var spearCaster = Spawn("Gungnir caster", 0, Vector3.right * 1380);
            skill.Bind(spearCaster, progress, null);
            randomState = UnityEngine.Random.state;
            angle = UnityEngine.Random.Range(0f, 360f);
            UnityEngine.Random.state = randomState;
            direction = Quaternion.Euler(0, angle, 0) * Vector3.forward;
            var spearNear = Spawn("Gungnir near", 1, spearCaster.transform.position + direction * 3);
            var spearFar = Spawn("Gungnir far", 1, spearCaster.transform.position + direction * 7);
            if (!skill.TryChooseGungnir())
                throw new InvalidOperationException("Gungnir selection failed.");
            skill.Cast(530);
            ExpectDamage(spearNear, 20);
            ExpectDamage(spearFar, 20);
            skill.Cast(533);
            ExpectDamage(spearNear, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseGungnir() || !skill.TryChooseGungnir() || skill.TryChooseGungnir() ||
                !progress.TryLearn(SkillBattlePreview.GungnirEvolution) || !skill.TryChooseGungnir())
                throw new InvalidOperationException("Gungnir evolution gate failed.");
            spearNear.transform.position = spearCaster.transform.position + Vector3.forward * 3;
            spearFar.transform.position = spearCaster.transform.position + Vector3.forward * 8 + Vector3.right * 1.4f;
            skill.Cast(534);
            ExpectDamage(spearNear, 35);
            ExpectDamage(spearFar, 35);
            Debug.Log("JOTUNNSLAYER_GUNGNIR_OK piercing spear, cooldown and evolved impact row");

            progress.Reset();
            var ravenCaster = Spawn("Raven caster", 0, Vector3.right * 1420);
            skill.Bind(ravenCaster, progress, null);
            var ravenTarget = Spawn("Raven target", 1, ravenCaster.transform.position + Vector3.forward * 5);
            if (!skill.TryChooseRavens())
                throw new InvalidOperationException("Ravens selection failed.");
            skill.Cast(540);
            skill.Tick(541);
            ExpectDamage(ravenTarget, 14);
            skill.Tick(543);
            ExpectDamage(ravenTarget, 28);
            skill.Tick(544);
            ExpectDamage(ravenTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseRavens() || !skill.TryChooseRavens() || skill.TryChooseRavens() ||
                !progress.TryLearn(SkillBattlePreview.RavenEvolution) || !skill.TryChooseRavens())
                throw new InvalidOperationException("Ravens evolution gate failed.");
            skill.Cast(548);
            skill.Tick(548.5f);
            ExpectDamage(ravenTarget, 20);
            ravenCaster.transform.position += Vector3.right * 4;
            ravenTarget.transform.position += Vector3.right * 2;
            skill.Tick(549);
            ExpectDamage(ravenTarget, 20);
            Debug.Log("JOTUNNSLAYER_RAVENS_OK three base ticks and evolved outbound/return hits");

            progress.Reset();
            var ragnarokCaster = Spawn("Ragnarok caster", 0, Vector3.right * 1460);
            skill.Bind(ragnarokCaster, progress, null);
            var ragnarokTarget = Spawn("Ragnarok target", 1, ragnarokCaster.transform.position + Vector3.forward * 5);
            var ragnarokAlly = Spawn("Ragnarok ally", 0, ragnarokCaster.transform.position + Vector3.right);
            if (skill.TryChooseRagnarok() || !progress.TryLearn(SkillBattlePreview.RagnarokUnlock) ||
                !skill.TryChooseRagnarok())
                throw new InvalidOperationException("Ragnarok selection failed.");
            skill.Cast(550);
            ExpectDamage(ragnarokTarget, 500);
            skill.Cast(594);
            ExpectDamage(ragnarokTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseRagnarok() || !skill.TryChooseRagnarok() || skill.TryChooseRagnarok() ||
                !progress.TryLearn(SkillBattlePreview.RagnarokEvolution) || !skill.TryChooseRagnarok())
                throw new InvalidOperationException("Ragnarok evolution gate failed.");
            skill.Cast(595);
            ExpectDamage(ragnarokTarget, 700);
            skill.Cast(624);
            ExpectDamage(ragnarokTarget, 0);
            skill.Cast(625);
            ExpectDamage(ragnarokTarget, 700);
            ExpectDamage(ragnarokAlly, 0);
            Debug.Log("JOTUNNSLAYER_RAGNAROK_OK base/evolved damage, cooldown and friendly exclusion");

            progress.Reset();
            var boulderCaster = Spawn("Boulder caster", 0, Vector3.right * 1500);
            skill.Bind(boulderCaster, progress, null);
            var boulderTarget = Spawn("Boulder path", 1, boulderCaster.transform.position + Vector3.forward * 3);
            if (!skill.TryChooseBoulder())
                throw new InvalidOperationException("Boulder selection failed.");
            skill.Cast(630);
            skill.Tick(631);
            ExpectDamage(boulderTarget, 18);
            skill.Tick(632);
            ExpectDamage(boulderTarget, 0);
            skill.Tick(634);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseBoulder() || !skill.TryChooseBoulder() || skill.TryChooseBoulder() ||
                !progress.TryLearn(SkillBattlePreview.BoulderEvolution) || !skill.TryChooseBoulder())
                throw new InvalidOperationException("Boulder evolution gate failed.");
            boulderTarget.transform.position = boulderCaster.transform.position - Vector3.forward * 1.5f;
            skill.Cast(635);
            skill.Tick(635);
            ExpectDamage(boulderTarget, 38);
            boulderCaster.transform.position += Vector3.right * 3f;
            boulderTarget.transform.position = boulderCaster.transform.position - Vector3.forward * 1.5f + Vector3.right * 2;
            skill.Tick(638);
            ExpectDamage(boulderTarget, 20);
            Debug.Log("JOTUNNSLAYER_BOULDER_OK rolling sweep, following contact and explosion");

            progress.Reset();
            var warriorCaster = Spawn("Warrior caster", 0, Vector3.right * 1540);
            skill.Bind(warriorCaster, progress, null);
            var warriorTarget = Spawn("Warrior target", 1, warriorCaster.transform.position + Vector3.forward * 5);
            if (!skill.TryChooseWarrior())
                throw new InvalidOperationException("Einherjar selection failed.");
            skill.Cast(650);
            skill.Tick(650.1f);
            ExpectDamage(warriorTarget, 0);
            skill.Tick(650.25f);
            ExpectDamage(warriorTarget, 14);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseWarrior() || !skill.TryChooseWarrior() || skill.TryChooseWarrior() ||
                !progress.TryLearn(SkillBattlePreview.WarriorEvolution) || !skill.TryChooseWarrior())
                throw new InvalidOperationException("Einherjar evolution gate failed.");

            var beforeWarriorRandom = UnityEngine.Random.state;
            UnityEngine.Random.State warriorRoll;
            float archerRoll, poisonRoll;
            do
            {
                warriorRoll = UnityEngine.Random.state;
                UnityEngine.Random.Range(0, 1);
                archerRoll = UnityEngine.Random.value;
                poisonRoll = UnityEngine.Random.value;
            }
            while (archerRoll >= 0.75f || poisonRoll >= 0.3f);
            UnityEngine.Random.state = warriorRoll;
            skill.Cast(653);
            var warriorEdge = Spawn("Warrior edge", 1, warriorTarget.transform.position + Vector3.right * 1.5f);
            skill.Tick(653.25f);
            UnityEngine.Random.state = beforeWarriorRandom;
            ExpectDamage(warriorTarget, 40);
            ExpectDamage(warriorEdge, 20);
            skill.Tick(653.56f);
            ExpectDamage(warriorTarget, 3);
            Debug.Log("JOTUNNSLAYER_WARRIOR_OK melee, evolved radius, bonus archer and poison");

            progress.Reset();
            var vortexCaster = Spawn("Vortex caster", 0, Vector3.right * 1580);
            skill.Bind(vortexCaster, progress, null);
            var vortexTarget = Spawn("Vortex target", 1, vortexCaster.transform.position + Vector3.forward * 6);
            if (!skill.TryChooseVortex())
                throw new InvalidOperationException("Vortex selection failed.");
            skill.Cast(660);
            skill.Tick(661);
            ExpectDamage(vortexTarget, 0);
            if (Vector3.Distance(vortexTarget.transform.position, vortexCaster.transform.position + Vector3.forward * 3) > 1.01f)
                throw new InvalidOperationException("Vortex did not pull toward its center.");
            skill.Tick(662.5f);
            ExpectDamage(vortexTarget, 15);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseVortex() || !skill.TryChooseVortex() || skill.TryChooseVortex() ||
                !progress.TryLearn(SkillBattlePreview.VortexEvolution) || !skill.TryChooseVortex())
                throw new InvalidOperationException("Vortex evolution gate failed.");
            skill.Cast(668);
            skill.Tick(669);
            ExpectDamage(vortexTarget, 15);
            skill.Tick(670);
            ExpectDamage(vortexTarget, 15);
            Debug.Log("JOTUNNSLAYER_VORTEX_OK pull, base eruption and advancing evolved ticks");

            progress.Reset();
            var shipCaster = Spawn("Ship caster", 0, Vector3.right * 1620);
            skill.Bind(shipCaster, progress, null);
            var shipTarget = Spawn("Ship target", 1, shipCaster.transform.position + Vector3.forward * 4);
            if (!skill.TryChooseShip())
                throw new InvalidOperationException("Ship Charge selection failed.");
            skill.Cast(680);
            skill.Tick(681);
            ExpectDamage(shipTarget, 24);
            skill.Tick(684);
            ExpectDamage(shipTarget, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseShip() || !skill.TryChooseShip() || skill.TryChooseShip() ||
                !progress.TryLearn(SkillBattlePreview.ShipEvolution) || !skill.TryChooseShip())
                throw new InvalidOperationException("Ship Charge evolution gate failed.");
            shipTarget.transform.position = shipCaster.transform.position + Vector3.forward * 3;
            skill.Cast(689);
            skill.Tick(689.7f);
            bool shipCursed = SkillBattlePreview.HasRaidCurse(shipTarget, Time.time);
            ExpectDamage(shipTarget, shipCursed ? 67 : 61);

            shipTarget.transform.position = shipCaster.transform.position + Vector3.forward * 4 + Vector3.right * 2;
            skill.Tick(689.9f);
            ExpectDamage(shipTarget, shipCursed ? 9 : 7);
            Debug.Log("JOTUNNSLAYER_SHIP_OK hull deduplication and evolved arrows outside the hull");

            progress.Reset();
            var krakenCaster = Spawn("Kraken caster", 0, Vector3.right * 1660);
            skill.Bind(krakenCaster, progress, null);
            var krakenTarget = Spawn("Kraken target", 1, krakenCaster.transform.position + Vector3.forward * 4);
            if (!skill.TryChooseKraken())
                throw new InvalidOperationException("Kraken selection failed.");
            skill.Cast(700);
            skill.Tick(700.1f);
            ExpectDamage(krakenTarget, 0);
            skill.Tick(700.25f);
            ExpectDamage(krakenTarget, 16);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseKraken() || !skill.TryChooseKraken() || skill.TryChooseKraken() ||
                !progress.TryLearn(SkillBattlePreview.KrakenEvolution) || !skill.TryChooseKraken())
                throw new InvalidOperationException("Kraken evolution gate failed.");
            skill.Cast(708);
            skill.Tick(708.25f);
            ExpectDamage(krakenTarget, 50);
            krakenTarget.transform.position += Vector3.forward * 2;
            skill.Tick(708.7f);
            ExpectDamage(krakenTarget, 90);
            skill.Tick(709);
            ExpectDamage(krakenTarget, 0);
            Debug.Log("JOTUNNSLAYER_KRAKEN_OK delayed tentacles and five evolved barrels");

            progress.Reset();
            var leviathanCaster = Spawn("Leviathan caster", 0, Vector3.right * 1700);
            skill.Bind(leviathanCaster, progress, null);
            var leviathanTarget = Spawn("Leviathan target", 1, leviathanCaster.transform.position + Vector3.forward * 4);
            if (!skill.TryChooseLeviathan())
                throw new InvalidOperationException("Leviathan selection failed.");
            skill.Cast(720);
            skill.Tick(720.25f);
            ExpectDamage(leviathanTarget, 20);
            skill.Tick(720.75f);
            ExpectDamage(leviathanTarget, 20);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseLeviathan() || !skill.TryChooseLeviathan() || skill.TryChooseLeviathan() ||
                !progress.TryLearn(SkillBattlePreview.LeviathanEvolution) || !skill.TryChooseLeviathan())
                throw new InvalidOperationException("Leviathan evolution gate failed.");
            skill.Cast(726);
            skill.Tick(726.25f);
            ExpectDamage(leviathanTarget, 40);

            var beforeLeviathanRandom = UnityEngine.Random.state;
            UnityEngine.Random.State repeatRoll;
            do
            {
                repeatRoll = UnityEngine.Random.state;
                _ = UnityEngine.Random.value;
            }
            while (UnityEngine.Random.value >= 0.5f);
            UnityEngine.Random.state = repeatRoll;
            skill.Tick(726.75f);
            ExpectDamage(leviathanTarget, 40);
            skill.Tick(727.25f);
            int repeatDamage = leviathanTarget.MaxHealth - leviathanTarget.CurrentHealth;
            if (repeatDamage != 40 && repeatDamage != 48)
                throw new InvalidOperationException($"Leviathan repeat hit was missing: {repeatDamage}.");
            UnityEngine.Random.state = beforeLeviathanRandom;
            Debug.Log("JOTUNNSLAYER_LEVIATHAN_OK emergence, leap and evolved repeat");

            progress.Reset();
            var bubbleCaster = Spawn("Barrier caster", 0, Vector3.right * 1740);
            skill.Bind(bubbleCaster, progress, null);
            var bubbleEnemy = Spawn("Barrier attacker", 1, bubbleCaster.transform.position + Vector3.forward * 3);
            if (skill.TryChooseTidalBarrier() || !progress.TryLearn(SkillBattlePreview.BubbleUnlock) ||
                !skill.TryChooseTidalBarrier())
                throw new InvalidOperationException("Tidal Barrier selection failed.");
            skill.Cast(740);
            bubbleCaster.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.melee, bubbleEnemy, 0);
            ExpectDamage(bubbleCaster, 70);
            skill.Tick(742.9f);
            ExpectDamage(bubbleEnemy, 0);
            skill.Tick(743);
            ExpectDamage(bubbleEnemy, 40);
            bubbleCaster.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.melee, bubbleEnemy, 0);
            ExpectDamage(bubbleCaster, 100);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseTidalBarrier() || !skill.TryChooseTidalBarrier() || skill.TryChooseTidalBarrier() ||
                !progress.TryLearn(SkillBattlePreview.BubbleEvolution) || !skill.TryChooseTidalBarrier())
                throw new InvalidOperationException("Tidal Barrier evolution gate failed.");
            skill.Cast(750);
            ExpectDamage(bubbleEnemy, 200);
            skill.Tick(753.9f);
            bubbleCaster.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.melee, bubbleEnemy, 0);
            ExpectDamage(bubbleCaster, 70);
            skill.Tick(754);
            ExpectDamage(bubbleEnemy, 0);
            bubbleCaster.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.melee, bubbleEnemy, 0);
            ExpectDamage(bubbleCaster, 100);
            Debug.Log("JOTUNNSLAYER_TIDAL_OK fortify, base expiry burst and evolved activation burst");

            progress.Reset();
            var geboCaster = Spawn("Gebo caster", 0, Vector3.right * 1780);
            skill.Bind(geboCaster, progress, null);
            var geboVictim = Spawn("Gebo target", 1, geboCaster.transform.position + Vector3.forward * 3);
            if (!skill.TryChooseGebo())
                throw new InvalidOperationException("Gebo selection failed.");
            geboCaster.Server_KilledCharacter?.Invoke(geboVictim);
            if (skill.RaidExperience != 37)
                throw new InvalidOperationException("Gebo rank one XP was incorrect.");

            skill.SetRaidLevel(18);
            if (!skill.TryChooseGebo() || !skill.TryChooseGebo() || skill.TryChooseGebo() ||
                !progress.TryLearn("passive_broksindri_O_evolve") ||
                !progress.TryLearn(SkillBattlePreview.GeboEvolution) || skill.TryChooseGebo())
                throw new InvalidOperationException("Gebo evolution gate failed.");
            skill.SetRaidLevel(22);
            if (!skill.TryChooseGebo())
                throw new InvalidOperationException("Gebo evolution was not offered at level 22.");
            long beforeGeboXp = skill.RaidExperience;
            geboCaster.Server_KilledCharacter?.Invoke(geboVictim);
            if (skill.RaidExperience - beforeGeboXp != 49)
                throw new InvalidOperationException("Evolved Gebo XP was incorrect.");

            skill.SetRaidLevel(1);
            var beforeGeboRandom = UnityEngine.Random.state;
            UnityEngine.Random.State geboRoll;
            do
            {
                geboRoll = UnityEngine.Random.state;
            }
            while (UnityEngine.Random.value >= 0.2f);
            UnityEngine.Random.state = geboRoll;
            geboCaster.Server_KilledCharacter?.Invoke(geboVictim);
            UnityEngine.Random.state = beforeGeboRandom;
            if (skill.RaidLevel != 2 || skill.BonusRewards != 1)
                throw new InvalidOperationException("Evolved Gebo did not grant a bonus level reward.");
            Debug.Log("JOTUNNSLAYER_GEBO_OK rank-one/evolved XP and bonus reward");

            progress.Reset();
            var regenCaster = Spawn("Regeneration caster", 0, Vector3.right * 1820);
            skill.Bind(regenCaster, progress, null);
            regenCaster.CurrentHealth = 900;
            if (!skill.TryChooseRegenerationRune())
                throw new InvalidOperationException("Regeneration rune selection failed.");
            skill.Tick(760);
            skill.Tick(760.9f);
            ExpectDamage(regenCaster, 100);
            regenCaster.CurrentHealth = 900;
            skill.Tick(761);
            ExpectDamage(regenCaster, 98);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseRegenerationRune() || !skill.TryChooseRegenerationRune() ||
                skill.TryChooseRegenerationRune() || !progress.TryLearn(SkillBattlePreview.RegenerationEvolution) ||
                skill.TryChooseRegenerationRune())
                throw new InvalidOperationException("Regeneration rune evolution gate failed.");
            skill.SetRaidLevel(22);
            if (!skill.TryChooseRegenerationRune())
                throw new InvalidOperationException("Regeneration rune evolution was not offered at level 22.");

            regenCaster.CurrentHealth = 900;
            skill.Tick(763);
            ExpectDamage(regenCaster, 88);
            regenCaster.CurrentHealth = 998;
            skill.Tick(764);
            ExpectDamage(regenCaster, 0);
            var reviveEnemy = Spawn("Revive attacker", 1, regenCaster.transform.position + Vector3.forward);
            int runeDeaths = 0;
            regenCaster.Server_OnHealthDepleted += (_, _, _, _) => runeDeaths++;
            regenCaster.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, reviveEnemy, 0);
            if (regenCaster.CurrentHealth != regenCaster.MaxHealth || runeDeaths != 0)
                throw new InvalidOperationException("Rune did not intercept lethal damage before the death event.");
            regenCaster.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, reviveEnemy, 0);
            if (regenCaster.CurrentHealth != 0 || runeDeaths != 1)
                throw new InvalidOperationException("Rune did not consume its single revive charge.");

            Debug.Log("JOTUNNSLAYER_REGEN_RUNE_OK base/evolved healing, max-health clamp and one lethal save");

            progress.Reset();
            var healthRuneCaster = Spawn("Health Rune caster", 0, Vector3.right * 1860);
            skill.Bind(healthRuneCaster, progress, null);
            if (!skill.TryChooseHealthRune() || healthRuneCaster.MaxHealth != 1100)
                throw new InvalidOperationException("Health Rune rank one maximum health was incorrect.");

            healthRuneCaster.ServerSetMaximumHealth(healthRuneCaster.MaxHealth + 25);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseHealthRune() || healthRuneCaster.MaxHealth != 1185 ||
                !skill.TryChooseHealthRune() || healthRuneCaster.MaxHealth != 1245 || skill.TryChooseHealthRune() ||
                !progress.TryLearn(SkillBattlePreview.RegenerationEvolution) ||
                !progress.TryLearn(SkillBattlePreview.HealthRuneEvolution) || skill.TryChooseHealthRune())
                throw new InvalidOperationException("Health Rune tier or level 22 gate failed.");
            skill.SetRaidLevel(22);
            if (!skill.TryChooseHealthRune() || healthRuneCaster.MaxHealth != 1375 ||
                SkillBattlePreview.ScaleFoodHealing(healthRuneCaster, 50) != 100)
                throw new InvalidOperationException("Health Rune upgrades did not preserve another additive modifier.");

            progress.Reset();
            skill.Tick(770);
            if (healthRuneCaster.MaxHealth != 1245)
                throw new InvalidOperationException("Health Rune retained evolved stats after reset.");
            var healthRuneNext = Spawn("Health Rune replacement", 0, Vector3.right * 1900);
            skill.Bind(healthRuneNext, progress, null);
            if (healthRuneCaster.MaxHealth != 1025 || healthRuneNext.MaxHealth != 1000)
                throw new InvalidOperationException("Health Rune leaked into a different character lifetime.");
            Debug.Log("JOTUNNSLAYER_HEALTH_RUNE_OK maximum health growth, reset and rebinding");

            progress.Reset();
            var wingCaster = Spawn("Wing caster", 0, Vector3.right * 1940);
            skill.Bind(wingCaster, progress, null);
            var wingFront = Spawn("Wing front", 1, wingCaster.transform.position + Vector3.forward * 5);
            var wingEdge = Spawn("Wing edge", 1, wingCaster.transform.position +
                Quaternion.Euler(0, 55, 0) * Vector3.forward * 6);
            if (!skill.TryChooseWingStrike())
                throw new InvalidOperationException("Wing Strike selection failed.");
            skill.Cast(780);
            ExpectDamage(wingFront, 15);
            ExpectDamage(wingEdge, 0);
            skill.Cast(784);
            ExpectDamage(wingFront, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseWingStrike() || !skill.TryChooseWingStrike() || skill.TryChooseWingStrike() ||
                !progress.TryLearn(SkillBattlePreview.WingEvolution) || !skill.TryChooseWingStrike())
                throw new InvalidOperationException("Wing Strike evolution gate failed.");

            var beforeWingRandom = UnityEngine.Random.state;
            UnityEngine.Random.State wingRoll;
            do
            {
                wingRoll = UnityEngine.Random.state;
            }
            while (UnityEngine.Random.value >= 0.25f);
            UnityEngine.Random.state = wingRoll;
            skill.Cast(785);
            UnityEngine.Random.state = beforeWingRandom;
            ExpectDamage(wingFront, 35);
            ExpectDamage(wingEdge, 35);
            if (!SkillBattlePreview.IsStoppedBySkill(wingFront))
                throw new InvalidOperationException("Wing Strike did not apply stun.");
            skill.Tick(787);
            if (SkillBattlePreview.IsStoppedBySkill(wingFront))
                throw new InvalidOperationException("Wing Strike stun did not expire.");
            Debug.Log("JOTUNNSLAYER_WING_OK base/evolved arcs, cooldown and stun expiry");

            progress.Reset();
            var breathCaster = Spawn("Breath caster", 0, Vector3.right * 1980);
            skill.Bind(breathCaster, progress, null);
            var breathTarget = Spawn("Breath target", 1, breathCaster.transform.position + Vector3.forward * 7);
            var breathSide = Spawn("Breath side", 1, breathTarget.transform.position + Vector3.right * 2);
            if (skill.TryChooseCalamity() || !progress.TryLearn(SkillBattlePreview.CalamityUnlock) || !skill.TryChooseCalamity())
                throw new InvalidOperationException("Calamity Breath selection failed.");
            skill.Cast(790);
            ExpectDamage(breathTarget, 50);
            ExpectDamage(breathSide, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseCalamity() || !skill.TryChooseCalamity() || skill.TryChooseCalamity() ||
                !progress.TryLearn(SkillBattlePreview.CalamityEvolution) || !skill.TryChooseCalamity())
                throw new InvalidOperationException("Calamity Breath evolution gate failed.");

            var beforeBreathRandom = UnityEngine.Random.state;
            UnityEngine.Random.State breathRoll;
            do
            {
                breathRoll = UnityEngine.Random.state;
            }
            while (UnityEngine.Random.value >= 0.4f);
            UnityEngine.Random.state = breathRoll;
            skill.Cast(800);
            UnityEngine.Random.state = beforeBreathRandom;
            ExpectDamage(breathTarget, 70);
            if (!SkillBattlePreview.IsRootedByCalamity(breathTarget))
                throw new InvalidOperationException("Calamity Breath root did not apply.");
            skill.Tick(803);
            ExpectDamage(breathTarget, 45);
            ExpectDamage(breathSide, 0);
            if (SkillBattlePreview.IsRootedByCalamity(breathTarget))
                throw new InvalidOperationException("Calamity Breath root did not expire.");
            skill.Tick(804);
            ExpectDamage(breathTarget, 0);
            Debug.Log("JOTUNNSLAYER_CALAMITY_OK track hit, evolved corruption and root expiry");

            progress.Reset();
            var rayCaster = Spawn("Disease ray caster", 0, Vector3.right * 2020);
            skill.Bind(rayCaster, progress, null);
            var rayPrimary = Spawn("Disease ray primary", 1, rayCaster.transform.position + Vector3.forward * 6);
            var raySecond = Spawn("Disease ray secondary", 1, rayPrimary.transform.position + Vector3.right * 3);
            var rayEdge = Spawn("Disease ray edge", 1, rayPrimary.transform.position + Vector3.right * 1.5f);
            var rayExtra = Spawn("Disease ray extra", 1, rayPrimary.transform.position + Vector3.left * 4);
            if (!skill.TryChooseDiseaseRay())
                throw new InvalidOperationException("Disease ray selection failed.");
            skill.Cast(810);
            ExpectDamage(rayPrimary, 14);
            ExpectDamage(raySecond, 6);
            ExpectDamage(rayEdge, 6);
            ExpectDamage(rayExtra, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseDiseaseRay() || !skill.TryChooseDiseaseRay() || skill.TryChooseDiseaseRay() ||
                !progress.TryLearn(SkillBattlePreview.DiseaseRayEvolution) || !skill.TryChooseDiseaseRay())
                throw new InvalidOperationException("Disease ray evolution gate failed.");
            skill.Cast(813);
            ExpectDamage(rayPrimary, 35);
            ExpectDamage(raySecond, 15);
            ExpectDamage(rayEdge, 50);
            ExpectDamage(rayExtra, 15);
            skill.Cast(815);
            ExpectDamage(rayPrimary, 0);
            Debug.Log("JOTUNNSLAYER_DISEASE_RAY_OK primary radius, secondary limits and cooldown");

            var beforeDiseaseRandom = UnityEngine.Random.state;
            UnityEngine.Random.State diseaseRoll;
            do
            {
                diseaseRoll = UnityEngine.Random.state;
            }
            while (UnityEngine.Random.value >= 0.5f);
            UnityEngine.Random.state = diseaseRoll;
            skill.Cast(820);
            UnityEngine.Random.state = beforeDiseaseRandom;
            if (!SkillBattlePreview.HasDisease(rayPrimary))
                throw new InvalidOperationException("Disease Ray did not apply disease.");
            rayPrimary.CurrentHealth = 800;
            if (rayPrimary.ServerHeal(100, rayCaster.netId) != 50)
                throw new InvalidOperationException("Disease did not halve actual healing.");
            skill.Tick(823);
            if (SkillBattlePreview.HasDisease(rayPrimary) || rayPrimary.ServerHeal(100, rayCaster.netId) != 100)
                throw new InvalidOperationException("Disease healing reduction did not expire.");
            Debug.Log("JOTUNNSLAYER_DISEASE_OK applied status, half healing and expiry");

            progress.Reset();
            var raiseCaster = Spawn("Raise Dead caster", 0, Vector3.right * 2060);
            skill.Bind(raiseCaster, progress, null);
            var raiseTarget = Spawn("Raise Dead target", 1, raiseCaster.transform.position + Vector3.forward * 5);
            if (!skill.TryChooseRaiseDead())
                throw new InvalidOperationException("Raise Dead selection failed.");
            skill.Cast(830);
            if (!SkillBattlePreview.IsSlowedByRaiseDead(raiseTarget))
                throw new InvalidOperationException("Raise Dead area slow was missing.");
            skill.Tick(833);
            ExpectDamage(raiseTarget, 60);
            if (SkillBattlePreview.IsSlowedByRaiseDead(raiseTarget))
                throw new InvalidOperationException("Raise Dead area slow did not expire.");

            skill.SetRaidLevel(18);
            if (!skill.TryChooseRaiseDead() || !skill.TryChooseRaiseDead() || skill.TryChooseRaiseDead() ||
                !progress.TryLearn(SkillBattlePreview.RaiseEvolution) || !skill.TryChooseRaiseDead())
                throw new InvalidOperationException("Raise Dead evolution gate failed.");
            var raiseLeft = Spawn("Raise Dead left", 1, raiseTarget.transform.position + Vector3.left * 1.5f);
            var raiseRight = Spawn("Raise Dead right", 1, raiseTarget.transform.position + Vector3.right * 1.5f);
            skill.Cast(836);
            skill.Tick(841);
            ExpectDamage(raiseTarget, 0);
            ExpectDamage(raiseLeft, 180);
            ExpectDamage(raiseRight, 180);
            Debug.Log("JOTUNNSLAYER_RAISE_OK base area and evolved dual lanes");

            progress.Reset();
            var terrorCaster = Spawn("Terror caster", 0, Vector3.right * 2100);
            skill.Bind(terrorCaster, progress, null);
            var terrorVictim = Spawn("Terror victim", 1, terrorCaster.transform.position + Vector3.forward * 2);
            if (!skill.TryChooseTerror())
                throw new InvalidOperationException("Terror Crush selection failed.");
            skill.Cast(850);
            ExpectDamage(terrorVictim, 3);
            skill.Cast(850.5f);
            ExpectDamage(terrorVictim, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseTerror() || !skill.TryChooseTerror() || skill.TryChooseTerror() ||
                !progress.TryLearn(SkillBattlePreview.TerrorEvolution) || !skill.TryChooseTerror())
                throw new InvalidOperationException("Terror Crush evolution gate failed.");

            var beforeTerrorRandom = UnityEngine.Random.state;
            UnityEngine.Random.State terrorRoll;
            float fearRoll, bombRoll;
            do
            {
                terrorRoll = UnityEngine.Random.state;
                fearRoll = UnityEngine.Random.value;
                bombRoll = UnityEngine.Random.value;
            }
            while (fearRoll >= 0.05f || bombRoll >= 0.2f);
            UnityEngine.Random.state = terrorRoll;
            skill.Cast(852);
            UnityEngine.Random.state = beforeTerrorRandom;
            ExpectDamage(terrorVictim, 12);
            if (!SkillBattlePreview.TryGetTerrorDirection(terrorVictim, out var terrorFlee) || terrorFlee.z <= 0)
                throw new InvalidOperationException("Terror Crush did not produce a direction away from the caster.");
            skill.Tick(853);
            ExpectDamage(terrorVictim, 30);
            skill.Tick(853.5f);
            if (SkillBattlePreview.TryGetTerrorDirection(terrorVictim, out _))
                throw new InvalidOperationException("Terror Crush fear did not expire.");
            Debug.Log("JOTUNNSLAYER_TERROR_OK aura, fear direction and evolved living bomb");

            progress.Reset();
            var daggerCaster = Spawn("Dagger caster", 0, Vector3.right * 2140);
            skill.Bind(daggerCaster, progress, null);
            var daggerFront = Spawn("Dagger front", 1, daggerCaster.transform.position + Vector3.forward * 6);
            var daggerBack = Spawn("Dagger back", 1, daggerCaster.transform.position + Vector3.back * 6);
            var daggerSide = Spawn("Dagger side", 1, daggerCaster.transform.position +
                Quaternion.Euler(0, 80, 0) * Vector3.forward * 6);
            if (!skill.TryChooseDaggerDance())
                throw new InvalidOperationException("Dagger Dance selection failed.");
            skill.Cast(860);
            ExpectDamage(daggerFront, 13);
            ExpectDamage(daggerBack, 0);
            ExpectDamage(daggerSide, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseDaggerDance() || !skill.TryChooseDaggerDance())
                throw new InvalidOperationException("Dagger Dance base upgrades failed.");
            skill.Cast(864);
            ExpectDamage(daggerFront, 13);
            ExpectDamage(daggerSide, 13);
            ExpectDamage(daggerBack, 0);

            if (skill.TryChooseDaggerDance() || !progress.TryLearn(SkillBattlePreview.DaggerEvolution) ||
                !skill.TryChooseDaggerDance())
                throw new InvalidOperationException("Dagger Dance evolution gate failed.");
            skill.Cast(868);
            ExpectDamage(daggerFront, 45);
            ExpectDamage(daggerBack, 30);
            ExpectDamage(daggerSide, 30);
            skill.Cast(872);
            ExpectDamage(daggerFront, 0);
            Debug.Log("JOTUNNSLAYER_DAGGERS_OK fan growth, two-turn evolution and cooldown");
            ValidateShrapnelPot(skill, progress);
            ValidateFlamingRay(skill, progress);
            ValidateSisterhood(skill, progress);
            ValidateFlamingDarts(skill, progress);
            ValidateHammertime(skill, progress);
            ValidateThorHealth(skill, progress);
            ValidateIronFist(skill, progress);
            ValidateShieldwall(skill, progress);
            ValidateAnvil(skill, progress);
            ValidateSeismic(skill, progress);
            ValidateValkyrieCharge(skill, progress);
            ValidateFallenValkyries(skill, progress);
            ValidateSpearBarrier(skill, progress);
            ValidateJavelins(skill, progress);
            ValidateShadowImages(skill, progress);
            ValidateShadowImageDamagePrecision(skill, progress);
            ValidateLiveRespawn(progress);
            ValidatePermanentEconomy(skill, progress);
            ValidateLokiGold(skill, progress);
            ValidateSeeressGold(progress);
            ValidatePermanentMovement();
            ValidateLiveOffer(progress);
            ValidateRewardFallbacks();
            ValidateRulebreaker();
            ValidatePermanentCooldown();
            ValidateRewardRerolls();
            ValidateRewardBanishes();
            ValidateFreyaRevive();
            ValidateRewardSeals();
            ValidateDivination();
            ValidateSpearhead();
            ValidateFleetFooted();
            ValidateDivineRebirth();
            ValidateGodDamageBonus();
            ValidatePermanentDivineRefund();
            ValidateRaidAttackSpeed();
            ValidateAttackSpeedRefund();
            ValidateRaidWeaponDamage();
            ValidateOdinCombatPassives();
            ValidateDoubleRewardVirtue("virtue_odin_03_", 0.05f);
            ValidateDoubleRewardVirtue("virtue_broksindri_03_", 0.04f);
            ValidateLokiPassives();
            ValidateRaido();
            ValidateHagalaz();
            ValidateInguz();
            ValidateUruz();
            ValidateLaguz();
            ValidateKenaz();
            ValidateJera();
            ValidateTiwaz();
            ValidateRuneDamage(godSkill: false);
            ValidateRuneDamage(godSkill: true);
            ValidateMannazCooldownReset();
            ValidatePerth();
            ValidateAnsuzPersistentCasts();
            ValidateIceBlock();
            ValidateSnowFight();
            ValidateNjordPassives();
            ValidateNidhoggPassives();
            ValidateRaidDamageReduction();
            ValidatePermanentCombatRefund();
            ValidateRemoteSkillLoadouts();
            ValidateRewardLocalization();
            ValidateBulwark();
            ValidateFormation();
            ValidateHealingWard();
            ValidatePremonition();
            ValidateBoneChill();
            ValidateIllOmen();
            ValidateMindBlastSlow();
            ValidateBriarRoot();
            ValidateWardEffectiveness();
            ValidateTimeManipulation();
            ValidateWardRechargeVirtue();
            ValidateThorAllDamage();
            ValidateLokiEvasion();
            ValidateValkyrieEvasion();
            ValidateRaidCurse();
            ValidateMalediction();
            ValidateEssenceExpansion();
            ValidatePrimevalCurrents();
            ValidateWinterRaven();
            ValidateAncientKnowledge();
            ValidateExperienceCrystals();
            ValidateSeerFortune();
            ValidateMaliciousWisdom();
            ValidateExtraction();
            ValidateSoulReaper();
            ValidateDarkTidings();
            ValidateSpitefulPace();
            ValidateSoulmate();
            ValidateSwindle();
            ValidateCounterattack();
            ValidateBlazingSoul();
            ValidateChainReaction();
            ValidateHeartEvolutionBleed();
            ValidateRaidCritical();
            ValidateOpenWounds();
            ValidateRage();
            ValidateLichborne();
            ValidateMilitiaTraining();
            ValidateGourmet();
            ValidateFreyaSupport();
            ValidateLastStand();
            ValidateProjectileReflection();
            ValidateThorPassives();
            ValidateRaidDash();
            ValidateSoulShards();

            Debug.Log("JOTUNNSLAYER_BATTLE_OK missiles 40/72/104/234; sentinel cadence 0.85/0.55 and evolution 35; cooldown, ally exclusion and 35 XP level threshold");
        }

        private static void ValidateSoulShards()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Soul Shards validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            Vector3 origin = Vector3.right * 5300;
            var actor = Spawn("Soul Shards caster", 0, origin);
            var target = Spawn("Soul Shards target", 1, origin + Vector3.forward * 3f);
            var ally = Spawn("Soul Shards ally", 0, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseSoulShards() || !progress.TryLearn("passive_ranger_P") || !runtime.TryChooseSoulShards())
                throw new InvalidOperationException("Soul Shards unlock gate failed.");

            if (!runtime.TryStartRaidDash(Vector3.forward, 3200))
                throw new InvalidOperationException("Soul Shards validation could not start Ghostwalk.");
            runtime.Tick(3200);
            ExpectDamage(target, 10);
            runtime.Tick(3201);
            ExpectDamage(target, 20);
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseSoulShards())
                throw new InvalidOperationException("Soul Shards rank two was offered below level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseSoulShards() || !runtime.TryStartRaidDash(Vector3.forward, 3204))
                throw new InvalidOperationException("Soul Shards rank two or dash readiness failed.");
            runtime.Tick(3205);
            ExpectDamage(target, 50);
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseSoulShards() || runtime.TryChooseSoulShards() ||
                !runtime.TryStartRaidDash(Vector3.forward, 3208))
                throw new InvalidOperationException("Soul Shards rank three or cap failed.");
            runtime.Tick(3209);
            ExpectDamage(target, 70);
            ExpectDamage(ally, 0);
            runtime.TryStartRaidDash(Vector3.forward, 3212);
            runtime.Bind(null, progress, null);
            runtime.Tick(3213);
            ExpectDamage(target, 0);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_SOUL_SHARDS_OK unlock, arrow counts, timing, ally exclusion and teardown");
        }

        private static void ValidateRaidDash()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 25);
            foreach (string id in new[] { "virtue_ranger_03_a", "virtue_ranger_03_b",
                "virtue_valkyrie_01_a", "virtue_valkyrie_01_b", "virtue_valkyrie_01_c", "virtue_valkyrie_01_d" })
            {
                if (!progress.TryLearn(id))
                    throw new InvalidOperationException("Could not learn the dash virtues.");
            }

            Vector3 origin = Vector3.right * 5240;
            var actor = Spawn("Raid dash caster", 0, origin);
            var attacker = Spawn("Raid dash attacker", 1, origin + Vector3.forward);
            var ranger = new GameObject("Ghostwalk validation").AddComponent<SkillBattlePreview>();
            ranger.LiveMatch = true;
            ranger.SelectedRaidClass = RaidClass.Ranger;
            ranger.Bind(actor, progress, null);
            float now = Time.time;
            if (!ranger.TryStartRaidDash(Vector3.forward, now) || ranger.TryStartRaidDash(Vector3.forward, now + 3.99f))
                throw new InvalidOperationException("Raid dash did not enforce its base cooldown.");

            Vector3 first = SkillBattlePreview.ApplyRaidDash(actor, Vector3.down, now + 0.1f);
            Vector3 second = SkillBattlePreview.ApplyRaidDash(actor, Vector3.zero, now + 0.5f);
            if (Mathf.Abs(first.z + second.z - 3.96f) > 0.01f || first.y != -1f ||
                SkillBattlePreview.ApplyRaidDash(actor, Vector3.zero, now + 0.6f) != Vector3.zero)
                throw new InvalidOperationException("Raid dash displacement was not consumed exactly once.");
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, attacker, 0);
            ExpectDamage(actor, 50);
            if (!SkillBattlePreview.HasGhostwalk(actor, now + 1.59f) ||
                SkillBattlePreview.HasGhostwalk(actor, now + 1.61f))
                throw new InvalidOperationException("Ghostwalk did not apply the two extension nodes.");
            progress.Reset();
            if (!ranger.TryStartRaidDash(Vector3.forward, now + 4.01f) ||
                !SkillBattlePreview.HasGhostwalk(actor, now + 5.00f) ||
                SkillBattlePreview.HasGhostwalk(actor, now + 5.02f))
                throw new InvalidOperationException("Ghostwalk refund retained the previous duration on the same body.");
            foreach (string id in new[] { "virtue_ranger_03_a", "virtue_ranger_03_b" })
                if (!progress.TryLearn(id))
                    throw new InvalidOperationException("Ghostwalk relearning failed.");
            if (!ranger.TryStartRaidDash(Vector3.forward, now + 8.02f) ||
                !SkillBattlePreview.HasGhostwalk(actor, now + 9.61f) ||
                SkillBattlePreview.HasGhostwalk(actor, now + 9.63f))
                throw new InvalidOperationException("Ghostwalk relearning did not restore the duration on the same body.");
            ranger.Bind(null, progress, null);
            UnityEngine.Object.Destroy(ranger.gameObject);
            if (SkillBattlePreview.HasGhostwalk(actor, now))
                throw new InvalidOperationException("Ghostwalk persisted after unbind.");

            var valkyrie = new GameObject("Valkyrie dash validation").AddComponent<SkillBattlePreview>();
            foreach (string id in new[] { "virtue_valkyrie_01_a", "virtue_valkyrie_01_b", "virtue_valkyrie_01_c", "virtue_valkyrie_01_d" })
                if (!progress.TryLearn(id))
                    throw new InvalidOperationException("Valkyrie dash relearning failed.");
            valkyrie.LiveMatch = true;
            valkyrie.SelectedRaidClass = RaidClass.Valkyrie;
            valkyrie.Bind(actor, progress, null);
            if (!valkyrie.TryStartRaidDash(Vector3.forward, now) || valkyrie.TryStartRaidDash(Vector3.forward, now + 3.43f) ||
                !valkyrie.TryStartRaidDash(Vector3.forward, now + 3.45f) || SkillBattlePreview.HasGhostwalk(actor, now))
                throw new InvalidOperationException("Valkyrie dash cooldown or class gate failed.");
            progress.Reset();
            // The recharge started before refund retains its scheduled end at 6.89.
            if (valkyrie.TryStartRaidDash(Vector3.forward, now + 6.88f) ||
                !valkyrie.TryStartRaidDash(Vector3.forward, now + 6.90f) ||
                valkyrie.TryStartRaidDash(Vector3.forward, now + 10.35f) ||
                !valkyrie.TryStartRaidDash(Vector3.forward, now + 10.91f))
                throw new InvalidOperationException("Valkyrie refund did not preserve active recharge and reset the next cooldown.");
            foreach (string id in new[] { "virtue_valkyrie_01_a", "virtue_valkyrie_01_b", "virtue_valkyrie_01_c", "virtue_valkyrie_01_d" })
                if (!progress.TryLearn(id))
                    throw new InvalidOperationException("Valkyrie dash second relearning failed.");
            if (valkyrie.TryStartRaidDash(Vector3.forward, now + 14.90f) ||
                !valkyrie.TryStartRaidDash(Vector3.forward, now + 14.92f) ||
                valkyrie.TryStartRaidDash(Vector3.forward, now + 18.35f) ||
                !valkyrie.TryStartRaidDash(Vector3.forward, now + 18.37f))
                throw new InvalidOperationException("Valkyrie relearning did not preserve active recharge and restore the next cooldown.");
            valkyrie.Bind(null, progress, null);
            UnityEngine.Object.Destroy(valkyrie.gameObject);

            var rogue = new GameObject("Rogue dash charge validation").AddComponent<SkillBattlePreview>();
            rogue.LiveMatch = true;
            rogue.SelectedRaidClass = RaidClass.Rogue;
            rogue.Bind(actor, progress, null);
            if (!rogue.TryStartRaidDash(Vector3.forward, now) || rogue.TryStartRaidDash(Vector3.forward, now + 0.1f) ||
                !rogue.TryStartRaidDash(Vector3.right, now + 0.23f) || rogue.TryStartRaidDash(Vector3.right, now + 1f))
                throw new InvalidOperationException("Rogue dash charge capacity or active dash gate failed.");

            var replacement = Spawn("Rogue dash replacement", 0, origin);
            rogue.Bind(replacement, progress, null);
            if (rogue.TryStartRaidDash(Vector3.forward, now + 3.99f) ||
                !rogue.TryStartRaidDash(Vector3.forward, now + 4.01f) ||
                rogue.TryStartRaidDash(Vector3.forward, now + 7.99f) ||
                !rogue.TryStartRaidDash(Vector3.forward, now + 8.01f))
                throw new InvalidOperationException("Rogue dash charges did not recharge sequentially across respawn.");
            if (!rogue.TryStartRaidDash(Vector3.forward, now + 20f) ||
                !rogue.TryStartRaidDash(Vector3.forward, now + 20.23f) ||
                rogue.TryStartRaidDash(Vector3.forward, now + 21f))
                throw new InvalidOperationException("Rogue dash charges did not refill up to capacity.");
            rogue.Bind(null, progress, null);
            UnityEngine.Object.Destroy(rogue.gameObject);
            Debug.Log("JOTUNNSLAYER_RAID_DASH_REFUND_OK Ghostwalk 1.6/1/1.6, Valkyrie next cooldown 3.44/4/3.44, same body, scheduled recharge preserved");
            Debug.Log("JOTUNNSLAYER_RAID_DASH_OK displacement, Ghostwalk, Valkyrie cooldown and Rogue sequential charges");
        }

        private static void ValidateLichborne()
        {
            int[] expected = { 21, 22, 24, 27 };
            for (int i = 0; i < expected.Length; i++)
            {
                var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
                for (int learned = 0; learned <= i; learned++)
                    if (!progress.TryLearn($"virtue_ranger_02_{(char)('a' + learned)}"))
                        throw new InvalidOperationException("Could not learn the Lichborne node.");

                var runtime = new GameObject("Lichborne validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Ranger;
                Vector3 origin = Vector3.right * (5100 + i * 30);
                var actor = Spawn("Lichborne caster", 0, origin);
                var target = Spawn("Lichborne class target", 1, origin + Vector3.forward * 2f);
                var godTarget = Spawn("Lichborne god target", 1, origin + Vector3.right * 3f);
                runtime.Bind(actor, progress, null);
                runtime.SetRaidLevel(2);
                if (!runtime.TryChooseSkull() || !runtime.TryChooseLightning())
                    throw new InvalidOperationException("Could not select the class and god validation skills.");
                for (int phase = 0; phase < 3; phase++)
                {
                    if (phase == 1) progress.Reset();
                    else if (phase == 2)
                        for (int learned = 0; learned <= i; learned++)
                            if (!progress.TryLearn($"virtue_ranger_02_{(char)('a' + learned)}"))
                                throw new InvalidOperationException("Could not relearn Lichborne.");
                    runtime.Cast(3100 + phase * 10);
                    ExpectDamage(target, phase == 1 ? 20 : expected[i]);
                    ExpectDamage(godTarget, 17);
                }
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_LICHBORNE_OK four ranks, same-caster refund/relearn and god damage exclusion");
        }

        private static void ValidateMilitiaTraining()
        {
            int[] impactDamage = { 25, 26, 29, 32 };
            int[] followUpDamage = { 11, 11, 12, 14 };
            for (int i = 0; i < impactDamage.Length; i++)
            {
                var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
                for (int learned = 0; learned <= i; learned++)
                    if (!progress.TryLearn($"virtue_warden_02_{(char)('a' + learned)}"))
                        throw new InvalidOperationException("Could not learn the Militia Training node.");

                var runtime = new GameObject("Militia Training validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Warden;
                Vector3 origin = Vector3.right * (5600 + i * 30);
                var actor = Spawn("Militia Training caster", 0, origin);
                var target = Spawn("Militia Training class target", 1, origin + Vector3.forward * 2f);
                var godTarget = Spawn("Militia Training god target", 1, origin + Vector3.right * 3f);
                runtime.Bind(actor, progress, null);
                runtime.SetRaidLevel(2);
                if (!runtime.TryChooseAnvil() || !runtime.TryChooseLightning())
                    throw new InvalidOperationException("Could not select Militia Training validation skills.");

                int level = runtime.RaidLevel, choices = runtime.PendingRewards;
                for (int phase = 0; phase < 3; phase++)
                {
                    if (phase == 1) progress.Reset();
                    else if (phase == 2)
                        for (int learned = 0; learned <= i; learned++)
                            if (!progress.TryLearn($"virtue_warden_02_{(char)('a' + learned)}"))
                                throw new InvalidOperationException("Could not relearn Militia Training.");
                    float now = 3200 + phase * 10;
                    runtime.Cast(now);
                    ExpectDamage(target, phase == 1 ? 24 : impactDamage[i]);
                    ExpectDamage(godTarget, 17);
                    runtime.Tick(now + 1);
                    ExpectDamage(target, phase == 1 ? 10 : followUpDamage[i]);
                    if (runtime.RaidLevel != level || runtime.PendingRewards != choices)
                        throw new InvalidOperationException("Militia refund changed raid progression.");
                }
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_MILITIA_TRAINING_OK four ranks, delayed damage, same-caster refund/relearn, preserved raid progression and god damage exclusion");
        }

        private static void ValidateThorPassives()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 6320;
            var actor = Spawn("Thor passive caster", 0, origin);
            var near = Spawn("Discharge target", 1, origin + Vector3.right);
            var far = Spawn("Lightning target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Thor passive validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(3);
            if (runtime.TryChooseThorPotential() || runtime.TryChooseThunderDischarge() ||
                !progress.TryLearn("passive_thor_H") || !progress.TryLearn("passive_thor_I") ||
                !runtime.TryChooseThorPotential() || !runtime.TryChooseThunderDischarge() ||
                !runtime.TryChooseLightning())
                throw new InvalidOperationException("Thor passive unlock gates failed.");

            runtime.Cast(3600);
            ExpectDamage(far, 20);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.State stunRoll;
            do { stunRoll = UnityEngine.Random.state; }
            while (UnityEngine.Random.value >= 0.1f);
            UnityEngine.Random.state = stunRoll;
            if (!runtime.TryStartRaidDash(Vector3.forward, 3600) || runtime.TryStartRaidDash(Vector3.forward, 3601))
                throw new InvalidOperationException("Discharge did not share the successful dash gate.");
            ExpectDamage(near, 12);
            ExpectDamage(far, 0);
            runtime.Tick(3602.9f);
            if (!SkillBattlePreview.IsStoppedBySkill(near))
                throw new InvalidOperationException("Discharge did not apply the stun.");
            runtime.Tick(3603);
            if (SkillBattlePreview.IsStoppedBySkill(near))
                throw new InvalidOperationException("Discharge stun did not expire.");

            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseThorPotential() || !runtime.TryChooseThunderDischarge())
                throw new InvalidOperationException("Thor passive second ranks were unavailable.");
            runtime.Cast(3605);
            ExpectDamage(far, 24);
            if (!runtime.TryStartRaidDash(Vector3.forward, 3605))
                throw new InvalidOperationException("Second Discharge dash failed.");
            ExpectDamage(near, 20);

            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseThunderDischarge() || !runtime.TryStartRaidDash(Vector3.forward, 3610))
                throw new InvalidOperationException("Third Discharge dash failed.");
            ExpectDamage(near, 25);
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_THOR_PASSIVES_OK lightning scaling, dash gates, stun and three discharge ranks");
        }

        private static void ValidateProjectileReflection()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7160;
            var actor = Spawn("Reflection defender", 0, origin);
            var enemy = Spawn("Reflection source", 1, origin + Vector3.forward);
            var randomState = UnityEngine.Random.state;
            void ExpectReflection(float minimum, bool expected)
            {
                UnityEngine.Random.State rolls;
                float roll;
                do { rolls = UnityEngine.Random.state; roll = UnityEngine.Random.value; }
                while (roll < minimum || roll >= minimum + 0.01f);
                UnityEngine.Random.state = rolls;
                if (SkillBattlePreview.TryReflectRaidProjectile(actor, enemy) != expected)
                    throw new InvalidOperationException("Projectile reflection threshold failed.");
            }

            for (int i = 0; i < 2; i++)
            {
                if (!progress.TryLearn(i == 0 ? "virtue_skadi_03_a" : "virtue_skadi_03_b"))
                    throw new InvalidOperationException("Could not learn Deflection.");
                var runtime = new GameObject("Reflection validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Warden;
                runtime.Bind(actor, progress, null);
                actor.CurrentHealth = 800;
                ExpectReflection(i == 0 ? 0.10f : 0.23f, true);
                ExpectReflection(i == 0 ? 0.12f : 0.25f, false);
                if (actor.CurrentHealth != 800)
                    throw new InvalidOperationException("Deflection healed without Retribution.");
                if (i == 1)
                {
                    if (runtime.TryChooseRetribution() || !progress.TryLearn("passive_warden_O") ||
                        !runtime.TryChooseRetribution() || runtime.TryChooseRetribution())
                        throw new InvalidOperationException("Retribution unlock gate failed.");
                    ExpectReflection(0.38f, true);
                    runtime.SetRaidLevel(8);
                    if (!runtime.TryChooseRetribution() || runtime.TryChooseRetribution())
                        throw new InvalidOperationException("Retribution second rank failed.");
                    ExpectReflection(0.53f, true);
                    ExpectReflection(0.55f, false);
                    if (actor.CurrentHealth != 890 || SkillBattlePreview.TryReflectRaidProjectile(actor, actor))
                        throw new InvalidOperationException("Retribution heal or ally exclusion failed.");
                }
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_PROJECTILE_REFLECTION_OK Deflection ranks, Retribution heal and friendly exclusion");
        }

        private static void ValidateLastStand()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7120;
            var actor = Spawn("Last Stand caster", 0, origin);
            var enemy = Spawn("Last Stand target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Last Stand validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseLastStand() || !progress.TryLearn("passive_freya_I"))
                throw new InvalidOperationException("Last Stand unlock gate failed.");

            var randomState = UnityEngine.Random.state;
            int[] levels = { 1, 8, 15 };
            float[] delays = { 0.4f, 0.3f, 0.2f };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (!runtime.TryChooseLastStand() || runtime.TryChooseLastStand())
                    throw new InvalidOperationException("Last Stand rank gate failed.");

                float now = 4300f + rank * 2f;
                actor.transform.position += Vector3.right;
                runtime.Tick(now);
                runtime.Tick(now + delays[rank] - 0.02f);
                enemy.ServerApplyRaidSkillDamage(100, actor);
                ExpectDamage(enemy, 100);

                float threshold = (rank + 1) * 0.05f;
                UnityEngine.Random.State rolls;
                float roll;
                do { rolls = UnityEngine.Random.state; roll = UnityEngine.Random.value; }
                while (roll >= threshold || roll < threshold - 0.02f);
                runtime.Tick(now + delays[rank] + 0.02f);
                UnityEngine.Random.state = rolls;
                enemy.ServerApplyRaidSkillDamage(100, actor);
                ExpectDamage(enemy, 200);
                UnityEngine.Random.state = rolls;
                enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
                ExpectDamage(enemy, 100);
            }
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_LAST_STAND_OK three stationary thresholds, skill criticals and weapon isolation");
        }

        private static void ValidateFreyaSupport()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            Vector3 origin = Vector3.right * 6280;
            var actor = Spawn("Freya support recipient", 0, origin);
            float[] radii = { 4.28f, 4.56f, 5.12f, 6f };
            for (int i = 0; i < radii.Length; i++)
            {
                if (!progress.TryLearn($"virtue_freya_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn Wisdom.");

                var runtime = new GameObject("Freya support validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.Bind(actor, progress, null);
                int before = progress.Gold;
                runtime.RollMinionGold(origin + Vector3.forward * (radii[i] - 0.01f), 0f);
                runtime.RollMinionGold(origin + Vector3.forward * (radii[i] + 0.01f), 0f);
                runtime.Tick(3500);
                if (progress.Gold != before + 1)
                    throw new InvalidOperationException("Wisdom gold pickup radius failed.");

                if (i == radii.Length - 1)
                {
                    actor.CurrentHealth = 700;
                    if (runtime.TryChooseHeroHymn() || !progress.TryLearn("passive_freya_H") ||
                        !runtime.TryChooseHeroHymn() || runtime.TryChooseHeroHymn() ||
                        actor.ServerHeal(50, actor.netId) != 60)
                        throw new InvalidOperationException("Hero Hymn unlock or first rank failed.");
                    runtime.SetRaidLevel(8);
                    if (!runtime.TryChooseHeroHymn() || runtime.TryChooseHeroHymn() ||
                        actor.ServerHeal(50, actor.netId) != 70)
                        throw new InvalidOperationException("Hero Hymn second rank failed.");
                }
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            if (actor.ServerHeal(50, actor.netId) != 50)
                throw new InvalidOperationException("Hero Hymn persisted after unbinding.");
            Debug.Log("JOTUNNSLAYER_FREYA_SUPPORT_OK pickup boundaries, two healing ranks and unbinding");
        }

        private static void ValidateGourmet()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Gourmet recipient", 0, Vector3.right * 5760);
            int[] expected = { 54, 58, 66, 78 };
            for (int i = 0; i < expected.Length; i++)
            {
                if (!progress.TryLearn($"virtue_warden_01_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn the Gourmet node.");

                var runtime = new GameObject("Gourmet validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Warden;
                runtime.Bind(actor, progress, null);
                actor.CurrentHealth = 800;
                if (actor.ServerHeal(50, actor.netId) != 50 ||
                    actor.ServerHeal(SkillBattlePreview.ScaleFoodHealing(actor, 50), actor.netId) != expected[i])
                    throw new InvalidOperationException("Gourmet tier or ordinary healing isolation failed.");
                if (i == expected.Length - 1)
                {
                    progress.Reset();
                    if (SkillBattlePreview.ScaleFoodHealing(actor, 50) != 50)
                        throw new InvalidOperationException("Gourmet persisted after refund on the same bound character.");
                    for (int rank = 0; rank < expected.Length; rank++)
                        if (!progress.TryLearn($"virtue_warden_01_{(char)('a' + rank)}"))
                            throw new InvalidOperationException("Gourmet relearn after refund failed.");
                    if (SkillBattlePreview.ScaleFoodHealing(actor, 50) != 78)
                        throw new InvalidOperationException("Gourmet relearn did not affect the same bound character.");
                }
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }

            if (SkillBattlePreview.ScaleFoodHealing(actor, 50) != 50)
                throw new InvalidOperationException("Gourmet persisted after unbinding.");
            var ranger = new GameObject("Gourmet class gate validation").AddComponent<SkillBattlePreview>();
            ranger.LiveMatch = true;
            ranger.SelectedRaidClass = RaidClass.Ranger;
            ranger.Bind(actor, progress, null);
            if (SkillBattlePreview.ScaleFoodHealing(actor, 50) != 50)
                throw new InvalidOperationException("Gourmet affected another class.");
            ranger.Bind(null, progress, null);
            UnityEngine.Object.Destroy(ranger.gameObject);
            Debug.Log("JOTUNNSLAYER_GOURMET_OK four ranks, ordinary healing, class isolation, refund, relearn and unbinding");
        }

        private static void ValidateRage()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_berserker_04_a") || !progress.TryLearn("passive_berserker_P"))
                throw new InvalidOperationException("Could not learn the Rage nodes.");

            var runtime = new GameObject("Rage validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            Vector3 origin = Vector3.right * 5040;
            var actor = Spawn("Rage caster", 0, origin);
            var target = Spawn("Rage target", 1, origin + Vector3.forward);
            actor.CurrentHealth = 900;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseDischarge() || !runtime.TryChooseDischarge() || !runtime.TryChooseDischarge() ||
                runtime.TryChooseDischarge())
                throw new InvalidOperationException("Discharge rank gate failed.");

            var state = UnityEngine.Random.state;
            int seed = FindDamageRollSeed(0f, 0.10f);
            float now = Time.time;
            ExpectDirectHitDamage(target, actor, seed, 100);
            if (actor.CurrentHealth != 915 || !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.10f))
                throw new InvalidOperationException("Rage did not apply Discharge healing and movement.");
            ExpectDirectHitDamage(target, actor, seed, 120);
            if (actor.CurrentHealth != 915)
                throw new InvalidOperationException("Rage re-triggered during its active window.");

            runtime.Tick(now + 7.99f);
            if (actor.CurrentHealth != 936)
                throw new InvalidOperationException("Extended Rage regeneration was incorrect.");
            runtime.Tick(now + 8.01f);
            if (actor.CurrentHealth != 939 || SkillBattlePreview.RaidMoveMultiplier(actor) != 1f ||
                SkillBattlePreview.ScaleRaidAllDamage(actor, 100) != 100)
                throw new InvalidOperationException("Extended Rage failed to heal its last tick or expire.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Random.state = state;
            Debug.Log("JOTUNNSLAYER_RAGE_OK weapon trigger, Discharge, modifiers and doubled duration");
        }

        private static void ValidateOpenWounds()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Open Wounds validation").AddComponent<SkillBattlePreview>();
            Vector3 origin = Vector3.right * 4980;
            var actor = Spawn("Open Wounds caster", 0, origin);
            var target = Spawn("Open Wounds target", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseOpenWounds() || !progress.TryLearn("passive_berserker_O") || !runtime.TryChooseOpenWounds())
                throw new InvalidOperationException("Open Wounds unlock gate failed.");

            var state = UnityEngine.Random.state;
            int low = FindDamageRollSeed(0f, 0.15f);
            int middle = FindDamageRollSeed(0.15f, 0.25f);
            float now = Time.time;
            ExpectDirectHitDamage(target, actor, middle, 100);
            runtime.Tick(now + 5.01f);
            ExpectDamage(target, 0);
            ExpectDirectHitDamage(target, actor, low, 100);
            runtime.Tick(now + 0.99f);
            ExpectDamage(target, 0);
            runtime.Tick(now + 5.01f);
            ExpectDamage(target, 10);
            runtime.Tick(now + 6.01f);
            ExpectDamage(target, 0);
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseOpenWounds())
                throw new InvalidOperationException("Open Wounds rank two was offered below level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseOpenWounds() || runtime.TryChooseOpenWounds())
                throw new InvalidOperationException("Open Wounds rank two or rank cap failed.");
            ExpectDirectHitDamage(target, actor, middle, 100);
            runtime.Tick(now + 5.01f);
            ExpectDamage(target, 10);
            UnityEngine.Random.state = state;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_OPEN_WOUNDS_OK rank gates, bleed thresholds, ticks and expiry");
        }

        private static void ValidateRaidCritical()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 40);
            foreach (string id in new[] { "virtue_njord_01_a", "virtue_njord_01_b", "virtue_njord_01_c", "virtue_njord_01_e",
                "virtue_rogue_01_a", "virtue_rogue_01_b", "virtue_rogue_01_c", "virtue_rogue_01_d",
                "virtue_nidhogg_01_a", "virtue_nidhogg_01_b", "virtue_nidhogg_01_c", "virtue_nidhogg_01_d" })
            {
                if (!progress.TryLearn(id))
                    throw new InvalidOperationException("Could not learn the raid critical virtues.");
            }
            var runtime = new GameObject("Raid critical validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Rogue;
            Vector3 origin = Vector3.right * 4920;
            var actor = Spawn("Raid critical attacker", 0, origin);
            var target = Spawn("Raid critical target", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);

            var state = UnityEngine.Random.state;
            int low = FindDamageRollSeed(0.20f, 0.23f);
            int high = FindDamageRollSeed(0.23f, 0.25f);
            ExpectDirectHitDamage(target, actor, low, 235);
            ExpectDirectHitDamage(target, actor, high, 100);
            if (runtime.TryChooseInvisibleBlade() || !progress.TryLearn("passive_rogue_P") || !runtime.TryChooseInvisibleBlade())
                throw new InvalidOperationException("Invisible Blade unlock gate failed.");
            ExpectDirectHitDamage(target, actor, low, 255);
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseInvisibleBlade() || runtime.TryChooseInvisibleBlade())
                throw new InvalidOperationException("Invisible Blade rank two or level fifteen gate failed.");
            ExpectDirectHitDamage(target, actor, low, 265);
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseInvisibleBlade() || runtime.TryChooseInvisibleBlade())
                throw new InvalidOperationException("Invisible Blade maximum rank failed.");
            ExpectDirectHitDamage(target, actor, low, 275);
            UnityEngine.Random.InitState(low);
            target.ServerApplyRangeDot(100, AttackType.hitscan, actor);
            ExpectDamage(target, 100);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Random.state = state;
            Debug.Log("JOTUNNSLAYER_RAID_CRITICAL_OK Njord branch, Rogue stacking, critical damage and periodic exclusion");
        }

        private static void ValidateBlazingSoul()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7240;
            var actor = Spawn("Blazing Soul caster", 0, origin);
            var target = Spawn("Blazing Soul target", 1, origin + Vector3.forward * 3f);
            var ally = Spawn("Blazing Soul ally", 0, origin + Vector3.forward);
            float[] chances = { 0.07f, 0.14f, 0.28f, 0.50f };
            var randomState = UnityEngine.Random.state;
            for (int rank = 0; rank < chances.Length; rank++)
            {
                if (!progress.TryLearn($"virtue_rogue_02_{(char)('a' + rank)}"))
                    throw new InvalidOperationException("Could not learn Scorching Heat.");
                var runtime = new GameObject("Blazing Soul validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Rogue;
                runtime.Bind(actor, progress, null);
                actor.CurrentHealth = 800;
                UnityEngine.Random.State rolls;
                float burnRoll;
                do
                {
                    rolls = UnityEngine.Random.state;
                    UnityEngine.Random.Range(0, 1);
                    UnityEngine.Random.Range(4f, 16f);
                    burnRoll = UnityEngine.Random.value;
                }
                while (burnRoll >= chances[rank] || burnRoll < chances[rank] - 0.02f);

                UnityEngine.Random.state = rolls;
                runtime.Tick(4400f);
                ExpectDamage(target, 26);
                ExpectDamage(ally, 0);
                if (actor.CurrentHealth != 820)
                    throw new InvalidOperationException("Blazing Soul did not heal on effective damage.");
                runtime.Tick(4400.49f);
                ExpectDamage(target, 0);
                runtime.Tick(4403f);
                ExpectDamage(target, 24);
                runtime.Tick(4403.5f);
                ExpectDamage(target, 0);
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_BLAZING_SOUL_OK damage, healing, burn thresholds, duration and friendly exclusion");
        }

        private static void ValidateChainReaction()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 10);
            if (!progress.TryLearn("virtue_rogue_04_a"))
                throw new InvalidOperationException("Could not learn Chain Reaction.");
            Vector3 origin = Vector3.right * 8280;
            var actor = Spawn("Chain Reaction caster", 0, origin);
            var target = Spawn("Chain Reaction target", 1, origin + Vector3.forward * 3f);
            var ally = Spawn("Chain Reaction ally", 0, origin + Vector3.forward);
            var runtime = new GameObject("Chain Reaction validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Rogue;
            var originalRandom = UnityEngine.Random.state;
            try
            {
                for (int repeat = 0; repeat < 2; repeat++)
                {
                    runtime.Bind(actor, progress, null);
                    actor.CurrentHealth = 800;
                    UnityEngine.Random.State rolls;
                    float chance;
                    do
                    {
                        rolls = UnityEngine.Random.state;
                        UnityEngine.Random.Range(0, 1);
                        UnityEngine.Random.Range(4f, 16f);
                        chance = UnityEngine.Random.value;
                    }
                    while (chance < 0.29f || chance >= 0.31f || (chance < 0.3f) != (repeat == 1));
                    UnityEngine.Random.state = rolls;
                    float castAt = 5000f + repeat * 20f;
                    runtime.Tick(castAt);
                    ExpectDamage(target, repeat == 1 ? 52 : 26);
                    ExpectDamage(ally, 0);
                    if (actor.CurrentHealth != (repeat == 1 ? 840 : 820))
                        throw new InvalidOperationException("Chain Reaction healing did not match effective explosions.");
                    runtime.Tick(castAt + 0.01f);
                    ExpectDamage(target, 0);
                    runtime.Bind(null, progress, null);
                }

                runtime.Bind(null, progress, null);
                runtime.SelectedRaidClass = RaidClass.Seeress;
                runtime.Bind(actor, progress, null);
                runtime.Tick(5100f);
                ExpectDamage(target, 0);
                runtime.Bind(null, progress, null);
                runtime.Tick(5200f);
                ExpectDamage(target, 0);
            }
            finally
            {
                UnityEngine.Random.state = originalRandom;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_CHAIN_REACTION_OK chance boundary, double damage and healing, cooldown, class gate and unbind");
        }

        private static void ValidateHeartEvolutionBleed()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 10);
            if (!progress.TryLearn(SkillBattlePreview.HeartUnlock) ||
                !progress.TryLearn(SkillBattlePreview.HeartEvolution))
                throw new InvalidOperationException("Could not unlock Heart evolution bleed validation.");
            Vector3 origin = Vector3.right * 8340f;
            var actor = Spawn("Heart bleed caster", 0, origin);
            var target = Spawn("Heart bleed target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Heart evolution bleed validation").AddComponent<SkillBattlePreview>();
            runtime.Bind(actor, progress, null);
            var originalRandom = UnityEngine.Random.state;
            try
            {
                for (int trial = 0; trial < 4; trial++)
                {
                    runtime.Bind(null, progress, null);
                    runtime.Bind(actor, progress, null);
                    foreach (int level in new[] { 1, 6, 12, 18 })
                    {
                        runtime.SetRaidLevel(level);
                        if (!runtime.TryChooseHeartseeker())
                            throw new InvalidOperationException("Could not choose Heart evolution bleed ranks.");
                    }
                    bool bleed = trial != 0;
                    UnityEngine.Random.State rolls;
                    float chance;
                    do
                    {
                        rolls = UnityEngine.Random.state;
                        chance = UnityEngine.Random.value;
                    }
                    while (chance < 0.49f || chance >= 0.51f || (chance < 0.5f) != bleed);
                    UnityEngine.Random.state = rolls;
                    float castAt = 5300f + trial * 10f;
                    runtime.Cast(castAt);
                    runtime.Tick(castAt + 0.3f);
                    ExpectDamage(target, 20);
                    if (trial == 2)
                        runtime.Bind(null, progress, null);
                    if (trial == 3)
                        progress.Reset();
                    runtime.Tick(castAt + 1.2f);
                    ExpectDamage(target, 0);
                    runtime.Tick(castAt + 1.21f);
                    ExpectDamage(target, trial == 1 ? 2 : 0);
                    runtime.Tick(castAt + 4.21f);
                    ExpectDamage(target, trial == 1 ? 6 : 0);
                    runtime.Tick(castAt + 6f);
                    ExpectDamage(target, 0);
                }
            }
            finally
            {
                UnityEngine.Random.state = originalRandom;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_HEART_EVOLUTION_BLEED_OK 50 percent boundary, first tick timing, four 2 HP ticks, expiry, unbind and refund");
        }

        private static void ValidateCounterattack()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_rogue_03_a"))
                throw new InvalidOperationException("Could not learn Counterattack.");
            Vector3 origin = Vector3.right * 7200;
            var actor = Spawn("Counterattack defender", 0, origin);
            var enemy = Spawn("Counterattack attacker", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Counterattack validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Rogue;
            runtime.Bind(actor, progress, null);
            if (!runtime.TryChooseDaggerDance())
                throw new InvalidOperationException("Could not choose Dagger Dance.");

            var randomState = UnityEngine.Random.state;
            void SetEvasionRoll(bool reset)
            {
                UnityEngine.Random.State rolls;
                float evade, trigger;
                do
                {
                    rolls = UnityEngine.Random.state;
                    evade = UnityEngine.Random.value;
                    trigger = UnityEngine.Random.value;
                }
                while (evade >= 0.2f || (trigger < 0.2f) != reset);
                UnityEngine.Random.state = rolls;
            }

            float now = Time.time;
            runtime.Cast(now);
            ExpectDamage(enemy, 13);
            SetEvasionRoll(reset: false);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 0);
            runtime.Cast(now + 0.1f);
            ExpectDamage(enemy, 0);

            SetEvasionRoll(reset: true);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 0);
            runtime.Cast(now + 0.2f);
            ExpectDamage(enemy, 13);
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_COUNTERATTACK_OK innate evasion and cooldown reset success/failure");
        }

        private static void ValidateSwindle()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_loki_03_a") || !progress.TryLearn("virtue_loki_04_a"))
                throw new InvalidOperationException("Could not learn the Swindle and Evasion nodes.");

            var runtime = new GameObject("Swindle validation").AddComponent<SkillBattlePreview>();
            Vector3 origin = Vector3.right * 4860;
            var actor = Spawn("Swindle caster", 0, origin);
            var attacker = Spawn("Swindle attacker", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            var randomState = UnityEngine.Random.state;
            int miss = FindDamageRollSeed(0.10f, 0.20f);
            int evade = FindDamageRollSeed(0f, 0.10f);
            ExpectDirectHitDamage(actor, attacker, miss, 100);
            if (SkillBattlePreview.IsStoppedBySkill(attacker))
                throw new InvalidOperationException("Swindle triggered without an evasion.");
            float now = Time.time;
            ExpectDirectHitDamage(actor, attacker, evade, 0);
            UnityEngine.Random.state = randomState;
            runtime.Tick(now + 4.99f);
            if (!SkillBattlePreview.IsStoppedBySkill(attacker))
                throw new InvalidOperationException("Swindle did not hold the attacker for five seconds.");
            runtime.Tick(now + 5.01f);
            if (SkillBattlePreview.IsStoppedBySkill(attacker))
                throw new InvalidOperationException("Swindle did not expire.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_SWINDLE_OK evasion trigger, failed roll exclusion and five-second expiry");
        }

        private static void ValidateRaidCurse()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            foreach (char rank in "abc")
                if (!progress.TryLearn($"virtue_valkyrie_03_{rank}"))
                    throw new InvalidOperationException("Could not learn the curse stun virtue.");

            Vector3 origin = Vector3.right * 7320;
            var actor = Spawn("Raid curse caster", 0, origin);
            var target = Spawn("Raid curse target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Raid curse validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Valkyrie;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(6);
            if (!runtime.TryChooseShip() || !runtime.TryChooseShip())
                throw new InvalidOperationException("Could not select Ship Charge rank two.");

            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.State rolls;
            float curse, stun;
            do
            {
                rolls = UnityEngine.Random.state;
                curse = UnityEngine.Random.value;
                stun = UnityEngine.Random.value;
            }
            while (curse >= 0.25f || stun < 0.10f || stun >= 0.17f);
            UnityEngine.Random.state = rolls;
            float hitTime = Mathf.Max(Time.time, 0.5f);
            runtime.Cast(hitTime - 0.5f);
            runtime.Tick(hitTime);
            UnityEngine.Random.state = randomState;
            ExpectDamage(target, 30);
            if (!SkillBattlePreview.HasRaidCurse(target, hitTime) || !SkillBattlePreview.IsStoppedBySkill(target))
                throw new InvalidOperationException("Ship collision did not apply curse and its stun.");
            var ally = Spawn("Raid curse allied attacker", 0, origin);
            target.ServerApplyRaidSkillDamage(100, ally);
            ExpectDamage(target, 130);
            target.ServerApplyRangeDot(100, AttackType.explosion, ally);
            ExpectDamage(target, 130);
            target.transform.position += Vector3.right * 20f;
            runtime.Tick(hitTime + 2f);
            if (!SkillBattlePreview.HasRaidCurse(target, hitTime + 2f) || SkillBattlePreview.IsStoppedBySkill(target))
                throw new InvalidOperationException("Curse and stun durations were not independent.");
            runtime.Tick(hitTime + 4f);
            if (SkillBattlePreview.HasRaidCurse(target, hitTime + 4f))
                throw new InvalidOperationException("Curse did not expire.");
            target.ServerApplyRaidSkillDamage(100, ally);
            ExpectDamage(target, 100);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_RAID_CURSE_OK ship hit, allied direct and periodic damage, stun and independent expiry");
        }

        private static void ValidateMalediction()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7350;
            var actor = Spawn("Malediction caster", 0, origin);
            var target = Spawn("Malediction target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Malediction validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);

            var originalRandom = UnityEngine.Random.state;
            try
            {
                int low = FindDamageRollSeed(0.08f, 0.10f);
                int high = FindDamageRollSeed(0.10f, 0.12f);
                ExpectDirectHitDamage(target, actor, low, 100);
                if (!runtime.TryChooseMalediction() || runtime.TryChooseMalediction())
                    throw new InvalidOperationException("Malediction reward was unavailable at level one or repeatable.");

                ExpectDirectHitDamage(target, actor, high, 100);
                if (SkillBattlePreview.HasRaidCurse(target, Time.time))
                    throw new InvalidOperationException("A non-critical hit applied Malediction.");

                ExpectDirectHitDamage(target, actor, low, 200);
                float hitTime = Time.time;
                if (!SkillBattlePreview.HasRaidCurse(target, hitTime + 3.9f) ||
                    SkillBattlePreview.HasRaidCurse(target, hitTime + 4f))
                    throw new InvalidOperationException("Malediction did not apply a four-second curse.");
                ExpectDirectHitDamage(target, actor, high, 130);
                runtime.Bind(null, progress, null);
                if (SkillBattlePreview.HasRaidCurse(target, hitTime))
                    throw new InvalidOperationException("Malediction curse survived unbinding.");
            }
            finally
            {
                UnityEngine.Random.state = originalRandom;
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_MALEDICTION_OK one-rank reward, critical chance, curse and unbinding");
        }

        private static void ValidateEssenceExpansion()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 8220;
            var actor = Spawn("Essence Expansion caster", 0, origin);
            var target = Spawn("Essence Expansion target", 1, origin + Vector3.forward * 6f);
            var runtime = new GameObject("Essence Expansion validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChooseEssenceExpansion() || runtime.TryChooseEssenceExpansion())
                throw new InvalidOperationException("Essence Expansion was unavailable at level one or repeatable.");
            runtime.SetRaidLevel(3);
            if (!runtime.TryChooseMind() || !runtime.TryChooseTrap())
                throw new InvalidOperationException("Could not select Mind Blast and Time Trap.");

            const float castTime = 4550f;
            runtime.Cast(castTime);
            ExpectDamage(target, 18);
            target.transform.position = origin + Vector3.forward * 2f;
            runtime.Tick(castTime + 5.24f);
            if (!SkillBattlePreview.IsStoppedByTimeTrap(target, castTime + 5.24f))
                throw new InvalidOperationException("Essence Expansion did not extend the first-rank trap beyond 3.5 seconds.");
            runtime.Tick(castTime + 5.25f);
            if (SkillBattlePreview.IsStoppedByTimeTrap(target, castTime + 5.25f))
                throw new InvalidOperationException("The extended trap did not expire after 5.25 seconds.");

            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_ESSENCE_EXPANSION_OK reward, class skill damage and trap duration");
        }

        private static void ValidatePrimevalCurrents()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7500;
            var actor = Spawn("Primeval Currents caster", 0, origin);
            var enemy = Spawn("Primeval Currents target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Primeval Currents validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChoosePrimevalCurrents())
                throw new InvalidOperationException("Primeval Currents rank one was unavailable at level one.");
            runtime.SetRaidLevel(7);
            if (runtime.TryChoosePrimevalCurrents() || !runtime.TryChooseMind() || !runtime.TryChooseLightning())
                throw new InvalidOperationException("Primeval Currents rank gate or test skill selection failed.");
            runtime.Cast(4900f);
            ExpectDamage(enemy, 39);
            enemy.transform.position = origin + Vector3.forward * 3f;

            runtime.SetRaidLevel(8);
            if (!runtime.TryChoosePrimevalCurrents() || runtime.TryChoosePrimevalCurrents())
                throw new InvalidOperationException("Primeval Currents rank two selection or cap failed.");
            runtime.Cast(4906f);
            ExpectDamage(enemy, 46);
            if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100)
                throw new InvalidOperationException("Primeval Currents modified basic weapon damage.");

            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_PRIMEVAL_CURRENTS_OK level gates, class and god skills, weapon isolation");
        }

        private static void ValidateWinterRaven()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7540;
            var actor = Spawn("Winter Raven caster", 0, origin);
            var target = Spawn("Winter Raven target", 1, origin + Vector3.forward * 11f);
            var nearby = Spawn("Winter Raven blast target", 1, origin + Vector3.forward * 24f);
            var outside = Spawn("Winter Raven outside blast", 1, origin + Vector3.forward * 25f);
            var runtime = new GameObject("Winter Raven validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChooseWinterRaven() || runtime.TryChooseWinterRaven())
                throw new InvalidOperationException("Winter Raven selection or rank cap failed.");

            const float now = 4950f;
            runtime.Tick(now);
            runtime.Tick(now + 7f);
            nearby.transform.position = target.transform.position + Vector3.right * 1.4f;
            outside.transform.position = target.transform.position + Vector3.right * 1.6f;
            runtime.Tick(now + 7.99f);
            ExpectDamage(target, 0);
            runtime.Tick(now + 8f);
            ExpectDamage(target, 16);
            ExpectDamage(nearby, 16);
            ExpectDamage(outside, 0);
            runtime.Tick(now + 10.49f);
            if (!SkillBattlePreview.IsStoppedBySkill(target) || !SkillBattlePreview.IsStoppedBySkill(nearby) ||
                SkillBattlePreview.IsStoppedBySkill(outside))
                throw new InvalidOperationException("Winter Raven freeze did not respect the blast radius.");
            runtime.Tick(now + 10.5f);
            if (SkillBattlePreview.IsStoppedBySkill(target))
                throw new InvalidOperationException("Winter Raven freeze did not expire.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_WINTER_RAVEN_OK timer, flight, blast and freeze expiry");
        }

        private static void ValidateAncientKnowledge()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7580;
            var actor = Spawn("Ancient Knowledge collector", 0, origin);
            var runtime = new GameObject("Ancient Knowledge validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChooseAncientKnowledge())
                throw new InvalidOperationException("Ancient Knowledge was not offered at level one.");

            int gold = progress.Gold;
            runtime.RollMinionGold(origin + Vector3.forward * 5.19f, 0f);
            runtime.RollMinionGold(origin + Vector3.forward * 6.39f, 0f);
            runtime.RollMinionGold(origin + Vector3.forward * 6.41f, 0f);
            runtime.Tick(4980f);
            runtime.SetRaidLevel(7);
            if (progress.Gold != gold + 1 || runtime.TryChooseAncientKnowledge())
                throw new InvalidOperationException("Ancient Knowledge rank one radius or rank two gate failed.");

            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseAncientKnowledge() || runtime.TryChooseAncientKnowledge())
                throw new InvalidOperationException("Ancient Knowledge rank two selection or cap failed.");
            runtime.Tick(4981f);
            if (progress.Gold != gold + 2)
                throw new InvalidOperationException("Ancient Knowledge rank two did not collect exactly within 6.4 m.");

            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_ANCIENT_KNOWLEDGE_OK level gates and two gold pickup radii");
        }

        private static void ValidateExperienceCrystals()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7600;
            var actor = Spawn("Experience crystal collector", 0, origin);
            var victim = Spawn("Experience crystal source", 1, origin + Vector3.forward * 5.19f);
            var runtime = new GameObject("Experience crystal validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.UseExperienceCrystals = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            actor.Server_KilledCharacter?.Invoke(victim);
            runtime.Tick(4990f);
            if (runtime.RaidLevel != 1 || !runtime.TryChooseAncientKnowledge())
                throw new InvalidOperationException("Experience was granted before the crystal was in pickup range.");
            runtime.Tick(4991f);
            if (runtime.RaidLevel != 2)
                throw new InvalidOperationException("The expanded pickup radius did not collect the experience crystal.");
            for (int i = 0; i < 4; i++)
                runtime.Tick(4992f + i);
            if (runtime.RaidLevel != 2)
                throw new InvalidOperationException("A collected experience crystal was credited more than once.");

            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_EXPERIENCE_CRYSTALS_OK kill drop, pickup range, level up and single credit");
        }

        private static void ValidateSeerFortune()
        {
            var randomState = UnityEngine.Random.state;
            try
            {
                int successSeed = FindDamageRollSeed(0.28f, 0.30f);
                int failureSeed = FindDamageRollSeed(0.30f, 0.32f);
                foreach (bool expectedBonus in new[] { false, true })
                {
                    var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
                    Vector3 origin = Vector3.right * (expectedBonus ? 7820f : 7780f);
                    var actor = Spawn("Fortune caster", 0, origin);
                    var enemy = Spawn("Fortune target", 1, origin + Vector3.forward);
                    var runtime = new GameObject("Fortune validation").AddComponent<SkillBattlePreview>();
                    runtime.LiveMatch = true;
                    runtime.UseExperienceCrystals = true;
                    runtime.SelectedRaidClass = RaidClass.Seeress;
                    runtime.Bind(actor, progress, null);
                    runtime.SetRaidLevel(1);
                    actor.Server_KilledCharacter?.Invoke(enemy);
                    UnityEngine.Random.InitState(expectedBonus ? successSeed : failureSeed);
                    runtime.Tick(4995f);
                    if (runtime.RaidLevel != 2 || !runtime.TryChooseMind() || !runtime.TryChooseTrap() ||
                        runtime.TryChoosePrimevalCurrents() != expectedBonus || runtime.TryChooseAncientKnowledge())
                        throw new InvalidOperationException("Fortune did not grant exactly one extra choice on a successful roll.");
                    runtime.Bind(null, progress, null);
                    UnityEngine.Object.Destroy(runtime.gameObject);
                }
            }
            finally
            {
                UnityEngine.Random.state = randomState;
            }
            Debug.Log("JOTUNNSLAYER_SEER_FORTUNE_OK innate chance and extra reward consumption");
        }

        private static void ValidateMaliciousWisdom()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7860;
            var actor = Spawn("Malicious Wisdom caster", 0, origin);
            var source = Spawn("Malicious Wisdom drop source", 1, origin + Vector3.forward * 4f);
            source.CurrentHealth = 0;
            var enemy = Spawn("Malicious Wisdom contact", 1, origin + Vector3.forward * 2f);
            var outside = Spawn("Malicious Wisdom outside", 1, enemy.transform.position + Vector3.right);
            var runtime = new GameObject("Malicious Wisdom validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.UseExperienceCrystals = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(2);
            var randomState = UnityEngine.Random.state;
            try
            {
                int success = FindDamageRollSeed(0.23f, 0.25f);
                int failure = FindDamageRollSeed(0.25f, 0.27f);
                actor.Server_KilledCharacter?.Invoke(source);
                UnityEngine.Random.InitState(success);
                runtime.Tick(5000f);
                ExpectDamage(enemy, 5);
                ExpectDamage(outside, 0);
                actor.Server_KilledCharacter?.Invoke(source);
                UnityEngine.Random.InitState(failure);
                runtime.Tick(5001f);
                ExpectDamage(enemy, 0);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_MALICIOUS_WISDOM_OK crystal path, chance and damage");
        }

        private static void ValidateExtraction()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7640;
            var actor = Spawn("Extraction caster", 0, origin);
            var inside = Spawn("Extraction inside", 1, origin + Vector3.forward * 30f);
            var outside = Spawn("Extraction outside", 1, origin + Vector3.forward * 30.1f);
            var runtime = new GameObject("Extraction validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.UseExperienceCrystals = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            actor.CurrentHealth -= 100;
            var randomState = UnityEngine.Random.state;
            try
            {
                if (!runtime.TryChooseExtraction() || runtime.TryChooseExtraction())
                    throw new InvalidOperationException("Extraction selection or rank cap failed.");
                for (int i = 0; i < 3; i++)
                    actor.Server_KilledCharacter?.Invoke(inside);
                actor.Server_KilledCharacter?.Invoke(outside);

                UnityEngine.Random.InitState(FindDamageRollSeed(0.25f, 0.27f));
                SkillBattlePreview.TriggerEldritchWard(actor, outside, 5010f);
                if (runtime.RaidLevel != 1)
                    throw new InvalidOperationException("A failed Extraction roll awarded experience.");
                UnityEngine.Random.InitState(FindDamageRollSeed(0.23f, 0.25f));
                SkillBattlePreview.TriggerEldritchWard(actor, outside, 5012f);
                if (runtime.RaidLevel != 2)
                    throw new InvalidOperationException("Extraction did not collect exactly the crystals within 30 m.");
                actor.transform.position = outside.transform.position;
                runtime.Tick(5013f);
                if (runtime.RaidLevel != 3)
                    throw new InvalidOperationException("The out-of-range crystal was not available for normal pickup.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_EXTRACTION_OK ward trigger, chance boundary, radius and normal pickup");
        }

        private static void ValidateSoulReaper()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7680;
            var actor = Spawn("Soul Reaper caster", 0, origin);
            var enemy = Spawn("Soul Reaper target", 1, origin + Vector3.forward * 3f);
            var crystalSource = Spawn("Soul Reaper crystal source", 1, origin - Vector3.forward * 3f);
            crystalSource.CurrentHealth = 0;
            var runtime = new GameObject("Soul Reaper validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.UseExperienceCrystals = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChooseSoulReaper() || runtime.TryChooseSoulReaper())
                throw new InvalidOperationException("Soul Reaper selection or rank cap failed.");
            runtime.SetRaidLevel(2);
            if (!runtime.TryChooseMind())
                throw new InvalidOperationException("Could not select the cooldown reset test skill.");

            var randomState = UnityEngine.Random.state;
            try
            {
                int miss = FindDamageRollSeed(0.05f, 0.07f);
                int reset = FindDamageRollSeed(0.03f, 0.05f);
                runtime.Cast(5040f);
                ExpectDamage(enemy, 16);
                actor.Server_KilledCharacter?.Invoke(crystalSource);
                UnityEngine.Random.InitState(miss);
                runtime.Tick(5040.1f);
                ExpectDamage(enemy, 0);
                actor.Server_KilledCharacter?.Invoke(crystalSource);
                UnityEngine.Random.InitState(reset);
                runtime.Tick(5040.25f);
                ExpectDamage(enemy, 16);
                actor.Server_KilledCharacter?.Invoke(crystalSource);
                UnityEngine.Random.InitState(reset);
                runtime.Tick(5040.5f);
                ExpectDamage(enemy, 0);
                actor.Server_KilledCharacter?.Invoke(crystalSource);
                UnityEngine.Random.InitState(reset);
                runtime.Tick(5040.75f);
                ExpectDamage(enemy, 16);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_SOUL_REAPER_OK chance boundary, cooldown reset and retrigger interval");
        }

        private static void ValidateDarkTidings()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Dark Tidings caster", 0, Vector3.right * 7375);
            var runtime = new GameObject("Dark Tidings validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseDarkTidings() || SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100 ||
                SkillBattlePreview.ScaleRaidWeaponRange(actor, 100f) != 100f)
                throw new InvalidOperationException("Dark Tidings was active before level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseDarkTidings() || runtime.TryChooseDarkTidings() ||
                SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 105 ||
                !Mathf.Approximately(SkillBattlePreview.ScaleRaidWeaponRange(actor, 100f), 135f))
                throw new InvalidOperationException("Dark Tidings selection or weapon multipliers failed.");
            runtime.Bind(null, progress, null);
            if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100 ||
                SkillBattlePreview.ScaleRaidWeaponRange(actor, 100f) != 100f)
                throw new InvalidOperationException("Dark Tidings survived unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_DARK_TIDINGS_OK level gate, weapon damage, range and unbinding");
        }

        private static void ValidateSpitefulPace()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7400;
            var actor = Spawn("Spiteful Pace caster", 0, origin);
            var first = Spawn("Spiteful Pace first enemy", 1, origin + Vector3.forward);
            var second = Spawn("Spiteful Pace second enemy", 1, origin + Vector3.right);
            var runtime = new GameObject("Spiteful Pace validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChooseSpitefulPace() || runtime.TryChooseSpitefulPace() ||
                !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.05f))
                throw new InvalidOperationException("Spiteful Pace reward or base speed failed.");
            runtime.SetRaidLevel(2);
            if (!runtime.TryChooseTrap())
                throw new InvalidOperationException("Could not select Time Trap.");
            const float now = 4700f;
            runtime.Cast(now);
            runtime.Tick(now);
            if (!Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.09f))
                throw new InvalidOperationException("Two afflicted enemies did not grant two speed stacks.");
            first.transform.position += Vector3.forward * 10f;
            runtime.Tick(now + 0.25f);
            if (!Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.07f))
                throw new InvalidOperationException("Leaving the trap did not remove one speed stack.");
            second.CurrentHealth = 0;
            runtime.Tick(now + 0.5f);
            if (!Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.05f))
                throw new InvalidOperationException("A defeated enemy still contributed a speed stack.");
            runtime.Bind(null, progress, null);
            if (SkillBattlePreview.RaidMoveMultiplier(actor) != 1f)
                throw new InvalidOperationException("Spiteful Pace survived unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_SPITEFUL_PACE_OK reward, per-enemy stacks, removal and unbinding");
        }

        private static void ValidateSoulmate()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7380;
            var actor = Spawn("Soulmate caster", 0, origin);
            var boundary = Spawn("Soulmate boundary target", 1, origin + Vector3.forward * 7f);
            var target = Spawn("Soulmate nearest target", 1, origin + Vector3.forward * 6f);
            var outside = Spawn("Soulmate out of range", 1, origin + Vector3.forward * 8f);
            var runtime = new GameObject("Soulmate validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Valkyrie;
            runtime.Bind(actor, progress, null);

            var randomState = UnityEngine.Random.state;
            try
            {
                runtime.Tick(4600f);
                runtime.Tick(4600.5f);
                ExpectDamage(target, 0);
                runtime.Tick(4601f);
                runtime.Tick(4601.1f);
                ExpectDamage(target, 0);
                UnityEngine.Random.InitState(FindDamageRollSeed(0f, 0.2f));
                runtime.Tick(4601.2f);
                ExpectDamage(target, 20);
                ExpectDamage(boundary, 0);
                ExpectDamage(outside, 0);
                if (!SkillBattlePreview.HasRaidCurse(target, 4604.1f) ||
                    SkillBattlePreview.HasRaidCurse(target, 4604.3f))
                    throw new InvalidOperationException("Soulmate curse did not last three seconds.");
                target.Team = actor.Team;
                runtime.Tick(4602f);
                boundary.transform.position += Vector3.forward;
                runtime.Tick(4602.2f);
                ExpectDamage(boundary, 0);
                boundary.transform.position -= Vector3.forward;
                runtime.Tick(4603f);
                runtime.Tick(4603.2f);
                ExpectDamage(target, 0);
                ExpectDamage(boundary, 20);
                ExpectDamage(outside, 0);
                runtime.Tick(4604f);
                runtime.Bind(null, progress, null);
                if (SkillBattlePreview.HasRaidCurse(target, 4603.2f))
                    throw new InvalidOperationException("Soulmate curse survived unbinding.");
                runtime.Bind(actor, progress, null);
                runtime.Tick(4604.2f);
                ExpectDamage(boundary, 0);
                runtime.Bind(null, progress, null);
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_SOULMATE_OK windup, nearest enemy, range recheck, curse and unbinding");
        }

        private static void ValidateValkyrieEvasion()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_loki_03_a"))
                throw new InvalidOperationException("Could not learn Loki Evasion.");
            Vector3 origin = Vector3.right * 7280;
            var actor = Spawn("Valkyrie evasion recipient", 0, origin);
            var enemy = Spawn("Valkyrie evasion attacker", 1, origin + Vector3.forward);
            var randomState = UnityEngine.Random.state;
            float[] cutoffs = { 0.15f, 0.20f, 0.25f };
            for (int rank = 0; rank < cutoffs.Length; rank++)
            {
                if (!progress.TryLearn($"virtue_valkyrie_02_{(char)('a' + rank)}"))
                    throw new InvalidOperationException("Could not learn the Valkyrie evasion virtue.");
                var runtime = new GameObject("Valkyrie evasion validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Valkyrie;
                runtime.Bind(actor, progress, null);
                float cutoff = cutoffs[rank];
                int low = FindDamageRollSeed(cutoff - 0.02f, cutoff - 0.005f);
                int high = FindDamageRollSeed(cutoff + 0.005f, cutoff + 0.02f);
                ExpectDirectHitDamage(actor, enemy, low, 0);
                ExpectDirectHitDamage(actor, enemy, high, 100);
                UnityEngine.Random.InitState(low);
                actor.ServerApplyRangeDot(100, AttackType.explosion, enemy);
                ExpectDamage(actor, 100);
                runtime.Bind(null, progress, null);
                ExpectDirectHitDamage(actor, enemy, low, 100);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_VALKYRIE_EVASION_OK three thresholds, Loki stacking, periodic damage and unbinding");
        }

        private static void ValidateLokiEvasion()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Loki Evasion validation").AddComponent<SkillBattlePreview>();
            Vector3 origin = Vector3.right * 4800;
            var first = Spawn("Loki Evasion first rank", 0, origin);
            var second = Spawn("Loki Evasion second rank", 0, origin);
            var enemy = Spawn("Loki Evasion attacker", 1, origin + Vector3.forward);
            var randomState = UnityEngine.Random.state;
            int low = FindDamageRollSeed(0f, 0.10f);
            int middle = FindDamageRollSeed(0.10f, 0.20f);
            int high = FindDamageRollSeed(0.20f, 0.30f);
            if (!progress.TryLearn("virtue_loki_03_a"))
                throw new InvalidOperationException("Could not learn Loki Evasion rank one.");
            runtime.Bind(first, progress, null);
            ExpectDirectHitDamage(first, enemy, low, 0);
            ExpectDirectHitDamage(first, enemy, middle, 100);

            if (!progress.TryLearn("virtue_loki_03_b"))
                throw new InvalidOperationException("Could not learn Loki Evasion rank two.");
            runtime.Bind(second, progress, null);
            ExpectDirectHitDamage(first, enemy, low, 100);
            ExpectDirectHitDamage(second, enemy, middle, 0);
            ExpectDirectHitDamage(second, enemy, high, 100);
            UnityEngine.Random.InitState(low);
            second.ServerApplyRangeDot(100, AttackType.hitscan, enemy);
            ExpectDamage(second, 100);
            runtime.Bind(null, progress, null);
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_LOKI_EVASION_OK both thresholds, periodic damage and rebind");
        }

        private static void ValidateThorAllDamage()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Thor damage validation").AddComponent<SkillBattlePreview>();
            int[] expected = { 207, 214, 228, 250 };
            Health actor = null, target = null;
            for (int i = 0; i < expected.Length; i++)
            {
                if (!progress.TryLearn($"virtue_thor_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn Thor's damage virtue.");
                Vector3 origin = Vector3.right * (4660 + 30 * i);
                actor = Spawn("Thor damage caster", 0, origin);
                target = Spawn("Thor damage target", 1, origin + Vector3.forward * 3f);
                runtime.Bind(actor, progress, null);
                target.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, actor, 0);
                ExpectDamage(target, expected[i]);
            }

            if (!runtime.TryChooseLightning())
                throw new InvalidOperationException("Could not select Lightning Strike.");
            runtime.Cast(3000);
            ExpectDamage(target, 21);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.falldamage, actor, 0);
            ExpectDamage(actor, 100);
            runtime.Bind(null, progress, null);
            target.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, actor, 0);
            ExpectDamage(target, 200);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_THOR_DAMAGE_OK four ranks, skill damage, fall exclusion and unbind");
        }

        private static void ValidateWardRechargeVirtue()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 4600;
            var actor = Spawn("Ward recharge caster", 0, origin);
            var attacker = Spawn("Ward recharge attacker", 1, origin + Vector3.forward);
            actor.CurrentHealth = 900;
            float[] intervals = { 33.25f, 31.5f, 28f, 22.75f };
            for (int i = 0; i < intervals.Length; i++)
            {
                if (!progress.TryLearn($"virtue_seer_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn Ward Restoring.");
                var runtime = new GameObject("Ward recharge validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Seeress;
                runtime.Bind(actor, progress, null);
                for (int charge = 0; charge < 3; charge++)
                    SkillBattlePreview.TriggerEldritchWard(actor, attacker, 3000 + charge * 2);

                float readyAt = 3000 + intervals[i];
                SkillBattlePreview.TriggerEldritchWard(actor, attacker, readyAt - 0.01f);
                if (SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, readyAt - 0.01f))
                    throw new InvalidOperationException("Ward Restoring recharged before the configured interval.");
                SkillBattlePreview.TriggerEldritchWard(actor, attacker, readyAt);
                if (!SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, readyAt))
                    throw new InvalidOperationException("Ward Restoring did not recharge at the configured interval.");
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_WARD_RESTORING_OK all four recharge boundaries");
        }

        private static void ValidatePremonition()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7420;
            var actor = Spawn("Premonition caster", 0, origin);
            var attacker = Spawn("Premonition attacker", 1, origin + Vector3.forward);
            var runtime = new GameObject("Premonition validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            actor.CurrentHealth -= 100;

            var randomState = UnityEngine.Random.state;
            try
            {
                if (!runtime.TryChoosePremonition() || runtime.TryChoosePremonition())
                    throw new InvalidOperationException("Premonition was unavailable at level one or repeatable.");

                int refund = FindDamageRollSeed(0.28f, 0.30f);
                int spend = FindDamageRollSeed(0.30f, 0.32f);
                const float now = 4750f;
                foreach (float offset in new[] { 0f, 2f, 4f, 6f })
                {
                    UnityEngine.Random.InitState(offset == 2f ? refund : spend);
                    SkillBattlePreview.TriggerEldritchWard(actor, attacker, now + offset);
                    if (!SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, now + offset))
                        throw new InvalidOperationException("Premonition did not preserve the refunded charge.");
                }
                UnityEngine.Random.InitState(spend);
                SkillBattlePreview.TriggerEldritchWard(actor, attacker, now + 34.9f);
                if (SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, now + 34.9f))
                    throw new InvalidOperationException("An unsuccessful refund left an extra charge.");
                SkillBattlePreview.TriggerEldritchWard(actor, attacker, now + 35f);
                if (!SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, now + 35f))
                    throw new InvalidOperationException("A successful refund shifted the existing recharge deadline.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_PREMONITION_OK one-rank reward, refund threshold and recharge deadline");
        }

        private static void ValidateBoneChill()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7440;
            var actor = Spawn("Bone Chill caster", 0, origin);
            var enemy = Spawn("Bone Chill enemy", 1, origin + Vector3.forward * 5f);
            var outside = Spawn("Bone Chill outside", 1, origin + Vector3.forward * 5.1f);
            var ally = Spawn("Bone Chill ally", 0, origin + Vector3.right);
            var runtime = new GameObject("Bone Chill validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseBoneChill())
                throw new InvalidOperationException("Bone Chill was selectable before level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseBoneChill() || runtime.TryChooseBoneChill())
                throw new InvalidOperationException("Bone Chill was unavailable or repeatable.");

            const float now = 4780f;
            actor.CurrentHealth -= 100;
            SkillBattlePreview.TriggerEldritchWard(actor, outside, now);
            runtime.Tick(now + 2.99f);
            if (!SkillBattlePreview.IsStoppedByRaidControl(enemy, now + 2.99f) ||
                !SkillBattlePreview.IsFrozenByRaidSkill(enemy, now + 2.99f) ||
                SkillBattlePreview.IsFrozenByRaidSkill(enemy, now + 3f) ||
                SkillBattlePreview.IsStoppedByRaidControl(outside, now + 2.99f) ||
                SkillBattlePreview.IsStoppedByRaidControl(ally, now + 2.99f) ||
                SkillBattlePreview.IsStoppedByRaidControl(enemy, now + 3f))
                throw new InvalidOperationException("Bone Chill did not filter by distance and team.");
            runtime.Tick(now + 3f);
            if (SkillBattlePreview.IsStoppedBySkill(enemy))
                throw new InvalidOperationException("Bone Chill did not expire after three seconds.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_BONE_CHILL_OK level gate, radius, teams and duration");
        }

        private static void ValidateIllOmen()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7720;
            var actor = Spawn("Ill Omen caster", 0, origin);
            var target = Spawn("Ill Omen target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Ill Omen validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(1);
            if (!runtime.TryChooseIllOmen() || runtime.TryChooseIllOmen())
                throw new InvalidOperationException("Ill Omen selection or rank cap failed.");
            target.ServerApplyRaidSkillDamage(100, actor);
            ExpectDamage(target, 100);

            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseBoneChill())
                throw new InvalidOperationException("Could not select Bone Chill.");
            actor.CurrentHealth -= 100;
            float now = Time.time;
            SkillBattlePreview.TriggerEldritchWard(actor, target, now);
            target.ServerApplyRaidSkillDamage(100, actor);
            ExpectDamage(target, 110);
            target.ServerApplyRangeDot(100, AttackType.explosion, actor);
            ExpectDamage(target, 100);
            runtime.Tick(now + 3f);
            target.ServerApplyRaidSkillDamage(100, actor);
            ExpectDamage(target, 100);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_ILL_OMEN_OK frozen damage, periodic isolation and expiry");
        }

        private static void ValidateMindBlastSlow()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7900;
            var actor = Spawn("Mind Blast slow caster", 0, origin);
            var enemy = Spawn("Mind Blast slow target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Mind Blast slow validation").AddComponent<SkillBattlePreview>();
            runtime.Bind(actor, progress, null);
            if (!runtime.TryChooseMind())
                throw new InvalidOperationException("Could not select Mind Blast.");

            var randomState = UnityEngine.Random.state;
            try
            {
                UnityEngine.Random.State attackRoll;
                do
                {
                    attackRoll = UnityEngine.Random.state;
                    UnityEngine.Random.Range(0, 1);
                }
                while (UnityEngine.Random.value >= 0.2f);
                UnityEngine.Random.state = attackRoll;
                runtime.Cast(5100f);
                ExpectDamage(enemy, 16);
                if (Vector3.Distance(enemy.transform.position, origin + Vector3.forward * 3.5f) > 0.001f)
                    throw new InvalidOperationException("Mind Blast did not push the surviving target away from its origin.");
                if (!SkillBattlePreview.IsSlowedByRaidSkill(enemy, 5101.99f) ||
                    SkillBattlePreview.IsSlowedByRaidSkill(enemy, 5102f) ||
                    SkillBattlePreview.RaidSkillAttackSpeedFactor(enemy, 5101.99f) != 0.75f ||
                    SkillBattlePreview.RaidSkillAttackSpeedFactor(enemy, 5102f) != 1f)
                    throw new InvalidOperationException("Mind Blast slow did not last two seconds.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_MIND_BLAST_SLOW_OK successful hit, outward knockback and two-second expiry");
        }

        private static void ValidateBriarRoot()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7940;
            var actor = Spawn("Briar root caster", 0, origin);
            var enemy = Spawn("Briar root target", 1, origin);
            var runtime = new GameObject("Briar root validation").AddComponent<SkillBattlePreview>();
            runtime.Bind(actor, progress, null);
            if (!runtime.TryChooseBriar())
                throw new InvalidOperationException("Could not select Briar Thorns.");

            var randomState = UnityEngine.Random.state;
            try
            {
                runtime.Cast(5120f);
                UnityEngine.Random.InitState(FindDamageRollSeed(0.18f, 0.20f));
                runtime.Tick(5120f);
                ExpectDamage(enemy, 24);
                runtime.Tick(5121f);
                ExpectDamage(enemy, 0);
                if (!SkillBattlePreview.IsRootedByRaidSkill(enemy, 5123.99f) ||
                    SkillBattlePreview.IsRootedByRaidSkill(enemy, 5124f) || SkillBattlePreview.IsStoppedBySkill(enemy))
                    throw new InvalidOperationException("Briar root did not remain independent from other control effects.");
            }
            finally
            {
                UnityEngine.Random.state = randomState;
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_BRIAR_ROOT_OK hit deduplication and independent four-second root");
        }

        private static void ValidateWardEffectiveness()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7460;
            var actor = Spawn("Ward Effectiveness caster", 0, origin);
            var attacker = Spawn("Ward Effectiveness attacker", 1, origin + Vector3.forward);
            var runtime = new GameObject("Ward Effectiveness validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            actor.CurrentHealth -= 100;

            int[] levels = { 1, 8, 15 };
            float[] durations = { 1.5f, 1.85f, 2.2f };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                if (rank > 0)
                {
                    runtime.SetRaidLevel(levels[rank] - 1);
                    if (runtime.TryChooseWardEffectiveness())
                        throw new InvalidOperationException("Ward Effectiveness rank ignored its level gate.");
                }
                runtime.SetRaidLevel(levels[rank]);
                if (!runtime.TryChooseWardEffectiveness() || runtime.TryChooseWardEffectiveness())
                    throw new InvalidOperationException("Ward Effectiveness rank selection or cap failed.");

                float now = 4820f + rank * 4f;
                SkillBattlePreview.TriggerEldritchWard(actor, attacker, now);
                float end = now + durations[rank];
                if (!SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, end - 0.01f) ||
                    SkillBattlePreview.IsProtectedByEldritchWard(actor, attacker, end))
                    throw new InvalidOperationException("Ward Effectiveness applied an incorrect protection duration.");
            }
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_WARD_EFFECTIVENESS_OK three level gates and protection durations");
        }

        private static void ValidateTimeManipulation()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7480;
            var actor = Spawn("Time Manipulation caster", 0, origin);
            var enemy = Spawn("Time Manipulation enemy", 1, origin + Vector3.forward * 5f);
            var runtime = new GameObject("Time Manipulation validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(14);
            if (runtime.TryChooseTimeManipulation())
                throw new InvalidOperationException("Time Manipulation was offered before level fifteen.");
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseTimeManipulation() || runtime.TryChooseTimeManipulation() || !runtime.TryChooseMind() ||
                !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.21f))
                throw new InvalidOperationException("Time Manipulation selection or full-charge speed failed.");

            const float now = 4850f;
            runtime.Cast(now);
            ExpectDamage(enemy, 16);
            runtime.Cast(now + 1.97f);
            ExpectDamage(enemy, 0);
            runtime.Cast(now + 1.98f);
            ExpectDamage(enemy, 16);

            actor.CurrentHealth -= 100;
            SkillBattlePreview.TriggerEldritchWard(actor, enemy, now + 2f);
            if (!Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.14f))
                throw new InvalidOperationException("Spending a ward did not remove an attack speed stack.");
            runtime.Tick(now + 37f);
            if (!Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.21f))
                throw new InvalidOperationException("Recharging a ward did not restore its attack speed stack.");

            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_TIME_MANIPULATION_OK level gate, cooldown, ward expenditure and recharge");
        }

        private static void ValidateHealingWard()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_seer_04_a") || !progress.TryLearn("passive_seer_P"))
                throw new InvalidOperationException("Could not unlock the Seeress ward nodes.");

            var runtime = new GameObject("Healing Ward validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            Vector3 origin = Vector3.right * 4540;
            var actor = Spawn("Healing Ward caster", 0, origin);
            var enemy = Spawn("Healing Ward attacker", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseHealingWard() || !runtime.TryChooseHealingWard() || !runtime.TryChooseHealingWard() ||
                runtime.TryChooseHealingWard() || actor.MaxHealth != 1075)
                throw new InvalidOperationException("Healing Ward rank cap or maximum health failed.");

            float now = Time.time;
            actor.CurrentHealth = actor.MaxHealth;
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 55);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 0);
            for (int i = 1; i <= 5; i++)
            {
                actor.CurrentHealth = 900;
                float hitAt = now + i * 2f;
                SkillBattlePreview.TriggerEldritchWard(actor, enemy, hitAt);
                if (SkillBattlePreview.IsProtectedByEldritchWard(actor, enemy, hitAt) != (i <= 4))
                    throw new InvalidOperationException("Eldritch Ward did not have exactly five charges with Serpent Skin.");
            }
            SkillBattlePreview.TriggerEldritchWard(actor, enemy, now + 35.01f);
            if (!SkillBattlePreview.IsProtectedByEldritchWard(actor, enemy, now + 35.01f))
                throw new InvalidOperationException("Eldritch Ward did not recharge after 35 seconds.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_ELDRITCH_WARD_OK healing, immunity, five charges and recharge");
        }

        private static void ValidateFormation()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Formation validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Valkyrie;
            var actor = Spawn("Formation caster", 0, Vector3.right * 4480);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseFormation() || !progress.TryLearn("passive_valkyrie_P") || !runtime.TryChooseFormation() ||
                SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 110)
                throw new InvalidOperationException("Formation unlock or first rank failed.");
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseFormation())
                throw new InvalidOperationException("Formation rank two was offered below level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseFormation() || SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 120 ||
                runtime.TryChooseFormation())
                throw new InvalidOperationException("Formation rank two or level fifteen gate failed.");
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseFormation() || SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 130 ||
                runtime.TryChooseFormation() ||
                !SkillBattlePreview.IsFormationStationary(true, Vector2.zero, Vector3.zero) ||
                SkillBattlePreview.IsFormationStationary(false, Vector2.zero, Vector3.zero) ||
                SkillBattlePreview.IsFormationStationary(true, Vector2.up, Vector3.zero) ||
                SkillBattlePreview.IsFormationStationary(true, Vector2.zero, Vector3.right))
                throw new InvalidOperationException("Formation rank cap or stationary rules failed.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_FORMATION_OK rank gates, base damage and stationary predicate");
        }

        private static void ValidateBulwark()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Bulwark validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Warden;
            Vector3 origin = Vector3.right * 4420;
            var actor = Spawn("Bulwark caster", 0, origin);
            var enemy = Spawn("Bulwark attacker", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseBulwark() || !progress.TryLearn("passive_warden_P") || !runtime.TryChooseBulwark())
                throw new InvalidOperationException("Bulwark permanent unlock gate failed.");

            var randomState = UnityEngine.Random.state;
            int low = FindDamageRollSeed(0f, 0.06f);
            int middle = FindDamageRollSeed(0.06f, 0.13f);
            int high = FindDamageRollSeed(0.13f, 0.20f);
            ExpectDirectHitDamage(actor, enemy, low, 0);
            ExpectDirectHitDamage(actor, enemy, middle, 100);
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseBulwark())
                throw new InvalidOperationException("Bulwark rank two was offered below level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseBulwark() || runtime.TryChooseBulwark())
                throw new InvalidOperationException("Bulwark rank two or level fifteen gate failed.");
            ExpectDirectHitDamage(actor, enemy, middle, 0);
            ExpectDirectHitDamage(actor, enemy, high, 100);
            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseBulwark() || runtime.TryChooseBulwark())
                throw new InvalidOperationException("Bulwark rank three or maximum rank failed.");
            ExpectDirectHitDamage(actor, enemy, high, 0);
            UnityEngine.Random.InitState(low);
            actor.ServerApplyRangeDot(100, AttackType.hitscan, enemy);
            ExpectDamage(actor, 100);
            runtime.Bind(null, progress, null);
            ExpectDirectHitDamage(actor, enemy, low, 100);
            UnityEngine.Random.state = randomState;
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_BULWARK_OK level gates, three block thresholds, periodic damage and unbind");
        }

        private static int FindDamageRollSeed(float minimum, float maximum)
        {
            for (int seed = 0; seed < 1000; seed++)
            {
                UnityEngine.Random.InitState(seed);
                float roll = UnityEngine.Random.value;
                if (roll >= minimum && roll < maximum)
                    return seed;
            }
            throw new InvalidOperationException("Could not find a deterministic damage avoidance roll.");
        }

        private static void ExpectDirectHitDamage(Health actor, Health enemy, int seed, int expected)
        {
            UnityEngine.Random.InitState(seed);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, expected);
        }

        private static void ValidateRaidDamageReduction()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            foreach (string id in new[] { "virtue_berserker_01_a", "virtue_berserker_01_b", "virtue_berserker_01_c",
                "virtue_berserker_01_d", "virtue_freya_03_a", "virtue_freya_03_b", SkillBattlePreview.BubbleUnlock })
            {
                if (!progress.TryLearn(id))
                    throw new InvalidOperationException("Could not learn the damage reduction nodes.");
            }

            Vector3 origin = Vector3.right * 4360;
            var actor = Spawn("Raid damage reduction caster", 0, origin);
            var enemy = Spawn("Raid damage reduction attacker", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Berserker damage reduction validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 65);
            if (!runtime.TryChooseTidalBarrier())
                throw new InvalidOperationException("Could not choose Tidal Barrier for damage reduction.");
            runtime.Cast(2800);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 45);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);

            var warden = new GameObject("Warden damage reduction validation").AddComponent<SkillBattlePreview>();
            warden.LiveMatch = true;
            warden.SelectedRaidClass = RaidClass.Warden;
            warden.Bind(actor, progress, null);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 80);
            warden.Bind(null, progress, null);
            UnityEngine.Object.Destroy(warden.gameObject);
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 100);
            Debug.Log("JOTUNNSLAYER_DAMAGE_REDUCTION_OK server damage, class gate, Tidal Barrier and unbind");
        }

        private static void ValidatePermanentCombatRefund()
        {
            var catalog = SkillCatalog.Load();
            var profile = new SkillProgress(catalog, null, 50);
            var actor = Spawn("Permanent refund caster", 0, Vector3.right * 4380);
            var enemy = Spawn("Permanent refund attacker", 1, actor.transform.position + Vector3.forward * 3f);
            var runtime = new GameObject("Permanent combat refund validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Berserker;
            runtime.Bind(actor, profile, null);
            if (!runtime.TryChooseLightning())
                throw new InvalidOperationException("Could not choose a raid skill before permanent refund.");
            int level = runtime.RaidLevel, pending = runtime.PendingRewards;
            int[] outgoing = { 105, 110, 120, 135 };
            int[] incoming = { 98, 96, 92, 85 };
            void Check(int weapon, int received)
            {
                if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != weapon)
                    throw new InvalidOperationException($"Permanent weapon cache expected {weapon} after profile mutation.");
                actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
                ExpectDamage(actor, received);
                if (runtime.RaidLevel != level || runtime.PendingRewards != pending)
                    throw new InvalidOperationException("Permanent combat refresh reset raid progression.");
            }
            Check(100, 100);
            for (int cycle = 0; cycle < 2; cycle++)
            {
                for (int rank = 0; rank < 4; rank++)
                {
                    if (!profile.TryLearn($"virtue_berserker_02_{(char)('a' + rank)}") ||
                        !profile.TryLearn($"virtue_berserker_01_{(char)('a' + rank)}"))
                        throw new InvalidOperationException("Could not learn permanent combat ranks.");
                    Check(outgoing[rank], incoming[rank]);
                }
                profile.Reset();
                Check(100, 100);
            }
            // A replacement profile can have the same revision as the previous one.
            var replacement = new SkillProgress(catalog, null, 20);
            for (int reset = 0; reset < 12; reset++) replacement.Reset();
            for (int rank = 0; rank < 4; rank++)
                if (!replacement.TryLearn($"virtue_odin_01_{(char)('a' + rank)}"))
                    throw new InvalidOperationException("Could not learn replacement Bravery profile.");
            if (!replacement.TryLearn("virtue_ranger_04_a") || !replacement.TryLearn("virtue_freya_03_a"))
                throw new InvalidOperationException("Could not learn replacement class and shared nodes.");
            if (replacement.LearnedRevision != profile.LearnedRevision)
                throw new InvalidOperationException("Replacement profile did not exercise an equal revision.");
            runtime.Bind(actor, replacement, null);
            Check(120, 90);
            runtime.SelectedRaidClass = RaidClass.Ranger;
            Check(130, 90);
            runtime.SelectedRaidClass = RaidClass.Warden;
            Check(120, 90);
            runtime.Bind(actor, null, null);
            Check(100, 100);
            runtime.Bind(null, replacement, null);
            if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100)
                throw new InvalidOperationException("Permanent weapon cache leaked after unbinding.");
            actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
            ExpectDamage(actor, 100);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_PERMANENT_COMBAT_REFUND_OK live learning, four ranks, refund, relearn, profile replacement, class gates, null profile, raid preservation and unbind");
        }

        [Serializable]
        private sealed class RewardLocalizationCatalog
        {
            public RewardLocalizationEntry[] entries;
        }

        [Serializable]
        private sealed class RewardLocalizationEntry
        {
            public string id;
            public string[] effects, effectsZh;
        }

        private static void ValidateRewardLocalization()
        {
            var resource = Resources.Load<TextAsset>("Jotunnslayer/RaidRewardEffects");
            var data = resource ? JsonUtility.FromJson<RewardLocalizationCatalog>(resource.text) : null;
            if (data?.entries == null || data.entries.Length == 0)
                throw new InvalidOperationException("Reward localization resource missing.");
            var ids = new System.Collections.Generic.HashSet<string>();
            int ranks = 0;
            foreach (var entry in data.entries)
            {
                if (string.IsNullOrEmpty(entry.id) || !ids.Add(entry.id) || entry.effects == null ||
                    entry.effects.Length == 0 || entry.effectsZh == null || entry.effectsZh.Length != entry.effects.Length)
                    throw new InvalidOperationException("Reward localization has duplicate IDs or missing ranks: " + entry.id);
                for (int i = 0; i < entry.effects.Length; i++)
                {
                    string english = entry.effects[i], chinese = entry.effectsZh[i];
                    if (string.IsNullOrWhiteSpace(english) || string.IsNullOrWhiteSpace(chinese) ||
                        System.Text.RegularExpressions.Regex.IsMatch(chinese, "[A-Za-z]") ||
                        !System.Text.RegularExpressions.Regex.IsMatch(chinese, "[\\u4e00-\\u9fff]"))
                        throw new InvalidOperationException("Reward translation missing: " + entry.id + " rank " + i);
                    var enValues = System.Text.RegularExpressions.Regex.Matches(english, @"\d+(?:\.\d+)?|>>|[+%\-]");
                    var zhValues = System.Text.RegularExpressions.Regex.Matches(chinese, @"\d+(?:\.\d+)?|>>|[+%\-]");
                    if (enValues.Count != zhValues.Count)
                        throw new InvalidOperationException("Reward translation lost numeric operators: " + entry.id);
                    for (int value = 0; value < enValues.Count; value++)
                        if (enValues[value].Value != zhValues[value].Value)
                            throw new InvalidOperationException("Reward translation changed numeric meaning: " + entry.id);
                    ranks++;
                }
            }
            var catalog = SkillCatalog.Load();
            for (int raidClass = 0; raidClass <= (int)RaidClass.Valkyrie; raidClass++)
                for (int language = 0; language < 2; language++)
                {
                    bool english = language == 1;
                    var actor = Spawn("Reward language owner", 0, Vector3.right * 4470 + Vector3.forward * raidClass * 20);
                    var runtime = new GameObject("Reward language runtime").AddComponent<SkillBattlePreview>();
                    runtime.LiveMatch = true;
                    runtime.SelectedRaidClass = (RaidClass)raidClass;
                    runtime.ConfigureRewardDisplay(catalog, null, english);
                    runtime.Bind(actor, new SkillProgress(catalog, null, 20), null);
                    var snapshot = runtime.BuildRemoteSnapshot();
                    if (snapshot.details == null || snapshot.details.Length == 0 || snapshot.details.Length != snapshot.labels.Length)
                        throw new InvalidOperationException("Reward language snapshot missing for class " + raidClass);
                    foreach (string effect in snapshot.details)
                        if (string.IsNullOrWhiteSpace(effect) || (english
                            ? !System.Text.RegularExpressions.Regex.IsMatch(effect, "[A-Za-z]")
                            : System.Text.RegularExpressions.Regex.IsMatch(effect, "[A-Za-z]") ||
                              !System.Text.RegularExpressions.Regex.IsMatch(effect, "[\\u4e00-\\u9fff]")))
                            throw new InvalidOperationException("Reward snapshot mixed languages for class " + raidClass);
                    runtime.Bind(null, null, null);
                    UnityEngine.Object.Destroy(runtime.gameObject);
                    UnityEngine.Object.Destroy(actor.gameObject);
                }
            Debug.Log("JOTUNNSLAYER_REWARD_LOCALIZATION_OK entries=" + ids.Count + " ranks=" + ranks +
                " numericOperatorsPreserved=True classLanguageSnapshots=12; transport pending");
        }

        private static void ValidateRemoteSkillLoadouts()
        {
            var catalog = SkillCatalog.Load();
            var local = new SkillProgress(catalog, null, 10, 77);
            if (!local.TryLearn(SkillBattlePreview.MissileUnlock) ||
                !SkillProgress.TryImportLoadout(catalog, local.ExportLearnedNodes(), out var imported) ||
                !imported.IsLearned(SkillBattlePreview.MissileUnlock) || imported.Gold != 0 || local.Gold != 77)
                throw new InvalidOperationException("Remote loadout import mixed permanent skills and currency.");
            foreach (var invalid in new[] { new[] { "missing-node" },
                new[] { SkillBattlePreview.MissileUnlock, SkillBattlePreview.MissileUnlock },
                new[] { SkillBattlePreview.MissileEvolution } })
                if (SkillProgress.TryImportLoadout(catalog, invalid, out _))
                    throw new InvalidOperationException("Remote loadout accepted unknown, duplicate or missing prerequisite nodes.");
            var actor = Spawn("Remote snapshot owner", 0, Vector3.right * 4410);
            var otherActor = Spawn("Remote snapshot other owner", 0, Vector3.right * 4450);
            var runtime = new GameObject("Remote snapshot runtime").AddComponent<SkillBattlePreview>();
            var other = new GameObject("Remote snapshot other runtime").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = other.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            other.SelectedRaidClass = RaidClass.Berserker;
            runtime.ConfigureRewardDisplay(catalog, null, true);
            runtime.Bind(actor, imported, null);
            other.Bind(otherActor, new SkillProgress(catalog, null, 10, 99), null);
            var initial = runtime.BuildRemoteSnapshot();
            var untouched = other.BuildRemoteSnapshot();
            int acceptedIndex = -1;
            for (int i = 0; i < initial.actions.Length; i++)
                if ((initial.actions[i] & 1) != 0) { acceptedIndex = i; break; }
            if (initial.character != actor.netId || !initial.alive || initial.pending != 1 || acceptedIndex < 0 ||
                initial.details == null || initial.details.Length != initial.labels.Length ||
                !runtime.TryRemoteRewardAction(JotunnSkillAction.Accept, acceptedIndex))
                throw new InvalidOperationException("Remote snapshot did not expose an actual selectable server reward.");
            var accepted = runtime.BuildRemoteSnapshot();
            var stillUntouched = other.BuildRemoteSnapshot();
            if (accepted.pending != 0 || accepted.labels.Length != 0 ||
                runtime.TryRemoteRewardAction(JotunnSkillAction.Accept, acceptedIndex) ||
                untouched.pending != stillUntouched.pending || stillUntouched.gold != 99 ||
                stillUntouched.character != otherActor.netId || local.Gold != 77)
                throw new InvalidOperationException("Remote reward crossed owners or could be consumed twice.");
            runtime.Bind(null, imported, null);
            if (runtime.BuildRemoteSnapshot().alive)
                throw new InvalidOperationException("Unbound remote snapshot remained alive.");
            other.Bind(null, null, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Object.Destroy(other.gameObject);
            Debug.Log("JOTUNNSLAYER_REMOTE_LOADOUT_SNAPSHOT_OK import gates, currency separation, independent server owners, actual offer acceptance, duplicate rejection and unbind; transport pending");
        }

        private static void ValidateNidhoggPassives()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Nidhogg passive caster", 0, Vector3.right * 6200);
            var enemy = Spawn("Nidhogg passive enemy", 1, actor.transform.position + Vector3.forward);
            var ally = Spawn("Nidhogg passive ally", 0, actor.transform.position + Vector3.right);
            var runtime = new GameObject("Nidhogg passive validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseWingsOfFury() || !progress.TryLearn("passive_nidhogg_I") ||
                !progress.TryLearn("passive_nidhogg_F") || runtime.TryChooseAllIn() || !runtime.TryChooseWingsOfFury() ||
                !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.08f))
                throw new InvalidOperationException("Nidhogg passive first ranks or unlock gates failed.");

            actor.Server_KilledCharacter?.Invoke(enemy);
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseWingsOfFury() || !runtime.TryChooseAllIn() || runtime.TryChooseAllIn() ||
                !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.16f))
                throw new InvalidOperationException("Nidhogg passive level eight gates failed.");
            actor.CurrentHealth = 800;
            actor.Server_KilledCharacter?.Invoke(ally);
            if (actor.ServerHeal(50, actor.netId) != 0 || actor.CurrentHealth != 800 ||
                SkillBattlePreview.ScaleRaidAllDamage(actor, 1000) != 1000)
                throw new InvalidOperationException("All In allowed healing or counted an ineligible kill.");
            for (int kill = 0; kill < 10; kill++)
                actor.Server_KilledCharacter?.Invoke(enemy);
            if (SkillBattlePreview.ScaleRaidAllDamage(actor, 1000) != 1013)
                throw new InvalidOperationException("All In lost fractional damage stacks.");
            enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
            ExpectDamage(enemy, 101);

            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseWingsOfFury() || runtime.TryChooseWingsOfFury() ||
                !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.24f))
                throw new InvalidOperationException("Wings of Fury final rank failed.");
            runtime.Bind(null, progress, null);
            if (actor.ServerHeal(50, actor.netId) != 50 || SkillBattlePreview.RaidMoveMultiplier(actor) != 1f ||
                SkillBattlePreview.ScaleRaidAllDamage(actor, 1000) != 1000)
                throw new InvalidOperationException("Nidhogg passives persisted after unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_NIDHOGG_PASSIVES_OK kill stacks, healing lock, movement ranks and unbinding");
        }

        private static void ValidateNjordPassives()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Njord passive caster", 0, Vector3.right * 6000);
            var target = Spawn("Njord passive target", 1, actor.transform.position + Vector3.forward * 2f);
            var runtime = new GameObject("Njord passive validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(2);
            if (runtime.TryChooseMightyOars() || runtime.TryChooseTorrents() ||
                !progress.TryLearn("passive_njord_H") || !progress.TryLearn("passive_njord_I") ||
                !runtime.TryChooseMightyOars() || !runtime.TryChooseTorrents())
                throw new InvalidOperationException("Njord passive unlock gates failed.");

            float now = 3400;
            runtime.Tick(now);
            actor.transform.position += Vector3.right * 100f;
            target.transform.position += Vector3.right * 100f;
            runtime.Tick(now += 0.25f);
            ExpectDamage(target, 0);

            for (int rank = 1; rank <= 2; rank++)
            {
                if (rank == 2)
                {
                    runtime.SetRaidLevel(8);
                    if (!runtime.TryChooseMightyOars() || runtime.TryChooseMightyOars())
                        throw new InvalidOperationException("Mighty Oars second rank gate failed.");
                }
                int steps = rank == 1 ? 6 : 4;
                for (int step = 1; step <= steps; step++)
                {
                    actor.transform.position += Vector3.right * 5f;
                    target.transform.position = actor.transform.position + Vector3.forward * 2f;
                    runtime.Tick(now += 0.25f);
                    ExpectDamage(target, step == steps ? 22 : 0);
                }
                if (!Mathf.Approximately(target.transform.position.z - actor.transform.position.z, 3f))
                    throw new InvalidOperationException("Mighty Oars knockback failed.");
            }

            if (!runtime.TryChooseLightning())
                throw new InvalidOperationException("Could not choose Lightning for Torrents validation.");
            target.transform.position = actor.transform.position + Vector3.forward * 3f;
            float[] factors = { 0.94f, 0.87f, 0.8f };
            for (int i = 0; i < factors.Length; i++)
            {
                if (i > 0)
                {
                    runtime.SetRaidLevel(i == 1 ? 8 : 15);
                    if (!runtime.TryChooseTorrents())
                        throw new InvalidOperationException("Torrents upgrade gate failed.");
                }
                now = 3410 + i * 10;
                float cooldown = 4.25f * factors[i];
                runtime.Cast(now);
                ExpectDamage(target, 17);
                runtime.Cast(now + cooldown - 0.02f);
                ExpectDamage(target, 0);
                runtime.Cast(now + cooldown + 0.02f);
                ExpectDamage(target, 17);
            }
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_NJORD_PASSIVES_OK distance thresholds, teleport rejection, knockback and cooldowns");
        }

        private static void ValidateSnowFight()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Snow Fight caster", 0, Vector3.right * 5920);
            var target = Spawn("Snow Fight target", 1, actor.transform.position + Vector3.forward * 2f);
            var other = Spawn("Snow Fight farther target", 1, actor.transform.position + Vector3.right * 7f);
            var ally = Spawn("Snow Fight ally", 0, actor.transform.position + Vector3.forward);
            var runtime = new GameObject("Snow Fight validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseSnowFight() || !progress.TryLearn("passive_skadi_I") || !runtime.TryChooseSnowFight())
                throw new InvalidOperationException("Snow Fight unlock gate failed.");

            int[] levels = { 1, 8, 15 };
            int[] damage = { 8, 11, 15 };
            float[] cooldowns = { 1.5f, 1.1f, 0.7f };
            for (int i = 0; i < levels.Length; i++)
            {
                if (runtime.TryChooseSnowFight())
                    throw new InvalidOperationException("Snow Fight offered a rank too early.");
                if (i > 0)
                {
                    runtime.SetRaidLevel(levels[i]);
                    if (!runtime.TryChooseSnowFight())
                        throw new InvalidOperationException("Snow Fight upgrade gate failed.");
                }

                float now = 3300 + 4 * i;
                float before = target.transform.position.z;
                runtime.Tick(now);
                ExpectDamage(target, damage[i]);
                if (!Mathf.Approximately(target.transform.position.z - before, 0.5f))
                    throw new InvalidOperationException("Snow Fight weak knockback failed.");
                runtime.Tick(now + cooldowns[i] - 0.02f);
                ExpectDamage(target, 0);
                runtime.Tick(now + cooldowns[i] + 0.02f);
                ExpectDamage(target, damage[i]);
                ExpectDamage(ally, 0);
                ExpectDamage(other, 0);
            }
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_SNOW_FIGHT_OK three ranks, automatic cadence, targeting and weak knockback");
        }

        private static void ValidateIceBlock()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Ice Block recipient", 0, Vector3.right * 5880);
            var enemy = Spawn("Ice Block enemy", 1, actor.transform.position + Vector3.forward);
            var runtime = new GameObject("Ice Block validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);

            if (runtime.TryChooseIceBlock() || !progress.TryLearn("passive_skadi_H") || !runtime.TryChooseIceBlock() ||
                runtime.TryChooseIceBlock() || SkillBattlePreview.IsProtectedByIceBlock(actor, Time.time))
                throw new InvalidOperationException("Ice Block unlock, rank gate or selection trigger failed.");

            for (int rank = 1; rank <= 2; rank++)
            {
                if (rank == 2)
                {
                    runtime.SetRaidLevel(8);
                    if (!runtime.TryChooseIceBlock() || runtime.TryChooseIceBlock())
                        throw new InvalidOperationException("Ice Block second rank gate failed.");
                }

                int initialLevel = runtime.RaidLevel;
                for (int kill = 0; kill < 100 && runtime.RaidLevel == initialLevel; kill++)
                    actor.Server_KilledCharacter?.Invoke(enemy);
                float expiry = Time.time + (rank == 1 ? 4f : 7f);
                actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, enemy, 0);
                ExpectDamage(actor, 0);
                if (runtime.RaidLevel <= initialLevel || !SkillBattlePreview.IsProtectedByIceBlock(actor, expiry - 0.01f) ||
                    SkillBattlePreview.IsProtectedByIceBlock(actor, expiry))
                    throw new InvalidOperationException("Ice Block level-up trigger or duration failed.");
            }

            runtime.Bind(null, progress, null);
            if (SkillBattlePreview.IsProtectedByIceBlock(actor, Time.time))
                throw new InvalidOperationException("Ice Block persisted after unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_ICE_BLOCK_OK unlock, two level-up durations, damage immunity and unbinding");
        }

        private static void ValidateAnsuzPersistentCasts()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            if (!progress.TryLearn("passive_broksindri_E_evolve") ||
                !progress.TryLearn("passive_broksindri_F_evolve") || !progress.TryLearn(SkillBattlePreview.BoulderEvolution))
                throw new InvalidOperationException("Could not learn the persistent cast evolutions.");
            Vector3 origin = Vector3.right * 7020;
            var actor = Spawn("Ansuz persistent caster", 0, origin);
            var behind = Spawn("Following boulder target", 1, origin + Vector3.back * 1.5f);
            var ahead = Spawn("Fire Barrier target", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Ansuz persistent cast validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(22);
            for (int rank = 0; rank < 4; rank++)
                if (!runtime.TryChooseAnsuz() || !runtime.TryChooseBoulder())
                    throw new InvalidOperationException("Could not select the persistent cast ranks.");
            if (!runtime.TryChooseFireBarrier())
                throw new InvalidOperationException("Could not select Fire Barrier.");

            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.State rolls;
            float first, second;
            do
            {
                rolls = UnityEngine.Random.state;
                first = UnityEngine.Random.value;
                second = UnityEngine.Random.value;
            }
            while (first >= 0.13f || second >= 0.13f);
            UnityEngine.Random.state = rolls;
            runtime.Cast(4100f);
            UnityEngine.Random.state = randomState;
            runtime.Tick(4100f);
            ExpectDamage(behind, 134);
            ExpectDamage(ahead, 0);
            runtime.Tick(4101f);
            ExpectDamage(behind, 134);
            ExpectDamage(ahead, 56);
            runtime.Tick(4102f);
            ExpectDamage(behind, 134);
            ExpectDamage(ahead, 126);
            runtime.Tick(4103f);
            ExpectDamage(behind, 204);
            ExpectDamage(ahead, 0);

            actor.transform.position += Vector3.right * 1000f;
            var cometTarget = Spawn("Frost Comet target", 1, actor.transform.position + Vector3.forward * 6f);
            if (!runtime.TryChooseComet())
                throw new InvalidOperationException("Could not select Frost Comet.");
            do
            {
                rolls = UnityEngine.Random.state;
                UnityEngine.Random.Range(0, 1);
                first = UnityEngine.Random.value;
            }
            while (first >= 0.13f);
            UnityEngine.Random.state = rolls;
            runtime.Cast(4103.2f);
            UnityEngine.Random.state = randomState;
            runtime.Tick(4103.6f);
            ExpectDamage(cometTarget, 0);
            runtime.Tick(4103.8f);
            ExpectDamage(cometTarget, 70);
            runtime.Tick(4103.8f);
            ExpectDamage(cometTarget, 0);
            if (!runtime.TryChooseWarrior())
                throw new InvalidOperationException("Could not select Einherjar.");
            do
            {
                rolls = UnityEngine.Random.state;
                UnityEngine.Random.Range(0, 1);
                first = UnityEngine.Random.value;
            }
            while (first >= 0.13f);
            UnityEngine.Random.state = rolls;
            runtime.Cast(4104f);
            UnityEngine.Random.state = randomState;
            runtime.Tick(4104.2f);
            ExpectDamage(cometTarget, 0);
            runtime.Tick(4104.3f);
            ExpectDamage(cometTarget, 50);
            runtime.Tick(4104.3f);
            ExpectDamage(cometTarget, 0);
            cometTarget.transform.position = actor.transform.position + Vector3.forward * 4f;
            if (!runtime.TryChooseKraken())
                throw new InvalidOperationException("Could not select Kraken Touch.");
            do
            {
                rolls = UnityEngine.Random.state;
                first = UnityEngine.Random.value;
            }
            while (first >= 0.13f);
            UnityEngine.Random.state = rolls;
            runtime.Cast(4104.4f);
            UnityEngine.Random.state = randomState;
            runtime.Tick(4104.6f);
            ExpectDamage(cometTarget, 0);
            runtime.Tick(4104.7f);
            ExpectDamage(cometTarget, 56);
            runtime.Tick(4104.7f);
            ExpectDamage(cometTarget, 0);
            cometTarget.transform.position = actor.transform.position + Vector3.forward * 3f;
            if (!runtime.TryChooseShip())
                throw new InvalidOperationException("Could not select Ship Charge.");
            do
            {
                rolls = UnityEngine.Random.state;
                first = UnityEngine.Random.value;
            }
            while (first >= 0.13f);
            UnityEngine.Random.state = rolls;
            runtime.Cast(4104.8f);
            UnityEngine.Random.state = randomState;
            runtime.Tick(4105.4f);
            ExpectDamage(cometTarget, 84);
            runtime.Tick(4105.5f);
            ExpectDamage(cometTarget, 0);
            if (!runtime.TryChooseAura())
                throw new InvalidOperationException("Could not select Divine Aura.");
            do
            {
                rolls = UnityEngine.Random.state;
                first = UnityEngine.Random.value;
            }
            while (first >= 0.13f);
            UnityEngine.Random.state = rolls;
            runtime.Cast(4105.6f);
            UnityEngine.Random.state = randomState;
            ExpectDamage(cometTarget, 22);
            runtime.Cast(4105.6f);
            ExpectDamage(cometTarget, 0);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_ANSUZ_PERSISTENT_OK double contact, damage ticks and explosions");
        }

        private static void ValidatePerth()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7080;
            var actor = Spawn("Perth caster", 0, origin);
            var target = Spawn("Perth non-bot target", 1, origin + Vector3.forward);
            var runtime = new GameObject("Perth validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            var randomState = UnityEngine.Random.state;
            int[] levels = { 1, 8, 15, 22 };
            float[] chance = { 0.05f, 0.10f, 0.15f, 0.25f };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (runtime.TryChoosePerth() || !progress.TryLearn("passive_broksindri_A_evolve") ||
                    !progress.TryLearn("passive_broksindri_B_evolve") || !progress.TryLearn("passive_broksindri_C_evolve")))
                    throw new InvalidOperationException("Perth evolution gate failed.");
                if (!runtime.TryChoosePerth() || runtime.TryChoosePerth())
                    throw new InvalidOperationException("Perth rank or level gate failed.");

                for (int miss = 0; miss < 2; miss++)
                {
                    UnityEngine.Random.State rolls;
                    float roll;
                    float lower = chance[rank] + (miss == 0 ? -0.015f : 0f);
                    do { rolls = UnityEngine.Random.state; roll = UnityEngine.Random.value; }
                    while (roll < lower || roll >= lower + 0.01f);
                    UnityEngine.Random.state = rolls;
                    target.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
                    ExpectDamage(target, miss == 0 ? 200 : 100);
                }
            }
            if (SkillBattlePreview.RollPerthSlay(actor, target))
                throw new InvalidOperationException("Perth could slay a non-bot target.");
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_PERTH_OK four critical thresholds, evolution gate and non-bot exclusion");
        }

        private static void ValidateMannazCooldownReset()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            if (!progress.TryLearn("passive_broksindri_E_evolve") || !progress.TryLearn("passive_broksindri_F_evolve"))
                throw new InvalidOperationException("Could not learn Mannaz and Ansuz evolutions.");
            Vector3 origin = Vector3.right * 7040;
            var actor = Spawn("Mannaz reset caster", 0, origin);
            var classTarget = Spawn("Mannaz reset class target", 1, origin + Vector3.forward * 2f);
            var godTarget = Spawn("Mannaz reset god target", 1, origin + Vector3.right * 3f);
            var runtime = new GameObject("Mannaz cooldown reset validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(22);
            for (int rank = 0; rank < 4; rank++)
                if (!runtime.TryChooseAnsuz() || !runtime.TryChooseMannaz())
                    throw new InvalidOperationException("Could not select the cooldown reset ranks.");
            if (!runtime.TryChooseLightning() || !runtime.TryChooseSkull())
                throw new InvalidOperationException("Could not select the cooldown reset skills.");

            var randomState = UnityEngine.Random.state;
            void SetRoll(bool repeat, bool reset)
            {
                UnityEngine.Random.State rolls;
                float first, second;
                do
                {
                    rolls = UnityEngine.Random.state;
                    first = UnityEngine.Random.value;
                    second = UnityEngine.Random.value;
                }
                while ((first < 0.13f) != repeat || (repeat && (second < 0.2f) != reset));
                UnityEngine.Random.state = rolls;
            }

            SetRoll(repeat: true, reset: false);
            runtime.Cast(4200f);
            ExpectDamage(godTarget, 60);
            ExpectDamage(classTarget, 35);
            runtime.Cast(4200.1f);
            ExpectDamage(godTarget, 0);

            SetRoll(repeat: true, reset: true);
            runtime.Cast(4205f);
            ExpectDamage(godTarget, 60);
            ExpectDamage(classTarget, 35);
            SetRoll(repeat: false, reset: false);
            runtime.Cast(4205.1f);
            ExpectDamage(godTarget, 30);
            ExpectDamage(classTarget, 0);
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_MANNAZ_RESET_OK failed and successful reset rolls, triggering cooldown isolation");
        }

        private static void ValidateRuneDamage(bool godSkill)
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * (godSkill ? 6980 : 6940);
            var actor = Spawn("Rune damage recipient", 0, origin);
            var classTarget = Spawn("Rune class target", 1, origin + Vector3.forward * 2f);
            var godTarget = Spawn("Rune god target", 1, origin + Vector3.right * 3f);
            var runtime = new GameObject("Rune damage validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(3);
            if (!runtime.TryChooseSkull() || !runtime.TryChooseLightning())
                throw new InvalidOperationException("Rune damage validation skills were unavailable.");

            int[] levels = { 3, 8, 15, 22 };
            int[] damage = godSkill ? new[] { 20, 22, 25, 30 } : new[] { 23, 26, 29, 35 };
            string evolution = godSkill ? "passive_broksindri_F_evolve" : "passive_broksindri_E_evolve";
            bool ChooseRune() => godSkill ? runtime.TryChooseAnsuz() : runtime.TryChooseMannaz();
            var randomState = UnityEngine.Random.state;
            void SetAnsuzRoll(bool repeat)
            {
                UnityEngine.Random.State rollState;
                do { rollState = UnityEngine.Random.state; }
                while ((UnityEngine.Random.value < 0.13f) != repeat);
                UnityEngine.Random.state = rollState;
            }

            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (ChooseRune() ||
                    (godSkill && !progress.TryLearn("passive_broksindri_E_evolve")) || !progress.TryLearn(evolution)))
                    throw new InvalidOperationException($"Rune {evolution} evolution gate failed.");
                if (!ChooseRune() || ChooseRune())
                    throw new InvalidOperationException($"Rune {evolution} rank or level gate failed.");

                if (godSkill && rank == 3)
                    SetAnsuzRoll(repeat: false);
                runtime.Cast(4000f + rank * 5f);
                ExpectDamage(classTarget, godSkill ? 20 : damage[rank]);
                ExpectDamage(godTarget, godSkill ? damage[rank] : 17);
                if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100)
                    throw new InvalidOperationException($"Rune {evolution} modified weapon damage.");
            }
            if (godSkill)
            {
                SetAnsuzRoll(repeat: true);
                runtime.Cast(4020f);
                ExpectDamage(godTarget, 60);
                runtime.Cast(4020.1f);
                ExpectDamage(godTarget, 0);

                classTarget.transform.position += Vector3.up * 100f;
                godTarget.transform.position += Vector3.up * 100f;
                var viperTarget = Spawn("Ansuz Vipers target", 1, origin + Vector3.forward * 4f);
                if (!runtime.TryChooseVipers())
                    throw new InvalidOperationException("Ansuz Vipers validation skill was unavailable.");
                SetAnsuzRoll(repeat: true);
                runtime.Cast(4020.2f);
                runtime.Tick(4020.4f);
                ExpectDamage(viperTarget, 0);
                runtime.Tick(4020.51f);
                ExpectDamage(viperTarget, 104);
                runtime.Tick(4021f);
                ExpectDamage(viperTarget, 0);
            }
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            string marker = godSkill ? "JOTUNNSLAYER_ANSUZ_OK" : "JOTUNNSLAYER_MANNAZ_OK";
            Debug.Log($"{marker} four ranks and class, god and weapon damage isolation");
        }

        private static void ValidateTiwaz()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 6900;
            var actor = Spawn("Tiwaz recipient", 0, origin);
            var primary = Spawn("Tiwaz hit", 1, origin + Vector3.forward * 2f);
            var chainTarget = Spawn("Tiwaz chain target", 1, primary.transform.position + Vector3.right * 3f);
            var farTarget = Spawn("Tiwaz distant target", 1, primary.transform.position + Vector3.forward * 7f);
            var ally = Spawn("Tiwaz ally", 0, primary.transform.position + Vector3.forward);
            var runtime = new GameObject("Tiwaz validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            int[] levels = { 1, 8, 15, 22 };
            int[] damage = { 115, 130, 145, 175 };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (runtime.TryChooseTiwaz() || !progress.TryLearn("passive_broksindri_A_evolve")))
                    throw new InvalidOperationException("Tiwaz evolution gate failed.");
                if (!runtime.TryChooseTiwaz() || runtime.TryChooseTiwaz())
                    throw new InvalidOperationException("Tiwaz rank or level gate failed.");

                int shot = SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100);
                primary.Server_ChangeHealthStateRaw(shot, CharacterPart.body, AttackType.hitscan, actor, 0);
                ExpectDamage(primary, damage[rank]);
                ExpectDamage(chainTarget, rank == 3 ? 10 : 0);
                ExpectDamage(ally, 0);
                ExpectDamage(farTarget, 0);
            }

            SkillBattlePreview.OnRaidWeaponHit(actor, primary, Time.time + 1.49f);
            ExpectDamage(chainTarget, 0);
            SkillBattlePreview.OnRaidWeaponHit(actor, primary, Time.time + 1.51f);
            ExpectDamage(chainTarget, 10);
            runtime.Bind(null, progress, null);
            if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100)
                throw new InvalidOperationException("Tiwaz persisted after unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_TIWAZ_OK four weapon ranks, chain targets, cooldown and unbinding");
        }

        private static void ValidateJera()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Jera recipient", 0, Vector3.right * 6700);
            var runtime = new GameObject("Jera validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            int[] levels = { 1, 8, 15, 22 };
            float[] radii = { 4.8f, 5.6f, 6.4f, 8f };
            float now = 3900f;
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (runtime.TryChooseJera() || !progress.TryLearn("passive_broksindri_O_evolve")))
                    throw new InvalidOperationException("Jera evolution gate failed.");
                if (!runtime.TryChooseJera() || runtime.TryChooseJera())
                    throw new InvalidOperationException("Jera rank or level gate failed.");

                actor.transform.position += Vector3.right * 40f;
                Vector3 origin = actor.transform.position;
                runtime.RollMinionGold(origin + Vector3.forward * (radii[rank] - 0.01f), 0f);
                runtime.RollMinionGold(origin + Vector3.forward * (radii[rank] + 0.01f), 0f);
                runtime.Tick(now += 0.25f);
                if (progress.Gold != rank + 1)
                    throw new InvalidOperationException("Jera gold radius failed.");
            }

            actor.transform.position += Vector3.right * 100f;
            runtime.Tick(now += 0.25f);
            for (int step = 1; step <= 30; step++)
            {
                actor.transform.position += Vector3.back * 5f;
                runtime.Tick(now += 0.25f);
                if (progress.Gold != (step == 30 ? 8 : 4))
                    throw new InvalidOperationException("Jera movement threshold or gold magnet failed.");
            }
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_JERA_OK four pickup radii, evolution and 150m gold magnet");
        }

        private static void ValidateKenaz()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Kenaz recipient", 0, Vector3.right * 6600);
            var runtime = new GameObject("Kenaz validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(2);
            if (!runtime.TryChooseKenaz() || runtime.TryChooseKenaz() || runtime.RefreshLiveOffer() != 3 ||
                !runtime.TryBanishLiveReward(0))
                throw new InvalidOperationException("Kenaz first rank did not grant banishes.");
            runtime.RefreshLiveOffer();

            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseKenaz() || runtime.TryChooseKenaz() || runtime.RefreshLiveOffer() != 3 ||
                !runtime.TryRerollLiveOffer())
                throw new InvalidOperationException("Kenaz second rank did not grant rerolls.");
            runtime.RefreshLiveOffer();

            runtime.SetRaidLevel(15);
            if (!runtime.TryChooseKenaz() || runtime.TryChooseKenaz() || runtime.RefreshLiveOffer() != 3 ||
                !runtime.TrySealLiveReward(0))
                throw new InvalidOperationException("Kenaz third rank did not grant seals.");
            runtime.RefreshLiveOffer();

            runtime.SetRaidLevel(22);
            if (runtime.TryChooseKenaz() || !progress.TryLearn("passive_broksindri_O_evolve") ||
                !progress.TryLearn("passive_broksindri_N_evolve") || !progress.TryLearn("passive_broksindri_M_evolve") ||
                !runtime.TryChooseKenaz() || runtime.TryChooseKenaz() || runtime.RefreshLiveOffer() != 3)
                throw new InvalidOperationException("Kenaz evolution gate failed.");

            for (int i = 0; i < 4; i++)
            {
                if (!runtime.TryBanishLiveReward(0))
                    throw new InvalidOperationException("Kenaz evolved banish count was too low.");
                runtime.RefreshLiveOffer();
            }
            for (int i = 0; i < 7; i++)
            {
                if (!runtime.TryRerollLiveOffer())
                    throw new InvalidOperationException("Kenaz evolved reroll count was too low.");
                runtime.RefreshLiveOffer();
            }
            for (int i = 0; i < 3; i++)
            {
                if (!runtime.TrySealLiveReward(0))
                    throw new InvalidOperationException("Kenaz evolved seal capacity was too low.");
                runtime.RefreshLiveOffer();
            }
            if (runtime.TryBanishLiveReward(0) || runtime.TryRerollLiveOffer() || runtime.TrySealLiveReward(0))
                throw new InvalidOperationException("Kenaz evolution restored spent charges or exceeded seal capacity.");

            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_KENAZ_OK four ranks, actual offer operations and preserved spending");
        }

        private static void ValidateLaguz()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 6560;
            var actor = Spawn("Laguz recipient", 0, origin);
            var enemy = Spawn("Laguz enemy", 1, origin + Vector3.forward * 3f);
            var runtime = new GameObject("Laguz validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(2);
            if (!runtime.TryChooseLightning())
                throw new InvalidOperationException("Could not choose Lightning for Laguz validation.");

            int[] levels = { 2, 8, 15, 22 };
            float[] factors = { 0.92f, 0.84f, 0.76f, 0.65f, 0.65f * 0.94f };
            for (int rank = 0; rank < factors.Length; rank++)
            {
                if (rank < levels.Length)
                {
                    runtime.SetRaidLevel(levels[rank]);
                    if (rank == 3 && (runtime.TryChooseLaguz() || !progress.TryLearn("passive_broksindri_G_evolve") ||
                        !progress.TryLearn("passive_broksindri_I_evolve") || !progress.TryLearn("passive_broksindri_H_evolve")))
                        throw new InvalidOperationException("Laguz evolution gate failed.");
                    if (!runtime.TryChooseLaguz() || runtime.TryChooseLaguz())
                        throw new InvalidOperationException("Laguz rank or level gate failed.");
                }
                else if (!progress.TryLearn("passive_njord_I") || !runtime.TryChooseTorrents())
                    throw new InvalidOperationException("Could not choose Torrents for Laguz stacking validation.");

                float castAt = 3800 + 10 * rank;
                float cooldown = 4.25f * factors[rank];
                runtime.Cast(castAt);
                ExpectDamage(enemy, 17);
                runtime.Cast(castAt + cooldown - 0.02f);
                ExpectDamage(enemy, 0);
                runtime.Cast(castAt + cooldown + 0.02f);
                ExpectDamage(enemy, 17);
            }
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_LAGUZ_OK four cooldown ranks, evolution gate and Torrents stacking");
        }

        private static void ValidateUruz()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 6520;
            var actor = Spawn("Uruz recipient", 0, origin);
            var enemy = Spawn("Uruz enemy", 1, origin + Vector3.forward);
            var runtime = new GameObject("Uruz validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            int[] levels = { 1, 8, 15, 22 };
            int[] damage = { 120, 140, 160, 200 };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (runtime.TryChooseUruz() || !progress.TryLearn("passive_broksindri_G_evolve")))
                    throw new InvalidOperationException("Uruz evolution gate failed.");
                if (!runtime.TryChooseUruz() || runtime.TryChooseUruz())
                    throw new InvalidOperationException("Uruz rank or level gate failed.");
                enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
                ExpectDamage(enemy, damage[rank]);
                enemy.ServerApplyRangeDot(100, AttackType.explosion, actor);
                ExpectDamage(enemy, damage[rank]);
            }
            runtime.Bind(null, progress, null);
            enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
            ExpectDamage(enemy, 100);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_URUZ_OK four damage ranks, evolution gate, periodic damage and unbinding");
        }

        private static void ValidateInguz()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_njord_01_a"))
                throw new InvalidOperationException("Could not learn the Inguz critical chance prerequisite.");
            Vector3 origin = Vector3.right * 6480;
            var actor = Spawn("Inguz recipient", 0, origin);
            var enemy = Spawn("Inguz enemy", 1, origin + Vector3.forward);
            var runtime = new GameObject("Inguz validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            actor.CurrentHealth = 800;

            var originalRandom = UnityEngine.Random.state;
            UnityEngine.Random.State winningRoll;
            float criticalRoll, healRoll;
            do
            {
                winningRoll = UnityEngine.Random.state;
                criticalRoll = UnityEngine.Random.value;
                healRoll = UnityEngine.Random.value;
            }
            while (criticalRoll >= 0.02f || healRoll >= 0.3f);

            int[] levels = { 1, 8, 15, 22 };
            int[] damage = { 212, 224, 236, 260 };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (runtime.TryChooseInguz() || !progress.TryLearn("passive_broksindri_E_evolve") ||
                    !progress.TryLearn("passive_broksindri_F_evolve") || !progress.TryLearn("passive_broksindri_D_evolve")))
                    throw new InvalidOperationException("Inguz evolution gate failed.");
                if (!runtime.TryChooseInguz() || runtime.TryChooseInguz())
                    throw new InvalidOperationException("Inguz rank or level gate failed.");

                UnityEngine.Random.state = winningRoll;
                enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
                ExpectDamage(enemy, damage[rank]);
                if (actor.CurrentHealth != (rank == 3 ? 812 : 800))
                    throw new InvalidOperationException("Inguz evolved healing trigger failed.");
            }
            UnityEngine.Random.state = winningRoll;
            enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
            if (actor.CurrentHealth != 812)
                throw new InvalidOperationException("Inguz healed twice during its cooldown.");
            UnityEngine.Random.state = winningRoll;
            SkillBattlePreview.OnRaidCriticalHit(actor, enemy, Time.time + 2f);
            if (actor.CurrentHealth != 824)
                throw new InvalidOperationException("Inguz did not recover after its cooldown.");
            UnityEngine.Random.state = originalRandom;
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_INGUZ_OK four critical damage ranks, evolution healing and cooldown");
        }

        private static void ValidateHagalaz()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 6440;
            var actor = Spawn("Hagalaz recipient", 0, origin);
            var enemy = Spawn("Hagalaz enemy", 1, origin + Vector3.forward);
            var runtime = new GameObject("Hagalaz validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Ranger;
            runtime.Bind(actor, progress, null);
            actor.CurrentHealth = 700;
            int[] levels = { 1, 8, 15, 22 };
            int[] expected = { 1, 2, 3, 7 };
            for (int rank = 0; rank < levels.Length; rank++)
            {
                runtime.SetRaidLevel(levels[rank]);
                if (rank == 3 && (runtime.TryChooseHagalaz() || !progress.TryLearn("passive_broksindri_J_evolve") ||
                    !progress.TryLearn("passive_broksindri_K_evolve") || !progress.TryLearn("passive_broksindri_L_evolve")))
                    throw new InvalidOperationException("Hagalaz evolution gate failed.");
                if (!runtime.TryChooseHagalaz() || runtime.TryChooseHagalaz())
                    throw new InvalidOperationException("Hagalaz rank or level gate failed.");

                int before = actor.CurrentHealth;
                int enemyBefore = enemy.CurrentHealth;
                if (rank == 0)
                {
                    enemy.Server_ChangeHealthStateRaw(50, CharacterPart.body, AttackType.hitscan, actor, 0);
                    if (actor.CurrentHealth != before)
                        throw new InvalidOperationException("Hagalaz rounded a fractional point too early.");
                }
                enemy.Server_ChangeHealthStateRaw(rank == 0 ? 50 : 100, CharacterPart.body, AttackType.hitscan, actor, 0);
                if (actor.CurrentHealth != before + expected[rank])
                    throw new InvalidOperationException($"Hagalaz rank {rank + 1} dealt {enemyBefore - enemy.CurrentHealth} health damage and healed {actor.CurrentHealth - before} HP, expected {expected[rank]} HP.");
            }

            int beforeOverkill = actor.CurrentHealth;
            enemy.CurrentHealth = 20;
            enemy.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.hitscan, actor, 0);
            if (actor.CurrentHealth != beforeOverkill + 1 || actor.ServerHeal(50, actor.netId) != 65)
                throw new InvalidOperationException("Hagalaz overkill or evolved healing scaling failed.");

            runtime.Bind(null, progress, null);
            if (actor.ServerHeal(50, actor.netId) != 50)
                throw new InvalidOperationException("Hagalaz healing persisted after unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_HAGALAZ_OK four ranks, fractional life steal, overkill and healing power");
        }

        private static void ValidateRaido()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Raido recipient", 0, Vector3.right * 6400);
            var runtime = new GameObject("Raido validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            int[] levels = { 1, 8, 15 };
            for (int i = 0; i < levels.Length; i++)
            {
                runtime.SetRaidLevel(levels[i]);
                if (!runtime.TryChooseRaido() || runtime.TryChooseRaido() ||
                    !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1f + (i + 1) * 0.08f))
                    throw new InvalidOperationException("Raido base rank or level gate failed.");
            }
            runtime.SetRaidLevel(22);
            if (runtime.TryChooseRaido() || !progress.TryLearn("passive_broksindri_A_evolve") ||
                !progress.TryLearn("passive_broksindri_B_evolve") || !runtime.TryChooseRaido())
                throw new InvalidOperationException("Raido evolution unlock gate failed.");
            if (!Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.4f))
                throw new InvalidOperationException("Raido evolved attack speed failed.");
            if (!runtime.TryStartRaidDash(Vector3.forward, 3700f) ||
                runtime.TryStartRaidDash(Vector3.forward, 3703.39f) ||
                !runtime.TryStartRaidDash(Vector3.forward, 3703.41f))
                throw new InvalidOperationException("Raido evolved dash cooldown failed.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_RAIDO_OK attack speed, evolution gate and dash cooldown");
        }

        private static void ValidateLokiPassives()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            foreach (char rank in "abcd")
                if (!progress.TryLearn($"virtue_loki_01_{rank}"))
                    throw new InvalidOperationException("Could not learn Loki movement for Shapeshifter.");

            var actor = Spawn("Loki passive recipient", 0, Vector3.right * 6360);
            var runtime = new GameObject("Loki passive validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(2);
            if (runtime.TryChooseShapeshifter() || runtime.TryChooseRippedLottery() ||
                !progress.TryLearn("passive_loki_H") || !progress.TryLearn("passive_loki_I") ||
                !runtime.TryChooseShapeshifter() ||
                !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.0375f))
                throw new InvalidOperationException("Loki passive unlocks or first attack speed rank failed.");

            var randomState = UnityEngine.Random.state;
            int[] levels = { 2, 8, 15 };
            int[] gold = { 10, 10, 40 };
            for (int i = 0; i < levels.Length; i++)
            {
                runtime.SetRaidLevel(levels[i]);
                UnityEngine.Random.State rollState;
                do { rollState = UnityEngine.Random.state; }
                while ((UnityEngine.Random.value < 0.05f) != (i == 1));
                UnityEngine.Random.state = rollState;
                if (!runtime.TryChooseRippedLottery() || progress.Gold != gold[i] || runtime.TryChooseRippedLottery())
                    throw new InvalidOperationException("Ripped Lottery payout, loss or rank gate failed.");
                if (i == 1 && (!runtime.TryChooseShapeshifter() || runtime.TryChooseShapeshifter() ||
                    !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.075f)))
                    throw new InvalidOperationException("Shapeshifter second rank failed.");
            }
            UnityEngine.Random.state = randomState;
            runtime.Bind(null, progress, null);
            if (SkillBattlePreview.RaidAttackSpeedMultiplier(actor) != 1f)
                throw new InvalidOperationException("Shapeshifter persisted after unbinding.");
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_LOKI_PASSIVES_OK movement conversion, lottery payout and loss, rank gates and unbinding");
        }

        private static void ValidateDoubleRewardVirtue(string prefix, float chancePerRank)
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            var actor = Spawn("Yule Father recipient", 0, Vector3.right * 5840);
            var victim = Spawn("Yule Father target", 1, actor.transform.position + Vector3.forward);
            for (int rank = 1; rank <= 2; rank++)
            {
                if (!progress.TryLearn(prefix + (rank == 1 ? "a" : "b")))
                    throw new InvalidOperationException($"Could not learn double reward virtue {prefix}.");

                var runtime = new GameObject("Yule Father validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.Bind(actor, progress, null);
                int expected = 0;
                foreach (float minRoll in new[] { chancePerRank * 0.5f, chancePerRank * 1.5f, chancePerRank * 2.5f })
                {
                    var randomState = UnityEngine.Random.state;
                    UnityEngine.Random.State rollState;
                    float roll;
                    do
                    {
                        rollState = UnityEngine.Random.state;
                        roll = UnityEngine.Random.value;
                    }
                    while (roll < minRoll || roll >= minRoll + 0.01f);

                    runtime.SetRaidLevel(1);
                    for (int kill = 0; kill < 10 && runtime.RaidLevel == 1; kill++)
                    {
                        UnityEngine.Random.state = rollState;
                        actor.Server_KilledCharacter?.Invoke(victim);
                    }
                    UnityEngine.Random.state = randomState;
                    if (minRoll < rank * chancePerRank)
                        expected++;
                    if (runtime.RaidLevel != 2 || runtime.BonusRewards != expected)
                        throw new InvalidOperationException($"Double reward virtue {prefix} did not grant one reward at the correct threshold.");
                }
                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            string marker = prefix == "virtue_odin_03_" ? "JOTUNNSLAYER_YULE_FATHER_OK" : "JOTUNNSLAYER_WIGGLE_ROOM_OK";
            Debug.Log($"{marker} two thresholds and one extra choice per level");
        }

        private static void ValidateOdinCombatPassives()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            var actor = Spawn("Odin passive recipient", 0, Vector3.right * 5800);
            int[] braveryDamage = { 103, 106, 111, 120 };
            for (int i = 0; i < braveryDamage.Length; i++)
            {
                if (!progress.TryLearn($"virtue_odin_01_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn Bravery.");

                var runtime = new GameObject("Odin passive validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.SelectedRaidClass = RaidClass.Warden;
                runtime.Bind(actor, progress, null);
                if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != braveryDamage[i])
                    throw new InvalidOperationException("Bravery damage tier failed.");

                if (i == braveryDamage.Length - 1)
                {
                    runtime.SetRaidLevel(2);
                    if (runtime.TryChooseOlderThanTime() || runtime.TryChooseSleipnir() ||
                        !progress.TryLearn("passive_odin_H") || !progress.TryLearn("passive_odin_I") ||
                        !runtime.TryChooseOlderThanTime() || !runtime.TryChooseSleipnir() ||
                        actor.MaxHealth != 1075 || !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.1f))
                        throw new InvalidOperationException("Odin passive unlock or first ranks failed.");

                    runtime.SetRaidLevel(7);
                    if (runtime.TryChooseOlderThanTime() || runtime.TryChooseSleipnir())
                        throw new InvalidOperationException("Odin second rank unlocked early.");
                    runtime.SetRaidLevel(8);
                    if (!runtime.TryChooseOlderThanTime() || !runtime.TryChooseSleipnir() ||
                        runtime.TryChooseOlderThanTime() || runtime.TryChooseSleipnir() ||
                        actor.MaxHealth != 1150 || !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.2f))
                        throw new InvalidOperationException("Odin second ranks failed.");
                    runtime.SetRaidLevel(15);
                    if (!runtime.TryChooseSleipnir() || runtime.TryChooseSleipnir() ||
                        !Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), 1.3f))
                        throw new InvalidOperationException("Sleipnir's third rank failed.");
                }

                runtime.Bind(null, progress, null);
                if (actor.MaxHealth != 1000 || SkillBattlePreview.RaidAttackSpeedMultiplier(actor) != 1f ||
                    SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100)
                    throw new InvalidOperationException("Odin passive stats persisted after unbinding.");
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            Debug.Log("JOTUNNSLAYER_ODIN_COMBAT_PASSIVES_OK Bravery, Older than Time, Sleipnir and unbinding");
        }

        private static void ValidateRaidWeaponDamage()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var actor = Spawn("Raid weapon damage caster", 0, Vector3.right * 4300);
            int[] expected = { 105, 110, 120, 135 };
            for (int i = 0; i < expected.Length; i++)
            {
                if (!progress.TryLearn($"virtue_berserker_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn the Berserker damage virtue.");
                var berserker = new GameObject("Berserker weapon validation").AddComponent<SkillBattlePreview>();
                berserker.LiveMatch = true;
                berserker.Bind(actor, progress, null);
                if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != expected[i] || berserker.TryChooseFocusedShot())
                    throw new InvalidOperationException("Berserker damage or Ranger class gate failed.");
                berserker.Bind(null, progress, null);
                UnityEngine.Object.Destroy(berserker.gameObject);
            }

            if (!progress.TryLearn("virtue_ranger_04_a"))
                throw new InvalidOperationException("Could not learn Wild Heart.");
            var ranger = new GameObject("Ranger weapon validation").AddComponent<SkillBattlePreview>();
            ranger.LiveMatch = true;
            ranger.SelectedRaidClass = RaidClass.Ranger;
            ranger.Bind(actor, progress, null);
            if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 110 || ranger.TryChooseFocusedShot() ||
                !progress.TryLearn("passive_ranger_O") || !ranger.TryChooseFocusedShot() ||
                SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 125)
                throw new InvalidOperationException("Wild Heart or Focused Shot rank one failed.");
            ranger.SetRaidLevel(7);
            if (ranger.TryChooseFocusedShot())
                throw new InvalidOperationException("Focused Shot rank two was offered below level eight.");
            ranger.SetRaidLevel(8);
            if (!ranger.TryChooseFocusedShot() || SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 140)
                throw new InvalidOperationException("Focused Shot rank two damage failed.");
            ranger.Bind(null, progress, null);
            UnityEngine.Object.Destroy(ranger.gameObject);
            if (SkillBattlePreview.ScaleRaidWeaponDamage(actor, 100) != 100)
                throw new InvalidOperationException("Weapon damage persisted after unbind.");
            Debug.Log("JOTUNNSLAYER_WEAPON_DAMAGE_OK permanent ranks, class gates, Focused Shot and unbind");
        }

        private static void ValidateRaidAttackSpeed()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 40);
            var actor = Spawn("Raid attack speed caster", 0, Vector3.right * 4240);
            float[] shared = { 1.035f, 1.07f, 1.14f, 1.25f };
            for (int i = 0; i < shared.Length; i++)
            {
                char rank = (char)('a' + i);
                if (!progress.TryLearn($"virtue_freya_01_{rank}") || !progress.TryLearn($"virtue_ranger_01_{rank}"))
                    throw new InvalidOperationException("Could not learn the attack speed virtues.");
                ValidateAttackSpeedMultiplier(actor, progress, RaidClass.Berserker, shared[i]);
                ValidateAttackSpeedMultiplier(actor, progress, RaidClass.Ranger, 2f * shared[i] - 1f);
            }

            float[] warden = { 1.33f, 1.41f, 1.57f };
            for (int i = 0; i < warden.Length; i++)
            {
                if (!progress.TryLearn($"virtue_warden_03_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn the Warden attack speed virtue.");
                ValidateAttackSpeedMultiplier(actor, progress, RaidClass.Warden, warden[i]);
            }
            Debug.Log("JOTUNNSLAYER_ATTACK_SPEED_OK eleven ranks, class gating, stacking and unbind");
        }

        private static void ValidateAttackSpeedRefund()
        {
            string[] prefixes = { "virtue_freya_01_", "virtue_ranger_01_", "virtue_warden_03_" };
            RaidClass[] classes = { RaidClass.Berserker, RaidClass.Ranger, RaidClass.Warden };
            float[][] speeds = { new[] { 1.035f, 1.07f, 1.14f, 1.25f },
                new[] { 1.035f, 1.07f, 1.14f, 1.25f }, new[] { 1.08f, 1.16f, 1.32f } };
            for (int group = 0; group < prefixes.Length; group++)
                for (int rank = 0; rank < speeds[group].Length; rank++)
                {
                    var profile = new SkillProgress(SkillCatalog.Load(), null, 40);
                    for (int learned = 0; learned <= rank; learned++)
                        if (!profile.TryLearn(prefixes[group] + (char)('a' + learned)))
                            throw new InvalidOperationException("Attack speed refund profile unavailable.");
                    var actor = Spawn("Attack speed refund caster", 0, Vector3.right * (34000 + group * 300 + rank * 30));
                    var runtime = new GameObject("Attack speed refund").AddComponent<SkillBattlePreview>();
                    runtime.LiveMatch = true;
                    runtime.SelectedRaidClass = classes[group];
                    runtime.Bind(actor, profile, null);
                    int level = runtime.RaidLevel, choices = runtime.PendingRewards;
                    for (int phase = 0; phase < 3; phase++)
                    {
                        if (phase == 1) profile.Reset();
                        if (phase == 2)
                            for (int learned = 0; learned <= rank; learned++)
                                if (!profile.TryLearn(prefixes[group] + (char)('a' + learned)))
                                    throw new InvalidOperationException("Attack speed refund relearn unavailable.");
                        float expected = phase == 1 ? 1f : speeds[group][rank];
                        if (!Mathf.Approximately(SkillBattlePreview.RaidAttackSpeedMultiplier(actor), expected) ||
                            !Mathf.Approximately(runtime.BuildRemoteSnapshot().attackSpeed, expected) ||
                            runtime.RaidLevel != level || runtime.PendingRewards != choices)
                            throw new InvalidOperationException("Attack speed refund/snapshot or raid preservation failed.");
                    }
                    runtime.Bind(null, profile, null);
                    if (SkillBattlePreview.RaidAttackSpeedMultiplier(actor) != 1)
                        throw new InvalidOperationException("Attack speed refund unbind retained stats.");
                    UnityEngine.Object.Destroy(runtime.gameObject);
                }
            Debug.Log("JOTUNNSLAYER_ATTACK_SPEED_REFUND_OK eleven ranks, same-caster refund/relearn, snapshots and unbind");
        }

        private static void ValidateAttackSpeedMultiplier(Health actor, SkillProgress progress, RaidClass raidClass,
            float expected)
        {
            var runtime = new GameObject("Raid attack speed validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = raidClass;
            runtime.Bind(actor, progress, null);
            float actual = SkillBattlePreview.RaidAttackSpeedMultiplier(actor);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            if (!Mathf.Approximately(actual, expected) || SkillBattlePreview.RaidAttackSpeedMultiplier(actor) != 1f)
                throw new InvalidOperationException("Raid attack speed multiplier or unbind was incorrect.");
        }

        private static void ValidatePermanentDivineRefund()
        {
            for (int kind = 0; kind < 2; kind++)
            {
                bool allDamage = kind == 1;
                string prefix = allDamage ? "virtue_thor_02_" : "virtue_nidhogg_02_";
                int[] godDamage = allDamage ? new[] { 18, 18, 19, 21 } : new[] { 18, 19, 21 };
                int[] classDamage = allDamage ? new[] { 21, 21, 23, 25 } : new[] { 20, 20, 20 };
                int[] weaponDamage = allDamage ? new[] { 207, 214, 228, 250 } : new[] { 200, 200, 200 };
                for (int rank = 0; rank < godDamage.Length; rank++)
                {
                    var profile = new SkillProgress(SkillCatalog.Load(), null, 20);
                    for (int learned = 0; learned <= rank; learned++)
                        if (!profile.TryLearn(prefix + (char)('a' + learned)))
                            throw new InvalidOperationException("Could not learn divine damage refund profile.");
                    var runtime = new GameObject("Divine damage refund").AddComponent<SkillBattlePreview>();
                    runtime.LiveMatch = true;
                    runtime.SelectedRaidClass = RaidClass.Ranger;
                    Vector3 origin = Vector3.right * (33000 + kind * 300 + rank * 40);
                    var actor = Spawn("Divine refund caster", 0, origin);
                    var target = Spawn("Divine refund class target", 1, origin + Vector3.forward * 2);
                    var god = Spawn("Divine refund god target", 1, origin + Vector3.right * 3);
                    runtime.Bind(actor, profile, null);
                    runtime.SetRaidLevel(2);
                    if (!runtime.TryChooseSkull() || !runtime.TryChooseLightning())
                        throw new InvalidOperationException("Could not choose divine refund skills.");
                    int level = runtime.RaidLevel, pending = runtime.PendingRewards;
                    for (int phase = 0; phase < 3; phase++)
                    {
                        if (phase == 1) profile.Reset();
                        if (phase == 2)
                            for (int learned = 0; learned <= rank; learned++)
                                if (!profile.TryLearn(prefix + (char)('a' + learned)))
                                    throw new InvalidOperationException("Could not relearn divine damage profile.");
                        target.Server_ChangeHealthStateRaw(200, CharacterPart.body, AttackType.hitscan, actor, 0);
                        ExpectDamage(target, phase == 1 ? 200 : weaponDamage[rank]);
                        runtime.Cast(5000 + phase * 10);
                        ExpectDamage(target, phase == 1 ? 20 : classDamage[rank]);
                        ExpectDamage(god, phase == 1 ? 17 : godDamage[rank]);
                        actor.Server_ChangeHealthStateRaw(100, CharacterPart.body, AttackType.falldamage, actor, 0);
                        ExpectDamage(actor, 100);
                        if (runtime.RaidLevel != level || runtime.PendingRewards != pending)
                            throw new InvalidOperationException("Divine refund changed raid progression.");
                    }
                    runtime.Bind(null, profile, null);
                    UnityEngine.Object.Destroy(runtime.gameObject);
                }
            }
            Debug.Log("JOTUNNSLAYER_DIVINE_DAMAGE_REFUND_OK Nidhogg three and Thor four ranks, same-caster refund/relearn, weapon/class/god scopes, fall isolation and raid preservation");
        }

        private static void ValidateGodDamageBonus()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("God Damage validation").AddComponent<SkillBattlePreview>();
            int[] expected = { 18, 19, 21 };
            for (int i = 0; i < expected.Length; i++)
            {
                if (!progress.TryLearn($"virtue_nidhogg_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn the God Damage virtue.");

                Vector3 origin = Vector3.right * (4140 + 30 * i);
                var actor = Spawn("God Damage caster", 0, origin);
                var godTarget = Spawn("God Damage lightning target", 1, origin + Vector3.right * 3f);
                var classTarget = Spawn("God Damage assault target", 1, origin + Vector3.forward * 4.5f);
                runtime.Bind(actor, progress, null);
                runtime.SetRaidLevel(2);
                if (!runtime.TryChooseLightning() || !runtime.TryChooseAssault())
                    throw new InvalidOperationException("Could not select God Damage validation skills.");
                runtime.Cast(2700);
                ExpectDamage(godTarget, expected[i]);
                ExpectDamage(classTarget, 20);
            }
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_GOD_DAMAGE_OK three ranks and class damage isolation");
        }

        private static void ValidateDivineRebirth()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_valkyrie_04_a") || !progress.TryLearn("virtue_freya_04_a"))
                throw new InvalidOperationException("Could not learn the divine rebirth and revive nodes.");

            ValidateDivineRebirth(progress, RaidClass.Valkyrie, 120);
            ValidateDivineRebirth(progress, RaidClass.Warden, 0);
            Debug.Log("JOTUNNSLAYER_DIVINE_REBIRTH_OK lethal intercept, deferred damage, radius and class gate");
        }

        private static void ValidateDivineRebirth(SkillProgress progress, RaidClass raidClass, int expectedDamage)
        {
            var runtime = new GameObject("Divine Rebirth validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = raidClass;
            Vector3 origin = Vector3.right * (raidClass == RaidClass.Valkyrie ? 4040 : 4080);
            var actor = Spawn("Divine Rebirth caster", 0, origin);
            var inner = Spawn("Divine Rebirth attacker", 1, origin + Vector3.forward * 8f);
            var outer = Spawn("Divine Rebirth outer target", 1, origin + Vector3.forward * 8.1f);
            var ally = Spawn("Divine Rebirth ally", 0, origin + Vector3.right);
            runtime.Bind(actor, progress, null);
            actor.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, inner, 0);
            if (actor.CurrentHealth != actor.MaxHealth)
                throw new InvalidOperationException("Divine Rebirth validation did not revive its caster.");
            ExpectDamage(inner, 0);
            runtime.Tick(2600);
            ExpectDamage(inner, expectedDamage);
            ExpectDamage(outer, 0);
            ExpectDamage(ally, 0);
            runtime.Tick(2601);
            ExpectDamage(inner, 0);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
        }

        private static void ValidateFleetFooted()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_loki_01_a"))
                throw new InvalidOperationException("Could not learn Loki movement.");
            var runtime = new GameObject("Fleet Footed validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Rogue;
            Vector3 origin = Vector3.right * 3980;
            var actor = Spawn("Fleet Footed caster", 0, origin);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseFleetFooted() || !progress.TryLearn("passive_rogue_O") ||
                !runtime.TryChooseFleetFooted() || !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.12f))
                throw new InvalidOperationException("Fleet Footed unlock or first rank movement failed.");

            runtime.SetRaidLevel(7);
            if (runtime.TryChooseFleetFooted())
                throw new InvalidOperationException("Fleet Footed rank two was offered below level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseFleetFooted() || runtime.TryChooseFleetFooted() ||
                !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(actor), 1.20f))
                throw new InvalidOperationException("Fleet Footed rank two or rank cap failed.");

            var replacement = Spawn("Fleet Footed replacement", 0, origin);
            runtime.Bind(replacement, progress, null);
            if (SkillBattlePreview.RaidMoveMultiplier(actor) != 1f ||
                !Mathf.Approximately(SkillBattlePreview.RaidMoveMultiplier(replacement), 1.20f))
                throw new InvalidOperationException("Fleet Footed did not migrate to the replacement character.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_FLEET_FOOTED_OK two ranks, level gate, Loki stacking and rebind");
        }

        private static void ValidateSpearhead()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            var runtime = new GameObject("Spearhead validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Valkyrie;
            Vector3 origin = Vector3.right * 3920;
            var actor = Spawn("Spearhead caster", 0, origin);
            var chargeTarget = Spawn("Spearhead charge target", 1, origin + Vector3.forward * 7f);
            var godTarget = Spawn("Spearhead god target", 1, origin + Vector3.right * 3f);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseSpearhead() || !progress.TryLearn("passive_valkyrie_O") ||
                !runtime.TryChooseSpearhead() || !progress.TryLearn(SkillBattlePreview.ValkyrieChargeUnlock))
                throw new InvalidOperationException("Spearhead permanent unlock gate failed.");

            runtime.SetRaidLevel(7);
            if (runtime.TryChooseSpearhead() || !runtime.TryChooseValkyrieCharge())
                throw new InvalidOperationException("Spearhead rank two was offered below level eight.");
            runtime.Cast(2500);
            runtime.Tick(2501);
            ExpectDamage(chargeTarget, 63);

            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseSpearhead() || runtime.TryChooseSpearhead() || !runtime.TryChooseLightning())
                throw new InvalidOperationException("Spearhead rank cap or Lightning Strike selection failed.");
            runtime.Cast(2512);
            runtime.Tick(2513);
            // Soulmate has just locked a target. Its damage lands after the separate windup.
            ExpectDamage(chargeTarget, 75);
            ExpectDamage(godTarget, 17);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_SPEARHEAD_OK two ranks, level gate, charge damage and god exclusion");
        }

        private static void ValidateDivination()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 10);
            var runtime = new GameObject("Divination validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            Vector3 origin = Vector3.right * 3860;
            var actor = Spawn("Divination caster", 0, origin);
            var victim = Spawn("Divination target", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            if (runtime.TryChooseDivination() || !progress.TryLearn("passive_seer_O") || !runtime.TryChooseDivination())
                throw new InvalidOperationException("Divination unlock gate failed.");
            actor.Server_KilledCharacter?.Invoke(victim);
            if (runtime.RaidExperience != 40)
                throw new InvalidOperationException("Divination rank one experience was incorrect.");
            runtime.SetRaidLevel(7);
            if (runtime.TryChooseDivination())
                throw new InvalidOperationException("Divination rank two was offered below level eight.");
            runtime.SetRaidLevel(8);
            if (!runtime.TryChooseDivination() || runtime.TryChooseDivination())
                throw new InvalidOperationException("Divination rank two or maximum rank failed.");

            long before = runtime.RaidExperience;
            actor.Server_KilledCharacter?.Invoke(victim);
            actor.Server_KilledCharacter?.Invoke(victim);
            if (runtime.RaidExperience - before != 91)
                throw new InvalidOperationException("Divination rank two lost fractional experience.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_DIVINATION_OK permanent gate, level eight, two ranks and fractional experience");
        }

        private static void ValidateRewardSeals()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_skadi_04_a") || !progress.TryLearn("virtue_skadi_04_b"))
                throw new InvalidOperationException("Could not learn the seal nodes.");

            var runtime = new GameObject("Reward seal validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            var actor = Spawn("Reward seal caster", 0, Vector3.right * 3800);
            runtime.Bind(actor, progress, null);
            for (int i = 0; i < 3; i++)
            {
                if (runtime.RefreshLiveOffer() != 3)
                    throw new InvalidOperationException("Seal validation needs three skill choices.");
                string sealedLabel = runtime.LiveRewardLabel(0);
                if (!runtime.TrySealLiveReward(0) || runtime.TryAcceptLiveReward(0) ||
                    runtime.RefreshLiveOffer() != 3)
                    throw new InvalidOperationException("Seal did not replace the pending candidate.");
                var sealedSnapshot = runtime.BuildRemoteSnapshot();
                if (sealedSnapshot.sealCapacity != 3 || sealedSnapshot.sealedLabels.Length != i + 1 ||
                    Array.IndexOf(sealedSnapshot.sealedLabels, sealedLabel) < 0)
                    throw new InvalidOperationException("Sealed snapshot lost its god-qualified label or capacity.");
                for (int j = 0; j < 3; j++)
                {
                    if (runtime.LiveRewardLabel(j) == sealedLabel)
                        throw new InvalidOperationException("A sealed candidate returned to the offer.");
                }
            }

            runtime.Bind(null, progress, null);
            runtime.Bind(actor, progress, null);
            if (runtime.TrySealLiveReward(0) || !runtime.TryUnsealLiveReward(0) ||
                runtime.RefreshLiveOffer() != 3 || !runtime.TrySealLiveReward(0) ||
                runtime.RefreshLiveOffer() != 3 || !runtime.TryAcceptLiveReward(0))
                throw new InvalidOperationException("Seal capacity, rebind or unseal did not preserve the reward.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_REWARD_SEALS_OK capacity, replacement, rebind and unseal");
        }

        private static void ValidateFreyaRevive()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_freya_04_a") || !progress.TryLearn(SkillBattlePreview.RegenerationEvolution))
                throw new InvalidOperationException("Could not learn the revive nodes.");

            var runtime = new GameObject("Freya revive validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            Vector3 origin = Vector3.right * 3740;
            var actor = Spawn("Freya revive caster", 0, origin);
            var enemy = Spawn("Freya revive attacker", 1, origin + Vector3.forward);
            runtime.Bind(actor, progress, null);
            runtime.SetRaidLevel(22);
            for (int i = 0; i < 4; i++)
            {
                if (!runtime.TryChooseRegenerationRune())
                    throw new InvalidOperationException("Could not learn the raid revive rune.");
            }

            actor.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, enemy, 0);
            if (actor.CurrentHealth != actor.MaxHealth)
                throw new InvalidOperationException("Freya's revive did not intercept lethal damage.");

            var replacement = Spawn("Freya revive replacement", 0, origin);
            runtime.Bind(replacement, progress, null);
            replacement.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, enemy, 0);
            if (replacement.CurrentHealth != replacement.MaxHealth)
                throw new InvalidOperationException("The unspent rune revive did not migrate after Freya's revive.");

            replacement.Server_ChangeHealthStateRaw(1000, CharacterPart.body, AttackType.hitscan, enemy, 0);
            if (replacement.CurrentHealth != 0)
                throw new InvalidOperationException("A third lethal hit unexpectedly revived the caster.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_FREYA_REVIVE_OK lethal damage, rune stacking, rebind and charge exhaustion");
        }

        private static void ValidateRewardBanishes()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            if (!progress.TryLearn("virtue_seer_03_a") || !progress.TryLearn("virtue_seer_03_b"))
                throw new InvalidOperationException("Could not learn the banish virtues.");

            var runtime = new GameObject("Reward banish validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Seeress;
            var actor = Spawn("Reward banish caster", 0, Vector3.right * 3680);
            runtime.Bind(actor, progress, null);
            if (runtime.RefreshLiveOffer() != 3)
                throw new InvalidOperationException("Banish validation needs three skill choices.");

            string banned = runtime.LiveRewardLabel(0);
            string retained = runtime.LiveRewardLabel(1);
            if (!runtime.TryBanishLiveReward(0) || runtime.TryBanishLiveReward(1) || runtime.TryAcceptLiveReward(0))
                throw new InvalidOperationException("Banish did not invalidate the previous candidate list.");

            runtime.Bind(null, progress, null);
            runtime.Bind(actor, progress, null);
            if (runtime.RefreshLiveOffer() != 3 || runtime.LiveRewardLabel(0) != retained ||
                !runtime.TryBanishLiveReward(0) || runtime.RefreshLiveOffer() != 3 || runtime.TryBanishLiveReward(0))
                throw new InvalidOperationException("Banish did not preserve other choices or its remaining charge count.");
            if (!runtime.TryRerollLiveOffer() || runtime.RefreshLiveOffer() != 3)
                throw new InvalidOperationException("Banish interfered with reroll availability.");
            for (int i = 0; i < 3; i++)
            {
                if (runtime.LiveRewardLabel(i) == banned || runtime.LiveRewardLabel(i) == retained)
                    throw new InvalidOperationException("A banished family returned in a later offer.");
            }

            if (!runtime.TryAcceptLiveReward(0))
                throw new InvalidOperationException("Banish consumed the pending reward.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_REWARD_BANISH_OK two charges, rebind, exclusion and reward preservation");
        }

        private static void ValidateRewardRerolls()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 40);
            foreach (string node in new[] { "virtue_skadi_02_a", "virtue_skadi_02_b", "virtue_skadi_02_c",
                "virtue_seer_03_a", "virtue_seer_03_b" })
            {
                if (!progress.TryLearn(node))
                    throw new InvalidOperationException("Could not learn the reroll virtue.");
            }

            ValidateRewardRerolls(progress, RaidClass.Seeress, 13);
            ValidateRewardRerolls(progress, RaidClass.Warden, 7);
            Debug.Log("JOTUNNSLAYER_REWARD_REROLLS_OK charges, class gate, rebind and reward preservation");
        }

        private static void ValidateRewardRerolls(SkillProgress progress, RaidClass raidClass, int charges)
        {
            var runtime = new GameObject("Reward reroll validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = raidClass;
            var actor = Spawn("Reward reroll caster", 0, Vector3.right * 3620);
            runtime.Bind(actor, progress, null);
            for (int i = 0; i < charges; i++)
            {
                if (runtime.RefreshLiveOffer() != 3 || !runtime.TryRerollLiveOffer() ||
                    runtime.TryRerollLiveOffer() || runtime.TryAcceptLiveReward(0))
                    throw new InvalidOperationException("Could not reroll the current offer exactly once.");

                if (i == 0)
                {
                    runtime.Bind(null, progress, null);
                    runtime.Bind(actor, progress, null);
                }
            }

            if (runtime.RefreshLiveOffer() != 3 || runtime.TryRerollLiveOffer() || !runtime.TryAcceptLiveReward(0))
                throw new InvalidOperationException("Reroll budget or original reward was incorrect.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
        }

        private static void ValidatePermanentCooldown()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 40);
            for (char rank = 'a'; rank <= 'e'; rank++)
            {
                if ((rank != 'd' && !progress.TryLearn($"virtue_skadi_01_{rank}")) ||
                    (rank <= 'd' && !progress.TryLearn($"virtue_seer_02_{rank}")))
                    throw new InvalidOperationException("Could not learn the cooldown virtue.");
            }

            Vector3 origin = Vector3.right * 3560;
            var actor = Spawn("Cooldown virtue caster", 0, origin);
            var target = Spawn("Cooldown virtue target", 1, origin + Vector3.forward * 3f);
            ValidateLightningCooldown(actor, target, progress, RaidClass.Warden, 4.25f * 0.875f);
            if (!progress.TryLearn("virtue_skadi_01_d"))
                throw new InvalidOperationException("Could not learn the other Skadi cooldown branch.");
            ValidateLightningCooldown(actor, target, progress, RaidClass.Seeress, 3.6125f);
            ValidateLightningCooldown(actor, target, progress, RaidClass.Warden, 3.6125f);
            Debug.Log("JOTUNNSLAYER_PERMANENT_COOLDOWN_OK Skadi branches and separation from Seeress ward recharge");
        }

        private static void ValidateLightningCooldown(Health actor, Health target, SkillProgress progress,
            RaidClass raidClass, float cooldown)
        {
            var runtime = new GameObject("Cooldown validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = raidClass;
            runtime.Bind(actor, progress, null);
            if (!runtime.TryChooseLightning())
                throw new InvalidOperationException("Could not select the cooldown validation skill.");

            runtime.Cast(2400);
            ExpectDamage(target, 17);
            runtime.Cast(2400 + cooldown - 0.01f);
            ExpectDamage(target, 0);
            runtime.Cast(2400 + cooldown + 0.01f);
            ExpectDamage(target, 17);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
        }

        private static void ValidateRulebreaker()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            if (!progress.TryLearn("virtue_skadi_02_a"))
                throw new InvalidOperationException("Could not learn the Rulebreaker test reroll.");

            var actor = Spawn("Rulebreaker caster", 0, Vector3.right * 6240);
            var randomState = UnityEngine.Random.state;
            for (int rank = 1; rank <= 4; rank++)
            {
                if (!progress.TryLearn($"virtue_nidhogg_03_{(char)('a' + rank - 1)}"))
                    throw new InvalidOperationException("Could not learn Rulebreaker.");

                var runtime = new GameObject("Rulebreaker validation").AddComponent<SkillBattlePreview>();
                runtime.LiveMatch = true;
                runtime.Bind(actor, progress, null);
                runtime.SetRaidLevel(2);
                float roll;
                UnityEngine.Random.State selectedRoll;
                do
                {
                    selectedRoll = UnityEngine.Random.state;
                    roll = UnityEngine.Random.value;
                }
                while (roll < rank * 0.1f - 0.05f || roll >= rank * 0.1f);

                UnityEngine.Random.state = selectedRoll;
                if (runtime.RefreshLiveOffer() != 4 || !runtime.TryRerollLiveOffer())
                    throw new InvalidOperationException("Rulebreaker did not add a fourth skill at its rank threshold.");

                do
                {
                    selectedRoll = UnityEngine.Random.state;
                    roll = UnityEngine.Random.value;
                }
                while (roll < 0.4f);

                UnityEngine.Random.state = selectedRoll;
                if (runtime.RefreshLiveOffer() != 4 || !runtime.TryAcceptLiveReward(3) ||
                    runtime.TryAcceptLiveReward(0) || runtime.RefreshLiveOffer() != 3)
                    throw new InvalidOperationException("Rulebreaker rerolled its limit or granted an extra selection.");

                runtime.Bind(null, progress, null);
                UnityEngine.Object.Destroy(runtime.gameObject);
            }
            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_RULEBREAKER_OK four thresholds, reroll persistence and single acceptance");
        }

        private static void ValidateRewardFallbacks()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 10);
            var runtime = new GameObject("Reward fallback validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            var actor = Spawn("Reward fallback caster", 0, Vector3.right * 3500);
            runtime.Bind(actor, progress, null);
            // Force an exhausted skill pool while exercising the regular offer and acceptance paths.
            var options = typeof(SkillBattlePreview).GetField("rewardOptions",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            options.SetValue(runtime, Array.CreateInstance(options.FieldType.GetElementType(), 0));
            actor.CurrentHealth = 850;
            if (runtime.RefreshLiveOffer() != 2)
                throw new InvalidOperationException("An injured actor should be offered Cure and Golden Coin.");
            runtime.MoveLiveRewardFocus(1);
            if (runtime.FocusedLiveReward != 1)
                throw new InvalidOperationException("Reward focus did not move to Cure.");
            actor.CurrentHealth = actor.MaxHealth;
            runtime.MoveLiveRewardFocus(0);
            runtime.MoveLiveRewardFocus(1);
            if (runtime.FocusedLiveReward != 0 || runtime.TryAcceptLiveReward(1))
                throw new InvalidOperationException("A stale Cure card was selectable at full health.");
            actor.CurrentHealth = 850;
            if (!runtime.TryAcceptLiveReward(1) ||
                actor.CurrentHealth != 975 || progress.Gold != 0 || runtime.TryAcceptLiveReward(0))
                throw new InvalidOperationException("Cure did not heal or consumed an incorrect number of rewards.");

            runtime.SetRaidLevel(2);
            actor.CurrentHealth = actor.MaxHealth;
            if (runtime.RefreshLiveOffer() != 1 || !runtime.TryAcceptLiveReward(0) ||
                progress.Gold != 1 || runtime.TryAcceptLiveReward(0))
                throw new InvalidOperationException("Golden Coin did not credit exactly once at full health.");
            runtime.SetRaidLevel(8);
            actor.CurrentHealth = 850;
            if (!progress.TryLearn("passive_nidhogg_F") || !runtime.TryChooseAllIn() ||
                runtime.RefreshLiveOffer() != 1 || !runtime.TryAcceptLiveReward(0) ||
                progress.Gold != 2 || actor.CurrentHealth != 850)
                throw new InvalidOperationException("All In did not remove Cure from the fallback offer.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_REWARD_FALLBACK_OK exhausted pool, 125 HP, one gold and single consumption");
        }

        private static void ValidateLiveOffer(SkillProgress progress)
        {
            progress.Reset();
            var runtime = new GameObject("Live reward validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = RaidClass.Warden;
            var actor = Spawn("Live reward caster", 0, Vector3.right * 3440);
            runtime.Bind(actor, progress, null);
            if (runtime.TryAcceptLiveReward(0) || runtime.RefreshLiveOffer() != 3 ||
                runtime.TryAcceptLiveReward(-1) || runtime.TryAcceptLiveReward(3) ||
                runtime.TryChooseAssault() || runtime.RefreshLiveOffer() != 3 ||
                !runtime.TryAcceptLiveReward(0) || runtime.TryAcceptLiveReward(1) ||
                runtime.RefreshLiveOffer() != 0)
                throw new InvalidOperationException("Live reward size, eligibility or single consumption failed.");

            runtime.SetRaidLevel(2);
            if (runtime.RefreshLiveOffer() != 3 || !runtime.TryAcceptLiveReward(2))
                throw new InvalidOperationException("A new level did not generate a new offer.");
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_LIVE_OFFER_OK candidate limit, class gate, single consumption and next level");
        }

        private static void ValidatePermanentMovement()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 30);
            var actor = Spawn("Permanent movement caster", 0, Vector3.right * 3380);
            float[] loki = { 1.02f, 1.04f, 1.08f, 1.15f };
            float[] combined = { 1.16f, 1.17f, 1.19f, 1.22f };
            for (int i = 0; i < loki.Length; i++)
            {
                if (!progress.TryLearn($"virtue_loki_01_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn the Loki movement node.");
                ValidateMoveMultiplier(actor, progress, RaidClass.Warden, loki[i]);
            }

            for (int i = 0; i < combined.Length; i++)
            {
                if (!progress.TryLearn($"virtue_valkyrie_01_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Could not learn the Valkyrie movement node.");
                ValidateMoveMultiplier(actor, progress, RaidClass.Valkyrie, combined[i]);
            }

            ValidateMoveMultiplier(actor, progress, RaidClass.Warden, 1.15f);
            Debug.Log("JOTUNNSLAYER_PERMANENT_MOVEMENT_OK Loki ranks, Valkyrie ranks, class gate and unbind");
        }

        private static void ValidateMoveMultiplier(Health actor, SkillProgress progress, RaidClass raidClass, float expected)
        {
            var runtime = new GameObject("Movement validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = raidClass;
            runtime.Bind(actor, progress, null);
            float actual = SkillBattlePreview.RaidMoveMultiplier(actor);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            if (!Mathf.Approximately(actual, expected) || SkillBattlePreview.RaidMoveMultiplier(actor) != 1f)
                throw new InvalidOperationException("Permanent movement modifier or unbind was incorrect.");
        }

        private static void ValidateSeeressGold(SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 3320;
            var owner = Spawn("Seeress gold caster", 0, origin);
            float[] cutoffs = { 0.0107f, 0.0114f, 0.0128f, 0.015f };
            for (int i = 0; i < cutoffs.Length; i++)
            {
                if (!progress.TryLearn($"virtue_seer_01_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Seeress gold node could not be learned.");
                ValidateClassGold(owner, progress, RaidClass.Seeress, cutoffs[i]);
            }

            ValidateClassGold(owner, progress, RaidClass.Warden, 0.01f);
            Debug.Log("JOTUNNSLAYER_SEERESS_GOLD_OK four drop thresholds and class restriction");
        }

        private static void ValidateClassGold(Health owner, SkillProgress progress, RaidClass raidClass, float cutoff)
        {
            var runtime = new GameObject("Class gold validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            runtime.SelectedRaidClass = raidClass;
            runtime.Bind(owner, progress, null);
            int before = progress.Gold;
            runtime.RollMinionGold(owner.transform.position, cutoff - 0.0001f);
            runtime.RollMinionGold(owner.transform.position, cutoff + 0.0001f);
            runtime.Tick(2300);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            if (progress.Gold != before + 1)
                throw new InvalidOperationException("Class gold drop threshold was incorrect.");
        }

        private static void ValidateLokiGold(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 3260;
            float[] cutoffs = { 0.0107f, 0.0114f, 0.0128f, 0.015f };
            for (int i = 0; i < cutoffs.Length; i++)
            {
                if (!progress.TryLearn($"virtue_loki_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Loki Greed node could not be learned.");
                var owner = Spawn("Loki Greed caster", 0, origin);
                skill.Bind(owner, progress, null);
                int before = progress.Gold;
                skill.RollMinionGold(origin, cutoffs[i] - 0.0001f);
                skill.RollMinionGold(origin, cutoffs[i] + 0.0001f);
                skill.Tick(2200 + i);
                if (progress.Gold != before + 1)
                    throw new InvalidOperationException("Loki Greed drop threshold or pickup credit was incorrect.");
            }

            progress.Reset();
            skill.Bind(Spawn("Loki Greed refund caster", 0, origin), progress, null);
            int baseline = progress.Gold;
            skill.RollMinionGold(origin, 0.0101f);
            skill.Tick(2205);
            if (progress.Gold != baseline)
                throw new InvalidOperationException("Refunded Loki Greed affected the next caster.");
            Debug.Log("JOTUNNSLAYER_LOKI_GREED_OK four drop thresholds, pickup credit and refund");
        }

        private static void ValidatePermanentEconomy(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 3200;
            var target = Spawn("Odin Experience target", 1, origin + Vector3.forward * 3f);
            long[] expected = { 724, 749, 798, 875 };
            for (int i = 0; i < expected.Length; i++)
            {
                if (!progress.TryLearn($"virtue_odin_02_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Odin Experience node could not be learned.");
                var owner = Spawn("Odin Experience caster", 0, origin);
                skill.Bind(owner, progress, null);
                for (int kill = 0; kill < 20; kill++)
                    owner.Server_KilledCharacter?.Invoke(target);
                if (skill.RaidExperience != expected[i])
                    throw new InvalidOperationException("Odin Experience fractional accumulation failed.");
            }

            progress.Reset();
            if (!progress.TryLearn("virtue_thor_03_a"))
                throw new InvalidOperationException("Thor Regeneration rank one could not be learned.");
            var wounded = Spawn("Thor Regeneration rank one", 0, origin);
            wounded.CurrentHealth = 980;
            skill.Bind(wounded, progress, null);
            skill.Tick(2100);
            skill.Tick(2101);
            if (wounded.CurrentHealth != 981)
                throw new InvalidOperationException("Thor Regeneration rank one initial tick failed.");
            skill.Tick(2102);
            if (wounded.CurrentHealth != 983)
                throw new InvalidOperationException("Thor Regeneration lost a fractional heal.");

            if (!progress.TryLearn("virtue_thor_03_b"))
                throw new InvalidOperationException("Thor Regeneration rank two could not be learned.");
            var upgraded = Spawn("Thor Regeneration rank two", 0, origin);
            upgraded.CurrentHealth = 980;
            skill.Bind(upgraded, progress, null);
            if (!skill.TryChooseRegenerationRune())
                throw new InvalidOperationException("Could not select the raid regeneration rune.");
            skill.Tick(2110);
            skill.Tick(2111);
            if (upgraded.CurrentHealth != 985)
                throw new InvalidOperationException("Thor Regeneration did not stack with the raid regeneration rune.");
            Debug.Log("JOTUNNSLAYER_PERMANENT_ECONOMY_OK Odin experience ranks and Thor fractional regeneration");
        }

        private static void ValidateLiveRespawn(SkillProgress progress)
        {
            progress.Reset();
            var runtime = new GameObject("Live match respawn validation").AddComponent<SkillBattlePreview>();
            runtime.LiveMatch = true;
            Vector3 origin = Vector3.right * 3120;
            var oldBody = Spawn("Original match body", 0, origin);
            var newBody = Spawn("Replacement match body", 0, origin);
            var target = Spawn("Match target", 1, origin + Vector3.forward * 3f);
            runtime.Bind(oldBody, progress, null);
            const int matchLevel = 22;
            runtime.SetRaidLevel(matchLevel);
            if (!runtime.TryChooseHealthRune() || !runtime.TryChooseLightning() ||
                !runtime.TryChooseRegenerationRune() || !runtime.TryChooseRegenerationRune() ||
                !runtime.TryChooseRegenerationRune() || !progress.TryLearn(SkillBattlePreview.RegenerationEvolution) ||
                !runtime.TryChooseRegenerationRune())
                throw new InvalidOperationException("Could not configure match respawn validation skills.");

            if (!SkillBattlePreview.TryConsumeRuneRevive(oldBody))
                throw new InvalidOperationException("Could not consume the original body's revive.");
            runtime.Cast(2000);
            ExpectDamage(target, 17);
            runtime.Bind(null, progress, null);
            runtime.Bind(newBody, progress, null);
            if (runtime.RaidLevel != matchLevel)
                throw new InvalidOperationException($"Match respawn changed the level from {matchLevel} to {runtime.RaidLevel}.");
            if (oldBody.MaxHealth != 1000 || newBody.MaxHealth != 1100)
                throw new InvalidOperationException($"Match respawn health bonus transfer failed. Old={oldBody.MaxHealth}, new={newBody.MaxHealth}.");
            if (SkillBattlePreview.TryConsumeRuneRevive(newBody))
                throw new InvalidOperationException("Match respawn rearmed its consumed revive.");

            runtime.Cast(2004.24f);
            ExpectDamage(target, 0);
            runtime.Cast(2004.25f);
            ExpectDamage(target, 17);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_LIVE_RESPAWN_OK level, ranks, cooldown, max health and revive consumption");
        }

        private static void ValidateShadowImages(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 3060;
            var character = Spawn("Shadow Images caster", 0, origin);
            var enemy = Spawn("Shadow Images enemy", 1, origin);
            var ally = Spawn("Shadow Images ally", 0, origin);
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            var images = (System.Collections.IList)typeof(SkillBattlePreview).GetField("shadowImages", flags).GetValue(skill);
            var batches = (System.Collections.IList)typeof(SkillBattlePreview).GetField("shadowImageBatches", flags).GetValue(skill);
            Vector3 Position(int index) => (Vector3)images[index].GetType().GetField("position").GetValue(images[index]);
            void Counts(int live, int pending)
            {
                if (images.Count != live || batches.Count != pending)
                    throw new InvalidOperationException($"Shadow Images lifecycle: live={images.Count}/{live}, batches={batches.Count}/{pending}.");
            }
            void DamageRange(Health body, int minimum, int maximum)
            {
                int damage = body.MaxHealth - body.CurrentHealth;
                if (damage < minimum || damage > maximum)
                    throw new InvalidOperationException($"Shadow Images damage {damage} outside [{minimum}, {maximum}].");
                body.CurrentHealth = body.MaxHealth;
            }
            void Rebind(int rank)
            {
                skill.Bind(null, progress, null);
                character.CurrentHealth = character.MaxHealth;
                enemy.CurrentHealth = enemy.MaxHealth;
                character.transform.SetPositionAndRotation(origin, Quaternion.identity);
                enemy.transform.position = origin + Vector3.forward * 100f;
                skill.Bind(character, progress, null);
                skill.SetRaidLevel(18);
                for (int i = 0; i < rank; i++)
                    if (!skill.TryChooseShadowImages())
                        throw new InvalidOperationException("Shadow Images rank reward unavailable.");
            }

            skill.Bind(character, progress, null);
            if (!skill.TryChooseShadowImages())
                throw new InvalidOperationException("Shadow Images base reward was unavailable.");
            skill.Cast(1900f);
            Counts(0, 1);
            skill.Tick(1900.249f);
            Counts(0, 1);
            skill.Tick(1900.25f);
            Counts(1, 1);
            Vector3 first = Position(0) - origin;
            if (first.magnitude < 4f || first.magnitude > 8f || Vector3.Dot(first.normalized, Vector3.forward) < 0.999f)
                throw new InvalidOperationException("Shadow Images first spawn range or forward direction failed.");
            skill.Tick(1900.37f);
            ExpectDamage(enemy, 0);
            skill.Tick(1900.4f);
            DamageRange(enemy, 22, 28);
            Counts(1, 1); // An attack does not consume the follower.
            skill.Tick(1900.45f);
            ExpectDamage(enemy, 0);
            ExpectDamage(ally, 0);

            // The second spawn reads the current position AND forward, rather than the cast pose.
            Vector3 moved = origin + Vector3.right * 20f;
            character.transform.SetPositionAndRotation(moved, Quaternion.Euler(0f, 90f, 0f));
            skill.Tick(1900.5f);
            Counts(2, 1);
            Vector3 offset = Position(1) - moved;
            Vector3 expected = Quaternion.AngleAxis(120f, Vector3.up) * Vector3.right;
            if (offset.magnitude < 4f || offset.magnitude > 8f || Vector3.Dot(offset.normalized, expected) < 0.999f)
                throw new InvalidOperationException("Shadow Images did not use the moving caster's current stepped pose.");
            skill.Tick(1900.75f);
            Counts(3, 0);
            skill.Tick(1901.25f);
            Counts(2, 0);
            skill.Tick(1901.5f);
            Counts(1, 0);
            skill.Tick(1901.75f);
            Counts(0, 0);
            ExpectDamage(enemy, 0);

            Rebind(2);
            skill.Cast(2000f);
            for (int i = 1; i <= 5; i++)
            {
                skill.Tick(2000f + i * 0.201f);
                Counts(i, i == 5 ? 0 : 1);
            }
            skill.Tick(2002.1f);
            Counts(0, 0);
            skill.Cast(2004.99f);
            Counts(0, 0);
            skill.Cast(2005f);
            skill.Tick(2008f); // Lag emits one, not all remaining overdue followers.
            Counts(1, 1);
            skill.Tick(2008f);
            Counts(1, 1);
            skill.Bind(null, progress, null);
            Counts(0, 0);

            Rebind(3);
            if (skill.TryChooseShadowImages() || !progress.TryLearn(SkillBattlePreview.ShadowImagesEvolution) ||
                !skill.TryChooseShadowImages())
                throw new InvalidOperationException("Shadow Images evolution gate failed.");
            enemy.transform.position = origin + Vector3.forward;
            skill.Cast(2100f);
            DamageRange(enemy, 27, 33); // Separate evolved on-cast branch at the caster.
            ExpectDamage(ally, 0);
            skill.Tick(2100.02f);
            ExpectDamage(enemy, 0); // One hit per target throughout the projectile's lifetime.
            var entrant = Spawn("Shadow Images late entrant", 1, origin + Vector3.forward * 100f);
            entrant.transform.position = origin + Vector3.right;
            skill.Tick(2100.05f);
            DamageRange(entrant, 27, 33);
            skill.Tick(2100.07f);
            ExpectDamage(entrant, 0);
            entrant.transform.position = origin + Vector3.forward * 100f;
            enemy.transform.position = origin + Vector3.forward * 100f;
            skill.Tick(2100.11f);
            entrant.transform.position = origin + Vector3.right;
            skill.Tick(2100.12f);
            ExpectDamage(entrant, 0); // Entry after expiry does not reactivate the projectile.
            entrant.transform.position = origin + Vector3.forward * 100f;
            for (int i = 1; i <= 7; i++)
            {
                skill.Tick(2100f + i * 0.181f);
                Counts(Mathf.Min(i, 6), i == 7 ? 0 : 1);
                Vector3 step = Position(images.Count - 1) - origin;
                Vector3 direction = Quaternion.AngleAxis((i - 1) * 51.4f, Vector3.up) * Vector3.forward;
                if (step.magnitude < 4f || step.magnitude > 8f || Vector3.Dot(step.normalized, direction) < 0.999f)
                    throw new InvalidOperationException("Shadow Images evolved 51.4-degree step failed.");
            }
            // The first has expired by now; each of the remaining six keeps its own lifetime.
            Counts(6, 0);
            skill.Tick(2102.4f);
            Counts(0, 0);

            // A follower without an attack target still explodes at natural expiry.
            skill.Cast(2105f);
            skill.Tick(2105.181f);
            Counts(1, 1);
            enemy.transform.position = Position(0);
            ally.transform.position = Position(0);
            skill.Tick(2106.181f); // Also creates one late follower; no catch-up burst.
            DamageRange(enemy, 27, 33);
            ExpectDamage(ally, 0);
            Counts(1, 1);
            skill.Bind(null, progress, null);
            Counts(0, 0);
            skill.Tick(2108f);
            ExpectDamage(enemy, 0);

            // Refunding evolution affects future casts; cancellation must never detonate existing followers.
            Rebind(4);
            progress.Reset();
            skill.Cast(2200f);
            skill.Tick(2200.181f);
            enemy.transform.position = Position(0);
            character.CurrentHealth = 0;
            skill.Tick(2200.2f);
            Counts(0, 0);
            skill.Tick(2202f);
            ExpectDamage(enemy, 0);
            character.CurrentHealth = character.MaxHealth;
            enemy.transform.position = origin + Vector3.forward * 100f;
            skill.Cast(2205f);
            skill.Tick(2205.181f);
            enemy.transform.position = Position(0);
            skill.Tick(2206.181f);
            ExpectDamage(enemy, 0); // Refunded rank-four cast uses ordinary followers.
            skill.Bind(null, progress, null);
            Counts(0, 0);
            // A valid lock survives movement outside both the master and follower acquisition radii.
            entrant.transform.position = origin + Vector3.right * 200f;
            Rebind(1);
            enemy.transform.position = origin + Vector3.forward;
            skill.Cast(2300f);
            skill.Tick(2300.25f);
            enemy.transform.position = origin + Vector3.forward * 100f;
            skill.Tick(2300.4f);
            DamageRange(enemy, 22, 28);
            skill.Tick(2300.45f);
            ExpectDamage(enemy, 0);

            // A dead lock falls back around its transform, NOT the follower or master.
            Rebind(1);
            enemy.transform.position = origin + Vector3.forward;
            var fallback = Spawn("Shadow Images fallback", 1, origin + Vector3.forward * 100.5f);
            var outside = Spawn("Shadow Images outside fallback", 1, origin + Vector3.forward * 109f);
            skill.Cast(2400f);
            skill.Tick(2400.25f);
            enemy.transform.position = origin + Vector3.forward * 100f;
            enemy.CurrentHealth = 0;
            skill.Tick(2400.4f);
            DamageRange(fallback, 22, 28);
            ExpectDamage(outside, 0);
            skill.Tick(2400.45f);
            ExpectDamage(fallback, 0);
            fallback.transform.position = origin + Vector3.right * 200f;
            Rebind(1);
            enemy.transform.position = origin + Vector3.forward;
            skill.Cast(2500f);
            skill.Tick(2500.25f);
            enemy.transform.position = origin + Vector3.forward * 100f;
            enemy.CurrentHealth = 0;
            skill.Tick(2500.4f);
            ExpectDamage(outside, 0); // No fallback beyond the original eight-metre chain radius.
            ExpectDamage(fallback, 0);
            enemy.CurrentHealth = enemy.MaxHealth;
            skill.Bind(null, progress, null);
            Counts(0, 0);
            Debug.Log("JOTUNNSLAYER_SHADOW_IMAGES_TARGETING_OK moving valid lock retained, dead lock fallback around target, radius and one hit");
            Debug.Log("JOTUNNSLAYER_SHADOW_IMAGES_LIFECYCLE_OK delayed 3/5/7 batches, current pose, no catch-up, windup, ranged single attack, per-image expiry, evolution, cast hit window and cancellation; original priority/rounding/visual fidelity pending");
        }

        private static void ValidateShadowImageDamagePrecision(SkillBattlePreview skill, SkillProgress progress)
        {
            var savedRandom = UnityEngine.Random.state;
            try
            {
                Vector3 origin = Vector3.right * 3090;
                var character = Spawn("Shadow precision caster", 0, origin);
                var target = Spawn("Shadow precision target", 1, origin);
                for (int rank = 1; rank <= 2; rank++)
                {
                    skill.Bind(null, progress, null);
                    progress.Reset();
                    skill.Bind(character, progress, null);
                    skill.SetRaidLevel(18);
                    if (!skill.TryChooseShadowImages() || !progress.TryLearn("passive_valkyrie_O"))
                        throw new InvalidOperationException("Shadow precision setup unavailable.");
                    for (int i = 0; i < rank; i++)
                        if (!skill.TryChooseSpearhead())
                            throw new InvalidOperationException("Shadow precision Spearhead rank unavailable.");
                    // Select a reproducible real roll where rounding before the 25/50% bonus
                    // changes the final integer HP result. This rejects the previous pipeline.
                    UnityEngine.Random.State damageState = default;
                    int expected = 0, legacy = 0, selectedSeed = -1;
                    float raw = 0f;
                    for (int seed = 0; seed < 1000; seed++)
                    {
                        UnityEngine.Random.InitState(seed);
                        damageState = UnityEngine.Random.state;
                        raw = 25f * UnityEngine.Random.Range(0.9f, 1.1f);
                        expected = Mathf.RoundToInt(raw * (1f + rank * 0.25f));
                        legacy = (Mathf.RoundToInt(raw) * (4 + rank) + 2) / 4;
                        if (expected == legacy) continue;
                        selectedSeed = seed;
                        break;
                    }
                    if (selectedSeed < 0)
                        throw new InvalidOperationException("No precision-sensitive Shadow roll found.");
                    float now = 2600f + rank * 10f;
                    skill.Cast(now);
                    skill.Tick(now + 0.25f);
                    target.CurrentHealth = target.MaxHealth;
                    UnityEngine.Random.state = damageState;
                    skill.Tick(now + 0.4f);
                    ExpectDamage(target, expected);
                    Debug.Log($"JOTUNNSLAYER_SHADOW_PRECISION_SAMPLE rank={rank} seed={selectedSeed} raw={raw:R} expected={expected} previous={legacy}");
                    skill.Bind(null, progress, null);
                }
                Debug.Log("JOTUNNSLAYER_SHADOW_PRECISION_OK Spearhead two ranks preserve fractional roll through class scaling; integer TPS health boundary remains");
            }
            finally
            {
                skill.Bind(null, progress, null);
                UnityEngine.Random.state = savedRandom;
            }
        }

        private static void ValidateJavelins(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 3000;
            var character = Spawn("Javelins caster", 0, origin);
            var front = Spawn("Javelins front", 1, origin + Vector3.forward * 3f);
            var diagonal = Spawn("Javelins diagonal", 1, origin + (Vector3.forward + Vector3.right).normalized * 6f);
            var ally = Spawn("Javelins ally", 0, origin + Vector3.forward * 4f);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseJavelins())
                throw new InvalidOperationException("Javelins base reward was unavailable.");

            skill.Cast(1800);
            skill.Tick(1800.2f);
            ExpectDamage(front, 0);
            skill.Tick(1800.31f);
            ExpectDamage(front, 10);
            front.transform.position = origin + Vector3.forward * 7f;
            skill.Tick(1801);
            ExpectDamage(front, 0);
            ExpectDamage(diagonal, 0);
            skill.Cast(1803.99f);
            skill.Tick(1804);
            ExpectDamage(front, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseJavelins() || !skill.TryChooseJavelins() || skill.TryChooseJavelins() ||
                !progress.TryLearn(SkillBattlePreview.JavelinsEvolution) || !skill.TryChooseJavelins())
                throw new InvalidOperationException("Javelins evolution gate failed.");

            var state = UnityEngine.Random.state;
            int seed = 0;
            do { UnityEngine.Random.InitState(seed++); }
            while (UnityEngine.Random.value >= 0.25f || UnityEngine.Random.value >= 0.25f);
            UnityEngine.Random.InitState(seed - 1);
            skill.Cast(1804);
            skill.Tick(1805);
            UnityEngine.Random.state = state;
            ExpectDamage(front, 14);
            ExpectDamage(diagonal, 14);
            ExpectDamage(ally, 0);
            skill.Tick(1805.99f);
            ExpectDamage(front, 0);
            skill.Tick(1806);
            ExpectDamage(front, 2);
            skill.Tick(1810);
            ExpectDamage(front, 8);
            skill.Tick(1811);
            ExpectDamage(front, 0);
            Debug.Log("JOTUNNSLAYER_JAVELINS_OK sweep, deduplication, cooldown, evolved directions and bleed");
        }

        private static void ValidateSpearBarrier(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2940;
            var character = Spawn("Spear Barrier caster", 0, origin);
            var inner = Spawn("Spear Barrier inner", 1, origin + Vector3.forward * 3f);
            var outer = Spawn("Spear Barrier outer", 1, origin + Vector3.forward * 5f);
            var ally = Spawn("Spear Barrier ally", 0, outer.transform.position);
            Vector3 crossing = Vector3.back * 10f;
            skill.Bind(character, progress, null);
            if (!skill.TryChooseSpearBarrier())
                throw new InvalidOperationException("Spear Barrier base reward was unavailable.");

            skill.Cast(1700);
            ExpectDamage(inner, 10);
            ExpectDamage(outer, 0);
            Vector3 limited = SkillBattlePreview.LimitSpearBarrierMovement(outer, crossing, 1701);
            if (Mathf.Abs(limited.z + 0.95f) > 0.01f ||
                SkillBattlePreview.LimitSpearBarrierMovement(ally, crossing, 1701) != crossing ||
                SkillBattlePreview.LimitSpearBarrierMovement(outer, crossing, 1701.5f) != crossing)
                throw new InvalidOperationException("Spear Barrier crossing, ally exclusion or expiry failed.");

            skill.Tick(1701.5f);
            skill.Cast(1706.99f);
            ExpectDamage(inner, 0);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseSpearBarrier() || !skill.TryChooseSpearBarrier() || skill.TryChooseSpearBarrier() ||
                !progress.TryLearn(SkillBattlePreview.SpearBarrierEvolution) || !skill.TryChooseSpearBarrier())
                throw new InvalidOperationException("Spear Barrier evolution gate failed.");

            skill.Cast(1707);
            ExpectDamage(inner, 15);
            ExpectDamage(outer, 15);
            skill.Tick(1707.99f);
            ExpectDamage(outer, 0);
            skill.Tick(1708);
            ExpectDamage(outer, 7);
            skill.Tick(1710);
            ExpectDamage(outer, 14);
            ExpectDamage(ally, 0);
            skill.Cast(1714);
            ExpectDamage(outer, 15);
            skill.Bind(null, progress, null);
            if (SkillBattlePreview.LimitSpearBarrierMovement(outer, crossing, 1714.5f) != crossing)
                throw new InvalidOperationException("Spear Barrier blocked movement after unbind.");
            Debug.Log("JOTUNNSLAYER_SPEAR_BARRIER_OK crossing, cooldown, evolved ticks, expiry and unbind");
        }

        private static void ValidateFallenValkyries(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2880;
            Vector3 center = origin + Vector3.forward * 3f;
            var character = Spawn("Fallen Valkyries caster", 0, origin);
            var primary = Spawn("Fallen Valkyries primary", 1, center);
            var pulled = Spawn("Fallen Valkyries pulled", 1, center + Vector3.forward * 3.5f);
            var ally = Spawn("Fallen Valkyries ally", 0, center);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseFallenValkyries())
                throw new InvalidOperationException("Fallen Valkyries base reward was unavailable.");

            skill.Cast(1600);
            ExpectDamage(primary, 30);
            ExpectDamage(pulled, 30);
            if (Mathf.Abs(Vector3.Distance(pulled.transform.position, center) - 1f) > 0.01f ||
                SkillBattlePreview.IsStoppedBySkill(primary))
                throw new InvalidOperationException("Fallen Valkyries base pull or stun gate failed.");
            skill.Cast(1603.49f);
            ExpectDamage(primary, 0);

            pulled.transform.position = origin + Vector3.right * 11f;
            var second = Spawn("Fallen Valkyries second", 1, origin - Vector3.forward * 6f);
            var third = Spawn("Fallen Valkyries third", 1, origin - Vector3.right * 8f);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseFallenValkyries() || !skill.TryChooseFallenValkyries() ||
                skill.TryChooseFallenValkyries() ||
                !progress.TryLearn(SkillBattlePreview.FallenValkyriesEvolution) || !skill.TryChooseFallenValkyries())
                throw new InvalidOperationException("Fallen Valkyries evolution gate failed.");

            var state = UnityEngine.Random.state;
            int seed = 0;
            do { UnityEngine.Random.InitState(seed++); }
            while (UnityEngine.Random.value >= 0.25f);
            UnityEngine.Random.InitState(seed - 1);
            skill.Cast(1603.5f);
            UnityEngine.Random.state = state;
            ExpectDamage(primary, 30);
            ExpectDamage(second, 30);
            ExpectDamage(third, 30);
            ExpectDamage(pulled, 0);
            ExpectDamage(ally, 0);
            skill.Tick(1605.49f);
            if (!SkillBattlePreview.IsStoppedBySkill(primary))
                throw new InvalidOperationException("Fallen Valkyries did not stun the evolved target.");
            skill.Tick(1605.5f);
            if (SkillBattlePreview.IsStoppedBySkill(primary))
                throw new InvalidOperationException("Fallen Valkyries stun did not expire.");
            Debug.Log("JOTUNNSLAYER_FALLEN_VALKYRIES_OK pull, cooldown, evolution, stun and expiry");
        }

        private static void ValidateValkyrieCharge(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2820;
            var character = Spawn("Valkyrie Charge caster", 0, origin);
            var center = Spawn("Valkyrie Charge center", 1, origin + Vector3.forward * 2f);
            var outer = Spawn("Valkyrie Charge outer", 1, origin + Vector3.forward * 8f + Vector3.right * 4.5f);
            var gap = Spawn("Valkyrie Charge gap", 1, origin + Vector3.forward * 4f + Vector3.right * 0.75f);
            var ally = Spawn("Valkyrie Charge ally", 0, origin + Vector3.forward * 3f);
            skill.Bind(character, progress, null);
            if (skill.TryChooseValkyrieCharge() || !progress.TryLearn(SkillBattlePreview.ValkyrieChargeUnlock) ||
                !skill.TryChooseValkyrieCharge())
                throw new InvalidOperationException("Valkyrie Charge base unlock gate failed.");

            skill.Cast(1500);
            skill.Tick(1500.1f);
            ExpectDamage(center, 0);
            skill.Tick(1500.3f);
            ExpectDamage(center, 50);
            center.transform.position = origin + Vector3.forward * 7f + Vector3.right * 1.5f;
            skill.Tick(1501);
            ExpectDamage(center, 0);
            ExpectDamage(outer, 0);
            ExpectDamage(gap, 0);
            ExpectDamage(ally, 0);

            skill.Cast(1511.99f);
            skill.Tick(1512);
            ExpectDamage(center, 0);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseValkyrieCharge() || !skill.TryChooseValkyrieCharge() || skill.TryChooseValkyrieCharge() ||
                !progress.TryLearn(SkillBattlePreview.ValkyrieChargeEvolution) || !skill.TryChooseValkyrieCharge())
                throw new InvalidOperationException("Valkyrie Charge evolution gate failed.");

            skill.Cast(1512);
            skill.Tick(1513);
            ExpectDamage(center, 80);
            ExpectDamage(outer, 80);
            ExpectDamage(gap, 0);
            ExpectDamage(ally, 0);

            skill.Cast(1524);
            skill.Bind(null, progress, null);
            skill.Tick(1525);
            ExpectDamage(center, 0);
            Debug.Log("JOTUNNSLAYER_VALKYRIE_CHARGE_OK unlocks, sweep, deduplication, evolved lanes and unbind");
        }

        private static void ValidateSeismic(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2760;
            var character = Spawn("Seismic caster", 0, origin);
            Vector3 front = origin + Vector3.forward * 4f;
            var center = Spawn("Seismic center", 1, front + Vector3.forward * 1.8f);
            var left = Spawn("Seismic left", 1, origin + Quaternion.AngleAxis(-45f, Vector3.up) * Vector3.forward * 4f);
            var right = Spawn("Seismic right", 1, origin + Quaternion.AngleAxis(45f, Vector3.up) * Vector3.forward * 4f);
            var ally = Spawn("Seismic ally", 0, front);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseSeismic())
                throw new InvalidOperationException("Seismic Shock base reward was unavailable.");

            skill.Cast(1400);
            skill.Tick(1400.74f);
            ExpectDamage(center, 0);
            var state = UnityEngine.Random.state;
            int seed = 0;
            do { UnityEngine.Random.InitState(seed++); }
            while (UnityEngine.Random.value >= 0.2f);
            UnityEngine.Random.InitState(seed - 1);
            skill.Tick(1400.75f);
            UnityEngine.Random.state = state;
            ExpectDamage(center, 20);
            ExpectDamage(left, 0);
            ExpectDamage(right, 0);
            if (!SkillBattlePreview.IsSlowedByRaidSkill(center, 1404.74f) ||
                SkillBattlePreview.IsSlowedByRaidSkill(center, 1404.75f) ||
                Mathf.Abs(Vector3.Distance(center.transform.position, front) - 1f) > 0.01f)
                throw new InvalidOperationException("Seismic Shock pull or slow expiry failed.");

            skill.Cast(1403.99f);
            skill.Tick(1404.74f);
            ExpectDamage(center, 0);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseSeismic() || !skill.TryChooseSeismic() || skill.TryChooseSeismic() ||
                !progress.TryLearn(SkillBattlePreview.SeismicEvolution) || !skill.TryChooseSeismic())
                throw new InvalidOperationException("Seismic Shock evolution gate failed.");

            state = UnityEngine.Random.state;
            skill.Cast(1404.75f);
            skill.Tick(1405.5f);
            UnityEngine.Random.state = state;
            ExpectDamage(center, 32);
            ExpectDamage(left, 32);
            ExpectDamage(right, 32);
            ExpectDamage(ally, 0);

            skill.Cast(1409);
            skill.Bind(null, progress, null);
            skill.Tick(1410);
            ExpectDamage(center, 0);
            Debug.Log("JOTUNNSLAYER_SEISMIC_OK delay, cooldown, pull, slow expiry, evolved spread and unbind");
        }

        private static void ValidateAnvil(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2690;
            Vector3 impact = origin + Vector3.forward * 4f;
            var character = Spawn("Anvil caster", 0, origin);
            var primary = Spawn("Anvil primary", 1, impact);
            var pulled = Spawn("Anvil pulled", 1, impact + Vector3.right * 2.5f);
            var ally = Spawn("Anvil ally", 0, impact);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseAnvil())
                throw new InvalidOperationException("Anvil base reward was unavailable.");

            skill.Cast(1300);
            ExpectDamage(primary, 24);
            ExpectDamage(pulled, 0);
            skill.Tick(1300.99f);
            ExpectDamage(primary, 0);
            ExpectDamage(pulled, 0);
            skill.Tick(1301);
            ExpectDamage(primary, 10);
            ExpectDamage(pulled, 10);
            if (Mathf.Abs(Vector3.Distance(pulled.transform.position, impact) - 1f) > 0.01f)
                throw new InvalidOperationException("Anvil did not pull the nearby enemy.");
            skill.Cast(1302.99f);
            ExpectDamage(primary, 0);

            pulled.transform.position = origin + Vector3.right * 11f;
            var second = Spawn("Anvil second", 1, origin - Vector3.forward * 6f);
            var third = Spawn("Anvil third", 1, origin - Vector3.right * 8f);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseAnvil() || !skill.TryChooseAnvil() || skill.TryChooseAnvil() ||
                !progress.TryLearn(SkillBattlePreview.AnvilEvolution) || !skill.TryChooseAnvil())
                throw new InvalidOperationException("Anvil evolution gate failed.");

            skill.Cast(1303);
            ExpectDamage(primary, 28);
            ExpectDamage(second, 28);
            ExpectDamage(third, 28);
            ExpectDamage(pulled, 0);
            skill.Tick(1304);
            ExpectDamage(primary, 20);
            ExpectDamage(second, 20);
            ExpectDamage(third, 20);
            ExpectDamage(pulled, 0);
            ExpectDamage(ally, 0);

            skill.Cast(1306);
            ExpectDamage(primary, 28);
            skill.Bind(null, progress, null);
            skill.Tick(1307);
            ExpectDamage(primary, 0);
            Debug.Log("JOTUNNSLAYER_ANVIL_OK delay, pull, cooldown, evolved target count and unbind");
        }

        private static void ValidateShieldwall(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2630;
            var character = Spawn("Shieldwall caster", 0, origin);
            Vector3 forward = origin + Vector3.forward * 2f;
            var front = Spawn("Shieldwall forward", 1, forward);
            var side = Spawn("Shieldwall side", 1, origin + Vector3.right * 2f);
            var ally = Spawn("Shieldwall ally", 0, origin - Vector3.forward * 2f);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseShieldwall())
                throw new InvalidOperationException("Shieldwall base reward was unavailable.");

            skill.Cast(1200);
            ExpectDamage(front, 15);
            if (Vector3.Distance(front.transform.position, forward + Vector3.forward) > 0.01f)
                throw new InvalidOperationException("Shieldwall did not push its target outward.");
            front.transform.position = forward;
            skill.Tick(1200.1f);
            ExpectDamage(front, 0);
            ExpectDamage(side, 0);
            skill.Tick(1200.75f);
            ExpectDamage(side, 15);
            skill.Tick(1203);
            ExpectDamage(ally, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseShieldwall() || !skill.TryChooseShieldwall() || skill.TryChooseShieldwall() ||
                !progress.TryLearn(SkillBattlePreview.ShieldwallEvolution) || !skill.TryChooseShieldwall())
                throw new InvalidOperationException("Shieldwall evolution gate failed.");

            front.transform.position = forward;
            ExpectDamage(front, 15);
            skill.Cast(1204.99f);
            ExpectDamage(front, 0);
            skill.Cast(1205);
            ExpectDamage(front, 24);
            skill.Tick(1208);
            front.transform.position = forward;
            skill.Cast(1209);
            ExpectDamage(front, 24);
            ExpectDamage(ally, 0);
            Debug.Log("JOTUNNSLAYER_SHIELDWALL_OK orbit sweep, deduplication, knockback, evolution and cooldown");
        }

        private static void ValidateIronFist(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2570;
            var character = Spawn("Iron Fist caster", 0, origin);
            var inner = Spawn("Iron Fist inner", 1, origin + Vector3.forward);
            var outer = Spawn("Iron Fist outer", 1, origin + Vector3.forward * 2.5f);
            var ally = Spawn("Iron Fist ally", 0, origin + Vector3.right);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseIronFist())
                throw new InvalidOperationException("Iron Fist base reward was unavailable.");

            skill.Cast(1100);
            skill.Tick(1100.99f);
            ExpectDamage(inner, 0);
            skill.Tick(1101);
            ExpectDamage(inner, 22);
            skill.Tick(1102);
            ExpectDamage(inner, 22);
            ExpectDamage(outer, 0);

            skill.Cast(1104.99f);
            skill.Tick(1105);
            ExpectDamage(inner, 0);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseIronFist() || !skill.TryChooseIronFist() || skill.TryChooseIronFist() ||
                !progress.TryLearn(SkillBattlePreview.IronFistEvolution) || !skill.TryChooseIronFist())
                throw new InvalidOperationException("Iron Fist evolution gate failed.");

            var randomState = UnityEngine.Random.state;
            skill.Cast(1105);
            skill.Tick(1107.01f);
            UnityEngine.Random.state = randomState;
            ExpectDamage(inner, 150);
            ExpectDamage(outer, 150);
            ExpectDamage(ally, 0);

            skill.Cast(1110);
            skill.Bind(null, progress, null);
            skill.Tick(1112.01f);
            ExpectDamage(inner, 0);
            Debug.Log("JOTUNNSLAYER_IRON_FIST_OK cadence, cooldown, evolution gate, area and unbind");
        }

        private static void ValidateThorHealth(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            int[] bonuses = { 20, 40, 80, 150 };
            Health previous = null;
            for (int i = 0; i < bonuses.Length; i++)
            {
                if (!progress.TryLearn($"virtue_thor_01_{(char)('a' + i)}"))
                    throw new InvalidOperationException("Thor health node could not be learned.");

                var character = Spawn("Thor health caster", 0, Vector3.right * (2420 + i * 30));
                skill.Bind(character, progress, null);
                skill.Bind(character, progress, null);
                if (character.MaxHealth != 1000 + bonuses[i] || character.CurrentHealth != character.MaxHealth)
                    throw new InvalidOperationException("Thor health bonus did not apply exactly once at deployment.");
                if (previous && previous.MaxHealth != 1000)
                    throw new InvalidOperationException("Thor health bonus persisted on the previous caster.");
                previous = character;
            }

            if (!skill.TryChooseHealthRune() || previous.MaxHealth != 1250 || previous.CurrentHealth != 1150)
                throw new InvalidOperationException("Thor health did not stack with the raid health rune.");

            skill.Bind(null, progress, null);
            progress.Reset();
            skill.Bind(previous, progress, null);
            if (previous.MaxHealth != 1000 || previous.CurrentHealth != 1000)
                throw new InvalidOperationException("Refunded Thor health or a previous raid rune carried into a new bind.");

            Debug.Log("JOTUNNSLAYER_THOR_HEALTH_OK four ranks, full-health deployment, rune stacking and rebind");
        }

        private static void ValidateHammertime(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2360;
            var character = Spawn("Hammertime caster", 0, origin);
            var front = Spawn("Hammertime front", 1, origin + Vector3.forward * 3);
            var far = Spawn("Hammertime far", 1, origin - Vector3.forward * 6);
            var ally = Spawn("Hammertime ally", 0, origin + Vector3.right * 2);
            skill.Bind(character, progress, null);
            if (skill.TryChooseHammertime() || !progress.TryLearn(SkillBattlePreview.HammertimeUnlock) ||
                !skill.TryChooseHammertime())
                throw new InvalidOperationException("Hammertime base unlock gate failed.");

            skill.Cast(1000);
            skill.Tick(1000.374f);
            ExpectDamage(front, 0);
            skill.Tick(1000.375f);
            ExpectDamage(front, 10);
            skill.Tick(1001.5f);
            ExpectDamage(front, 0);
            ExpectDamage(far, 0);
            ExpectDamage(ally, 0);

            skill.SetRaidLevel(17);
            if (!skill.TryChooseHammertime() || !skill.TryChooseHammertime() ||
                !progress.TryLearn(SkillBattlePreview.HammertimeEvolution) || skill.TryChooseHammertime())
                throw new InvalidOperationException("Hammertime evolution level gate failed.");
            skill.SetRaidLevel(18);
            if (!skill.TryChooseHammertime())
                throw new InvalidOperationException("Hammertime evolution selection failed.");

            skill.Cast(1003);
            skill.Tick(1003.5f);
            ExpectDamage(front, 0);
            skill.Cast(1004);
            skill.Tick(1004.375f);
            ExpectDamage(front, 14);
            ExpectDamage(far, 14);
            skill.Tick(1005.5f);
            ExpectDamage(front, 14);
            ExpectDamage(far, 14);
            ExpectDamage(ally, 0);
            Debug.Log("JOTUNNSLAYER_HAMMERTIME_OK unlock, delayed sweep, range, cooldown, and dual-hammer deduplication");
        }

        private static void ValidateFlamingDarts(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2300;
            var character = Spawn("Flaming Darts caster", 0, origin);
            var primary = Spawn("Flaming Darts primary", 1, origin + Vector3.forward * 6);
            skill.Bind(character, progress, null);
            if (skill.TryChooseFlamingDarts() || !progress.TryLearn(SkillBattlePreview.FlamingDartsUnlock) ||
                !skill.TryChooseFlamingDarts())
                throw new InvalidOperationException("Flaming Darts base unlock gate failed.");

            var randomState = UnityEngine.Random.state;
            int seed = 0;
            int explosions;
            do
            {
                UnityEngine.Random.InitState(seed++);
                explosions = 0;
                for (int i = 0; i < 3; i++)
                {
                    UnityEngine.Random.Range(0, 1);
                    if (UnityEngine.Random.value < 0.2f)
                        explosions++;
                }
            }
            while (explosions == 0 || explosions == 3);

            UnityEngine.Random.InitState(seed - 1);
            skill.Cast(940);
            ExpectDamage(primary, 14 * (3 + explosions));
            skill.Cast(942.99f);
            ExpectDamage(primary, 0);

            skill.SetRaidLevel(18);
            if (!skill.TryChooseFlamingDarts() || !skill.TryChooseFlamingDarts() || skill.TryChooseFlamingDarts() ||
                !progress.TryLearn(SkillBattlePreview.FlamingDartsEvolution) || !skill.TryChooseFlamingDarts())
                throw new InvalidOperationException("Flaming Darts evolution gate failed.");
            skill.Cast(943);
            ExpectDamage(primary, 280);

            var secondary = Spawn("Flaming Darts bounce", 1, primary.transform.position + Vector3.right * 4);
            do
            {
                UnityEngine.Random.InitState(seed++);
                UnityEngine.Random.Range(0, 2);
            }
            while (UnityEngine.Random.value >= 0.5f);
            UnityEngine.Random.InitState(seed - 1);
            skill.Cast(945);
            int totalDamage = primary.MaxHealth - primary.CurrentHealth + secondary.MaxHealth - secondary.CurrentHealth;
            if (totalDamage < 320 || totalDamage > 560 || totalDamage % 40 != 0)
                throw new InvalidOperationException($"Flaming Darts bounce damage was {totalDamage}.");

            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_FLAMING_DARTS_OK unlocks, base explosion, cooldown, evolved explosion and bounce");
        }

        private static void ValidateSisterhood(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2260;
            var character = Spawn("Sisterhood caster", 0, origin);
            var targets = new Health[5];
            for (int i = 0; i < targets.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0, i * 72f, 0) * Vector3.forward;
                targets[i] = Spawn($"Sisterhood target {i}", 1, origin + direction * 4.4f);
            }

            skill.Bind(character, progress, null);
            if (!skill.TryChooseSisterhood())
                throw new InvalidOperationException("Sisterhood base selection failed.");
            skill.Cast(910);
            skill.Tick(910);
            ExpectDamage(targets[0], 10);
            ExpectDamage(targets[1], 0);
            character.transform.position += Vector3.forward * 10;
            skill.Tick(916);
            ExpectDamage(targets[0], 50);
            skill.Tick(917);
            ExpectDamage(targets[0], 0);

            skill.Bind(Spawn("Sisterhood evolved caster", 0, origin), progress, null);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseSisterhood() || !skill.TryChooseSisterhood() || !skill.TryChooseSisterhood() ||
                skill.TryChooseSisterhood() || !progress.TryLearn(SkillBattlePreview.SisterhoodEvolution) ||
                !skill.TryChooseSisterhood())
                throw new InvalidOperationException("Sisterhood evolution gate failed.");
            skill.Cast(920);
            skill.Tick(920);
            foreach (var target in targets)
                ExpectDamage(target, 10);

            skill.Cast(924);
            skill.Tick(924);
            foreach (var target in targets)
                ExpectDamage(target, 20);

            skill.Cast(924.5f);
            skill.Bind(character, progress, null);
            skill.Tick(925);
            foreach (var target in targets)
                ExpectDamage(target, 0);

            Debug.Log("JOTUNNSLAYER_SISTERHOOD_OK stationary attacks, five evolved decoys, expiry and rebind cleanup");
        }

        private static void ValidateFlamingRay(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            Vector3 origin = Vector3.right * 2220;
            var character = Spawn("Flaming Ray caster", 0, origin);
            var near = Spawn("Flaming Ray near", 1, origin + Vector3.forward * 2);
            var front = Spawn("Flaming Ray front", 1, origin + Vector3.forward * 4.8f);
            var side = Spawn("Flaming Ray side", 1, origin + Quaternion.Euler(0, 120, 0) * Vector3.forward * 4.8f);
            var rotated = Spawn("Flaming Ray rotation", 1, origin + Quaternion.Euler(0, 30, 0) * Vector3.forward * 4.8f);
            skill.Bind(character, progress, null);
            if (!skill.TryChooseFlamingRay())
                throw new InvalidOperationException("Flaming Ray base selection failed.");
            skill.Cast(890);
            skill.Tick(890);
            ExpectDamage(near, 10);
            ExpectDamage(front, 0);
            ExpectDamage(side, 0);

            skill.Bind(Spawn("Flaming Ray evolved caster", 0, origin), progress, null);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseFlamingRay() || !skill.TryChooseFlamingRay() || !skill.TryChooseFlamingRay() ||
                skill.TryChooseFlamingRay() || !progress.TryLearn(SkillBattlePreview.FlamingRayEvolution) ||
                !skill.TryChooseFlamingRay())
                throw new InvalidOperationException("Flaming Ray evolution gate failed.");
            skill.Cast(900);
            skill.Tick(900);
            ExpectDamage(front, 20);
            ExpectDamage(side, 20);
            ExpectDamage(rotated, 0);
            skill.Tick(900.25f);
            ExpectDamage(rotated, 20);
            skill.Bind(character, progress, null);
            skill.Tick(903);
            ExpectDamage(front, 0);
            ExpectDamage(side, 0);
            ExpectDamage(rotated, 0);
            Debug.Log("JOTUNNSLAYER_FLAMING_RAY_OK base range, evolved rays, rotation and rebind cleanup");
        }

        private static void ValidateShrapnelPot(SkillBattlePreview skill, SkillProgress progress)
        {
            progress.Reset();
            var character = Spawn("Pot caster", 0, Vector3.right * 2180);
            var target = Spawn("Pot victim", 1, character.transform.position + Vector3.forward * 6);
            skill.Bind(character, progress, null);
            skill.SetRaidLevel(18);
            if (!skill.TryChooseShrapnelPot() || !skill.TryChooseShrapnelPot() ||
                !skill.TryChooseShrapnelPot() || skill.TryChooseShrapnelPot() ||
                !progress.TryLearn(SkillBattlePreview.PotEvolution) || !skill.TryChooseShrapnelPot())
                throw new InvalidOperationException("Shrapnel Pot evolution gate failed.");

            var randomState = UnityEngine.Random.state;
            int seed = 0;
            do
            {
                UnityEngine.Random.InitState(seed++);
            }
            while (UnityEngine.Random.value >= 0.5f);
            UnityEngine.Random.InitState(seed - 1);

            skill.Cast(880);
            skill.Tick(880.59f);
            ExpectDamage(target, 0);
            skill.Tick(880.61f);
            ExpectDamage(target, 35);
            skill.Tick(880.91f);
            ExpectDamage(target, 23);
            skill.Cast(882.49f);
            skill.Tick(882.5f);
            ExpectDamage(target, 15);
            skill.Cast(882.5f);
            skill.Bind(Spawn("Replacement pot caster", 0, character.transform.position), progress, null);
            skill.Tick(883.2f);
            ExpectDamage(target, 0);
            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_SHRAPNEL_POT_OK delay, secondary explosion, poison and rebind cleanup");
        }

        private static void ValidateMagicMissileInterception()
        {
            Vector3 launchDirection = SkillBattlePreview.MagicMissileLaunchDirection(
                Vector3.forward + Vector3.up, Vector3.right, 65f, 60f);
            Vector3 horizontal = Vector3.ProjectOnPlane(launchDirection, Vector3.up);
            if (Mathf.Abs(Vector3.SignedAngle(Vector3.forward, horizontal, Vector3.up) - 60f) > 0.01f ||
                Mathf.Abs(Vector3.Angle(horizontal, launchDirection) - 65f) > 0.01f)
                throw new InvalidOperationException("Locked Magic Missile should preserve both yaw and elevation.");

            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 7980f;
            var actor = Spawn("Magic Missile path caster", 0, origin);
            var target = Spawn("Magic Missile locked target", 1, origin + Vector3.forward * 8f);
            var runtime = new GameObject("Magic Missile interception validation").AddComponent<SkillBattlePreview>();
            runtime.Bind(actor, progress, null);
            if (!progress.TryLearn(SkillBattlePreview.MissileUnlock) || !runtime.TryChoose(true))
                throw new InvalidOperationException("Could not select Magic Missiles.");

            runtime.Cast(5140f);
            target.transform.position += Vector3.right * 6f;
            Vector3 elevatedPath = Quaternion.AngleAxis(-65f, Vector3.right) * Vector3.forward;
            var blocker = Spawn("Magic Missile interceptor", 1, origin + elevatedPath * 1.1f);
            var ally = Spawn("Magic Missile friendly pass-through", 0, origin + elevatedPath * 0.55f);
            runtime.Tick(5140.09f);
            ExpectDamage(target, 0);
            ExpectDamage(ally, 0);
            ExpectDamage(blocker, 8);
            target.transform.position -= Vector3.right * 6f;
            blocker.Team = actor.Team;
            runtime.Tick(5141f);
            ExpectDamage(blocker, 0);

            if (!progress.TryLearn(SkillBattlePreview.MissileEvolution))
                throw new InvalidOperationException("Could not unlock Magic Missile evolution.");
            runtime.SetRaidLevel(6);
            Choose(runtime, true);
            runtime.SetRaidLevel(12);
            Choose(runtime, true);
            runtime.SetRaidLevel(18);
            Choose(runtime, true);
            var randomState = UnityEngine.Random.state;
            UnityEngine.Random.Range(0, 1);
            float mainElevation = UnityEngine.Random.Range(50f, 80f);
            UnityEngine.Random.state = randomState;
            Vector3 mainHeading = Quaternion.AngleAxis(-mainElevation, Vector3.right) * Vector3.forward;
            Vector3 expiryOrigin = origin + mainHeading * 26f;
            runtime.Cast(5144f);
            target.Team = actor.Team;
            var shardTarget = Spawn("Magic Missile expiry shard target", 1,
                expiryOrigin + Vector3.right * 2f);
            var otherShardTarget = Spawn("Magic Missile distinct shard target", 1,
                expiryOrigin - Vector3.right * 2f);
            runtime.Tick(5146f);
            ExpectDamage(shardTarget, 0);
            ExpectDamage(otherShardTarget, 0);
            runtime.Tick(5146.13f);
            ExpectDamage(shardTarget, 0);
            ExpectDamage(otherShardTarget, 0);
            runtime.Tick(5147.3f);
            ExpectDamage(shardTarget, 12);
            ExpectDamage(otherShardTarget, 12);
            runtime.Tick(5147.4f);
            ExpectDamage(shardTarget, 0);
            ExpectDamage(otherShardTarget, 0);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            Debug.Log("JOTUNNSLAYER_MAGIC_MISSILE_INTERCEPTION_OK enemy contact, friendly pass-through and expiry shards");
        }

        private static void ValidateMagicMissileTerrain()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 8040f;
            var actor = Spawn("Magic Missile terrain caster", 0, origin);
            var target = Spawn("Magic Missile target under ceiling", 1, origin + Vector3.forward * 2f);
            var runtime = new GameObject("Magic Missile terrain validation").AddComponent<SkillBattlePreview>();
            var ceiling = new GameObject("Magic Missile blocking ceiling").AddComponent<BoxCollider>();
            ceiling.transform.position = origin + Vector3.up * 1.7f;
            ceiling.size = new Vector3(4f, 0.2f, 4f);
            Physics.SyncTransforms();
            runtime.Bind(actor, progress, null);
            if (!progress.TryLearn(SkillBattlePreview.MissileUnlock))
                throw new InvalidOperationException("Could not unlock Magic Missiles for terrain validation.");
            Choose(runtime, true);

            runtime.Cast(5160f);
            runtime.Tick(5161f);
            ExpectDamage(target, 0);
            ceiling.isTrigger = true;
            Physics.SyncTransforms();
            runtime.Cast(5164f);
            runtime.Tick(5165f);
            ExpectDamage(target, 8);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Object.Destroy(ceiling.gameObject);
            NetworkServer.Destroy(actor.gameObject);
            NetworkServer.Destroy(target.gameObject);
            Debug.Log("JOTUNNSLAYER_MAGIC_MISSILE_TERRAIN_OK solid ceiling blocks and trigger allows passage");
        }

        private static void ValidateHeartseekerPath()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 8100f;
            var actor = Spawn("Heart Seeker path caster", 0, origin);
            var first = Spawn("Heart Seeker initial target", 1, origin + Vector3.forward * 3f);
            var bounce = Spawn("Heart Seeker outside cast range", 1, origin + Vector3.forward * 12f);
            var final = Spawn("Heart Seeker final allowed hit", 1, origin + Vector3.forward * 14f);
            var extra = Spawn("Heart Seeker excess target inside bounce range", 1, origin + Vector3.forward * 16f);
            var outside = Spawn("Heart Seeker outside bounce range", 1, origin + Vector3.forward * 30f);
            var runtime = new GameObject("Heart Seeker path validation").AddComponent<SkillBattlePreview>();
            runtime.Bind(actor, progress, null);
            if (!progress.TryLearn(SkillBattlePreview.HeartUnlock) || !runtime.TryChooseHeartseeker())
                throw new InvalidOperationException("Could not select Heart Seeker for path validation.");

            var randomState = UnityEngine.Random.state;
            int noBleed = FindDamageRollSeed(.5f, 1f);
            runtime.Cast(5170f);
            UnityEngine.Random.InitState(noBleed);
            runtime.Tick(5170.3f);
            ExpectDamage(first, 15);
            ExpectDamage(bounce, 0);
            UnityEngine.Random.InitState(noBleed);
            runtime.Tick(5171f);
            ExpectDamage(bounce, 15);
            UnityEngine.Random.InitState(noBleed);
            runtime.Tick(5171.3f);
            ExpectDamage(final, 15);
            runtime.Tick(5174f);
            ExpectDamage(first, 0);
            ExpectDamage(extra, 0);
            ExpectDamage(outside, 0);
            NetworkServer.Destroy(final.gameObject);
            NetworkServer.Destroy(extra.gameObject);

            runtime.Cast(5174f);
            first.transform.position += Vector3.right * 4f;
            runtime.Tick(5174.5f);
            ExpectDamage(first, 0);
            first.transform.position -= Vector3.right * 4f;
            first.Team = actor.Team;
            UnityEngine.Random.InitState(noBleed);
            runtime.Tick(5178f);
            ExpectDamage(first, 0);
            ExpectDamage(bounce, 15);
            first.Team = 1;

            var wall = new GameObject("Heart Seeker wall").AddComponent<BoxCollider>();
            wall.transform.position = origin + Vector3.up + Vector3.forward * 1.5f;
            wall.size = new Vector3(2f, 2f, .25f);
            Physics.SyncTransforms();
            runtime.Cast(5178f);
            runtime.Tick(5179f);
            ExpectDamage(first, 0);
            ExpectDamage(bounce, 0);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Object.Destroy(wall.gameObject);
            NetworkServer.Destroy(actor.gameObject);
            NetworkServer.Destroy(first.gameObject);
            NetworkServer.Destroy(bounce.gameObject);
            NetworkServer.Destroy(outside.gameObject);
            UnityEngine.Random.state = randomState;
            Debug.Log("JOTUNNSLAYER_HEART_PATH_OK straight flight, local bounce range, hit cap and terrain");
        }

        private static void ValidateFocusFirePath()
        {
            var progress = new SkillProgress(SkillCatalog.Load(), null, 20);
            Vector3 origin = Vector3.right * 8160f;
            var actor = Spawn("Focus Fire path caster", 0, origin);
            var target = Spawn("Focus Fire tracking target", 1, origin + Vector3.forward * 5f);
            var runtime = new GameObject("Focus Fire path validation").AddComponent<SkillBattlePreview>();
            runtime.Bind(actor, progress, null);
            if (!runtime.TryChooseFocus())
                throw new InvalidOperationException("Could not select Focus Fire for path validation.");

            runtime.Cast(5185f);
            runtime.Tick(5185.05f);
            ExpectDamage(target, 0);
            target.transform.position += Vector3.right * 3f;
            runtime.Tick(5185.2f);
            ExpectDamage(target, 0);
            runtime.Tick(5185.52f);
            ExpectDamage(target, 8);
            runtime.Tick(5186.4f);
            ExpectDamage(target, 56);

            target.transform.position -= Vector3.right * 3f;
            var wall = new GameObject("Focus Fire wall").AddComponent<BoxCollider>();
            wall.transform.position = origin + Vector3.up + Vector3.forward * 1.5f;
            wall.size = new Vector3(2f, 2f, .25f);
            Physics.SyncTransforms();
            runtime.Cast(5189f);
            runtime.Tick(5190.4f);
            ExpectDamage(target, 0);
            runtime.Bind(null, progress, null);
            UnityEngine.Object.Destroy(runtime.gameObject);
            UnityEngine.Object.Destroy(wall.gameObject);
            NetworkServer.Destroy(actor.gameObject);
            NetworkServer.Destroy(target.gameObject);
            Debug.Log("JOTUNNSLAYER_FOCUS_PATH_OK travel, moving target and terrain");
        }

        private static void ValidateGoldPickup()
        {
            var character = Spawn("Gold collector", 0, Vector3.right * 100);
            var preview = new GameObject("Gold pickup validation").AddComponent<SkillBattlePreview>();
            var progress = new SkillProgress(SkillCatalog.Load(), null, 0, 2);
            preview.Bind(character, progress, null);
            Vector3 drop = character.transform.position + Vector3.forward * 5;
            preview.RollMinionGold(character.transform.position, 0.01f);
            preview.RollMinionGold(drop, 0.009f);
            preview.Tick(0);
            if (progress.Gold != 2)
                throw new InvalidOperationException("Gold was credited outside pickup distance or at the failed roll boundary.");

            character.transform.position += Vector3.forward;
            preview.Tick(1);
            preview.Tick(2);
            if (progress.Gold != 3 || !progress.TryBuyPoints(1) || progress.Gold != 0 || progress.Points != 1)
                throw new InvalidOperationException("Gold pickup was not credited exactly once to the exchange balance.");

            UnityEngine.Object.Destroy(preview.gameObject);
            NetworkServer.Destroy(character.gameObject);
            Debug.Log("JOTUNNSLAYER_GOLD_PICKUP_OK chance boundary, pickup range, single credit and exchange");
        }

        private static void Choose(SkillBattlePreview skill, bool missile)
        {
            if (!skill.TryChoose(missile))
                throw new InvalidOperationException("Eligible raid reward could not be selected.");
        }

        private static Health Spawn(string name, int team, Vector3 position)
        {
            var actor = new GameObject(name);
            actor.SetActive(false);
            actor.AddComponent<NetworkIdentity>();
            var body = actor.AddComponent<Health>();
            actor.SetActive(true);
            body.transform.position = position;
            body.Team = team;
            body.CurrentHealth = body.MaxHealth = 1000;
            NetworkServer.Spawn(body.gameObject);
            GameManager.AddHealthInstance(body);
            return body;
        }

        private static void ExpectDamage(Health body, int damage)
        {
            int actual = body.MaxHealth - body.CurrentHealth;
            if (actual != damage)
                throw new InvalidOperationException($"{body.name}: expected {damage} damage, got {actual}.");
            body.CurrentHealth = body.MaxHealth;
        }
    }
}
#endif



































































