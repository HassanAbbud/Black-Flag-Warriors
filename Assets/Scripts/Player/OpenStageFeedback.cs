using Ahoy.Core;
using Ahoy.Feel;
using UnityEngine;

namespace Ahoy.Player
{
    // Makes Zahim's blows feel heavier while a boss stands open (R22-R26). Only listens and reads
    // the IWeakPoint contract (Observer); the boss and its gauge know nothing about it.
    //   break       — the gauge appears: freeze, big kick, shake, burst on the boss.
    //   open        — the camera holds a zoom (and the open-stage Volume fades in); every swing
    //                 beat that lands freezes longer, shakes harder, kicks the FOV, and adds a burst.
    //   smashing    — the zoom tightens while the Smash is on its way.
    //   smash lands — the biggest freeze, kick and shake, and a burst on the boss.
    // The boss it watches is the one Zahim is locked on to, or else the last one he hit.
    public sealed class OpenStageFeedback : MonoBehaviour
    {
        const string MainCameraTag = "MainCamera";

        [SerializeField] BalanceConfig balance;
        [SerializeField] PlayerAttack attack;
        [Tooltip("Optional. A locked-on boss is watched even before Zahim hits it.")]
        [SerializeField] LockOn lockOn;
        [Tooltip("Zahim's own hitstop, so the open-stage freezes land on his swing.")]
        [SerializeField] HitstopReceiver hitstop;
        [SerializeField] CameraShake shake;
        [Tooltip("On the scene camera. Left empty, it is looked up on the MainCamera-tagged camera.")]
        [SerializeField] CameraZoom zoom;

        [Header("Pools (all optional)")]
        [Tooltip("Extra burst on every hit that lands on an exposed boss.")]
        [SerializeField] VfxPool openHit;
        [Tooltip("Burst on the boss when its weak point is exposed.")]
        [SerializeField] VfxPool breakBurst;
        [Tooltip("Burst on the boss when the Smash lands.")]
        [SerializeField] VfxPool smashBurst;
        [Tooltip("Height above the boss's feet where the break and Smash bursts play, metres.")]
        [SerializeField] float burstHeight = 1.5f;

        IUnit focus;
        Transform focusTransform;
        WeakPointState lastState;

        void Start()
        {
            if (zoom != null) return;
            GameObject main = GameObject.FindWithTag(MainCameraTag);
            if (main != null) zoom = main.GetComponent<CameraZoom>();
        }

        void OnEnable()
        {
            attack.Hit += OnHit;
            if (lockOn != null) lockOn.TargetChanged += OnTargetChanged;
        }

        void OnDisable()
        {
            attack.Hit -= OnHit;
            if (lockOn != null) lockOn.TargetChanged -= OnTargetChanged;
            if (zoom != null) zoom.SetHold(0f, 0f);
        }

        void OnTargetChanged(IUnit unit)
        {
            if (unit != null && unit.WeakPoint != null) Watch(unit);
        }

        void OnHit(HitReport hit)
        {
            if (hit.Target is not IUnit unit || unit.WeakPoint == null) return;
            if (unit != focus && (lockOn == null || !lockOn.HasTarget)) Watch(unit);

            WeakPointState state = unit.WeakPoint.State;
            if (state != WeakPointState.Exposed && state != WeakPointState.Smashing) return;

            Vector3 away = hit.Point - transform.position;
            away.y = 0f;
            Quaternion facing = away.sqrMagnitude > 0f ? Quaternion.LookRotation(away) : transform.rotation;
            if (openHit != null) openHit.Play(hit.Point, facing);

            // Once per swing beat, like CombatFeedback, so a multi-hit does not stack into mush.
            if (!hit.Hitstopped) return;
            // PlayerAttack freezes after raising Hit; Freeze keeps the longer of the two.
            hitstop.Freeze(hit.Attack.hitstopFrames + balance.openHitExtraHitstopFrames);
            shake.AddTrauma(balance.openHitTrauma);
            if (zoom != null) zoom.Punch(balance.openHitFovPunch);
        }

        void Watch(IUnit unit)
        {
            focus = unit;
            focusTransform = unit is Component component ? component.transform : null;
            lastState = unit.WeakPoint.State;
        }

        // Polls one boss's gauge state: one interface read a frame, no allocation.
        void Update()
        {
            if (focus != null && !focus.IsAlive) focus = null;

            WeakPointState state = focus != null && focus.WeakPoint != null
                ? focus.WeakPoint.State
                : WeakPointState.Hidden;

            if (state != lastState) OnStateChanged(lastState, state);
            lastState = state;

            if (zoom == null) return;
            switch (state)
            {
                case WeakPointState.Exposed:
                    zoom.SetHold(balance.openZoomDegrees, balance.openVolumeWeight);
                    break;
                case WeakPointState.Smashing:
                    zoom.SetHold(balance.smashZoomDegrees, 1f);
                    break;
                default:
                    zoom.SetHold(0f, 0f);
                    break;
            }
        }

        void OnStateChanged(WeakPointState from, WeakPointState to)
        {
            if (to == WeakPointState.Exposed)
                Moment(balance.breakHitstopFrames, balance.breakTrauma, balance.breakFovPunch, breakBurst);
            else if (from == WeakPointState.Smashing && to == WeakPointState.Hidden)
                Moment(balance.smashLandHitstopFrames, balance.smashLandTrauma, balance.smashLandFovPunch, smashBurst);
        }

        void Moment(int frames, float trauma, float fovPunch, VfxPool burst)
        {
            hitstop.Freeze(frames);
            shake.AddTrauma(trauma);
            if (zoom != null) zoom.Punch(fovPunch);
            if (burst != null && focusTransform != null)
                burst.Play(focusTransform.position + Vector3.up * burstHeight, focusTransform.rotation);
        }
    }
}
