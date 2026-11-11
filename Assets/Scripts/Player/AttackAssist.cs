using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Player
{
    // Hyrule Warriors-style swing magnetism: an attack turns Zahim toward a nearby monster.
    // Stick idle → the nearest one. Stick held → the one closest to the stick, if it is within
    // attackAssistAngle; otherwise the stick wins. Locked on (R52), every swing faces the target.
    public sealed class AttackAssist : MonoBehaviour
    {
        const int MaxCandidates = 32;

        [SerializeField] BalanceConfig balance;
        [SerializeField] LockOn lockOn;
        [Tooltip("Same layers PlayerAttack hits.")]
        [SerializeField] LayerMask targetMask;

        readonly Collider[] candidates = new Collider[MaxCandidates];

        public Vector3 PickFacing(Vector3 stick)
        {
            Vector3 origin = transform.position;
            if (lockOn != null && lockOn.HasTarget) return lockOn.Target.position - origin;

            int count = Physics.OverlapSphereNonAlloc(origin, balance.attackAssistRange, candidates,
                                                      targetMask, QueryTriggerInteraction.Collide);
            bool steering = stick.sqrMagnitude > 0f;
            float bestScore = float.MaxValue;
            Vector3 best = stick;

            for (int i = 0; i < count; i++)
            {
                IDamageable target = candidates[i].GetComponentInParent<IDamageable>();
                if (target == null || !target.IsAlive || target.Faction == Faction.Village) continue;

                Vector3 to = candidates[i].bounds.center - origin;
                to.y = 0f;
                if (to.sqrMagnitude <= 0f) continue;

                float score;
                if (steering)
                {
                    score = Vector3.Angle(stick, to);
                    if (score > balance.attackAssistAngle) continue;
                }
                else
                {
                    score = to.sqrMagnitude;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    best = to;
                }
            }
            return best;
        }
    }
}
