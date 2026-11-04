using System;
using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // A named unit: a captain, guardian, giant or herald. Unlike the crowd it gets its own
    // Update (Code Patterns 2.8) and a CharacterController, so Zahim's physics sweep finds it.
    //
    // The fight, as the weak-point loop expects it (R19-R26, R33, R35):
    //   1. It walks up and only starts a combo once Zahim is inside that combo's real reach
    //      and it is facing him, so it never swings at empty air.
    //   2. The encounter opens with its opening pattern (R33), usually unguardable.
    //   3. Each attack telegraphs. It tracks Zahim at first, then commits, so a late dodge works.
    //   4. A combo that leaves a punish window ends with the boss standing open. A hit from
    //      Zahim then exposes the weak point (R20c). Exposed means staggered; this reads that.
    //   5. Giants take only a share of ordinary damage until the gauge is exposed (R35).
    // Swings hit Zahim and any ally standing in the hitbox. Allies never drain the gauge.
    [RequireComponent(typeof(CharacterController))]
    public sealed class BossController : MonoBehaviour, IUnit
    {
        public enum BossState { Idle, Chase, Telegraph, Strike, Recover, Punish, Staggered, Dead }

        const int MaxStruckPerAttack = 16;

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
        IVillageTargets village;

        BossState state;
        float stateTime;
        float stateSeconds;     // length of timed states: recover, punish
        float health;
        float verticalSpeed;
        Slide slide;

        BossPatternDef pattern;
        int stepIndex;
        MonsterAttackDef attack;
        bool opened;            // the opening pattern has been used
        bool punishOpen;        // from the last strike of a punish combo until the boss recovers
        float nextPatternAt;
        float[] patternReadyAt;

        // Everyone this attack has already hit, so one swing hits each target once.
        readonly IDamageable[] struck = new IDamageable[MaxStruckPerAttack];
        int struckCount;

        public BossState State => state;
        public float Health => health;
        public float MaxHealth => def.maxHealth;
        public bool PunishWindowOpen => punishOpen;

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
            village = BattleContext.Village;

            if (player != null)
                Initialize(player.GetComponent<IPlayerMotor>(), player.GetComponent<IDamageable>(), locationId);
        }

        void Start()
        {
            // Without a target the boss silently does nothing, which looks like a broken boss.
            if (motor == null)
                Debug.LogError($"{name}: no player. Assign Player on the BossController or call Initialize.", this);
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

            bool fromZahim = info.Source == null;

            // R20c, R33: a hit while it stands open is what shows the gauge.
            if (punishOpen && fromZahim && !IsExposed()) WeakPoint?.TryExpose(ExposeSource.PunishWindow);

            // R35: a giant shrugs off most damage until its gauge is exposed.
            float amount = def.tier == Tier.Giant && !IsExposed()
                ? info.Amount * balance.giantDamageOutsideStun
                : info.Amount;

            health = Mathf.Max(0f, health - amount);
            HealthChanged?.Invoke(health, def.maxHealth);

            // R22: only Zahim's blows drain the gauge; five allies must not empty it for him.
            if (fromZahim) WeakPoint?.Drain(info.WeakPointDrain);

            if (health <= 0f) Die();
        }

        bool IsExposed() =>
            WeakPoint != null &&
            (WeakPoint.State == WeakPointState.Exposed || WeakPoint.State == WeakPointState.Smashing);

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
            float now = Time.time;
            stateTime += dt;

            Vector3 toPlayer = motor.Position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 direction = distance > 0.0001f ? toPlayer / distance : transform.forward;
            bool playerAlive = !motor.IsDead;

            // The weak point overrides everything but death: exposed means stand and take it.
            bool exposed = IsExposed();
            if (exposed && state != BossState.Staggered) EnterStagger();
            else if (!exposed && state == BossState.Staggered) EndStagger();

            Vector3 velocity = Vector3.zero;
            switch (state)
            {
                case BossState.Idle:
                    if (playerAlive && distance <= def.aggroRange) SetState(BossState.Chase);
                    break;

                case BossState.Chase:
                    velocity = Chase(dt, now, direction, distance, playerAlive);
                    break;

                case BossState.Telegraph:
                    // Tracks Zahim for the first part of the wind-up, then commits.
                    if (stateTime < attack.telegraphSeconds * attack.telegraphAimLockAt)
                        EnemyCombat.Turn(transform, direction, attack.telegraphTurnSpeed, dt);
                    if (stateTime >= attack.telegraphSeconds) BeginStrike();
                    break;

                case BossState.Strike:
                    velocity = slide.Velocity(stateTime, dt);
                    StrikeTargets();
                    if (stateTime >= attack.strikeSeconds) EndStrike();
                    break;

                case BossState.Recover:
                    if (stateTime >= stateSeconds) AfterRecover(now);
                    break;

                case BossState.Punish:
                    if (stateTime >= stateSeconds) SetState(BossState.Chase);   // clears the window
                    break;

                case BossState.Staggered:
                    break;   // held until the weak point closes
            }

            Move(velocity, dt);
        }

        Vector3 Chase(float dt, float now, Vector3 direction, float distance, bool playerAlive)
        {
            if (!playerAlive)
            {
                SetState(BossState.Idle);
                return Vector3.zero;
            }

            EnemyCombat.Turn(transform, direction, def.turnSpeed, dt);

            // Only commit to an attack when it can land and the boss is actually facing Zahim.
            bool facing = Vector3.Angle(transform.forward, direction) <= balance.bossFacingDegrees;
            if (now >= nextPatternAt && facing && TryStartPattern(distance, now)) return Vector3.zero;

            float speed = distance > def.stopDistance ? def.moveSpeed : 0f;
            if (animator != null) animator.SetFloat(MonsterAnimatorIds.Speed, speed, def.speedDampSeconds, dt);
            return transform.forward * speed;
        }

        // R33: the encounter opens with its opening pattern. After that, a weighted pick among
        // the combos whose reach fits and whose cooldown is over.
        bool TryStartPattern(float distance, float now)
        {
            if (!opened)
            {
                if (def.openingPattern == null)
                {
                    opened = true;
                }
                else
                {
                    if (!InReach(def.openingPattern, distance)) return false;   // wait until it can land
                    opened = true;
                    StartPattern(def.openingPattern, now);
                    return true;
                }
            }

            BossPatternDef[] patterns = def.patterns;
            if (patterns == null) return false;

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

            StartPattern(patterns[chosen], now);
            return true;
        }

        // A combo can start only when Zahim is inside its first attack's real reach.
        static bool InReach(BossPatternDef p, float distance)
        {
            if (p == null || p.steps == null || p.steps.Length == 0 || p.steps[0] == null) return false;
            return distance >= p.minRange && distance <= Mathf.Min(p.maxRange, p.steps[0].Reach);
        }

        bool Eligible(int index, float distance, float now) =>
            InReach(def.patterns[index], distance) && now >= patternReadyAt[index];

        void StartPattern(BossPatternDef chosen, float now)
        {
            for (int i = 0; i < def.patterns.Length; i++)
                if (def.patterns[i] == chosen) patternReadyAt[i] = now + chosen.cooldownSeconds;

            pattern = chosen;
            stepIndex = 0;
            BeginStep();
        }

        void BeginStep()
        {
            attack = pattern.steps[stepIndex];
            Array.Clear(struck, 0, struckCount);
            struckCount = 0;
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

        // One swing hits Zahim and every ally in the hitbox, each once.
        void StrikeTargets()
        {
            float padding = balance.enemyHitPadding;
            float scale = balance.enemyDamageScale;

            if (target != null && !HasStruck(target) &&
                EnemyCombat.TryStrike(this, transform, attack, target, motor.Position, padding, scale))
                MarkStruck(target);

            for (int i = 0; i < village.Count; i++)
            {
                ITargetable ally = village[i];
                if (!ally.IsAlive || HasStruck(ally)) continue;
                if (EnemyCombat.TryStrike(this, transform, attack, ally, ally.Position, padding, scale))
                    MarkStruck(ally);
            }
        }

        bool HasStruck(IDamageable who)
        {
            for (int i = 0; i < struckCount; i++)
                if (ReferenceEquals(struck[i], who)) return true;
            return false;
        }

        void MarkStruck(IDamageable who)
        {
            if (struckCount < MaxStruckPerAttack) struck[struckCount++] = who;
        }

        void EndStrike()
        {
            SetState(BossState.Recover);
            stateSeconds = attack.recoverSeconds;

            // The recovery after the last strike of a punish combo already counts as open.
            bool lastStep = pattern != null && stepIndex + 1 >= pattern.steps.Length;
            punishOpen = lastStep && pattern.leavesPunishWindow;
        }

        // Next combo step, or the end of the combo: rest, then either stand open or chase.
        void AfterRecover(float now)
        {
            if (pattern != null && attack != null && stepIndex + 1 < pattern.steps.Length)
            {
                stepIndex++;
                BeginStep();
                return;
            }

            BossPatternDef finished = pattern;
            pattern = null;
            attack = null;
            nextPatternAt = now + def.attackGapSeconds;

            if (finished != null && finished.leavesPunishWindow)
            {
                SetState(BossState.Punish);
                stateSeconds = finished.punishSeconds;
                punishOpen = true;
                return;
            }
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

            // The window spans the last recovery and the punish pose, and nothing else.
            if (next != BossState.Recover && next != BossState.Punish) punishOpen = false;

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
