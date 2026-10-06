using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Player
{
    // Type Object: one asset per attack (ATK_Y1 … ATK_C5). nextLight / nextHeavy make the
    // combo graph — Y(n) light → Y(n+1), Y(n) heavy → C(n+1) (R50). Adding a move is a new
    // asset plus an animator state with the same name, never a new class.
    [CreateAssetMenu(menuName = "Ahoy/Attack Def", fileName = "ATK_")]
    public sealed class AttackDef : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Animator state name on the base layer. Must match exactly.")]
        public string animatorState;
        [Tooltip("Playback multiplier for the clip.")]
        public float playbackSpeed = 1f;
        [Tooltip("Seconds of cross-fade into this attack.")]
        public float crossFadeSeconds = 0.08f;

        [Header("Damage")]
        public float damage;
        public HitReaction reaction = HitReaction.Flinch;
        [Tooltip("Frozen frames on the first target of the swing (light 3, heavy 6, finisher 8).")]
        public int hitstopFrames = 3;
        [Tooltip("R22: light 4, strong 10.")]
        public float weakPointDrain = 4f;

        [Header("Hitbox, in Zahim's local space (metres)")]
        public Vector3 hitboxCenter = new Vector3(0f, 1f, 1.2f);
        public Vector3 hitboxHalfExtents = new Vector3(1.2f, 1f, 1f);

        [Header("Lunge")]
        [Tooltip("Metres he travels forward when the attack starts.")]
        public float lungeDistance;
        [Tooltip("Seconds the lunge takes.")]
        public float lungeSeconds = 0.15f;

        [Header("Combo graph (R50)")]
        public AttackDef nextLight;
        public AttackDef nextHeavy;

        public int StateHash { get; private set; }

        void OnEnable() => StateHash = Animator.StringToHash(animatorState);
        void OnValidate() => StateHash = Animator.StringToHash(animatorState);
    }
}
