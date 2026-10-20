using UnityEngine;

namespace Ahoy.Ships
{
    // Type Object: everything about enemy cannonballs that a designer might tune.
    [CreateAssetMenu(menuName = "Ahoy/Ships/Cannonball", fileName = "PRJ_Cannonball")]
    public sealed class CannonballDef : ScriptableObject
    {
        [Tooltip("Cannonballs created at load. When all are in flight a shot is skipped - never Instantiated mid-battle.")]
        public int poolSize = 64;
        [Tooltip("Collision radius, metres.")]
        public float radius = 0.4f;
        [Tooltip("Downward acceleration, metres per second squared (positive).")]
        public float gravity = 9.81f;
        [Tooltip("Seconds before a ball that hit nothing goes back to the pool.")]
        public float lifetime = 6f;
        [Tooltip("Metres below the firing ship's waterline at which a ball counts as splashed into the sea.")]
        public float splashDepth = 1f;
        [Tooltip("Layers a cannonball can hit.")]
        public LayerMask hitLayers = ~0;
    }
}
