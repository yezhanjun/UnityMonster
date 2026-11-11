using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace MTPSKIT.UI
{

    /// <summary>
    /// component for option panel with mouse sensitivity and audio volume slider
    /// </summary>
    public class ControlSettingsUI : MonoBehaviour
    {
        [Header("MouseSensitivity")]
        [SerializeField] InputField _mouseSensInput;
        [SerializeField] Slider _mouseSensSlider;

        [SerializeField] InputField _mainAudioVolumeInput;
        [SerializeField] Slider _mainAudioVolumeSlider;

        [SerializeField] AudioMixer _mainAudioMixer;

        [SerializeField] Button _applyChanges;

        string _playerPrefs_Sensitivity = "Sensitivity";
        string _playerPrefs_MainAdioVolume = "MainAudioVolume";

        void Start()
        {
            LoadUserPreferences();

            _mouseSensSlider.onValueChanged.AddListener(UI_MouseSensitivitySlider);
            _mouseSensInput.onEndEdit.AddListener(UI_MouseSensitivityInputField);

            _mainAudioVolumeSlider.onValueChanged.AddListener(UI_MainAudioVolumeSlider);
            _mainAudioVolumeInput.onEndEdit.AddListener(UI_MainAudioVolumeField);

            _applyChanges.onClick.AddListener(ApplyChanges);
        }

        #region mouse sensitivity

        //prefix UI_ for methods launcher by UI elements
        void UI_MouseSensitivitySlider(float value)
        {
            _mouseSensInput.text = _mouseSensSlider.value.ToString();
        }
        void UI_MouseSensitivityInputField(string value)
        {
            if (value != string.Empty)
            {
                float newValue = System.Convert.ToSingle(value.Replace(",","."));

                if (newValue >= 0)
                {
                    newValue = Mathf.Clamp(newValue, _mouseSensSlider.minValue, _mouseSensSlider.maxValue);
                    _mouseSensSlider.value = newValue;
                    _mouseSensInput.text = newValue.ToString();
                }
                else
                {
                    _mouseSensSlider.value = 0;
                    _mouseSensInput.text = "0".ToString();
                }
            }
            else
            {
                _mouseSensSlider.value = 0;
                _mouseSensInput.text = "0".ToString();
            }
        }
        #endregion

        #region MainAudioVolume

        void UI_MainAudioVolumeSlider(float value)
        {
            _mainAudioVolumeInput.text = _mainAudioVolumeSlider.value.ToString();
        }
        void UI_MainAudioVolumeField(string value)
        {
            if (value != string.Empty)
            {
                float newValue = System.Convert.ToSingle(value.Replace(",", "."));

                if (newValue >= 0)
                {
                    newValue = Mathf.Clamp(newValue, _mainAudioVolumeSlider.minValue, _mainAudioVolumeSlider.maxValue);
                    _mainAudioVolumeSlider.value = newValue;
                    _mainAudioVolumeInput.text = newValue.ToString();
                }
                else
                {
                    _mainAudioVolumeSlider.value = 0;
                    _mainAudioVolumeInput.text = "0".ToString();
                }
            }
            else
            {
                _mainAudioVolumeSlider.value = 0;
                _mainAudioVolumeInput.text = "0".ToString();
            }
        }

        #endregion


        void ApplyChanges()
        {
            //save changes to player prefs so game will remember them after restart
            PlayerPrefs.SetFloat(_playerPrefs_Sensitivity, _mouseSensSlider.value);
            PlayerPrefs.SetFloat(_playerPrefs_MainAdioVolume, _mainAudioVolumeSlider.value);

            //apply changes
            LoadUserPreferences();
        }
        void LoadUserPreferences()
        {
            UserSettings.MouseSensitivity = PlayerPrefs.GetFloat(_playerPrefs_Sensitivity);

            if (UserSettings.MouseSensitivity == 0) UserSettings.MouseSensitivity = 0.1f;

            //update UI with player preferences
            _mouseSensSlider.value = UserSettings.MouseSensitivity;
            _mouseSensInput.text = UserSettings.MouseSensitivity.ToString();

            float mainAudioVolume = PlayerPrefs.GetFloat(_playerPrefs_MainAdioVolume);

            //set game audio volume to user preference 
            float db = Mathf.Log10(mainAudioVolume)*20;
            _mainAudioMixer.SetFloat("MasterVolume", db);


            _mainAudioVolumeSlider.value = mainAudioVolume;
            _mainAudioVolumeInput.text = mainAudioVolume.ToString();
        }
    }
}
