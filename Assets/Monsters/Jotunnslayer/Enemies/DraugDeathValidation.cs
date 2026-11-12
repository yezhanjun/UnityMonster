#if UNITY_EDITOR
using System;
using System.Collections;
using MTPSKIT.Gameplay;
using Mirror;
using UnityEngine;

namespace Jotunnslayer
{
    internal static class DraugDeathValidation
    {
        internal static IEnumerator Run(CharacterInstance target, Action<bool> report)
        {
            bool passed = true;
            CharacterInstance actor = null;
            float previousScale = Time.timeScale;
            int baseline = DraugControllerResources.OwnerCount;
            int jobs = UnityEngine.Object.FindObjectsByType<DraugDeathRemoval>(FindObjectsSortMode.None).Length;
            void Check(bool value, string reason)
            {
                if (value) return;
                passed = false;
                Debug.LogError("DRAUG_DEATH_FAIL " + reason);
            }
            try
            {
                for (int mode = 0; mode < 5 && passed; mode++)
                {
                    Time.timeScale = mode == 2 ? .5f : 1f;
                    actor = Skeleton1HSpawn.TrySpawn(target, new Vector3(500, 100, 500), Quaternion.identity, Skeleton1HSpawn.Kind.DraugThrower);
                    Check(actor, "spawn " + Skeleton1HSpawn.LastSpawnError);
                    if (!actor) break;
                    actor.GetComponent<Skeleton1HCombat>().SetGmCombat(false);
                    yield return null;
                    Check(!actor.GetComponent<HumanoidDeath>() && !actor.GetComponent<RangeRobotVisual>(), "foreign player presentation remains");
                    var presenter = actor.GetComponent<DraugDeathPresentation>();
                    uint netId = actor.netId;
                    bool callback = false;
                    actor.Server_OnHealthDepleted += (part, attack, killer, force) => callback = true;
                    float start = Time.time, realStart = Time.realtimeSinceStartup;
                    actor.ServerApplyRangeItemBlast(100000, target);
                    Check(callback && actor.CurrentHealth <= 0, "real lethal callback absent");
                    Check(!actor.gameObject.activeSelf && presenter.RemovalPending, "death did not deactivate/schedule");
                    Check(DraugControllerResources.OwnerCount == baseline, "death retained controller lease");
                    foreach (var renderer in actor.GetComponentsInChildren<Renderer>(true))
                        Check(!renderer.gameObject.activeInHierarchy, "death renderer remains active");
                    if (mode == 1)
                    {
                        Time.timeScale = 0;
                        yield return new WaitForSecondsRealtime(.35f);
                        Check(actor && NetworkServer.spawned.ContainsKey(netId) && presenter.RemovalPending, "pause consumed death delay");
                        Time.timeScale = 1;
                    }
                    if (mode == 3)
                    {
                        NetworkServer.Destroy(actor.gameObject);
                        yield return null;
                        yield return null;
                        Check(!actor && !NetworkServer.spawned.ContainsKey(netId), "early network removal failed");
                    }
                    else
                    {
                        // Synthetic health recovery tests only the guard, not resurrection gameplay.
                        if (mode == 4) actor.CurrentHealth = 1;
                        float deadline = Time.realtimeSinceStartup + 8;
                        while (presenter && presenter.RemovalPending && Time.realtimeSinceStartup < deadline)
                        {
                            if (Time.time - start < 1.95f)
                                Check(actor && NetworkServer.spawned.ContainsKey(netId), "removed before scaled delay");
                            yield return null;
                        }
                        yield return null;
                        Check(Time.time - start >= 2f, "delay shorter than two scaled seconds");
                        if (mode == 2) Check(Time.realtimeSinceStartup - realStart >= 3.9f, "half speed used unscaled delay");
                        if (mode == 4)
                        {
                            Check(actor && NetworkServer.spawned.ContainsKey(netId) && !presenter.RemovalPending, "still-dead guard failed");
                            if (actor) NetworkServer.Destroy(actor.gameObject);
                            yield return null;
                        }
                        else Check(!actor && !NetworkServer.spawned.ContainsKey(netId), "automatic removal failed");
                    }
                    yield return null;
                    Check(UnityEngine.Object.FindObjectsByType<DraugDeathRemoval>(FindObjectsSortMode.None).Length == jobs, "external death job leaked");
                    Check(DraugControllerResources.OwnerCount == baseline, "lease leaked");
                    Debug.Log($"DRAUG_DEATH_CASE mode={mode} passed={passed} scaled={Time.time-start:F3} real={Time.realtimeSinceStartup-realStart:F3}");
                }
            }
            finally
            {
                Time.timeScale = previousScale;
                if (actor) NetworkServer.Destroy(actor.gameObject);
            }
            Debug.Log("DRAUG_DEATH_" + (passed ? "PASS" : "FAIL") + " cases=5 defaultDestroyOnly=True");
            report(passed);
        }
    }
}
#endif
