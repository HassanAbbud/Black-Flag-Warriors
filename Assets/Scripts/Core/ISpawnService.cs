using UnityEngine;

namespace Ahoy.Core
{
    // How the battle asks for crowd units without knowing who owns them (Code Patterns 2.6, 2.7).
    // One per faction, reached through BattleContext.Spawner(faction): Enemies' MonsterManager
    // for the monsters, the Island's AllyCrowd for the village's soldiers. Never Instantiates.
    public interface ISpawnService
    {
        int Capacity { get; }

        // A pooled unit of that species (a MonsterDef or an AllySoldierDef). Null when the pool
        // is dry or the crowd is at capacity; the caller tries again later.
        IUnit SpawnCrowd(ScriptableObject species, Vector3 position, Quaternion rotation, int locationId);

        // Back to the pool without counting as a death: a front Zahim left behind.
        void Despawn(IUnit unit);
    }
}
