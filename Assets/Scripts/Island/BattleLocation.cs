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
        [Tooltip("Ally strength here at the start instead of the def's; below 0 uses the def. Zahim's landing party.")]
        [SerializeField] float startAllyOverride = -1f;
        [Tooltip("False for places only reached by sea: no crowds on the field, numbers only.")]
        [SerializeField] bool onLand = true;

        // State, owned and written by BattleSimulation only.
        internal Faction holder;
        internal float monsters;
        internal float allies;
        internal float incoming;
        internal float incomingAllies;
        internal BattleLocation allyTarget;   // where surplus soldiers march to attack
        internal float waveTimer;
        internal float meter;
        internal bool hasCamp;          // a real PortalCamp reports for this location
        internal PortalPhase campPhase;
        internal bool fighting;
        internal bool seen;
        internal bool locked;
        internal bool guardianDown;     // a stronghold's guardian is dead
        internal BattleFront front;     // optional: where this location's battle becomes real
        internal float playerDistance;

        // True while its numbers stand on the field as real crowds.
        internal bool Materialized => front != null && front.Active;

        public int LocationId => locationId;
        public string DisplayName => string.IsNullOrEmpty(displayName) ? name : displayName;
        public BattleLocationDef Def => def;
        public BattleLocation Next => next;
        public bool OnLand => onLand;

        internal void ResetState()
        {
            holder = def.startHolder;
            monsters = def.startMonsterStrength;
            allies = startAllyOverride >= 0f ? startAllyOverride : def.startAllyStrength;
            incoming = 0f;
            incomingAllies = 0f;
            allyTarget = null;
            waveTimer = def.waveInterval;
            meter = 0f;
            hasCamp = false;
            campPhase = PortalPhase.Open;
            fighting = false;
            seen = false;
            locked = false;
            guardianDown = false;
            if (front == null) front = GetComponentInChildren<BattleFront>();
        }

        internal BattleLocationStatus Status()
        {
            bool sendsWaves = def.waveStrength > 0f && holder == Faction.Monsters && !locked;
            return new BattleLocationStatus(DisplayName, def.kind, holder, fighting, locked, sendsWaves ? waveTimer : -1f, meter);
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
