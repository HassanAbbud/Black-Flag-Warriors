using UnityEngine;

public abstract class ShipController : MonoBehaviour
{
    [SerializeField] protected float shipSpeed = 10f;
    [SerializeField] protected float shipRotationSpeed = 100f;
    [SerializeField] protected int shipHealth = 100;
    [SerializeField] protected int shipAttack = 100;
    [SerializeField] protected int shipDefense = 100;
    [SerializeField] protected GameObject shipBullet;

    public float ShipSpeed => shipSpeed;
    public float ShipRotationSpeed => shipRotationSpeed;
    public int ShipHealth => shipHealth;
    public int ShipAttack => shipAttack;
    public int ShipDefense => shipDefense;


    protected abstract void Attack();
    protected abstract void Rotate();
    protected virtual void Move()
    {
        transform.Translate(Vector3.forward * shipSpeed * Time.deltaTime);
    }

}
