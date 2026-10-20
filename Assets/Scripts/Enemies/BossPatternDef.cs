using UnityEngine;

namespace Ahoy.Enemies
{
    // Type Object: a boss combo (PTN_Giant_Opening). The steps play back to back with no chase
    // between them; each step's recoverSeconds is the gap before the next one.
    [CreateAssetMenu(menuName = "Ahoy/Boss Pattern Def", fileName = "PTN_")]
    public sealed class BossPatternDef : ScriptableObject
    {
        [Header("Combo")]
        public MonsterAttackDef[] steps;

        [Header("Use")]
        [Tooltip("Closest distance to Zahim at which the boss may start this combo, metres.")]
        public float minRange = 0f;
        [Tooltip("Farthest distance to Zahim at which the boss may start this combo, metres.")]
        public float maxRange = 6f;
        [Tooltip("Relative chance against the other combos that fit the range.")]
        public float weight = 1f;
        [Tooltip("Seconds before this combo can be chosen again.")]
        public float cooldownSeconds = 4f;
    }
}
