using UnityEngine;
using System.Runtime.InteropServices;
using System.Text;

namespace InstantDestruction
{
    /// <summary>
    /// A simple destruction control class that triggers destruction based on physical collision.
    /// Inherits from BaseDestruction and defines GUIDs for resources needed to load assets.
    /// </summary>
    public class InstantDestructionSimple : BaseDestruction
    {
        // ==================================================
        // Asset GUID Definitions
        // ==================================================
        protected override string GuidComputeShader => "464b4875e5462c0468b8372c24d8c021";
        protected override string GuidShader        => "fc69ed8e390e936458a28219f93e6046";
        protected override string GuidParticle      => "fac253c45a46001458eda61bab5ede26";
        protected override string GuidAudioClip     => "13a35bc44c7da2b45a428252ae2ea957";

        #region Unity Lifecycle Methods
        // ==================================================
        // Unity Lifecycle Methods
        // ==================================================
        /// <summary>
        /// Initialization processing when a component is attached in the editor or reset.
        /// In addition to base class initialization, it automatically configures colliders for physics detection.
        /// </summary>
        protected override void Reset()
        {
            base.Reset(); // Execute automatic loading of parent class

            // If the game object does not have a collider, add a BoxCollider
            AddCollider();

        }

        /// <summary>
        /// Initialization processing when the game starts.
        /// Ensures colliders are set up correctly even if the script is dynamically added.
        /// </summary>
        protected override void Start()
        {
            base.Start();
            // If dynamically added using AddComponent, Reset() is not called, so try to load here
            AddCollider();

        }

        /// <summary>
        /// Update processing every frame. Calls base class rendering updates (e.g., rendering fragments).
        /// </summary>
        protected override void Update()
        {
            base.Update();
        }

        /// <summary>
        /// Processing when the object is destroyed. Calls base class resource release processing.
        /// </summary>
        protected override void OnDestroy()
        {
            base.OnDestroy();
        }

        /// <summary>
        /// Called when a physical collision occurs, triggering the destruction event.
        /// </summary>
        /// <param name="collision">Collision information</param>
        private void OnCollisionEnter(Collision collision)
        {
            // Calculate the average coordinates of the collision position (contact points)
            Vector3 contactPoint = Vector3.zero;
            if (collision.contacts.Length > 0)
            {
                foreach (ContactPoint contact in collision.contacts)
                {
                    contactPoint += contact.point;
                }
                contactPoint /= collision.contacts.Length;
            }
            else
            {
                contactPoint = transform.position;
            }

            // Call base class destruction processing (OnTrigger), passing the collision position as the center of the explosion
            OnTrigger(contactPoint);
        }

        /// <summary>
        /// Called when a trigger collision occurs, triggering the destruction event.
        /// </summary>
        /// <param name="other">Trigger collider information</param>
        private void OnTriggerEnter(Collider other)
        {
            // Calculate the coordinates of the trigger position using the closest point on the collider
            Vector3 contactPoint = other.ClosestPoint(transform.position);

            // Call base class destruction processing (OnTrigger), passing the trigger position as the center of the explosion
            OnTrigger(contactPoint);
        }
        #endregion



        #region Hook Processing
        // ==================================================
        // Hook Processing
        // ==================================================
        /// <summary>
        /// Hook processing called immediately after destruction processing (OnTrigger) is executed.
        /// </summary>
        protected override void OnAfterTrigger()
        {
            // Disable collider
            SetColliderEnabled(false);
        }

        /// <summary>
        /// Hook processing called immediately after reset processing (ResetRoutine) is executed.
        /// </summary>
        protected override void OnAfterResetRoutine()
        {
            // Enable collider
            SetColliderEnabled(true);
        }
        #endregion

        #region Internal Methods
        // ==================================================
        // Internal Methods
        // ==================================================
        /// <summary>
        /// Ensures a Collider exists on the GameObject.
        /// If missing, adds a BoxCollider fitted to the mesh bounds.
        /// </summary>
        private void AddCollider()
        {
            if (GetComponent<Collider>() == null)
            {
                BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
                MeshFilter filter = GetComponent<MeshFilter>();
                if (filter != null && filter.sharedMesh != null)
                {
                    boxCollider.center = filter.sharedMesh.bounds.center;
                    boxCollider.size = filter.sharedMesh.bounds.size;
                }
            }
        }

        /// <summary>
        /// Sets whether the collider is enabled or disabled.
        /// </summary>
        private void SetColliderEnabled(bool isEnable)
        {
            Collider col = GetComponent<Collider>();
            if (col != null)
            {
                col.enabled = isEnable;
            }
        }
        #endregion
    }
}
