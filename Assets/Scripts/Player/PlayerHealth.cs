using System;
using Ahoy.Core;
using Ahoy.Feel;
using UnityEngine;

namespace Ahoy.Player
{
    // Takes damage. The lose condition (R3) belongs to Systems, which subscribes to Died.
    public sealed class PlayerHealth : MonoBehaviour, IDamageable
    {
        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerMotor motor;
        [SerializeField] HitstopReceiver hitstop;

        public event Action<float, float> HealthChanged;   // current, max
        public event Action Died;
        public event Action<Vector3> Damaged;   // origin of the hit
        public event Action<Vector3> Blocked;   // origin of the blocked hit

        public float Current { get; private set; }
        public float Max => balance.playerHealth;

        public Faction Faction => Faction.Village;
        public bool IsAlive => Current > 0f;

        void Awake() => Current = balance.playerHealth;

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive || motor.IsInvulnerable) return;   // R51 dodge i-frames
            if (motor.TryBlock(in info))                     // R51 guard
            {
                hitstop.Freeze(info.HitstopFrames);
                Blocked?.Invoke(info.Origin);
                return;
            }

            Current = Mathf.Max(0f, Current - info.Amount);
            HealthChanged?.Invoke(Current, Max);
            hitstop.Freeze(info.HitstopFrames);
            Damaged?.Invoke(info.Origin);

            if (Current > 0f)
            {
                motor.ApplyHitReaction(info.Reaction, info.Origin);
                return;
            }

            motor.Kill();
            Died?.Invoke();
        }

        // Hearts (red urns) and level-ups. Does nothing once he is dead.
        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Current = Mathf.Min(Max, Current + amount);
            HealthChanged?.Invoke(Current, Max);
        }
    }
}
