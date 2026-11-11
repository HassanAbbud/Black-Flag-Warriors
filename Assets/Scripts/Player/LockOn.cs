using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Player
{
    // R52 lock-on, Hyrule Warriors style: press to lock the powerful monster nearest the
    // centre of the view, flick the right stick (or the mouse) to switch, press again to
    // release. Only powerful monsters can be locked — scuttlers never (R19). Chooses the
    // target only; PlayerCamera frames it, AttackAssist and the guard face it.
    public sealed class LockOn : MonoBehaviour
    {
        const string MouseScheme = "Keyboard&Mouse";
        const int MaxCandidates = 32;

        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerCamera playerCamera;
        [SerializeField] PlayerInput playerInput;
        [Tooltip("Layers monsters are on.")]
        [SerializeField] LayerMask targetMask;
        [Tooltip("Layers that block line of sight to a target. Nothing = no check.")]
        [SerializeField] LayerMask obstacleMask;

        readonly Collider[] candidates = new Collider[MaxCandidates];

        float stickX;
        float mouseX;
        bool flickArmed = true;
        float nextSwitchAt;

        public Transform Target { get; private set; }
        public IUnit Unit { get; private set; }
        public bool HasTarget => Target != null;

        // The new target, or null when the lock is released or broken.
        public event Action<IUnit> TargetChanged;

        void OnDisable() => Set(null, null);

        // ── Input (PlayerInput, Send Messages) ─────────────────────────────────────────
        // Send Messages calls this on press and on release; only the press counts.
        void OnLockOn(InputValue value)
        {
            if (!value.isPressed) return;
            if (HasTarget) Set(null, null);
            else Acquire();
        }

        void OnLook(InputValue value)
        {
            Vector2 look = value.Get<Vector2>();
            if (playerInput != null && playerInput.currentControlScheme == MouseScheme) mouseX += look.x;
            else stickX = look.x;
        }

        void Update()
        {
            if (HasTarget && !IsValid()) Set(null, null);

            float flick = mouseX;
            mouseX = 0f;
            if (!HasTarget) return;

            // A stick flick switches once, then the stick has to come back to centre.
            int direction = 0;
            if (Mathf.Abs(stickX) >= balance.lockOnFlickThreshold)
            {
                if (flickArmed) direction = stickX > 0f ? 1 : -1;
                flickArmed = false;
            }
            else if (Mathf.Abs(stickX) < balance.lockOnFlickThreshold * 0.5f)
            {
                flickArmed = true;
            }
            if (Mathf.Abs(flick) >= balance.lockOnMouseFlickPixels) direction = flick > 0f ? 1 : -1;

            if (direction != 0 && Time.time >= nextSwitchAt) Switch(direction);
        }

        void Acquire()
        {
            Vector3 view = playerCamera.YawRotation * Vector3.forward;
            int count = Query(balance.lockOnRange);

            Transform best = null;
            IUnit bestUnit = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetPowerful(candidates[i], out IUnit unit, out Transform candidate)) continue;

                Vector3 to = Flat(candidate.position - transform.position);
                float angle = Vector3.Angle(view, to);
                if (angle > balance.lockOnMaxAngle || Blocked(candidate)) continue;

                float score = angle + to.magnitude * balance.lockOnDistanceWeight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                    bestUnit = unit;
                }
            }
            if (best != null) Set(best, bestUnit);
        }

        // Next target clockwise (direction 1) or counter-clockwise (-1) around Zahim,
        // measured from the current one.
        void Switch(int direction)
        {
            nextSwitchAt = Time.time + balance.lockOnSwitchCooldown;
            Vector3 current = Flat(Target.position - transform.position);
            int count = Query(balance.lockOnRange);

            Transform best = null;
            IUnit bestUnit = null;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                if (!TryGetPowerful(candidates[i], out IUnit unit, out Transform candidate)) continue;
                if (candidate == Target || Blocked(candidate)) continue;

                float angle = Vector3.SignedAngle(current, Flat(candidate.position - transform.position), Vector3.up) * direction;
                if (angle <= 0f) angle += 360f;
                if (angle < bestAngle)
                {
                    bestAngle = angle;
                    best = candidate;
                    bestUnit = unit;
                }
            }
            if (best != null) Set(best, bestUnit);
        }

        bool IsValid()
        {
            if (!Target.gameObject.activeInHierarchy || !Unit.IsAlive) return false;
            float range = balance.lockOnBreakRange;
            return (Target.position - transform.position).sqrMagnitude <= range * range;
        }

        void Set(Transform target, IUnit unit)
        {
            if (target == Target) return;
            Target = target;
            Unit = unit;
            TargetChanged?.Invoke(unit);
        }

        int Query(float range) => Physics.OverlapSphereNonAlloc(transform.position, range, candidates,
                                                                targetMask, QueryTriggerInteraction.Collide);

        // R19: everything above scuttler rank has a gauge, and that is what lock-on is for.
        static bool TryGetPowerful(Collider collider, out IUnit unit, out Transform root)
        {
            unit = collider.GetComponentInParent<IUnit>();
            root = unit is Component component ? component.transform : null;
            return root != null && unit.IsAlive && unit.Faction != Faction.Village && unit.Tier != Tier.Scuttler;
        }

        bool Blocked(Transform candidate)
        {
            if (obstacleMask.value == 0) return false;
            Vector3 eye = transform.position + Vector3.up * balance.cameraPivotHeight;
            Vector3 aim = candidate.position + Vector3.up * balance.lockOnTargetHeight;
            return Physics.Linecast(eye, aim, obstacleMask, QueryTriggerInteraction.Ignore);
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
