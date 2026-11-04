using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // A crowd monster: a scuttler or a brute. It has no Update, no Rigidbody and no collider.
    // MonsterManager ticks it (Code Patterns 2.8) and everyone else finds it through
    // ICrowdQuery. It holds only its own state; the species comes from its MonsterDef (2.9).
    //
    // Musou behaviour: the crowd is there to be mown down, not to win. Most of it walks up and
    // mills around its target in a loose ring. Only a few monsters at a time hold an attack
    // slot (a token from the manager), wind up where the player can read it, and swing for
    // light damage. Everyone else waits their turn.
    public sealed class Monster : MonoBehaviour, IUnit
    {
        public enum MonsterState { Idle, Chase, Circle, Telegraph, Strike, Recover, Staggered, Dead }

        [Tooltip("Optional. Leave empty for a capsule test dummy. Default state must be Locomotion.")]
        [SerializeField] Animator animator;

        BalanceConfig balance;
        MonsterManager manager;
        IDamageable playerTarget;

        // Who it is fighting: Zahim, or an ally that drew it away from him.
        IDamageable target;
        ITargetable allyTarget;

        MonsterDef def;
        int locationId;
        int probeSlot;          // offsets ground probes so the crowd never probes on one frame
        float groundY;

        // Personality, rolled at activation so a crowd never moves in lockstep.
        float ringAngle;        // where on the ring around its target it waits, degrees
        float ringRadius;
        float circleDirection;  // +1 or -1
        float speedScale;

        MonsterState state;
        float stateTime;
        float stateSeconds;     // length of the timed state: stagger
        float health;
        float nextAttackTime;
        float nextRetargetTime;
        float tokenAt;
        bool holdsToken;
        bool tokenOnAlly;
        MonsterAttackDef attack;
        bool strikeHit;
        Slide slide;

        internal int ActiveIndex = -1;

        public MonsterState State => state;
        public MonsterDef Def => def;
        public float BodyRadius => def.bodyRadius;
        public bool HoldsAttackSlot => holdsToken;
        public bool IsFightingAlly => allyTarget != null;

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
            playerTarget = player;
        }

        internal void Activate(MonsterDef species, int home, Vector3 position, Quaternion rotation, int slot)
        {
            def = species;
            locationId = home;
            probeSlot = slot;

            ringRadius = balance.crowdRingRadius + Random.value * balance.crowdRingRadiusSpread;
            circleDirection = Random.value < 0.5f ? -1f : 1f;
            speedScale = 1f + (Random.value * 2f - 1f) * balance.crowdSpeedJitter;

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

            float now = Time.time;
            health = def.maxHealth;
            state = MonsterState.Idle;
            stateTime = 0f;
            attack = null;
            strikeHit = false;
            holdsToken = false;
            target = null;
            allyTarget = null;
            nextAttackTime = now + Random.value * balance.crowdFirstAttackDelay;
            nextRetargetTime = now + Random.value * balance.crowdRetargetInterval;
            slide.Clear();
        }

        // Called by the manager when this monster returns to the pool.
        internal void DropAttackSlot() => DropToken();

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

            DropToken();   // knocked out of its attack: the slot goes to someone else
            Vector3 away = transform.position - origin;
            away.y = 0f;
            away = away.sqrMagnitude > 0f ? away.normalized : -transform.forward;

            bool flinch = reaction == HitReaction.Flinch;
            SetState(MonsterState.Staggered);
            stateSeconds = flinch ? def.flinchSeconds : def.knockbackSeconds;
            nextAttackTime = Mathf.Max(nextAttackTime, Time.time + stateSeconds);
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
            DropToken();
            SetState(MonsterState.Dead);
            //EnemyCombat.Play(animator, MonsterAnimatorIds.Death, def.animationCrossFade);
            manager.NotifyDied(this);
        }

        // ── State machine ────────────────────────────────────────────────────────────
        // Called by MonsterManager once a frame. The crowd has no Update of its own.
        internal void Tick(float dt, float now, Vector3 playerPosition, bool playerAlive, int frame)
        {
            stateTime += dt;

            if (state == MonsterState.Dead)
            {
                if (stateTime >= def.corpseSeconds) manager.Release(this);
                return;
            }

            // Who to fight. Only between attacks and only without an attack slot, so a monster
            // never changes its mind in the middle of a swing.
            bool canRetarget = !holdsToken &&
                               (state == MonsterState.Idle || state == MonsterState.Chase || state == MonsterState.Circle);
            if (canRetarget && (now >= nextRetargetTime || target == null || !target.IsAlive))
            {
                nextRetargetTime = now + balance.crowdRetargetInterval;
                PickTarget(playerPosition, playerAlive);
            }
            bool hasTarget = target != null && target.IsAlive;

            Vector3 targetPosition = allyTarget != null ? allyTarget.Position : playerPosition;
            Vector3 toTarget = targetPosition - transform.position;
            toTarget.y = 0f;
            float distance = toTarget.magnitude;
            Vector3 direction = distance > 0.0001f ? toTarget / distance : transform.forward;
            Vector3 velocity = Vector3.zero;

            switch (state)
            {
                case MonsterState.Idle:
                    if (hasTarget && distance <= def.aggroRange) SetState(MonsterState.Chase);
                    break;

                case MonsterState.Chase:
                case MonsterState.Circle:
                    velocity = Approach(dt, now, targetPosition, direction, distance, hasTarget);
                    break;

                case MonsterState.Telegraph:
                    if (stateTime < attack.telegraphSeconds * attack.telegraphAimLockAt)
                        EnemyCombat.Turn(transform, direction, attack.telegraphTurnSpeed, dt);
                    if (stateTime >= attack.telegraphSeconds) BeginStrike();
                    break;

                case MonsterState.Strike:
                    velocity = slide.Velocity(stateTime, dt);
                    if (!strikeHit && target != null)
                        strikeHit = EnemyCombat.TryStrike(this, transform, attack, target, targetPosition,
                                                          balance.enemyHitPadding, balance.enemyDamageScale);
                    if (stateTime >= attack.strikeSeconds) EndStrike();
                    break;

                case MonsterState.Recover:
                    if (stateTime >= attack.recoverSeconds) SetState(MonsterState.Chase);
                    break;

                case MonsterState.Staggered:
                    velocity = slide.Velocity(stateTime, dt);
                    if (stateTime >= stateSeconds) SetState(MonsterState.Chase);
                    break;
            }

            Vector3 position = transform.position + velocity * dt;
            if ((frame + probeSlot) % Mathf.Max(1, balance.crowdGroundProbeInterval) == 0) ProbeGround(position);
            position.y = Mathf.MoveTowards(position.y, groundY, balance.crowdGroundFollowSpeed * dt);
            transform.position = position;
        }

        // Walk to the ring around the target and mill there, or, with an attack slot, walk
        // straight in and swing.
        Vector3 Approach(float dt, float now, Vector3 targetPosition, Vector3 direction, float distance, bool hasTarget)
        {
            if (!hasTarget)
            {
                DropToken();
                SetState(MonsterState.Idle);
                return Vector3.zero;
            }

            if (holdsToken) return Commit(dt, now, direction, distance);

            // Close enough and ready: ask for an attack slot.
            bool ready = now >= nextAttackTime &&
                         distance <= ringRadius + balance.crowdRingTolerance + balance.crowdAttackRequestRange;
            if (ready && manager.TryAcquireToken(allyTarget != null, now))
            {
                holdsToken = true;
                tokenOnAlly = allyTarget != null;
                tokenAt = now;
                SetState(MonsterState.Chase);
                return Vector3.zero;
            }

            // Otherwise: far away, close the gap; at the ring, drift around it.
            MonsterState want = distance > ringRadius + balance.crowdRingTolerance
                ? MonsterState.Chase : MonsterState.Circle;
            if (state != want) SetState(want);
            if (want == MonsterState.Circle)
                ringAngle += circleDirection * balance.crowdCircleDegreesPerSecond * dt;

            float rad = ringAngle * Mathf.Deg2Rad;
            Vector3 ringPoint = targetPosition + new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * ringRadius;
            Vector3 toPoint = ringPoint - transform.position;
            toPoint.y = 0f;
            float gap = toPoint.magnitude;

            EnemyCombat.Turn(transform, direction, def.turnSpeed, dt);
            float speed = def.moveSpeed * speedScale;
            if (animator != null)
                animator.SetFloat(MonsterAnimatorIds.Speed, gap > 0.05f ? speed : 0f, def.speedDampSeconds, dt);
            if (gap <= 0.05f) return Vector3.zero;

            // Never overshoot the point in one frame.
            float step = Mathf.Min(speed, gap / dt);
            return toPoint / gap * step;
        }

        // Holding a slot: go for the target and attack the moment it is in reach.
        Vector3 Commit(float dt, float now, Vector3 direction, float distance)
        {
            if (now - tokenAt > balance.crowdTokenHoldSeconds)
            {
                // Could not get a swing in (target ran, or the data has no attack in reach).
                DropToken();
                ScheduleNextAttack(now);
                SetState(MonsterState.Chase);
                return Vector3.zero;
            }

            MonsterAttackDef next = PickAttack(distance);
            if (next != null)
            {
                BeginAttack(next, now);
                return Vector3.zero;
            }

            EnemyCombat.Turn(transform, direction, def.turnSpeed, dt);
            float speed = distance > def.stopDistance ? def.moveSpeed * speedScale : 0f;
            if (animator != null) animator.SetFloat(MonsterAnimatorIds.Speed, speed, def.speedDampSeconds, dt);
            return transform.forward * speed;
        }

        // Zahim by default; an ally if one is closer than he is and within engage range.
        void PickTarget(Vector3 playerPosition, bool playerAlive)
        {
            Vector3 here = transform.position;
            float playerDistance = playerAlive ? EnemyCombat.FlatDistance(here, playerPosition) : float.MaxValue;

            ITargetable nearestAlly = null;
            float allyDistance = balance.crowdAllyEngageRange;
            IVillageTargets village = manager.Village;
            for (int i = 0; i < village.Count; i++)
            {
                ITargetable candidate = village[i];
                if (!candidate.IsAlive) continue;
                float d = EnemyCombat.FlatDistance(here, candidate.Position);
                if (d < allyDistance)
                {
                    allyDistance = d;
                    nearestAlly = candidate;
                }
            }

            IDamageable next = null;
            ITargetable nextAlly = null;
            if (nearestAlly != null && allyDistance < playerDistance)
            {
                next = nearestAlly;
                nextAlly = nearestAlly;
            }
            else if (playerAlive)
            {
                next = playerTarget;
            }

            // New target: approach it from the side the monster is already on, instead of
            // running through the middle of the crowd to a random point on the ring.
            if (!ReferenceEquals(next, target) && next != null)
            {
                Vector3 centre = nextAlly != null ? nextAlly.Position : playerPosition;
                Vector3 rel = here - centre;
                ringAngle = Mathf.Atan2(rel.z, rel.x) * Mathf.Rad2Deg + (Random.value - 0.5f) * 40f;
            }
            target = next;
            allyTarget = nextAlly;
        }

        MonsterAttackDef PickAttack(float distance)
        {
            MonsterAttackDef[] attacks = def.attacks;
            if (attacks == null) return null;

            for (int i = 0; i < attacks.Length; i++)
                if (distance >= attacks[i].minRange && distance <= attacks[i].maxRange) return attacks[i];
            return null;
        }

        void BeginAttack(MonsterAttackDef next, float now)
        {
            attack = next;
            strikeHit = false;
            ScheduleNextAttack(now);
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

        // The swing is over: give the slot back so another monster can step in, then recover.
        void EndStrike()
        {
            DropToken();
            SetState(MonsterState.Recover);
        }

        // Each monster's own cooldown, varied so the crowd never swings in step.
        void ScheduleNextAttack(float now)
        {
            float cooldown = attack != null ? attack.cooldownSeconds : balance.crowdFirstAttackDelay;
            float jitter = 1f + (Random.value * 2f - 1f) * balance.crowdCooldownJitter;
            nextAttackTime = now + cooldown * jitter;
        }

        void DropToken()
        {
            if (!holdsToken) return;
            holdsToken = false;
            manager.ReturnToken(tokenOnAlly);
        }

        // The single place state changes; breakpoint here.
        void SetState(MonsterState next)
        {
            bool wasLocomotion = state == MonsterState.Idle || state == MonsterState.Chase || state == MonsterState.Circle;
            bool nowLocomotion = next == MonsterState.Idle || next == MonsterState.Chase || next == MonsterState.Circle;
            state = next;
            stateTime = 0f;
            slide.Clear();
            if (nowLocomotion && !wasLocomotion)
                EnemyCombat.Play(animator, MonsterAnimatorIds.Locomotion, def.animationCrossFade);
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
