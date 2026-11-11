using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
namespace MTPSKIT.UI {
    public class FocusedInputField : InputField
    {
        private void Update()
        {
            if (Input.GetMouseButtonUp(0))
            {
                FocusInputField();
            }
        }
        public void FocusInputField()
        {
            EventSystem.current.SetSelectedGameObject(gameObject, null);
            OnPointerClick(new PointerEventData(EventSystem.current));
        }

    }
}
