using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.Rendering;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection;

namespace InstantDestruction
{
    /// <summary>
    /// Editor window for configuring parameters and running physics simulations of destruction effects.
    /// Manages an isolated preview scene to visualize the mesh fragmentation and compute shader behavior.
    /// </summary>
    public class DestructionSettingsWindow : EditorWindow
    {
        private static readonly string[] UpdateTips = new string[]
        {
            "Update: Released version 1.0.0 (Added preview window)"
        };

        private static readonly string[] BasicTips = new string[]
        {
            "Info: RenderMeshIndirect is used to achieve high-performance draw calls without GameObject overhead.",
            "Info: Set 'After Destruction Mode' to 'Reset' to reuse the destructible object.",
            "Info: The simulation warms up shaders for the first 120 frames to avoid compilation spikes.",
            "Info: Particle scale is dynamically calculated based on the mesh size."
        };

        private static readonly string[] ControlTips = new string[]
        {
            "Controls: Left-click and drag in the viewport to rotate the preview camera.",
            "Controls: Scroll wheel in the viewport to zoom the camera in and out.",
            "Controls: Right-click on the preview mesh to trigger a custom impact simulation at that point.",
            "Controls: Click 'Simulate' to start the physics simulation from the center."
        };

        /// <summary>
        /// Selects a random tip based on category weights (20% Update, 60% Basic, 20% Controls).
        /// </summary>
        private static string GetRandomInfoTip()
        {
            float rand = Random.value;
            if (rand < 0.20f)
            {
                return UpdateTips[Random.Range(0, UpdateTips.Length)];
            }
            else if (rand < 0.80f) // 0.20 + 0.60
            {
                return BasicTips[Random.Range(0, BasicTips.Length)];
            }
            else
            {
                return ControlTips[Random.Range(0, ControlTips.Length)];
            }
        }

        #region Properties and Variables
        // ==================================================
        // Properties and Variables
        // ==================================================
        private BaseDestruction targetComponent;            // Target destruction component to preview
        private SerializedObject serializedTarget;          // Serialized representation of target component
        
        private VisualElement root;                         // Root visual element of UI Toolkit
        private VisualElement previewViewport;              // Container viewport element for preview rendering
        private Label previewPlaceholder;                   // Placeholder label shown when no target is active
        private Button simulateButton;                      // Button to trigger simulation start
        private Button resetButton;                         // Button to reset simulation state
        private ScrollView contentScroll;                   // Scroll view container for properties
        private Label infoText;                             // Label displaying random information and tips
        
        // Preview rendering utilities
        private PreviewRenderUtility previewUtility;       // Utility helper managing the isolated rendering scene
        private GameObject previewDummyGO;                  // Instantiated copy of the target mesh for rendering
        private static readonly Vector3 DummyPosition = new Vector3(0f, -2000f, 0f); // Fixed coordinates to isolate the preview dummy
        
        // Camera Orbit Rotation State
        private float cameraYaw = 45f;                      // Yaw rotation angle of orbit camera
        private float cameraPitch = 20f;                    // Pitch rotation angle of orbit camera
        private float cameraDistance = 5;                   // Distance from the target pivot point
        private Vector3 cameraTargetOffset = Vector3.zero;  // Target offset pivot calculation variable
        private bool isDragging = false;                    // Tracking status of mouse drag rotation
        
        // Simulation State
        private float lastTime;                             // Cache to store the previous tick timestamp
        private float simulatedTime = 0f;                   // Total elapsed time since simulation started
        private bool isSimulating = false;                  // Simulation running status flag
        
        // Custom Impact Simulation State
        private Vector3 customImpactCenter = Vector3.zero;  // Clicked point on mesh in world coordinates
        private Vector3 customImpactVelocity = Vector3.zero;// Direction of raycast
        private bool useCustomImpact = false;               // True when triggered by right-click raycast

