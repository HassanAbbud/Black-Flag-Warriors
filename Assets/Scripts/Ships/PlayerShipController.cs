using System;
using Unity.VisualScripting;
using UnityEngine;

public class PlayerShipController : ShipController
{

    // Update is called once per frame
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Attack();
        }
        else if (Input.GetKey(KeyCode.W))
        {
            Move();
        }
        else if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.D))
        {
            Rotate();
        }
    }

    #region Ship Functions
    protected override void Attack()
    {
        //refactor when pool is implemented
        //Instantiate(shipBullet, transform.position + transform.forward, transform.rotation);
        PoolManager.Instance.GetObject(shipBullet.name, shipBullet, transform.position + transform.forward, transform.rotation);
    }

    protected override void Rotate()
    {
        transform.rotation *= Quaternion.AngleAxis(Input.GetAxis("Horizontal") * shipRotationSpeed * Time.deltaTime, Vector3.up);
    }
    #endregion
}
