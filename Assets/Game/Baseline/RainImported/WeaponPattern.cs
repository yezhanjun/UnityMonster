using System;
using UnityEngine;
namespace MTPSKIT.RainImported {
    [Serializable] internal sealed class WeaponPattern {
        public string id,title;
        public float coefficient,interval,procCoefficient,reach;
        public int pellets;
    }
    [Serializable] internal sealed class WeaponPatterns {
        public WeaponPattern[] weapons;
        public static WeaponPatterns Load()=>JsonUtility.FromJson<WeaponPatterns>(Resources.Load<TextAsset>("RainImported/WeaponPatterns").text);
    }
}

