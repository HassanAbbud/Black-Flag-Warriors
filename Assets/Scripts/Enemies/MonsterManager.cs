using System;
using System.Collections.Generic;
using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // The Update Manager for the crowd (Code Patterns 2.8): 300 monsters, one Update. Also owns
    // their pools (2.7), sized from BalanceConfig at load, and answers the player's sweep
    // through ICrowdQuery. Keep this object at the origin with scale 1; the pool is parented
    // to it.
    public sealed class MonsterManager : MonoBehaviour, ICrowdQuery
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
        Monster[] active;
        int activeCount;
        int spawnCounter;
        IPlayerMotor playerMotor;
        IDamageable playerTarget;

        // Raised for every crowd kill, so Systems subscribes once instead of to 300 monsters (R8, KO count).
        public event Action<IUnit> UnitDied;

        public int ActiveCount => activeCount;

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

            active = new Monster[balance.crowdCapacity];
            Prewarm();
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
        internal void Release(Monster monster)
        {
            int index = monster.ActiveIndex;
            if (index < 0) return;

            Monster last = active[--activeCount];
            active[index] = last;
            last.ActiveIndex = index;
            active[activeCount] = null;

            monster.ActiveIndex = -1;
            monster.gameObject.SetActive(false);
            free[monster.Def].Push(monster);
        }

        public void ReleaseAll()
        {
            for (int i = activeCount - 1; i >= 0; i--) Release(active[i]);
        }

        internal void NotifyDied(Monster monster) => UnitDied?.Invoke(monster);

        void Update()
        {
            if (activeCount == 0) return;

            float dt = Time.deltaTime;
            Vector3 playerPosition = playerMotor.Position;
            bool playerAlive = !playerMotor.IsDead;
            int frame = Time.frameCount;

            // Backwards, because a dead monster releases itself and swap-removes.
            for (int i = activeCount - 1; i >= 0; i--)
                active[i].Tick(dt, playerPosition, playerAlive, frame);
        }

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
    }
}
