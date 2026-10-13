using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Player
{
    public sealed class PlayerCamera : MonoBehaviour
    {
        const string MouseScheme = "Keyboard&Mouse";
        static readonly Collider[] hitBuffer = new Collider[32];

        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerInput playerInput;
        [Tooltip("The scene camera to drive. Wire it on the scene instance, not on the prefab.")]
        [SerializeField] Transform cameraTransform;
        [SerializeField] bool lockCursor = true;

        [Header("Lock-on")]
        [SerializeField] LayerMask enemyMask;      // enemy Layer
        [SerializeField] LayerMask obstacleMask;   // layer blocking line-of-sight to the target

        float yaw;
        float pitch;
        Vector2 stick;
        Vector2 mouseDelta;
        bool recentering;

        public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);
        public Transform LockedTarget { get; private set; }
        public bool IsLockedOn => LockedTarget != null;

        public void RecenterBehind() => recentering = true;

        void Awake()
        {
            yaw = transform.eulerAngles.y;
            pitch = balance.cameraDefaultPitch;
        }

        void OnEnable()
        {
            if (lockCursor) Cursor.lockState = CursorLockMode.Locked;
        }

        void OnDisable()
        {
            if (lockCursor) Cursor.lockState = CursorLockMode.None;
            LockedTarget = null;
        }

        void OnLook(InputValue value)
        {
            Vector2 look = value.Get<Vector2>();
            if (playerInput != null && playerInput.currentControlScheme == MouseScheme) mouseDelta += look;
            else stick = look;
        }

        // Send Messages will be called twice, check isPressed
        void OnLockOn(InputValue value)
        {
            if (!value.isPressed) return;
            if (IsLockedOn) LockedTarget = null;
            else TryAcquireTarget();
        }

        Vector3 PivotPosition => transform.position + Vector3.up * balance.cameraPivotHeight;
        Vector3 AimPoint(Transform t) => t.position + Vector3.up * balance.lockOnTargetHeight;

        void TryAcquireTarget()
        {
            Vector3 origin = PivotPosition;
            Vector3 viewForward = YawRotation * Vector3.forward; // camera forward projected onto the horizontal plane

            int count = Physics.OverlapSphereNonAlloc(
                transform.position, balance.lockOnRange, hitBuffer, enemyMask, QueryTriggerInteraction.Ignore);

            Transform best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider col = hitBuffer[i];
                Transform candidate = col.attachedRigidbody != null ? col.attachedRigidbody.transform : col.transform;

                Vector3 point = AimPoint(candidate);
                Vector3 toTarget = point - origin;
                Vector3 flat = new Vector3(toTarget.x, 0f, toTarget.z);
                if (flat.sqrMagnitude < 0.01f) continue;

                float angle = Vector3.Angle(viewForward, flat);
                if (angle > balance.lockOnMaxAngle) continue;

                if (obstacleMask.value != 0 &&
                    Physics.Linecast(origin, point, obstacleMask, QueryTriggerInteraction.Ignore)) continue;

                float score = angle + toTarget.magnitude * balance.lockOnDistanceWeight;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }

            LockedTarget = best; // keep unlocked if no valid target was found, so the player can try again next frame
        }

        bool IsTargetValid()
        {
            if (LockedTarget == null || !LockedTarget.gameObject.activeInHierarchy) return false;
            float breakRange = balance.lockOnBreakRange;
            return (LockedTarget.position - transform.position).sqrMagnitude <= breakRange * breakRange;
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector2 turn = mouseDelta * balance.cameraMouseSensitivity + stick * (balance.cameraStickSpeed * dt);
            mouseDelta = Vector2.zero;

            if (IsLockedOn && !IsTargetValid()) LockedTarget = null;

            if (IsLockedOn)
            {
                recentering = false;

                Vector3 toTarget = AimPoint(LockedTarget) - PivotPosition;
                toTarget.y = 0f;
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    float desiredYaw = Mathf.Atan2(toTarget.x, toTarget.z) * Mathf.Rad2Deg;
                    float k = 1f - Mathf.Exp(-balance.lockOnSmoothing * dt); // frame-rate independent smoothing
                    yaw = Mathf.LerpAngle(yaw, desiredYaw, k);
                    pitch = Mathf.Lerp(pitch, balance.lockOnPitch, k);
                }
            }
            else
            {
                if (turn.sqrMagnitude > 0f) recentering = false;
                if (recentering)
                {
                    yaw = Mathf.MoveTowardsAngle(yaw, transform.eulerAngles.y, balance.cameraRecenterSpeed * dt);
                    recentering = Mathf.DeltaAngle(yaw, transform.eulerAngles.y) != 0f;
                }

                yaw += turn.x;
                pitch = Mathf.Clamp(pitch - turn.y, balance.cameraPitchLimits.x, balance.cameraPitchLimits.y);
            }

            if (cameraTransform == null) return;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
            cameraTransform.SetPositionAndRotation(
                PivotPosition - rotation * Vector3.forward * balance.cameraDistance, rotation);
        }
    }
}