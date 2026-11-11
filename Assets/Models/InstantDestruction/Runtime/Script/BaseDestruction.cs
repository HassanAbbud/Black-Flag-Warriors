using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;

namespace InstantDestruction
{
    /// <summary>
    /// Abstract base class that manages the destruction representation of objects.
    /// It fragments the mesh and uses compute shaders to perform physical behavior and rendering.
    /// </summary>
    public abstract class BaseDestruction : MonoBehaviour
    {
        #region Structures and Enums
        // ==================================================
        // Structures
        // ==================================================
        [StructLayout(LayoutKind.Sequential)]
        protected struct FragmentVertex {
            public Vector3 position;    // Local vertex coordinates
            public Vector3 normal;      // Normal (for lighting calculation)
            public Vector2 uv;          // Texture coordinates
            public Vector3 center;      // Triangle centroid (axis for rotation and scattering)
            public float randomId;      // Per-fragment random ID for variety
            public Vector3 velocity;    // For movement vector
            public Vector3 rotationaxis;// Rotation axis
            public Vector3 impactforce; // Impact vector
        }

        public enum AfterDestructionMode
        {
            Nothing,        // Do nothing (only stop script)
            Destroy,        // Completely destroy
            Reset           // Auto-reset (allows for multiple destructions)
        }

        #endregion

        #region Properties and Variables
        // ==================================================
        // Properties and Variables
        // ==================================================
        [Header("Physics Settings")]
        [Tooltip("Whether to apply gravity to fragments")]
        [SerializeField] public bool useGravity = true;
        [Tooltip("Explosion strength (initial velocity of scattering fragments)")]
        [SerializeField][Range(0f, 20f)] protected float explosionSpeed = 2.0f;
        [Tooltip("Force towards collision direction")]
        [SerializeField][Range(0f, 20f)] protected float impactStrength = 0.3f;

        [Header("Effect Settings")]
        [Tooltip("Particle prefab to spawn upon destruction")]
        [SerializeField] protected ParticleSystem dustParticlePrefab;
        [Tooltip("Audio clip to play upon destruction")]
        [SerializeField] protected AudioClip destructionAudioClip;
        [Tooltip("Audio volume")]
        [SerializeField] [Range(0f, 1f)] protected float destructionAudioVolume = 0.5f;

        [Header("After Destruction Settings")]
        [Tooltip("Processing after destruction (completely disappear, reset, etc.)")]
        [SerializeField] public AfterDestructionMode afterDestructionMode = AfterDestructionMode.Reset;
        [Tooltip("Time (in seconds) until processing happens after destruction")]
        [SerializeField] public float afterDestructionTime = 1.5f; 

        [SerializeField, HideInInspector] public ComputeShader instantDestructionCS;       // Compute shader for calculation
        [SerializeField, HideInInspector] public Shader instantDestructionSG;              // Shader graph for rendering

        // ==================================================
        // Abstract Properties (Specify GUID in child class)
        // ==================================================
        protected abstract string GuidComputeShader { get; }
        protected abstract string GuidShader { get; }
        protected abstract string GuidParticle { get; }
        protected abstract string GuidAudioClip { get; }

        // ==================================================
        // State and Physics Parameters
        // ==================================================
        protected bool isExploded = false;                  // Has already exploded
        protected Vector3 impactVelocity = Vector3.zero;    // Impact vector applied from outside

        // ==================================================
        // GPU Resources (Compute and rendering buffers)
        // ==================================================
        protected GraphicsBuffer[] fragmentBuffers;         // Fragment data buffer
        public GraphicsBuffer[] FragmentBuffers => fragmentBuffers;
        protected GraphicsBuffer[] argsBuffers;             // Rendering arguments buffer
        public GraphicsBuffer[] ArgsBuffers => argsBuffers;
        protected Material[] targetMaterials;               // Materials for rendering
        public Material[] TargetMaterials => targetMaterials;
        protected RenderParams[] renderParamsList;          // Parameters for RenderMeshIndirect
        public RenderParams[] RenderParamsList => renderParamsList;
        protected Mesh[] emptyMeshes;                       // Dummy meshes for specifying vertex count
        public Mesh[] EmptyMeshes => emptyMeshes;
        protected int[] threadGroupsList;                   // Number of CS thread groups
        public int[] ThreadGroupsList => threadGroupsList;
        protected int warmupFrames = 120;                   // Grace frames for warmup to handle async compile
        public int WarmupFrames => warmupFrames;

