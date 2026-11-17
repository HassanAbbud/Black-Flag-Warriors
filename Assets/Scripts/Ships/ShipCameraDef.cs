using UnityEngine;

namespace Ahoy.Ships
{
    // Type Object: how the sea camera looks down on the ship it follows.
    [CreateAssetMenu(menuName = "Ahoy/Ships/Ship Camera", fileName = "CAM_ShipTopDown")]
    public sealed class ShipCameraDef : ScriptableObject
    {
        [Tooltip("Degrees the camera looks down. 90 = straight down.")]
        [Range(10f, 90f)] public float pitch = 55f;
        [Tooltip("World heading the camera faces, degrees. It never turns with the ship, so W is always 'up the screen'.")]
        public float yaw = 0f;
        [Tooltip("Metres from the ship to the camera.")]
        public float distance = 45f;
        [Tooltip("Metres above the ship's pivot the camera looks at.")]
        public float lookHeight = 2f;
        [Tooltip("How quickly the camera catches up with the ship. Higher = tighter.")]
        public float followSharpness = 6f;
    }
}
