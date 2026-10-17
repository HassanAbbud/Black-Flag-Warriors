using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public Transform targetPlayer;

    public float cameraDistance = 10;
    public float offsetHeight = 30;
    public float cameraAngle = 35;
    public float minDistance = 10;
    public float maxDistance = 30;
    public float zoomSpeed = 1;

    private Vector3 targetHeader;
    private PlayerInput input;

    void Start()
    {
        input = targetPlayer.gameObject.GetComponent<PlayerInput>();
    }
    void LateUpdate()
    {
        if(input.currentControlScheme == "Keyboard&Mouse" && input.actions["Zoom"].ReadValue<float>() != 0)
        {
            cameraDistance -= input.actions["Zoom"].ReadValue<float>() * zoomSpeed;
            cameraDistance = Mathf.Clamp(cameraDistance, minDistance, maxDistance);
        }

        targetHeader = targetPlayer.position + targetPlayer.up * offsetHeight;
        this.transform.position = Quaternion.AngleAxis(cameraAngle,targetPlayer.right) * -targetPlayer.forward * cameraDistance + targetHeader;
        this.transform.rotation = Quaternion.LookRotation(targetHeader - this.transform.position);
    }
}