        // Effect State
        private ParticleSystem previewParticleSystem;       // Instantiated particle system for preview
        private AudioSource previewAudioSource;             // Instantiated audio source for preview
        #endregion
        
        /// <summary>
        /// Menu Item entry point to open standard settings window.
        /// </summary>
        [MenuItem("Window/Instant Destruction/Settings")]
        public static void OpenWindow()
        {
            var window = GetWindow<DestructionSettingsWindow>();
            window.titleContent = new GUIContent("Destruction Settings", EditorGUIUtility.IconContent("d_Settings").image);
            window.minSize = new Vector2(500, 350);
            window.Show();
        }

        /// <summary>
        /// Opens the settings window and binds a specific target component immediately.
        /// </summary>
        /// <param name="target">The target BaseDestruction component to edit and preview.</param>
        public static void ShowWindow(BaseDestruction target)
        {
            var window = GetWindow<DestructionSettingsWindow>();
            window.titleContent = new GUIContent("Destruction Settings", EditorGUIUtility.IconContent("d_Settings").image);
            window.minSize = new Vector2(500, 350);
            window.SetTarget(target);
            window.Show();
        }

        /// <summary>
        /// Lifecycle callback when the editor window is enabled.
        /// Subscribes to selection change events.
        /// </summary>
        private void OnEnable()
        {
            // Subscribe to selection change events
            Selection.selectionChanged += OnSelectionChanged;
        }

        /// <summary>
        /// Lifecycle callback when the editor window is disabled/closed.
        /// Unsubscribes from events and releases isolated preview scene resources.
        /// </summary>
        private void OnDisable()
        {
            Selection.selectionChanged -= OnSelectionChanged;
            
            StopSimulation();
            DestroyPreviewParticle();
            StopAudioClipInEditor();
            DestroyPreviewDummyObject();
            
            if (targetComponent != null)
            {
                targetComponent.CleanUpBuffers();
            }

            // Fully release internal camera, target textures, and scenes securely
            if (previewUtility != null)
            {
                previewUtility.Cleanup();
                previewUtility = null;
            }
        }

        /// <summary>
        /// Initializes the UI Toolkit elements, loads layout assets, and registers callbacks.
        /// </summary>
        private void CreateGUI()
        {
            root = rootVisualElement;

            // Load UXML layout and USS style sheet
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/Rebush/InstantDestruction/Editor/UI/DestructionSettingsWindow.uxml");
            var uss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Assets/Rebush/InstantDestruction/Editor/UI/DestructionSettingsWindow.uss");

            if (uxml != null)
            {
                uxml.CloneTree(root);
            }
            else
            {
                root.Add(new Label("Failed to load UXML layout. Please check the file path."));
                return;
            }

            if (uss != null)
            {
                root.styleSheets.Add(uss);
            }

            // Bind UI elements from UXML
            previewViewport = root.Q<VisualElement>("preview-viewport");
            previewPlaceholder = root.Q<Label>("preview-placeholder");
            simulateButton = root.Q<Button>("simulate-button");
            resetButton = root.Q<Button>("reset-button");
            contentScroll = root.Q<ScrollView>("content-scroll");
            infoText = root.Q<Label>("info-text");

            if (infoText != null)
            {
                infoText.text = GetRandomInfoTip();
            }

            // Register button click events
            if (simulateButton != null) simulateButton.clicked += OnSimulateButtonClicked;
            if (resetButton != null) resetButton.clicked += ResetSimulation;

            // Register geometry changed event to resize RenderTexture dynamically
            if (previewViewport != null)
            {
                previewViewport.RegisterCallback<GeometryChangedEvent>(OnViewportGeometryChanged);
                
                // Register pointer events for camera orbit manipulation
                previewViewport.RegisterCallback<PointerDownEvent>(OnPointerDown);
                previewViewport.RegisterCallback<PointerMoveEvent>(OnPointerMove);
                previewViewport.RegisterCallback<PointerUpEvent>(OnPointerUp);
                previewViewport.RegisterCallback<PointerLeaveEvent>(OnPointerLeave);
                previewViewport.RegisterCallback<WheelEvent>(OnScrollWheel);
            }

            // Initialize target with current selection
            if (targetComponent == null)
            {
                OnSelectionChanged();
            }
            else
            {
                BindTarget();
            }
        }

