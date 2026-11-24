using System;
using System.Collections.Generic;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace Ahoy.Presentation
{
    // Decides when a tutorial page shows; TutorialPanel draws it and TutorialDef holds the words.
    // Listens to Core news (Code Patterns 2.5): BattleEvents.PortalPhaseChanged and
    // BattleEvents.WeakPointChanged. The level start is its own Start, and a quest being accepted
    // comes from TutorialQuestCue on the quest NPC. Each trigger fires once.
    //
    // A page pauses the game and switches Zahim's PlayerInput off, so neither the world nor the
    // press that closes the page moves him. Input comes back only once that button is released:
    // Dodge and Guard are Value actions, re-read when the map is enabled, and a still-held Space
    // would otherwise roll him the moment play resumes.
    //
    // Time.timeScale is used here for a real pause, never for feel (Code Patterns 3, Time).
    public sealed class TutorialDirector : MonoBehaviour
    {
        enum State { Idle, Reading, Armed, Closing, AwaitingRelease }

        static readonly int TriggerCount = Enum.GetValues(typeof(TutorialTrigger)).Length;

        [SerializeField] TutorialDef def;
        [SerializeField] TutorialPanel panel;
        [Tooltip("Zahim's PlayerInput. Found on the object tagged Player when left empty.")]
        [SerializeField] PlayerInput playerInput;
        [Tooltip("Tag searched for Zahim's PlayerInput when the field above is empty.")]
        [SerializeField] string playerTag = "Player";

        readonly Queue<int> pending = new Queue<int>();
        bool[] fired;
        State state;
        float shownAt;
        float pausedTimeScale = 1f;
        IDisposable anyPress;
        Action<InputControl> onAnyPress;
        InputControl closedWith;

        void Awake()
        {
            Debug.Assert(def != null, "TutorialDirector needs a TutorialDef", this);
            Debug.Assert(panel != null, "TutorialDirector needs its TutorialPanel", this);
            fired = new bool[TriggerCount];
            onAnyPress = OnAnyPress;

            if (playerInput == null)
            {
                GameObject player = GameObject.FindGameObjectWithTag(playerTag);
                if (player != null) playerInput = player.GetComponentInChildren<PlayerInput>();
                if (playerInput == null)
                    Debug.LogWarning($"{name}: no PlayerInput found; the press that closes a page may also reach Zahim.", this);
            }
        }

        void OnEnable()
        {
            BattleEvents.PortalPhaseChanged += OnPortalPhaseChanged;
            BattleEvents.WeakPointChanged += OnWeakPointChanged;
        }

        void OnDisable()
        {
            BattleEvents.PortalPhaseChanged -= OnPortalPhaseChanged;
            BattleEvents.WeakPointChanged -= OnWeakPointChanged;
            StopListening();
            // Never leave the game frozen or Zahim deaf if this object goes away mid-page.
            if (state == State.Reading || state == State.Armed || state == State.Closing) Time.timeScale = pausedTimeScale;
            if (state != State.Idle && playerInput != null) playerInput.ActivateInput();
            pending.Clear();
            state = State.Idle;
        }

        void Start() => Fire(TutorialTrigger.GameStart);

        // ── Triggers ─────────────────────────────────────────────────────────────────────
        // For cues that live on other objects, such as TutorialQuestCue on the quest NPC.
        public void Notify(TutorialTrigger trigger) => Fire(trigger);

        void OnPortalPhaseChanged(int locationId, PortalPhase phase)
        {
            if (phase == PortalPhase.SpawnBoss) Fire(TutorialTrigger.BossSpawned);
            // Camps announce their starting phase on Start; only a seal that follows a boss counts.
            else if (phase == PortalPhase.Sealed && fired[(int)TutorialTrigger.BossSpawned]) Fire(TutorialTrigger.BossDefeated);
        }

        // Telegraphing and Vulnerable are the two states that show the red diamond.
        void OnWeakPointChanged(IUnit unit, WeakPointState weakPoint)
        {
            if (weakPoint == WeakPointState.Telegraphing || weakPoint == WeakPointState.Vulnerable)
                Fire(TutorialTrigger.WeakPointRevealed);
        }

        void Fire(TutorialTrigger trigger)
        {
            int index = (int)trigger;
            if (def == null || fired[index]) return;
            fired[index] = true;

            for (int i = 0; i < def.pages.Length; i++)
                if (def.pages[i].trigger == trigger) pending.Enqueue(i);

            if (pending.Count == 0) return;
            if (state == State.Idle || state == State.AwaitingRelease) Pause();
            if (state != State.Reading && state != State.Armed && state != State.Closing) ShowNext();
        }

        // ── Flow ─────────────────────────────────────────────────────────────────────────
        void Update()
        {
            switch (state)
            {
                case State.Reading:
                    if (Time.unscaledTime - shownAt < def.minShowSeconds) break;
                    panel.ShowPrompt();
                    anyPress = InputSystem.onAnyButtonPress.CallOnce(onAnyPress);
                    SetState(State.Armed);
                    break;

                // The press arrives inside the input update; act on it here, on the main loop.
                case State.Closing:
                    StopListening();
                    if (pending.Count > 0)
                    {
                        ShowNext();
                        break;
                    }
                    panel.Hide();
                    Time.timeScale = pausedTimeScale;
                    SetState(State.AwaitingRelease);
                    break;

                case State.AwaitingRelease:
                    if (closedWith != null && closedWith.IsPressed()) break;
                    if (playerInput != null) playerInput.ActivateInput();
                    closedWith = null;
                    SetState(State.Idle);
                    break;
            }
        }

        void OnAnyPress(InputControl control)
        {
            anyPress = null;   // CallOnce has already removed itself
            if (state != State.Armed) return;
            closedWith = control;
            SetState(State.Closing);
        }

        void Pause()
        {
            if (state == State.Idle)
            {
                pausedTimeScale = Time.timeScale;
                if (playerInput != null) playerInput.DeactivateInput();
            }
            // From AwaitingRelease the input is still off and the old time scale is already kept.
            Time.timeScale = 0f;
        }

        void ShowNext()
        {
            TutorialDef.Page page = def.pages[pending.Dequeue()];
            panel.Show(page.title, page.body, def.continuePrompt, def.fadeInSeconds);
            shownAt = Time.unscaledTime;
            SetState(State.Reading);
        }

        void StopListening()
        {
            anyPress?.Dispose();
            anyPress = null;
        }

        // The single place state changes; breakpoint here.
        void SetState(State next) => state = next;
    }
}
