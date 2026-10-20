using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // Type Object: one asset per monster attack (ATK_Scuttler_Bite, ATK_Giant_Slam). The three
    // phases are timed in data, so a designer tunes how readable an attack is without code.
    [CreateAssetMenu(menuName = "Ahoy/Monster Attack Def", fileName = "ATK_")]
    public sealed class MonsterAttackDef : ScriptableObject
    {
        [Header("Animation")]
        [Tooltip("Animator state name on the base layer. Must match exactly. Empty plays no clip.")]
        public string animatorState;
        [Tooltip("Playback multiplier for the clip.")]
        public float playbackSpeed = 1f;
        [Tooltip("Seconds of cross-fade into this attack.")]
        public float crossFadeSeconds = 0.08f;

        [Header("Phases, seconds")]
        [Tooltip("Wind-up. The tell Zahim reads; the weak-point counter window opens here.")]
        public float telegraphSeconds = 0.5f;
        [Tooltip("Degrees per second the attacker keeps turning toward Zahim while winding up. 0 locks the aim at the start, which makes the attack dodgeable.")]
        public float telegraphTurnSpeed = 0f;
        [Tooltip("How long the hitbox is live.")]
        public float strikeSeconds = 0.2f;
        [Tooltip("Vulnerable time after the strike. Inside a boss combo this is the gap before the next step.")]
        public float recoverSeconds = 0.6f;

        [Header("Damage")]
        public float damage = 50f;
        public HitReaction reaction = HitReaction.Flinch;
        [Tooltip("Frames Zahim freezes when this connects.")]
        public int hitstopFrames = 4;
        [Tooltip("R51: Zahim's guard cannot stop this.")]
        public bool unblockable;

        [Header("Hitbox, in the attacker's local space (metres)")]
        public Vector3 hitboxCenter = new Vector3(0f, 1f, 1.2f);
        public Vector3 hitboxHalfExtents = new Vector3(1f, 1f, 1f);

        [Header("Use by crowd units")]
        [Tooltip("Closest distance at which a crowd unit starts this attack, metres.")]
        public float minRange = 0f;
        [Tooltip("Farthest distance at which a crowd unit starts this attack, metres.")]
        public float maxRange = 1.8f;
        [Tooltip("Seconds before the same unit may start another attack.")]
        public float cooldownSeconds = 1.2f;

        [Header("Lunge")]
        [Tooltip("Metres the attacker travels forward when the strike starts.")]
        public float lungeDistance;
        public float lungeSeconds = 0.15f;

        public int StateHash { get; private set; }
        public bool HasClip => !string.IsNullOrEmpty(animatorState);

        void OnEnable() => StateHash = Animator.StringToHash(animatorState ?? string.Empty);
        void OnValidate() => StateHash = Animator.StringToHash(animatorState ?? string.Empty);
    }
}
