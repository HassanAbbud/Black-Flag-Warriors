using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Ships
{
    public class PlayerShipCannonController : MonoBehaviour
    {
        [Header("Limits (相对船体前方)")]
        [SerializeField] private float yawLimit = 90f;        // 左右各 90 度，共 180 度
        [SerializeField] private float maxPitchUp = 30f;      // 最多向上 30 度
        [SerializeField] private float maxPitchDown = 0f;     // 向下限制，0 = 不允许向下

        [Header("Sensitivity")]
        [SerializeField] private float sensitivityX = 0.1f;   // 度 / 像素
        [SerializeField] private float sensitivityY = 0.1f;
        [SerializeField] private bool invertY = false;

        [Header("Cursor")]
        [SerializeField] private bool lockCursor = true;

        private float _yaw;     // 相对船头的水平角
        private float _pitch;   // 向上为正

        private void Start()
        {
            // 以当前局部旋转作为初始角度
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

            // 按 Esc 释放光标，点击重新锁定（方便调试）
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

            // Unity 中绕 X 轴负方向是抬头，所以俯仰取负
            transform.localRotation = Quaternion.Euler(-_pitch, _yaw, 0f);
        }

        private static void SetCursorLocked(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }
    }
}