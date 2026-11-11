using Ahoy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ahoy.Feel
{
    // Field-of-view zoom for big moments: a held zoom that eases in and out, plus kicks that
    // snap in and spring back. Only the FOV is touched, so it never fights the script that
    // moves the camera (follow, lock-on) or CameraShake, which owns position and rotation.
    // Optionally blends a post-processing Volume (vignette, saturation...) with the held zoom.
    // Unscaled time, like the shake: it keeps easing through hitstop and pause.
    [RequireComponent(typeof(Camera))]
    public sealed class CameraZoom : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;
        [Tooltip("Optional. Its weight follows the held zoom's weight; set its own Weight to 0.")]
        [SerializeField] Volume volume;

        Camera cam;
        float baseFov;
        float holdTarget;
        float hold;
        float volumeTarget;
        float punch;

        // Degrees to stay zoomed in until told otherwise, and the Volume weight that goes with it.
        public void SetHold(float degrees, float volumeWeight)
        {
            holdTarget = degrees;
            volumeTarget = Mathf.Clamp01(volumeWeight);
        }

        // Kicks the view in by this many degrees; it springs back on its own. The biggest kick wins.
        public void Punch(float degrees) => punch = Mathf.Max(punch, degrees);

        void Awake()
        {
            cam = GetComponent<Camera>();
            baseFov = cam.fieldOfView;
            if (volume != null) volume.weight = 0f;
        }

        void OnDisable()
        {
            hold = holdTarget = punch = 0f;
            if (cam != null) cam.fieldOfView = baseFov;
            if (volume != null) volume.weight = 0f;
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            // Frame-rate independent easing, as in PlayerCamera.
            float holdK = 1f - Mathf.Exp(-balance.zoomHoldSharpness * dt);
            float punchK = 1f - Mathf.Exp(-balance.zoomPunchRecovery * dt);

            hold = Mathf.Lerp(hold, holdTarget, holdK);
            punch = Mathf.Lerp(punch, 0f, punchK);
            cam.fieldOfView = baseFov - hold - punch;

            if (volume != null) volume.weight = Mathf.Lerp(volume.weight, volumeTarget, holdK);
        }
    }
}
