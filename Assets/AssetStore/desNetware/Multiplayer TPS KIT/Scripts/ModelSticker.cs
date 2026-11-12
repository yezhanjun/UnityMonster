using UnityEngine;

namespace MTPSKIT
{
    public class ModelSticker : MonoBehaviour
    {

        /// <summary>
        /// object that this object will stick to, for example item to player hand
        /// </summary>
        Transform _transformToStickTo;
        Vector3 _rotationCorrector;
        public void SetSticker(Transform transformToStickTo, Vector3 rotationCorrector)
        {
            _transformToStickTo = transformToStickTo;
            _rotationCorrector = rotationCorrector;
        }
        
        void Update()
        {
            if (_transformToStickTo)
            {
                transform.position = _transformToStickTo.position;
                transform.rotation = _transformToStickTo.rotation;
                transform.Rotate(_rotationCorrector);
            }
        }
    }
}