        // ==================================================
        // Caches and Runtime-Generated Instances
        // ==================================================
        protected MeshFilter meshFilter;                    // For referencing the original mesh
        public MeshFilter MeshFilterComponent => meshFilter;
        protected Renderer meshRenderer;                    // For toggling original mesh visibility
        public Renderer MeshRendererComponent => meshRenderer;
        protected Bounds localBounds;                       // Local bounding box of the mesh

        protected AudioSource audioSource;                  // Audio source for playing destruction sounds
        protected ParticleSystem destructionParticleSystem; // Generated particle instance
        protected float finalParticleScale = 1.0f;          // Precalculated particle scale
        protected Gradient particleGradient;                // Random gradient to apply to particles
        public Gradient ParticleGradient => particleGradient;
        public float DestructionAudioVolume => destructionAudioVolume;

        #endregion

        #region Unity Lifecycle Methods
        // ==================================================
        // Unity Lifecycle Methods
        // ==================================================
#if UNITY_EDITOR
        /// <summary>
        /// Called when the script is loaded or a value is changed in the inspector (Editor only).
        /// Ensures resources are loaded and serialized so they are included in the build.
        /// </summary>
        protected virtual void OnValidate()
        {
            bool changed = SetGuidResource();
            changed |= SetAudioSource();
            ResetParticalGradient();

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
                // if (UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this))
                // {
                //     UnityEditor.AssetDatabase.SaveAssets();
                // }
            }
        }
#endif
        /// <summary>
        /// Initializes component references and resources when attached or reset in the Inspector.
        /// Performs automatic settings for necessary resources and warns about read/write settings of the mesh.
        /// </summary>
        protected virtual void Reset()
        {
#if UNITY_EDITOR
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter != null && filter.sharedMesh != null)
            {
                if (!filter.sharedMesh.isReadable)
                {
                    Debug.LogWarning($"[instantDestruction] Mesh '{filter.sharedMesh.name}' is set to non-readable. Please enable 'Read/Write' in the model's import settings.");
                }
            }

            // Parameter settings
            // Load using GUID returned by child class
            bool changed = SetGuidResource();

            // Component settings
            // Add audio source
            changed |= SetAudioSource();

            // Initialize particleGradient
            ResetParticalGradient();

