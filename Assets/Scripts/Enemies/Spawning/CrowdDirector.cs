using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // W5.6 Crowd spawn management: turns battle progress and battle time into how many units each
    // side fields at every battle front and how fast they arrive, inside the crowd budget
    // (R69–R73), and publishes it as ISpawnPacing through BattleContext. Portal camps, battle
    // fronts and the battle simulation read it; nobody is told what to do. Data in CrowdPacingDef.
    public sealed class CrowdDirector : MonoBehaviour, ISpawnPacing
    {
        const float SecondsPerMinute = 60f;
        const int MaxRanks = 16;

        [SerializeField] CrowdPacingDef def;

        readonly int[] monsterCaps = new int[MaxRanks];
        readonly int[] allyCaps = new int[MaxRanks];
        float nextRefreshAt;

        public float MonsterIntervalScale { get; private set; } = 1f;
        public float MonsterCapScale { get; private set; } = 1f;
        public float WaveStrengthScale { get; private set; } = 1f;
        public float AllyRegenScale { get; private set; } = 1f;
        public float FrontMonsterInterval { get; private set; } = 1f;
        public float FrontAllyInterval { get; private set; } = 1f;
        public int FrontBurst { get; private set; }

        public int FrontMonsterCap(int rank) => rank >= 0 && rank < MaxRanks ? monsterCaps[rank] : 0;
        public int FrontAllyCap(int rank) => rank >= 0 && rank < MaxRanks ? allyCaps[rank] : 0;

        void Awake()
        {
            Debug.Assert(def != null, "CrowdDirector needs a CrowdPacingDef", this);
            Refresh();
        }

        void OnEnable() => BattleContext.RegisterPacing(this);
        void OnDisable() => BattleContext.UnregisterPacing(this);

        void Update()
        {
            if (Time.time < nextRefreshAt) return;
            nextRefreshAt = Time.time + def.refreshSeconds;
            Refresh();
        }

        void Refresh()
        {
            if (def == null) return;
            IBattleProgress battle = BattleContext.Progress;
            float minutes = battle.BattleSeconds / SecondsPerMinute;
            float progress = battle.Progress01;
            bool rallying = battle.VillageIntegrity01 < def.rallyBelowIntegrity;

            MonsterIntervalScale = Mathf.Max(0.05f, def.monsterIntervalScale.Evaluate(minutes));
            MonsterCapScale = Mathf.Max(0.05f, def.monsterCapScale.Evaluate(minutes));
            WaveStrengthScale = Mathf.Max(0f, def.waveStrengthScale.Evaluate(minutes));
            float regen = Mathf.Max(0f, def.allyRegenScale.Evaluate(progress));
            AllyRegenScale = rallying ? regen * def.rallyRegenScale : regen;

            FrontMonsterInterval = Mathf.Max(0.05f, def.frontMonsterInterval.Evaluate(minutes));
            float allyInterval = Mathf.Max(0.05f, def.frontAllyInterval.Evaluate(progress));
            FrontAllyInterval = rallying ? allyInterval * def.rallyIntervalScale : allyInterval;
            FrontBurst = def.frontBurst;

            FillCaps(monsterCaps, def.frontMonsterCaps, def.frontMonsterScale.Evaluate(minutes), MonsterBudget());
            FillCaps(allyCaps, def.frontAllyCaps, def.frontAllyScale.Evaluate(progress), AllyBudget());
        }

        // R69: whatever the curves ask for, the fronts together never go past the pools.
        int MonsterBudget() => Mathf.Max(0, BattleContext.Spawner(Faction.Monsters).Capacity - def.campReserve);
        int AllyBudget() => BattleContext.Spawner(Faction.Village).Capacity;

        static void FillCaps(int[] caps, int[] authored, float scale, int budget)
        {
            int wanted = 0;
            for (int i = 0; i < caps.Length; i++)
            {
                caps[i] = authored != null && i < authored.Length ? Mathf.Max(0, Mathf.RoundToInt(authored[i] * scale)) : 0;
                wanted += caps[i];
            }
            if (wanted <= budget || wanted == 0) return;
            float fit = (float)budget / wanted;
            int total = 0;
            for (int i = 0; i < caps.Length; i++) total += caps[i] = Mathf.FloorToInt(caps[i] * fit);
            Debug.Assert(total <= budget, "front caps went over the crowd budget");
        }
    }
}
