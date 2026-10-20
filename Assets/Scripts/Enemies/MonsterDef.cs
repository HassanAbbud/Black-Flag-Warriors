using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // Type Object + Flyweight: one asset per species (MON_Scuttler, MON_Brute). Every instance
    // points at its def and keeps only its own health, state and timers. The numbers here are
    // placeholders until the Build Spec's monster table is applied.
    [CreateAssetMenu(menuName = "Ahoy/Monster Def", fileName = "MON_")]
    public class MonsterDef : ScriptableObject
    {
        [Header("Identity")]
        public Tier tier = Tier.Scuttler;
        [Tooltip("R64: the unseen-battle value of one unit.")]
        public float strength = 1f;

        [Header("Stats")]
        public float maxHealth = 60f;
        [Tooltip("Metres per second.")]
        public float moveSpeed = 3.5f;
        [Tooltip("Degrees per second.")]
        public float turnSpeed = 360f;
        [Tooltip("Metres from Zahim at which it stops advancing and waits for its attack.")]
        public float stopDistance = 1f;
        [Tooltip("Metres from Zahim at which it notices him and charges.")]
        public float aggroRange = 60f;
        [Tooltip("Metres. Crowd queries grow a unit's position by this, since it has no collider.")]
        public float bodyRadius = 0.5f;

        [Header("Attacks (crowd units; bosses use their patterns)")]
        [Tooltip("Checked in order; the first whose range fits is used.")]
        public MonsterAttackDef[] attacks;

        [Header("Hit reactions")]
        [Tooltip("Reactions weaker than this are shrugged off (super armour). Flinch reacts to everything but None.")]
        public HitReaction minReaction = HitReaction.Flinch;
        public float flinchSeconds = 0.3f;
        public float knockbackSeconds = 0.6f;
        [Tooltip("Metres a knockback slides it.")]
        public float knockbackDistance = 2f;
        [Tooltip("Metres a launch or blowaway slides it.")]
        public float blowawayDistance = 4f;

        [Header("Death")]
        [Tooltip("Seconds the body stays before it returns to the pool.")]
        public float corpseSeconds = 1.5f;

        [Header("Animation")]
        public float animationCrossFade = 0.1f;
        [Tooltip("Seconds for the animator Speed parameter to catch up.")]
        public float speedDampSeconds = 0.08f;
    }
}
