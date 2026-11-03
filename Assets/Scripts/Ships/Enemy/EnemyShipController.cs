using System;
using UnityEngine;

namespace Ahoy.Ships
{
    // Moves an enemy ship and decides what it is doing. Firing is EnemyShipCannons' job.
    // Wander: sail to a random point near its spawn. Idle: drift there a moment.
    // Engage: circle the player ship at engageDistance so its broadsides bear.
    // Bombard: the player is on land - stay put, turn a broadside on him and fire.
    // Targets are found by tag (PlayerShip / Player), so respawned ones are picked up on their own.
    [DisallowMultipleComponent]
    public sealed class EnemyShipController : MonoBehaviour
    {
        // State: one enum, one switch, every transition through SetState.
        public enum ShipState { Wander, Idle, Engage, Bombard }

        private const string PlayerTag = "Player";
        private const string PlayerShipTag = "PlayerShip";

        [SerializeField] private EnemyShipDef def;
        [Tooltip("Told who to shoot at while engaging. Found on this ship automatically if left empty.")]
        [SerializeField] private EnemyShipCannons cannons;
        [Tooltip("Keeps it clear of other ships. Found on this ship automatically if left empty.")]
        [SerializeField] private EnemyShipAvoidance avoidance;

        // Observer: local news, e.g. for a battle-music or HUD listener. Announces, never commands.
        public event Action<ShipState> StateChanged;

        public EnemyShipDef Def => def;
        public ShipState State => state;
        public Transform Target => target;

        private ShipState state;
        private Transform target;   // the player ship (Engage) or the player on land (Bombard)
        private readonly Collider[] nearby = new Collider[16];   // preallocated: NonAlloc queries only
        private Vector3 home;
        private Vector3 wanderPoint;
        private float stateUntil;
        private float speed;
        private float nextScanAt;
        private float lastSeenAt;

        // Own components are resolved once here, never per frame.
        private void Awake()
        {
            if (cannons == null)
                TryGetComponent(out cannons);
            if (avoidance == null)
                TryGetComponent(out avoidance);
        }

        // Pooled or respawned ships reset their own state here.
        private void OnEnable()
        {
            home = transform.position;
            speed = 0f;
            nextScanAt = 0f;
            target = null;
            SetState(ShipState.Wander);
        }

        private void Update()
        {
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

        // First object tagged PlayerShip or Player within range and in sight.
        private Transform FindTarget(float range, bool engaged, out ShipState mode)
        {
            mode = ShipState.Wander;
            int count = Physics.OverlapSphereNonAlloc(transform.position, range, nearby,
                                                      def.targetLayers, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Transform found = TaggedRoot(nearby[i].transform);
                if (found == null || !InSight(found, engaged))
                    continue;
                mode = found.CompareTag(PlayerShipTag) ? ShipState.Engage : ShipState.Bombard;
                return found;
            }
            return null;
        }

        // Walks up from the collider so a tag on the root still counts when the collider is on a child.
        private static Transform TaggedRoot(Transform t)
        {
            for (; t != null; t = t.parent)
                if (t.CompareTag(PlayerShipTag) || t.CompareTag(PlayerTag))
                    return t;
            return null;
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
            Vector3 tangent = Vector3.Cross(Vector3.up, radial);
            if (Vector3.Dot(tangent, transform.forward) < 0f)
                tangent = -tangent;   // keep circling the way it is already facing
            float pull = Mathf.Clamp((distance - def.engageDistance) / def.engageDistance, -1f, 1f);
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
        }
    }
}
