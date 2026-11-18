using UnityEngine;

namespace Ahoy.Ships
{
    // Shows where the cannon is aimed: keeps an arrow beside the ship, pointing at the mouse.
    // Sits on the ship root. Whatever rotation the arrow has in the prefab, relative to the ship
    // (e.g. X 90 to lay a sprite flat, Z to turn its tip toward the bow), is kept as its rest pose;
    // the script only adds the turn toward the mouse. Set it up so the tip points at the bow.
    // It is placed in world space so it does not spin with the hull, and is hidden while sunk.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(PlayerShipController))]
    public sealed class PlayerShipAimArrow : MonoBehaviour
    {
        [Tooltip("The arrow object. Its +Z is the direction it points.")]
        [SerializeField] private Transform arrow;
        [Tooltip("Found in this ship's children automatically if left empty.")]
        [SerializeField] private PlayerShipCannonController cannon;

        private PlayerShipController ship;
        private PlayerShipDef def;
        private Quaternion restRotation;   // the arrow's prefab rotation relative to the ship

        private void Awake()
        {
            ship = GetComponent<PlayerShipController>();
            def = ship.Def;
            if (cannon == null)
                cannon = GetComponentInChildren<PlayerShipCannonController>();
            if (arrow != null)
                restRotation = Quaternion.Inverse(transform.rotation) * arrow.rotation;
        }

        private void OnEnable()
        {
            ship.Died += HandleDied;
            if (arrow != null)
                arrow.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            ship.Died -= HandleDied;
        }

        private void HandleDied()
        {
            if (arrow != null)
                arrow.gameObject.SetActive(false);
        }

        // LateUpdate: after the ship has moved and the cannon has read the mouse this frame.
        private void LateUpdate()
        {
            if (arrow == null || cannon == null || !ship.IsAlive)
                return;
            Vector3 direction = cannon.AimDirection;
            Vector3 origin = cannon.AimOrigin;
            origin.y = transform.position.y;   // height from the ship's pivot, so arrowHeight means the same on any ship
            Vector3 position = origin + direction * def.arrowDistance + Vector3.up * def.arrowHeight;
            arrow.SetPositionAndRotation(position, Quaternion.LookRotation(direction, Vector3.up) * restRotation);
        }
    }
}
