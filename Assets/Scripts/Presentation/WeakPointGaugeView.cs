using Ahoy.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Ahoy.Presentation
{
    // The diamond over a named unit (R19-R26). Lives on a world-space canvas inside the unit's
    // prefab and reads only the IUnit / IWeakPoint contracts, so it works on any boss.
    //   Telegraphing — the diamond pulses red during a wind-up: hit the boss now to expose it.
    //   Vulnerable   — the same red pulse while it stands open after a punish combo.
    //   Exposed      — the gauge shows and drains; a thin bar under it is the time left.
    //   Smashing     — the gauge is empty; the diamond holds its Smash colour.
    public sealed class WeakPointGaugeView : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;
        [Tooltip("A child holding everything that hides with the weak point. Not this object, or the view stops updating.")]
        [SerializeField] GameObject root;
        [SerializeField] Image diamond;
        [Tooltip("Image Type Filled. fillAmount is the share of the gauge left.")]
        [SerializeField] Image gaugeFill;
        [Tooltip("Image Type Filled. fillAmount is the share of the window left.")]
        [SerializeField] Image windowFill;
        [Tooltip("Optional. The gauge and window bars, shown only while exposed or smashing; the diamond alone shows while the boss is vulnerable or in a counter window.")]
        [SerializeField] GameObject gaugeGroup;

        [Header("Look")]
        [Tooltip("Pulse while a hit now exposes the weak point: during a wind-up or a punish window.")]
        [SerializeField] Color vulnerableColor = new Color(1f, 0.2f, 0.15f);
        [SerializeField] Color exposedColor = new Color(1f, 0.8f, 0.2f);
        [SerializeField] Color smashColor = Color.white;
        [Tooltip("Radians per second of the pulse.")]
        [SerializeField] float pulseSpeed = 18f;
        [Tooltip("Scale added to the diamond at the top of a pulse.")]
        [SerializeField] float pulseScale = 0.25f;

        IUnit unit;
        IWeakPoint weakPoint;
        Transform view;

        void Awake() => unit = GetComponentInParent<IUnit>();

        // The unit resolves its weak point in its own Awake, so read it once everything is up.
        void Start()
        {
            weakPoint = unit?.WeakPoint;
            Camera main = Camera.main;
            if (main != null) view = main.transform;
            if (weakPoint == null) Debug.LogWarning($"{name}: no IWeakPoint on the unit above this view.", this);
            Show(WeakPointState.Hidden);
        }

        void LateUpdate()
        {
            if (weakPoint == null) return;
            if (view != null) transform.rotation = view.rotation;   // billboard

            WeakPointState state = unit.IsAlive ? weakPoint.State : WeakPointState.Hidden;
            Show(state);
            if (state == WeakPointState.Hidden) return;

            switch (state)
            {
                case WeakPointState.Vulnerable:
                case WeakPointState.Telegraphing:
                    Pulse(vulnerableColor);
                    break;

                case WeakPointState.Exposed:
                    diamond.color = exposedColor;
                    diamond.rectTransform.localScale = Vector3.one;
                    gaugeFill.fillAmount = weakPoint.Progress;
                    windowFill.fillAmount = balance.wpWindowSeconds > 0f
                        ? weakPoint.WindowRemaining / balance.wpWindowSeconds
                        : 0f;
                    break;

                case WeakPointState.Smashing:
                    diamond.color = smashColor;
                    diamond.rectTransform.localScale = Vector3.one;
                    gaugeFill.fillAmount = 0f;
                    windowFill.fillAmount = 0f;
                    break;
            }
        }

        void Pulse(Color color)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * pulseSpeed);
            diamond.color = color;
            diamond.rectTransform.localScale = Vector3.one * (1f + pulseScale * pulse);
        }

        void Show(WeakPointState state)
        {
            bool visible = state != WeakPointState.Hidden;
            if (root.activeSelf != visible) root.SetActive(visible);

            bool gauge = state == WeakPointState.Exposed || state == WeakPointState.Smashing;
            if (gaugeGroup != null && gaugeGroup.activeSelf != gauge) gaugeGroup.SetActive(gauge);
        }
    }
}