        /// <summary>
        /// Targets a specific component for editing and precalculates its data structures.
        /// </summary>
        /// <param name="target">The targeted component.</param>
        private void SetTarget(BaseDestruction target)
        {
            ResetSimulation();
            targetComponent = target;
            BindTarget();
            
            if (targetComponent != null)
            {
                targetComponent.PrecalculateDestructionData();
            }
            
            InitializePreviewScene();
            CreatePreviewDummyObject();
            
            if (previewPlaceholder != null)
            {
                previewPlaceholder.style.display = DisplayStyle.None;
            }
            
            RenderPreview();
        }

        /// <summary>
        /// Event callback triggered when the active inspector selection changes.
        /// </summary>
        private void OnSelectionChanged()
        {
            // Check if the newly selected GameObject has a BaseDestruction component
            var activeGO = Selection.activeGameObject;
            if (activeGO != null)
            {
                var destruction = activeGO.GetComponent<BaseDestruction>();
                if (destruction != null)
                {
                    SetTarget(destruction);
                    return;
                }
            }
            
            // Clear binding and hide viewport contents if selection is lost
            if (!isSimulating)
            {
                targetComponent = null;
                serializedTarget = null;
                root.Unbind();
                if (contentScroll != null) contentScroll.style.display = DisplayStyle.None;
                if (previewPlaceholder != null) previewPlaceholder.style.display = DisplayStyle.Flex;
                
                DestroyPreviewDummyObject();
                RenderPreview();
            }
        }

        /// <summary>
        /// Binds the UI Toolkit properties container to the serialized component properties.
        /// </summary>
        private void BindTarget()
        {
            if (targetComponent == null || root == null) return;

            serializedTarget = new SerializedObject(targetComponent);
            root.Bind(serializedTarget);

            if (contentScroll != null)
            {
                contentScroll.style.display = DisplayStyle.Flex;
            }
        }

        #region Viewport & Rendering Pipeline
        /// <summary>
        /// Resizes preview viewport render textures dynamically when geometry dimensions change.
        /// </summary>
        private void OnViewportGeometryChanged(GeometryChangedEvent evt)
        {
            var width = Mathf.Max(32, (int)evt.newRect.width);
            var height = Mathf.Max(32, (int)evt.newRect.height);
            
            RenderPreview();
        }

        /// <summary>
        /// Configures the isolated preview scene camera, lights, and color states.
        /// </summary>
        private void InitializePreviewScene()
        {
            if (previewUtility == null)
            {
                // Instantiates an SRP-compatible decoupled preview scene cleanly
                previewUtility = new PreviewRenderUtility();
                
                // Configure the built-in preview camera fields directly
                previewUtility.camera.nearClipPlane = 0.05f;
                previewUtility.camera.farClipPlane = 1000f;
                previewUtility.camera.fieldOfView = 40f;
                previewUtility.camera.clearFlags = CameraClearFlags.Color;
                previewUtility.camera.backgroundColor = new Color(0.12f, 0.12f, 0.12f, 1f);

                // Setup the internal default lights automatically
                previewUtility.lights[0].intensity = 2.0f;
                previewUtility.lights[0].transform.rotation = Quaternion.Euler(30f, 30f, 0f);
            }
        }

