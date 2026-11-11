namespace Ahoy.Core
{
    // W5.6 crowd spawn management: how hard each side reinforces right now, set from battle
    // progress and battle time by the crowd director and read by portals, battle fronts and the
    // battle simulation. The stub keeps authored rates and fields no real crowds.
    public interface ISpawnPacing
    {
        // Portal camps.
        float MonsterIntervalScale { get; }   // multiplies a portal camp's spawnInterval
        float MonsterCapScale { get; }        // multiplies a portal camp's maxAlive

        // The unseen battle.
        float WaveStrengthScale { get; }      // multiplies the reinforcement waves
        float AllyRegenScale { get; }         // multiplies the garrisons' regrowth

        // Real crowds at the battle fronts. Rank 0 is the front nearest Zahim; fronts further
        // down the list get a smaller share of the crowd budget (R69–R73).
        int FrontMonsterCap(int rank);
        int FrontAllyCap(int rank);
        float FrontMonsterInterval { get; }   // seconds between two spawn bursts per front
        float FrontAllyInterval { get; }
        int FrontBurst { get; }               // units per side per burst
    }
}
