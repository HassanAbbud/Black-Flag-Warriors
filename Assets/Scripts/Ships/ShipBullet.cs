using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Ships
{
    public class ShipBullet : MonoBehaviour
    {
        [SerializeField] private int shipDamage;
        [SerializeField] private ShipCannonBall shipBullet;
        [SerializeField] private int totalDamage;
        private Rigidbody rigidbody;

        public int TotalDamage => totalDamage;

        private void Awake()
        {
            rigidbody = GetComponent<Rigidbody>();
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void OnEnable()
        {
            Invoke("RemoveMe", shipBullet.lifetime);
            totalDamage = shipDamage + shipBullet.damage;
            rigidbody.AddForce(transform.forward * shipBullet.addedForce, ForceMode.Impulse);
        }


        void RemoveMe()
        {
            CancelInvoke(nameof(RemoveMe));   // returned on a hit: don't let the lifetime timer return it again once reused
            rigidbody.linearVelocity = Vector3.zero;
            rigidbody.angularVelocity = Vector3.zero;
            PoolManager.Instance.ReturnObject(gameObject.name, gameObject);
        }

        public void SetDamage(int damage)
        {
            shipDamage = damage;
            totalDamage = shipDamage + shipBullet.damage;   // OnEnable ran before this was called
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.layer == LayerMask.NameToLayer("Player")) return;
            IShipDamageable ship = collision.gameObject.GetComponent<IShipDamageable>();
            if (ship != null && ship.IsAlive)
            {
                ship.TakeDamage(totalDamage);
                RemoveMe();
            }
        }
    }
}

