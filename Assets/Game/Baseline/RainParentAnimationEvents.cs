using UnityEngine;

namespace MTPSKIT.Gameplay
{
    // Only RefreshAllIK is ready. Do not install the other original events until their receivers exist.
    public sealed class RainParentAnimationEvents : MonoBehaviour
    {
        RainParentIKTargetPassive[] targets;

        void Awake() => RefreshTargetCache();

        // Call after changing the owned model hierarchy; no per-animation-event array allocation.
        public void RefreshTargetCache() => targets = GetComponentsInChildren<RainParentIKTargetPassive>(true);

        public void RefreshAllIK()
        {
            foreach (var target in targets)
            {
                // Original GetComponentsInChildren excludes inactive GameObjects, not disabled behaviours.
                if (!target || !target.gameObject.activeInHierarchy) continue;
                target.UpdateIKTargetPosition();
                target.UpdateYOffset();
            }
        }
    }
}
