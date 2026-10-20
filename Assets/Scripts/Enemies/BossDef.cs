using UnityEngine;

namespace Ahoy.Enemies
{
    // One asset per named unit (MON_Guardian, MON_Giant). Inherits the shared stats; its
    // attacks live in combos, so the base class's attacks list is unused here.
    [CreateAssetMenu(menuName = "Ahoy/Boss Def", fileName = "MON_")]
    public sealed class BossDef : MonsterDef
    {
        [Header("Combos")]
        public BossPatternDef[] patterns;
        [Tooltip("Seconds of breathing room after a stagger ends before it fights again.")]
        public float restSeconds = 0.8f;
    }
}
