
using UnityEngine;
using UnityEngine.InputSystem;

public class BombPlacer : MonoBehaviour
{
    [SerializeField]
    private InputAction placeAction = new InputAction(
        "PlaceBomb", InputActionType.Button);

    private void Awake()
    {
        placeAction.AddBinding("<Keyboard>/b");
        placeAction.AddBinding("<Gamepad>/buttonEast");
    }

    private void OnEnable()
    {
        placeAction.performed += OnPlace;
        placeAction.Enable();
    }

    private void OnDisable()
    {
        placeAction.performed -= OnPlace;
        placeAction.Disable();
    }

    private void OnPlace(InputAction.CallbackContext ctx)
    {
        Bomb.Spawn(transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);
    }
}


//using UnityEngine;

//public class BombPlacer : MonoBehaviour
//{
//    [SerializeField] private KeyCode placeKey = KeyCode.B;
//    [SerializeField] private KeyCode gamepadKey = KeyCode.JoystickButton1;

//    private void Update()
//    {
//        if (Input.GetKeyDown(placeKey) || Input.GetKeyDown(gamepadKey))
//        {
//            Bomb.Spawn(transform.position + transform.forward * 1.5f + Vector3.up * 0.5f);
//        }
//    }
//}