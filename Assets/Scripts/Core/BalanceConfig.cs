using UnityEngine;

namespace Ahoy.Core
{
    // The single tuning source (Build Spec, "Tuning constants"). Every track adds its own
    // [Header] section here; a hard-coded gameplay number anywhere else is a bug.
    // Change a value in Data/Balance/BalanceConfig.asset and in the Build Spec in the same PR.
    [CreateAssetMenu(menuName = "Ahoy/Balance Config", fileName = "BalanceConfig")]
    public sealed class BalanceConfig : ScriptableObject
    {
        [Header("Zahim — health (R3, R51)")]
        [Tooltip("Hit points. Empty means defeat.")]
        public float playerHealth = 1000f;

        [Header("Zahim — locomotion")]
        [Tooltip("Run speed, metres per second.")]
        public float moveSpeed = 7f;
        [Tooltip("Sprint speed, metres per second.")]
        public float sprintSpeed = 10f;
        [Tooltip("Seconds of continuous running before he breaks into a sprint on his own (Hyrule Warriors).")]
        public float autoSprintDelay = 1f;
        [Tooltip("Stick deflection (0..1) that counts as running for autoSprintDelay.")]
        public float autoSprintStickThreshold = 0.7f;
        [Tooltip("How fast he turns to face the stick, degrees per second.")]
        public float turnSpeed = 900f;
        [Tooltip("Seconds for the animator Speed parameter to catch up; smooths blend-tree pops.")]
        public float speedDampSeconds = 0.08f;
        [Tooltip("Downward acceleration, metres per second squared (negative).")]
        public float gravity = -25f;
        [Tooltip("Small downward speed kept while grounded so the controller stays snapped to slopes.")]
        public float groundedStickSpeed = -2f;

        [Header("Zahim — dodge (R51)")]
        [Tooltip("Roll distance, metres.")]
        public float dodgeDistance = 5f;
        [Tooltip("Seconds the roll takes to cover dodgeDistance.")]
        public float dodgeDuration = 0.35f;
        [Tooltip("Seconds of invulnerability from the start of the roll.")]
        public float dodgeInvulnerability = 0.3f;
        [Tooltip("Seconds after the roll before he can act again.")]
        public float dodgeRecovery = 0.12f;
        [Tooltip("Playback multiplier for the roll clip so it fits dodgeDuration + dodgeRecovery.")]
        public float dodgeAnimationSpeed = 3.1f;

        [Header("Zahim — guard (R51)")]
        [Tooltip("Width of the front arc that blocks, degrees.")]
        public float guardArcDegrees = 120f;
        [Tooltip("Move speed while guarding, metres per second.")]
        public float guardMoveSpeed = 3f;
        [Tooltip("Metres a blocked hit pushes him back.")]
        public float guardPushback = 0.4f;
        [Tooltip("Seconds a blocked hit holds him.")]
        public float guardHitSeconds = 0.2f;
        [Tooltip("How fast the shield pose fades in and out, layer weight per second.")]
        public float guardPoseBlendSpeed = 10f;

        [Header("Zahim — hit reactions")]
        [Tooltip("Seconds a flinch holds him before he can act.")]
        public float flinchSeconds = 0.35f;
        [Tooltip("Seconds a knockback, launch or blowaway holds him.")]
        public float knockbackSeconds = 0.8f;
        [Tooltip("Metres a knockback slides him away from the hit.")]
        public float knockbackDistance = 2.5f;
        [Tooltip("Metres a launch or blowaway slides him away from the hit.")]
        public float blowawayDistance = 5f;

        [Header("Zahim — animation")]
        [Tooltip("Seconds of cross-fade into locomotion, dodge, hit and death states.")]
        public float animationCrossFade = 0.1f;

        [Header("Zahim — combat input (R50)")]
        [Tooltip("Seconds a button press stays queued waiting for the combo to accept it.")]
        public float inputBufferSeconds = 0.2f;

        [Header("Zahim — follow camera")]
        [Tooltip("Metres behind the pivot.")]
        public float cameraDistance = 6f;
        [Tooltip("Height of the orbit pivot above his feet, metres.")]
        public float cameraPivotHeight = 1.6f;
        [Tooltip("Starting pitch, degrees (positive looks down).")]
        public float cameraDefaultPitch = 15f;
        [Tooltip("Lowest and highest pitch, degrees.")]
        public Vector2 cameraPitchLimits = new Vector2(-20f, 60f);
        [Tooltip("Degrees per pixel of mouse movement.")]
        public float cameraMouseSensitivity = 0.12f;
        [Tooltip("Degrees per second at full right-stick deflection.")]
        public float cameraStickSpeed = 180f;
        [Tooltip("Degrees per second the camera swings behind him when he raises his guard.")]
        public float cameraRecenterSpeed = 540f;
    }
}
