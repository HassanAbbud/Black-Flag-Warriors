using Ahoy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ahoy.Feel
{
    // Trauma-based shake: hits, stomps and explosions add trauma (0..1), it decays over time, and the shake grows
    // with trauma squared so small hits stay subtle and big ones land hard.
    // The offset is applied only while the main camera renders and removed right after, so
    // it never fights whichever script drives the camera (follow, lock-on) and never drifts.
    public sealed class CameraShake : MonoBehaviour
    {
        const string MainCameraTag = "MainCamera";

        [SerializeField] BalanceConfig balance;

        float trauma;
        float seed;
        Camera shaken;
        Vector3 savedPosition;
        Quaternion savedRotation;

        public void AddTrauma(float amount) => trauma = Mathf.Clamp01(trauma + amount);

        void Awake() => seed = Random.value * 100f;

        void OnEnable()
        {
            RenderPipelineManager.beginCameraRendering += Apply;
            RenderPipelineManager.endCameraRendering += Restore;
        }

        void OnDisable()
        {
            RenderPipelineManager.beginCameraRendering -= Apply;
            RenderPipelineManager.endCameraRendering -= Restore;
            trauma = 0f;
        }

        // Unscaled: shake keeps decaying through hitstop and pause.
        void Update() => trauma = Mathf.Max(0f, trauma - balance.shakeDecayPerSecond * Time.unscaledDeltaTime);

        void Apply(ScriptableRenderContext context, Camera camera)
        {
            if (trauma <= 0f || camera.cameraType != CameraType.Game || !camera.CompareTag(MainCameraTag)) return;

            float shake = trauma * trauma;
            float t = Time.unscaledTime * balance.shakeFrequency;
            Transform cam = camera.transform;
            shaken = camera;
            savedPosition = cam.position;
            savedRotation = cam.rotation;

            Vector3 offset = new Vector3(Noise(t, 0f), Noise(t, 1f), 0f) * (balance.shakeMaxOffset * shake);
            Vector3 angles = new Vector3(Noise(t, 2f), Noise(t, 3f), Noise(t, 4f)) * (balance.shakeMaxAngle * shake);
            cam.SetPositionAndRotation(savedPosition + savedRotation * offset, savedRotation * Quaternion.Euler(angles));
        }

        void Restore(ScriptableRenderContext context, Camera camera)
        {
            if (camera != shaken) return;
            camera.transform.SetPositionAndRotation(savedPosition, savedRotation);
            shaken = null;
        }

        // -1..1 Perlin, one independent channel per axis.
        float Noise(float t, float channel) => Mathf.PerlinNoise(seed + channel * 10f, t) * 2f - 1f;
    }
}
