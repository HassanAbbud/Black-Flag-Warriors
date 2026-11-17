using UnityEngine;

namespace Ahoy.Ships
{
    // Type Object: everything about the player ship that a designer might tune.
    // Shared by the ship, its cannon and its aim arrow so there is exactly one asset to assign.
    [CreateAssetMenu(menuName = "Ahoy/Ships/Player Ship", fileName = "ALLY_PlayerShip")]
    public sealed class PlayerShipDef : ScriptableObject
    {
        [Header("Sailing")]
        [Tooltip("Top speed, metres per second.")]
        public float maxSpeed = 10f;
        [Tooltip("How fast it speeds up, metres per second squared.")]
        public float acceleration = 4f;
        [Tooltip("How fast it slows down with no input, metres per second squared.")]
        public float deceleration = 3f;
        [Tooltip("How fast it turns toward the WASD direction, degrees per second.")]
        public float turnSpeed = 90f;
        [Tooltip("Speed multiplier (0..1) while turning hard - ships slow down in a tight turn.")]
        [Range(0f, 1f)] public float turnSlowdown = 0.4f;
        [Tooltip("Input below this length counts as no input (stick drift).")]
        public float inputDeadZone = 0.1f;

        [Header("Cannon")]
        [Tooltip("How fast the cannon swings toward the mouse, degrees per second.")]
        public float cannonTurnSpeed = 240f;
        [Tooltip("Degrees left or right of the bow the cannon can swing. 180 = all the way round.")]
        [Range(0f, 180f)] public float cannonYawLimit = 180f;
        [Tooltip("Degrees the barrel is raised above flat. Higher lobs the ball further.")]
        public float cannonElevation = 10f;
        [Tooltip("Seconds between shots.")]
        public float fireCooldown = 0.6f;
        [Tooltip("Seconds a fire press is remembered while the cannon reloads.")]
        public float fireBuffer = 0.2f;

        [Header("Aim arrow")]
        [Tooltip("Metres from the ship's centre to the arrow.")]
        public float arrowDistance = 8f;
        [Tooltip("Metres above the ship's pivot the arrow floats, so it clears the deck and waves.")]
        public float arrowHeight = 1.5f;
    }
}
