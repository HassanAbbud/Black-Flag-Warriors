using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Ships
{
    // Swings the player ship's cannon toward the mouse and fires it. Sits on the cannon object
    // (a child of the ship). The mouse is projected onto the sea at the ship's height, so the
    // cannon points where the cursor is on the water, not where it is on the screen.
    // Input arrives from PlayerShipController; nothing here reads a device directly.
    [DisallowMultipleComponent]
    public sealed class PlayerShipCannonController : MonoBehaviour
    {
        [Tooltip("The ship this cannon belongs to. Found in the parents automatically if left empty.")]
        [SerializeField] private PlayerShipController ship;
        [Tooltip("Where cannonballs leave the barrel. Its forward is the firing direction.")]
        [SerializeField] private Transform muzzlePoint;
        [Tooltip("Camera the mouse position is read through. Uses the MainCamera-tagged camera if left empty.")]
        [SerializeField] private Camera aimCamera;

        // Flat, normalised world direction from the cannon toward the mouse. Read by PlayerShipAimArrow.
        public Vector3 AimDirection => aimDirection;
        // Where aiming is measured from: the cannon's pivot, which sits toward the bow, not the ship's centre.
        public Vector3 AimOrigin => transform.position;

        private PlayerShipDef def;
        private Vector2 aimScreenPoint;
        private bool hasAimInput;
        private Vector3 aimDirection;
        private float yaw;              // degrees off the bow, what the cannon is pointing at now
        private float fireRequestedAt;  // when the last unconsumed fire press happened; < 0 = none
        private float readyAt;

        private void Awake()
        {
            if (ship == null)
                ship = GetComponentInParent<PlayerShipController>();
            if (aimCamera == null)
                aimCamera = Camera.main;
            def = ship.Def;
        }

        private void OnEnable()
        {
            yaw = 0f;
            readyAt = 0f;
            fireRequestedAt = -1f;
            aimDirection = ship.transform.forward;
        }

        public void SetAimScreenPoint(Vector2 screenPoint)
        {
            aimScreenPoint = screenPoint;
            hasAimInput = true;
        }

        // Command: remember the press; TryConsumeFire fires it once the cannon is loaded.
        public void BufferFire() => fireRequestedAt = Time.time;

        private void Update()
        {
            if (!ship.IsAlive)
            {
                fireRequestedAt = -1f;
                return;
            }

            UpdateAimDirection();
            TurnTowardAim(Time.deltaTime);
            TryConsumeFire();
        }

        private void UpdateAimDirection()
        {
            if (!hasAimInput || aimCamera == null)
                return;
            Transform shipTransform = ship.transform;
            Plane sea = new Plane(Vector3.up, shipTransform.position);
            Ray ray = aimCamera.ScreenPointToRay(aimScreenPoint);
            if (!sea.Raycast(ray, out float enter))
                return;   // cursor above the horizon - keep the last aim

            // From the cannon, not the ship's centre: the cannon is near the bow, and aiming from the
            // centre left it pointing parallel to the mouse instead of at it.
            Vector3 toMouse = ray.GetPoint(enter) - transform.position;
            toMouse.y = 0f;
            if (toMouse.sqrMagnitude > 0.0001f)
                aimDirection = toMouse.normalized;
        }

        private void TurnTowardAim(float dt)
        {
            Transform shipTransform = ship.transform;
            float wanted = Vector3.SignedAngle(shipTransform.forward, aimDirection, Vector3.up);
            wanted = Mathf.Clamp(wanted, -def.cannonYawLimit, def.cannonYawLimit);
            yaw = Mathf.MoveTowardsAngle(yaw, wanted, def.cannonTurnSpeed * dt);
            // Unity is left-handed: a negative x rotation raises the barrel.
            transform.localRotation = Quaternion.Euler(-def.cannonElevation, yaw, 0f);
        }

        private void TryConsumeFire()
        {
            if (fireRequestedAt < 0f)
                return;
            if (Time.time - fireRequestedAt > def.fireBuffer)
            {
                fireRequestedAt = -1f;   // pressed too long before the reload finished - drop it
                return;
            }
            if (Time.time < readyAt)
                return;

            fireRequestedAt = -1f;
            readyAt = Time.time + def.fireCooldown;
            Fire();
        }

        private void Fire()
        {
            GameObject ball = ship.Cannonball;
            if (ball == null || muzzlePoint == null)
                return;
            GameObject shot = PoolManager.Instance.GetObject(ball.name, ball, muzzlePoint.position, muzzlePoint.rotation);
            if (shot.TryGetComponent(out ShipBullet bullet))
                bullet.SetDamage(ship.CannonDamage);
        }
    }
}
