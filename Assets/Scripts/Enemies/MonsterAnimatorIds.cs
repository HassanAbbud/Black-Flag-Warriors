using UnityEngine;

namespace Ahoy.Enemies
{
    // The animator contract for every monster, hashed once. Attack states are hashed by their
    // MonsterAttackDef. The controller's default state must be Locomotion.
    static class MonsterAnimatorIds
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");

        public static readonly int Locomotion = Animator.StringToHash("Locomotion");
        public static readonly int HitFlinch = Animator.StringToHash("HitFlinch");
        public static readonly int HitKnockback = Animator.StringToHash("HitKnockback");
        public static readonly int Stagger = Animator.StringToHash("Stagger");
        public static readonly int Death = Animator.StringToHash("Death");
    }
}
