using System;
using UnityEngine;

namespace BKPureNature
{
    [ExecuteAlways]
    public sealed class BK_LightShafts : MonoBehaviour
    {
        private static readonly Quaternion LightRotationOffset = Quaternion.Euler(-90f, 0f, 0f);

        [Header("Directional Light")]
        [SerializeField] private Light directionalLight;

        [Tooltip("Copies the Directional Light RGB color to new and already alive particles without modifying alpha or material properties.")]
        [SerializeField] private bool useDirectionalLightColor;

        [Header("Particle Systems")]
        [Tooltip("Uses every Particle System on this GameObject and in its children, including inactive children.")]
        [SerializeField] private bool useChildParticleSystems;

        [Tooltip("Particle Systems used when Use Child Particle Systems is disabled.")]
        [SerializeField] private ParticleSystem[] particleSystems = Array.Empty<ParticleSystem>();

        private CachedParticleSystem[] cachedParticleSystems = Array.Empty<CachedParticleSystem>();
        private Quaternion lastLightRotation;
        private Color lastLightColor;
        private bool lightStateInitialized;

        private sealed class CachedParticleSystem
        {
            public readonly ParticleSystem ParticleSystem;
            public ParticleSystem.Particle[] ParticleBuffer = Array.Empty<ParticleSystem.Particle>();

            public CachedParticleSystem(ParticleSystem particleSystem)
            {
                ParticleSystem = particleSystem;
            }
        }

        private void OnEnable()
        {
            RefreshParticleSystemCache();
            ApplyLightState(forceRotation: true, forceColor: useDirectionalLightColor);
        }

        private void OnValidate()
        {
            RefreshParticleSystemCache();
            ApplyLightState(forceRotation: true, forceColor: useDirectionalLightColor);
        }

        private void OnTransformChildrenChanged()
        {
            if (!useChildParticleSystems)
            {
                return;
            }

            RefreshParticleSystemCache();
            ApplyLightState(forceRotation: true, forceColor: useDirectionalLightColor);
        }

        private void Update()
        {
            if (directionalLight == null)
            {
                lightStateInitialized = false;
                return;
            }

            Quaternion currentRotation = directionalLight.transform.rotation;
            Color currentColor = directionalLight.color;

            bool rotationChanged = !lightStateInitialized || currentRotation != lastLightRotation;
            bool colorChanged = useDirectionalLightColor &&
                                (!lightStateInitialized || !SameRgb(currentColor, lastLightColor));

            if (rotationChanged || colorChanged)
            {
                ApplyLightState(rotationChanged, colorChanged);
            }
        }

        private void RefreshParticleSystemCache()
        {
            ParticleSystem[] systems = useChildParticleSystems
                ? GetComponentsInChildren<ParticleSystem>(includeInactive: true)
                : particleSystems ?? Array.Empty<ParticleSystem>();

            CachedParticleSystem[] newCache = new CachedParticleSystem[systems.Length];

            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                CachedParticleSystem existingCache = FindCachedParticleSystem(system);
                newCache[i] = existingCache ?? new CachedParticleSystem(system);
            }

            cachedParticleSystems = newCache;
        }

        private CachedParticleSystem FindCachedParticleSystem(ParticleSystem particleSystem)
        {
            for (int i = 0; i < cachedParticleSystems.Length; i++)
            {
                CachedParticleSystem cachedSystem = cachedParticleSystems[i];

                if (cachedSystem != null && cachedSystem.ParticleSystem == particleSystem)
                {
                    return cachedSystem;
                }
            }

            return null;
        }

