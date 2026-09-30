# Ahoy! Warriors — Code Patterns & Conventions

**Status:** authoritative. **Owner:** Andres. **Applies to:** every human and every AI agent
that writes code in this repository.

If you are an AI agent: read this file and the sections of the Build Spec your card cites
*before* writing any code, then follow the patterns below exactly. When a request conflicts
with this document, say so and ask — do not quietly invent a different structure.

---

## 0. The ten rules

1. **One pattern per problem.** The patterns in section 2 are the whole vocabulary. Do not
   introduce a new architectural pattern without changing this file first.
2. **Contracts live in `Ahoy.Core`.** Every assembly references Core and nothing else.
   Two tracks never reference each other's concrete classes.
3. **Data, not code, for tuning.** Any number a designer might change is a field on a
   ScriptableObject. A magic number in a method body is a bug.
4. **Nothing allocates per frame.** No LINQ, no `new` in `Update`, no `foreach` over
   interfaces in hot loops, no string concatenation, preallocated arrays for physics.
5. **No `Instantiate` or `Destroy` during a battle.** Everything comes from a pool.
6. **No singletons with mutable state.** One service locator, interfaces only.
7. **No `GameObject.Find`, `FindObjectOfType` or `SendMessage` in gameplay code.**
   Wire references in the inspector or resolve them through the locator in `Awake`.
8. **Events announce; they never command.** A system raises what happened. It does not
   reach into UI, audio or another track to make something happen.
9. **Cite the rule.** When code implements a numbered rule from the Build Spec, put the ID
   in a comment: `// R51 dodge i-frames`. Reviewers check the rule, not your memory.
10. **Your scene is yours.** `Battle.unity` belongs to Systems. Everyone else works in
    their own additive test scene. Prefabs, never scene objects.

---

## 1. Project layout

```
Assets/
  Scripts/
    Core/           Ahoy.Core        contracts, enums, structs. No MonoBehaviours.
    Player/         Ahoy.Player      motor, health, attacks, hitstop      (Andres)
    Enemies/        Ahoy.Enemies     monsters, captains, bosses           (Hassan)
    Island/         Ahoy.Island      portals, objectives, simulation      (Systems)
    Ships/          Ahoy.Ships       boat + naval enemies                 (Xiaowei / Siying)
    Presentation/   Ahoy.Presentation HUD, minimap, menus, audio          (Myles)
    Tools/          Ahoy.Tools       debug overlay, editor helpers
  Data/
    Attacks/        AttackDef assets
    Units/          stats assets
    Balance/        BalanceConfig.asset  (the single tuning source)
  Prefabs/
  Scenes/
    Battle.unity          Systems only
    Test_Player.unity     Andres
    Test_Enemies.unity    Hassan
    Test_Sea.unity        Ships
```

Every folder under `Scripts/` has an `.asmdef` named after its namespace. Compile errors
then stay inside one track — and so do merge conflicts.

---

## 2. The patterns we use

### 2.1 Component (Unity's own)
**Use for:** everything on a GameObject.
**Our rule:** one MonoBehaviour, one responsibility, under ~200 lines. `PlayerMotor` moves,
`PlayerHealth` takes damage, `PlayerAttack` swings. If a class needs three "and"s to
describe, split it.
**Anti-pattern:** `PlayerController.cs` that moves, attacks, takes damage and updates the HUD.

### 2.2 State
**Use for:** anything with modes — the player, every monster, portals, the boat.
**Our rule:** an `enum` plus one `switch`, or one class per state when a state owns real
behaviour. Transitions go through a single `SetState` method so there is exactly one place
to breakpoint. Never track mode with a pile of bools (`isAttacking && !isDodging && ...`).
**Example:** `PlayerMotor.MotorState { Locomotion, Dodging, Attacking, Staggered, Dead }`.

### 2.3 Type Object (ScriptableObject as data)
**Use for:** attacks, unit stats, patterns, portal configuration.
**Our rule:** behaviour lives in one class; the *variants* are assets. Adding the C4 launcher
means creating `ATK_C4.asset`, not writing `C4Attack.cs`.
**Example:** `AttackDef` — damage, reaction, hitstop frames, hitbox, and the two links that
make the combo graph.
**Anti-pattern:** a subclass per attack, or a `switch (attackName)` in the attack code.

### 2.4 Command (buffered input)
**Use for:** all player input.
**Our rule:** a press becomes an *intent* with a timestamp, consumed when the game is ready
(0.2 s buffer). Never call gameplay directly from the input callback. This is what makes a
combo feel responsive instead of dropping inputs at the end of an animation.
**Example:** `PlayerAttack.Buffer()` → `TryConsume()`.

### 2.5 Observer (events)
**Use for:** telling other tracks that something happened.
**Our rule:** local news uses a C# `event` on the component (`PlayerHealth.Died`). Battle-wide
news uses the static `BattleEvents` bus owned by Systems. Always unsubscribe in `OnDisable`.
Handlers must not assume ordering.
**Anti-pattern:** `FindObjectOfType<HUD>().SetHealth(...)` from gameplay code.

