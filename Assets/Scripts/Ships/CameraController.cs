using UnityEngine;

namespace Ahoy.Ships
{
    // Top-down follow camera for the sea: looks down on the target from a fixed angle and
    // trails it smoothly. It never turns with the ship, so WASD always matches the screen.
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private ShipCameraDef def;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            SnapToTarget();
        }

        private void Start()
        {
            SnapToTarget();
        }

        // LateUpdate: after the ship has moved this frame, so the camera never lags a frame behind.
        private void LateUpdate()
        {
            if (target == null || def == null)
                return;
            Quaternion rotation = Quaternion.Euler(def.pitch, def.yaw, 0f);
            Vector3 wanted = DesiredPosition(rotation);
            // Frame-rate independent smoothing: the same catch-up feel at 30 or 144 fps.
            float t = 1f - Mathf.Exp(-def.followSharpness * Time.deltaTime);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, wanted, t), rotation);
        }

        private void SnapToTarget()
        {
            if (target == null || def == null)
                return;
            Quaternion rotation = Quaternion.Euler(def.pitch, def.yaw, 0f);
            transform.SetPositionAndRotation(DesiredPosition(rotation), rotation);
        }

        private Vector3 DesiredPosition(Quaternion rotation)
        {
            Vector3 lookAt = target.position + Vector3.up * def.lookHeight;
            return lookAt - rotation * Vector3.forward * def.distance;
        }
    }
}
