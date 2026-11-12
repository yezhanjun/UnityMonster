using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace MTPSKIT.BuildTools
{
    public static class RainAntlerPhysicsValidation
    {
        [Serializable] sealed class Source { public Data data; }
        [Serializable] sealed class Data
        {
            public float dynamicFriction, staticFriction, bounciness;
            public int frictionCombine, bounceCombine;
        }

        public static void Validate()
        {
            var source = JsonUtility.FromJson<Source>(File.ReadAllText("Docs/FrontEnd/antlers-physics-material-source.json")).data;
            var material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>("Assets/Resources/RainImported/Antlers/physmatItems.physicMaterial");
            if (!material || !Mathf.Approximately(material.dynamicFriction, source.dynamicFriction) ||
                !Mathf.Approximately(material.staticFriction, source.staticFriction) ||
                !Mathf.Approximately(material.bounciness, source.bounciness) ||
                (int)material.frictionCombine != source.frictionCombine || (int)material.bounceCombine != source.bounceCombine)
                throw new InvalidOperationException("RAIN_ANTLER_PHYSICS_MATERIAL_FAILED");
            Debug.Log($"RAIN_ANTLER_PHYSICS_MATERIAL_PASSED friction={material.dynamicFriction}/{material.staticFriction} bounce={material.bounciness} runtime_collision_pending=True");
        }
    }
}
