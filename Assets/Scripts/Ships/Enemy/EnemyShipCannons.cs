using UnityEngine;

namespace Ahoy.Ships
{
    // Fires broadsides at whatever EnemyShipController says to target. Tuning comes from the
    // controller's EnemyShipDef so a ship has exactly one data asset to assign.
    [RequireComponent(typeof(EnemyShipController))]
    public sealed class EnemyShipCannons : MonoBehaviour
    {
        [Tooltip("Muzzle points along the left side. Only their positions are used.")]
        [SerializeField] private Transform[] portMuzzles;
        [Tooltip("Muzzle points along the right side. Only their positions are used.")]
        [SerializeField] private Transform[] starboardMuzzles;
        [Tooltip("The scene's CannonballPool. Wire it in the scene, or have the spawner call SetPool.")]
        [SerializeField] private CannonballPool pool;

        private EnemyShipDef def;
        private Transform target;
        private Vector3 lastTargetPosition;
        private Vector3 targetVelocity;   // estimated - the player ship has no Rigidbody to ask
        private float portReadyAt;
        private float starboardReadyAt;
        private Transform[] firingSide;   // non-null while a broadside is going off
        private int nextMuzzle;
        private float nextShotAt;

        private void Awake()
        {
            def = GetComponent<EnemyShipController>().Def;
        }

        public void SetPool(CannonballPool cannonballPool) => pool = cannonballPool;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            firingSide = null;
            targetVelocity = Vector3.zero;
            if (target != null)
                lastTargetPosition = target.position;
        }

        private void Update()
        {
            if (target == null || pool == null)
                return;

            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                targetVelocity = (target.position - lastTargetPosition) / dt;
                lastTargetPosition = target.position;
            }

            if (firingSide != null)
            {
                ContinueBroadside();
                return;
            }

            Vector3 toTarget = target.position - transform.position;
            toTarget.y = 0f;
            bool starboard = Vector3.Dot(toTarget, transform.right) >= 0f;
            Vector3 beam = starboard ? transform.right : -transform.right;
            if (Vector3.Angle(beam, toTarget) > def.broadsideArc)
                return;   // target is off the bow or stern - the guns don't bear

            Transform[] muzzles = starboard ? starboardMuzzles : portMuzzles;
            float readyAt = starboard ? starboardReadyAt : portReadyAt;
            if (muzzles == null || muzzles.Length == 0 || Time.time < readyAt)
                return;

            if (starboard)
                starboardReadyAt = Time.time + def.fireCooldown;
            else
                portReadyAt = Time.time + def.fireCooldown;
            firingSide = muzzles;
            nextMuzzle = 0;
            nextShotAt = Time.time;
            ContinueBroadside();
        }

        // The cannons of one side go off one after another, cannonStagger apart.
        private void ContinueBroadside()
        {
            while (firingSide != null && Time.time >= nextShotAt)
            {
                Transform muzzle = firingSide[nextMuzzle];
                if (muzzle != null)
                    FireFrom(muzzle.position);
                nextMuzzle++;
                nextShotAt += def.cannonStagger;
                if (nextMuzzle >= firingSide.Length)
                    firingSide = null;
            }
        }

        private void FireFrom(Vector3 muzzle)
        {
            float speed = def.muzzleSpeed;
            float gravity = pool.Gravity;

            // Aim where the target will be: solve once for the flight time, lead, solve again.
            if (!TrySolveArc(muzzle, target.position, speed, gravity, out Vector3 velocity, out float flightTime))
                return;   // out of range
            Vector3 lead = target.position + targetVelocity * flightTime;
            if (!TrySolveArc(muzzle, lead, speed, gravity, out velocity, out _))
                return;

            float spread = def.aimSpread;
            velocity = Quaternion.Euler(Random.Range(-spread, spread), Random.Range(-spread, spread), 0f) * velocity;
            pool.Launch(muzzle, velocity, def.cannonDamage, def.hitReaction, transform);
        }

        // Low ballistic arc from `from` to `to` at a fixed launch speed.
        private static bool TrySolveArc(Vector3 from, Vector3 to, float speed, float gravity,
                                        out Vector3 velocity, out float flightTime)
        {
            velocity = Vector3.zero;
            flightTime = 0f;
            Vector3 delta = to - from;
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            float x = flat.magnitude;
            if (x < 0.01f || gravity <= 0f)
                return false;

            float v2 = speed * speed;
            float discriminant = v2 * v2 - gravity * (gravity * x * x + 2f * delta.y * v2);
            if (discriminant < 0f)
                return false;

            float angle = Mathf.Atan((v2 - Mathf.Sqrt(discriminant)) / (gravity * x));
            float horizontalSpeed = speed * Mathf.Cos(angle);
            velocity = flat / x * horizontalSpeed + Vector3.up * (speed * Mathf.Sin(angle));
            flightTime = x / horizontalSpeed;
            return true;
        }
    }
}