            if (changed)
            {
                UnityEditor.EditorUtility.SetDirty(this);
            }
#endif
        }

        /// <summary>
        /// Initializes the gradient for particles with random colors.
        /// </summary>
        private void ResetParticalGradient()
        {
            particleGradient = new Gradient();
            // Randomly change saturation and brightness while maintaining the hue
            Color randomColor = Random.ColorHSV(0.05f, 0.15f, 0.8f, 1f, 0.8f, 1f);
            particleGradient.SetKeys(
                new GradientColorKey[] { 
                                        new GradientColorKey(randomColor, 0.0f)
                                        },
                new GradientAlphaKey[] { 
                                        new GradientAlphaKey(1.0f, 0.0f),
                                        new GradientAlphaKey(1.0f, 0.2f),
                                        new GradientAlphaKey(0.0f, 1.0f) 
                                        }
            );
        }

        /// <summary>
        /// Initialization processing when the game starts.
        /// Executes precalculation of destruction data and compute shader warmup to reduce load at the time of destruction.
        /// </summary>
        protected virtual void Start()
        {
            // Try to set up here in case it wasn't initialized by Reset() (e.g. if dynamically added via AddComponent)

            // Parameter settings
            // Load using GUID returned by child class
            SetGuidResource();

            // Component settings
            // Add audio source
            SetAudioSource();

            // Precalculate whatever is possible to reduce load during OnTrigger
            PrecalculateDestructionData();
            
            // Precalculate what is possible, like particle scale
            PrecalculateParticleScale();

            // --- Warmup Processing ---
            // 1. Compute Shader Warmup
            // Execute the same kernel used for actual destruction with dummy parameters to force compilation.
            if (instantDestructionCS != null && fragmentBuffers != null)
            {
                for (int i = 0; i < fragmentBuffers.Length; i++)
                {
                    if (fragmentBuffers[i] == null) continue;
                    instantDestructionCS.SetBuffer(0, "_FragmentBuffer", fragmentBuffers[i]);
                    instantDestructionCS.SetFloat("_DeltaTime", 0f);
                    instantDestructionCS.SetFloat("_ExplosionSpeed", 0f); // Set to 0 so fragments won't scatter
                    instantDestructionCS.SetMatrix("_CustomLocalToWorld", transform.localToWorldMatrix);
                    instantDestructionCS.SetVector("_ExplosionCenter", Vector3.zero);
                    instantDestructionCS.SetVector("_ImpactVelocity", Vector3.zero);
                    instantDestructionCS.SetFloat("_ImpactStrength", impactStrength);
                    instantDestructionCS.Dispatch(0, 1, 1, 1); // Empty dispatch with minimum threads
                }
            }

            // * Rendering shader (Shader Graph) warmup is executed for a few frames in Update as a countermeasure against culling
        }

        /// <summary>
        /// Rendering processing every frame.
        /// Issues rendering commands (RenderMeshIndirect) for warmup drawing as an async compilation countermeasure, and for drawing fragments after destruction.
        /// </summary>
        protected virtual void Update()
        {
            // Do not draw if initialization failed
            if (emptyMeshes == null || argsBuffers == null) return;

            if (!isExploded)
            {
                // To account for delays in asynchronous shader compilation (especially in the editor),
                // continuously issue drawing commands in the background for a while even before destruction.
                // * The scale is set to 0, so nothing will be rendered on the screen.
                if (warmupFrames > 0)
                {
                    for (int i = 0; i < emptyMeshes.Length; i++)
                    {
                        if (emptyMeshes[i] == null || argsBuffers[i] == null) continue;
                        Graphics.RenderMeshIndirect(renderParamsList[i], emptyMeshes[i], argsBuffers[i]);
                    }
                    warmupFrames--;
                }
            }
            else
            {
                // Drawing commands after destruction
                for (int i = 0; i < emptyMeshes.Length; i++)
                {
                    if (emptyMeshes[i] == null || argsBuffers[i] == null) continue;
                    Graphics.RenderMeshIndirect(renderParamsList[i], emptyMeshes[i], argsBuffers[i]);
                }
            }
        }

        /// <summary>
        /// Processing when the object is destroyed.
        /// Releases resources such as allocated GPU buffers and materials.
        /// </summary>
        protected virtual void OnDestroy()
        {
            ReleaseBuffers();
        }

        /// <summary>
        /// Safely releases graphics buffers to prevent memory leaks.
        /// </summary>
        public void CleanUpBuffers()
        {
            if (fragmentBuffers != null) { foreach (var b in fragmentBuffers) b?.Release(); fragmentBuffers = null; }
            if (argsBuffers != null) { foreach (var b in argsBuffers) b?.Release(); argsBuffers = null; }

            // Local function to handle unified object destruction depending on the play mode
            System.Action<Object[]> clean = (objects) =>
            {
                if (objects == null) return;
                if (Application.isPlaying) foreach (var obj in objects) { if (obj != null) Destroy(obj); }
                else foreach (var obj in objects) { if (obj != null) DestroyImmediate(obj); }
            };

            // Clean up materials and dummy meshes
            clean(targetMaterials); targetMaterials = null;
            clean(emptyMeshes); emptyMeshes = null;
        }

        /// <summary>
        /// Safely releases dynamic resources like GPU buffers, materials, meshes, etc.
        /// </summary>
        protected void ReleaseBuffers()
        {
            CleanUpBuffers();
        }

        #endregion

        #region Public Methods (Destruction Trigger)
        // ==================================================
        // Public Methods (Destruction Trigger)
        // ==================================================
        /// <summary>
        /// Triggers the destruction event using the object's center as the explosion center point.
        /// </summary>
        public virtual void OnTrigger()
        {
            if (isExploded) {return;} // Prevent double destruction
            Vector3 explosionCenter = transform.TransformPoint(localBounds.center);
            OnTrigger(explosionCenter);
        }

        /// <summary>
        /// Triggers the destruction event using the specified position as the explosion center point.
        /// Performs particle generation, audio playback, calculation instructions to shader, etc.
        /// </summary>
        /// <param name="expPos">Explosion center coordinates (world space)</param>
        public virtual void OnTrigger(Vector3 expPos)
        {
            if (isExploded) {return;} // Prevent double destruction

            // Hook processing for child classes just before destruction
            OnBeforeTrigger();

            // --------------------------------------------------
            // 1. Component Verification
            // --------------------------------------------------
            if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
            {
                StringBuilder sb = new StringBuilder();
                if(meshFilter == null) sb.Append("meshFilter is missing. ");
                if(meshFilter.sharedMesh == null) sb.Append("sharedMesh is missing. ");
                if(meshRenderer == null) sb.Append("meshRenderer is missing. ");
                Debug.LogWarning( sb.ToString() + "Check if PrecalculateDestructionData ran.");
                return;
            }

            // --------------------------------------------------
            // 2. Dynamic Parameter Setting
            // --------------------------------------------------
            // Get matrix (position and rotation at the moment of destruction)
            Matrix4x4 localToWorld = transform.localToWorldMatrix;

            for (int i = 0; i < targetMaterials.Length; i++)
            {
                if (targetMaterials[i] == null) continue;
                targetMaterials[i].SetMatrix("_CustomLocalToWorld", localToWorld); // Mesh matrix (used in fragment movement calculation)
                targetMaterials[i].SetFloat("_StartTime", Time.time);              // Set the time of explosion
                targetMaterials[i].SetFloat("_UseGravity", useGravity ? 1.0f : 0.0f);            // Whether to use gravity
                targetMaterials[i].SetFloat("_IsPlayMode", Application.isPlaying ? 1.0f : 0.0f); // Determine whether to use the logic for play mode or editor mode
            }

            // --------------------------------------------------
            // 3. Compute Shader Setup and Execution
            // --------------------------------------------------
            instantDestructionCS.SetFloat("_DeltaTime", Time.deltaTime);         // Elapsed time
            instantDestructionCS.SetFloat("_ExplosionSpeed", explosionSpeed);    // Explosion strength
            instantDestructionCS.SetMatrix("_CustomLocalToWorld", localToWorld); // Mesh matrix (used in fragment movement calculation)
            instantDestructionCS.SetVector("_ExplosionCenter", expPos);          // Explosion center coordinates (destruction position)
            instantDestructionCS.SetVector("_ImpactVelocity", impactVelocity);   // Velocity at collision
            instantDestructionCS.SetFloat("_ImpactStrength", impactStrength);    // Force in impact direction

            for (int i = 0; i < fragmentBuffers.Length; i++)
            {
                if (fragmentBuffers[i] == null) continue;
                instantDestructionCS.SetBuffer(0, "_FragmentBuffer", fragmentBuffers[i]);
                // Compute shader dispatch (batch execution)
                instantDestructionCS.Dispatch(0, threadGroupsList[i], 1, 1);
            }
            
            // --------------------------------------------------
            // 4. Update State and Hide Original Mesh
            // --------------------------------------------------
            isExploded = true;
            meshRenderer.enabled = false;

            // --------------------------------------------------
            // 5.1 Generate Destruction Effects
            // --------------------------------------------------
            if (destructionParticleSystem != null)
            {
                destructionParticleSystem.transform.position = expPos; // Set particle system coordinates to explosion position
                destructionParticleSystem.Play();                      // Play particle system
                destructionParticleSystem.transform.SetParent(null);   // Detach from parent to avoid being affected by parent object
            }

            // --------------------------------------------------
            // 5.2 Play Destruction Sound
            // --------------------------------------------------
            if (audioSource != null)
            {
                audioSource.PlayOneShot(destructionAudioClip, destructionAudioVolume); // Play destruction sound
            }

            // --------------------------------------------------
            // 6. Processing After Destruction (AfterDestructionMode)
            // --------------------------------------------------
            HandleAfterDestruction();

            // Hook processing for child classes just after destruction
            OnAfterTrigger();
        }

        /// <summary>
        /// Handles object behavior after destruction is complete (after destruction effect occurs).
        /// </summary>
        protected virtual void HandleAfterDestruction()
        {
            switch (afterDestructionMode)
            {
                case AfterDestructionMode.Nothing:
                    // Do nothing
                    break;

                case AfterDestructionMode.Destroy:
                    // Destroy GameObject itself after specified time
                    Destroy(gameObject, afterDestructionTime);
                    break;

                case AfterDestructionMode.Reset:
                    // Reset to original mesh after specified time, allowing it to be destroyed again
                    StartCoroutine(ResetRoutine(afterDestructionTime));
                    break;
            }
        }
        /// <summary>
        /// Retrieves GPU data from the specified buffer and formats a single vertex at the given index
        /// </summary>
        /// <param name="buffer">buffer</param>
        /// <param name="index">index</param>
        /// <returns></returns>
        public virtual string DumpVertices(GraphicsBuffer buffer, int index)
        {
            if (buffer == null)
            {
                return $"[BaseDestruction Dump] Specified buffer is null.";
            }

            int totalElements = buffer.count;
            
            // Ensure the requested index fits within the actual allocated buffer boundaries
            if (index < 0 || index >= totalElements)
            {
                return $"[BaseDestruction Dump] Target index {index} is out of bounds for buffer size {totalElements}.";
            }

            // Allocate temporary memory array matching the structure count to pull data from VRAM
            var resultVertices = new FragmentVertex[totalElements];
            buffer.GetData(resultVertices);

            var logBuilder = new System.Text.StringBuilder();
            logBuilder.AppendLine($"[BaseDestruction Dump] Vertex Index: {index} | Total Buffer Size: {totalElements}");
            logBuilder.AppendLine("========================================================================");

            var v = resultVertices[index];
            logBuilder.AppendLine($"  Position     : {v.position.x:F4}, {v.position.y:F4}, {v.position.z:F4}");
            logBuilder.AppendLine($"  Normal       : {v.normal.x:F4}, {v.normal.y:F4}, {v.normal.z:F4}");
            logBuilder.AppendLine($"  UV           : {v.uv.x:F4}, {v.uv.y:F4}");
            logBuilder.AppendLine($"  Center       : {v.center.x:F4}, {v.center.y:F4}, {v.center.z:F4}");
            logBuilder.AppendLine($"  Random ID    : {v.randomId:F4}");
            logBuilder.AppendLine($"  Velocity     : {v.velocity.x:F4}, {v.velocity.y:F4}, {v.velocity.z:F4}");
            logBuilder.AppendLine($"  Rotation Axis: {v.rotationaxis.x:F4}, {v.rotationaxis.y:F4}, {v.rotationaxis.z:F4}");
            logBuilder.AppendLine($"  Impact Force : {v.impactforce.x:F4}, {v.impactforce.y:F4}, {v.impactforce.z:F4}");
            logBuilder.AppendLine("========================================================================");

            return logBuilder.ToString();
        }

        /// <summary>
        /// Coroutine that resets the object to its original mesh state after a specified time, making it destructible again.
        /// </summary>
        /// <param name="delay">Wait time before resetting (seconds)</param>
        private System.Collections.IEnumerator ResetRoutine(float delay)
        {
            yield return new WaitForSeconds(delay);

            isExploded = false;
            meshRenderer.enabled = true;

            // Set up again to revert buffers modified by ComputeShader to initial state
            PrecalculateDestructionData();

            // Reset particle system
            ResetParticle();
            
            // Reset frame count to perform warmup again
            warmupFrames = 120;
            
            // Hook processing immediately after reset
            OnAfterResetRoutine();
        }

        #region Hook Processing
        // ==================================================
        // Hook Processing (For inheriting classes)
        // ==================================================
        /// <summary> Processing for child classes called immediately before OnTrigger execution </summary>
        protected virtual void OnBeforeTrigger()
        {
        }

        /// <summary> Processing for child classes called immediately after OnTrigger execution </summary>
        protected virtual void OnAfterTrigger()
        {
        }

        /// <summary> Processing for child classes called after ResetRoutine finishes </summary>
        protected virtual void OnAfterResetRoutine()
        {
        }
        #endregion

        #endregion

        #region Internal Methods (Precalculation/Initialization)
        // ==================================================
        // Internal Methods
        // Restrictions:
        // ==================================================
        /// <summary>
        /// Calculates the appropriate scale for particles based on object size.
        /// </summary>
        public virtual float CalculateParticleScale()
        {
            // --- Parameters to adjust particle size ---
            float particleSizeAdjust = 1.0f; // Overall magnification adjustment
            float minParticleScale = 1.0f;   // Minimum size below which it won't shrink
            float maxParticleScale = 4.0f;   // Maximum size above which it won't grow

            // Calculate size in world space by multiplying mesh bounding box and Transform scale
            Vector3 worldSize = Vector3.Scale(localBounds.size, transform.lossyScale);
            
            // Adopt the size of the largest axis as base scale
            float baseScale = Mathf.Max(worldSize.x, Mathf.Max(worldSize.y, worldSize.z));

            // Multiply adjustment parameter, then clamp within min/max bounds (preventing too small/too large)
            return Mathf.Clamp(baseScale * particleSizeAdjust, minParticleScale, maxParticleScale);
        }

        /// <summary>
        /// Precalculates the scale of particles generated upon destruction based on object size.
        /// </summary>
        public virtual void PrecalculateParticleScale()
        {
            finalParticleScale = CalculateParticleScale();

            // Reset particle system
            ResetParticle();
        }

        /// <summary>
        /// Instantiates particle system displayed upon destruction and initializes its scale and color.
        /// </summary>
        protected virtual void ResetParticle()
        {
            // Destroy if old particle system remains
            if (destructionParticleSystem != null)
            {
                Destroy(destructionParticleSystem.gameObject);
                destructionParticleSystem = null;
            }

            // Generate particle system
            destructionParticleSystem = Instantiate(dustParticlePrefab, transform.position, Quaternion.identity);
            
            // Apply scale to generated effect's GameObject
            destructionParticleSystem.transform.localScale = Vector3.one * finalParticleScale;

            // Set particle system gradient
            var col = destructionParticleSystem.colorOverLifetime;
            col.enabled = true;
            col.color = particleGradient;

            // Set parent of particle system
            destructionParticleSystem.transform.SetParent(this.transform);
        }

        /// <summary>
        /// Analyzes mesh data and prebuilds fragment data (vertices, normals, UVs, etc.) 
        /// as GPU buffers for computation in the compute shader.
        /// </summary>
        public void PrecalculateDestructionData()
        {
            meshFilter = GetComponent<MeshFilter>();
            meshRenderer = GetComponent<Renderer>();

            // Abort processing if required components are not attached
            if (meshFilter == null || meshFilter.sharedMesh == null || meshRenderer == null)
            {
                Debug.LogWarning("MeshFilter or Renderer is missing.");
                return;
            }

            // Get mesh information
            Mesh mesh = meshFilter.sharedMesh;
            localBounds = mesh.bounds;
            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;

            int subMeshCount = mesh.subMeshCount;
            Material[] sharedMaterials = meshRenderer.sharedMaterials;

            // Release if existing buffers exist
            ReleaseBuffers();

            // Initialize necessary buffers and arrays per submesh
            fragmentBuffers = new GraphicsBuffer[subMeshCount];
            targetMaterials = new Material[subMeshCount];
            argsBuffers = new GraphicsBuffer[subMeshCount];
            renderParamsList = new RenderParams[subMeshCount];
            emptyMeshes = new Mesh[subMeshCount];
            threadGroupsList = new int[subMeshCount];

            bool hasNormals = normals != null && normals.Length > 0;
            bool hasUvs = uvs != null && uvs.Length > 0;
            int stride = Marshal.SizeOf<FragmentVertex>(); // Size of structure passed to compute shader

            // Build data per submesh
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                int[] indices = mesh.GetIndices(subMeshIndex);
                int vertexCount = indices.Length;

                FragmentVertex[] fragmentVertices = new FragmentVertex[vertexCount];

                // Generate fragment data for every 3 vertices (1 triangle)
                for (int i = 0; i < vertexCount; i += 3)
                {
                    int i0 = indices[i];
                    int i1 = indices[i + 1];
                    int i2 = indices[i + 2];

                    // Get vertex data
                    Vector3 v0 = vertices[i0];
                    Vector3 v1 = vertices[i1];
                    Vector3 v2 = vertices[i2];

                    // Centroid of this triangle (used as axis for rotation and scattering)
                    Vector3 center = (v0 + v1 + v2) / 3.0f;
                    // Random value to give individual differences to each fragment
                    float randomId = Random.value;

                    // Set each vertex data of the fragment (position, normal, UV, centroid, random value, etc.)
                    fragmentVertices[i] = new FragmentVertex { position = v0, normal = hasNormals ? transform.TransformDirection(normals[i0]) : Vector3.up, uv = hasUvs ? uvs[i0] : Vector2.zero, center = center, randomId = randomId, velocity = Vector3.zero, rotationaxis = Vector3.zero };
                    fragmentVertices[i + 1] = new FragmentVertex { position = v1, normal = hasNormals ? transform.TransformDirection(normals[i1]) : Vector3.up, uv = hasUvs ? uvs[i1] : Vector2.zero, center = center, randomId = randomId, velocity = Vector3.zero, rotationaxis = Vector3.zero };
                    fragmentVertices[i + 2] = new FragmentVertex { position = v2, normal = hasNormals ? transform.TransformDirection(normals[i2]) : Vector3.up, uv = hasUvs ? uvs[i2] : Vector2.zero, center = center, randomId = randomId, velocity = Vector3.zero, rotationaxis = Vector3.zero };
                }

                // Build graphics buffer for calculating in compute shader and set data
                fragmentBuffers[subMeshIndex] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, stride);
                fragmentBuffers[subMeshIndex].SetData(fragmentVertices);

                // Set material for rendering (inherit properties of original material)
                Material srcMat = sharedMaterials.Length > subMeshIndex ? sharedMaterials[subMeshIndex] : sharedMaterials[0];
                targetMaterials[subMeshIndex] = new Material(instantDestructionSG);
                targetMaterials[subMeshIndex].CopyPropertiesFromMaterial(srcMat);
                targetMaterials[subMeshIndex].SetBuffer("_FragmentBuffer", fragmentBuffers[subMeshIndex]);

                // Build argument buffer for RenderMeshIndirect
                argsBuffers[subMeshIndex] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
                uint[] args = new uint[5];
                args[0] = (uint)vertexCount; // Vertex count
                args[1] = 1;                 // Instance count
                args[2] = 0;
                args[3] = 0;
                args[4] = 0;
                argsBuffers[subMeshIndex].SetData(args);

                // Build rendering parameters
                renderParamsList[subMeshIndex] = new RenderParams(targetMaterials[subMeshIndex]);
                // Set to not display shadows of fragments upon destruction
                renderParamsList[subMeshIndex].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderParamsList[subMeshIndex].receiveShadows = meshRenderer.receiveShadows;

                // Set an extremely large Bounds to prevent warmup rendering from being skipped due to being judged out of camera view
                renderParamsList[subMeshIndex].worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000000f);
                
                // Scale to 0 so it doesn't appear on screen (and doesn't cast shadow) during warmup
                targetMaterials[subMeshIndex].SetMatrix("_CustomLocalToWorld", Matrix4x4.Scale(Vector3.zero));

                // Generate empty dummy mesh having only vertex count (for RenderMeshIndirect)
                emptyMeshes[subMeshIndex] = new Mesh();
                emptyMeshes[subMeshIndex].vertices = new Vector3[vertexCount]; 
                int[] dummyIndices = new int[vertexCount];
                for (int i = 0; i < vertexCount; i++) dummyIndices[i] = i;
                emptyMeshes[subMeshIndex].SetIndices(dummyIndices, MeshTopology.Triangles, 0);
                emptyMeshes[subMeshIndex].UploadMeshData(true);

                // Calculate thread groups count for compute shader execution (in units of 64 threads)
                threadGroupsList[subMeshIndex] = Mathf.CeilToInt((float)vertexCount / 64);
            }

            // Initialize particleGradient
            ResetParticalGradient();
        }

        public void PrecalculateDestructionData(
            Mesh mesh,
            Material[] sharedMaterials,
            Shader shaderGraph,
            Matrix4x4 localToWorldMatrix,
            bool receiveShadows)
        {

            if (mesh == null || sharedMaterials == null || shaderGraph == null)
            {
                Debug.LogWarning("Required assets or mesh references are missing.");
                return;
            }

            Vector3[] vertices = mesh.vertices;
            Vector3[] normals = mesh.normals;
            Vector2[] uvs = mesh.uv;
            int subMeshCount = mesh.subMeshCount;

            // Clear existing GPU resources before allocating new ones
            ReleaseBuffers();

            // Allocate fresh arrays to store the results into member variables
            fragmentBuffers = new GraphicsBuffer[subMeshCount];
            targetMaterials = new Material[subMeshCount];
            argsBuffers = new GraphicsBuffer[subMeshCount];
            renderParamsList = new RenderParams[subMeshCount];
            emptyMeshes = new Mesh[subMeshCount];
            threadGroupsList = new int[subMeshCount];
            localBounds = mesh.bounds;

            bool hasNormals = normals != null && normals.Length > 0;
            bool hasUvs = uvs != null && uvs.Length > 0;
            int stride = Marshal.SizeOf<FragmentVertex>();

            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                int[] indices = mesh.GetIndices(subMeshIndex);
                int vertexCount = indices.Length;
                var fragmentVertices = new FragmentVertex[vertexCount];

                for (int i = 0; i < vertexCount; i += 3)
                {
                    int i0 = indices[i];
                    int i1 = indices[i + 1];
                    int i2 = indices[i + 2];

                    Vector3 v0 = vertices[i0];
                    Vector3 v1 = vertices[i1];
                    Vector3 v2 = vertices[i2];
                    Vector3 center = (v0 + v1 + v2) / 3.0f;
                    float randomId = Random.value;

                    // Compute world normals cleanly using the passed matrix input
                    Vector3 n0 = hasNormals ? localToWorldMatrix.MultiplyVector(normals[i0]).normalized : Vector3.up;
                    Vector3 n1 = hasNormals ? localToWorldMatrix.MultiplyVector(normals[i1]).normalized : Vector3.up;
                    Vector3 n2 = hasNormals ? localToWorldMatrix.MultiplyVector(normals[i2]).normalized : Vector3.up;

                    fragmentVertices[i] = new FragmentVertex { position = v0, normal = n0, uv = hasUvs ? uvs[i0] : Vector2.zero, center = center, randomId = randomId, velocity = Vector3.zero, rotationaxis = Vector3.zero };
                    fragmentVertices[i + 1] = new FragmentVertex { position = v1, normal = n1, uv = hasUvs ? uvs[i1] : Vector2.zero, center = center, randomId = randomId, velocity = Vector3.zero, rotationaxis = Vector3.zero };
                    fragmentVertices[i + 2] = new FragmentVertex { position = v2, normal = n2, uv = hasUvs ? uvs[i2] : Vector2.zero, center = center, randomId = randomId, velocity = Vector3.zero, rotationaxis = Vector3.zero };
                }

                // Write structures to graphics buffer
                fragmentBuffers[subMeshIndex] = new GraphicsBuffer(GraphicsBuffer.Target.Structured, vertexCount, stride);
                fragmentBuffers[subMeshIndex].SetData(fragmentVertices);

                // Setup submesh material clone
                Material srcMat = sharedMaterials.Length > subMeshIndex ? sharedMaterials[subMeshIndex] : sharedMaterials[0];
                targetMaterials[subMeshIndex] = new Material(shaderGraph);
                targetMaterials[subMeshIndex].CopyPropertiesFromMaterial(srcMat);
                targetMaterials[subMeshIndex].SetBuffer("_FragmentBuffer", fragmentBuffers[subMeshIndex]);

                // Construct argument buffer for indirect rendering
                argsBuffers[subMeshIndex] = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
                var args = new uint[5] { (uint)vertexCount, 1, 0, 0, 0 };
                argsBuffers[subMeshIndex].SetData(args);

                // Configure render parameter settings
                renderParamsList[subMeshIndex] = new RenderParams(targetMaterials[subMeshIndex]);
                renderParamsList[subMeshIndex].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderParamsList[subMeshIndex].receiveShadows = receiveShadows;
                renderParamsList[subMeshIndex].worldBounds = new Bounds(Vector3.zero, Vector3.one * 100000000f);

                targetMaterials[subMeshIndex].SetMatrix("_CustomLocalToWorld", Matrix4x4.Scale(Vector3.zero));

                // Generate non-indexed dummy topology mesh
                emptyMeshes[subMeshIndex] = new Mesh();
                emptyMeshes[subMeshIndex].vertices = new Vector3[vertexCount];
                var dummyIndices = new int[vertexCount];
                for (int i = 0; i < vertexCount; i++) dummyIndices[i] = i;
                emptyMeshes[subMeshIndex].SetIndices(dummyIndices, MeshTopology.Triangles, 0);
                emptyMeshes[subMeshIndex].UploadMeshData(true);

                // Store individual thread group sizes
                threadGroupsList[subMeshIndex] = Mathf.CeilToInt((float)vertexCount / 64);
            }

        }


        /// <summary>
        /// For Editor environment only: loads and returns asset from GUID.
        /// </summary>
        /// <typeparam name="T">Asset type to load</typeparam>
        /// <param name="guid">GUID string of target asset</param>
        /// <returns>Loaded asset, or null</returns>
        protected T LoadAssetByGuid<T>(string guid) where T : UnityEngine.Object
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid)) return null;
            string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
            return UnityEditor.AssetDatabase.LoadAssetAtPath<T>(path);
