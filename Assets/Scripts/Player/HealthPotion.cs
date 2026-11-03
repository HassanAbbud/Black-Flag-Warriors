using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Player
{
    // Red potion: heals Zahim the moment he touches it, then disappears.
    [RequireComponent(typeof(Collider))]
    public class HealthPotion : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider other)
        {
            PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
            if (player == null || !player.IsAlive) return;

            player.Heal(balance.potionHealAmount);
            gameObject.SetActive(false);
        }
    }
}