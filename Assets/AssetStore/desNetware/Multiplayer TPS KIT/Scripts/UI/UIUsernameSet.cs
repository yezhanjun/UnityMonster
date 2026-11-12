using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;
namespace MTPSKIT
{
    public class UIUsernameSet : MonoBehaviour
    {
        [SerializeField] InputField UsernameField;

        [SerializeField] TMPro.TMP_InputField tmpUsernameField;
        const string _playerPrefs_Username = "PlayerNickname";
        void SetText(string value)
        {
            if (tmpUsernameField) tmpUsernameField.SetTextWithoutNotify(value);
            else if (UsernameField) UsernameField.SetTextWithoutNotify(value);
        }

        private void Start()
        {
            if (tmpUsernameField) tmpUsernameField.onEndEdit.AddListener(UsernameModified);
            else if (UsernameField) UsernameField.onEndEdit.AddListener(UsernameModified);

            ReadUsernameFromPlayerPrefs();

        }

        void ReadUsernameFromPlayerPrefs()
        {
            string username = PlayerPrefs.GetString(_playerPrefs_Username, PlayerPrefs.GetString(string.Empty, "游客"));
            SetText(username);
            UserSettings.UserNickname = username;
        }



        void UsernameModified(string s)
        {
            UserSettings.UserNickname = CheckUsername(s);
            SetText(UserSettings.UserNickname);

            PlayerPrefs.SetString(_playerPrefs_Username, UserSettings.UserNickname);
        }

        string CheckUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                return "游客";
            }

            return username;
        }
    }
}
