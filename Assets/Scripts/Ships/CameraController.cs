using UnityEngine;

namespace Ahoy.Ships
{
    // Slanted follow camera for the sea: looks down on the target at def.pitch and trails it
    // smoothly. With def.followShipHeading it swings round behind the ship as it turns, so the
    // view always looks ahead of the bow; otherwise it holds a fixed world heading.
    public sealed class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private ShipCameraDef def;

        private float currentYaw;
        // Whose heading the camera swings round to: the ship root that WASD steers, never a child
        // (cannon, aim arrow) that turns with the mouse.
        private Transform headingSource;

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
            ResolveHeadingSource();
            SnapToTarget();
        }

        private void Start()
        {
            ResolveHeadingSource();
            SnapToTarget();
        }

        private void ResolveHeadingSource()
        {
            if (target == null)
            {
                headingSource = null;
                return;
            }
            ShipController ship = target.GetComponentInParent<ShipController>();
            if (ship == null)
                ship = target.GetComponentInChildren<ShipController>();
            headingSource = ship != null ? ship.transform : target;
        }

        // LateUpdate: after the ship has moved this frame, so the camera never lags a frame behind.
        private void LateUpdate()
        {
            if (target == null || def == null)
                return;
            float dt = Time.deltaTime;
            // Frame-rate independent smoothing: the same catch-up feel at 30 or 144 fps.
            float yawT = 1f - Mathf.Exp(-def.yawSharpness * dt);
            currentYaw = def.followShipHeading
                ? Mathf.LerpAngle(currentYaw, WantedYaw(), yawT)
                : def.yaw;

            Quaternion rotation = Quaternion.Euler(def.pitch, currentYaw, 0f);
            Vector3 wanted = DesiredPosition(rotation);
            float t = 1f - Mathf.Exp(-def.followSharpness * dt);
            transform.SetPositionAndRotation(Vector3.Lerp(transform.position, wanted, t), AimRotation());
        }

        private void SnapToTarget()
        {
            if (target == null || def == null)
                return;
            currentYaw = WantedYaw();
            Quaternion rotation = Quaternion.Euler(def.pitch, currentYaw, 0f);
            transform.SetPositionAndRotation(DesiredPosition(rotation), AimRotation());
        }

        // Where the camera faces: pitch places it, then aimUpAngle tilts the view up from there.
        private Quaternion AimRotation()
        {
            return Quaternion.Euler(def.pitch - def.aimUpAngle, currentYaw, 0f);
        }

        private float WantedYaw()
        {
            if (!def.followShipHeading)
                return def.yaw;
            Transform source = headingSource != null ? headingSource : target;
            return source.eulerAngles.y + def.yaw;
        }

        private Vector3 DesiredPosition(Quaternion rotation)
        {
            Vector3 flatForward = Quaternion.Euler(0f, currentYaw, 0f) * Vector3.forward;
            Vector3 lookAt = target.position + Vector3.up * def.lookHeight + flatForward * def.lookAhead;
            return lookAt - rotation * Vector3.forward * def.distance;
        }
    }
}
