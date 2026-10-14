using Ahoy.Core;
using Ahoy.Feel;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Testing
{
    // Throwaway giant for testing lock-on (R52) and the weak point loop, with the rhythm of a
    // Hyrule Warriors giant boss. The real giants belong to AI and the real gauge to WP & UI;
    // this stub implements IUnit and IWeakPoint just enough to fight, and shows how they behave:
    //   swipe   — wide arm sweep in front; guard it
    //   crush   — the opening attack (R33): glows red while it winds up, cannot be guarded,
    //             launches; dodge it
    //   punish  — after the crush it stands open (yellow); hit it then and the gauge appears (R20c)
    //   exposed — stunned and flashing; every hit drains DamageInfo.WeakPointDrain for 8 s (R22–R23)
    //   smash   — gauge empty: Zahim's Weak Point Smash lands for 25% of max health (R24)
    // Outside the stun it takes a quarter of ordinary damage (R35). F4 exposes it at once,
    // standing in for a tool counter or Focus Spirit Attack. Delete before submission.
    public sealed class TrainingBoss : MonoBehaviour, IUnit, IWeakPoint
    {
        enum Phase { Chase, Swipe, Crush, Punish, Exposed, Smashing, Downed, Dead }

        static readonly int IdleState = Animator.StringToHash("Idle");
        static readonly int WalkState = Animator.StringToHash("Walk");
        static readonly int SwipeState = Animator.StringToHash("Swipe");
        static readonly int CrushState = Animator.StringToHash("Crush");
        static readonly int StunnedState = Animator.StringToHash("Stunned");
        static readonly int DieState = Animator.StringToHash("Die");
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] BalanceConfig balance;
        [SerializeField] Animator animator;
        [SerializeField] HitstopReceiver hitstop;
        [SerializeField] HitFlash flash;
        [Tooltip("Zahim. Must have an IDamageable.")]
        [SerializeField] GameObject player;
        [Tooltip("Giant: smash takes 25% of max health. Captain or Guardian: the smash kills.")]
        [SerializeField] Tier tier = Tier.Giant;
        [Tooltip("Test value; the spec giant has 12000.")]
        [SerializeField] float maxHealth = 6000f;

        [Header("Movement")]
        [SerializeField] float walkSpeed = 2f;
        [SerializeField] float turnDegreesPerSecond = 90f;
        [Tooltip("Metres from Zahim where it stops walking and attacks.")]
        [SerializeField] float engageRange = 4.5f;

        [Header("Swipe (golem Attack02)")]
        [SerializeField] float swipeSeconds = 2f;
        [SerializeField] float swipeImpactAt = 0.9f;
        [SerializeField] float swipeRange = 5.5f;
        [SerializeField] float swipeArcDegrees = 160f;
        [SerializeField] float swipeDamage = 120f;

        [Header("Crush (golem Attack01) — the opening attack")]
        [SerializeField] float crushSeconds = 2.67f;
        [Tooltip("When the red glow starts; the wind-up shows from here to the impact.")]
        [SerializeField] float crushTelegraphFrom = 0.3f;
        [SerializeField] float crushImpactAt = 1.75f;
        [Tooltip("It stops turning toward Zahim here, so a late dodge still works.")]
        [SerializeField] float crushCommitAt = 1.2f;
        [SerializeField] float crushRange = 5.5f;
        [SerializeField] float crushArcDegrees = 70f;
        [SerializeField] float crushDamage = 250f;
        [Tooltip("Seconds it stands open after the crush (R20c punish window).")]
        [SerializeField] float punishSeconds = 2.5f;

        [Header("Rhythm")]
        [SerializeField] float attackCooldown = 1.2f;
        [SerializeField] int hitstopFrames = 8;
        [SerializeField] float downedSeconds = 2.5f;
        [Tooltip("Hits closer than this to the gauge emptying belong to the swing that emptied it.")]
        [SerializeField] float smashGraceSeconds = 0.15f;
        [Tooltip("R25: if the Smash never connects, it lands anyway. The player never loses a Smash.")]
        [SerializeField] float smashTimeoutSeconds = 3f;
        [SerializeField] float exposedFlashInterval = 0.3f;
        [SerializeField] float respawnSeconds = 5f;
        [Tooltip("Length of the golem's Die clip; it loops, so it is held on its last frame.")]
        [SerializeField] float dieClipSeconds = 1.15f;
        [SerializeField] Color telegraphColor = new Color(1f, 0.35f, 0.3f);
        [SerializeField] Color punishColor = new Color(1f, 0.9f, 0.4f);

        Renderer[] renderers;
        MaterialPropertyBlock block;
        IDamageable target;

        Phase phase;
        float phaseTime;
        bool impactDone;
        int attacksStarted;
        float health;
        float gauge;
        float memoryUntil = float.NegativeInfinity;
        float nextFlash;
        int playing;

        // ── IUnit ─────────────────────────────────────────────────────────────────────────
        public Faction Faction => Faction.Monsters;
        public bool IsAlive => phase != Phase.Dead;
        public Tier Tier => tier;
        public int LocationId => -1;
        public float Strength => 0f;
        public IWeakPoint WeakPoint => this;

        // ── IWeakPoint ────────────────────────────────────────────────────────────────────
        public WeakPointState State { get; private set; }
        public float Progress => gauge / GaugeMax;
        public ToolType? CounterTool => ToolType.Bomb;   // modelled on King Dodongo
        public float WindowRemaining => State == WeakPointState.Exposed ? Mathf.Max(0f, balance.wpWindowSeconds - phaseTime) : 0f;

        float GaugeMax => tier == Tier.Giant ? balance.wpGaugeGiant : balance.wpGaugeCaptain;

        public bool TryExpose(ExposeSource source)
        {
            if (!IsAlive || State == WeakPointState.Exposed || State == WeakPointState.Smashing) return false;

            // R23: a partly drained gauge keeps its progress for a while.
            if (Time.time > memoryUntil) gauge = GaugeMax;
            State = WeakPointState.Exposed;
            SetPhase(Phase.Exposed, StunnedState);
            Debug.Log($"{name}: weak point exposed ({source}), gauge {gauge:0}/{GaugeMax:0}", this);
            return true;
        }

        public void Drain(float amount)
        {
            if (State != WeakPointState.Exposed || amount <= 0f) return;
            gauge = Mathf.Max(0f, gauge - amount);
            if (gauge > 0f) return;

            State = WeakPointState.Smashing;
            SetPhase(Phase.Smashing, StunnedState);
        }

        // ── IDamageable ───────────────────────────────────────────────────────────────────
        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            hitstop.Freeze(info.HitstopFrames);
            flash.Flash();

            if (phase == Phase.Smashing)
            {
                if (phaseTime >= smashGraceSeconds) ResolveSmash();
                return;
            }
            if (phase == Phase.Punish || (phase == Phase.Crush && impactDone)) TryExpose(ExposeSource.PunishWindow);

            bool stunned = State == WeakPointState.Exposed;
            float amount = tier == Tier.Giant && !stunned ? info.Amount * balance.giantDamageOutsideStun : info.Amount;
            health -= amount;
            if (health <= 0f)
            {
                Die();
                return;
            }
            if (stunned) Drain(info.WeakPointDrain);
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────────────────
        void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            target = player.GetComponent<IDamageable>();
            Respawn();
        }

        void Update()
        {
            if (Keyboard.current != null && Keyboard.current.f4Key.wasPressedThisFrame) TryExpose(ExposeSource.FocusSpiritAttack);
            if (hitstop.IsFrozen) return;

            float dt = Time.deltaTime;
            phaseTime += dt;

            switch (phase)
            {
                case Phase.Chase:
                    float distance = Turn(dt, true);
                    if (distance > engageRange)
                    {
                        Play(WalkState);
                        transform.position += transform.forward * (walkSpeed * dt);
                    }
                    else
                    {
                        Play(IdleState);
                        if (phaseTime >= attackCooldown) StartAttack();
                    }
                    break;

                case Phase.Swipe:
                    if (phaseTime < swipeImpactAt) Turn(dt, true);
                    if (!impactDone && phaseTime >= swipeImpactAt)
                    {
                        impactDone = true;
                        Strike(swipeRange, swipeArcDegrees, swipeDamage, HitReaction.Knockback, false);
                    }
                    if (phaseTime >= swipeSeconds) SetPhase(Phase.Chase, IdleState);
                    break;

                case Phase.Crush:
                    if (phaseTime < crushCommitAt) Turn(dt, true);
                    if (!impactDone && phaseTime >= crushImpactAt)
                    {
                        impactDone = true;
                        State = WeakPointState.Hidden;
                        Strike(crushRange, crushArcDegrees, crushDamage, HitReaction.Launch, true);
                    }
                    if (phaseTime >= crushSeconds) SetPhase(Phase.Punish, IdleState);
                    break;

                case Phase.Punish:
                    if (phaseTime >= punishSeconds) SetPhase(Phase.Chase, IdleState);
                    break;

                case Phase.Exposed:
                    if (Time.time >= nextFlash)
                    {
                        nextFlash = Time.time + exposedFlashInterval;
                        flash.Flash();   // R26: flashes white while exposed
                    }
                    if (phaseTime >= balance.wpWindowSeconds)
                    {
                        State = WeakPointState.Hidden;
                        memoryUntil = Time.time + balance.wpMemorySeconds;
                        SetPhase(Phase.Chase, IdleState);
                    }
                    break;

                case Phase.Smashing:
                    if (phaseTime >= smashTimeoutSeconds) ResolveSmash();
                    break;

                case Phase.Downed:
                    if (phaseTime >= downedSeconds) SetPhase(Phase.Chase, IdleState);
                    break;

                case Phase.Dead:
                    if (phaseTime >= dieClipSeconds) animator.speed = 0f;
                    if (phaseTime >= respawnSeconds) Respawn();
                    break;
            }

            Paint();
        }

        void StartAttack()
        {
            // Every second attack is the opening one, so the loop comes round quickly in a test.
            bool crush = attacksStarted++ % 2 == 1;
            impactDone = false;
            if (crush) State = WeakPointState.Telegraphing;
            SetPhase(crush ? Phase.Crush : Phase.Swipe, crush ? CrushState : SwipeState);
        }

        // Hits Zahim if he is inside the arc in front. The giant never queries anything else.
        void Strike(float range, float arcDegrees, float damage, HitReaction reaction, bool unblockable)
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > range * range || Vector3.Angle(transform.forward, to) > arcDegrees * 0.5f) return;

            var info = new DamageInfo(damage, transform.position, reaction, hitstopFrames, 0f, this, unblockable);
            target.TakeDamage(in info);
        }

        void ResolveSmash()
        {
            State = WeakPointState.Hidden;
            gauge = GaugeMax;
            memoryUntil = float.NegativeInfinity;

            // R24: a captain or guardian dies; a giant loses a quarter of its maximum health.
            health = tier == Tier.Giant ? health - maxHealth * balance.smashGiantHealthFraction : 0f;
            Debug.Log($"{name}: Weak Point Smash! {Mathf.Max(0f, health):0}/{maxHealth:0} left", this);
            if (health <= 0f) Die();
            else SetPhase(Phase.Downed, StunnedState);
        }

        void Die()
        {
            health = 0f;
            State = WeakPointState.Hidden;
            SetPhase(Phase.Dead, DieState);
            Debug.Log($"{name}: defeated", this);
        }

        void Respawn()
        {
            animator.speed = 1f;
            health = maxHealth;
            gauge = GaugeMax;
            memoryUntil = float.NegativeInfinity;
            attacksStarted = 0;
            State = WeakPointState.Hidden;
            SetPhase(Phase.Chase, IdleState);
        }

        void SetPhase(Phase next, int animatorState)
        {
            phase = next;
            phaseTime = 0f;
            Play(animatorState);
        }

        void Play(int animatorState)
        {
            if (playing == animatorState && animatorState != SwipeState && animatorState != CrushState) return;
            playing = animatorState;
            animator.CrossFadeInFixedTime(animatorState, 0.15f, 0, 0f);
        }

        // Turns toward Zahim; returns the flat distance to him.
        float Turn(float dt, bool turn)
        {
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            if (turn && to.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(to),
                                                              turnDegreesPerSecond * dt);
            return to.magnitude;
        }

        // Red while the opening attack winds up, yellow while it stands open.
        void Paint()
        {
            Color color = Color.white;
            if (phase == Phase.Crush && !impactDone && phaseTime >= crushTelegraphFrom)
                color = Color.Lerp(Color.white, telegraphColor, 0.5f + 0.5f * Mathf.Sin(phaseTime * 18f));
            else if (phase == Phase.Punish || (phase == Phase.Crush && impactDone))
                color = punishColor;

            block.SetColor(BaseColor, color);
            for (int i = 0; i < renderers.Length; i++) renderers[i].SetPropertyBlock(block);
        }

        // Test-only readout: health, the diamond as a bar, and what it is doing.
        void OnGUI()
        {
            const float width = 400f;
            float x = (Screen.width - width) * 0.5f;
            GUI.Box(new Rect(x - 10f, 10f, width + 20f, 74f), GUIContent.none);
            GUI.Label(new Rect(x, 14f, width, 20f), $"{name} ({tier})  {Mathf.Max(0f, health):0} / {maxHealth:0}   [{phase}]");
            DrawBar(new Rect(x, 34f, width, 12f), health / maxHealth, new Color(0.85f, 0.2f, 0.2f));

            string label = State switch
            {
                WeakPointState.Telegraphing => "Opening attack! Dodge it",
                WeakPointState.Exposed => $"Weak point ◆ {gauge:0}/{GaugeMax:0}  ({WindowRemaining:0.0}s)",
                WeakPointState.Smashing => "WEAK POINT SMASH!",
                _ => phase == Phase.Punish ? "Open — hit it!" :
                     Time.time <= memoryUntil ? $"Weak point remembered {gauge:0}/{GaugeMax:0}" : "F4: expose (test)",
            };
            GUI.Label(new Rect(x, 50f, width, 20f), label);
            if (State == WeakPointState.Exposed || State == WeakPointState.Smashing || Time.time <= memoryUntil)
                DrawBar(new Rect(x, 70f, width, 8f), Progress, new Color(1f, 0.8f, 0.2f));
        }

        static void DrawBar(Rect rect, float fill, Color color)
        {
            Color previous = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = color;
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(fill), rect.height), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
