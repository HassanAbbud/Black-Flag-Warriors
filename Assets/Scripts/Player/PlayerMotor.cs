using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Player
{
    // Moves Zahim. Owns the one state machine every other player component reads; it never
    // decides what attack plays (PlayerAttack) or how much a hit hurts (PlayerHealth).
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerMotor : MonoBehaviour, IPlayerMotor
    {
        public enum MotorState { Locomotion, Guarding, Dodging, Attacking, Staggered, Dead }

        [SerializeField] BalanceConfig balance;
        [SerializeField] CharacterController controller;
        [SerializeField] Animator animator;
        [SerializeField] HitstopReceiver hitstop;
        [SerializeField] PlayerCamera playerCamera;

        // previous, next. Raised only when the state actually changes.
        public event Action<MotorState, MotorState> StateChanged;

        public MotorState State { get; private set; }

        Vector2 moveInput;
        bool guardHeld;
        bool dodgeHeld;
        bool sprintLatched;     // R51: a roll with Dodge still held turns into a sprint
        float runTime;          // seconds of unbroken running, for the automatic sprint
        InputBuffer dodgeBuffer;

        float stateTime;
        float stateSeconds;     // length of timed states: dodge, stagger
        bool attackCancelable;
        float verticalSpeed;

        // Dodge, lunge and knockback are all "slide this way for this long".
        Vector3 travelDirection;
        float travelSpeed;
        float travelSeconds;

        public Vector3 Position => transform.position;
        public Vector3 Forward => transform.forward;
        public bool IsDead => State == MotorState.Dead;
        public bool IsInvulnerable =>
            State == MotorState.Dead ||
            (State == MotorState.Dodging && stateTime < balance.dodgeInvulnerability); // R51 dodge i-frames
        public bool CanStartAttack =>
            State == MotorState.Locomotion || State == MotorState.Guarding ||
            (State == MotorState.Attacking && attackCancelable);

        // ── Input (PlayerInput, Send Messages) ─────────────────────────────────────────
        void OnMove(InputValue value) => moveInput = value.Get<Vector2>();

        // Raising the guard also swings the camera behind him, as in Hyrule Warriors.
        void OnGuard(InputValue value)
        {
            guardHeld = value.isPressed;
            if (guardHeld && State != MotorState.Dead && playerCamera != null) playerCamera.RecenterBehind();
        }

        void OnDodge(InputValue value)
        {
            dodgeHeld = value.isPressed;
            if (dodgeHeld) dodgeBuffer.Press(Time.time);
        }

        // ── Called by PlayerAttack and PlayerHealth ───────────────────────────────────
        public void BeginAttack(AttackDef attack)
        {
            Vector3 aim = CameraRelative(moveInput);
            if (aim.sqrMagnitude > 0f) transform.rotation = Quaternion.LookRotation(aim);

            SetState(MotorState.Attacking);
            sprintLatched = false;
            animator.SetFloat(PlayerAnimatorIds.Speed, 0f);
            BeginTravel(transform.forward, attack.lungeDistance, attack.lungeSeconds);
        }

        public void OpenCancelWindow()
        {
            if (State == MotorState.Attacking) attackCancelable = true;
        }

        public void EndAttack()
        {
            if (State != MotorState.Attacking) return;
            SetState(MotorState.Locomotion);
            animator.CrossFadeInFixedTime(PlayerAnimatorIds.Locomotion, balance.animationCrossFade);
        }

        // R51: guard blocks from the front only, and never an unblockable attack.
        public bool TryBlock(in DamageInfo info)
        {
            if (State != MotorState.Guarding || info.Unblockable) return false;

            Vector3 toAttacker = info.Origin - transform.position;
            toAttacker.y = 0f;
            if (toAttacker.sqrMagnitude > 0f &&
                Vector3.Angle(transform.forward, toAttacker) > balance.guardArcDegrees * 0.5f) return false;

            stateTime = 0f;
            BeginTravel(-transform.forward, balance.guardPushback, balance.guardHitSeconds);
            return true;
        }

        public void ApplyHitReaction(HitReaction reaction, Vector3 origin)
        {
            if (State == MotorState.Dead || reaction == HitReaction.None) return;

            Vector3 away = transform.position - origin;
            away.y = 0f;
            away = away.sqrMagnitude > 0f ? away.normalized : -transform.forward;

            bool flinch = reaction == HitReaction.Flinch;
            SetState(MotorState.Staggered);
            stateSeconds = flinch ? balance.flinchSeconds : balance.knockbackSeconds;
            if (!flinch)
            {
                transform.rotation = Quaternion.LookRotation(-away);
                float distance = reaction == HitReaction.Knockback ? balance.knockbackDistance : balance.blowawayDistance;
                BeginTravel(away, distance, stateSeconds);
            }
            animator.CrossFadeInFixedTime(flinch ? PlayerAnimatorIds.HitFlinch : PlayerAnimatorIds.HitKnockback,
                                          balance.animationCrossFade, 0, 0f);
        }

        public void Kill()
        {
            if (State == MotorState.Dead) return;
            SetState(MotorState.Dead);
            animator.CrossFadeInFixedTime(PlayerAnimatorIds.Death, balance.animationCrossFade);
        }

        public void Teleport(Vector3 position, Quaternion rotation)
        {
            controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            controller.enabled = true;
            verticalSpeed = 0f;
        }

        // ── State machine ─────────────────────────────────────────────────────────────
        void Update()
        {
            if (hitstop.IsFrozen) return;

            float dt = Time.deltaTime;
            stateTime += dt;
            Vector3 horizontal = Vector3.zero;

            switch (State)
            {
                case MotorState.Locomotion:
                    horizontal = Locomote(dt);
                    if (dodgeBuffer.IsPending(Time.time, balance.inputBufferSeconds)) StartDodge();
                    else if (guardHeld) SetState(MotorState.Guarding);
                    break;

                case MotorState.Guarding:
                    // A blocked hit slides him back and holds him for guardHitSeconds.
                    horizontal = stateTime < travelSeconds ? Travel(dt) : Strafe(dt);
                    if (dodgeBuffer.IsPending(Time.time, balance.inputBufferSeconds)) StartDodge();
                    else if (!guardHeld) SetState(MotorState.Locomotion);
                    break;

                case MotorState.Dodging:
                    horizontal = Travel(dt);
                    if (stateTime >= stateSeconds)
                    {
                        sprintLatched = dodgeHeld;
                        SetState(MotorState.Locomotion);
                        animator.CrossFadeInFixedTime(PlayerAnimatorIds.Locomotion, balance.animationCrossFade);
                    }
                    break;

                case MotorState.Attacking:
                    horizontal = Travel(dt);
                    if (attackCancelable && dodgeBuffer.IsPending(Time.time, balance.inputBufferSeconds)) StartDodge();
                    break;

                case MotorState.Staggered:
                    horizontal = Travel(dt);
                    if (stateTime >= stateSeconds)
                    {
                        SetState(MotorState.Locomotion);
                        animator.CrossFadeInFixedTime(PlayerAnimatorIds.Locomotion, balance.animationCrossFade);
                    }
                    break;
            }

            float guardWeight = State == MotorState.Guarding ? 1f : 0f;
            animator.SetLayerWeight(PlayerAnimatorIds.GuardLayer, Mathf.MoveTowards(
                animator.GetLayerWeight(PlayerAnimatorIds.GuardLayer), guardWeight, balance.guardPoseBlendSpeed * dt));

            verticalSpeed = controller.isGrounded && verticalSpeed < 0f
                ? balance.groundedStickSpeed
                : verticalSpeed + balance.gravity * dt;

            controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
        }

        // The single place state changes; breakpoint here.
        void SetState(MotorState next)
        {
            MotorState previous = State;
            State = next;
            stateTime = 0f;
            travelSeconds = 0f;
            runTime = 0f;
            attackCancelable = false;
            if (previous != next) StateChanged?.Invoke(previous, next);
        }

        Vector3 Locomote(float dt)
        {
            Vector3 direction = CameraRelative(moveInput);
            float amount = direction.magnitude;

            // Hyrule Warriors: keep running and he breaks into a sprint on his own. Letting go
            // of a dodge-sprint while still running keeps the sprint.
            runTime = amount >= balance.autoSprintStickThreshold ? runTime + dt : 0f;
            if (sprintLatched && !dodgeHeld)
            {
                sprintLatched = false;
                if (runTime > 0f) runTime = Mathf.Max(runTime, balance.autoSprintDelay);
            }
            bool sprinting = amount > 0f && (sprintLatched || runTime >= balance.autoSprintDelay);
            float speed = sprinting ? balance.sprintSpeed : balance.moveSpeed * amount;

            if (amount > 0f)
            {
                direction /= amount;
                transform.rotation = Quaternion.RotateTowards(
                    transform.rotation, Quaternion.LookRotation(direction), balance.turnSpeed * dt);
            }

            animator.SetFloat(PlayerAnimatorIds.Speed, speed, balance.speedDampSeconds, dt);
            return direction * speed;
        }

        // Guarding: walk under the shield without turning, so the guard keeps facing the threat.
        Vector3 Strafe(float dt)
        {
            Vector3 direction = CameraRelative(moveInput);
            float speed = balance.guardMoveSpeed * direction.magnitude;
            animator.SetFloat(PlayerAnimatorIds.Speed, speed, balance.speedDampSeconds, dt);
            return direction * balance.guardMoveSpeed;
        }

        void StartDodge()
        {
            dodgeBuffer.Clear();

            Vector3 direction = CameraRelative(moveInput);
            direction = direction.sqrMagnitude > 0f ? direction.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(direction);

            SetState(MotorState.Dodging);
            stateSeconds = balance.dodgeDuration + balance.dodgeRecovery;
            BeginTravel(direction, balance.dodgeDistance, balance.dodgeDuration);

            animator.SetFloat(PlayerAnimatorIds.DodgeSpeed, balance.dodgeAnimationSpeed);
            animator.CrossFadeInFixedTime(PlayerAnimatorIds.Dodge, balance.animationCrossFade, 0, 0f);
        }

        void BeginTravel(Vector3 direction, float distance, float seconds)
        {
            travelDirection = direction;
            travelSeconds = seconds;
            travelSpeed = seconds > 0f ? distance / seconds : 0f;
        }

        // Velocity averaged over this frame, counting only the part of it still inside the
        // slide, so the distance covered is exact at any frame rate.
        Vector3 Travel(float dt)
        {
            float active = Mathf.Clamp(travelSeconds - (stateTime - dt), 0f, dt);
            return active > 0f ? travelDirection * (travelSpeed * active / dt) : Vector3.zero;
        }

        // Stick input turned by the camera's yaw, so "up" is always "away from the camera".
        Vector3 CameraRelative(Vector2 input)
        {
            Vector3 flat = new Vector3(input.x, 0f, input.y);
            if (flat.sqrMagnitude > 1f) flat.Normalize();
            return playerCamera != null ? playerCamera.YawRotation * flat : flat;
        }
    }
}
