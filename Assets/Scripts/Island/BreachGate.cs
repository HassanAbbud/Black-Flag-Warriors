using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // Keeps the final breach's camp out of the battle until the simulation says enough portals are
    // sealed (GDD Victory). Listens through IBattleProgress and only switches on its own child.
    public sealed class BreachGate : MonoBehaviour
    {
        const float CheckSeconds = 0.5f;

        [Tooltip("The battle location of the final breach.")]
        [SerializeField] int locationId = 9;
        [Tooltip("Off until the breach opens: the final portal camp and anything that belongs with it.")]
        [SerializeField] GameObject gated;

        float nextCheckAt;

        void Awake()
        {
            Debug.Assert(gated != null, "BreachGate needs the object it keeps closed", this);
            if (gated != null) gated.SetActive(false);
        }

        void Update()
        {
            if (gated == null || gated.activeSelf || Time.time < nextCheckAt) return;
            nextCheckAt = Time.time + CheckSeconds;
            if (!BattleContext.Progress.TryGetLocation(locationId, out BattleLocationStatus status) || status.Locked) return;
            Debug.Log($"{name}: the great breach is open", this);
            gated.SetActive(true);
        }
    }
}
