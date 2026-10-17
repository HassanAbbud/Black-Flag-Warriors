using UnityEngine;

public abstract class ShipController : MonoBehaviour
{
    [SerializeField] protected float shipForwardSpeed = 10f;
    [SerializeField] protected float shipReverseSpeed = 5f;
    [SerializeField] protected float shipRotationSpeed = 100f;
    [SerializeField] protected int shipHealth = 100;
    [SerializeField] protected int shipAttack = 100;
    [SerializeField] protected int shipDefense = 100;
    [SerializeField] protected GameObject shipBullet;

    public float ShipForwardSpeed => shipForwardSpeed;
    public float ShipReverseSpeed => shipReverseSpeed;
    public float ShipRotationSpeed => shipRotationSpeed;
    public int ShipHealth => shipHealth;
    public int ShipAttack => shipAttack;
    public int ShipDefense => shipDefense;

}
