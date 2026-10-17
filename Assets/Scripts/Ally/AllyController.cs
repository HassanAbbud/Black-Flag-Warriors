using Ahoy.Core;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Events;

namespace Ahoy.Allies
{
    public enum AllyState { Idle, Patrol, Chase, Attack }

    // Ally NPC brain: Idle -> Patrol -> Chase -> Attack. Add Escape later as another enum entry + case.
    // Only talks to targets through IDamageable; never casts to a concrete Monster or AllyUnit.
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class AllyController : MonoBehaviour
    {
        static readonly int SpeedHash = Animator.StringToHash("Speed");
        static readonly int AttackHash = Animator.StringToHash("Attack");
        static readonly Collider[] hitBuffer = new Collider[16];

        [Header("Detection")]
        [SerializeField] LayerMask enemyMask;
        [SerializeField] LayerMask obstacleMask;               // Layer blocks eyesight, nullable
        [SerializeField] float detectRange = 12f;
        [SerializeField, Range(1f, 360f)] float fieldOfView = 360f;
        [SerializeField] float loseTargetRange = 18f;          // bigger than detectRange to avoid losing target when player runs away
        [SerializeField] float scanInterval = 0.2f;
        [SerializeField] float eyeHeight = 1.2f;

        [Header("Idle / Patrol")]
        [SerializeField] Vector2 idleDuration = new Vector2(2f, 4f);
        [SerializeField] Transform[] patrolPoints;             // patrol waypoints, optional
        [SerializeField] float patrolRadius = 8f;
        [SerializeField] float patrolSpeed = 2f;
        [SerializeField] float arriveDistance = 0.5f;

        [Header("Chase")]
        [SerializeField] float chaseSpeed = 4.5f;
        [SerializeField] float repathInterval = 0.25f;

        [Header("Attack")]
        [SerializeField] float attackRange = 1.8f;
        [SerializeField] float attackExitBuffer = 0.5f;        // exit attack when target is beyond attackRange+buffer
        [SerializeField] float attackCooldown = 1f;
        [SerializeField] float attackDamage = 10f;
        [SerializeField] HitReaction hitReaction = HitReaction.Flinch;
        [SerializeField] float weakPointDrain = 4f;            // R22: light 4 / strong 10 / tool 15 / special 35
        [SerializeField] int hitstopFrames = 0;
        [SerializeField] bool unblockable;                     // R51
        [SerializeField] float turnSpeed = 540f;               // degrees per second
        [SerializeField] float facingTolerance = 20f;          // attack only if facing within this angle of the target
        [SerializeField] UnityEvent onAttack;

        [Header("Animation (optional)")]
        [SerializeField] Animator animator;                    // parameter：float "Speed"，trigger "Attack"

        NavMeshAgent agent;
        IDamageable self;
        IUnit selfUnit;
        Vector3 home;
        int patrolIndex = -1;
        float stateTimer;
        float idleWait;
        float scanTimer;
        float repathTimer;
        float attackTimer;
        Transform target;
        IDamageable targetDamageable;

        public AllyState State { get; private set; }
        public Transform Target => target;

        void Awake()
        {
            agent = GetComponent<NavMeshAgent>();
            home = transform.position;

            selfUnit = GetComponent<IUnit>();
            self = selfUnit;   // IUnit : IDamageable
            Debug.Assert(selfUnit != null, "AllyController needs an IUnit (AllyUnit) on the same object", this);
        }

        void Start() => ChangeState(AllyState.Idle);

        void Update()
        {
            if (!agent.isOnNavMesh) return;

            float dt = Time.deltaTime;
            stateTimer += dt;
            attackTimer -= dt; // cooldown timer counts down even when not attacking, so the first attack after a long chase is immediate

            // stop moving if dead, to avoid NavMeshAgent errors
            if (self != null && !self.IsAlive)
            {
                agent.ResetPath();
                return;
            }

            switch (State)
            {
                case AllyState.Idle: UpdateIdle(); break;
                case AllyState.Patrol: UpdatePatrol(); break;
                case AllyState.Chase: UpdateChase(dt); break;
                case AllyState.Attack: UpdateAttack(dt); break;
            }

            if (animator != null) animator.SetFloat(SpeedHash, agent.velocity.magnitude);
        }

        // ---------- external ----------

        // force this ally to engage a specific enemy, bypassing the normal scan logic
        public void Engage(Transform enemy)
        {
            if (enemy == null) return;
            IDamageable dmg = enemy.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive || !IsHostile(dmg)) return;

            SetTarget(enemy, dmg);
            ChangeState(AllyState.Chase);
        }

        // ---------- state machine ----------

        void ChangeState(AllyState next)
        {
            State = next;
            stateTimer = 0f;

            switch (next)
            {
                case AllyState.Idle:
                    agent.ResetPath();
                    idleWait = Random.Range(idleDuration.x, idleDuration.y);
                    break;

                case AllyState.Patrol:
                    agent.speed = patrolSpeed;
                    agent.stoppingDistance = 0f;
                    if (!TryPickPatrolDestination()) ChangeState(AllyState.Idle);
                    break;

                case AllyState.Chase:
                    agent.speed = chaseSpeed;
                    agent.stoppingDistance = attackRange * 0.9f; // stop a bit short of attack range to avoid overshooting
                    repathTimer = 0f;                            // pathfinding immediately on entering chase state
                    break;

                case AllyState.Attack:
                    agent.ResetPath();
                    break;
            }
        }

