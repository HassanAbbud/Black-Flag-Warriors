using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Ships
{
    // Type Object: one EnemyShipController, many kinds of enemy ship. A new kind is a new
    // MON_ asset, not a new class. Every ship of a kind shares the asset (Flyweight).
    [CreateAssetMenu(menuName = "Ahoy/Ships/Enemy Ship", fileName = "MON_EnemyShip")]
    public sealed class EnemyShipDef : ScriptableObject
    {
        [Header("Wander")]
        [Tooltip("Metres from its spawn point that it picks wander points within.")]
        public float wanderRadius = 25f;
        [Tooltip("Cruising speed while wandering, metres per second.")]
        public float wanderSpeed = 4f;
        [Tooltip("Metres from a wander point that counts as arrived.")]
        public float arriveDistance = 4f;
        [Tooltip("Seconds it idles at a wander point before picking the next one (min, max).")]
        public Vector2 idleSeconds = new Vector2(1f, 4f);
        [Tooltip("Seconds before it gives up on a wander point it can't reach and picks another.")]
        public float wanderTimeout = 20f;

        [Header("Steering")]
        [Tooltip("How fast it turns, degrees per second.")]
        public float turnSpeed = 35f;
        [Tooltip("Speed multiplier (0..1) while turning hard - ships slow down in a tight turn.")]
        public float turnSlowdown = 0.5f;
        [Tooltip("How fast it speeds up or slows down, metres per second squared.")]
        public float acceleration = 2f;

        [Header("Spotting the player")]
        [Tooltip("Layers the player and the player ship are on. The scan only looks at these.")]
        public LayerMask targetLayers = ~0;
        [Tooltip("Metres at which it spots the player or the player ship.")]
        public float detectRange = 60f;
        [Tooltip("Width of its view, degrees. 360 sees all round.")]
        public float viewAngle = 360f;
        [Tooltip("Metres at which it loses the player or the player ship again. Keep above detectRange.")]
        public float loseRange = 80f;
        [Tooltip("Seconds out of range or sight before it gives up and goes back to wandering.")]
        public float loseSightSeconds = 3f;
        [Tooltip("Seconds between spotting checks.")]
        public float scanInterval = 0.2f;
        [Tooltip("Height of its lookout above the ship's pivot, metres (line-of-sight checks).")]
        public float lookoutHeight = 6f;
        [Tooltip("Layers that block its line of sight, e.g. islands. Nothing = no line-of-sight check.")]
        public LayerMask sightBlockers;

        [Header("Engaging")]
        [Tooltip("Speed while engaging, metres per second.")]
        public float engageSpeed = 6f;
        [Tooltip("Metres it tries to keep from the player ship while it circles and fires broadsides.")]
        public float engageDistance = 30f;

        [Header("Keeping clear of other ships")]
        [Tooltip("Metres of clearance at which it starts steering away from another ship.")]
        public float avoidDistance = 15f;
        [Tooltip("How hard it steers away, compared with where it wants to go (1 = equally).")]
        public float avoidStrength = 2f;
        [Tooltip("Layers ship colliders are on. Only enemy ships and the player ship are avoided.")]
        public LayerMask shipLayers = ~0;

        [Header("Cannons")]
        [Tooltip("Seconds between broadsides from the same side.")]
        public float fireCooldown = 3f;
        [Tooltip("Seconds between each cannon of one broadside going off.")]
        public float cannonStagger = 0.15f;
        [Tooltip("Degrees off the beam (straight out sideways) that a side's cannons can still fire.")]
        public float broadsideArc = 45f;
        [Tooltip("Cannonball launch speed, metres per second. Max range is about speed squared / gravity.")]
        public float muzzleSpeed = 35f;
        [Tooltip("Random aim error per cannon, degrees.")]
        public float aimSpread = 2f;
        [Tooltip("Metres above the target's pivot the guns aim at. Pivots sit at the feet or waterline, so aim at the body instead of the ground.")]
        public float aimHeight = 1f;
        [Tooltip("How far ahead of a moving target it aims (0 = straight at it, 1 = full prediction).")]
        [Range(0f, 1f)] public float leadFraction = 0.5f;
        [Tooltip("Damage per cannonball hit.")]
        public float cannonDamage = 50f;
        [Tooltip("How a hit target reacts.")]
        public HitReaction hitReaction = HitReaction.Knockback;
    }
}
