using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace MTPSKIT
{
    public static class UserSettings
    {
        public static string UserNickname;

        public static float MouseSensitivity = 1f;

        public static KeyCode FirstItemSlot = KeyCode.Alpha1;
        public static KeyCode SecondItemSlot = KeyCode.Alpha2;
        public static KeyCode ThirdtItemSlot = KeyCode.Alpha3;
        public static KeyCode FourthItemSlot = KeyCode.Alpha4;

        public static float FieldOfView { get; set; } = 60;

        //Gameplay
        public static bool AutoEquip = true;
        public static bool ShowKillfeedInMultiplayer = true;

        //Gameplay preferences
        public static int[] PlayerLodout { get; set; }
    }
}