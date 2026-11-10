using System;
using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // The diamond (R19-R26) on a named unit. BossController finds it through IWeakPoint and
    // reacts to its state; this class owns only the gauge, its window and the Smash timing.
    // It never blinks the boss: a white flash means "you hit it", so only hits show it.
    //
    //   Hidden       — nothing to see.
    //   Vulnerable   — the boss stands open after a punish combo; Zahim's next hit exposes it (R20c).
    //   Telegraphing — the boss is winding up an attack that opens a counter window (the red
    //                  diamond). Zahim's hit, or the counter tool (R20a), exposes it now.
    //   Exposed      — the gauge shows for wpWindowSeconds; Zahim's hits drain it (R22-R23).
    //                  The boss stands staggered for as long as this lasts.
    //   Smashing     — the gauge is empty; Zahim's Smash is on its way (R24). The next hit of his
    //                  after the grace period lands it, or the timeout does (R25).
    //
    // A window that closes before the gauge is empty keeps its progress for wpMemorySeconds (R23).
    [RequireComponent(typeof(BossController))]
    public sealed class WeakPointGauge : MonoBehaviour, IWeakPoint
    {
        [SerializeField] BalanceConfig balance;
        [SerializeField] BossController boss;

        // The Smash connected or timed out. The boss applies its damage (R24).
        public event Action SmashLanded;
        // Local news for the HUD and audio: the gauge appeared, or the window closed unspent.
        public event Action Exposed;
        public event Action Closed;

        float gauge;
        float stateTime;
        float memoryUntil = float.NegativeInfinity;

        public WeakPointState State { get; private set; }
        public float Progress => GaugeMax > 0f ? gauge / GaugeMax : 0f;
        public ToolType? CounterTool => boss.Def.hasCounterTool ? boss.Def.counterTool : (ToolType?)null;
        public float WindowRemaining => State == WeakPointState.Exposed
            ? Mathf.Max(0f, balance.wpWindowSeconds - stateTime)
            : 0f;

        float GaugeMax => boss.Def.tier == Tier.Giant ? balance.wpGaugeGiant : balance.wpGaugeCaptain;

        void Reset() => boss = GetComponent<BossController>();

        void Awake()
        {
            if (boss == null) boss = GetComponent<BossController>();
            gauge = GaugeMax;
        }

        void OnEnable()
        {
            boss.TelegraphStarted += OnTelegraphStarted;
            boss.TelegraphEnded += OnTelegraphEnded;
            boss.Died += OnBossDied;
        }

        void OnDisable()
        {
            boss.TelegraphStarted -= OnTelegraphStarted;
            boss.TelegraphEnded -= OnTelegraphEnded;
            boss.Died -= OnBossDied;
        }

        // ── IWeakPoint ───────────────────────────────────────────────────────────────────
        public bool TryExpose(ExposeSource source)
        {
            if (!boss.IsAlive || State == WeakPointState.Exposed || State == WeakPointState.Smashing) return false;
            // R20a: a tool only counters the wind-up it was thrown into. The thrower has already
            // checked CounterTool; this guards against a throw that arrives after the strike.
            if (source == ExposeSource.ToolCounter && (State != WeakPointState.Telegraphing || CounterTool == null))
                return false;
            // The hit must land inside the wind-up; once the strike starts the window has closed.
            if (source == ExposeSource.TelegraphHit && State != WeakPointState.Telegraphing) return false;

            // R23: a partly drained gauge comes back where it was left, until its memory runs out.
            if (Time.time > memoryUntil) gauge = GaugeMax;
            SetState(WeakPointState.Exposed);
            Exposed?.Invoke();
            return true;
        }

        // R22: only Zahim's blows reach here; the boss filters out allies.
        public void Drain(float amount)
        {
            if (State != WeakPointState.Exposed || amount <= 0f) return;
            gauge = Mathf.Max(0f, gauge - amount);
            if (gauge <= 0f) SetState(WeakPointState.Smashing);   // PlayerAttack starts the Smash on seeing this
        }

        // ── The Smash (R24-R25) ──────────────────────────────────────────────────────────
        // Called by the boss for each of Zahim's hits while Smashing. Hits inside the grace
        // period are the tail of the swing that emptied the gauge; the first one after is the
        // Smash itself. True if this hit landed it.
        public bool TryLandSmash()
        {
            if (State != WeakPointState.Smashing || stateTime < balance.wpSmashGraceSeconds) return false;
            LandSmash();
            return true;
        }

        void LandSmash()
        {
            gauge = GaugeMax;
            memoryUntil = float.NegativeInfinity;   // a spent gauge starts full next time
            SetState(WeakPointState.Hidden);
            SmashLanded?.Invoke();
        }

        // ── Update ───────────────────────────────────────────────────────────────────────
        void Update()
        {
            // The punish window is the boss's; mirror it so the HUD can say "hit it now".
            if (State == WeakPointState.Hidden && boss.PunishWindowOpen) SetState(WeakPointState.Vulnerable);
            else if (State == WeakPointState.Vulnerable && !boss.PunishWindowOpen) SetState(WeakPointState.Hidden);
            if (State != WeakPointState.Exposed && State != WeakPointState.Smashing) return;

            stateTime += Time.deltaTime;
            switch (State)
            {
                case WeakPointState.Exposed:
                    if (stateTime >= balance.wpWindowSeconds)
                    {
                        memoryUntil = Time.time + balance.wpMemorySeconds;   // R23
                        SetState(WeakPointState.Hidden);
                        Closed?.Invoke();
                    }
                    break;

                case WeakPointState.Smashing:
                    if (stateTime >= balance.wpSmashTimeoutSeconds) LandSmash();   // R25
                    break;
            }
        }

        // ── Boss news ────────────────────────────────────────────────────────────────────
        void OnTelegraphStarted(MonsterAttackDef attack)
        {
            if (State == WeakPointState.Hidden && attack != null && attack.opensCounterWindow)
                SetState(WeakPointState.Telegraphing);
        }

        void OnTelegraphEnded()
        {
            if (State == WeakPointState.Telegraphing) SetState(WeakPointState.Hidden);
        }

        void OnBossDied(IUnit _)
        {
            gauge = GaugeMax;
            memoryUntil = float.NegativeInfinity;
            SetState(WeakPointState.Hidden);
        }

        // The single place state changes; breakpoint here.
        void SetState(WeakPointState next)
        {
            State = next;
            stateTime = 0f;
        }
    }
}
