using System;
using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // A named unit: a captain, guardian, giant or herald. Unlike the crowd it gets its own
    // Update (Code Patterns 2.8) and a CharacterController, so Zahim's physics sweep finds it.
    // It picks a combo from its BossDef, telegraphs, strikes and recovers. Swings never stagger
    // it; the weak point does (R19–R26), and this reads that state rather than being told.
    [RequireComponent(typeof(CharacterController))]
    public sealed class BossController : MonoBehaviour, IUnit
    {
        public enum BossState { Idle, Chase, Telegraph, Strike, Recover, Staggered, Dead }

        [SerializeField] BossDef def;
        [SerializeField] BalanceConfig balance;
        [SerializeField] CharacterController controller;
        [Tooltip("Optional. Default state must be Locomotion.")]
        [SerializeField] Animator animator;
        [Tooltip("Zahim's root object. Optional: a spawner can call Initialize instead.")]
        [SerializeField] GameObject player;
        [Tooltip("Home portal (R8). Overwritten by Initialize.")]
        [SerializeField] int locationId;

        public event Action<IUnit> Died;
        public event Action<float, float> HealthChanged;   // current, max
        // The weak-point track listens: the counter window is the telegraph.
        public event Action<MonsterAttackDef> TelegraphStarted;
        public event Action TelegraphEnded;

        IPlayerMotor motor;
        IDamageable target;

        BossState state;
        float stateTime;
        float stateSeconds;     // length of timed states: recover
        float health;
        float verticalSpeed;
        Slide slide;

        BossPatternDef pattern;
        int stepIndex;
        MonsterAttackDef attack;
        bool strikeHit;
        float[] patternReadyAt;

        public BossState State => state;
        public float Health => health;
        public float MaxHealth => def.maxHealth;

        public Faction Faction => Faction.Monsters;
        public bool IsAlive => state != BossState.Dead;
        public Tier Tier => def.tier;
        public int LocationId => locationId;
        public float Strength => def.strength;   // R64
        public IWeakPoint WeakPoint { get; private set; }

        void Awake()
        {
            health = def.maxHealth;
            patternReadyAt = new float[def.patterns != null ? def.patterns.Length : 0];
            WeakPoint = GetComponent<IWeakPoint>();   // null until the weak-point track adds one

            if (player != null)
                Initialize(player.GetComponent<IPlayerMotor>(), player.GetComponent<IDamageable>(), locationId);
        }

        // The spawner (Systems) calls this when a portal's guardian comes out.
        public void Initialize(IPlayerMotor playerMotor, IDamageable playerTarget, int home)
        {
            motor = playerMotor;
            target = playerTarget;
            locationId = home;
        }

        // ── Damage ───────────────────────────────────────────────────────────────────
        public void TakeDamage(in DamageInfo info)
        {
            if (state == BossState.Dead) return;

            health = Mathf.Max(0f, health - info.Amount);
            HealthChanged?.Invoke(health, def.maxHealth);
            WeakPoint?.Drain(info.WeakPointDrain);   // R22; the weak point ignores it unless exposed

            if (health <= 0f) Die();
        }

        void Die()
        {
            SetState(BossState.Dead);
            EnemyCombat.Play(animator, MonsterAnimatorIds.Death, def.animationCrossFade);
            controller.enabled = false;
            Died?.Invoke(this);
        }

        // ── State machine ────────────────────────────────────────────────────────────
        void Update()
        {
            if (state == BossState.Dead || motor == null) return;

            float dt = Time.deltaTime;
            stateTime += dt;

            Vector3 toPlayer = motor.Position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 direction = distance > 0.0001f ? toPlayer / distance : transform.forward;
            bool playerAlive = !motor.IsDead;

            // The weak point overrides everything but death: exposed means stand and take it.
            bool exposed = WeakPoint != null &&
                           (WeakPoint.State == WeakPointState.Exposed || WeakPoint.State == WeakPointState.Smashing);
            if (exposed && state != BossState.Staggered) EnterStagger();
            else if (!exposed && state == BossState.Staggered) EndStagger();

            Vector3 velocity = Vector3.zero;
            switch (state)
            {
                case BossState.Idle:
                    if (playerAlive && distance <= def.aggroRange) SetState(BossState.Chase);
                    break;

                case BossState.Chase:
                    velocity = Chase(dt, direction, distance, playerAlive);
                    break;

                case BossState.Telegraph:
                    EnemyCombat.Turn(transform, direction, attack.telegraphTurnSpeed, dt);
                    if (stateTime >= attack.telegraphSeconds) BeginStrike();
                    break;

                case BossState.Strike:
                    velocity = slide.Velocity(stateTime, dt);
                    if (!strikeHit && target != null)
                        strikeHit = EnemyCombat.TryStrike(this, transform, attack, target, motor.Position,
                                                          balance.enemyHitPadding);
                    if (stateTime >= attack.strikeSeconds) EndStrike();
                    break;

                case BossState.Recover:
                    if (stateTime >= stateSeconds) AfterRecover();
                    break;

                case BossState.Staggered:
                    break;   // held until the weak point closes
            }

            Move(velocity, dt);
        }

        Vector3 Chase(float dt, Vector3 direction, float distance, bool playerAlive)
        {
            if (!playerAlive)
            {
                SetState(BossState.Idle);
                return Vector3.zero;
            }
            if (TryStartPattern(distance)) return Vector3.zero;

            EnemyCombat.Turn(transform, direction, def.turnSpeed, dt);
            float speed = distance > def.stopDistance ? def.moveSpeed : 0f;
            if (animator != null) animator.SetFloat(MonsterAnimatorIds.Speed, speed, def.speedDampSeconds, dt);
            return transform.forward * speed;
        }

        // Weighted pick among the combos whose range fits and whose cooldown is over.
        bool TryStartPattern(float distance)
        {
            BossPatternDef[] patterns = def.patterns;
            if (patterns == null) return false;

            float now = Time.time;
            float total = 0f;
            for (int i = 0; i < patterns.Length; i++)
                if (Eligible(i, distance, now)) total += patterns[i].weight;
            if (total <= 0f) return false;

            float roll = UnityEngine.Random.value * total;
            int chosen = -1;
            for (int i = 0; i < patterns.Length; i++)
            {
                if (!Eligible(i, distance, now)) continue;
                chosen = i;   // also the fallback if rounding leaves roll just above zero
                roll -= patterns[i].weight;
                if (roll <= 0f) break;
            }

            pattern = patterns[chosen];
            patternReadyAt[chosen] = now + pattern.cooldownSeconds;
            stepIndex = 0;
            BeginStep();
            return true;
        }

        bool Eligible(int index, float distance, float now)
        {
            BossPatternDef p = def.patterns[index];
            return p != null && p.steps != null && p.steps.Length > 0 &&
                   distance >= p.minRange && distance <= p.maxRange && now >= patternReadyAt[index];
        }

        void BeginStep()
        {
            attack = pattern.steps[stepIndex];
            strikeHit = false;
            SetState(BossState.Telegraph);   // raises TelegraphStarted with this attack

            if (animator != null && attack.HasClip)
            {
                animator.SetFloat(MonsterAnimatorIds.AttackSpeed, attack.playbackSpeed);
                EnemyCombat.Play(animator, attack.StateHash, attack.crossFadeSeconds);
            }
        }

        void BeginStrike()
        {
            SetState(BossState.Strike);
            slide.Begin(transform.forward, attack.lungeDistance, attack.lungeSeconds);
        }

        void EndStrike()
        {
            SetState(BossState.Recover);
            stateSeconds = attack.recoverSeconds;
        }

        // Next combo step, or back to the chase when the combo is done.
        void AfterRecover()
        {
            if (pattern != null && attack != null && stepIndex + 1 < pattern.steps.Length)
            {
                stepIndex++;
                BeginStep();
                return;
            }
            pattern = null;
            attack = null;
            SetState(BossState.Chase);
        }

        void EnterStagger()
        {
            pattern = null;
            attack = null;
            SetState(BossState.Staggered);
            EnemyCombat.Play(animator, MonsterAnimatorIds.Stagger, def.animationCrossFade);
        }

        void EndStagger()
        {
            SetState(BossState.Recover);
            stateSeconds = def.restSeconds;
            EnemyCombat.Play(animator, MonsterAnimatorIds.Locomotion, def.animationCrossFade);
        }

        // The single place state changes; breakpoint here.
        void SetState(BossState next)
        {
            BossState previous = state;
            state = next;
            stateTime = 0f;
            slide.Clear();

            if (previous == BossState.Telegraph && next != BossState.Telegraph) TelegraphEnded?.Invoke();
            if (next == BossState.Telegraph) TelegraphStarted?.Invoke(attack);
            if ((next == BossState.Idle || next == BossState.Chase) &&
                previous != BossState.Idle && previous != BossState.Chase)
                EnemyCombat.Play(animator, MonsterAnimatorIds.Locomotion, def.animationCrossFade);
        }

        void Move(Vector3 horizontal, float dt)
        {
            verticalSpeed = controller.isGrounded && verticalSpeed < 0f
                ? balance.enemyGroundedStickSpeed
                : verticalSpeed + balance.enemyGravity * dt;
            controller.Move((horizontal + Vector3.up * verticalSpeed) * dt);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() => EnemyCombat.DrawHitbox(transform, attack, state == BossState.Strike);
#endif
    }
}
