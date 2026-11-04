using UnityEngine;

namespace Ahoy.Enemies
{
    // Monsters borrow Zahim's humanoid clips, which carry his PlayerAttack events. A monster's
    // hit timing lives in its MonsterAttackDef, so it swallows them here. Without a receiver
    // Unity logs an error on every swing. Sits next to the Animator.
    public sealed class AnimationEventSink : MonoBehaviour
    {
        void AE_HitboxOpen(AnimationEvent e) { }
        void AE_CancelOpen(AnimationEvent e) { }
        void AE_AttackEnd(AnimationEvent e) { }
    }
}
