using System;
using System.Collections;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Explicit development acceptance. Measures planted-foot drift rather than guessing a speed multiplier.
    public sealed class HeroMotionDiagnostics : MonoBehaviour
    {
        CharacterInstance character;
        bool driving;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!LocalAcceptanceClient.Requested || Array.IndexOf(Environment.GetCommandLineArgs(), "--hero-motion-diagnostics") < 0) return;
            var go = new GameObject("Hero motion diagnostics"); DontDestroyOnLoad(go); go.AddComponent<HeroMotionDiagnostics>();
        }
        void Update()
        {
            if (!driving || !character) return;
            character.lookInput = Vector2.zero;
            character.SetMovementInput(Vector2.up);
            character.SetActionKeyCode(ActionCodes.LockedLocomotion, false);
        }
        IEnumerator Start()
        {
            float deadline = Time.realtimeSinceStartup + 90f;
            while ((!GameManager.myPlayerInstance || !GameManager.myPlayerInstance.MyCharacter) && Time.realtimeSinceStartup < deadline) yield return null;
            character = GameManager.myPlayerInstance.MyCharacter;
            GameManager.myPlayerInstance.ServerSetRangeHero(12);
            character.CharacterItemManager.ServerEquipTestWeapon(Resources.Load<GameObject>("FpsRifle"));
            yield return new WaitForSeconds(2f);
            var input = PlayerGameplayInput.Instance; input.enabled = false;
            character.RangeInvulnerable = true; driving = true;
            yield return new WaitForSeconds(.6f);
            var display = character.GetComponent<RangeRobotVisual>();
            var left = display.DisplayAnimator.GetBoneTransform(HumanBodyBones.LeftFoot);
            var right = display.DisplayAnimator.GetBoneTransform(HumanBodyBones.RightFoot);
            var lastLeft = left.position; var lastRight = right.position; var lastRoot = character.transform.position;
            for (int i = 0; i < 90; i++)
            {
                yield return new WaitForEndOfFrame();
                float dt = Mathf.Max(.001f, Time.deltaTime);
                Debug.Log("RUN_PROBE phase=" + display.RiflePresentation.GaitPhase + " run=" + display.RiflePresentation.RunWeight +
                    " root=" + ((character.transform.position - lastRoot).z / dt) + " left=" + ((left.position-lastLeft).z/dt) +
                    " right=" + ((right.position-lastRight).z/dt) + " ly=" + left.position.y + " ry=" + right.position.y +
                    " scale=" + display.DisplayAnimator.transform.lossyScale + " human=" + display.DisplayAnimator.humanScale);
                lastLeft = left.position; lastRight = right.position; lastRoot = character.transform.position;
            }
            driving = false; character.SetMovementInput(Vector2.zero); input.enabled = true;
            Debug.Log("RUN_PROBE_COMPLETE");
        }
    }
}
