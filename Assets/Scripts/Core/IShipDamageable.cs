using UnityEngine;

namespace Ahoy.Core
{
    public interface IShipDamageable
    {
        bool IsAlive { get; }
        void TakeDamage(int damage);
    }
}
