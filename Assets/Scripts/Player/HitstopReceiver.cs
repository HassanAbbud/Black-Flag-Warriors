using UnityEngine;

namespace Ahoy.Player
{
    // Local hitstop: freezes this one Animator, never Time.timeScale, so the rest of the
    // battlefield keeps moving. Uses unscaled time so pause menus do not stretch it.
    public sealed class HitstopReceiver : MonoBehaviour
    {
        // Hitstop is authored in frames of the 60 fps target (R69).
        const float AuthoredFramesPerSecond = 60f;

        [SerializeField] Animator animator;

        float frozenUntil;
        float speedBeforeFreeze = 1f;

        public bool IsFrozen { get; private set; }

        public void Freeze(int frames)
        {
            if (frames <= 0) return;

            float until = Time.unscaledTime + frames / AuthoredFramesPerSecond;
            if (!IsFrozen)
            {
                speedBeforeFreeze = animator.speed;
                animator.speed = 0f;
                IsFrozen = true;
            }
            if (until > frozenUntil) frozenUntil = until;
        }

        void Update()
        {
            if (IsFrozen && Time.unscaledTime >= frozenUntil) Release();
        }

        void OnDisable()
        {
            if (IsFrozen) Release();
        }

        void Release()
        {
            animator.speed = speedBeforeFreeze;
            IsFrozen = false;
        }
    }
}
