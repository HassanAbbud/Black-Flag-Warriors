using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Ahoy.Allies
{
    // Ally's body: health and identity. Implements IUnit (which includes IDamageable) so nothing
    // outside ever needs to know this is an ally.
    public sealed class AllyUnit : MonoBehaviour, IUnit
    {
        static readonly int DieHash = Animator.StringToHash("Die");

        [Header("Identity")]
        [SerializeField] Faction faction = Faction.Village;
        [SerializeField] Tier tier = Tier.Brute;
        [SerializeField] int locationId;           // home portal (R8)： only its own kills will advance that progress bar
        [SerializeField] float strength = 10f;     // strength is used to calculate damage dealt to enemies in the battle system you can't see 

        [Header("Health")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float destroyDelay = 3f;

        [Header("Animation (optional)")]
        [SerializeField] Animator animator;        // trigger "Die"

        float health;

        // hit reaction and death events
        public event Action<HitReaction> Hit;
        public event Action Died;

        // IDamageable
        public Faction Faction => faction;
        public bool IsAlive => health > 0f;

        // IUnit
        public Tier Tier => tier;
        public int LocationId => locationId;
        public float Strength => strength;
        public IWeakPoint WeakPoint => null;       // ally has no weak point, but the interface requires it

        public float HealthNormalized => health / maxHealth;

        void Awake() => health = maxHealth;

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            health = Mathf.Max(0f, health - info.Amount);

            if (!IsAlive)
            {
                Die();
                return;
            }

            if (info.Reaction != HitReaction.None) Hit?.Invoke(info.Reaction);
        }

        void Die()
        {
            // close off the unit so it can't be hit again, and trigger death animation if any
            if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
            foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

            if (animator != null) animator.SetTrigger(DieHash);

            Died?.Invoke();
            Destroy(gameObject, destroyDelay);
        }
    }
}