#else
            return null;
#endif
        }
        /// <summary>
        /// Loads and sets required resources such as shaders, particles, audio, etc. using GUIDs provided by derived class.
        /// </summary>
        /// <returns>True if any resource was newly assigned</returns>
        private bool SetGuidResource()
        {
            bool changed = false;

            if (instantDestructionCS == null)
            {
                ComputeShader cs = LoadAssetByGuid<ComputeShader>(GuidComputeShader);
                if (cs != null) { instantDestructionCS = cs; changed = true; }
            }
            if (instantDestructionSG == null)
            {
                Shader sg = LoadAssetByGuid<Shader>(GuidShader);
                if (sg != null) { instantDestructionSG = sg; changed = true; }
            }
            if (dustParticlePrefab == null)
            {
                ParticleSystem ps = LoadAssetByGuid<ParticleSystem>(GuidParticle);
                if (ps != null) { dustParticlePrefab = ps; changed = true; }
            }
            if (destructionAudioClip == null)
            {
                AudioClip clip = LoadAssetByGuid<AudioClip>(GuidAudioClip);
                if (clip != null) { destructionAudioClip = clip; changed = true; }
            }

            return changed;
        }

        /// <summary>
        /// Gets AudioSource component for destruction audio playback, or adds one if not attached.
        /// </summary>
        /// <returns>True if component was added</returns>
        private bool SetAudioSource()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                return true;
            }
            return false;
        }
        #endregion
    }
}
