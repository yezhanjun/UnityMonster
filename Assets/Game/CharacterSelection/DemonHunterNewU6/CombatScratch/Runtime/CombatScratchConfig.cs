using System;
using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [Serializable]
    public sealed class CombatScratchConfig
    {
        public bool enabled;
        public bool contactSparks = true;
        public Material material;
        [Tooltip("0: 剑伤遮罩层；1: 径向遮罩层（用于钝击）。共享材质按槽保存类型。")]
        [Range(0, 1)] public int stampKind;
        [Min(0.01f)] public float length = 0.65f;
        [Min(0.001f)] public float width = 0.16f;
        [Min(0.001f)] public float depth = 0.04f;
        [Min(0.01f)] public float lifetime = 0.7f;
        [Min(0.01f)] public float glowDuration = 0.5f;
        [Range(0.01f, 1f)] public float fadeFraction = 0.65f;
        [Tooltip("玩家模型局部空间的裂痕主轴；后续可由逐招刀刃轨迹覆盖。")]
        public Vector3 localSlashDirection = new Vector3(0.25f, 1f, 0f);
        [ColorUsage(false, true)] public Color glowColor = new Color(5f, 1.3f, 0.2f, 1f);
        public Color scarColor = new Color(0.15f, 0.025f, 0.015f, 1f);
        [Min(0.01f)] public float maxSurfaceDistance = 0.8f;
    }
}
