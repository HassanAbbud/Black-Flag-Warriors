using UnityEngine;

namespace Ahoy.Core
{
    public class DestroyedTarget : MonoBehaviour
    {
        [SerializeField] private Faction targetFaction = Faction.Monsters;

        public void Die() => GameEvents.TargetDestroyed(targetFaction);
    }
}