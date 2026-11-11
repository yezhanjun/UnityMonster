using System.Collections;
using System.Linq;
using Mirror;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    public sealed partial class RangeGmSmokeTest
    {
        IEnumerator VerifyParentReplica()
        {
            const string marker = "RAIN_CLIENT_PARENT";
            float deadline = Time.realtimeSinceStartup + 65f;
            CharacterInstance enemy = null;
            while (!enemy && NetworkClient.active && Time.realtimeSinceStartup < deadline)
            {
                enemy = FindObjectsByType<CharacterInstance>(FindObjectsSortMode.None)
                    .FirstOrDefault(candidate => candidate.RainEnemyKind == RangeResearchEnemy.ParentKind + 1);
                yield return null;
            }
            if (!enemy) { Debug.LogError(marker + "_SPAWN_MISSING"); yield break; }
            uint id = enemy.netId;
            bool shape = false, slam = false, blink = false, restored = false, death = false;
            bool animatedBlink = false, moved = false;
            Vector3 blinkPosition = default;
            while (enemy && NetworkClient.isConnected && Time.realtimeSinceStartup < deadline)
            {
                var model = enemy.transform.Find("mdlParent");
                var visual = enemy.GetComponent<RainActorVisual>();
                if (model && visual)
                {
                    var boxes = model.GetComponentsInChildren<HitBox>(true);
                    var controller = enemy.GetComponent<CharacterController>();
                    var motor = enemy.GetComponent<CharacterMotor>();
                    shape |= boxes.Length == 13 && boxes.All(box => box._health == enemy && box.part == CharacterPart.body) &&
                        Mathf.Abs(controller.radius - 1.5f) < .001f && Mathf.Abs(controller.height - 8.6f) < .001f &&
                        Mathf.Abs(model.localPosition.y + .19f) < .001f;
                    slam |= enemy.RainEnemyAnimation == RangeResearchEnemy.ParentSlamAnimation && enemy.RainTelegraph != 0;
                    bool hidden = enemy.RainParentBlinkUntil > NetworkTime.time && visual.ModelRenderingHidden;
                    if (hidden)
                    {
                        if (!blink) blinkPosition = enemy.transform.position;
                        blink |= boxes.Length == 13 && boxes.All(box => !box.GetComponent<Collider>().enabled) &&
                            !controller.enabled && !motor.enabled;
                        animatedBlink |= model.gameObject.activeSelf && model.GetComponent<Animator>().isActiveAndEnabled &&
                            enemy.RainEnemyAnimation == RangeResearchEnemy.ParentSlamAnimation;
                    }
                    if (blink && enemy.CurrentHealth > 0 && enemy.RainParentBlinkUntil == 0 && !visual.ModelRenderingHidden &&
                        controller.enabled && boxes.All(box => box.GetComponent<Collider>().enabled))
                    {
                        restored = true;
                        moved |= Vector3.ProjectOnPlane(enemy.transform.position - blinkPosition, Vector3.up).magnitude >= 20f;
                    }
                    if (enemy.CurrentHealth <= 0 && boxes.Length == 13 && boxes.All(box => !box.GetComponent<Collider>().enabled))
                        death = true;
                }
                yield return null;
            }
            bool removed = !NetworkClient.spawned.ContainsKey(id);
            if (shape && slam && blink && animatedBlink && restored && moved && death && removed && NetworkClient.isConnected)
                Debug.Log(marker + "_REPLICA_CHECKED");
            else Debug.LogError($"{marker}_REPLICA_FAILED shape={shape} slam={slam} blink={blink} animated={animatedBlink} " +
                $"restored={restored} moved={moved} death={death} removed={removed}");
        }
    }
}
