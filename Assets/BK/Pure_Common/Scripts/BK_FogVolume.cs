using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.Scripting.APIUpdating;

namespace BKPureNature
{
    [MovedFrom(true, sourceNamespace: "BKPureNature", sourceAssembly: null, sourceClassName: "FogVolumeTrigger")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(BoxCollider))]
    public sealed class BK_FogVolume : MonoBehaviour
    {
        private static readonly List<BK_FogVolume> Volumes = new List<BK_FogVolume>();

        private static Camera mainCamera;
        private static BK_EnvironmentManager cachedEnvironmentManager;

        private static bool hasBaseline;
        private static bool controlsFog;
        private static bool baselineFogEnabled;
        private static Color baselineFogColor;
        private static float baselineFogDensity;
        private static int lastProcessedFrame = -1;

        [Header("Fog")]
        public Color fogColor = Color.white;

        [Min(0f)]
        public float fogDensity = 0.01f;

        [Header("Transition")]
        [Tooltip("Time in seconds used to fade the fog when entering or leaving the volume.")]
        [Min(0f)]
        public float fadeDuration = 1f;

        [Tooltip("Higher-priority volumes are applied after lower-priority volumes when they overlap.")]
        public int priority;

        [Header("References")]
        [Tooltip("Optional. When empty, the camera tagged MainCamera is used.")]
        public Camera targetCamera;

        [Tooltip("Optional. When empty, the Environment Manager is found automatically.")]
        [FormerlySerializedAs("envManager")]
        public BK_EnvironmentManager environmentManager;

        private BoxCollider volumeCollider;
        private float blend;

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();

            if (!Volumes.Contains(this))
            {
                Volumes.Add(this);
            }
        }

        private void OnDisable()
        {
            Volumes.Remove(this);
            blend = 0f;

            if (!HasBlendedVolume())
            {
                RestoreBaselineFog();
            }
        }

        private void OnValidate()
        {
            fogDensity = Mathf.Max(0f, fogDensity);
            fadeDuration = Mathf.Max(0f, fadeDuration);
            CacheReferences();
        }

        private void LateUpdate()
        {
            ProcessVolumesOncePerFrame();
        }

        private void CacheReferences()
        {
            if (volumeCollider == null)
            {
                volumeCollider = GetComponent<BoxCollider>();
            }
        }

        private Camera ResolveCamera()
        {
            if (targetCamera != null)
            {
                return targetCamera;
            }

            if (mainCamera == null || !mainCamera.isActiveAndEnabled)
            {
                mainCamera = Camera.main;
            }

            return mainCamera;
        }

        private bool ContainsCamera(Camera camera)
        {
            if (camera == null || volumeCollider == null || !volumeCollider.enabled)
            {
                return false;
            }

            Vector3 localPoint = volumeCollider.transform.InverseTransformPoint(camera.transform.position);
            Vector3 offsetFromCenter = localPoint - volumeCollider.center;
            Vector3 halfSize = volumeCollider.size * 0.5f;

            return Mathf.Abs(offsetFromCenter.x) <= halfSize.x &&
                   Mathf.Abs(offsetFromCenter.y) <= halfSize.y &&
                   Mathf.Abs(offsetFromCenter.z) <= halfSize.z;
        }

        private void UpdateBlend(float deltaTime)
        {
            float targetBlend = ContainsCamera(ResolveCamera()) ? 1f : 0f;

            if (fadeDuration <= 0f)
            {
                blend = targetBlend;
                return;
            }

            blend = Mathf.MoveTowards(blend, targetBlend, deltaTime / fadeDuration);
        }

        private static void ProcessVolumesOncePerFrame()
        {
            if (!Application.isPlaying || lastProcessedFrame == Time.frameCount)
            {
                return;
            }

            lastProcessedFrame = Time.frameCount;
            RemoveMissingVolumes();

            if (Volumes.Count == 0)
            {
                RestoreBaselineFog();
                return;
            }

            Volumes.Sort(CompareVolumes);

            float deltaTime = Time.deltaTime;
            bool hasInfluence = false;

            for (int i = 0; i < Volumes.Count; i++)
            {
                BK_FogVolume volume = Volumes[i];
                volume.UpdateBlend(deltaTime);

                if (volume.blend > 0f)
                {
                    hasInfluence = true;
                }
            }

            if (!hasInfluence)
            {
                if (controlsFog)
                {
                    RestoreBaselineFog();
                }
                else
                {
                    CaptureBaselineFog();
                }

                return;
            }

            if (!controlsFog)
            {
                CaptureBaselineFog();
                controlsFog = true;
            }

            Color outputColor = GetEnvironmentFogColor();
            float outputDensity = baselineFogDensity;

            for (int i = 0; i < Volumes.Count; i++)
            {
                BK_FogVolume volume = Volumes[i];

                if (volume.blend <= 0f)
                {
                    continue;
                }

                float weight = Mathf.Clamp01(volume.blend);
                outputColor = Color.Lerp(outputColor, volume.fogColor, weight);
                outputDensity = Mathf.Lerp(outputDensity, volume.fogDensity, weight);
            }

            RenderSettings.fog = true;
            RenderSettings.fogColor = outputColor;
            RenderSettings.fogDensity = Mathf.Max(0f, outputDensity);
        }

        private static int CompareVolumes(BK_FogVolume a, BK_FogVolume b)
        {
            int priorityComparison = a.priority.CompareTo(b.priority);

            if (priorityComparison != 0)
            {
                return priorityComparison;
            }

            return a.GetInstanceID().CompareTo(b.GetInstanceID());
        }

        private static void CaptureBaselineFog()
        {
            baselineFogEnabled = RenderSettings.fog;
            baselineFogColor = RenderSettings.fogColor;
            baselineFogDensity = RenderSettings.fogDensity;
            hasBaseline = true;
        }

        private static Color GetEnvironmentFogColor()
        {
            BK_EnvironmentManager manager = ResolveEnvironmentManager();

            if (manager != null &&
                manager.overrideFogColor &&
                manager.directionalLight != null &&
                manager.fogColorGradient != null)
            {
                float dot = Vector3.Dot(manager.directionalLight.transform.forward, Vector3.up);
                float lightingTime = Mathf.Clamp01((dot + 1f) * 0.5f);
                baselineFogColor = manager.fogColorGradient.Evaluate(lightingTime);
            }

            return baselineFogColor;
        }

        private static BK_EnvironmentManager ResolveEnvironmentManager()
        {
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                BK_FogVolume volume = Volumes[i];

                if (volume != null && volume.environmentManager != null)
                {
                    cachedEnvironmentManager = volume.environmentManager;
                    return cachedEnvironmentManager;
                }
            }

            if (cachedEnvironmentManager == null)
            {
                cachedEnvironmentManager = FindEnvironmentManager();
            }

            return cachedEnvironmentManager;
        }

        private static BK_EnvironmentManager FindEnvironmentManager()
        {
#if UNITY_2022_2_OR_NEWER
            return FindFirstObjectByType<BK_EnvironmentManager>();
#else
            return FindObjectOfType<BK_EnvironmentManager>();
#endif
        }

        private static void RestoreBaselineFog()
        {
            if (!hasBaseline)
            {
                controlsFog = false;
                return;
            }

            RenderSettings.fog = baselineFogEnabled;
            RenderSettings.fogColor = GetEnvironmentFogColor();
            RenderSettings.fogDensity = baselineFogDensity;
            controlsFog = false;
        }

        private static bool HasBlendedVolume()
        {
            for (int i = 0; i < Volumes.Count; i++)
            {
                BK_FogVolume volume = Volumes[i];

                if (volume != null && volume.blend > 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void RemoveMissingVolumes()
        {
            for (int i = Volumes.Count - 1; i >= 0; i--)
            {
                if (Volumes[i] == null)
                {
                    Volumes.RemoveAt(i);
                }
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStaticState()
        {
            Volumes.Clear();
            mainCamera = null;
            cachedEnvironmentManager = null;
            hasBaseline = false;
            controlsFog = false;
            baselineFogEnabled = false;
            baselineFogColor = Color.black;
            baselineFogDensity = 0f;
            lastProcessedFrame = -1;
        }
    }
}