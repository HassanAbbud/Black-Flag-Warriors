using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Player
{
    // Walks the combo graph of AttackDef assets (R50). Y1 light → Y2 …; a heavy after n-1
    // lights plays C(n). Timing comes only from the three animation events on each clip.
    public sealed class PlayerAttack : MonoBehaviour
    {
        // Preallocated query buffers (R70: zero GC per frame). A sweep never needs more.
        const int MaxCollidersPerQuery = 64;
        const int MaxVictimsPerSwing = 64;

        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerMotor motor;
        [SerializeField] PlayerHealth self;
        [SerializeField] Animator animator;
        [SerializeField] HitstopReceiver hitstop;

        [Header("Combo graph roots (R50)")]
        [Tooltip("Light from neutral — ATK_Y1.")]
        [SerializeField] AttackDef lightOpener;
        [Tooltip("Heavy from neutral — ATK_C1, the gap-closer.")]
        [SerializeField] AttackDef heavyOpener;

        [Tooltip("Layers a swing can hit. Monsters only; villagers are never on it.")]
        [SerializeField] LayerMask hitMask;

        readonly Collider[] overlap = new Collider[MaxCollidersPerQuery];
        readonly IDamageable[] victims = new IDamageable[MaxVictimsPerSwing];
        int victimCount;

        InputBuffer lightBuffer;
        InputBuffer heavyBuffer;

        AttackDef current;
        bool hitboxOpen;
        bool stateEntered;

        public AttackDef Current => current;

        // Raised for every connection, after the damage is dealt. For VFX, audio, KO count.
        public event Action<IDamageable, AttackDef> Hit;

        void OnEnable() => motor.StateChanged += OnMotorStateChanged;
        void OnDisable() => motor.StateChanged -= OnMotorStateChanged;

        // ── Input (PlayerInput, Send Messages) ─────────────────────────────────────────
        void OnLightAttack(InputValue value)
        {
            if (value.isPressed) BufferLight();
        }

        void OnHeavyAttack(InputValue value)
        {
            if (value.isPressed) BufferHeavy();
        }

        public void BufferLight() => lightBuffer.Press(Time.time);
        public void BufferHeavy() => heavyBuffer.Press(Time.time);

        // ── Animation events. The animator places them; code never guesses frames. ───────
        // Each handler checks the state that fired it: during a cross-fade the outgoing
        // attack's events still fire and must not touch the incoming one.
        void AE_HitboxOpen(AnimationEvent e)
        {
            if (!FiredByCurrent(e)) return;
            hitboxOpen = true;
            ClearVictims();
        }

        // The swing is over once it can be cancelled, so the hitbox closes here.
        void AE_CancelOpen(AnimationEvent e)
        {
            if (!FiredByCurrent(e)) return;
            hitboxOpen = false;
            motor.OpenCancelWindow();
        }

        void AE_AttackEnd(AnimationEvent e)
        {
            if (FiredByCurrent(e)) motor.EndAttack();
        }

        bool FiredByCurrent(AnimationEvent e) =>
            current != null && e.animatorStateInfo.shortNameHash == current.StateHash;

        // ── Update ────────────────────────────────────────────────────────────────────
        void Update()
        {
            if (hitstop.IsFrozen) return;

            TryStartBufferedAttack();
            if (current == null) return;

            if (hitboxOpen) Sweep();
            EndIfClipFinished();
        }

        void TryStartBufferedAttack()
        {
            if (!motor.CanStartAttack) return;

            float now = Time.time;
            bool light = lightBuffer.IsPending(now, balance.inputBufferSeconds);
            bool heavy = heavyBuffer.IsPending(now, balance.inputBufferSeconds);
            if (!light && !heavy) return;

            // Both queued: the later press is what the player meant.
            bool useHeavy = heavy && (!light || heavyBuffer.PressedAt >= lightBuffer.PressedAt);
            lightBuffer.Clear();
            heavyBuffer.Clear();

            AttackDef next = current == null
                ? (useHeavy ? heavyOpener : lightOpener)
                : (useHeavy ? current.nextHeavy : current.nextLight);
            if (next != null) Begin(next);
        }

        void Begin(AttackDef attack)
        {
            current = attack;
            hitboxOpen = false;
            stateEntered = false;
            ClearVictims();

            motor.BeginAttack(attack);
            animator.SetFloat(PlayerAnimatorIds.AttackSpeed, attack.playbackSpeed);
            animator.CrossFadeInFixedTime(attack.StateHash, attack.crossFadeSeconds, 0, 0f);
        }

        void Sweep()
        {
            Vector3 center = transform.TransformPoint(current.hitboxCenter);
            int count = Physics.OverlapBoxNonAlloc(center, current.hitboxHalfExtents, overlap,
                                                   transform.rotation, hitMask, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count && victimCount < MaxVictimsPerSwing; i++)
            {
                if (!TryGetDamageable(overlap[i], out IDamageable target)) continue;
                if (target == (IDamageable)self || !target.IsAlive || target.Faction == Faction.Village) continue;
                if (AlreadyHit(target)) continue;

                // Hitstop on the first target of a swing only, so a sweep through thirty
                // scuttlers does not stutter.
                bool first = victimCount == 0;
                victims[victimCount++] = target;

                var info = new DamageInfo(current.damage, transform.position, current.reaction,
                                          first ? current.hitstopFrames : 0, current.weakPointDrain, null);
                target.TakeDamage(in info);
                Hit?.Invoke(target, current);

                if (first) hitstop.Freeze(current.hitstopFrames);
            }
        }

        // Safety net for a clip that is missing AE_AttackEnd: end when the state finishes
        // or when something else has replaced it.
        void EndIfClipFinished()
        {
            if (animator.IsInTransition(0)) return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (state.shortNameHash == current.StateHash)
            {
                stateEntered = true;
                if (state.normalizedTime >= 1f) motor.EndAttack();
            }
            else if (stateEntered)
            {
                motor.EndAttack();
            }
        }

        void OnMotorStateChanged(PlayerMotor.MotorState previous, PlayerMotor.MotorState next)
        {
            if (previous != PlayerMotor.MotorState.Attacking) return;
            current = null;
            hitboxOpen = false;
            ClearVictims();
        }

        bool AlreadyHit(IDamageable target)
        {
            for (int i = 0; i < victimCount; i++)
                if (victims[i] == target) return true;
            return false;
        }

        void ClearVictims()
        {
            Array.Clear(victims, 0, victimCount);
            victimCount = 0;
        }

        static bool TryGetDamageable(Collider collider, out IDamageable target)
        {
            if (collider.TryGetComponent(out target)) return true;
            target = collider.GetComponentInParent<IDamageable>();
            return target != null;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            AttackDef shown = current != null ? current : lightOpener;
            if (shown == null) return;
            Gizmos.color = hitboxOpen ? Color.red : Color.yellow;
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.DrawWireCube(shown.hitboxCenter, shown.hitboxHalfExtents * 2f);
        }
#endif
    }
}
