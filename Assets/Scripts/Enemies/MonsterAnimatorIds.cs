using UnityEngine;

namespace Ahoy.Enemies
{
    // The animator contract for every monster, hashed once. Attack states are hashed by their
    // MonsterAttackDef. The controller's default state must be Locomotion.
    static class MonsterAnimatorIds
    {
        public static readonly int Speed = Animator.StringToHash("Speed");
        public static readonly int AttackSpeed = Animator.StringToHash("AttackSpeed");
        // Bosses only: drives the Stagger state so one hit is dragged out across the whole stun.
        public static readonly int StunSpeed = Animator.StringToHash("StunSpeed");

        public static readonly int Locomotion = Animator.StringToHash("Locomotion");
        public static readonly int HitFlinch = Animator.StringToHash("HitFlinch");
        public static readonly int HitKnockback = Animator.StringToHash("HitKnockback");
        public static readonly int Stagger = Animator.StringToHash("Stagger");
        public static readonly int Death = Animator.StringToHash("Death");
        // Bosses only, all optional: an entrance roar, a quick hit flinch, and idle fidgets.
        public static readonly int Roar = Animator.StringToHash("Roar");
        public static readonly int Hit = Animator.StringToHash("Hit");
        public static readonly int Fidget = Animator.StringToHash("Fidget");
        public static readonly int Fidget2 = Animator.StringToHash("Fidget2");
    }
}
