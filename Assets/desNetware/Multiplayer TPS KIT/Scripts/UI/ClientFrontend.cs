using Mirror;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MTPSKIT.Gameplay.Gamemodes;
using MTPSKIT.Gameplay;

namespace MTPSKIT.UI
{
    public static class ClientFrontend
    {

        public static bool Pause { private set; get; } = false;

        public static int ThisClientTeam { set; get; } = -1;

        public static bool Hub { set; get; } = true;

        //this will be called from server when we receive all the neccesary info about game properties like gamemode
        public delegate void OnPlayerJoined(Gamemode gamemode, NetworkIdentity player);
        public static OnPlayerJoined ClientEvent_OnJoinedToGame { get; set; }

        /// <summary>
        /// reference to GamemodeUI, In scene UI objects can listen on events that are included in 
        /// this class that are related to gamemode events
        /// </summary>
        public static UIGamemode GamemodeUI;

        ///<summary>
        ///This number is increased by 1 every time any UI element needs to show cursor (showing pause menu, showing chat etc)and decreased by 1 every time those elements dont need anymore cursor. If this number is equal to zero, then it
        ///means that curson invisible and locked, and player can control character, move, shoot etc. If number is greater than 0
        ///than curson is shown and player controller cannot be controlled. Thanks to this approach we can stack on top of each other 
        ///prompts that require cursor, and keep track of when player character should be able to be controllable again
        ///</summary>
        static int cursorRequests = 0;

        /// <summary>
        /// Passing messages to event so UI can read from it
        /// </summary>
        public delegate void GamemodeMsg(string msg, float liveTime);
        public static GamemodeMsg GamemodeEvent_Message;

        public static void ShowCursor(bool show)
        {
            cursorRequests = show ? cursorRequests + 1 : cursorRequests - 1;

            if (cursorRequests < 0) cursorRequests = 0;

            Cursor.visible = cursorRequests != 0;

            if (cursorRequests != 0)
                Cursor.lockState = CursorLockMode.Confined;
            else
                Cursor.lockState = CursorLockMode.Locked;
        }

        public static void ClearCursorBlockers() 
        {
            cursorRequests = 0;
        }

        public static bool GamePlayInput()
        {
            return cursorRequests == 0;
        }

        public static void SetPause(bool pause)
        {
            Pause = pause;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void InitializeClientFrontEnd()
        {
            ShowCursor(false);
        }

        #region character
        //character that UI is displaying statistics of
        static CharacterInstance _observedCharacterInstance;

        public delegate void OnObservedCharacterSet(CharacterInstance characterInstance);
        public static OnObservedCharacterSet ClientFrontendEvent_OnObservedCharacterSet { get; set; }
        public static Health ObservedCharacter { get; internal set; }

        //sets currently controlled or spectated character so UI elements can keep track of it
        public static void SetObservedCharacter(CharacterInstance characterInstance)
        {
            if (_observedCharacterInstance)
            {
                _observedCharacterInstance.IsObserved = false;
            }

            ObservedCharacter = characterInstance;

            _observedCharacterInstance = characterInstance;

            _observedCharacterInstance.IsObserved = true;

            ClientFrontendEvent_OnObservedCharacterSet?.Invoke(_observedCharacterInstance);

        }
        #endregion
    }
}