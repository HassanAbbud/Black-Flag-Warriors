using System;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

public class PlayerShipController : ShipController
{
    private Vector2 moveInput;
    private Vector3 moveInputZ;

    // Update is called once per frame
    void Update()
    {
        if(moveInputZ.z >= 0)
            transform.Translate(shipForwardSpeed * Time.deltaTime * moveInputZ);
        else
            transform.Translate(shipReverseSpeed * Time.deltaTime * moveInputZ);
        
        transform.Rotate(Vector3.up, shipRotationSpeed * Time.deltaTime * moveInput.x);
    }

    public void OnAttack(InputAction.CallbackContext context)
    {
        if (context.performed)
        {
            PoolManager.Instance.GetObject(shipBullet.name, shipBullet, transform.position + transform.forward, transform.rotation);
        }
    }

    public void OnMove(InputAction.CallbackContext context)
    {
        moveInput = context.ReadValue<Vector2>();
        moveInputZ = new Vector3(0, 0, moveInput.y);
    }

}
