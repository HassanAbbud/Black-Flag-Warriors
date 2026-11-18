using UnityEngine;

namespace Ahoy.Island
{
    // The battle-wide numbers of the unseen battle (W5.2). Per-location numbers live in BattleLocationDef.
    [CreateAssetMenu(menuName = "Ahoy/Battle Sim Def", fileName = "SIM_")]
    public sealed class BattleSimDef : ScriptableObject
    {
        [Tooltip("Seconds between two simulation steps. The unseen battle does not need every frame.")]
        public float tickSeconds = 0.5f;
        [Tooltip("R64: strength each side loses per second, per point of enemy strength facing it.")]
        public float killRate = 0.04f;
        [Tooltip("Below this a side counts as gone.")]
        public float emptyStrength = 0.05f;

        [Header("Village")]
        public float villageIntegrity = 100f;
        [Tooltip("Integrity lost per second, per point of monster strength in the undefended village.")]
        public float integrityDamage = 0.15f;
        [Tooltip("Raise BattleEnded(false) when integrity reaches zero.")]
        public bool loseWhenVillageFalls = true;

        [Header("Morale (Dynasty Warriors style: Zahim's deeds carry the village's soldiers)")]
        [Range(0f, 1f)] public float startMorale = 0.4f;
        [Tooltip("Morale per monster Zahim KOs himself.")]
        public float moralePerKo = 0.002f;
        [Tooltip("Morale when the village takes a location.")]
        public float moralePerCapture = 0.08f;
        [Tooltip("Morale lost when the monsters take a location.")]
        public float moralePerLoss = 0.1f;
        [Tooltip("Morale per second drifting back to startMorale, so it has to be kept up.")]
        public float moraleDrift = 0.002f;

        [Header("Final breach")]
        [Tooltip("Portals that must be sealed before the final breach opens to Zahim.")]
        public int sealedForFinalBreach = 6;

        void OnValidate()
        {
            tickSeconds = Mathf.Max(0.05f, tickSeconds);
            killRate = Mathf.Max(0f, killRate);
            emptyStrength = Mathf.Max(0.001f, emptyStrength);
            villageIntegrity = Mathf.Max(1f, villageIntegrity);
            integrityDamage = Mathf.Max(0f, integrityDamage);
            sealedForFinalBreach = Mathf.Max(0, sealedForFinalBreach);
        }
    }
}
