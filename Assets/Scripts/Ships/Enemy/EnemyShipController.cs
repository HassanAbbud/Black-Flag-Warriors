using Ahoy.Core;
using System;
using UnityEngine;

namespace Ahoy.Ships
{
    // Moves an enemy ship and decides what it is doing. Firing is EnemyShipCannons' job.
    // Wander: sail to a random point near its spawn. Idle: drift there a moment.
    // Engage: circle the player ship at engageDistance so its broadsides bear.
    // Bombard: the player is on land - stay put, turn a broadside on him and fire.
    // Dead: health hit 0 - cannons fall silent, it burns, then it is switched off (never destroyed).
    // Targets are found by tag (PlayerShip / Player), so respawned ones are picked up on their own.
    [DisallowMultipleComponent]
    public sealed class EnemyShipController : ShipController, IShipDamageable
    {
        // State: one enum, one switch, every transition through SetState.
        public enum ShipState { Wander, Idle, Engage, Bombard, Dead }

        private const string PlayerTag = "Player";
        private const string PlayerShipTag = "PlayerShip";
        // Below this squared length a heading is noise, and its direction flips frame to frame.
        // A numerical guard, not a tuning value.
        private const float MinSteer = 0.01f;

        [SerializeField] private EnemyShipDef def;
        [Tooltip("Told who to shoot at while engaging. Found on this ship automatically if left empty.")]
        [SerializeField] private EnemyShipCannons cannons;
        [Tooltip("Keeps it clear of other ships. Found on this ship automatically if left empty.")]
        [SerializeField] private EnemyShipAvoidance avoidance;
        [Tooltip("A vfx_Flames_01 instance placed as a child of this ship. Played when the ship is destroyed.")]
        [SerializeField] private ParticleSystem deathFlames;

        // Observer: local news, e.g. for a battle-music or HUD listener. Announces, never commands.
        public event Action<ShipState> StateChanged;

        public EnemyShipDef Def => def;
        public ShipState State => state;
        public Transform Target => target;

        private bool isAlive = true;
        public bool IsAlive => isAlive;

        private ShipState state;
        private Transform target;   // the player ship (Engage) or the player on land (Bombard)
        private readonly System.Collections.Generic.List<GameObject> tagged = new System.Collections.Generic.List<GameObject>(4);
        private Vector3 home;
        private Vector3 wanderPoint;
        private float stateUntil;
        private float speed;
        private float nextScanAt;
        private float lastSeenAt;
        private int maxHealth;
        private float orbitSign = 1f;   // +1 anticlockwise / -1 clockwise, seen from above
        private float engageRadius;     // this ship's own circling distance for the current fight

        // Own components are resolved once here, never per frame.
        private void Awake()
        {
            if (cannons == null)
                TryGetComponent(out cannons);
            if (avoidance == null)
                TryGetComponent(out avoidance);
            maxHealth = shipHealth;
        }

        // Pooled or respawned ships reset their own state here.
        private void OnEnable()
        {
            home = transform.position;
            speed = 0f;
            nextScanAt = 0f;
            target = null;
            shipHealth = maxHealth;
            isAlive = true;
            if (deathFlames != null)
                deathFlames.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            SetState(ShipState.Wander);
        }

        private void Update()
        {
            if (state == ShipState.Dead)
            {
                if (Time.time >= stateUntil)
                    gameObject.SetActive(false);   // back to the pool / spawner, never Destroy
                return;   // no scanning, steering or firing while it burns
            }

            if (Time.time >= nextScanAt)
            {
                nextScanAt = Time.time + def.scanInterval;
                Scan();
            }

            switch (state)
            {
                case ShipState.Wander:
                    Steer(wanderPoint - transform.position, def.wanderSpeed);
                    if (FlatDistance(wanderPoint) <= def.arriveDistance)
                        SetState(ShipState.Idle);
                    else if (Time.time >= stateUntil)
                        SetState(ShipState.Wander);   // couldn't get there - pick another point
                    break;

                case ShipState.Idle:
                    Steer(transform.forward, 0f);
                    if (Time.time >= stateUntil)
                        SetState(ShipState.Wander);
                    break;

                case ShipState.Engage:
                case ShipState.Bombard:
                    if (target == null || !target.gameObject.activeInHierarchy)
                    {
                        target = null;
                        SetState(ShipState.Wander);   // destroyed or switched off mid-fight
                        break;
                    }
                    if (state == ShipState.Engage)
                        Steer(EngageHeading(), def.engageSpeed);
                    else
                        Steer(BroadsideHeading(), 0f);   // turn in place, never sail toward land
                    break;
            }
        }

        private void SetState(ShipState next)
        {
            if (next == ShipState.Engage && state != ShipState.Engage)
            {
                orbitSign = def.circleClockwise ? -1f : 1f;
                // Floored at arriveDistance so a large spread can never make the radius 0 or negative.
                engageRadius = Mathf.Max(def.arriveDistance,
                    def.engageDistance + UnityEngine.Random.Range(-def.engageDistanceSpread, def.engageDistanceSpread));
            }
            state = next;
            switch (next)
            {
                case ShipState.Wander:
                    Vector2 offset = UnityEngine.Random.insideUnitCircle * def.wanderRadius;
                    wanderPoint = home + new Vector3(offset.x, 0f, offset.y);
                    stateUntil = Time.time + def.wanderTimeout;
                    break;
                case ShipState.Idle:
                    stateUntil = Time.time + UnityEngine.Random.Range(def.idleSeconds.x, def.idleSeconds.y);
                    break;
                case ShipState.Engage:
                case ShipState.Bombard:
                    lastSeenAt = Time.time;
                    break;
                case ShipState.Dead:
                    target = null;
                    stateUntil = Time.time + def.burnSeconds;
                    if (deathFlames != null)
                        deathFlames.Play(true);
                    break;
            }
            if (cannons != null)
                cannons.SetTarget(next == ShipState.Engage || next == ShipState.Bombard ? target : null);
            StateChanged?.Invoke(next);
        }

