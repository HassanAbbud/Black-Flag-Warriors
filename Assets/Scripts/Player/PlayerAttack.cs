using System;
using Ahoy.Core;
using Ahoy.Feel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Player
{
    // One connection of a swing, for feedback (VFX, shake, audio, KO count).
    public readonly struct HitReport
    {
        public readonly IDamageable Target;
        public readonly AttackDef Attack;
        public readonly Vector3 Point;     // where the blade met the target
        public readonly bool Hitstopped;   // this connection froze the swing

        public HitReport(IDamageable target, AttackDef attack, Vector3 point, bool hitstopped)
        {
            Target = target;
            Attack = attack;
            Point = point;
            Hitstopped = hitstopped;
        }
    }

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
        [SerializeField] AttackAssist assist;

        [Header("Combo graph roots (R50)")]
        [Tooltip("Light from neutral — ATK_Y1.")]
        [SerializeField] AttackDef lightOpener;
        [Tooltip("Heavy from neutral — ATK_C1, the gap-closer.")]
        [SerializeField] AttackDef heavyOpener;
        [Tooltip("R24: plays on its own when a hit empties a weak point gauge — ATK_Smash.")]
        [SerializeField] AttackDef smash;

        [Tooltip("Layers a swing can hit. Monsters only; villagers are never on it.")]
        [SerializeField] LayerMask hitMask;

        readonly Collider[] overlap = new Collider[MaxCollidersPerQuery];
        readonly IDamageable[] victims = new IDamageable[MaxVictimsPerSwing];
        readonly float[] victimHitAt = new float[MaxVictimsPerSwing];
        int victimCount;

        InputBuffer lightBuffer;
        InputBuffer heavyBuffer;

        AttackDef current;
        bool hitboxOpen;
        bool stateEntered;

        public AttackDef Current => current;

        // Raised for every connection, after the damage is dealt.
        public event Action<HitReport> Hit;

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
            current != null && current.Owns(e.animatorStateInfo.shortNameHash);

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

        void Begin(AttackDef attack) =>
            Play(attack, assist != null ? assist.PickFacing(motor.StickDirection) : motor.StickDirection);

        void Play(AttackDef attack, Vector3 aim)
        {
            current = attack;
            hitboxOpen = false;
            stateEntered = false;
            ClearVictims();

            motor.BeginAttack(attack, aim);
            animator.SetFloat(PlayerAnimatorIds.AttackSpeed, attack.playbackSpeed);
            animator.CrossFadeInFixedTime(attack.StateHash, attack.crossFadeSeconds, 0, 0f);
        }

        void Sweep()
        {
            Vector3 center = transform.TransformPoint(current.hitboxCenter);
            int count = Physics.OverlapBoxNonAlloc(center, current.hitboxHalfExtents, overlap,
                                                   transform.rotation, hitMask, QueryTriggerInteraction.Collide);

            float now = Time.time;
            bool frozeThisSweep = false;
            Transform smashTarget = null;
            for (int i = 0; i < count && smashTarget == null; i++)
            {
                if (!TryGetDamageable(overlap[i], out IDamageable target)) continue;
                if (target == (IDamageable)self || !target.IsAlive || target.Faction == Faction.Village) continue;

                int slot = IndexOf(target);
                if (slot < 0)
                {
                    if (victimCount == MaxVictimsPerSwing) continue;
                    slot = victimCount++;
                    victims[slot] = target;
                }
                else if (current.rehitSeconds <= 0f || now - victimHitAt[slot] < current.rehitSeconds)
                {
                    continue;
                }

                // Hitstop on the first connection of the swing (and of each multi-hit tick)
                // only, so a sweep through thirty scuttlers does not stutter.
                bool freeze = !frozeThisSweep && (slot == 0 || victimHitAt[slot] > 0f);
                frozeThisSweep |= freeze;
                victimHitAt[slot] = now;

                Vector3 point = overlap[i].bounds.ClosestPoint(center);
                Vector3 origin = current.push == PushDirection.AlongFacing
                    ? point - transform.forward
                    : transform.position;

                var info = new DamageInfo(current.damage, origin, current.reaction,
                                          freeze ? current.hitstopFrames : 0, current.weakPointDrain, null);
                target.TakeDamage(in info);
                Hit?.Invoke(new HitReport(target, current, point, freeze));

                if (freeze) hitstop.Freeze(current.hitstopFrames);
                smashTarget = SmashTargetOf(target);
            }

            if (smashTarget != null) BeginSmash(smashTarget);
        }

        // R24: the hit that empties a gauge turns straight into the Smash, as in Hyrule Warriors.
        Transform SmashTargetOf(IDamageable target)
        {
            if (smash == null || current == smash) return null;
            if (target is not IUnit unit || unit.WeakPoint == null) return null;
            if (unit.WeakPoint.State != WeakPointState.Smashing) return null;
            return unit is Component component ? component.transform : null;
        }

        void BeginSmash(Transform target)
        {
            lightBuffer.Clear();
            heavyBuffer.Clear();
            Play(smash, target.position - transform.position);
        }

        // Safety net for a clip that is missing AE_AttackEnd: end when the state finishes
        // (its follow-up, if it has one) or when something else has replaced it.
        void EndIfClipFinished()
        {
            if (animator.IsInTransition(0)) return;

            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            if (current.Owns(state.shortNameHash))
            {
                stateEntered = true;
                bool last = !current.HasFollowUp || state.shortNameHash == current.FollowUpHash;
                if (last && state.normalizedTime >= 1f) motor.EndAttack();
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

        int IndexOf(IDamageable target)
        {
            for (int i = 0; i < victimCount; i++)
                if (victims[i] == target) return i;
            return -1;
        }

        void ClearVictims()
        {
            Array.Clear(victims, 0, victimCount);
            Array.Clear(victimHitAt, 0, victimCount);
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
