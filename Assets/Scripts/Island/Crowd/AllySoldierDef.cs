using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // Type Object + Flyweight: one asset per kind of village soldier. Every soldier points at its
    // def and keeps only its own health, state and timers (Code Patterns 2.3, 2.9).
    [CreateAssetMenu(menuName = "Ahoy/Ally Soldier Def", fileName = "ALY_")]
    public sealed class AllySoldierDef : ScriptableObject
    {
        [Header("Identity")]
        public Tier tier = Tier.Scuttler;
        [Tooltip("R64: the unseen-battle value of one soldier.")]
        public float strength = 1f;

        [Header("Stats")]
        public float maxHealth = 80f;
        [Tooltip("Metres per second.")]
        public float moveSpeed = 4f;
        [Tooltip("Degrees per second.")]
        public float turnSpeed = 540f;
        [Tooltip("Random ± share of moveSpeed, so a line never moves in lockstep.")]
        [Range(0f, 0.5f)] public float speedJitter = 0.15f;
        [Tooltip("Metres. Crowd queries grow a soldier by this, since it has no collider.")]
        public float bodyRadius = 0.45f;

        [Header("Fighting")]
        [Tooltip("Metres within which it looks for a monster to fight.")]
        public float engageRange = 35f;
        [Tooltip("Seconds between two looks for the nearest monster.")]
        public float retargetInterval = 0.5f;
        [Tooltip("Metres from the monster at which it stops and swings.")]
        public float attackRange = 1.6f;
        public float damage = 14f;
        public HitReaction reaction = HitReaction.Flinch;
        [Tooltip("R22: light 4.")]
        public float weakPointDrain = 4f;
        [Tooltip("Seconds of one swing.")]
        public float swingSeconds = 0.9f;
        [Tooltip("Seconds into the swing at which the blow lands.")]
        public float hitAt = 0.4f;
        [Tooltip("Seconds between swings, ± cooldownJitter.")]
        public float attackCooldown = 1.4f;
        [Range(0f, 1f)] public float cooldownJitter = 0.4f;

        [Header("Holding ground")]
        [Tooltip("Metres around its post it spreads out to while nothing is in reach.")]
        public float postSpread = 6f;

        [Header("Hit reactions")]
        public float flinchSeconds = 0.35f;
        public float knockbackSeconds = 0.6f;
        public float knockbackDistance = 2f;
        [Tooltip("Seconds the body stays after death before going back to the pool.")]
        public float corpseSeconds = 2.5f;

        void OnValidate()
        {
            maxHealth = Mathf.Max(1f, maxHealth);
            moveSpeed = Mathf.Max(0f, moveSpeed);
            engageRange = Mathf.Max(0f, engageRange);
            retargetInterval = Mathf.Max(0.05f, retargetInterval);
            attackRange = Mathf.Max(0.1f, attackRange);
            swingSeconds = Mathf.Max(0.05f, swingSeconds);
            hitAt = Mathf.Clamp(hitAt, 0f, swingSeconds);
            attackCooldown = Mathf.Max(0f, attackCooldown);
            corpseSeconds = Mathf.Max(0f, corpseSeconds);
        }
    }
}
