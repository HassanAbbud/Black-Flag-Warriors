using Ahoy.Core;
using System;
using System.Security.Cryptography.X509Certificates;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace Ahoy.Ships
{
    public class PlayerShipController : ShipController, IShipDamageable
    {
        [SerializeField] private Transform muzzlePoint;
        private Vector2 moveInput;
        private Vector3 moveInputZ;

        [SerializeField] private int playerAttackInherited = 100;
        private bool isAlive = true;
        public bool IsAlive => isAlive;

        // Update is called once per frame
        void Update()
        {
            if (moveInputZ.z >= 0)
                transform.Translate(shipForwardSpeed * Time.deltaTime * moveInputZ);
            else
                transform.Translate(shipReverseSpeed * Time.deltaTime * moveInputZ);

            transform.Rotate(Vector3.up, shipRotationSpeed * Time.deltaTime * moveInput.x);
        }

        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed)
            {
                GameObject go = PoolManager.Instance.GetObject(shipBullet.name, shipBullet, muzzlePoint.position, muzzlePoint.rotation);
                go.GetComponent<ShipBullet>().SetDamage(shipAttack + playerAttackInherited);
            }
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
            moveInputZ = new Vector3(0, 0, moveInput.y);
        }


        public void SetPlayerAttackInherited(int attack)
        {
            playerAttackInherited = attack;
        }

        public void TakeDamage(int damage)
        {
            if (isAlive)
            {
                shipHealth -= (damage - shipDefense);
                Debug.Log("Player ship took damage: " + damage + ", remaining health: " + shipHealth);
                if (shipHealth <= 0)
                {
                    isAlive = false;
                    // Handle ship destruction logic here
                    Debug.Log("Player ship destroyed!");
                }
            }
        }
    }
}

