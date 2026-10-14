using UnityEngine;

namespace Ahoy.Ships
{
    // Keeps enemy ships from sailing through each other and through the player ship.
    // Ships are moved by transform, not by physics, so colliders alone would just overlap:
    // this steers away from nearby ships early, and pushes apart any that still touch.
    // Only enemy ships and the player ship count - never the sea or islands.
    [RequireComponent(typeof(EnemyShipController))]
    public sealed class EnemyShipAvoidance : MonoBehaviour
    {
        [Tooltip("The ship's hull collider. Leave empty to have a box fitted around the ship's model at load.")]
        [SerializeField] private Collider hull;

        private const int MaxNeighbours = 16;   // query buffer capacity, not a tuning value
        private readonly Collider[] neighbours = new Collider[MaxNeighbours];

        private EnemyShipController controller;
        private EnemyShipDef def;
        private float reach;   // how far the hull sticks out from the pivot, metres

        // Added to the controller's wanted heading each frame.
        public Vector3 SteerOffset { get; private set; }

        private void Awake()
        {
            controller = GetComponent<EnemyShipController>();
            def = controller.Def;
            if (hull == null)
                hull = FitHull();
            Vector3 extents = hull.bounds.extents;
            reach = Mathf.Max(extents.x, extents.z);
        }

        private void Update()
        {
            Vector3 position = transform.position;
            int count = Physics.OverlapSphereNonAlloc(position, reach + def.avoidDistance, neighbours,
                                                      def.shipLayers, QueryTriggerInteraction.Ignore);
            Vector3 offset = Vector3.zero;
            for (int i = 0; i < count; i++)
            {
                Collider other = neighbours[i];
                if (other.transform.IsChildOf(transform))
                    continue;   // our own hull or parts
                bool enemy = other.GetComponentInParent<EnemyShipController>() != null;
                if (!enemy && !IsPlayerShip(other))
                    continue;

                Vector3 away = position - other.ClosestPointOnBounds(position);
                away.y = 0f;
                if (away.sqrMagnitude < 0.0001f)
                {
                    away = position - other.bounds.center;   // our pivot is inside its bounds
                    away.y = 0f;
                }
                float gap = away.magnitude - reach;          // roughly hull to hull
                offset += away.normalized * Mathf.Clamp01(1f - gap / def.avoidDistance);

                PushApart(other, enemy);
            }
            SteerOffset = offset * def.avoidStrength;
        }

        private bool IsPlayerShip(Collider other)
        {
            Transform player = controller.PlayerShip;
            return player != null && other.transform.IsChildOf(player);
        }

        private void PushApart(Collider other, bool otherIsEnemy)
        {
            Transform hullTransform = hull.transform;
            Transform otherTransform = other.transform;
            if (!Physics.ComputePenetration(hull, hullTransform.position, hullTransform.rotation,
                                            other, otherTransform.position, otherTransform.rotation,
                                            out Vector3 direction, out float depth))
                return;
            direction.y = 0f;
            // Two enemy ships each move themselves half the way; the player ship doesn't move for us.
            float share = otherIsEnemy ? 0.5f : 1f;
            transform.position += direction * (depth * share);
        }

        // Load time only: a box around every renderer of the ship, in the ship's own axes.
        private Collider FitHull()
        {
            BoxCollider box = gameObject.AddComponent<BoxCollider>();
            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return box;

            Quaternion rotation = transform.rotation;
            transform.rotation = Quaternion.identity;   // so world bounds line up with the ship's axes
            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                bounds.Encapsulate(renderers[i].bounds);
            Vector3 scale = transform.lossyScale;
            box.center = transform.InverseTransformPoint(bounds.center);
            box.size = new Vector3(bounds.size.x / scale.x, bounds.size.y / scale.y, bounds.size.z / scale.z);
            transform.rotation = rotation;
            return box;
        }
    }
}