### 2.6 Service Locator
**Use for:** the handful of battle-wide services: portals, objectives, spawning, pooling.
**Our rule:** one `BattleContext` exposing **interfaces** (`IPortalService`, `ISpawnService`).
Resolve once in `Awake`, cache the reference, never resolve in `Update`. Every service ships
a do-nothing stub so any track can run its own test scene alone.
**Anti-pattern:** `public static PortalManager Instance` with public mutable fields.

### 2.7 Object Pool
**Use for:** monsters, projectiles, hit VFX, damage numbers, arrows, bullet-hell shots.
**Our rule:** pools are created at load, sized from `BalanceConfig`, and objects are
*deactivated*, never destroyed. A pooled object resets its own state in `OnEnable`.
**Budget:** 300 visible monsters, 600 live projectiles at sea, zero GC per frame.

### 2.8 Update Manager
**Use for:** the crowd.
**Our rule:** 300 monsters do **not** have 300 `Update()` methods. One manager iterates a
contiguous list (and later a Burst job) and calls into cheap structs. Only named units —
the player, captains, guardians, giants, the boat — get their own `Update`.
**Anti-pattern:** `MonoBehaviour.Update` on every scuttler.

### 2.9 Flyweight
**Use for:** anything shared by hundreds of instances.
**Our rule:** stats, meshes, materials and clips are *referenced* from a shared asset, never
copied per instance. A scuttler instance holds a pointer to `ALLY_Scuttler.asset` plus its own
three floats (health, position, state).

---

## 3. Conventions

**Naming.** `PascalCase` types and methods, `camelCase` private fields, no `m_` or `_`
prefixes. Assets prefixed by kind: `ATK_`, `ALLY_`, `MON_`, `PTN_`. Animator states match the
`AttackDef.animatorState` string exactly.

**Fields.** `[SerializeField] private` over `public`. Public fields only on ScriptableObject
data classes. Group with `[Header]`, explain units in `[Tooltip]` ("seconds", "metres").

**Animation events.** Prefixed `AE_` and only ever three per attack clip:
`AE_HitboxOpen`, `AE_CancelOpen`, `AE_AttackEnd`. The animator (Siying) adds them; the
programmer never guesses frame numbers in code.

**Animator parameters.** Hash them once into `static readonly int` — never pass strings
per frame.

**Physics.** Layer masks always, `NonAlloc` queries always, preallocated result arrays
always. Crowd monsters have no Rigidbody and no collider on the crowd tier: the *attacker*
queries, the victim does not collide.

**Time.** Use `Time.deltaTime` for gameplay, `WaitForSecondsRealtime` for hitstop. Never
touch `Time.timeScale` for feel effects — it would freeze the entire battlefield.

**Comments.** Explain *why*, and cite the spec rule ID. The code already says what.

---

## 4. Talking to other tracks

| You need | You use | You never |
|---|---|---|
| To damage anything | `IDamageable.TakeDamage(in DamageInfo)` | Cast to `Monster` or `AllyUnit` |
| To know a portal changed hands | `BattleEvents.LocationChanged` | Poll `PortalManager` in `Update` |
| To know the player died | `PlayerHealth.Died` | Check `health <= 0` from outside |
| To spawn anything | `ISpawnService` | `Instantiate` |
| A tuning number | `BalanceConfig` | A literal in a method |

If you need something that is not in `Ahoy.Core`, do not reach across — propose the contract,
add it to Core, and tell the owning track.

---

## 5. Definition of done

A card is done when all six are true:

1. Compiles with no new warnings in your assembly.
2. Runs in your test scene **and** in `Battle.unity`.
3. Every number comes from a ScriptableObject.
4. Zero allocation per frame; no `Instantiate`/`Destroy` mid-battle.
5. A teammate reviewed the pull request.
6. If it changed a rule, the Build Spec was edited in the same PR.

---

## 6. Banned list

- Singletons with public mutable state
- `FindObjectOfType`, `GameObject.Find`, `SendMessage` in gameplay code
- `Time.timeScale` for hitstop or slow-motion feel
- `Update()` on crowd units
- Magic numbers in method bodies
- Inheritance deeper than two levels
- Coroutines that own game state (use the state machine)
- `async void`
- Editing another track's scene or prefab without asking
- A second event bus

---

## 7. Spec rule IDs referenced by player code

| ID | Rule |
|---|---|
| R19–R26 | Weak Point Gauge: exposure, drain values, window, the Smash |
| R50 | Two attack buttons: light chain Y1–Y5, heavy branches C1–C5 |
| R51 | Guard blocks from the front; dodge gives 0.3 s of invulnerability |
| R52 | Lock-on: nearest powerful monster, flick to switch |
| R53 | Special Attack gauge: fills from kills and Force Fragments |
| R54–R55 | Magic gauge, Focus Spirit, Focus Spirit Attack |
| R69–R73 | Performance budget: 300 visible, 60 full-sim, zero GC |