        /// <summary>
        /// Recalculates coordinates and updates the orbit camera look-at position.
        /// </summary>
        private void UpdateCameraPosition()
        {
            if (previewUtility == null || previewUtility.camera == null) return;

            var pitchRad = cameraPitch * Mathf.Deg2Rad;
            var yawRad = cameraYaw * Mathf.Deg2Rad;

            var direction = new Vector3(
                Mathf.Cos(pitchRad) * Mathf.Sin(yawRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(pitchRad) * Mathf.Cos(yawRad)
            );

            // FORCE target directly to the true origin context for dynamic rendering verification
            var targetPos = DummyPosition;
            
            // Apply coordinates directly to the official utility camera component instance
            previewUtility.camera.transform.position = targetPos - direction * cameraDistance;
            previewUtility.camera.transform.LookAt(targetPos);
            
            // Align the default utility light source position dynamically tracking along the camera path
            if (previewUtility.lights != null && previewUtility.lights.Length > 0 && previewUtility.lights[0] != null)
            {
                var camTransform = previewUtility.camera.transform;
                previewUtility.lights[0].transform.position = camTransform.position + camTransform.up * 2f + camTransform.right * 2f;
            }
        }

        /// <summary>
        /// Creates a temporary dummy copy of the target component for static layout preview rendering.
        /// </summary>
        private void CreatePreviewDummyObject()
        {
            DestroyPreviewDummyObject();

            if (targetComponent == null) return;

            var targetMeshFilter = targetComponent.GetComponent<MeshFilter>();
            var targetRenderer = targetComponent.GetComponent<Renderer>();

            if (targetMeshFilter == null || targetMeshFilter.sharedMesh == null || targetRenderer == null)
                return;

            // Instantiate dynamic preview dummy at isolated coordinates
            previewDummyGO = new GameObject("PreviewDummy") { hideFlags = HideFlags.HideAndDontSave };
            previewDummyGO.transform.position = DummyPosition;
            previewDummyGO.transform.rotation = targetComponent.transform.rotation;
            previewDummyGO.transform.localScale = targetComponent.transform.lossyScale;

            var filter = previewDummyGO.AddComponent<MeshFilter>();
            filter.sharedMesh = targetMeshFilter.sharedMesh;

            var renderer = previewDummyGO.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = targetRenderer.sharedMaterials;

            var collider = previewDummyGO.AddComponent<MeshCollider>();
            collider.sharedMesh = targetMeshFilter.sharedMesh;

            // Frame camera around target boundaries
            var bounds = targetMeshFilter.sharedMesh.bounds;
            cameraTargetOffset = bounds.center;
            
            var scale = targetComponent.transform.lossyScale;
            var worldSize = Vector3.Scale(bounds.size, scale);
            var maxDim = Mathf.Max(worldSize.x, worldSize.y, worldSize.z);
            cameraDistance = Mathf.Max(1.5f, maxDim * 5.0f);
            
            UpdateCameraPosition();
        }

        /// <summary>
        /// Clears and safely destroys the active preview dummy GameObject.
        /// </summary>
        private void DestroyPreviewDummyObject()
        {
            if (previewDummyGO != null)
            {
                DestroyImmediate(previewDummyGO);
                previewDummyGO = null;
            }
        }

        /// <summary>
        /// Main render cycle to draw both the static dummy preview and active GPU-calculated shards.
        /// </summary>
        private void RenderPreview()
        {
            InitializePreviewScene();

            // Fetch layout dimensions safely from the UI Toolkit viewport component
            Rect renderRect = previewViewport != null ? previewViewport.layout : new Rect(0, 0, 256, 256);
            if (float.IsNaN(renderRect.width) || renderRect.width <= 0) renderRect.width = 256;
            if (float.IsNaN(renderRect.height) || renderRect.height <= 0) renderRect.height = 256;

            // Begin the official editor render pass allocation context
            previewUtility.BeginPreview(renderRect, GUIStyle.none);

            // Synchronize the utility camera positioning with orbit calculations
            UpdateCameraPosition(); 

            if (!isSimulating && previewDummyGO != null)
            {
                previewUtility.AddSingleGO(previewDummyGO);
            }

            if (isSimulating && previewParticleSystem != null)
            {
                previewUtility.AddSingleGO(previewParticleSystem.gameObject);
            }

            if (isSimulating && targetComponent != null && targetComponent.EmptyMeshes != null)
            {
               for (int i = 0; i < targetComponent.EmptyMeshes.Length; i++)
                {
                    if (targetComponent.EmptyMeshes[i] == null || targetComponent.ArgsBuffers[i] == null) continue;
                    // Copy RenderParams and assign the preview camera
                    RenderParams rp = targetComponent.RenderParamsList[i];
                    rp.camera = previewUtility.camera;
                    Graphics.RenderMeshIndirect(rp, targetComponent.EmptyMeshes[i], targetComponent.ArgsBuffers[i]);
                }
            }

            // Render the isolated scene view cleanly via the SRP-supported utility channel
            previewUtility.camera.Render();

            // End the preview pass and extract the resulting texture directly into the UI Toolkit layout background
            Texture renderedResult = previewUtility.EndPreview();
            
            if (previewViewport != null && renderedResult != null)
            {
                // Route explicitly into UI Toolkit's exact built-in structure APIs based on type evaluation
                if (renderedResult is RenderTexture renderTex)
                {
                    previewViewport.style.backgroundImage = Background.FromRenderTexture(renderTex);
                }
                else if (renderedResult is Texture2D tex2D)
                {
                    previewViewport.style.backgroundImage = Background.FromTexture2D(tex2D);
                }
            }
        }

        #endregion

        #region Pointer Events for Viewport Interaction
        /// <summary>
        /// Pointer event callback when clicking on viewport container. Starts orbit drag process.
        /// </summary>
        private void OnPointerDown(PointerDownEvent evt)
        {
            if (evt.button == 1 && previewUtility != null && previewDummyGO != null)
            {
                // Right click: Calculate custom impact point on mesh collider
                Vector2 localMousePos = evt.localPosition;
                Rect viewportRect = previewViewport.layout;
                
                // Convert UI elements layout position to normalized viewport coordinates for camera raycast
                Vector2 normalizedPos = new Vector2(
                    localMousePos.x / viewportRect.width,
                    1.0f - (localMousePos.y / viewportRect.height)
                );
                
                Ray ray = previewUtility.camera.ViewportPointToRay(normalizedPos);
                if (previewDummyGO.TryGetComponent<MeshCollider>(out var collider) && collider.Raycast(ray, out RaycastHit hit, 1000f))
                {
                    customImpactCenter = hit.point;
                    customImpactVelocity = ray.direction * serializedTarget.FindProperty("explosionSpeed").floatValue;
                    useCustomImpact = true;
                    
                    // Trigger simulation with custom coordinates
                    StartSimulation();
                }
                
                evt.StopPropagation();
                return;
            }

            if (evt.button != 0 || previewUtility == null) return;
            
            previewViewport.CapturePointer(evt.pointerId);
            isDragging = true;
            evt.StopPropagation();
        }

        /// <summary>
        /// Pointer event callback when dragging inside viewport. Rotates orbit camera relative to drag speed.
        /// </summary>
        private void OnPointerMove(PointerMoveEvent evt)
        {
            if (!isDragging || previewUtility == null) return;

            // Update rotation values from pointer drag offsets cleanly
            cameraYaw += evt.deltaPosition.x * 0.5f;
            cameraPitch -= evt.deltaPosition.y * 0.5f;
            cameraPitch = Mathf.Clamp(cameraPitch, -80f, 80f);

            UpdateCameraPosition();
            RenderPreview();
            evt.StopPropagation();
        }

        /// <summary>
        /// Pointer event callback when releasing click. Ends orbit drag process.
        /// </summary>
        private void OnPointerUp(PointerUpEvent evt)
        {
            if (!isDragging) return;
            
            previewViewport.ReleasePointer(evt.pointerId);
            isDragging = false;
            evt.StopPropagation();
        }

        /// <summary>
        /// Pointer event callback when mouse leaves viewport. Releases click focus cleanly.
        /// </summary>
        private void OnPointerLeave(PointerLeaveEvent evt)
        {
            if (isDragging)
            {
                previewViewport.ReleasePointer(evt.pointerId);
                isDragging = false;
            }
        }

        /// <summary>
        /// Scroll wheel event callback. Zooms camera in and out of the pivot point.
        /// </summary>
        private void OnScrollWheel(WheelEvent evt)
        {
            if (previewUtility == null) return;

            // Zoom camera in/out exponentially based on current distance limits
            cameraDistance += evt.delta.y * 0.5f * (cameraDistance * 0.2f);
            cameraDistance = Mathf.Clamp(cameraDistance, 0.5f, 50f);

            UpdateCameraPosition();
            RenderPreview();
            evt.StopPropagation();
        }
        #endregion

        #region Simulation Controller
        /// <summary>
        /// Configures Compute Shader buffers, updates parameters, and dispatches the initial kernel.
        /// </summary>
        /// <returns>True if computation set up succeeded.</returns>
        private bool SetupPreviewSimulationData()
        {
            if (targetComponent == null) return false;

            // Release old buffers to prevent memory leaks before re-calculating
            targetComponent.CleanUpBuffers();

            // Re-calculate and reset fragment data (and randomized parameters) before starting simulation
            targetComponent.PrecalculateDestructionData();

            // Guard check to ensure that the buffers were allocated correctly
            if (targetComponent.FragmentBuffers == null || targetComponent.FragmentBuffers.Length == 0)
            {
                return false;
            }

            // Reference the newly exposed public compute shader directly from the target instance
            ComputeShader computeShader = targetComponent.instantDestructionCS;

            if (computeShader != null)
            {
                var localToWorld = Matrix4x4.TRS(DummyPosition, targetComponent.transform.rotation, targetComponent.transform.lossyScale);
                
                serializedTarget.Update();
                float speed = serializedTarget.FindProperty("explosionSpeed").floatValue;
                float strength = serializedTarget.FindProperty("impactStrength").floatValue;

                int kernelIndex = computeShader.FindKernel("CSMain");

                // Bind initial global uniform properties for the setup phase using direct reference
                computeShader.SetFloat("_DeltaTime", 0.0f);
                computeShader.SetFloat("_ExplosionSpeed", speed);
                computeShader.SetMatrix("_CustomLocalToWorld", localToWorld);
                
                if (useCustomImpact)
                {
                    computeShader.SetVector("_ExplosionCenter", customImpactCenter);
                    computeShader.SetVector("_ImpactVelocity", customImpactVelocity);
                }
                else
                {
                    computeShader.SetVector("_ExplosionCenter", DummyPosition);
                    computeShader.SetVector("_ImpactVelocity", Vector3.zero);
                }
                computeShader.SetFloat("_ImpactStrength", strength);

                int subMeshCount = targetComponent.FragmentBuffers.Length;
                for (int i = 0; i < subMeshCount; i++)
                {
                    GraphicsBuffer graphicsBuffer = targetComponent.FragmentBuffers[i];
                    
                    if (graphicsBuffer == null || targetComponent.ThreadGroupsList == null || targetComponent.ThreadGroupsList.Length <= i)
                    {
                        continue;
                    }

                    // Bind buffer resources and perform a single initial compute dispatch pass
                    computeShader.SetBuffer(kernelIndex, "_FragmentBuffer", graphicsBuffer);
                    computeShader.Dispatch(kernelIndex, targetComponent.ThreadGroupsList[i], 1, 1);

                }
            }

            return true;
        }

        /// <summary>
        /// Starts simulation mode, hiding the dummy mesh, dispatching CS, and subscribing to editor updates.
        /// </summary>
        private void StartSimulation()
        {
            if (targetComponent == null) return;
            
            serializedTarget.Update();

            // Hide standard renderer of static object preview
            if (previewDummyGO != null)
            {
                var r = previewDummyGO.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = false;
            }

            if (!SetupPreviewSimulationData())
            {
                ResetSimulation();
                return;
            }

            isSimulating = true;
            simulatedTime = 0f;
            lastTime = (float)EditorApplication.timeSinceStartup;

            // Set matrix bounds and start times on shaders
            var localToWorld = Matrix4x4.TRS(DummyPosition, targetComponent.transform.rotation, targetComponent.transform.lossyScale);
            int subMeshCount = targetComponent.TargetMaterials.Length;
                float currentTime = (float)EditorApplication.timeSinceStartup;

                for (int i = 0; i < subMeshCount; i++)
                {
                    var mat = targetComponent.TargetMaterials[i];
                    if (mat == null) continue;

                    // Update parameters directly on the materials managed by BaseDestruction
                    mat.SetMatrix("_CustomLocalToWorld", localToWorld);                       // Mesh matrix (used in fragment movement calculation)
                    mat.SetFloat("_StartTime", currentTime);                                  // Set the time of explosion
                    mat.SetFloat("_UseGravity", targetComponent.useGravity ? 1.0f : 0.0f);    // Whether to use gravity
                    mat.SetFloat("_IsPlayMode", Application.isPlaying ? 1.0f : 0.0f);         // Determine whether to use the logic for play mode or editor mode


                }

            // Setup Particle System
            var dustPrefab = serializedTarget.FindProperty("dustParticlePrefab").objectReferenceValue as ParticleSystem;
            if (dustPrefab != null)
            {
                DestroyPreviewParticle();
                Vector3 spawnPos = useCustomImpact ? customImpactCenter : DummyPosition;
                previewParticleSystem = Instantiate(dustPrefab, spawnPos, Quaternion.identity);
                previewParticleSystem.gameObject.hideFlags = HideFlags.HideAndDontSave;

                // Use the base class's logic to calculate scale and fetch the randomized gradient
                float particleScale = targetComponent.CalculateParticleScale();
                previewParticleSystem.transform.localScale = Vector3.one * particleScale;
                
                var col = previewParticleSystem.colorOverLifetime;
                col.enabled = true;
                col.color = targetComponent.ParticleGradient;
                
                previewParticleSystem.Play();
            }

            // Play Audio
            var audioClip = serializedTarget.FindProperty("destructionAudioClip").objectReferenceValue as AudioClip;
            if (audioClip != null)
            {
                float volume = serializedTarget.FindProperty("destructionAudioVolume").floatValue;
                PlayAudioClipInEditor(audioClip, volume);
            }

            EditorApplication.update -= SimulateStep;
            EditorApplication.update += SimulateStep;
        }

        /// <summary>
        /// Ticks the editor-loop physics and rendering updates.
        /// </summary>
        private void SimulateStep()
        {
            if (targetComponent == null)
            {
                StopSimulation();
                return;
            }

            float currentTime = (float)EditorApplication.timeSinceStartup;
            float deltaTime = currentTime - lastTime;
            lastTime = currentTime;

            simulatedTime += deltaTime;

            if (previewParticleSystem != null)
            {
                previewParticleSystem.Simulate(deltaTime, true, false);
            }

            if (targetComponent.TargetMaterials != null)
            {
                int subMeshCount = targetComponent.TargetMaterials.Length;
                for (int i = 0; i < subMeshCount; i++)
                {
                    var mat = targetComponent.TargetMaterials[i];
                    if (mat == null) continue;

                    mat.SetFloat("_EditorTime", simulatedTime);
                }
            }

            RenderPreview();
            
            // Force the UI system to repaint the editor window to support smooth real-time animation frames
            Repaint();
            
            serializedTarget.Update();
            var afterMode = (BaseDestruction.AfterDestructionMode)serializedTarget.FindProperty("afterDestructionMode").enumValueIndex;
            var afterTime = serializedTarget.FindProperty("afterDestructionTime").floatValue;
            
            if (afterMode == BaseDestruction.AfterDestructionMode.Reset && simulatedTime >= afterTime)
            {
                ResetSimulation();
            }
        }

        /// <summary>
        /// Stops simulation update ticks.
        /// </summary>
        private void StopSimulation()
        {
            if (isSimulating)
            {
                EditorApplication.update -= SimulateStep;
                isSimulating = false;
            }
        }

        /// <summary>
        /// Completely resets preview state and restores static object visibility.
        /// </summary>
        private void ResetSimulation()
        {
            StopSimulation();

            if (targetComponent != null)
            {
                targetComponent.CleanUpBuffers();
            }

            if (previewDummyGO != null)
            {
                var r = previewDummyGO.GetComponent<MeshRenderer>();
                if (r != null) r.enabled = true;
            }

            DestroyPreviewParticle();
            StopAudioClipInEditor();

            RenderPreview();
        }
        
        /// <summary>
        /// Triggered when the manual simulation button is clicked. Resets custom impact state.
        /// </summary>
        private void OnSimulateButtonClicked()
        {
            useCustomImpact = false;
            StartSimulation();
        }
        #endregion

        #region Preview Audio & Particle Utilities
        // ==================================================
        // Utilities for Audio & Particle in Editor
        // ==================================================
        private void DestroyPreviewParticle()
        {
            if (previewParticleSystem != null)
            {
                DestroyImmediate(previewParticleSystem.gameObject);
                previewParticleSystem = null;
            }
        }

        private void PlayAudioClipInEditor(AudioClip clip, float volume)
        {
            if (clip == null) return;
            StopAudioClipInEditor();

            bool playedViaSource = false;

            // Try playing using AudioSource on the preview camera to respect volume in editor
            if (previewUtility != null && previewUtility.camera != null)
            {
                if (previewAudioSource == null)
                {
                    previewAudioSource = previewUtility.camera.gameObject.AddComponent<AudioSource>();
                    previewAudioSource.hideFlags = HideFlags.HideAndDontSave;
                }
                
                previewAudioSource.volume = volume;
                previewAudioSource.clip = clip;
                previewAudioSource.spatialBlend = 0f;
                previewAudioSource.Play();
                
                // Track whether the AudioSource is currently active and processing the audio pipeline
                playedViaSource = previewAudioSource.isPlaying;
            }

            // Execute the unmanaged fallback routine ONLY if the standard AudioSource pipeline fails to output
            if (!playedViaSource)
            {
                // NOTE: AudioUtil fallback will disregard the volume parameter due to native Unity Editor internal API constraints
                PlayAudioUtilFallback(clip);
            }
        }

        private void PlayAudioUtilFallback(AudioClip clip)
        {
            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                var playClipMethod = audioUtilClass.GetMethod("PlayPreviewClip", BindingFlags.Static | BindingFlags.Public, null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null) 
                                  ?? audioUtilClass.GetMethod("PlayClip", BindingFlags.Static | BindingFlags.Public, null, new System.Type[] { typeof(AudioClip), typeof(int), typeof(bool) }, null);
                if (playClipMethod != null)
                {
                    playClipMethod.Invoke(null, new object[] { clip, 0, false });
                }
            }
        }

        private void StopAudioClipInEditor()
        {
            if (previewAudioSource != null)
            {
                previewAudioSource.Stop();
            }

            var unityEditorAssembly = typeof(AudioImporter).Assembly;
            var audioUtilClass = unityEditorAssembly.GetType("UnityEditor.AudioUtil");
            if (audioUtilClass != null)
            {
                var stopMethod = audioUtilClass.GetMethod("StopAllPreviewClips", BindingFlags.Static | BindingFlags.Public)
                              ?? audioUtilClass.GetMethod("StopAllClips", BindingFlags.Static | BindingFlags.Public);
                if (stopMethod != null)
                {
                    stopMethod.Invoke(null, null);
                }
            }
        }
        #endregion
    }
}
