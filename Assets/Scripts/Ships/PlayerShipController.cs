using Ahoy.Core;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Ships
{
    // Sails the player ship and takes its damage. Aiming and firing are PlayerShipCannonController's
    // job; this class only hands it the input. WASD is relative to the camera: W sails up the
    // screen, D to the right, and the ship turns toward that heading at turnSpeed.
    // PlayerInput (Invoke Unity Events) calls OnMove, OnAim and OnAttack.
    [DisallowMultipleComponent]
    public sealed class PlayerShipController : ShipController, IShipDamageable
    {
        // State: one enum, one switch, every transition through SetState.
        public enum ShipState { Sailing, Dead }

        [SerializeField] private PlayerShipDef def;
        [Tooltip("Aims and fires. Found in this ship's children automatically if left empty.")]
        [SerializeField] private PlayerShipCannonController cannon;
        [Tooltip("Camera that WASD is relative to. Uses the MainCamera-tagged camera if left empty.")]
        [SerializeField] private Transform view;

        [SerializeField] private int playerAttackInherited = 100;

        // Observer: local news, e.g. for a HUD or respawn listener. Announces, never commands.
        public event Action Died;

        public PlayerShipDef Def => def;
        public ShipState State => state;
        public bool IsAlive => state != ShipState.Dead;
        public Faction Faction => Faction.Village;
        public GameObject Cannonball => shipBullet;
        public int CannonDamage => shipAttack + playerAttackInherited;

        private ShipState state;
        private Vector2 moveInput;
        private float speed;
        private int maxHealth;

        // Own components are resolved once here, never per frame.
        private void Awake()
        {
            if (cannon == null)
                cannon = GetComponentInChildren<PlayerShipCannonController>();
            if (view == null && Camera.main != null)
                view = Camera.main.transform;
            maxHealth = shipHealth;
        }

        // A respawned ship resets its own state here.
        private void OnEnable()
        {
            shipHealth = maxHealth;
            speed = 0f;
            moveInput = Vector2.zero;
            SetState(ShipState.Sailing);
        }

        private void Update()
        {
            switch (state)
            {
                case ShipState.Sailing:
                    Sail();
                    break;
                case ShipState.Dead:
                    break;   // no steering while it sinks
            }
        }

        private void SetState(ShipState next)
        {
            state = next;
            switch (next)
            {
                case ShipState.Sailing:
                    break;
                case ShipState.Dead:
                    speed = 0f;
                    Died?.Invoke();
                    break;
            }
        }

        private void Sail()
        {
            float dt = Time.deltaTime;
            Vector3 heading = CameraRelative(moveInput);
            float throttle = Mathf.Clamp01(heading.magnitude);
            float targetSpeed = 0f;

            if (throttle > def.inputDeadZone)
            {
                Quaternion wanted = Quaternion.LookRotation(heading);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, wanted, def.turnSpeed * dt);
                float turning = Quaternion.Angle(transform.rotation, wanted) / 180f;   // 0..1
                targetSpeed = def.maxSpeed * throttle * Mathf.Lerp(1f, def.turnSlowdown, turning);
            }

            float rate = targetSpeed > speed ? def.acceleration : def.deceleration;
            speed = Mathf.MoveTowards(speed, targetSpeed, rate * dt);
            transform.position += transform.forward * (speed * dt);
        }

        // Turns screen-space input into a flat world direction using the camera's heading.
        private Vector3 CameraRelative(Vector2 input)
        {
            if (view == null)
                return new Vector3(input.x, 0f, input.y);
            Vector3 forward = view.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                forward = view.up;   // camera looking straight down: its up is "up the screen"
            forward.y = 0f;
            forward.Normalize();
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            return right * input.x + forward * input.y;
        }

        public void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        // Command: the press becomes an intent; the cannon fires it when it is loaded.
        public void OnAttack(InputAction.CallbackContext context)
        {
            if (context.performed && cannon != null)
                cannon.BufferFire();
        }

        public void OnAim(InputAction.CallbackContext context)
        {
            if (cannon != null)
                cannon.SetAimScreenPoint(context.ReadValue<Vector2>());
        }

        public void SetPlayerAttackInherited(int attack)
        {
            playerAttackInherited = attack;
        }

        public void TakeDamage(int damage)
        {
            if (!IsAlive)
                return;
            shipHealth -= Mathf.Max(damage - shipDefense, 1);   // a weak hit still scratches, never heals
            if (shipHealth <= 0)
                SetState(ShipState.Dead);
        }
    }
}
