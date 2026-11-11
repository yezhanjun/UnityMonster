using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MTPSKIT.UI
{
    public class UISliderInputOption : MonoBehaviour
    {
        [SerializeField] Slider _slider;
        [SerializeField] InputField _inputField;
        [SerializeField] float _upperLimit = 100f;
        [SerializeField] float _lowerLimit = 0;

        private void Awake()
        {
            _slider.onValueChanged.AddListener(OnValueChanged_Slider);
            _inputField.onEndEdit.AddListener(OnValueChanged_InputField);

            _inputField.contentType = InputField.ContentType.DecimalNumber;

            _slider.minValue = _lowerLimit;
            _slider.maxValue = _upperLimit;
        }
        public float Value() { return _slider.value; }

        public void SetValue(float value)
        {
            _slider.minValue = _lowerLimit;
            _slider.maxValue = _upperLimit;

            value = Mathf.Clamp(value, _lowerLimit, _upperLimit);

            _slider.value = value;
            _inputField.text = value.ToString();
        }

        void OnValueChanged_InputField(string text)
        {
            float value = (float)System.Convert.ToDouble(_inputField.text);
            value = Mathf.Clamp(value, _lowerLimit, _upperLimit);

            _slider.value = value;
        }
        void OnValueChanged_Slider(float value)
        {
            _inputField.text = _slider.value.ToString();
        }
    }
}