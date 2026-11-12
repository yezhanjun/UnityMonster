using UnityEngine;
namespace Jotunnslayer
{
    // Approximate spin presentation; the carrier and damage logic retain ownership of movement.
    internal sealed class CultistSpinVisual : MonoBehaviour
    {
        Skeleton1HVisual visual;
        Quaternion rest;
        void Awake() { visual=GetComponent<Skeleton1HVisual>(); rest=transform.localRotation; }
        void LateUpdate()
        {
            if(visual && visual.CurrentMotion==Skeleton1HVisual.Motion.AttackAlternate)
                transform.Rotate(Vector3.up,540f*Time.deltaTime,Space.Self);
            else transform.localRotation=rest;
        }
        void OnDisable() { transform.localRotation=rest; }
    }
}
