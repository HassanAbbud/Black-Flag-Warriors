using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Feel
{
    // Swaps every renderer under this object to a flat flash material for a few frames —
    // red on Zahim, white on monsters. Works with any shader (no emission keyword needed).
    // Material arrays are built once in Awake; flashing allocates nothing.
    public sealed class HitFlash : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;
        [SerializeField] Material flashMaterial;

        Renderer[] renderers;
        Material[][] normal;
        Material[][] flashing;
        float flashUntil;
        bool on;

        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>(true);
            normal = new Material[renderers.Length][];
            flashing = new Material[renderers.Length][];
            for (int i = 0; i < renderers.Length; i++)
            {
                normal[i] = renderers[i].sharedMaterials;
                flashing[i] = new Material[normal[i].Length];
                for (int m = 0; m < flashing[i].Length; m++) flashing[i][m] = flashMaterial;
            }
        }

        public void Flash()
        {
            flashUntil = Time.unscaledTime + balance.hitFlashSeconds;
            if (on) return;
            on = true;
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = flashing[i];
        }

        void Update()
        {
            if (on && Time.unscaledTime >= flashUntil) Restore();
        }

        void OnDisable()
        {
            if (on) Restore();
        }

        void Restore()
        {
            on = false;
            for (int i = 0; i < renderers.Length; i++) renderers[i].sharedMaterials = normal[i];
        }
    }
}
