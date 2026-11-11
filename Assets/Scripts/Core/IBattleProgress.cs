using UnityEngine;

namespace Ahoy.Core
{
    public enum LocationKind { Village, Portal, SeaPortal, Stronghold, Outpost, FinalBreach }

    // One battle location as the HUD and the spawners see it. A copy, so reading it never
    // lets anyone change the simulation.
    public readonly struct BattleLocationStatus
    {
        public readonly int Id;
        public readonly string Name;
        public readonly LocationKind Kind;
        public readonly Faction Holder;
        public readonly Vector3 Position;
        public readonly float MonsterStrength;   // R64 units, not head-count
        public readonly float AllyStrength;
        public readonly bool Fighting;           // both sides present: the HUD's battle-status marker
        public readonly bool Seen;               // Zahim is here, so real units fight instead of the simulation
        public readonly bool Locked;             // the final breach before enough portals are sealed
        public readonly float NextWaveIn;        // seconds; negative when this location sends no waves
        public readonly float Meter01;           // portal meter toward its guardian, or how cleared it is

        public BattleLocationStatus(int id, string name, LocationKind kind, Faction holder, Vector3 position,
                                    float monsterStrength, float allyStrength, bool fighting, bool seen,
                                    bool locked, float nextWaveIn, float meter01)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Holder = holder;
            Position = position;
            MonsterStrength = monsterStrength;
            AllyStrength = allyStrength;
            Fighting = fighting;
            Seen = seen;
            Locked = locked;
            NextWaveIn = nextWaveIn;
            Meter01 = meter01;
        }

        public bool IsPortal => Kind == LocationKind.Portal || Kind == LocationKind.SeaPortal || Kind == LocationKind.FinalBreach;
    }

    // Read-only view of the whole battle (W5.2), for the HUD, minimap and spawn pacing.
    public interface IBattleProgress
    {
        int LocationCount { get; }
        BattleLocationStatus GetLocation(int index);
        bool TryGetLocation(int locationId, out BattleLocationStatus status);

        int PortalCount { get; }
        int SealedCount { get; }
        int HeldCount { get; }             // locations held by the village, the village itself excluded
        float Progress01 { get; }          // share of the island won back
        float VillageIntegrity01 { get; }
        int KoCount { get; }
        float BattleSeconds { get; }
        int SeenLocationId { get; }        // -1 when Zahim is at none
        bool Ended { get; }
    }
}
