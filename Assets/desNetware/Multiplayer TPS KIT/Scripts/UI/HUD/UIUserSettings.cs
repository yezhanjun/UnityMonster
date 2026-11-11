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
    public class UIUserSettings : MonoBehaviour
    {
        [Header("MouseSensitivity")]
        [SerializeField] UISliderInputOption _mouseSensSlider;

        [Header("Audio")]
        [SerializeField] UISliderInputOption _mainAudioVolumeSlider;

        [SerializeField] AudioMixer _mainAudioMixer;



        [Header("Visuals")]
        [SerializeField] UISliderInputOption _fovSlider;

        [Header("Universal")]
        [SerializeField] Button _applyChanges;

        //player prefs keys
        string _playerPrefs_Sensitivity = "Sensitivity";
        string _playerPrefs_MainAdioVolume = "MainAudioVolume";

        string _playerPrefs_FOV = "FOV";

        void Start()
        {

            _applyChanges.onClick.AddListener(ApplyChanges);

            LoadUserPreferences();
        }

        void ApplyChanges()
        {
            //save changes to player prefs so game will remember them after restart
            PlayerPrefs.SetFloat(_playerPrefs_Sensitivity, _mouseSensSlider.Value());
            PlayerPrefs.SetFloat(_playerPrefs_MainAdioVolume, _mainAudioVolumeSlider.Value());

            PlayerPrefs.SetFloat(_playerPrefs_FOV, _fovSlider.Value());
            //PlayerPrefs.SetFloat(_playerPrefs_FPPFOV, _fppfovSlider.Value());

            //apply changes
            LoadUserPreferences();
        }
        void LoadUserPreferences()
        {
            UserSettings.MouseSensitivity = PlayerPrefs.GetFloat(_playerPrefs_Sensitivity);

            if (UserSettings.MouseSensitivity == 0) UserSettings.MouseSensitivity = 1f;

            //update UI with player preferences
            _mouseSensSlider.SetValue(UserSettings.MouseSensitivity);

            float mainAudioVolume = PlayerPrefs.GetFloat(_playerPrefs_MainAdioVolume);

            //set game audio volume to user preference 
            float db = Mathf.Log10(mainAudioVolume)*20;
            _mainAudioMixer.SetFloat("MasterVolume", db);

            _mainAudioVolumeSlider.SetValue(mainAudioVolume);

            //fov
            float fov = PlayerPrefs.GetFloat(_playerPrefs_FOV);
            if (fov == 0) 
                fov = 60;

            UserSettings.FieldOfView = fov;
            _fovSlider.SetValue(fov);
        }
    }
}
