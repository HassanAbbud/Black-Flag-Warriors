using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Bosses
{
    // Type Object: one asset per flying sea boss (W7.1). SeaDragonBoss holds the behaviour.
    [CreateAssetMenu(menuName = "Ahoy/Sea Dragon Def", fileName = "SEA_")]
    public sealed class SeaDragonDef : ScriptableObject
    {
        [Header("Body")]
        public int maxHealth = 6000;
        [Tooltip("Metres above the sea it flies at.")]
        public float altitude = 14f;
        [Tooltip("Metres per second while circling.")]
        public float flySpeed = 9f;
        public float turnSpeed = 90f;

        [Header("Finding Zahim's ship")]
        [Tooltip("Metres from its lair at which it notices the player ship.")]
        public float detectRange = 90f;
        [Tooltip("Circles its lair at this radius until a ship comes.")]
        public float patrolRadius = 25f;
        [Tooltip("Circles the player ship at this radius while fighting.")]
        public float engageRadius = 35f;
        [Tooltip("Gives up and flies home past this distance from its lair.")]
        public float leashRange = 160f;

        [Header("Fireball volleys (bullet hell)")]
        [Tooltip("Seconds between two volleys.")]
        public float volleyCooldown = 3.5f;
        [Tooltip("Seconds of the shooting pose before the volley leaves its mouth.")]
        public float windUpSeconds = 0.8f;
        [Tooltip("Fireballs in an aimed fan.")]
        public int fanCount = 7;
        [Tooltip("Degrees the fan spreads across.")]
        public float fanSpread = 50f;
        [Tooltip("Fireballs in a ring; every third volley is a ring.")]
        public int ringCount = 18;
        [Tooltip("Metres per second each fireball leaves at.")]
        public float fireballSpeed = 22f;
        public float fireballDamage = 60f;
        public HitReaction fireballReaction = HitReaction.Knockback;

        [Header("Death")]
        [Tooltip("Seconds it falls into the sea before it is switched off.")]
        public float deathSeconds = 4f;
    }
}
