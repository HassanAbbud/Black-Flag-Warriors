using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace InstantDestruction
{
    /// <summary>
    /// Manages the demo scene environment, including FPS-style camera control, projectile instantiation, and scene lifecycle.
    /// </summary>
    public class DemoSceneManager : MonoBehaviour
    {
        [Header("Ball Settings")]
        //public GameObject ballPrefab; // Reference to the ball prefab for instantiation
        public float launchForce = 20f; // Initial impulse magnitude when launched
        private float ballScale = 0.3f; // Local scale multiplier for the ball

        [Header("Camera Control Settings")]
        public float moveSpeed = 5f; // Horizontal and vertical travel speed
        public float lookSensitivity = 2f; // Sensitivity for mouse-based rotation
        private float rotationX = 0f; // Current horizontal rotation value
        private float rotationY = 0f; // Current vertical rotation value

        [SerializeField] private GameObject pumpkinPrefab;
        private int currentSpawnHeight = 1; // Tracks the Y coordinate for spawning pumpkin grids

        /// <summary>
        /// Initialization process. Locks and hides the mouse cursor.
        /// </summary>
        private void Start()
        {
            // Lock and hide the mouse cursor during execution (press ESC to release)
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            // Get initial rotation from the object's transform rotation set in Inspector
            Vector3 currentRotation = this.transform.localEulerAngles;
            this.rotationX = currentRotation.y;
            this.rotationY = currentRotation.x;

            // Normalize angles to the range [-180, 180] to match clamp and avoid sudden flipping
            if (this.rotationX > 180f) this.rotationX -= 360f;
            if (this.rotationY > 180f) this.rotationY -= 360f;
        }

        /// <summary>
        /// Update process per frame. Handles camera control, launching balls, scene reloading, etc.
        /// </summary>
        private void Update()
        {
            // 1. Camera rotation (Mouse input)
            this.HandleRotation();

            // 2. Camera movement (WASD input)
            this.HandleMovement();

            // 3. Launch ball (Space key)
            if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                this.LaunchBall();
            }

            // 4. Reload the current scene
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }

            if (Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var allDestructions = FindObjectsByType<BaseDestruction>(FindObjectsSortMode.None);
                
                foreach (var dest in allDestructions)
                {
                    dest.OnTrigger();
                }
                
                sw.Stop();
                Debug.Log($"<color=#4AF626>[InstantDestruction]</color> Triggered {allDestructions.Length} destructions in {sw.ElapsedMilliseconds} ms ({sw.ElapsedTicks} ticks)");
            }
            if (Keyboard.current != null && Keyboard.current.f3Key.wasPressedThisFrame)
            {
                this.SpawnPumpkinGrid();
            }

            // Feature to release the cursor via ESC key (for debugging)
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Left Click
            if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                this.FireWeapon();
            }

        }

        /// <summary>
        /// Handles camera rotation based on mouse input.
        /// </summary>
        private void HandleRotation()
        {
            if (Mouse.current != null)
            {
                // Multiply by a factor (e.g. 0.1f) because Mouse.delta in the new Input System 
                // is not scaled down like old Input.GetAxis
                this.rotationX += Mouse.current.delta.x.ReadValue() * 0.1f * this.lookSensitivity;
                this.rotationY -= Mouse.current.delta.y.ReadValue() * 0.1f * this.lookSensitivity;
            }
            
            // Clamp vertical rotation to prevent flipping at the poles
            this.rotationY = Mathf.Clamp(this.rotationY, -90f, 90f);

            this.transform.localRotation = Quaternion.Euler(this.rotationY, this.rotationX, 0);
        }

        /// <summary>
        /// Handles camera movement based on WASD input.
        /// </summary>
        private void HandleMovement()
        {
            float moveX = 0f;
            float moveZ = 0f;

            if (Keyboard.current != null)
            {
                if (Keyboard.current.dKey.isPressed) moveX += 1f;
                if (Keyboard.current.aKey.isPressed) moveX -= 1f;
                
                if (Keyboard.current.wKey.isPressed) moveZ += 1f;
                if (Keyboard.current.sKey.isPressed) moveZ -= 1f;
            }

            Vector3 move = this.transform.right * moveX + this.transform.forward * moveZ;
            this.transform.position += move * this.moveSpeed * Time.deltaTime;
        }

        /// <summary>
        /// Instantiates a ball object from the defined spawn position and launches it forward.
        /// </summary>
        private void LaunchBall()
        {
            GameObject ball;
            // if (this.ballPrefab != null)
            // {
            //     ball = Instantiate(this.ballPrefab);
            // }
            // else
            // {
            //     ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                
            //     // Reduce the default sphere size (e.g., 10cm diameter)
            //     ball.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            //     ball.AddComponent<Rigidbody>();
            // }

            ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            var render = ball.GetComponent<Renderer>();
            if (render != null)
            {
                Shader urpLitShader = Shader.Find("Universal Render Pipeline/Lit");
                if (urpLitShader != null)
                {
                    render.material = new Material(urpLitShader);
                }
            }

            // Reduce the default sphere size (e.g., 10cm diameter)
            //ball.transform.localScale = new Vector3(0.1f, 0.1f, 0.1f);
            ball.AddComponent<Rigidbody>();

            // Determine launch origin (defaults to camera position if spawnPoint is null)
            Transform origin = this.transform;
            ball.transform.position = origin.position;
            ball.transform.localScale = Vector3.one * this.ballScale;

            Rigidbody rb = ball.GetComponent<Rigidbody>();
            if (rb != null)
            {
                // Launch the ball directly forward from the camera's orientation
                rb.AddForce(this.transform.forward * this.launchForce, ForceMode.Impulse);
            }

            // Clean up the ball after 3 seconds
            Destroy(ball, 3f);
        }

        /// <summary>
        /// Executes a raycast from the center of the screen to trigger destruction on hit objects with a BaseDestruction component.
        /// </summary>
        private void FireWeapon()
        {
            // Generate a ray from the center of the viewport (crosshair position)
            Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
            
            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                var destruction = hit.collider.GetComponentInParent<BaseDestruction>();
                if (destruction != null)
                {
                    destruction.OnTrigger(hit.point);
                }

            }
        }

        /// <summary>
        /// Spawns a 10x10 grid of pumpkin prefabs. Stacks upwards with each subsequent call.
        /// </summary>
        private void SpawnPumpkinGrid()
        {
            if (this.pumpkinPrefab == null)
            {
                Debug.LogWarning("<color=#4AF626>[DemoSceneManager]</color> Pumpkin prefab is not assigned.");
                return;
            }

            for (int x = 0; x < 10; x++)
            {
                for (int z = 0; z < 10; z++)
                {
                    Vector3 position = new Vector3(x + 0.5f, this.currentSpawnHeight, z + 0.5f);
                    Instantiate(this.pumpkinPrefab, position, Quaternion.identity);
                }
            }

            this.currentSpawnHeight++;
        }
    }
}