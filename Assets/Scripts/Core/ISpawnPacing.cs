namespace Ahoy.Core
{
    // How fast each side reinforces right now (W5.6). Set from battle progress and battle time by
    // the crowd director; read by portals, ally spawners and the simulation. The stub is neutral.
    public interface ISpawnPacing
    {
        float MonsterIntervalScale { get; }   // multiplies a portal camp's spawnInterval
        float MonsterCapScale { get; }        // multiplies a portal camp's maxAlive
        float WaveStrengthScale { get; }      // multiplies the unseen reinforcement waves
        float AllyInterval { get; }           // seconds between two real ally spawns
        int AllyMaxAlive { get; }             // real allies one spawner keeps out at once
        float AllyRegenScale { get; }         // multiplies the unseen garrisons' regrowth
    }
}
