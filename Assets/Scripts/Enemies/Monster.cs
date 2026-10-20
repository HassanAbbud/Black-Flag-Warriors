using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // A crowd monster: a scuttler or a brute. It has no Update, no Rigidbody and no collider.
    // MonsterManager ticks it (Code Patterns 2.8) and the player's sweep finds it through
    // ICrowdQuery. It holds only its own state; the species comes from its MonsterDef (2.9).
    public sealed class Monster : MonoBehaviour, IUnit
    {
        public enum MonsterState { Idle, Chase, Telegraph, Strike, Recover, Staggered, Dead }

        [Tooltip("Optional. Leave empty for a capsule test dummy. Default state must be Locomotion.")]
        [SerializeField] Animator animator;

        BalanceConfig balance;
        MonsterManager manager;
        IDamageable target;

        MonsterDef def;
        int locationId;
        int probeSlot;          // offsets ground probes so the crowd never probes on one frame
        float groundY;

        MonsterState state;
        float stateTime;
        float stateSeconds;     // length of the timed state: stagger
        float health;
        float nextAttackTime;
        MonsterAttackDef attack;
        bool strikeHit;
        Slide slide;

        internal int ActiveIndex = -1;

        public MonsterState State => state;
        public MonsterDef Def => def;
        public float BodyRadius => def.bodyRadius;

        public Faction Faction => Faction.Monsters;
        public bool IsAlive => state != MonsterState.Dead;
        public Tier Tier => def.tier;
        public int LocationId => locationId;     // R8: only its home portal's meter cares
        public float Strength => def.strength;   // R64
        public IWeakPoint WeakPoint => null;     // R19: crowd units have none

        // ── Pool plumbing, called by MonsterManager ──────────────────────────────────
        internal void Bind(MonsterManager owner, BalanceConfig config, IDamageable player)
        {
            manager = owner;
            balance = config;
            target = player;
        }

        internal void Activate(MonsterDef species, int home, Vector3 position, Quaternion rotation, int slot)
        {
            def = species;
            locationId = home;
            probeSlot = slot;

            groundY = position.y;
            ProbeGround(position);
            transform.SetPositionAndRotation(new Vector3(position.x, groundY, position.z), rotation);
            gameObject.SetActive(true);   // OnEnable resets the rest
        }

        // A pooled object resets itself here (Code Patterns 2.7). Skipped on the first
        // Instantiate, before any species has been assigned.
        void OnEnable()
        {
            if (def == null) return;

            health = def.maxHealth;
            state = MonsterState.Idle;
            stateTime = 0f;
            attack = null;
            strikeHit = false;
            nextAttackTime = 0f;
            slide.Clear();
        }

        // ── Damage ───────────────────────────────────────────────────────────────────
        public void TakeDamage(in DamageInfo info)
        {
            if (state == MonsterState.Dead) return;

            health -= info.Amount;
            if (health <= 0f)
            {
                Die();
                return;
            }
            ApplyReaction(info.Reaction, info.Origin);
        }

        void ApplyReaction(HitReaction reaction, Vector3 origin)
        {
            if (reaction == HitReaction.None || reaction < def.minReaction) return;

            Vector3 away = transform.position - origin;
            away.y = 0f;
            away = away.sqrMagnitude > 0f ? away.normalized : -transform.forward;

            bool flinch = reaction == HitReaction.Flinch;
            SetState(MonsterState.Staggered);
            stateSeconds = flinch ? def.flinchSeconds : def.knockbackSeconds;
            if (!flinch)
            {
                float distance = reaction == HitReaction.Knockback ? def.knockbackDistance : def.blowawayDistance;
                slide.Begin(away, distance, stateSeconds);
            }
            EnemyCombat.Play(animator, flinch ? MonsterAnimatorIds.HitFlinch : MonsterAnimatorIds.HitKnockback,
                             def.animationCrossFade);
        }

        void Die()
        {
            health = 0f;
            SetState(MonsterState.Dead);
            EnemyCombat.Play(animator, MonsterAnimatorIds.Death, def.animationCrossFade);
            manager.NotifyDied(this);
        }

        // ── State machine ────────────────────────────────────────────────────────────
        // Called by MonsterManager once a frame. The crowd has no Update of its own.
        internal void Tick(float dt, Vector3 playerPosition, bool playerAlive, int frame)
        {
            stateTime += dt;

            Vector3 toPlayer = playerPosition - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            Vector3 direction = distance > 0.0001f ? toPlayer / distance : transform.forward;
            Vector3 velocity = Vector3.zero;

            switch (state)
            {
                case MonsterState.Idle:
                    if (playerAlive && distance <= def.aggroRange) SetState(MonsterState.Chase);
                    break;

                case MonsterState.Chase:
                    velocity = Chase(dt, direction, distance, playerAlive);
                    break;

                case MonsterState.Telegraph:
                    EnemyCombat.Turn(transform, direction, attack.telegraphTurnSpeed, dt);
                    if (stateTime >= attack.telegraphSeconds) BeginStrike();
                    break;

                case MonsterState.Strike:
                    velocity = slide.Velocity(stateTime, dt);
                    if (!strikeHit)
                        strikeHit = EnemyCombat.TryStrike(this, transform, attack, target, playerPosition,
                                                          balance.enemyHitPadding);
                    if (stateTime >= attack.strikeSeconds) SetState(MonsterState.Recover);
                    break;

                case MonsterState.Recover:
                    if (stateTime >= attack.recoverSeconds) SetState(MonsterState.Chase);
                    break;

                case MonsterState.Staggered:
                    velocity = slide.Velocity(stateTime, dt);
                    if (stateTime >= stateSeconds) SetState(MonsterState.Chase);
                    break;

                case MonsterState.Dead:
                    if (stateTime >= def.corpseSeconds) manager.Release(this);
                    return;
            }

            Vector3 position = transform.position + velocity * dt;
            if ((frame + probeSlot) % Mathf.Max(1, balance.crowdGroundProbeInterval) == 0) ProbeGround(position);
            position.y = Mathf.MoveTowards(position.y, groundY, balance.crowdGroundFollowSpeed * dt);
            transform.position = position;
        }

        Vector3 Chase(float dt, Vector3 direction, float distance, bool playerAlive)
        {
            if (!playerAlive)
            {
                SetState(MonsterState.Idle);
                return Vector3.zero;
            }

            MonsterAttackDef next = PickAttack(distance);
            if (next != null && Time.time >= nextAttackTime)
            {
                BeginAttack(next);
                return Vector3.zero;
            }

            EnemyCombat.Turn(transform, direction, def.turnSpeed, dt);
            float speed = distance > def.stopDistance ? def.moveSpeed : 0f;
            if (animator != null) animator.SetFloat(MonsterAnimatorIds.Speed, speed, def.speedDampSeconds, dt);
            return transform.forward * speed;
        }

        MonsterAttackDef PickAttack(float distance)
        {
            MonsterAttackDef[] attacks = def.attacks;
            if (attacks == null) return null;

            for (int i = 0; i < attacks.Length; i++)
                if (distance >= attacks[i].minRange && distance <= attacks[i].maxRange) return attacks[i];
            return null;
        }

        void BeginAttack(MonsterAttackDef next)
        {
            attack = next;
            strikeHit = false;
            nextAttackTime = Time.time + next.cooldownSeconds;
            SetState(MonsterState.Telegraph);

            if (animator != null && next.HasClip)
            {
                animator.SetFloat(MonsterAnimatorIds.AttackSpeed, next.playbackSpeed);
                EnemyCombat.Play(animator, next.StateHash, next.crossFadeSeconds);
            }
        }

        void BeginStrike()
        {
            SetState(MonsterState.Strike);
            slide.Begin(transform.forward, attack.lungeDistance, attack.lungeSeconds);
        }

        // The single place state changes; breakpoint here.
        void SetState(MonsterState next)
        {
            bool returning = (next == MonsterState.Idle || next == MonsterState.Chase) &&
                             state != MonsterState.Idle && state != MonsterState.Chase;
            state = next;
            stateTime = 0f;
            slide.Clear();
            if (returning) EnemyCombat.Play(animator, MonsterAnimatorIds.Locomotion, def.animationCrossFade);
        }

        void ProbeGround(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * balance.crowdGroundProbeUp;
            float reach = balance.crowdGroundProbeUp + balance.crowdGroundProbeDown;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, balance.groundMask,
                                QueryTriggerInteraction.Ignore))
                groundY = hit.point.y;
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected() => EnemyCombat.DrawHitbox(transform, attack, state == MonsterState.Strike);
#endif
    }
}