        private void Scan()
        {
            bool engaged = state == ShipState.Engage || state == ShipState.Bombard;
            Transform seen = FindTarget(engaged ? def.loseRange : def.detectRange, engaged, out ShipState mode);

            if (seen != null)
            {
                lastSeenAt = Time.time;
                if (seen != target || state != mode)
                {
                    target = seen;
                    SetState(mode);
                }
            }
            else if (engaged && Time.time - lastSeenAt > def.loseSightSeconds)
            {
                target = null;
                SetState(ShipState.Wander);   // heads back to a point near home
            }
        }

        // Nearest object tagged PlayerShip or Player within range and in sight. Looked up by tag
        // rather than a physics overlap, so scenery colliders (an island's rocks and trees) can't
        // crowd the player out of the results.
        private Transform FindTarget(float range, bool engaged, out ShipState mode)
        {
            mode = ShipState.Wander;
            Transform best = null;
            float bestDistance = range;
            ConsiderTagged(PlayerShipTag, ref best, ref bestDistance, engaged);
            ConsiderTagged(PlayerTag, ref best, ref bestDistance, engaged);
            if (best != null)
                mode = best.CompareTag(PlayerShipTag) ? ShipState.Engage : ShipState.Bombard;
            return best;
        }

        private void ConsiderTagged(string tag, ref Transform best, ref float bestDistance, bool engaged)
        {
            GameObject.FindGameObjectsWithTag(tag, tagged);   // fills the reused list, no allocation
            for (int i = 0; i < tagged.Count; i++)
            {
                Transform t = tagged[i].transform;
                float distance = FlatDistance(t.position);
                if (distance > bestDistance || !InSight(t, engaged))
                    continue;
                best = t;
                bestDistance = distance;
            }
        }

        private bool InSight(Transform t, bool engaged)
        {
            Vector3 toTarget = t.position - transform.position;
            toTarget.y = 0f;
            if (!engaged && def.viewAngle < 360f
                && Vector3.Angle(transform.forward, toTarget) > def.viewAngle * 0.5f)
                return false;

            if (def.sightBlockers != 0)
            {
                Vector3 lookout = transform.position + Vector3.up * def.lookoutHeight;
                if (Physics.Linecast(lookout, t.position, def.sightBlockers, QueryTriggerInteraction.Ignore))
                    return false;
            }
            return true;
        }

        // Circle the player: sail along the tangent (keeps a broadside on it), plus a pull in or
        // out to hold engageDistance.
        private Vector3 EngageHeading()
        {
            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance < 0.01f)
                return transform.forward;
            Vector3 radial = toPlayer / distance;
            // Every ship circles the same way: ships going opposite ways round one ring met head-on,
            // and avoidance plus the overlap push made them jiggle and reverse.
            Vector3 tangent = Vector3.Cross(Vector3.up, radial) * orbitSign;
            // Closes in hard but backs off only gently, so a player who chases it gets a broadside, not a stern.
            float pull = Mathf.Clamp((distance - engageRadius) / engageRadius, -def.engageBackOff, 1f);
            return tangent + radial * pull;
        }

        // Heading that puts the nearer side's cannons square on the target, without the pull.
        private Vector3 BroadsideHeading()
        {
            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            if (toTarget.sqrMagnitude < 0.0001f)
                return transform.forward;
            Vector3 tangent = Vector3.Cross(Vector3.up, toTarget.normalized);
            return Vector3.Dot(tangent, transform.forward) >= 0f ? tangent : -tangent;
        }

        private void Steer(Vector3 direction, float targetSpeed)
        {
            float dt = Time.deltaTime;
            direction.y = 0f;
            if (avoidance != null)
            {
                if (direction.sqrMagnitude > 0.0001f)
                    direction.Normalize();   // so the avoidance weighs the same at any distance
                direction += avoidance.SteerOffset;
                direction.y = 0f;
            }
            if (direction.sqrMagnitude < MinSteer)
                direction = transform.forward;   // wants and avoidance cancel out: hold course rather than flip
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion wanted = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, def.turnSpeed * dt);
                float turning = Quaternion.Angle(transform.rotation, wanted) / 180f;   // 0..1
                targetSpeed *= Mathf.Lerp(1f, def.turnSlowdown, turning);
            }
            speed = Mathf.MoveTowards(speed, targetSpeed, def.acceleration * dt);
            transform.position += transform.forward * (speed * dt);
        }

        private float FlatDistance(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            return d.magnitude;
        }

        private void OnDrawGizmosSelected()
        {
            if (def == null)
                return;
            Vector3 centre = Application.isPlaying ? home : transform.position;
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(centre, def.wanderRadius);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(transform.position, def.detectRange);
            if (Application.isPlaying && state == ShipState.Wander)
                Gizmos.DrawLine(transform.position, wanderPoint);
            if (Application.isPlaying && state == ShipState.Engage && target != null)
            {
                Gizmos.color = Color.yellow;   // the ring this ship is trying to hold round the player
                Gizmos.DrawWireSphere(target.position, engageRadius);
            }
        }

        public void TakeDamage(int damage)
        {
            if (isAlive)
            {
                shipHealth -= Mathf.Max(damage - shipDefense, 1);   // a weak hit still scratches, never heals
                Debug.Log("Enemy ship took damage: " + damage + ", remaining health: " + shipHealth);
                if (shipHealth <= 0)
                {
                    isAlive = false;
                    SetState(ShipState.Dead);   // also clears the cannons' target, so it stops firing
                }
            }
        }
    }
}
