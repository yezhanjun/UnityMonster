using System;
using System.Collections;
using System.IO;
using System.Text;
using Mirror;
using MTPSKIT;
using MTPSKIT.Gameplay;
using UnityEngine;

namespace Jotunnslayer
{
    // Explicit flag only. Runs in the same graphical Player used for manual GM acceptance.
    public sealed class JotunnPlayerAcceptance : MonoBehaviour
    {
        bool failed;
        string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || LocalAcceptanceClient.Lane != "jotunn" ||
                Array.IndexOf(Environment.GetCommandLineArgs(), "--jotunn-acceptance-smoke") < 0) return;
            var host = new GameObject("Jotunn Player acceptance");
            DontDestroyOnLoad(host); host.AddComponent<JotunnPlayerAcceptance>();
        }

        void Awake()
        {
            output = Path.Combine(Path.GetDirectoryName(Application.dataPath), "JotunnAcceptance");
            Directory.CreateDirectory(output);
            Application.logMessageReceived += OnLog;
        }
        void OnDestroy() => Application.logMessageReceived -= OnLog;
        void OnLog(string text, string stack, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Error || type == LogType.Assert) failed = true;
        }
        void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("JOTUNN_PLAYER_ACCEPTANCE_FAILED " + message);
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 100f;
            CharacterInstance owner = null;
            while (!owner && Time.realtimeSinceStartup < deadline)
            {
                owner = GameManager.myPlayerInstance ? GameManager.myPlayerInstance.MyCharacter : null;
                yield return null;
            }
            Require(owner, "Player not ready");
            owner.RangeInvulnerable = true;
            bool input = PlayerGameplayInput.Instance && PlayerGameplayInput.Instance.enabled;
            if (PlayerGameplayInput.Instance) PlayerGameplayInput.Instance.enabled = false;
            yield return new WaitForSeconds(1f);
            foreach (string id in new[] { "skeleton_1h_sur", "milipede_sur" })
            {
                var actor = JotunnMonsterGm.TrySpawn(owner, id);
                Require(actor, id + " spawn failed: " + Skeleton1HSpawn.LastSpawnError);
                yield return new WaitForSeconds(.6f);
                var visual = actor.GetComponentInChildren<Skeleton1HVisual>();
                Require(visual, id + " visual missing");
                Require(!actor.GetComponent<HumanoidDeath>() && !actor.GetComponent<RangeRobotVisual>(), id + " still has player presentation");
                var animator = visual.GetComponent<Animator>();
                if (animator.isHuman) Require(animator.humanScale > .5f, id + " invalid human scale " + animator.humanScale);
                foreach (var skin in visual.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    Require(skin.enabled && !skin.forceRenderingOff, id + " hidden renderer");
                    Require(skin.bounds.size.magnitude > .8f && skin.bounds.size.magnitude < 8f, id + " malformed bounds " + skin.bounds);
                    Require(Vector3.Distance(skin.bounds.center, actor.transform.position) < 4f, id + " model displaced from carrier");
                    Debug.Log("JOTUNN_PLAYER_MODEL " + id + " bounds=" + skin.bounds + " humanScale=" + (animator.isHuman ? animator.humanScale : -1f));
                }
                Capture(actor.transform.position, id);
                Vector3 position = actor.transform.position;
                yield return new WaitForSeconds(.6f);
                Require(Vector3.Distance(position, actor.transform.position) < .5f, id + " idle moved");
                JotunnMonsterGm.SetCombat(owner, true);
                yield return new WaitForSeconds(1f);
                Require(Vector3.Distance(position, actor.transform.position) > .1f, id + " combat did not move");
                JotunnMonsterGm.SetCombat(owner, false);
                int corpses = FindObjectsByType<RagDoll>(FindObjectsSortMode.None).Length;
                actor.ServerApplyRaidSkillDamage(actor.MaxHealth * 4, owner);
                yield return new WaitForSeconds(.3f);
                Require(!actor, id + " dead sample not removed");
                Require(FindObjectsByType<RagDoll>(FindObjectsSortMode.None).Length == corpses, id + " created unrelated ragdoll");
                Debug.Log("JOTUNN_PLAYER_MONSTER_PASSED " + id);
            }

            var report = new StringBuilder("# 神祇客户端运行抽查\n\n每项3秒，固定目标在角色前方3米；未扣血不直接判失败，需考虑治疗、命中区域、延时与控制。指示器事件是表现接通证据，不是原作技能等价证明。\n\n|技能|形态|启用与HUD|指示器事件|实际扣血|\n|---|---|---|---|---|\n");
            int tested = 0;
            foreach (string id in SkillBattlePreview.GmGodSkillIds)
                foreach (bool evolved in new[] { false, true })
                {
                    var victim = JotunnMonsterGm.TrySpawn(owner, "skeleton_1h_sur");
                    Require(victim, "Skill target missing");
                    victim.transform.position = owner.transform.position + owner.transform.forward * 3f;
                    victim.MaxHealth = victim.CurrentHealth = 100000;
                    string result = GodSkillGm.Activate(owner, id, evolved);
                    Require(result.StartsWith("已启用", StringComparison.Ordinal), result);
                    var session = owner.GetComponent<GodSkillGm>();
                    Require(session.HasSkillHud && session.ActiveSkillIcon && session.ActiveSkillLevel == (evolved ? 4 : 1), id + " HUD invalid");
                    var runtime = owner.GetComponentInChildren<SkillBattlePreview>();
                    Require(runtime, id + " runtime missing");
                    yield return new WaitForSeconds(1f);
                    if (id == "active_loki_B_evolve" || id == "active_skadi_B_evolve")
                    {
                        Capture(owner.transform.position, id + (evolved ? "-evolved" : "-basic"));
                        // ScreenCapture completes at the end of the frame, before the HUD is cleared.
                        yield return new WaitForEndOfFrame();
                    }
                    yield return new WaitForSeconds(2f);
                    int damage = victim ? 100000 - victim.CurrentHealth : 100000;
                    report.Append('|').Append(id).Append('|').Append(evolved ? "进化" : "基础").Append("|通过|")
                        .Append(runtime.IndicatorEvents).Append('|').Append(damage).AppendLine("|");
                    File.WriteAllText(Path.Combine(output, "神祇运行抽查.md"), report.ToString());
                    Debug.Log("JOTUNN_PLAYER_GOD_OBSERVED " + id + " evolved=" + evolved + " indicators=" + runtime.IndicatorEvents + " damage=" + damage);
                    GodSkillGm.Stop(owner);
                    Require(!session.HasSkillHud, id + " HUD not cleared");
                    JotunnMonsterGm.Clear(owner);
                    tested++;
                    yield return null;
                }
            owner.RangeInvulnerable = false;
            if (PlayerGameplayInput.Instance) PlayerGameplayInput.Instance.enabled = input;
            Require(!failed, "Unexpected runtime error: inspect Player log");
            File.WriteAllText(Path.Combine(output, "result.txt"), "MONSTERS=2/2\nGOD_CONFIGURATIONS=" + tested + "/70\nRUNTIME_ERRORS=0\n");
            Debug.Log("JOTUNN_PLAYER_ACCEPTANCE_PASSED monsters=2/2 godConfigurations=" + tested + "/70");
        }

        void Capture(Vector3 center, string label)
        {
            var source = GameplayCamera._instance ? GameplayCamera._instance.GetComponent<Camera>() : Camera.main;
            if (!source) return;
            var host = new GameObject("Acceptance image camera");
            var camera = host.AddComponent<Camera>(); camera.CopyFrom(source); camera.enabled = false;
            camera.transform.position = center + new Vector3(4f, 3.5f, -5f);
            camera.transform.LookAt(center + Vector3.up);
            var target = RenderTexture.GetTemporary(960, 640, 24);
            var previous = RenderTexture.active;
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            var texture = new Texture2D(960, 640, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 960, 640), 0, 0); texture.Apply();
            File.WriteAllBytes(Path.Combine(output, label + ".png"), texture.EncodeToPNG());
            RenderTexture.active = previous; camera.targetTexture = null; RenderTexture.ReleaseTemporary(target);
            Destroy(texture); Destroy(host);
            ScreenCapture.CaptureScreenshot(Path.Combine(output, label + "-HUD.png"));
        }
    }
}
