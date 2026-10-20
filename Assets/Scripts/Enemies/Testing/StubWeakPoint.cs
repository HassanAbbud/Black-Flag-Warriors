using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Enemies.Testing
{
    // Throwaway: stands in for the weak-point track so the boss's stagger can be tested alone
    // (Code Patterns 2.6: every service ships a do-nothing stub). Put it on the boss and press H
    // to expose it. Delete before submission.
    public sealed class StubWeakPoint : MonoBehaviour, IWeakPoint
    {
        [Tooltip("R23: the exposed window.")]
        [SerializeField] float windowSeconds = 8f;

        float exposedUntil;

        public WeakPointState State => Time.time < exposedUntil ? WeakPointState.Exposed : WeakPointState.Hidden;
        public float Progress => 1f;
        public ToolType? CounterTool => ToolType.Bomb;
        public float WindowRemaining => Mathf.Max(0f, exposedUntil - Time.time);

        public bool TryExpose(ExposeSource source)
        {
            exposedUntil = Time.time + windowSeconds;
            return true;
        }

        public void Drain(float amount) => Debug.Log($"{name}: weak point drained by {amount}", this);

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.hKey.wasPressedThisFrame) TryExpose(ExposeSource.ToolCounter);
        }
    }
}
