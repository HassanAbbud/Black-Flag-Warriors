namespace Ahoy.Core
{
    // Keep: a small watchtower post, quick to take; once the village holds it, it sends soldiers.
    public enum LocationKind { Village, Portal, SeaPortal, Stronghold, Outpost, FinalBreach, Keep }

    // One battle location as the HUD sees it: a copy, so reading it never changes the simulation.
    public readonly struct BattleLocationStatus
    {
        public readonly string Name;
        public readonly LocationKind Kind;
        public readonly Faction Holder;
        public readonly bool Fighting;           // both sides present: the HUD's battle-status marker
        public readonly bool Locked;             // the final breach before enough portals are sealed
        public readonly float NextWaveIn;        // seconds; negative when this location sends no waves
        public readonly float Meter01;           // portal meter toward its guardian, or how cleared it is

        public BattleLocationStatus(string name, LocationKind kind, Faction holder, bool fighting, bool locked,
                                    float nextWaveIn, float meter01)
        {
            Name = name;
            Kind = kind;
            Holder = holder;
            Fighting = fighting;
            Locked = locked;
            NextWaveIn = nextWaveIn;
            Meter01 = meter01;
        }

        public bool IsPortal => Kind == LocationKind.Portal || Kind == LocationKind.SeaPortal || Kind == LocationKind.FinalBreach;
    }

    // Read-only view of the whole battle (W5.2), for the HUD and the spawn pacing.
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
        int FieldMonsters { get; }         // real crowd units fighting at the fronts right now
        int FieldAllies { get; }
    }
}
