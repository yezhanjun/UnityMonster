using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT
{
    public class PooledObject : MonoBehaviour
    {
        public virtual void OnObjectInstantiated()
        {
            gameObject.SetActive(false);
        }
        public virtual void OnObjectReused()
        {
            gameObject.SetActive(true);
        }

        //it is here to avoid using GetComponent() by gun script, for performance reasons
        public virtual void StartBullet(Vector3 targetPoint)
        {



        }
    }
}