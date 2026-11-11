using System;
using System.Collections.Generic;
using Ahoy.Core;
using UnityEngine;


namespace Ahoy.Enemies
{
    // The Update Manager for the crowd (Code Patterns 2.8): 300 monsters, one Update. Also owns
    // their pools (2.7), sized from BalanceConfig at load, the attack slots that keep the crowd
    // from hitting Zahim all at once, and the crowd queries Zahim and the allies use to find
    // monsters (registered with BattleContext). It is also the monsters' ISpawnService, so
    // portals and battle fronts spawn through BattleContext.Spawner (W5.6). Monsters beyond
    // crowdFullSimRadius think every few frames (R70). Keep this object at the origin with
    // scale 1; the pool is parented to it.
    public sealed class MonsterManager : MonoBehaviour, ICrowdQuery, ISpawnService
    {
        [Serializable]
        struct PoolEntry
        {
            public MonsterDef def;
            public Monster prefab;
            [Tooltip("Fraction of crowdCapacity pooled for this species. Shares may add up to more than 1; capacity still caps what is alive.")]
            [Range(0f, 1f)] public float share;
        }

        [SerializeField] BalanceConfig balance;
        [Tooltip("Zahim's root object. Must carry his IPlayerMotor and IDamageable components.")]
        [SerializeField] GameObject player;
        [SerializeField] PoolEntry[] pools;

        readonly Dictionary<MonsterDef, Stack<Monster>> free = new Dictionary<MonsterDef, Stack<Monster>>();
        // Cached at Prewarm so recycling never calls GetComponent.
        readonly Dictionary<Monster, DestroyedTarget> targets = new Dictionary<Monster, DestroyedTarget>();
        Monster[] active;
        int activeCount;
        int spawnCounter;
        IPlayerMotor playerMotor;
        IDamageable playerTarget;

        // Attack slots: how many monsters are committed to an attack right now.
        int playerAttackers;
        int allyAttackers;
        float nextPlayerAttackAt;

        // Raised for every crowd kill, so Systems subscribes once instead of to 300 monsters (R8, KO count).
        public event Action<IUnit> UnitDied;

        public int ActiveCount => activeCount;
        public int Capacity => active != null ? active.Length : balance.crowdCapacity;
        public int PlayerAttackers => playerAttackers;
        public IVillageTargets Village { get; private set; }

        void Awake()
        {
            playerMotor = player.GetComponent<IPlayerMotor>();
            playerTarget = player.GetComponent<IDamageable>();
            if (playerMotor == null || playerTarget == null)
            {
                Debug.LogError($"{name}: the player object needs IPlayerMotor and IDamageable components.", this);
                enabled = false;
                return;
            }

            Village = BattleContext.Village;
            active = new Monster[balance.crowdCapacity];
            Prewarm();
        }

        // Others find the crowd through BattleContext, never through this class.
        void OnEnable()
        {
            BattleContext.RegisterCrowd(this);
            BattleContext.RegisterSpawner(Faction.Monsters, this);
        }

        void OnDisable()
        {
            BattleContext.UnregisterCrowd(this);
            BattleContext.UnregisterSpawner(Faction.Monsters, this);
        }

        // ── ISpawnService ────────────────────────────────────────────────────────────
        public IUnit SpawnCrowd(ScriptableObject species, Vector3 position, Quaternion rotation, int locationId)
        {
            return species is MonsterDef def ? Spawn(def, position, rotation, locationId) : null;
        }

        // A front Zahim walked away from: back to the pool, not a kill.
        public void Despawn(IUnit unit)
        {
            if (unit is Monster monster && monster.ActiveIndex >= 0) Release(monster, false);
        }

        // The only Instantiate calls, all at load (Code Patterns 0.5).
        void Prewarm()
        {
            for (int i = 0; i < pools.Length; i++)
            {
                PoolEntry entry = pools[i];
                int count = Mathf.CeilToInt(balance.crowdCapacity * entry.share);
                var stack = new Stack<Monster>(count);
                for (int n = 0; n < count; n++)
                {
                    Monster monster = Instantiate(entry.prefab, transform);
                    monster.Bind(this, balance, playerTarget);

                    if (monster.TryGetComponent(out DestroyedTarget target))
                        targets[monster] = target;

                    monster.gameObject.SetActive(false);
                    stack.Push(monster);
                }
                free[entry.def] = stack;
            }
        }

        // Returns null when the crowd is at capacity or the species' pool is dry. Callers
        // skip the spawn; this never Instantiates mid-battle.
        public Monster Spawn(MonsterDef def, Vector3 position, Quaternion rotation, int locationId)
        {
            if (activeCount >= active.Length) return null;
            if (!free.TryGetValue(def, out Stack<Monster> stack) || stack.Count == 0) return null;

            Monster monster = stack.Pop();
            monster.ActiveIndex = activeCount;
            active[activeCount++] = monster;
            monster.Activate(def, locationId, position, rotation, spawnCounter++);
            return monster;
        }

