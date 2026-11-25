using UnityEngine;

public class BombPlacer : MonoBehaviour
{
    [SerializeField] private KeyCode placeKey = KeyCode.B;
    [SerializeField] private KeyCode gamepadKey = KeyCode.JoystickButton1;

    private void Update()
    {
        if (Input.GetKeyDown(placeKey) || Input.GetKeyDown(gamepadKey))
        {
            Bomb.Spawn(transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);
        }
    }
}