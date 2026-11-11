using System;
using System.Collections;
using Mirror;
using MTPSKIT;
using MTPSKIT.Battlefield;
using MTPSKIT.Gameplay;
using MTPSKIT.UI.HUD;
using UnityEngine;

namespace MTPSKIT.UI.DamageNumbers
{
    // Explicit test profile: controlled damage and a camera focused on the opponent.
    [DefaultExecutionOrder(10000)]
    public sealed class DamageNumbersRuntimeCheck : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-damageNumbersCheck") < 0) return;
            var root = new GameObject("DamageNumbersRuntimeCheck_TEST");
            DontDestroyOnLoad(root);
            root.AddComponent<DamageNumbersRuntimeCheck>();
        }

        PlayerInstance mine, opponent;
        Transform cameraRig;
        Camera testCamera;
        void LateUpdate()
        {
            if (!cameraRig || !opponent || !opponent.MyCharacter || !testCamera) return;
            GameplayCamera._instance.SetTarget(cameraRig);
            var focus = opponent.MyCharacter.GetPositionToAttack();
            cameraRig.SetPositionAndRotation(focus + new Vector3(4, 1.8f, 6), Quaternion.LookRotation(new Vector3(-4, -1.8f, -6)));
            testCamera.transform.SetPositionAndRotation(cameraRig.position, cameraRig.rotation);
        }

        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 75;
            while (Time.realtimeSinceStartup < deadline && (!NetworkClient.localPlayer || GameManager.Players.Count < 2 ||
                !UICharacter._instance || !UICharacter._instance.DamageNumbers || !UICharacter._instance.DamageNumbers.Ready)) yield return null;
            if (!NetworkClient.localPlayer || GameManager.Players.Count < 2 || !UICharacter._instance || !UICharacter._instance.DamageNumbers.Ready)
            { Debug.LogError("DAMAGE_NUMBERS_FAIL session/UI not ready"); yield break; }
            mine = NetworkClient.localPlayer.GetComponent<PlayerInstance>();
            foreach (var player in GameManager.Players.Values) if (player != mine) { opponent = player; break; }
            while (Time.realtimeSinceStartup < deadline && (!mine.MyCharacter || !opponent || !opponent.MyCharacter)) yield return null;
            if (!mine.MyCharacter || !opponent || !opponent.MyCharacter) { Debug.LogError("DAMAGE_NUMBERS_FAIL characters not ready"); yield break; }
            cameraRig = new GameObject("DamageNumbersTestCameraAnchor").transform;
            cameraRig.SetParent(transform, false);
            GameplayCamera._instance.SetTarget(cameraRig);
            testCamera = GameplayCamera._instance.GetComponent<Camera>();
            var presenter = UICharacter._instance.DamageNumbers;
            yield return new WaitForSecondsRealtime(3);
            if (!NetworkServer.active)
            {
                while (Time.realtimeSinceStartup < deadline && presenter.Presented < 1) yield return null;
                Debug.Log("DAMAGE_NUMBERS_CLIENT_" + (presenter.Presented == 1 ? "PASS" : "FAIL") + " ownDamageEvents=" + presenter.Presented);
                yield break;
            }

            int initial = presenter.Presented;
            var source = mine.MyCharacter;
            var victim = opponent.MyCharacter;
            LateUpdate();
            victim.Server_ChangeHealthStateRaw(17, CharacterPart.body, AttackType.hitscan, source, 0);
            yield return new WaitForSecondsRealtime(.15f);
            victim.Server_ChangeHealthStateRaw(31, CharacterPart.head, AttackType.hitscan, source, 0);
            yield return new WaitForSecondsRealtime(.15f); // Host RPC callbacks are delivered by the network loop.
            bool own = presenter.Presented - initial == 2 && presenter.SingleCount == 2 && presenter.TotalCount == 1;
            source.Server_ChangeHealthStateRaw(23, CharacterPart.body, AttackType.hitscan, victim, 0);
            source.Server_ChangeHealthStateRaw(5, CharacterPart.body, AttackType.falldamage, source, 0);
            yield return new WaitForSecondsRealtime(.15f);
            bool self = presenter.Presented - initial == 2;
            var mode = BattlefieldGamemode.Instance;
            var protectedBase = mode.Buildings["Wave_BaseCampGR"];
            int hp = protectedBase.Health.CurrentHealth;
            protectedBase.Health.Server_ChangeHealthStateRaw(9999, CharacterPart.body, AttackType.hitscan, source, 0);
            yield return new WaitForSecondsRealtime(.1f);
            bool shield = protectedBase.Health.CurrentHealth == hp && presenter.Presented - initial == 2;
            Debug.Log($"DAMAGE_NUMBERS_PIPELINE_{(own && self && shield ? "PASS" : "FAIL")} own={own} incomingAndSelfIgnored={self} shield={shield} shown={presenter.Presented - initial} owned={source.isOwned} projection={testCamera.WorldToScreenPoint(victim.GetPositionToAttack())}");

            // Leave repeated regular/headshot examples visible long enough for visual inspection.
            for (int i = 0; i < 12; i++)
            {
                yield return new WaitForSecondsRealtime(.5f);
                victim.Server_ChangeHealthStateRaw(3, i % 2 == 0 ? CharacterPart.head : CharacterPart.body, AttackType.hitscan, source, 0);
            }
            for (int i = 0; i < 60; i++) victim.Server_ChangeHealthStateRaw(1, CharacterPart.body, AttackType.hitscan, source, 0);
            yield return new WaitForSecondsRealtime(.15f);
            bool bounded = presenter.SingleCount <= 48 && presenter.TotalCount <= 8 && presenter.SinglesCreated <= 48;
            yield return new WaitForSecondsRealtime(2);
            bool recycled = presenter.SingleCount == 0 && presenter.TotalCount == 0;
            int before = presenter.Presented;
            LateUpdate();
            victim.Server_ChangeHealthStateRaw(2, CharacterPart.body, AttackType.hitscan, source, 0);
            yield return new WaitForSecondsRealtime(.15f);
            bool reused = presenter.Presented == before + 1 && presenter.SinglesCreated <= 48;
            presenter.Clear();
            bool clear = presenter.SingleCount == 0 && presenter.TotalCount == 0;
            Debug.Log($"DAMAGE_NUMBERS_POOL_{(bounded && recycled && reused && clear ? "PASS" : "FAIL")} bounded={bounded} recycled={recycled} reused={reused} clear={clear} singlesCreated={presenter.SinglesCreated} totalsCreated={presenter.TotalsCreated}");
        }
    }
}
