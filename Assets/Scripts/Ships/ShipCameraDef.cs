using UnityEngine;

namespace Ahoy.Ships
{
    // Type Object: how the sea camera looks down on the ship it follows.
    [CreateAssetMenu(menuName = "Ahoy/Ships/Ship Camera", fileName = "CAM_ShipTopDown")]
    public sealed class ShipCameraDef : ScriptableObject
    {
        [Tooltip("Degrees the camera looks down. 90 = straight down; lower = more slanted, so you see further ahead.")]
        [Range(10f, 90f)] public float pitch = 45f;
        [Tooltip("Heading offset, degrees. With Follow Ship Heading off this is the fixed world heading; with it on, it is added to the ship's heading.")]
        public float yaw = 0f;
        [Tooltip("Metres from the ship to the camera.")]
        public float distance = 45f;
        [Tooltip("Metres above the ship's pivot the camera looks at.")]
        public float lookHeight = 2f;
        [Tooltip("Metres in front of the ship the camera aims at, so more of the sea ahead is on screen.")]
        public float lookAhead = 10f;
        [Tooltip("Degrees the camera tilts up from the ship after it is placed. Same camera position, but the view aims higher, so more sea ahead is on screen. 0 = centred on the look point.")]
        [Range(0f, 45f)] public float aimUpAngle = 0f;
        [Tooltip("How quickly the camera catches up with the ship. Higher = tighter.")]
        public float followSharpness = 6f;

        [Header("Heading")]
        [Tooltip("Swing the camera round behind the ship when it turns left or right.")]
        public bool followShipHeading = true;
        [Tooltip("How quickly the camera swings round to the ship's heading. Higher = tighter.")]
        public float yawSharpness = 3f;
    }
}
