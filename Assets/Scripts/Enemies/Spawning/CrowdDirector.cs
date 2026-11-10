using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // W5.6 Crowd spawn management: turns battle progress and battle time into spawn rates for both
    // sides, and publishes them as ISpawnPacing through BattleContext. Portal camps, ally spawners
    // and the battle simulation read it; nobody is told what to do. Curves live in CrowdPacingDef.
    public sealed class CrowdDirector : MonoBehaviour, ISpawnPacing
    {
        const float SecondsPerMinute = 60f;

        [SerializeField] CrowdPacingDef def;

        float nextRefreshAt;

        public float MonsterIntervalScale { get; private set; } = 1f;
        public float MonsterCapScale { get; private set; } = 1f;
        public float WaveStrengthScale { get; private set; } = 1f;
        public float AllyInterval { get; private set; }
        public int AllyMaxAlive { get; private set; }
        public float AllyRegenScale { get; private set; } = 1f;
        public bool Rallying { get; private set; }

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
            Rallying = battle.VillageIntegrity01 < def.rallyBelowIntegrity;

            MonsterIntervalScale = Mathf.Max(0.05f, def.monsterIntervalScale.Evaluate(minutes));
            MonsterCapScale = Mathf.Max(0.05f, def.monsterCapScale.Evaluate(minutes));
            WaveStrengthScale = Mathf.Max(0f, def.waveStrengthScale.Evaluate(minutes));

            float interval = Mathf.Max(0.1f, def.allyInterval.Evaluate(progress));
            float regen = Mathf.Max(0f, def.allyRegenScale.Evaluate(progress));
            AllyInterval = Rallying ? interval * def.rallyIntervalScale : interval;
            AllyRegenScale = Rallying ? regen * def.rallyRegenScale : regen;
            AllyMaxAlive = Mathf.Max(0, Mathf.RoundToInt(def.allyMaxAlive.Evaluate(progress)));
        }
    }
}
