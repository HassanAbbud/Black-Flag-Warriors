using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Player
{
    // Orbit-follow camera. Lives on Zahim so it receives Look from PlayerInput; drives the
    // scene camera assigned in cameraTransform. While LockOn has a target (R52) the camera
    // swings to keep it in front and ignores look input, which LockOn reads as flicks.
    public sealed class PlayerCamera : MonoBehaviour
    {
        const string MouseScheme = "Keyboard&Mouse";

        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerInput playerInput;
        [SerializeField] LockOn lockOn;
        [Tooltip("The scene camera to drive. Wire it on the scene instance, not on the prefab.")]
        [SerializeField] Transform cameraTransform;
        [SerializeField] bool lockCursor = true;

        float yaw;
        float pitch;
        Vector2 stick;
        Vector2 mouseDelta;
        bool recentering;

        public Quaternion YawRotation => Quaternion.Euler(0f, yaw, 0f);

        // Swing behind him (guard). Any look input takes control back.
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
        }

        // Mouse sends a delta per event; the stick sends a held value.
        void OnLook(InputValue value)
        {
            Vector2 look = value.Get<Vector2>();
            if (playerInput != null && playerInput.currentControlScheme == MouseScheme) mouseDelta += look;
            else stick = look;
        }

        Vector3 PivotPosition => transform.position + Vector3.up * balance.cameraPivotHeight;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            Vector2 turn = mouseDelta * balance.cameraMouseSensitivity + stick * (balance.cameraStickSpeed * dt);
            mouseDelta = Vector2.zero;

            if (lockOn != null && lockOn.HasTarget)
            {
                recentering = false;

                Vector3 toTarget = lockOn.Target.position + Vector3.up * balance.lockOnTargetHeight - PivotPosition;
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
            cameraTransform.SetPositionAndRotation(PivotPosition - rotation * Vector3.forward * balance.cameraDistance, rotation);
        }
    }
}