        void UpdateIdle()
        {
            if (ScanForTarget()) return;
            if (stateTimer >= idleWait) ChangeState(AllyState.Patrol);
        }

        void UpdatePatrol()
        {
            if (ScanForTarget()) return;

            bool arrived = !agent.pathPending && agent.remainingDistance <= arriveDistance;
            bool blocked = !agent.pathPending && agent.pathStatus != NavMeshPathStatus.PathComplete;
            if (arrived || blocked) ChangeState(AllyState.Idle);
        }

        void UpdateChase(float dt)
        {
            if (!IsTargetValid()) { LoseTarget(); return; }

            float sqrDist = (target.position - transform.position).sqrMagnitude;
            if (sqrDist > loseTargetRange * loseTargetRange) { LoseTarget(); return; }
            if (sqrDist <= attackRange * attackRange) { ChangeState(AllyState.Attack); return; }

            repathTimer -= dt;
            if (repathTimer <= 0f)
            {
                repathTimer = repathInterval;
                agent.SetDestination(target.position);
            }
        }

        void UpdateAttack(float dt)
        {
            if (!IsTargetValid()) { LoseTarget(); return; }

            Vector3 toTarget = target.position - transform.position;
            float exitRange = attackRange + attackExitBuffer;
            if (toTarget.sqrMagnitude > exitRange * exitRange) { ChangeState(AllyState.Chase); return; }

            Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
            if (flat.sqrMagnitude > 0.0001f)
            {
                Quaternion look = Quaternion.LookRotation(flat);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, turnSpeed * dt);

                if (attackTimer <= 0f && Vector3.Angle(transform.forward, flat) <= facingTolerance)
                    PerformAttack();
            }
        }

        void PerformAttack()
        {
            attackTimer = attackCooldown;

            if (animator != null) animator.SetTrigger(AttackHash);
            onAttack?.Invoke();

            // move this to animation event if you want to sync the damage with the animation
            var info = new DamageInfo(
                attackDamage,
                transform.position,   // Origin：attack from self position, not target position
                hitReaction,
                hitstopFrames,
                weakPointDrain,
                selfUnit,             
                unblockable);

            targetDamageable.TakeDamage(in info);
        }

        // ---------- target management ----------

        bool IsHostile(IDamageable other)
        {
            Faction mine = self != null ? self.Faction : Faction.Village;
            return FactionRules.AreHostile(mine, other.Faction);
        }

        bool ScanForTarget()
        {
            scanTimer -= Time.deltaTime;
            if (scanTimer > 0f) return false;
            scanTimer = scanInterval;

            if (!TryFindBestTarget(out Transform found, out IDamageable dmg)) return false;

            SetTarget(found, dmg);
            ChangeState(AllyState.Chase);
            return true;
        }

        bool TryFindBestTarget(out Transform best, out IDamageable bestDamageable)
        {
            best = null;
            bestDamageable = null;

            Vector3 eye = transform.position + Vector3.up * eyeHeight;
            int count = Physics.OverlapSphereNonAlloc(
                transform.position, detectRange, hitBuffer, enemyMask, QueryTriggerInteraction.Ignore);

            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = hitBuffer[i];
                IDamageable dmg = col.GetComponentInParent<IDamageable>();
                if (dmg == null || !dmg.IsAlive || !IsHostile(dmg)) continue;

                Transform candidate = col.attachedRigidbody != null ? col.attachedRigidbody.transform : col.transform;
                Vector3 toTarget = candidate.position - transform.position;

                // field of view check
                if (fieldOfView < 360f)
                {
                    Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                    if (flat.sqrMagnitude > 0.0001f && Vector3.Angle(transform.forward, flat) > fieldOfView * 0.5f) continue;
                }

                // blocked by obstacle check (optional)
                if (obstacleMask.value != 0 &&
                    Physics.Linecast(eye, candidate.position + Vector3.up * eyeHeight, obstacleMask, QueryTriggerInteraction.Ignore))
                    continue;

                float sqr = toTarget.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = candidate;
                    bestDamageable = dmg;
                }
            }

            return best != null;
        }

        void SetTarget(Transform t, IDamageable dmg)
        {
            target = t;
            targetDamageable = dmg;
        }

        bool IsTargetValid()
        {
            // check target is alive, active, and not null. 
            return target != null
                && target.gameObject.activeInHierarchy
                && targetDamageable != null
                && targetDamageable.IsAlive;
        }

        void LoseTarget()
        {
            target = null;
            targetDamageable = null;
            ChangeState(AllyState.Idle);
        }

        // ---------- patrol ----------

        bool TryPickPatrolDestination()
        {
            Vector3 destination;
            if (patrolPoints != null && patrolPoints.Length > 0)
            {
                patrolIndex = (patrolIndex + 1) % patrolPoints.Length;
                destination = patrolPoints[patrolIndex].position;
            }
            else
            {
                Vector2 offset = Random.insideUnitCircle * patrolRadius;
                destination = home + new Vector3(offset.x, 0f, offset.y);
            }

            if (!NavMesh.SamplePosition(destination, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
            return agent.SetDestination(hit.position);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, detectRange);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, attackRange);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, loseTargetRange);
        }
    }
}