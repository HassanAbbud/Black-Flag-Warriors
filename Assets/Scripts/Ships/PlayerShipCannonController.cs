using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Ships
{
    public class PlayerShipCannonController : MonoBehaviour
    {
        [Header("Limits (relative to ship front)")]
        [SerializeField] private float yawLimit = 90f;        // 180 degrees left/right
        [SerializeField] private float maxPitchUp = 30f;      // 30 degrees up
        [SerializeField] private float maxPitchDown = 0f;     // no look down

        [Header("Sensitivity")]
        [SerializeField] private float sensitivityX = 0.1f;   
        [SerializeField] private float sensitivityY = 0.1f;
        [SerializeField] private bool invertY = false;

        [Header("Cursor")]
        [SerializeField] private bool lockCursor = true;

        private float _yaw;     // relative to ship front
        private float _pitch;   // positive = look up

        private void Start()
        {
            Vector3 e = transform.localEulerAngles;
            _yaw = Mathf.DeltaAngle(0f, e.y);
            _pitch = -Mathf.DeltaAngle(0f, e.x);

            if (lockCursor) SetCursorLocked(true);
        }

        private void OnDisable()
        {
            if (lockCursor) SetCursorLocked(false);
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return;

            // ESC to unlock cursor, left click to lock cursor
            if (lockCursor)
            {
                if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
                    SetCursorLocked(false);
                else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                    SetCursorLocked(true);

                if (Cursor.lockState != CursorLockMode.Locked) return;
            }

            Vector2 delta = mouse.delta.ReadValue();

            _yaw += delta.x * sensitivityX;
            _pitch += delta.y * sensitivityY * (invertY ? -1f : 1f);

            _yaw = Mathf.Clamp(_yaw, -yawLimit, yawLimit);
            _pitch = Mathf.Clamp(_pitch, -maxPitchDown, maxPitchUp);

            // unity uses left-handed coordinate system, so we negate pitch to look up
            transform.localRotation = Quaternion.Euler(-_pitch, _yaw, 0f);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}