        private void ApplyLightState(bool forceRotation, bool forceColor)
        {
            if (directionalLight == null)
            {
                lightStateInitialized = false;
                return;
            }

            Quaternion lightRotation = directionalLight.transform.rotation;
            Color lightColor = directionalLight.color;

            for (int i = 0; i < cachedParticleSystems.Length; i++)
            {
                CachedParticleSystem cachedSystem = cachedParticleSystems[i];
                ParticleSystem particleSystem = cachedSystem?.ParticleSystem;

                if (particleSystem == null)
                {
                    continue;
                }

                if (forceRotation)
                {
                    particleSystem.transform.rotation = lightRotation * LightRotationOffset;
                }

                if (forceColor && useDirectionalLightColor)
                {
                    ApplyLightRgb(cachedSystem, lightColor);
                }
            }

            lastLightRotation = lightRotation;
            lastLightColor = lightColor;
            lightStateInitialized = true;
        }

        private static void ApplyLightRgb(CachedParticleSystem cachedSystem, Color lightColor)
        {
            ParticleSystem particleSystem = cachedSystem.ParticleSystem;

            // Future particles.
            ParticleSystem.MainModule main = particleSystem.main;
            ParticleSystem.MinMaxGradient startColor = main.startColor;

            switch (startColor.mode)
            {
                case ParticleSystemGradientMode.Color:
                    startColor.color = ReplaceRgb(startColor.color, lightColor);
                    break;

                case ParticleSystemGradientMode.TwoColors:
                    startColor.colorMin = ReplaceRgb(startColor.colorMin, lightColor);
                    startColor.colorMax = ReplaceRgb(startColor.colorMax, lightColor);
                    break;

                case ParticleSystemGradientMode.Gradient:
                case ParticleSystemGradientMode.RandomColor:
                    startColor.gradient = CopyGradientWithLightRgb(startColor.gradient, lightColor);
                    break;

                case ParticleSystemGradientMode.TwoGradients:
                    startColor.gradientMin = CopyGradientWithLightRgb(startColor.gradientMin, lightColor);
                    startColor.gradientMax = CopyGradientWithLightRgb(startColor.gradientMax, lightColor);
                    break;
            }

            main.startColor = startColor;

            // Already alive particles. Only RGB is replaced; each particle keeps its current alpha.
            int particleCount = particleSystem.particleCount;

            if (particleCount <= 0)
            {
                return;
            }

            if (cachedSystem.ParticleBuffer.Length < particleCount)
            {
                int bufferSize = Mathf.NextPowerOfTwo(particleCount);
                cachedSystem.ParticleBuffer = new ParticleSystem.Particle[bufferSize];
            }

            int aliveParticleCount = particleSystem.GetParticles(cachedSystem.ParticleBuffer);
            Color32 lightColor32 = lightColor;

            for (int i = 0; i < aliveParticleCount; i++)
            {
                Color32 particleColor = cachedSystem.ParticleBuffer[i].startColor;
                particleColor.r = lightColor32.r;
                particleColor.g = lightColor32.g;
                particleColor.b = lightColor32.b;
                cachedSystem.ParticleBuffer[i].startColor = particleColor;
            }

            particleSystem.SetParticles(cachedSystem.ParticleBuffer, aliveParticleCount);
        }

        private static Color ReplaceRgb(Color original, Color lightColor)
        {
            return new Color(lightColor.r, lightColor.g, lightColor.b, original.a);
        }

        private static Gradient CopyGradientWithLightRgb(Gradient source, Color lightColor)
        {
            if (source == null)
            {
                return null;
            }

            GradientColorKey[] colorKeys = source.colorKeys;
            GradientAlphaKey[] alphaKeys = source.alphaKeys;

            for (int i = 0; i < colorKeys.Length; i++)
            {
                colorKeys[i].color = new Color(lightColor.r, lightColor.g, lightColor.b, 1f);
            }

            Gradient result = new Gradient
            {
                mode = source.mode
            };

            result.SetKeys(colorKeys, alphaKeys);
            return result;
        }

        private static bool SameRgb(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) &&
                   Mathf.Approximately(a.g, b.g) &&
                   Mathf.Approximately(a.b, b.b);
        }
    }
}