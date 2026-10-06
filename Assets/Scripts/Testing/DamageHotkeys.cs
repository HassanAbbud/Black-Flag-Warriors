using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Testing
{
    // Throwaway: hurt the target from the keyboard to test hit reactions and death without
    // enemies. F1 flinch, F2 knockback, F3 kill. Delete before submission.
    public sealed class DamageHotkeys : MonoBehaviour
    {
        [SerializeField] GameObject target;
        [SerializeField] float flinchDamage = 50f;
        [SerializeField] float knockbackDamage = 150f;
        [SerializeField] float killDamage = 100000f;
        [Tooltip("Metres in front of the target the fake hit comes from.")]
        [SerializeField] float hitDistance = 2f;
        [SerializeField] int hitstopFrames = 6;

        IDamageable damageable;

        void Awake() => damageable = target.GetComponent<IDamageable>();

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.f1Key.wasPressedThisFrame) Hit(flinchDamage, HitReaction.Flinch);
            if (keyboard.f2Key.wasPressedThisFrame) Hit(knockbackDamage, HitReaction.Knockback);
            if (keyboard.f3Key.wasPressedThisFrame) Hit(killDamage, HitReaction.Blowaway);
        }

        void Hit(float amount, HitReaction reaction)
        {
            Vector3 origin = target.transform.position + target.transform.forward * hitDistance;
            var info = new DamageInfo(amount, origin, reaction, hitstopFrames, 0f, null);
            damageable.TakeDamage(in info);
        }
    }
}
