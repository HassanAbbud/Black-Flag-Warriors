using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Ships
{
    // Object Pool + Update Manager: every enemy cannonball lives here. They are created
    // once at load and deactivated, never destroyed, and this single Update flies all
    // of them instead of one Update per ball. The balls have no collider or Rigidbody: the pool
    // queries ahead along each ball's path (the attacker queries, the victim does not collide).
    public sealed class CannonballPool : MonoBehaviour
    {
        [SerializeField] private CannonballDef def;
        [Tooltip("Visual only - the pool moves it and does the hit checks. Must not carry ShipBullet.")]
        [SerializeField] private GameObject cannonballPrefab;

        private struct Ball
        {
            public GameObject gameObject;
            public Transform transform;
            public bool live;
            public Vector3 position;
            public Vector3 velocity;
            public float age;
            public float splashHeight;
            public float damage;
            public HitReaction reaction;
            public Transform shooter;
        }

        private Ball[] balls;
        private readonly RaycastHit[] hits = new RaycastHit[8];   // preallocated: NonAlloc queries only

        public float Gravity => def.gravity;

        private void Awake()
        {
            balls = new Ball[def.poolSize];
            for (int i = 0; i < balls.Length; i++)
            {
                GameObject go = Instantiate(cannonballPrefab, transform);   // load time only
                go.SetActive(false);
                balls[i].gameObject = go;
                balls[i].transform = go.transform;
            }
        }

        // Returns false when every ball is already in flight: the shot is skipped, not Instantiated.
        public bool Launch(Vector3 position, Vector3 velocity, float damage, HitReaction reaction, Transform shooter)
        {
            for (int i = 0; i < balls.Length; i++)
            {
                ref Ball b = ref balls[i];
                if (b.live)
                    continue;
                b.live = true;
                b.position = position;
                b.velocity = velocity;
                b.age = 0f;
                b.splashHeight = shooter.position.y - def.splashDepth;
                b.damage = damage;
                b.reaction = reaction;
                b.shooter = shooter;
                b.transform.SetPositionAndRotation(position, Quaternion.identity);
                b.gameObject.SetActive(true);
                return true;
            }
            return false;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            for (int i = 0; i < balls.Length; i++)
            {
                ref Ball b = ref balls[i];
                if (!b.live)
                    continue;

                b.age += dt;
                // Exact projectile step (v·t − ½g·t²), so the ball flies the arc TrySolveArc aimed,
                // at any frame rate.
                Vector3 step = b.velocity * dt + Vector3.down * (0.5f * def.gravity * dt * dt);
                b.velocity.y -= def.gravity * dt;
                float distance = step.magnitude;

                if ((distance > 0f && HitSomething(ref b, step / distance, distance))
                    || b.age >= def.lifetime || b.position.y + step.y < b.splashHeight)
                {
                    Return(ref b);
                    continue;
                }

                b.position += step;
                b.transform.position = b.position;
            }
        }

        private bool HitSomething(ref Ball b, Vector3 direction, float distance)
        {
            int count = Physics.SphereCastNonAlloc(b.position, def.radius, direction, hits, distance,
                                                   def.hitLayers, QueryTriggerInteraction.Ignore);
            int nearest = -1;
            for (int i = 0; i < count; i++)
            {
                if (b.shooter != null && hits[i].collider.transform.IsChildOf(b.shooter))
                    continue;   // never hit the ship that fired it
                if (nearest < 0 || hits[i].distance < hits[nearest].distance)
                    nearest = i;
            }
            if (nearest < 0)
                return false;

            // Damage only ever goes through IDamageable, and enemies don't hurt each other.
            IDamageable target = hits[nearest].collider.GetComponentInParent<IDamageable>();
            if (target != null && target.IsAlive && target.Faction != Faction.Monsters)
                target.TakeDamage(new DamageInfo(b.damage, b.position, b.reaction, 0, 0f, null));
            return true;
        }

        private static void Return(ref Ball b)
        {
            b.live = false;
            b.shooter = null;
            b.gameObject.SetActive(false);
        }
    }
}
