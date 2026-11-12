using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    /// <summary>
    /// script responsible for assigning hitboxes to character model
    /// </summary>
    public class HitboxSetup : MonoBehaviour
    {
        List<Transform> myHitboxes = new List<Transform>();
        public void SetHiboxes(GameObject _armatureRoot, Health _health)
        {
            GameTools.SetLayerRecursively(gameObject, 8);

            foreach (Transform t in transform.GetComponentsInChildren<Transform>(true))
            {
                Transform hitboxParent = GameTools.GetChildByName(_armatureRoot, t.name);

                if (hitboxParent)
                {
                    t.SetParent(hitboxParent);
                    t.SetPositionAndRotation(hitboxParent.position, hitboxParent.rotation);
                    t.GetComponent<HitBox>()._health = _health;
                    t.name = $"hitbox: {t.name}";
                    myHitboxes.Add(t);
                }
                else
                {
                    t.gameObject.SetActive(false);
                }
            }
        }
        public void DisableHitboxes()
        {
            foreach (Transform hitbox in myHitboxes)
            {
                hitbox.gameObject.SetActive(false);
            }
        }
    }
}