using UnityEngine;

public class BombPlacer : MonoBehaviour
{
    [SerializeField] private KeyCode placeKey = KeyCode.B;

    private void Update()
    {
        if (Input.GetKeyDown(placeKey))
        {
            Bomb.Spawn(transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);
        }
    }
}