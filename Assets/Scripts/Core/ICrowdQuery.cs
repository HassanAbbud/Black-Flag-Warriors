using UnityEngine;

namespace Ahoy.Core
{
    // How everyone else finds crowd monsters. They have no colliders (Code Patterns, "Physics"),
    // so the attacker asks the crowd instead of the physics engine. Implemented by Enemies'
    // MonsterManager and reached through BattleContext.Crowd.
    public interface ICrowdQuery
    {
        // Fills results with the living units inside the oriented box and returns how many.
        // Never allocates; stops when results is full. Zahim's sweep uses this.
        int QueryBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, IDamageable[] results);

        // The living unit nearest to from (flat distance) within maxDistance. Allies use this
        // to find something to fight; never allocates.
        bool TryFindNearest(Vector3 from, float maxDistance, out IDamageable nearest);
    }
}
