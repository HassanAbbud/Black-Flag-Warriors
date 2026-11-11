using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // One asset per named unit (MON_Guardian, MON_Giant). Inherits the shared stats; its
    // attacks live in combos, so the base class's attacks list is unused here.
    [CreateAssetMenu(menuName = "Ahoy/Boss Def", fileName = "MON_")]
    public sealed class BossDef : MonsterDef
    {
        [Header("Combos")]
        [Tooltip("R33: the first attack of the encounter. Usually the unguardable one that leaves a punish window.")]
        public BossPatternDef openingPattern;
        public BossPatternDef[] patterns;
        [Tooltip("Minimum seconds between the end of one combo and the start of the next. Gives Zahim room to act.")]
        public float attackGapSeconds = 1.2f;
        [Tooltip("Seconds of breathing room after a stagger ends before it fights again.")]
        public float restSeconds = 0.8f;

        [Header("Weak point (R19-R26)")]
        [Tooltip("R20a: true when a tool thrown during a counter-window telegraph exposes the gauge.")]
        public bool hasCounterTool;
        [Tooltip("The tool that counters this species (King Dodongo: bomb). Ignored unless Has Counter Tool is set.")]
        public ToolType counterTool = ToolType.Bomb;
        [Tooltip("Seconds a giant lies down after a Weak Point Smash before it fights again (R24).")]
        public float downedSeconds = 2.5f;
        [Tooltip("The clip on the animator's Stagger state (Golem_Stunned). Set, it is played once, slowed " +
                 "to last the whole stun, through the StunSpeed parameter. Empty, Stagger plays at its own speed.")]
        public AnimationClip staggerClip;
    }
}
