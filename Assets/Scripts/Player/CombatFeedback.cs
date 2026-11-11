using Ahoy.Core;
using Ahoy.Feel;
using UnityEngine;

namespace Ahoy.Player
{
    // Turns combat events into feel: sparks, shake and flash. Only listens (Observer);
    // the systems that raise the events know nothing about it.
    public sealed class CombatFeedback : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerAttack attack;
        [SerializeField] PlayerHealth health;
        [SerializeField] CameraShake shake;
        [SerializeField] HitFlash flash;

        [Header("Pools")]
        [SerializeField] VfxPool lightHit;
        [SerializeField] VfxPool heavyHit;
        [SerializeField] VfxPool block;

        [Tooltip("Where block sparks appear, in Zahim's local space (metres).")]
        [SerializeField] Vector3 shieldOffset = new Vector3(0f, 1.2f, 0.5f);

        void OnEnable()
        {
            attack.Hit += OnHit;
            health.Damaged += OnDamaged;
            health.Blocked += OnBlocked;
        }

        void OnDisable()
        {
            attack.Hit -= OnHit;
            health.Damaged -= OnDamaged;
            health.Blocked -= OnBlocked;
        }

        void OnHit(HitReport hit)
        {
            Vector3 away = hit.Point - transform.position;
            away.y = 0f;
            Quaternion facing = away.sqrMagnitude > 0f ? Quaternion.LookRotation(away) : transform.rotation;

            VfxPool pool = hit.Attack.hitVfx == HitVfxKind.Heavy ? heavyHit : lightHit;
            pool.Play(hit.Point, facing);

            // One shake per swing beat, not one per scuttler.
            if (hit.Hitstopped) shake.AddTrauma(hit.Attack.shakeTrauma);
        }

        void OnDamaged(Vector3 origin)
        {
            flash.Flash();
            shake.AddTrauma(balance.playerHitTrauma);
        }

        void OnBlocked(Vector3 origin)
        {
            block.Play(transform.TransformPoint(shieldOffset), transform.rotation);
            shake.AddTrauma(balance.blockTrauma);
        }
    }
}
