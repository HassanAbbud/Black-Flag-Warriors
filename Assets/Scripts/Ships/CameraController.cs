using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Ships
{
    public class CameraController : MonoBehaviour
    {
        [SerializeField] private Transform target;

        [SerializeField] private Vector3 offset = new Vector3(0f, 2f, -1f);

        void LateUpdate()
        {
            if (target == null) return;
            transform.position = target.position + target.rotation * offset;
            transform.rotation = target.rotation;
        }
    }
}


