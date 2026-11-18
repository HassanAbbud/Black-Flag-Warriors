using System;
using System.Collections.Generic;
using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // The village's side of the battleground crowd (W5.2): the Update Manager and pools for
    // every AllySoldier (Code Patterns 2.7, 2.8), and the allies' ISpawnService, registered with
    // BattleContext so battle fronts spawn through it. Soldiers beyond crowdFullSimRadius think
    // every few frames (R70). Keep this object at the origin with scale 1; the pool is parented to it.
    public sealed class AllyCrowd : MonoBehaviour, ISpawnService
    {
        [Serializable]
        struct PoolEntry
        {
            public AllySoldierDef def;
            public AllySoldier prefab;
            [Tooltip("Soldiers of this design created at load. Several entries may share a def: their designs mix.")]
            public int count;
        }

        [SerializeField] BalanceConfig balance;
        [Tooltip("Zahim, for the full-sim radius.")]
        [SerializeField] Transform player;
        [SerializeField] PoolEntry[] pools;

        readonly Dictionary<AllySoldierDef, Stack<AllySoldier>> free = new Dictionary<AllySoldierDef, Stack<AllySoldier>>();
        AllySoldier[] active;
        int activeCount;
        int spawnCounter;

        internal float Morale { get; private set; } = 0.5f;   // read once a frame for every soldier

        public int Capacity => active != null ? active.Length : 0;

        void Awake()
        {
            Debug.Assert(balance != null, "AllyCrowd needs the BalanceConfig", this);
            Debug.Assert(player != null, "AllyCrowd needs the player wired in the inspector", this);
            int total = 0;
            for (int i = 0; i < pools.Length; i++) total += Mathf.Max(0, pools[i].count);
            active = new AllySoldier[total];
            Prewarm();
        }

        void OnEnable() => BattleContext.RegisterSpawner(Faction.Village, this);
        void OnDisable() => BattleContext.UnregisterSpawner(Faction.Village, this);

        // The only Instantiate calls, all at load.
        void Prewarm()
        {
            for (int i = 0; i < pools.Length; i++)
            {
                PoolEntry entry = pools[i];
                if (entry.def == null || entry.prefab == null) continue;
                if (!free.TryGetValue(entry.def, out Stack<AllySoldier> stack)) free[entry.def] = stack = new Stack<AllySoldier>(entry.count);
                for (int n = 0; n < entry.count; n++)
                {
                    AllySoldier soldier = Instantiate(entry.prefab, transform);
                    soldier.Bind(this, balance);
                    soldier.gameObject.SetActive(false);
                    stack.Push(soldier);
                }
            }
            foreach (Stack<AllySoldier> stack in free.Values) Shuffle(stack);
        }

        // Designs were pushed one after another; shuffled once so every spawn draws a mix.
        static void Shuffle(Stack<AllySoldier> stack)
        {
            AllySoldier[] items = stack.ToArray();
            for (int i = items.Length - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
            stack.Clear();
            for (int i = 0; i < items.Length; i++) stack.Push(items[i]);
        }

        // ── ISpawnService ────────────────────────────────────────────────────────────
        public IUnit SpawnCrowd(ScriptableObject species, Vector3 position, Quaternion rotation, int locationId)
        {
            if (!(species is AllySoldierDef def) || activeCount >= active.Length) return null;
            if (!free.TryGetValue(def, out Stack<AllySoldier> stack) || stack.Count == 0) return null;

            AllySoldier soldier = stack.Pop();
            soldier.ActiveIndex = activeCount;
            active[activeCount++] = soldier;
            soldier.Activate(def, locationId, position, rotation, spawnCounter++);
            return soldier;
        }

        public void Despawn(IUnit unit)
        {
            if (unit is AllySoldier soldier && soldier.ActiveIndex >= 0) Release(soldier);
        }

        // Swap-remove, so the list stays contiguous.
        internal void Release(AllySoldier soldier)
        {
            int index = soldier.ActiveIndex;
            if (index < 0) return;
            AllySoldier last = active[--activeCount];
            active[index] = last;
            last.ActiveIndex = index;
            active[activeCount] = null;
            soldier.ActiveIndex = -1;
            soldier.gameObject.SetActive(false);
            free[soldier.Def].Push(soldier);
        }

        void Update()
        {
            if (activeCount == 0) return;
            Morale = BattleContext.Progress.Morale01;
            float dt = Time.deltaTime;
            float now = Time.time;
            int frame = Time.frameCount;
            Vector3 at = player != null ? player.position : Vector3.zero;
            float fullSimSqr = balance.crowdFullSimRadius * balance.crowdFullSimRadius;
            int farInterval = Mathf.Max(1, balance.crowdFarTickInterval);

            // Backwards, because a dead soldier releases itself and swap-removes.
            for (int i = activeCount - 1; i >= 0; i--)
            {
                AllySoldier soldier = active[i];
                soldier.StepAnimation(dt, frame);
                Vector3 offset = soldier.transform.position - at;
                float flatSqr = offset.x * offset.x + offset.z * offset.z;
                if (flatSqr > fullSimSqr && (frame + soldier.TickSlot) % farInterval != 0)
                {
                    soldier.SavedTime += dt;
                    soldier.Glide(dt, frame);
                    continue;
                }
                soldier.SetLod(flatSqr, balance);
                float step = dt + soldier.SavedTime;
                soldier.SavedTime = 0f;
                soldier.Tick(step, dt, now, frame);
            }
            Separate(at, fullSimSqr);
        }

        // Soldiers near Zahim never stand inside each other: each overlapping pair moves half the overlap apart.
        // shortcut: O(n²) over the soldiers near Zahim, fine for this pool of ~140; use a grid if pools grow a lot.
        void Separate(Vector3 at, float fullSimSqr)
        {
            for (int i = 0; i < activeCount; i++)
            {
                AllySoldier a = active[i];
                if (!a.IsAlive) continue;
                Vector3 pa = a.transform.position;
                float ax = pa.x - at.x, az = pa.z - at.z;
                if (ax * ax + az * az > fullSimSqr) continue;
                for (int j = i + 1; j < activeCount; j++)
                {
                    AllySoldier b = active[j];
                    if (!b.IsAlive) continue;
                    Vector3 pb = b.transform.position;
                    float dx = pb.x - pa.x, dz = pb.z - pa.z;
                    float min = a.BodyRadius + b.BodyRadius;
                    float sqr = dx * dx + dz * dz;
                    if (sqr >= min * min) continue;
                    float distance = Mathf.Sqrt(sqr);
                    Vector3 push = distance > 0.0001f ? new Vector3(dx / distance, 0f, dz / distance) : new Vector3(1f, 0f, 0f);
                    push *= (min - distance) * 0.5f;
                    pa -= push;
                    b.transform.position = pb + push;
                }
                a.transform.position = pa;
            }
        }
    }
}
