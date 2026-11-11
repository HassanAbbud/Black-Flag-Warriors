using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // Type Object: one asset per kind of battle location (portal, sea portal, stronghold, outpost,
    // village, final breach, watchtower keep). BattleLocation holds where it is; this holds how it behaves.
    // Strengths are in R64 units: one Imp is worth its MonsterDef.strength.
    [CreateAssetMenu(menuName = "Ahoy/Battle Location Def", fileName = "LOC_")]
    public sealed class BattleLocationDef : ScriptableObject
    {
        public LocationKind kind = LocationKind.Portal;

        [Header("Start of the battle")]
        public Faction startHolder = Faction.Monsters;
        public float startMonsterStrength = 10f;
        public float startAllyStrength;

        [Header("Monsters")]
        [Tooltip("Strength one reinforcement wave adds while the monsters hold it. 0: sends no waves.")]
        public float waveStrength = 6f;
        [Tooltip("Seconds between two waves.")]
        public float waveInterval = 60f;
        [Tooltip("Most monster strength that can gather here.")]
        public float maxMonsterStrength = 40f;
        [Tooltip("Strength the monsters keep here; anything above marches on to the next location.")]
        public float holdStrength = 10f;
        [Tooltip("Strength per second that marches on toward the next location.")]
        public float marchRate = 0.3f;
        [Tooltip("Monster strength needed, once the defenders are gone, to take it (a sealed portal reopens).")]
        public float captureStrength = 4f;

        [Header("Village garrison")]
        [Tooltip("Ally strength per second that regrows while the village holds it.")]
        public float allyRegen = 0.1f;
        public float maxAllyStrength = 15f;
        [Tooltip("Ally strength that moves in the moment the village takes it (a portal is sealed).")]
        public float garrisonOnCapture = 8f;
        [Tooltip("Ally strength the village keeps here; anything above marches out to attack the nearest monster-held neighbour.")]
        public float allyHold = 20f;
        [Tooltip("Ally strength per second that marches out.")]
        public float allyMarchRate = 0.3f;

        [Header("Zahim")]
        [Tooltip("Metres. Zahim inside it is 'at' this location (HUD, captures).")]
        public float seenRadius = 60f;

        void OnValidate()
        {
            startMonsterStrength = Mathf.Max(0f, startMonsterStrength);
            startAllyStrength = Mathf.Max(0f, startAllyStrength);
            waveStrength = Mathf.Max(0f, waveStrength);
            waveInterval = Mathf.Max(1f, waveInterval);
            maxMonsterStrength = Mathf.Max(0f, maxMonsterStrength);
            holdStrength = Mathf.Clamp(holdStrength, 0f, maxMonsterStrength);
            marchRate = Mathf.Max(0f, marchRate);
            captureStrength = Mathf.Max(0.01f, captureStrength);
            allyRegen = Mathf.Max(0f, allyRegen);
            maxAllyStrength = Mathf.Max(0f, maxAllyStrength);
            garrisonOnCapture = Mathf.Max(0f, garrisonOnCapture);
            allyHold = Mathf.Max(0f, allyHold);
            allyMarchRate = Mathf.Max(0f, allyMarchRate);
            seenRadius = Mathf.Max(0f, seenRadius);
        }
    }
}
