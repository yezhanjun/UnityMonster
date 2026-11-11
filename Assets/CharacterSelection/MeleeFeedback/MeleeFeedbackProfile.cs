using UnityEngine;

namespace MTPSKIT.Gameplay
{
    [CreateAssetMenu(menuName = "TPSBattle/近战命中反馈", fileName = "MeleeFeedback")]
    public sealed class MeleeFeedbackProfile : ScriptableObject
    {
        [Header("刀剑命中印花 · DemonHunter")]
        public CombatScratchConfig swordStamp = new CombatScratchConfig();

        [Header("径向模糊 · SC Post Effects")]
        [Range(0f, 1f)] public float radialAmount = 0.25f;
        [Min(0f)] public float radialDuration = 0.14f;
        public Vector2 radialCenter = new Vector2(0.5f, 0.5f);
        [Range(3, 12)] public int radialIterations = 6;
        public Shader radialShader;

        [Header("屏幕震动")]
        [Min(0f)] public float shakeDuration = 0.14f;
        [Min(0f)] public float shakeFrequency = 25f;
        public Vector3 shakePosition = new Vector3(0.015f, 0.01f, 0f);
        public Vector3 shakeRotation = new Vector3(0.6f, 0.35f, 0.3f);

        [Header("攻击者顿帧 · 角色局部时间")]
        [Range(0f, 1f)] public float attackerRate = 0.1f;
        [Min(0f)] public float attackerDuration = 0.06f;
        [Header("受击者顿帧")]
        [Range(0f, 1f)] public float victimRate = 0.15f;
        [Min(0f)] public float victimDuration = 0.05f;
    }
}
