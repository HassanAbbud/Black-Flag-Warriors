using UnityEngine;

namespace Ahoy.Enemies
{
    // W5.6 spawn pacing: how hard each side reinforces as the battle goes on. Monster curves run on
    // battle minutes (GDD: the island fights back as the day goes on); ally curves run on battle
    // progress, the share of the island won back, so taking ground brings more help.
    [CreateAssetMenu(menuName = "Ahoy/Crowd Pacing Def", fileName = "PACE_")]
    public sealed class CrowdPacingDef : ScriptableObject
    {
        [Tooltip("Seconds between two re-evaluations of the curves.")]
        public float refreshSeconds = 0.5f;

        [Header("Monsters, by battle minutes")]
        [Tooltip("Multiplies every portal camp's spawnInterval. Below 1 spawns faster.")]
        public AnimationCurve monsterIntervalScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 0.6f));
        [Tooltip("Multiplies every portal camp's maxAlive.")]
        public AnimationCurve monsterCapScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 1.5f));
        [Tooltip("Multiplies the strength of the unseen reinforcement waves.")]
        public AnimationCurve waveStrengthScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(30f, 2f));

        [Header("Allies, by battle progress (0..1)")]
        [Tooltip("Seconds between two real ally spawns.")]
        public AnimationCurve allyInterval = new AnimationCurve(new Keyframe(0f, 8f), new Keyframe(1f, 3f));
        [Tooltip("Real allies one spawner keeps out at once.")]
        public AnimationCurve allyMaxAlive = new AnimationCurve(new Keyframe(0f, 3f), new Keyframe(1f, 8f));
        [Tooltip("Multiplies how fast the unseen garrisons regrow.")]
        public AnimationCurve allyRegenScale = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 2f));

        [Header("Village in danger")]
        [Tooltip("Below this village integrity (0..1) the allies rally: interval and regen are boosted.")]
        [Range(0f, 1f)] public float rallyBelowIntegrity = 0.5f;
        [Tooltip("Ally interval is multiplied by this while rallying.")]
        public float rallyIntervalScale = 0.5f;
        [Tooltip("Ally regen is multiplied by this while rallying.")]
        public float rallyRegenScale = 1.5f;

        void OnValidate()
        {
            refreshSeconds = Mathf.Max(0.05f, refreshSeconds);
            rallyIntervalScale = Mathf.Max(0.05f, rallyIntervalScale);
            rallyRegenScale = Mathf.Max(0f, rallyRegenScale);
        }
    }
}
