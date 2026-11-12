using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace MTPSKIT.UI.HUD{
    public class UIWorldIcon : MonoBehaviour
    {

        protected Transform target;
        protected Camera mainCamera;
        bool olwaysOnScreen = true;

        float minX;
        float maxX;

        float minY;
        float maxY;

        public float iconPixelBorder;

        [SerializeField] protected GameObject overlay;

        protected Vector3 correctionPosition;

        protected virtual void Update()
        {
            if (target)
            {
                Vector2 pos = mainCamera.WorldToScreenPoint(target.position+target.rotation*correctionPosition);

                if (Vector3.Dot((target.position - mainCamera.transform.position), mainCamera.transform.forward) < 0)
                {
                    if (pos.x < Screen.width)
                    {
                        pos.x = maxX;
                    }
                    else
                    {
                        pos.x = minX;
                    }
                }


                pos.x = Mathf.Clamp(pos.x, minX, maxX);
                pos.y = Mathf.Clamp(pos.y, minY, maxY);

                transform.position = pos;
            }
        }
        protected virtual void InitializeWorldIcon(Transform _target, bool _alwaysOnScreen)
        {
            olwaysOnScreen = _alwaysOnScreen;
            mainCamera = GameplayCamera._instance.GetComponent<Camera>();

            target = _target;

            if (_alwaysOnScreen)
            {
                minX = iconPixelBorder;
                maxX = Screen.width - iconPixelBorder;

                minY = iconPixelBorder;
                maxY = Screen.height - iconPixelBorder;
            }
            else 
            {
                minX = -9999;
                maxX = -minX;
                minY = minX;
                maxY = -minX;
            }
        }

        public void RenderIcon(bool _render) 
        {
            overlay.SetActive(_render);
        }
    }
}
