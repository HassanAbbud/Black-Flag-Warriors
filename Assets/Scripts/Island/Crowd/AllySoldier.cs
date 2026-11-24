using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // A village soldier in the battleground crowd (W5.2). No Update, no NavMeshAgent, no collider:
    // AllyCrowd ticks it (Code Patterns 2.8). It finds monsters through BattleContext.Crowd and
    // joins BattleContext.Village while alive, so the monster crowd turns and fights it.
    //   Hold   : no monster in reach; spreads out around its post
    //   Chase  : runs at the nearest monster
    //   Swing  : stops, swings, lands the blow at hitAt
    //   Stagger: hit; flinches or slides back
    //   Dead   : plays its death, then goes back to the pool
    public sealed class AllySoldier : MonoBehaviour, IUnit, ITargetable, IRouteFollower
    {
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int LocomotionId = Animator.StringToHash("Locomotion");
        static readonly int AttackId = Animator.StringToHash("Attack");
        static readonly int HitId = Animator.StringToHash("Hit");
        static readonly int DeathId = Animator.StringToHash("Death");
        const float CrossFade = 0.1f;
        const float SpeedDamp = 0.1f;

        [Tooltip("States: Locomotion (blend on Speed), Attack, Hit, Death.")]
        [SerializeField] Animator animator;

        const float RouteReach = 1.5f;
        const float RouteSpread = 4f;
        enum SoldierState { Hold, Chase, Swing, Stagger, Dead }

        AllyCrowd crowd;
        BalanceConfig balance;
        AllySoldierDef def;
        int locationId;
        int slot;
        Vector3 post;
        Vector3 postOffset;
        float speedScale;
        float groundY;
        float groundOffset;     // standing on a rock or bridge above the terrain

        SoldierState state;
        float stateTime;
        float health;
        float nextRetargetAt;
        float nextSwingAt;
        bool blowLanded;
        IDamageable target;
        Transform targetBody;
        Vector3 slideVelocity;
        Vector3 lastVelocity;   // R70: carried on the frames it does not think
        Vector3[] route;        // a battle front's road; the post is its end
        int routeIndex;
        IUnit lastAttacker;

        internal int ActiveIndex = -1;
        internal float SavedTime;
        SkinnedMeshRenderer[] skins;
        int lod;
        float animSaved;        // R71: time owed to a hand-stepped animator
        internal int TickSlot => slot;
        internal AllySoldierDef Def => def;

        public Faction Faction => Faction.Village;
        public bool IsAlive => state != SoldierState.Dead;
        public Tier Tier => def.tier;
        public int LocationId => locationId;
        public float Strength => def.strength;   // R64
        public IWeakPoint WeakPoint => null;
        public Vector3 Position => transform.position;
        public float BodyRadius => def.bodyRadius;

        // ── Pool plumbing, called by AllyCrowd ───────────────────────────────────────
        internal void Bind(AllyCrowd owner, BalanceConfig config)
        {
            crowd = owner;
            balance = config;
            skins = GetComponentsInChildren<SkinnedMeshRenderer>(true);
            lod = -1;
        }

        // R71, Hyrule Warriors style: shadows only up close, lighter skinning further out.
        // R71: half-rate animation. Called every frame; only acts while the animator is hand-stepped,
        // and skips units nobody can see.
        internal void StepAnimation(float dt, int frame)
        {
            if (animator == null || animator.enabled) return;
            animSaved = Mathf.Min(animSaved + dt, 0.5f);
            if (((frame + TickSlot) & 1) != 0 || skins.Length == 0 || !skins[0].isVisible) return;
            animator.Update(animSaved);
            animSaved = 0f;
        }

        internal void SetLod(float flatSqr, BalanceConfig config)
        {
            int band = flatSqr < config.crowdShadowRadius * config.crowdShadowRadius ? 0
                     : flatSqr < config.crowdLowSkinRadius * config.crowdLowSkinRadius ? 1 : 2;
            // R71: far units' animators are stepped by hand, every other frame (StepAnimation).
            if (animator != null) animator.enabled = flatSqr < config.crowdHalfAnimRadius * config.crowdHalfAnimRadius;
            if (band == lod) return;
            lod = band;
            for (int i = 0; i < skins.Length; i++)
            {
                skins[i].shadowCastingMode = band == 0 ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                skins[i].quality = band == 0 ? SkinQuality.Auto : band == 1 ? SkinQuality.Bone2 : SkinQuality.Bone1;
            }
        }

        internal void Activate(AllySoldierDef species, int home, Vector3 position, Quaternion rotation, int spawnSlot)
        {
            def = species;
            locationId = home;
            slot = spawnSlot;
            post = position;
            Vector2 spread = Random.insideUnitCircle * def.postSpread;
            postOffset = new Vector3(spread.x, 0f, spread.y);
            speedScale = 1f + (Random.value * 2f - 1f) * def.speedJitter;
            ProbeGround(position);
            SavedTime = 0f;
            lod = -1;
            if (animator != null) animator.enabled = true;
            transform.SetPositionAndRotation(new Vector3(position.x, groundY, position.z), rotation);
            gameObject.SetActive(true);   // OnEnable resets the rest
        }

        void OnEnable()
        {
            if (def == null) return;
            health = def.maxHealth;
            state = SoldierState.Hold;
            stateTime = 0f;
            target = null;
            targetBody = null;
            slideVelocity = Vector3.zero;
            lastVelocity = Vector3.zero;
            lastAttacker = null;
            route = null;
            nextRetargetAt = Time.time + Random.value * def.retargetInterval;
            nextSwingAt = Time.time + Random.value * def.attackCooldown;
            BattleContext.Village.Add(this);
            Play(LocomotionId);
        }

        void OnDisable() => BattleContext.Village.Remove(this);

        public void FollowRoute(Vector3[] points)
        {
            route = points;
            routeIndex = 0;
            post = points[points.Length - 1];
            Vector2 side = Random.insideUnitCircle * RouteSpread;
            postOffset = new Vector3(side.x, 0f, side.y);
        }

        // ── Damage ───────────────────────────────────────────────────────────────────
        public void TakeDamage(in DamageInfo info)
        {
            if (state == SoldierState.Dead) return;
            health -= info.Amount / Power;
            lastAttacker = info.Source;
            if (health <= 0f)
            {
                Die();
                return;
            }
            if (info.Reaction == HitReaction.None) return;

            Vector3 away = transform.position - info.Origin;
            away.y = 0f;
            away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
            bool flinch = info.Reaction == HitReaction.Flinch;
            float seconds = flinch ? def.flinchSeconds : def.knockbackSeconds;
            slideVelocity = flinch ? Vector3.zero : away * (def.knockbackDistance / Mathf.Max(0.05f, seconds));
            SetState(SoldierState.Stagger);
            Play(HitId);
        }

        void Die()
        {
            health = 0f;
            SetState(SoldierState.Dead);
            BattleContext.Village.Remove(this);   // monsters stop picking it at once
            Play(DeathId);
            BattleEvents.RaiseUnitKilled(this, lastAttacker);   // R64: comes off its location's ally strength
        }

        // ── State machine, ticked by AllyCrowd ───────────────────────────────────────
        // dt is the time since it last thought; moveDt is this frame's, since Glide moved it in between.
        internal void Tick(float dt, float moveDt, float now, int frame)
        {
            stateTime += dt;
            if (state == SoldierState.Dead)
            {
                if (stateTime >= def.corpseSeconds) crowd.Release(this);
                return;
            }

            if (state != SoldierState.Swing && state != SoldierState.Stagger && now >= nextRetargetAt)
            {
                nextRetargetAt = now + def.retargetInterval;
                FindTarget();
            }
            bool hasTarget = target != null && target.IsAlive && targetBody != null && targetBody.gameObject.activeInHierarchy;

            Vector3 velocity = Vector3.zero;
            switch (state)
            {
                case SoldierState.Hold:
                case SoldierState.Chase:
                    velocity = hasTarget ? Engage(dt, now) : HoldPost(dt);
                    break;

                case SoldierState.Swing:
                    if (!blowLanded && stateTime >= def.hitAt)
                    {
                        blowLanded = true;
                        if (hasTarget) Strike();
                    }
                    if (stateTime >= def.swingSeconds) SetState(SoldierState.Chase);
                    break;

                case SoldierState.Stagger:
                    velocity = slideVelocity;
                    float length = slideVelocity.sqrMagnitude > 0f ? def.knockbackSeconds : def.flinchSeconds;
                    if (stateTime >= length)
                    {
                        slideVelocity = Vector3.zero;
                        SetState(SoldierState.Chase);
                        Play(LocomotionId);
                    }
                    break;
            }

            if (animator != null) animator.SetFloat(SpeedId, new Vector3(velocity.x, 0f, velocity.z).magnitude, SpeedDamp, dt);
            lastVelocity = velocity;
            Move(velocity, moveDt, frame);
        }

        // R70: a far soldier that is not thinking this frame keeps going the way it was.
        internal void Glide(float dt, int frame)
        {
            if (state == SoldierState.Dead) return;
            Move(lastVelocity, dt, frame);
        }

        void Move(Vector3 velocity, float dt, int frame)
        {
            Vector3 position = transform.position + velocity * dt;
            if ((frame + slot) % Mathf.Max(1, balance.crowdGroundProbeInterval) == 0) ProbeGround(position);
            else groundY = CrowdGround.Floor(position) + groundOffset;   // the terrain every frame, so slopes never leave it hanging
            position.y = Mathf.MoveTowards(position.y, groundY, balance.crowdGroundFollowSpeed * dt);
            transform.position = position;
        }

        Vector3 Engage(float dt, float now)
        {
            Vector3 to = targetBody.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            Vector3 direction = distance > 0.0001f ? to / distance : transform.forward;
            Turn(direction, dt);

            if (distance <= def.attackRange)
            {
                if (now >= nextSwingAt) BeginSwing(now);
                return Vector3.zero;
            }
            if (state != SoldierState.Chase) SetState(SoldierState.Chase);
            float speed = def.moveSpeed * speedScale;
            float step = Mathf.Min(speed, (distance - def.attackRange * 0.9f) / Mathf.Max(dt, 0.0001f));
            return direction * Mathf.Max(0f, step);
        }

        Vector3 HoldPost(float dt)
        {
            if (state != SoldierState.Hold) SetState(SoldierState.Hold);
            // Marching: the next road point; then the post at the road's end.
            Vector3 goal = route != null && routeIndex < route.Length ? route[routeIndex] : post;
            Vector3 to = goal + postOffset - transform.position;
            to.y = 0f;
            float gap = to.magnitude;
            if (route != null && routeIndex < route.Length && gap <= RouteReach)
            {
                routeIndex++;
                return Vector3.zero;
            }
            if (gap <= 0.3f) return Vector3.zero;
            Vector3 direction = to / gap;
            Turn(direction, dt);
            float speed = def.moveSpeed * speedScale * (gap > 4f ? 1f : 0.5f);
            return direction * Mathf.Min(speed, gap / Mathf.Max(dt, 0.0001f));
        }

        void FindTarget()
        {
            if (BattleContext.Crowd.TryFindNearest(transform.position, def.engageRange, out IDamageable nearest)
                && nearest is Component body)
            {
                target = nearest;
                targetBody = body.transform;
                return;
            }
            target = null;
            targetBody = null;
        }

        void BeginSwing(float now)
        {
            SetState(SoldierState.Swing);
            blowLanded = false;
            nextSwingAt = now + def.attackCooldown * (1f + (Random.value * 2f - 1f) * def.cooldownJitter);
            Play(AttackId);
        }

        void Strike()
        {
            Vector3 to = targetBody.position - transform.position;
            to.y = 0f;
            if (to.magnitude > def.attackRange + balance.enemyHitPadding + def.bodyRadius) return;
            var info = new DamageInfo(def.damage * Power, transform.position, def.reaction, 0, def.weakPointDrain, this);
            target.TakeDamage(in info);
        }

        float Power => Mathf.Lerp(def.powerAtLowMorale, def.powerAtHighMorale, crowd.Morale);

        void Turn(Vector3 direction, float dt)
        {
            if (direction.sqrMagnitude < 0.0001f) return;
            Quaternion look = Quaternion.LookRotation(direction);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, look, def.turnSpeed * dt);
        }

        void SetState(SoldierState next)
        {
            bool wasMoving = state == SoldierState.Hold || state == SoldierState.Chase;
            state = next;
            stateTime = 0f;
            if ((next == SoldierState.Hold || next == SoldierState.Chase) && !wasMoving) Play(LocomotionId);
        }

        void Play(int stateHash)
        {
            if (animator != null && animator.gameObject.activeInHierarchy) animator.CrossFadeInFixedTime(stateHash, CrossFade);
        }

        void ProbeGround(Vector3 position)
        {
            groundOffset = CrowdGround.OffsetAboveFloor(position, balance);
            groundY = CrowdGround.Floor(position) + groundOffset;
        }
    }
}
