using Ahoy.Core;
using UnityEngine;

public class DestroyedTarget: MonoBehaviour
{
    [SerializeField] private Faction targetFaction = Faction.Monsters;

    public void Die()
    {
        GameEvents.TargetDestroyed(targetFaction);
        Destroy(gameObject);
    }
}
