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

        [Header("Zahim — attack assist")]
        [Tooltip("Metres within which an attack turns him toward a monster.")]
        public float attackAssistRange = 5f;
        [Tooltip("With the stick held, only monsters within this many degrees of it pull the swing.")]
        public float attackAssistAngle = 60f;

        [Header("Combat feel — shared (Ahoy.Feel)")]
        [Tooltip("Seconds anything flashes when hit.")]
        public float hitFlashSeconds = 0.08f;
        [Tooltip("Camera offset at full trauma, metres.")]
        public float shakeMaxOffset = 0.3f;
        [Tooltip("Camera roll/pitch/yaw at full trauma, degrees.")]
        public float shakeMaxAngle = 3f;
        [Tooltip("Noise speed of the shake, cycles per second.")]
        public float shakeFrequency = 22f;
        [Tooltip("Trauma lost per second (trauma runs 0..1; shake grows with its square).")]
        public float shakeDecayPerSecond = 2.5f;
        [Tooltip("Pooled instances per hit-VFX type, allocated at load.")]
        public int hitVfxPoolSize = 24;

        [Header("Zahim — combat feel")]
        [Tooltip("Trauma added when Zahim takes a hit.")]
        public float playerHitTrauma = 0.45f;
        [Tooltip("Trauma added when his guard blocks a hit.")]
        public float blockTrauma = 0.2f;

        [Header("Zahim — lock-on targeting (R52)")]
        [Tooltip("Right-stick deflection (0..1) that counts as a flick to the next target.")]
        public float lockOnFlickThreshold = 0.8f;
        [Tooltip("Mouse movement in one frame, pixels, that counts as a flick to the next target.")]
        public float lockOnMouseFlickPixels = 30f;
        [Tooltip("Seconds between two target switches, so one flick moves one target.")]
        public float lockOnSwitchCooldown = 0.25f;

        [Header("Weak point (R22–R25) — WP & UI owns the gauge; these are the spec values")]
        [Tooltip("Gauge points for captains and portal guardians.")]
        public float wpGaugeCaptain = 100f;
        [Tooltip("Gauge points for giants.")]
        public float wpGaugeGiant = 300f;
        [Tooltip("Seconds the gauge stays up once exposed (normal difficulty; easy 12, hard 5).")]
        public float wpWindowSeconds = 8f;
        [Tooltip("Seconds a partly drained gauge keeps its progress after the window closes.")]
        public float wpMemorySeconds = 20f;
        [Tooltip("Share of a giant's maximum health one Weak Point Smash removes. Captains die outright.")]
        public float smashGiantHealthFraction = 0.25f;
        [Tooltip("Share of ordinary damage a giant takes while its gauge is not exposed (R35).")]
        public float giantDamageOutsideStun = 0.25f;
        [Tooltip("Seconds after the gauge empties during which further hits still belong to the swing that emptied it, so they do not land the Smash early.")]
        public float wpSmashGraceSeconds = 0.15f;
        [Tooltip("R25: seconds after the gauge empties before the Smash lands on its own. The player never loses a Smash.")]
        public float wpSmashTimeoutSeconds = 3f;

        [Header("Combat feel — boss open stage (R22–R26)")]
        [Tooltip("Extra trauma on each swing beat that lands on an exposed boss, on top of the attack's own.")]
        public float openHitTrauma = 0.2f;
        [Tooltip("Extra frames Zahim freezes on a swing beat that lands on an exposed boss.")]
        public int openHitExtraHitstopFrames = 4;
        [Tooltip("Degrees of field of view a hit on an exposed boss kicks in, then springs back.")]
        public float openHitFovPunch = 2.5f;
        [Tooltip("Degrees the camera stays zoomed in for as long as the boss is exposed.")]
        public float openZoomDegrees = 7f;
        [Tooltip("Weight (0..1) of the open-stage post-processing Volume while the boss is exposed.")]
        public float openVolumeWeight = 0.6f;
        [Tooltip("Degrees the camera zooms in while the Smash is on its way.")]
        public float smashZoomDegrees = 13f;
        [Tooltip("Trauma when the weak point is exposed: the 'break' moment.")]
        public float breakTrauma = 0.45f;
        [Tooltip("Degrees of field of view the break kicks in.")]
        public float breakFovPunch = 5f;
        [Tooltip("Frames Zahim freezes when the break happens.")]
        public int breakHitstopFrames = 6;
        [Tooltip("Trauma when the Smash lands.")]
        public float smashLandTrauma = 0.9f;
        [Tooltip("Degrees of field of view the Smash landing kicks in.")]
        public float smashLandFovPunch = 9f;
        [Tooltip("Frames Zahim freezes when the Smash lands.")]
        public int smashLandHitstopFrames = 10;
        [Tooltip("How fast the held zoom eases in and out (1/seconds; higher is snappier).")]
        public float zoomHoldSharpness = 5f;
        [Tooltip("How fast a field-of-view kick springs back (1/seconds; higher is snappier).")]
        public float zoomPunchRecovery = 9f;

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

        [Header("Enemies — crowd budget (R69–R73)")]
        [Tooltip("Most crowd monsters alive at once. Pools are sized from it and spawns beyond it are refused.")]
        public int crowdCapacity = 300;

        [Header("Enemies — crowd ground following")]
        [Tooltip("Layers the ground probe hits: terrain and level geometry. Leave Zahim's layer out.")]
        public LayerMask groundMask = 1;
        [Tooltip("Metres above a monster the probe starts, so it survives a spawn inside a slope.")]
        public float crowdGroundProbeUp = 2f;
        [Tooltip("Metres below a monster the probe reaches.")]
        public float crowdGroundProbeDown = 4f;
        [Tooltip("Frames between one monster's probes. Monsters are offset so they do not all probe on one frame.")]
        public int crowdGroundProbeInterval = 6;
        [Tooltip("Metres per second a monster rises or sinks to the probed ground; hides the probe stepping.")]
        public float crowdGroundFollowSpeed = 20f;

        [Header("Enemies — shared combat")]
        [Tooltip("Metres added around an enemy hitbox so a hit counts when Zahim's body overlaps it, not just his feet.")]
        public float enemyHitPadding = 0.4f;
        [Tooltip("Downward acceleration for named enemies, metres per second squared (negative).")]
        public float enemyGravity = -25f;
        [Tooltip("Small downward speed kept while grounded so named enemies hug slopes.")]
        public float enemyGroundedStickSpeed = -2f;

        [Header("Enemies — crowd attack budget (musou: most of the crowd only closes in)")]
        [Tooltip("Crowd monsters allowed to be winding up or striking at Zahim at once. The rest circle him.")]
        public int crowdMaxAttackersOnPlayer = 3;
        [Tooltip("Same limit for crowd monsters fighting allies, so allies are not deleted instantly.")]
        public int crowdMaxAttackersOnAllies = 6;
        [Tooltip("Minimum seconds between two crowd attacks starting on Zahim. Spreads hits out instead of a simultaneous volley.")]
        public float crowdAttackStartInterval = 0.5f;
        [Tooltip("Random extra seconds added to that interval.")]
        public float crowdAttackStartJitter = 0.3f;
        [Tooltip("Seconds a monster may hold an attack slot without attacking before it gives it back.")]
        public float crowdTokenHoldSeconds = 2.5f;
        [Tooltip("A monster waits a random 0 to this many seconds after waking before it first wants to attack.")]
        public float crowdFirstAttackDelay = 2f;
        [Tooltip("Each monster's attack cooldown varies by this fraction either way, so they never attack in step.")]
        public float crowdCooldownJitter = 0.5f;

        [Header("Enemies — crowd surround")]
        [Tooltip("Metres from its target at which a waiting monster mills around.")]
        public float crowdRingRadius = 2.6f;
        [Tooltip("Random extra metres per monster, so the crowd is a thick ring, not a circle.")]
        public float crowdRingRadiusSpread = 1.2f;
        [Tooltip("A waiting monster counts as at the ring within this many metres of it.")]
        public float crowdRingTolerance = 0.4f;
        [Tooltip("How far outside the ring (metres) a monster may stand and still ask for an attack slot.")]
        public float crowdAttackRequestRange = 1.5f;
        [Tooltip("Degrees per second a waiting monster drifts around its target.")]
        public float crowdCircleDegreesPerSecond = 25f;
        [Tooltip("Each monster's speed varies by this fraction either way.")]
        public float crowdSpeedJitter = 0.2f;
        [Tooltip("Seconds between a monster re-checking who to fight.")]
        public float crowdRetargetInterval = 0.4f;
        [Tooltip("An ally this close (metres) and nearer than Zahim draws a monster away from him.")]
        public float crowdAllyEngageRange = 5f;

        [Header("Enemies — named units and difficulty")]
        [Tooltip("A named enemy only starts an attack once it faces its target within this many degrees.")]
        public float bossFacingDegrees = 30f;
        [Tooltip("Multiplies all enemy damage. The difficulty dial; 1 is Normal.")]
        public float enemyDamageScale = 1f;
        
        [Header("Lock-on")]
        public float lockOnRange = 15f;           
        public float lockOnBreakRange = 20f;      
        public float lockOnMaxAngle = 50f;        
        public float lockOnDistanceWeight = 1f;  
        public float lockOnSmoothing = 10f;       
        public float lockOnPitch = 15f;         
        public float lockOnTargetHeight = 1f;

        [Header("Healing Potion")]
        public float potionHealAmount = 50f;
    }
}
