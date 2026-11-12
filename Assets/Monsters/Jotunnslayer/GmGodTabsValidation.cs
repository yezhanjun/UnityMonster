#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using MTPSKIT.RainImported;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Jotunnslayer
{
    // Explicit current-project smoke: tests the actual training host and GM callbacks.
    [InitializeOnLoad]
    public sealed class GmGodTabsValidation : MonoBehaviour
    {
        const string Pending = "TPS.GmGodTabsValidation";
        const string Interactive = "TPS.GmGodTabsValidation.Interactive";
        bool ending;
        static GmGodTabsValidation()
        {
            Debug.Log("GM_VALIDATION_READY temp=" + System.IO.Path.GetTempPath());
            EditorApplication.delayCall += ConsumeLocalRequest;
            EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode && SessionState.GetBool(Interactive, false))
            {
                SessionState.EraseBool(Interactive);
                string previous = SessionState.GetString(Interactive + ".Scene", "");
                if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous);
            }
            if (state == PlayModeStateChange.EnteredEditMode) EditorApplication.delayCall += ConsumeLocalRequest;
            if (state != PlayModeStateChange.EnteredPlayMode || !SessionState.GetBool(Pending, false)) return;
            SessionState.EraseBool(Pending);
            var host = new GameObject("GM god and monster tabs validation");
            DontDestroyOnLoad(host);
            host.AddComponent<GmGodTabsValidation>();
        };
        }

        static void ConsumeLocalRequest()
        {
            if (Application.isBatchMode) return;
            string request = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TPS-GmGodTabsValidation.request");
            if (!System.IO.File.Exists(request)) return;
            System.IO.File.Delete(request);
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                EditorSceneManager.GetActiveScene().isDirty)
            {
                Debug.LogWarning("GM_GOD_TABS_DEFERRED editor busy or unsaved scene; use Tools/TPS/Validate GM God and Monsters when ready");
                return;
            }
            RunInteractive();
        }

        [MenuItem("Tools/TPS/Validate GM God and Monsters")]
        public static void RunInteractive()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            string request = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TPS-GmGodTabsValidation.request");
            if (System.IO.File.Exists(request)) System.IO.File.Delete(request);
            SessionState.SetString(Interactive + ".Scene", EditorSceneManager.GetActiveScene().path);
            SessionState.SetBool(Interactive, true);
            RunBatch();
        }

        static void Finish(int code)
        {
            if (SessionState.GetBool(Interactive, false)) EditorApplication.ExitPlaymode();
            else EditorApplication.Exit(code);
        }

        public static void RunBatch()
        {
            RepairSkeletonAvatar();
            EditorSceneManager.OpenScene("Assets/Game/Baseline/Scenes/Startup.unity");
            SessionState.SetBool(Pending, true);
            EditorApplication.EnterPlaymode();
        }

        public static void RepairSkeletonAvatar()
        {
            const string path = "Assets/Monsters/Jotunnslayer/Enemies/Resources/Jotunnslayer/Skeleton1H/skeleton_1h_sur.fbx";
            var importer = (ModelImporter)AssetImporter.GetAtPath(path);
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var description = importer.humanDescription;
            var bones = model.GetComponentsInChildren<Transform>(true);
            var actual = bones.Select(bone => new SkeletonBone { name = bone.name,
                position = bone.localPosition, rotation = bone.localRotation, scale = bone.localScale }).ToArray();
            var hips = bones.FirstOrDefault(bone => bone.name == "hips");
            Debug.Log("GM_AVATAR_REST hips=" + (hips ? hips.localPosition.ToString("F5") : "missing") +
                " importedHumanScale=" + model.GetComponent<Animator>().humanScale);
            bool equal = description.skeleton.Length == actual.Length;
            for (int i = 0; equal && i < actual.Length; i++)
                equal = description.skeleton[i].name == actual[i].name &&
                    Vector3.Distance(description.skeleton[i].position, actual[i].position) < .000001f &&
                    Vector3.Distance(description.skeleton[i].scale, actual[i].scale) < .000001f;
            if (equal) return;
            description.skeleton = actual;
            importer.humanDescription = description;
            importer.SaveAndReimport();
            model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            Debug.Log("GM_AVATAR_REBUILT humanScale=" + model.GetComponent<Animator>().humanScale);
        }

        void Awake() => Application.logMessageReceived += OnLog;
        void OnDestroy() => Application.logMessageReceived -= OnLog;
        void OnLog(string message, string stack, LogType type)
        {
            if (!ending && (type == LogType.Exception || type == LogType.Error || type == LogType.Assert))
                Fail(message);
        }

        void Fail(string reason)
        {
            if (ending) return;
            ending = true;
            Debug.LogError("GM_GOD_TABS_FAILED " + reason);
            Finish(1);
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 15f;
            RoomCreator creator = null;
            while (!creator && Time.realtimeSinceStartup < deadline)
            {
                creator = FindFirstObjectByType<RoomCreator>();
                yield return null;
            }
            if (!creator) { Fail("RoomCreator missing"); yield break; }
            int map = Array.FindIndex(creator.Maps, entry => entry && entry.Scene != null &&
                MTPSKIT.RoomCreator.IsTrainingScene(entry.Scene));
            if (map < 0) { Fail("Training scene missing"); yield break; }
            DNNetworkManager.Instance.SetTransportPort(17939);
            creator.LaunchFromFrontEnd(map, 0, false);
            deadline = Time.realtimeSinceStartup + 40f;
            CharacterInstance owner = null;
            while (!owner && Time.realtimeSinceStartup < deadline)
            {
                owner = GameManager.myPlayerInstance ? GameManager.myPlayerInstance.MyCharacter : null;
                yield return null;
            }
            if (!owner) { Fail("Host character missing"); yield break; }
            yield return new WaitForSeconds(1f);
            owner.RangeInvulnerable = true;
            var lab = FindFirstObjectByType<RangeResearchLab>();
            if (!lab) { Fail("Training GM missing"); yield break; }
            var canvas = FrontEndUI.Canvas(transform, 200);
            var browser = JournalBrowser.Open(canvas.transform, () => { }, lab.ExecuteCatalog, _ => true, lab.RemoveCatalog);
            browser.SetCombatControls(() => false, lab.SetMonsterCombat);
            var godTab = Button(browser, "神祇");
            var monsterTab = Button(browser, "怪物（巨人杀手）");
            if (!godTab || !monsterTab) { Fail("New tabs missing"); yield break; }
            godTab.onClick.Invoke();
            yield return null;
            if (!Button(browser, "启用基础") || !Button(browser, "启用进化") || !Button(browser, "停用技能"))
            { Fail("God skill controls missing"); yield break; }
            int tests = 0;
            foreach (string id in SkillBattlePreview.GmGodSkillIds)
            {
                foreach (bool evolved in new[] { false, true })
                {
                    string result = GodSkillGm.Activate(owner, id, evolved);
                    if (!result.StartsWith("已启用", StringComparison.Ordinal))
                    { Fail(id + " evolved=" + evolved + " " + result); yield break; }
                    var session = owner.GetComponent<GodSkillGm>();
                    if (!session.HasSkillHud || !session.ActiveSkillIcon || session.ActiveSkillId != id ||
                        session.ActiveSkillLevel != (evolved ? 4 : 1))
                    { Fail("God HUD selection mismatch: " + id); yield break; }
                    GodSkillGm.Stop(owner);
                    if (session.HasSkillHud || session.ActiveSkillLevel != 0)
                    { Fail("God HUD did not clear: " + id); yield break; }
                    tests++;
                    yield return null;
                }
            }
            Debug.Log("GM_GOD_SELECTIONS_OK " + tests + " temporary basic/evolution selections and stops; not 70 effect acceptances");

            monsterTab = Button(browser, "怪物（巨人杀手）");
            monsterTab.onClick.Invoke();
            yield return null;
            var labels = browser.GetComponentsInChildren<TMPro.TMP_Text>();
            if (!labels.Any(label => label.text == "近战骷髅") || !labels.Any(label => label.text == "蜈蚣"))
            { Fail("Two sample entries missing"); yield break; }
            if (!Button(browser, "召唤") || !Button(browser, "移除") || !Button(browser, "待机") || !Button(browser, "战斗"))
            { Fail("Monster controls missing"); yield break; }

            // All existing GM buttons retain their real callbacks, not a parallel mock menu.
            var cell = browser.GetComponentsInChildren<Button>().FirstOrDefault(button =>
                button.name == "" && button.GetComponentsInChildren<TMPro.TMP_Text>().Any(label => label.text == "近战骷髅"));
            if (!cell) { Fail("Skeleton cell missing"); yield break; }
            cell.onClick.Invoke();
            Button(browser, "召唤").onClick.Invoke();
            yield return null;
            var skeleton = FindObjectsByType<Skeleton1HCombat>(FindObjectsSortMode.None)
                .FirstOrDefault(combat => combat.GetComponent<CharacterInstance>().Team != owner.Team);
            if (!skeleton) { Fail("GM skeleton spawn failed"); yield break; }
            var victim = skeleton.GetComponent<CharacterInstance>();
            yield return new WaitForSeconds(.5f);
            ReportModel(victim);
            CaptureMonster(victim, "Skeleton");
            var earlyMilipede = JotunnMonsterGm.TrySpawn(owner, "milipede_sur");
            if (!earlyMilipede) { Fail("Milipede spawn failed"); yield break; }
            yield return new WaitForSeconds(.5f);
            ReportModel(earlyMilipede);
            CaptureMonster(earlyMilipede, "Milipede");
            JotunnMonsterGm.Remove(owner, "milipede_sur");
            Vector3 position = victim.transform.position;
            yield return new WaitForSeconds(1.25f);
            if (JotunnMonsterGm.IsFighting(owner) || Vector3.Distance(position, victim.transform.position) > .5f)
            { Fail("GM summon did not remain idle"); yield break; }
            Button(browser, "战斗").onClick.Invoke();
            yield return new WaitForSeconds(1f);
            if (!JotunnMonsterGm.IsFighting(owner) || Vector3.Distance(position, victim.transform.position) < .1f)
            { Fail("Combat did not resume movement"); yield break; }
            Button(browser, "待机").onClick.Invoke();
            // Lightning Strike hits four small areas three metres from the caster.
            victim.transform.position = owner.transform.position + owner.transform.forward * 3f;
            int health = victim.CurrentHealth;
            string cast = GodSkillGm.Activate(owner, "active_thor_A_evolve", false);
            if (!cast.StartsWith("已启用", StringComparison.Ordinal)) { Fail(cast); yield break; }
            var godSession = owner.GetComponent<GodSkillGm>();
            if (!godSession.HasSkillHud || !godSession.ActiveSkillIcon || godSession.ActiveSkillLevel != 1)
            { Fail("God skill HUD missing icon or level"); yield break; }
            yield return new WaitForSeconds(.1f);
            CaptureMonster(owner, "Lightning");
            browser.gameObject.SetActive(false);
            ScreenCapture.CaptureScreenshot(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TPS-GM-GodHud.png"));
            yield return null;
            yield return null;
            browser.gameObject.SetActive(true);
            deadline = Time.realtimeSinceStartup + 8f;
            while (victim && victim.CurrentHealth >= health && Time.realtimeSinceStartup < deadline) yield return null;
            bool presentationPlaytest = Environment.GetCommandLineArgs().Contains("-godPresentationPlaytest");
            if (presentationPlaytest && (!GodAudioReady(owner, "ThorLightningOriginal") || !LightningCueReady(owner)))
            { Fail("Basic lightning did not show its strike cue and play original audio"); yield break; }
            GodSkillGm.Stop(owner);
            if (victim && victim.CurrentHealth >= health) { Fail("God lightning dealt no real target damage"); yield break; }
            // Runtime coverage is distinct from selection coverage: no damage is a recorded
            // observation, not a pass for buffs, delayed, directional or summon-based skills.
            if (!victim) { Fail("Lightning target unexpectedly destroyed"); yield break; }
            victim.MaxHealth = victim.CurrentHealth = 100000;
            if (presentationPlaytest)
            {
                int before = victim.CurrentHealth;
                GodSkillGm.Activate(owner, SkillBattlePreview.LightningEvolution, true);
                deadline = Time.realtimeSinceStartup + 8f;
                while (victim && victim.CurrentHealth >= before && Time.realtimeSinceStartup < deadline) yield return null;
                var audio = owner.GetComponentsInChildren<AudioSource>().FirstOrDefault(source =>
                    source.name == "GM God Cast Audio" && source.clip && source.clip.name == "ThorLightningEvolvedOriginal");
                if (!victim || victim.CurrentHealth >= before || !audio || !audio.isPlaying || !LightningCueReady(owner))
                { Fail("Evolved lightning did not deal damage and play original audio"); yield break; }
                int damage = before - victim.CurrentHealth;
                GodSkillGm.Stop(owner);
                yield return null;
                if (audio || LightningCueReady(owner)) { Fail("Lightning presentation survived stopping the skill"); yield break; }
                JotunnMonsterGm.Clear(owner);
                Debug.Log($"GM_GOD_LIGHTNING_AUDIO_PASSED basic=True evolved=True cue=True evolvedDamage={damage} cleanup=True");
                yield return VerifyBlessing(owner);
                if (ending) yield break;
                ending = true;
                Finish(0);
                yield break;
            }
            var report = new System.Text.StringBuilder("# GM 神祇运行抽查\n\n每项运行 3 秒，目标固定在角色正前方 3 米。未扣血不等于技能失效；增益、延时和其他命中区域需要单独验收。此表不代表原作特效完整。\n\n|技能|形态|启用|目标扣血|\n|---|---|---|---|\n");
            JotunnMonsterGm.Clear(owner);
            yield return null;
            foreach (string id in SkillBattlePreview.GmGodSkillIds)
            {
                foreach (bool evolved in new[] { false, true })
                {
                    // A lethal skill must not leave a dead or pending-destroy target for the next case.
                    victim = JotunnMonsterGm.TrySpawn(owner, "skeleton_1h_sur");
                    if (!victim)
                    {
                        Debug.LogWarning($"GM_GOD_RUNTIME_SKIP {id} evolved={evolved} targetSpawnFailed");
                        continue;
                    }
                    victim.transform.position = owner.transform.position + owner.transform.forward * 3f;
                    victim.MaxHealth = victim.CurrentHealth = 100000;
                    Physics.SyncTransforms();
                    string activation = GodSkillGm.Activate(owner, id, evolved);
                    yield return new WaitForSeconds(3f);
                    int dealt = victim ? 100000 - victim.CurrentHealth : 100000;
                    report.Append('|').Append(id).Append('|').Append(evolved ? "进化" : "基础")
                        .Append('|').Append(activation.StartsWith("已启用", StringComparison.Ordinal) ? "成功" : activation)
                        .Append('|').Append(dealt).AppendLine("|");
                    Debug.Log("GM_GOD_RUNTIME " + id + " evolved=" + evolved + " damage=" + dealt);
                    GodSkillGm.Stop(owner);
                    JotunnMonsterGm.Clear(owner);
                    yield return null;
                }
            }
            System.IO.File.WriteAllText(System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TPS-GM-GodRuntime.md"), report.ToString());
            JotunnMonsterGm.Clear(owner);
            Debug.Log("GM_GOD_DAMAGE_OK actual training skeleton idle/combat and god lightning damage");

            var milipede = JotunnMonsterGm.TrySpawn(owner, "milipede_sur");
            if (!milipede) { Fail("Milipede spawn failed"); yield break; }
            yield return new WaitForSeconds(.5f);
            ReportModel(milipede);
            CaptureMonster(milipede, "Milipede");
            if (JotunnMonsterGm.IsFighting(owner)) { Fail("Milipede not idle"); yield break; }
            if (!JotunnMonsterGm.Remove(owner, "milipede_sur")) { Fail("Milipede removal failed"); yield break; }
            yield return null;
            if (milipede) { Fail("Removed sample still exists"); yield break; }
            browser.Close();
            owner.RangeInvulnerable = false;
            ending = true;
            Debug.Log("GM_GOD_TABS_PASSED current project training, 35x2 selections, real lightning damage, two monster entries");
            DNNetworkManager.Instance.StopHost();
            Finish(0);
        }

        IEnumerator VerifyBlessing(CharacterInstance owner)
        {
            int originalHealth = owner.CurrentHealth;
            int injured = owner.MaxHealth / 2;
            int expectedHealing = Mathf.Max(1, Mathf.RoundToInt(owner.MaxHealth * .05f));
            foreach (bool evolved in new[] { false, true })
            {
                owner.CurrentHealth = injured;
                string activation = GodSkillGm.Activate(owner, SkillBattlePreview.BlessingEvolution, evolved);
                if (!activation.StartsWith("已启用", StringComparison.Ordinal))
                { Fail(activation); yield break; }
                float deadline = Time.realtimeSinceStartup + 4f;
                bool audio = false, cue = false;
                while (Time.realtimeSinceStartup < deadline && owner.CurrentHealth < injured + expectedHealing)
                {
                    audio |= GodAudioReady(owner, "FreyaBlessingOriginal");
                    cue |= owner.GetComponentsInChildren<LineRenderer>().Any(line =>
                        line.name == "GM skill indicator" && line.enabled);
                    yield return null;
                }
                int healed = owner.CurrentHealth - injured;
                GodSkillGm.Stop(owner);
                yield return null;
                bool cleared = !GodAudioReady(owner, "FreyaBlessingOriginal");
                if (healed < expectedHealing || !audio || !cue || !cleared)
                {
                    owner.CurrentHealth = originalHealth;
                    Fail($"Blessing evolved={evolved} heal={healed} cue={cue} audio={audio} cleanup={cleared}");
                    yield break;
                }
                Debug.Log($"GM_GOD_BLESSING_PASSED evolved={evolved} healed={healed} cue=True audio=True cleanup=True");
            }
            owner.CurrentHealth = originalHealth;
        }

        static bool GodAudioReady(CharacterInstance owner, string clipName) =>
            owner.GetComponentsInChildren<AudioSource>().Any(source =>
                source.name == "GM God Cast Audio" && source.clip && source.clip.name == clipName && source.isPlaying);

        static bool LightningCueReady(CharacterInstance owner) =>
            owner.GetComponentsInChildren<LineRenderer>().Any(line => line.name == "GM Lightning Strike" && line.enabled);

        static void ReportModel(CharacterInstance actor)
        {
            var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
            if (!visual) throw new InvalidOperationException("Spawned monster has no visual");
            foreach (var renderer in visual.GetComponentsInChildren<Renderer>(true))
            {
                Debug.Log("GM_MONSTER_RENDER " + actor.name + " mesh=" + renderer.name +
                    " enabled=" + renderer.enabled + " active=" + renderer.gameObject.activeInHierarchy +
                    " hidden=" + renderer.forceRenderingOff + " bounds=" + renderer.bounds +
                    " scale=" + renderer.transform.lossyScale);
                if (renderer.bounds.size.magnitude < .5f)
                    throw new InvalidOperationException("Monster model is smaller than half a metre: " + renderer.bounds);
            }
            foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var baked = new Mesh();
                skin.BakeMesh(baked);
                Debug.Log("GM_MONSTER_SKIN " + skin.name + " mesh=" + skin.sharedMesh.bounds +
                    " baked=" + baked.bounds + " root=" + (skin.rootBone ? skin.rootBone.lossyScale.ToString() : "none") +
                    " avatarScale=" + (visual.GetComponent<Animator>().isHuman ? visual.GetComponent<Animator>().humanScale : -1f));
                Destroy(baked);
            }
        }

        static void CaptureMonster(CharacterInstance actor, string label)
        {
            var source = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (!source) throw new InvalidOperationException("No gameplay camera for visual proof");
            var host = new GameObject("GM validation capture");
            var camera = host.AddComponent<Camera>();
            camera.CopyFrom(source);
            camera.enabled = false;
            camera.transform.position = actor.transform.position + new Vector3(3f, 2.2f, -4f);
            camera.transform.LookAt(actor.transform.position + Vector3.up);
            camera.fieldOfView = 45f;
            var target = RenderTexture.GetTemporary(800, 600, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target;
            camera.Render();
            RenderTexture.active = target;
            var image = new Texture2D(800, 600, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, 800, 600), 0, 0);
            image.Apply();
            string path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "TPS-GM-" + label + ".png");
            System.IO.File.WriteAllBytes(path, image.EncodeToPNG());
            RenderTexture.active = previous;
            camera.targetTexture = null;
            RenderTexture.ReleaseTemporary(target);
            Destroy(image);
            Destroy(host);
            Debug.Log("GM_VISUAL_CAPTURE " + path);
        }

        static Button Button(JournalBrowser browser, string label) => browser.GetComponentsInChildren<Button>()
            .FirstOrDefault(button => button.name == label);
    }
}
#endif
