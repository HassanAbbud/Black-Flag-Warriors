using UnityEngine;

namespace Ahoy.Core
{
    // What other tracks may know about Zahim's body. Damage goes through IDamageable,
    // death through PlayerHealth.Died.
    public interface IPlayerMotor
    {
        Vector3 Position { get; }
        Vector3 Forward { get; }
        bool IsInvulnerable { get; }   // R51 dodge i-frames
        bool IsDead { get; }

        // Respawn, dock transitions (R39). Moves him without sweeping the controller.
        void Teleport(Vector3 position, Quaternion rotation);
    }
}
