using System;
using System.Collections;
using Mirror;
using MTPSKIT.Gameplay;
using MTPSKIT.UI;
using UnityEngine;
using UnityEngine.AI;

namespace MTPSKIT.Battlefield
{
    [DefaultExecutionOrder(10000)]
    public sealed class BattlefieldHealthBarsCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-battlefieldHealthBarsCheck") < 0) return;
            var root = new GameObject("BattlefieldHealthBarsCheck_TEST");
            DontDestroyOnLoad(root);
            root.AddComponent<BattlefieldHealthBarsCheck>();
        }

        BattlefieldEntity tower, minion;
        Transform cameraAnchor;
        Vector3 viewPosition, viewFocus;
        bool viewReady;
        void LateUpdate()
        {
            if (!tower || !cameraAnchor || !viewReady || !GameplayCamera._instance) return;
            GameplayCamera._instance.SetTarget(cameraAnchor);
            cameraAnchor.SetPositionAndRotation(viewPosition, Quaternion.LookRotation(viewFocus - viewPosition));
            GameplayCamera._instance.transform.SetPositionAndRotation(cameraAnchor.position, cameraAnchor.rotation);
        }

        bool FindView(bool includeMinion)
        {
            var towerCenter = tower.Health.GetPositionToAttack();
            var minionCenter = minion.Health.GetPositionToAttack();
            for (int height = 0; height < 3; height++)
            for (int i = 0; i < 16; i++)
            {
                float angle = i * Mathf.PI / 8;
                var candidate = towerCenter + new Vector3(Mathf.Sin(angle) * 12, 3 + height * 2, Mathf.Cos(angle) * 12);
                if (Physics.CheckSphere(candidate, .4f, 1, QueryTriggerInteraction.Ignore) ||
                    Physics.Linecast(candidate, towerCenter, 1, QueryTriggerInteraction.Ignore) ||
                    includeMinion && Physics.Linecast(candidate, minionCenter, 1, QueryTriggerInteraction.Ignore)) continue;
                viewPosition = candidate;
                viewFocus = includeMinion ? (towerCenter + minionCenter) * .5f : towerCenter;
                return true;
            }
            return false;
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 80;
            BattlefieldGamemode mode;
            while (Time.realtimeSinceStartup < deadline && (!(mode = BattlefieldGamemode.Instance) ||
                mode.Entities.Count < 18 || !ClientFrontend.GamemodeUI || !NetworkClient.localPlayer || GameManager.Players.Count < 2)) yield return null;
            mode = BattlefieldGamemode.Instance;
            if (!mode || !ClientFrontend.GamemodeUI) { Debug.LogError("BATTLEFIELD_HEALTHBARS_FAIL not ready"); yield break; }
            var bars = ClientFrontend.GamemodeUI.GetComponent<BattlefieldHealthBars>();
            foreach (var entity in mode.Entities)
            {
                if (entity.Health.Team != 1) continue;
                if (!tower && entity.Kind == BattlefieldKind.Tower && entity.SourceId == "WaveTowerFactory4") tower = entity;
                if (!minion && entity.Kind == BattlefieldKind.Minion) minion = entity;
            }
            if (!bars || !tower || !minion) { Debug.LogError("BATTLEFIELD_HEALTHBARS_FAIL entities/UI missing"); yield break; }
            cameraAnchor = new GameObject("HealthBarsTestCamera").transform;
            cameraAnchor.SetParent(transform, false);
            if (NetworkServer.active)
            {
                // Freeze only this test minion and place it beside the tower for a stable visual check.
                minion.GetComponent<BattlefieldEntity>().enabled = false;
                var agent = minion.GetComponent<NavMeshAgent>();
                if (!FindView(false)) { Debug.LogError("BATTLEFIELD_HEALTHBARS_FAIL no open camera view"); yield break; }
                var direction = viewPosition - tower.transform.position;
                direction.y = 0;
                direction.Normalize();
                var placement = tower.transform.position + direction * 5 + Vector3.Cross(Vector3.up, direction) * 2;
                if (NavMesh.SamplePosition(placement, out var hit, 8, NavMesh.AllAreas)) agent.Warp(hit.position);
                agent.isStopped = true;
                minion.GetComponent<CharacterInstance>().SetMovementInput(Vector2.zero);
                viewReady = FindView(true);
                yield return new WaitForSecondsRealtime(3);
                var attacker = NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter;
                tower.BuildingState = BattlefieldBuildingState.Active;
                tower.Health.Server_ChangeHealthStateRaw(180, CharacterPart.body, AttackType.hitscan, attacker, 0);
                minion.Health.Server_ChangeHealthStateRaw(45, CharacterPart.body, AttackType.hitscan, attacker, 0);
            }
            else
            {
                while (Time.realtimeSinceStartup < deadline && (tower.Health.CurrentHealth != 420 || minion.Health.CurrentHealth != 55)) yield return null;
                viewReady = FindView(true);
            }
            yield return new WaitForSecondsRealtime(.5f);
            bool towerOK = bars.TryGetDisplayedHealth(tower, out int towerHP, out float towerRatio, out bool towerVisible) && towerHP == 420 && Mathf.Abs(towerRatio - .7f) < .001f;
            bool minionOK = bars.TryGetDisplayedHealth(minion, out int minionHP, out float minionRatio, out bool minionVisible) && minionHP == 55 && Mathf.Abs(minionRatio - .55f) < .001f;
            Debug.Log($"BATTLEFIELD_HEALTHBARS_{(towerOK && minionOK && towerVisible && minionVisible ? "PASS" : "FAIL")} server={NetworkServer.active} tower={towerHP} ratio={towerRatio:F2} visible={towerVisible} minion={minionHP} ratio={minionRatio:F2} visible={minionVisible}");
            if (!NetworkServer.active) yield break;
            yield return new WaitForSecondsRealtime(10);
            var source = NetworkClient.localPlayer.GetComponent<PlayerInstance>().MyCharacter;
            tower.BuildingState = BattlefieldBuildingState.Active;
            tower.Health.Server_ChangeHealthStateRaw(99999, CharacterPart.body, AttackType.hitscan, source, 0);
            minion.Health.Server_ChangeHealthStateRaw(99999, CharacterPart.body, AttackType.hitscan, source, 0);
            yield return new WaitForSecondsRealtime(.5f);
            bool removed = !bars.TryGetDisplayedHealth(tower, out _, out _, out _) && !bars.TryGetDisplayedHealth(minion, out _, out _, out _);
            Debug.Log("BATTLEFIELD_HEALTHBARS_DEATH_" + (removed ? "PASS" : "FAIL"));
        }
    }
}
