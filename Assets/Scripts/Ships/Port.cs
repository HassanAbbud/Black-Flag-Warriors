using UnityEngine;

namespace Ahoy.Ships
{
    // A place where control changes hands (R39 dock transitions). Pure data: two spots.
    // berth is where the ship sits, bow pointing out to sea; landing is where Zahim stands.
    // PlayModeSwitcher reads these; the port itself does nothing.
    public sealed class Port : MonoBehaviour
    {
        [Tooltip("Where the ship moors. Its forward is the bow, so point it out to sea.")]
        [SerializeField] private Transform berth;
        [Tooltip("Where Zahim stands when he steps off, and the spot he must be near to board.")]
        [SerializeField] private Transform landing;

        public Transform Berth => berth;
        public Transform Landing => landing;

        private void Awake()
        {
            Debug.Assert(berth != null && landing != null, "Port needs a berth and a landing", this);
        }

        private void OnDrawGizmos()
        {
            if (berth != null)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawWireSphere(berth.position, 1f);
                Gizmos.DrawRay(berth.position, berth.forward * 4f);
            }
            if (landing != null)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(landing.position, 0.5f);
            }
        }
    }
}
