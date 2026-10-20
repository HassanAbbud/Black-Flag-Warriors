using Ahoy.Core;
using System;
using UnityEngine;

namespace Ahoy.Ships
{
    // Moves an enemy ship and decides what it is doing. Firing is EnemyShipCannons' job.
    // Wander: sail to a random point near its spawn. Idle: drift there a moment.
    // Engage: circle the player ship at engageDistance so its broadsides bear.
    [DisallowMultipleComponent]
    public sealed class EnemyShipController : ShipController, IShipDamageable
    {
        // State: one enum, one switch, every transition through SetState.
        public enum ShipState { Wander, Idle, Engage }

        [SerializeField] private EnemyShipDef def;
        [Tooltip("The player's ship. Wire it in the scene, or have the spawner call SetTarget (no Find).")]
        [SerializeField] private Transform playerShip;
        [Tooltip("Told who to shoot at while engaging. Found on this ship automatically if left empty.")]
        [SerializeField] private EnemyShipCannons cannons;
        [Tooltip("Keeps it clear of other ships. Found on this ship automatically if left empty.")]
        [SerializeField] private EnemyShipAvoidance avoidance;

        // Observer: local news, e.g. for a battle-music or HUD listener. Announces, never commands.
        public event Action<ShipState> StateChanged;

        public EnemyShipDef Def => def;
        public ShipState State => state;
        public Transform PlayerShip => playerShip;

        private bool isAlive = true;
        public bool IsAlive => isAlive;

        private ShipState state;
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
            SetState(ShipState.Wander);
        }

        public void SetTarget(Transform target)
        {
            playerShip = target;
            if (state == ShipState.Engage && cannons != null)
                cannons.SetTarget(target);
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
                    Steer(EngageHeading(), def.engageSpeed);
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
                    lastSeenAt = Time.time;
                    break;
            }
            if (cannons != null)
                cannons.SetTarget(next == ShipState.Engage ? playerShip : null);
            StateChanged?.Invoke(next);
        }

        private void Scan()
        {
            if (CanSeePlayer())
            {
                lastSeenAt = Time.time;
                if (state != ShipState.Engage)
                    SetState(ShipState.Engage);
            }
            else if (state == ShipState.Engage && Time.time - lastSeenAt > def.loseSightSeconds)
            {
                SetState(ShipState.Wander);   // heads back to a point near home
            }
        }

        private bool CanSeePlayer()
        {
            if (playerShip == null || !playerShip.gameObject.activeInHierarchy)
                return false;

            Vector3 toPlayer = playerShip.position - transform.position;
            float range = state == ShipState.Engage ? def.loseRange : def.detectRange;
            if (toPlayer.sqrMagnitude > range * range)
                return false;

            toPlayer.y = 0f;
            if (state != ShipState.Engage && def.viewAngle < 360f
                && Vector3.Angle(transform.forward, toPlayer) > def.viewAngle * 0.5f)
                return false;

            if (def.sightBlockers != 0)
            {
                Vector3 lookout = transform.position + Vector3.up * def.lookoutHeight;
                if (Physics.Linecast(lookout, playerShip.position, def.sightBlockers, QueryTriggerInteraction.Ignore))
                    return false;
            }
            return true;
        }

        // Circle the player: sail along the tangent (keeps a broadside on it), plus a pull in or
        // out to hold engageDistance.
        private Vector3 EngageHeading()
        {
            Vector3 toPlayer = playerShip.position - transform.position;
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

        public void TakeDamage(int damage)
        {
            if (isAlive)
            {
                shipHealth -= (damage - shipDefense);
                Debug.Log("Enemy ship took damage: " + damage + ", remaining health: " + shipHealth);
                if (shipHealth <= 0)
                {
                    isAlive = false;
                    // Handle ship destruction logic here
                    Debug.Log("Enemy ship destroyed!");
                }
            }
        }
    }
}
