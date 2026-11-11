using UnityEngine;

namespace Ahoy.Enemies
{
    // W5.6 crowd spawn management data. Monster curves run on battle minutes (GDD: the island
    // fights back as the day goes on); ally curves run on battle progress, the share of the island
    // won back, so taking ground brings more help. Front caps are per rank: rank 0 is the battle
    // front nearest Zahim and gets the biggest share of the crowd budget (R69–R73).
    [CreateAssetMenu(menuName = "Ahoy/Crowd Pacing Def", fileName = "PACE_")]
    public sealed class CrowdPacingDef : ScriptableObject
    {
        [Tooltip("Seconds between two re-evaluations of the curves.")]
        public float refreshSeconds = 0.5f;

        [Header("Battle fronts: crowd budget by rank (nearest front first)")]
        [Tooltip("Monsters each front fields, by rank. Every front on the island gets a share, so the whole map fights at once; fronts past the end of the list field none.")]
        public int[] frontMonsterCaps = { 45, 32, 24, 20, 16, 14, 12, 10, 10, 10, 8, 8 };
        [Tooltip("Allies each front fields, by rank.")]
        public int[] frontAllyCaps = { 22, 16, 12, 10, 10, 8, 8, 6, 6, 6, 6, 6 };
        [Tooltip("Monster slots kept free for the portal camps and their guardians' fights.")]
        public int campReserve = 40;
        [Tooltip("Units per side that join a front in one burst.")]
        public int frontBurst = 3;

        [Header("Monsters, by battle minutes")]
        [Tooltip("Multiplies the monsters each front fields.")]
        public AnimationCurve frontMonsterScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 1.4f));
        [Tooltip("Seconds between two monster bursts at a front.")]
        public AnimationCurve frontMonsterInterval = new AnimationCurve(new Keyframe(0f, 1.2f), new Keyframe(30f, 0.6f));
        [Tooltip("Multiplies every portal camp's spawnInterval. Below 1 spawns faster.")]
        public AnimationCurve monsterIntervalScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 0.6f));
        [Tooltip("Multiplies every portal camp's maxAlive.")]
        public AnimationCurve monsterCapScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 1.5f));
        [Tooltip("Multiplies the strength of the unseen reinforcement waves.")]
        public AnimationCurve waveStrengthScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 2f));

        [Header("Allies, by battle progress (0..1)")]
        [Tooltip("Multiplies the allies each front fields.")]
        public AnimationCurve frontAllyScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 1.6f));
        [Tooltip("Seconds between two ally bursts at a front.")]
        public AnimationCurve frontAllyInterval = new AnimationCurve(new Keyframe(0f, 1.6f), new Keyframe(1f, 0.8f));
        [Tooltip("Multiplies how fast the unseen garrisons regrow.")]
        public AnimationCurve allyRegenScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2f));

        [Header("Village in danger")]
        [Tooltip("Below this village integrity (0..1) the allies rally.")]
        [Range(0f, 1f)] public float rallyBelowIntegrity = 0.5f;
        [Tooltip("Ally burst interval is multiplied by this while rallying.")]
        public float rallyIntervalScale = 0.5f;
        [Tooltip("Ally regen is multiplied by this while rallying.")]
        public float rallyRegenScale = 1.5f;

        void OnValidate()
        {
            refreshSeconds = Mathf.Max(0.05f, refreshSeconds);
            campReserve = Mathf.Max(0, campReserve);
            frontBurst = Mathf.Max(1, frontBurst);
            rallyIntervalScale = Mathf.Max(0.05f, rallyIntervalScale);
            rallyRegenScale = Mathf.Max(0f, rallyRegenScale);
        }
    }
}
