using UnityEngine;

namespace Ahoy.Core
{
    // How the player's sweep finds crowd monsters. They have no colliders (Code Patterns,
    // "Physics"), so the attacker asks the crowd instead of the physics engine. Implemented by
    // Enemies' MonsterManager; PlayerAttack calls it alongside its OverlapBox.
    public interface ICrowdQuery
    {
        // Fills results with the living units inside the oriented box and returns how many.
        // Never allocates; stops when results is full.
        int QueryBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, IDamageable[] results);
    }
}
