using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // A place on the island the two sides fight over: a portal, an outpost, the village. It only
    // holds its identity, its route and its state; BattleSimulation ticks every location in one
    // loop (Code Patterns 2.8), so this has no Update.
    public sealed class BattleLocation : MonoBehaviour
    {
        static readonly Color GizmoMonsters = new Color(0.9f, 0.2f, 0.2f, 0.9f);
        static readonly Color GizmoVillage = new Color(0.2f, 0.5f, 1f, 0.9f);
        static readonly Color GizmoRoute = new Color(1f, 0.6f, 0.1f, 0.8f);

        [Tooltip("Unique. Matches the PortalCamp's and the units' locationId (R8). The village uses its own.")]
        [SerializeField] int locationId;
        [SerializeField] string displayName;
        [SerializeField] BattleLocationDef def;
        [Tooltip("Where monsters gathering here march next, toward the village. Empty for the village.")]
        [SerializeField] BattleLocation next;

        // State, owned and written by BattleSimulation only.
        internal Faction holder;
        internal float monsters;
        internal float allies;
        internal float incoming;
        internal float waveTimer;
        internal float meter;
        internal bool hasCamp;          // a real PortalCamp reports for this location
        internal PortalPhase campPhase;
        internal bool fighting;
        internal bool seen;
        internal bool locked;

        public int LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public BattleLocationDef Def => def;
        public BattleLocation Next => next;

        internal void ResetState()
        {
            holder = def.startHolder;
            monsters = def.startMonsterStrength;
            allies = def.startAllyStrength;
            incoming = 0f;
            waveTimer = def.waveInterval;
            meter = 0f;
            hasCamp = false;
            campPhase = PortalPhase.Open;
            fighting = false;
            seen = false;
            locked = false;
        }

        internal BattleLocationStatus Status()
        {
            bool sendsWaves = def.waveStrength > 0f && holder == Faction.Monsters && !locked;
            return new BattleLocationStatus(locationId, DisplayName, def.kind, holder, transform.position,
                                            monsters, allies, fighting, seen, locked,
                                            sendsWaves ? waveTimer : -1f, meter);
        }

        // Red for the monsters, blue for the village, the route to the next location in orange.
        void OnDrawGizmos()
        {
            if (def == null) return;
            Faction shown = Application.isPlaying ? holder : def.startHolder;
            Gizmos.color = shown == Faction.Monsters ? GizmoMonsters : GizmoVillage;
            Gizmos.DrawWireSphere(transform.position, 4f);
            Gizmos.DrawWireSphere(transform.position, def.seenRadius);
            if (next == null) return;
            Gizmos.color = GizmoRoute;
            Gizmos.DrawLine(transform.position + Vector3.up * 2f, next.transform.position + Vector3.up * 2f);
        }
    }
}