        // Swap-remove, so the list stays contiguous.
        // counted = false is for bulk clears (ReleaseAll / ReleaseLocation): no TargetDestroyed event.
        internal void Release(Monster monster, bool counted = true)
        {
            int index = monster.ActiveIndex;
            if (index < 0) return;

            monster.DropAttackSlot();
            Monster last = active[--activeCount];
            active[index] = last;
            last.ActiveIndex = index;
            active[activeCount] = null;

            monster.ActiveIndex = -1;

            if (counted && targets.TryGetValue(monster, out DestroyedTarget target))
                target.Die();

            monster.gameObject.SetActive(false);
            free[monster.Def].Push(monster);
        }

        public void ReleaseAll()
        {
            for (int i = activeCount - 1; i >= 0; i--) Release(active[i], false);
        }

        // A sealed portal takes its own monsters with it, uncounted.
        public void ReleaseLocation(int locationId)
        {
            for (int i = activeCount - 1; i >= 0; i--)
                if (active[i].LocationId == locationId) Release(active[i], false);
        }

        internal void NotifyDied(Monster monster)
        {
            UnitDied?.Invoke(monster);
            BattleEvents.RaiseUnitKilled(monster, monster.LastAttacker);   // KO count and the battle simulation (R64)
        }

        // ── Attack slots ─────────────────────────────────────────────────────────────
        // Zahim is limited by both a head-count and a minimum gap between attack starts, so
        // a ring of monsters arriving together cannot land a volley. Allies get a separate,
        // looser limit so they last long enough to be worth having.
        internal bool TryAcquireToken(bool onAlly, float now)
        {
            if (onAlly)
            {
                if (allyAttackers >= balance.crowdMaxAttackersOnAllies) return false;
                allyAttackers++;
                return true;
            }

            if (playerAttackers >= balance.crowdMaxAttackersOnPlayer || now < nextPlayerAttackAt) return false;
            playerAttackers++;
            nextPlayerAttackAt = now + balance.crowdAttackStartInterval + UnityEngine.Random.value * balance.crowdAttackStartJitter;
            return true;
        }

        internal void ReturnToken(bool onAlly)
        {
            if (onAlly) allyAttackers = Mathf.Max(0, allyAttackers - 1);
            else playerAttackers = Mathf.Max(0, playerAttackers - 1);
        }

        void Update()
        {
            if (activeCount == 0) return;

            float dt = Time.deltaTime;
            float now = Time.time;
            Vector3 playerPosition = playerMotor.Position;
            bool playerAlive = !playerMotor.IsDead;
            int frame = Time.frameCount;
            float fullSimSqr = balance.crowdFullSimRadius * balance.crowdFullSimRadius;
            int farInterval = Mathf.Max(1, balance.crowdFarTickInterval);

            // Backwards, because a dead monster releases itself and swap-removes.
            // R70: far monsters think every few frames, catching up on the time they saved, and
            // glide along their last velocity in between so they never jump.
            for (int i = activeCount - 1; i >= 0; i--)
            {
                Monster monster = active[i];
                Vector3 offset = monster.transform.position - playerPosition;
                float flatSqr = offset.x * offset.x + offset.z * offset.z;
                if (flatSqr > fullSimSqr && (frame + monster.TickSlot) % farInterval != 0)
                {
                    monster.SavedTime += dt;
                    monster.Glide(dt, frame);
                    continue;
                }
                float step = dt + monster.SavedTime;
                monster.SavedTime = 0f;
                monster.Tick(step, dt, now, playerPosition, playerAlive, frame);
            }
        }

        // ── ICrowdQuery ──────────────────────────────────────────────────────────────
        public int QueryBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, IDamageable[] results)
        {
            Quaternion inverse = Quaternion.Inverse(rotation);
            int found = 0;

            for (int i = 0; i < activeCount && found < results.Length; i++)
            {
                Monster monster = active[i];
                if (!monster.IsAlive) continue;

                Vector3 local = inverse * (monster.transform.position - center);
                float r = monster.BodyRadius;
                if (Mathf.Abs(local.x) <= halfExtents.x + r &&
                    Mathf.Abs(local.y) <= halfExtents.y + r &&
                    Mathf.Abs(local.z) <= halfExtents.z + r)
                    results[found++] = monster;
            }
            return found;
        }

        public bool TryFindNearest(Vector3 from, float maxDistance, out IDamageable nearest)
        {
            nearest = null;
            float best = maxDistance * maxDistance;

            for (int i = 0; i < activeCount; i++)
            {
                Monster monster = active[i];
                if (!monster.IsAlive) continue;

                Vector3 d = monster.transform.position - from;
                d.y = 0f;
                float sqr = d.sqrMagnitude;
                if (sqr < best)
                {
                    best = sqr;
                    nearest = monster;
                }
            }
            return nearest != null;
        }
    }
}