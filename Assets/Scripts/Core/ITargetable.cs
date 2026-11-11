using UnityEngine;

namespace Ahoy.Core
{
    // Something monsters may attack besides Zahim: an ally, a villager. Monsters never cast to
    // a concrete AllyUnit; they ask BattleContext.Village for these.
    public interface ITargetable : IDamageable
    {
        Vector3 Position { get; }
    }
}
