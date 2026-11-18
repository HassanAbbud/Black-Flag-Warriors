using UnityEngine;

namespace Ahoy.Island
{
    // The UAL sword clips carry Zahim's animation events; soldiers time their blows from
    // AllySoldierDef instead, so this sits next to their Animator and takes the events.
    public sealed class SoldierAnimationEvents : MonoBehaviour
    {
        void AE_HitboxOpen() { }
        void AE_CancelOpen() { }
        void AE_AttackEnd() { }
    }
}
