using UnityEngine;

[CreateAssetMenu(fileName = "ShipCannonBall", menuName = "Scriptable Objects/ShipCannonBall")]
public class ShipCannonBall : ScriptableObject
{
    public int damage = 10;
    public int lifetime = 5;
    public float addedForce = 5f;
  
}
