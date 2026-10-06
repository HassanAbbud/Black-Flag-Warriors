using UnityEngine;

namespace Ahoy.Player
{
    // Animator parameters and non-attack states, hashed once. Attack states are hashed by
    // their AttackDef.
    static class PlayerAnimatorIds
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
        public static readonly int DodgeSpeed = Animator.StringToHash("DodgeSpeed");

        public static readonly int Locomotion = Animator.StringToHash("Locomotion");
        public static readonly int Dodge = Animator.StringToHash("Dodge");
        public static readonly int HitFlinch = Animator.StringToHash("HitFlinch");
        public static readonly int HitKnockback = Animator.StringToHash("HitKnockback");
        public static readonly int Death = Animator.StringToHash("Death");

        // Upper-body layer holding the shield pose; its weight is the guard.
        public const int GuardLayer = 1;
    }
}
