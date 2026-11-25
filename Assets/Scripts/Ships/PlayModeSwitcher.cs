using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayMode = Ahoy.Core.PlayMode;

namespace Ahoy.Ships
{
    // Hands control between Zahim and the ship at a port (R39). Press Interact near a port to
    // sail, press it again near a port to step off. Lives on its own scene object, never on
    // Zahim, because Zahim's object is switched off while the ship is in use.
    //
    // Why Zahim is deactivated and the ship only has components disabled:
    //  - Both bodies carry a PlayerInput on the same action map, and the first one enabled owns
    //    the keyboard and gamepad. Turning one off before the other on is the whole fix.
    //  - Deactivating Zahim also stops his follow camera, so only one camera driver is live.
    //  - The ship stays visible and moored while Zahim walks, so its object stays active.
    public sealed class PlayModeSwitcher : MonoBehaviour
    {
        [SerializeField] private BalanceConfig balance;

        [Header("Zahim")]
        [Tooltip("Zahim's root object (the one with PlayerMotor and PlayerInput).")]
        [SerializeField] private GameObject zahim;

        [Header("Ship")]
        [Tooltip("The player ship's root object. Keep it active in the scene.")]
        [SerializeField] private GameObject ship;
        [Tooltip("The ship's PlayerInput.")]
        [SerializeField] private PlayerInput shipInput;
        [Tooltip("Everything on the ship that sails, aims or draws the aim arrow: PlayerShipController, PlayerShipCannonController, PlayerShipAimArrow.")]
        [SerializeField] private Behaviour[] shipDrivers;
        [Tooltip("The sea camera on the Main Camera. Starts disabled.")]
        [SerializeField] private CameraController shipCamera;

        [Header("Ports")]
        [SerializeField] private Port[] ports;

        // Observer: announces the new mode, never commands (Code Patterns 2.5).
        public event Action<PlayMode> ModeChanged;

        public PlayMode Mode { get; private set; } = PlayMode.OnFoot;
        // True while a press of Interact would switch right now. For a prompt HUD to read.
        public bool CanSwitch { get; private set; }

        private IPlayerMotor zahimMotor;
        private PlayerInput zahimInput;
        private InputAction interact;
        private Port nearPort;
        private float nextSwitchAt;

        private void Awake()
        {
            if (balance == null || zahim == null || ship == null || shipInput == null || shipCamera == null)
            {
                Debug.LogError($"{name}: Balance, Zahim, Ship, Ship Input and Ship Camera must all be assigned.", this);
                enabled = false;
                return;
            }

            zahimMotor = zahim.GetComponent<IPlayerMotor>();
            zahimInput = zahim.GetComponent<PlayerInput>();
            // The ship's own prefab may ship inactive; it must be live to sit at a berth.
            if (!ship.activeSelf) ship.SetActive(true);

            // One standalone action reads E / D-pad up whoever owns the devices, and works even
            // if the Input Actions asset has no Interact (unchecked: the asset was not available).
            interact = new InputAction("Interact", InputActionType.Button);
            interact.AddBinding("<Keyboard>/e");
            interact.AddBinding("<Gamepad>/dpad/up");
        }

        private void OnEnable() => interact?.Enable();
        private void OnDisable() => interact?.Disable();
        private void OnDestroy() => interact?.Dispose();

        private void Start()
        {
            // At scene load both PlayerInputs enable and the ship's may claim the devices first.
            // Park the ship, then cycle Zahim's PlayerInput so it pairs again.
            SetShipActive(false);
            if (zahimInput != null)
            {
                zahimInput.enabled = false;
                zahimInput.enabled = true;
            }
        }

        private void Update()
        {
            CanSwitch = TryFindPort(out nearPort);
            if (!CanSwitch || Time.unscaledTime < nextSwitchAt || !interact.WasPressedThisFrame()) return;

            nextSwitchAt = Time.unscaledTime + balance.portSwitchCooldown;
            if (Mode == PlayMode.OnFoot) BoardShip(nearPort);
            else Disembark(nearPort);
        }

        // On foot: Zahim must be near a port's landing. At sea: the ship near a port's berth.
        private bool TryFindPort(out Port found)
        {
            found = null;
            bool atSea = Mode == PlayMode.Ship;
            if (!atSea && zahimMotor != null && zahimMotor.IsDead) return false;

            Vector3 from = atSea ? ship.transform.position : zahim.transform.position;
            float range = atSea ? balance.portShipRange : balance.portZahimRange;
            float best = range * range;

            for (int i = 0; i < ports.Length; i++)
            {
                Port port = ports[i];
                Transform anchor = atSea ? port.Berth : port.Landing;
                if (anchor == null) continue;
                Vector3 d = anchor.position - from;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr > best) continue;
                best = sqr;
                found = port;
            }
            return found != null;
        }

        private void BoardShip(Port port)
        {
            zahim.SetActive(false);   // first: frees keyboard, gamepad and cursor, stops his camera
            ship.transform.SetPositionAndRotation(port.Berth.position, port.Berth.rotation);
            SetShipActive(true);
            shipCamera.SetTarget(ship.transform);   // snaps the sea camera onto the ship
            Mode = PlayMode.Ship;
            ModeChanged?.Invoke(Mode);
        }

        private void Disembark(Port port)
        {
            SetShipActive(false);   // first: the ship lets go of the devices
            ship.transform.SetPositionAndRotation(port.Berth.position, port.Berth.rotation);   // moors it
            // Placed while he is inactive, so the CharacterController cannot override the move.
            zahim.transform.SetPositionAndRotation(port.Landing.position, port.Landing.rotation);
            zahim.SetActive(true);
            Mode = PlayMode.OnFoot;
            ModeChanged?.Invoke(Mode);
        }

        private void SetShipActive(bool on)
        {
            shipInput.enabled = on;
            for (int i = 0; i < shipDrivers.Length; i++)
                if (shipDrivers[i] != null) shipDrivers[i].enabled = on;
            shipCamera.enabled = on;
        }
    }
}
