using System;
using System.Collections.Generic;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;
using Random = UnityEngine.Random;

namespace Ahoy.Portal
{
    // A portal that works as a musou camp:
    //   Open     : dormant until Zahim steps inside triggerRadius
    //   Contested: keeps up to maxAlive monsters spawned around it and counts their deaths
    //   SpawnBoss: once bossThreshold of campSize is killed, the boss comes out
    //   Sealed   : the boss is dead; its leftover minions are removed and the swirl greys out
    // Only monsters this portal spawned move its meter (R8). All tuning lives in PortalCampDef.
    // Known gaps against CODE-PATTERNS, waiting on contracts other tracks own:
    //   deaths are polled through IDamageable.IsAlive until IUnit carries a Died event, and
    //   spawning uses Instantiate until ISpawnService and its pool exist.
    public sealed class PortalCamp : MonoBehaviour
    {
        // A spawned minion: the root to remove on sealing, and the unit that says whether it is dead.
        readonly struct Minion
        {
            public readonly GameObject Root;
            public readonly IDamageable Unit;

            public Minion(GameObject root, IDamageable unit)
            {
                Root = root;
                Unit = unit;
            }
        }

        const float MinDirectionSqr = 0.001f;
        const int GizmoSegments = 48;
        static readonly Color GizmoDormant = Color.yellow;
        static readonly Color GizmoActive = Color.red;
        static readonly Color GizmoSealed = Color.blue;
        static readonly Color GizmoSpawnRing = new Color(1f, 0.5f, 0f, 0.6f);

        // Custom/URP Portal Swirl properties.
        static readonly int ColorAId = Shader.PropertyToID("_ColorA");
        static readonly int ColorBId = Shader.PropertyToID("_ColorB");
        static readonly int GlowId = Shader.PropertyToID("_Glow");
        static readonly int SpeedId = Shader.PropertyToID("_Speed");

        [SerializeField] PortalCampDef def;
        [Tooltip("Zahim.")]
        [SerializeField] Transform player;
        [Tooltip("Where the boss appears. Left empty, the portal itself.")]
        [SerializeField] Transform bossSpawnPoint;
        [Tooltip("The portal's centre mesh, using the Custom/URP Portal Swirl shader. Greyed out when sealed.")]
        [SerializeField] Renderer swirl;

        [Header("Testing")]
        [Tooltip("Current phase. Change it in Play mode to jump straight to a phase; it runs that phase's entry.")]
        [SerializeField] PortalPhase phase = PortalPhase.Open;

        readonly List<Minion> alive = new List<Minion>();
        PortalPhase enteredPhase = PortalPhase.Open;
        MaterialPropertyBlock swirlBlock;
        IDamageable boss;
        int spawned;
        float nextSpawnAt;

        public event Action Activated;
        public event Action BossSpawned;
        public event Action Sealed;
        // Every phase change, including jumps from the Testing dropdown; for visuals such as PortalRing.
        public event Action<PortalPhase> PhaseChanged;

        public PortalCampDef Def => def;
        public PortalPhase Phase => phase;
        public int Kills { get; private set; }
        public int KillsForBoss => Mathf.CeilToInt(def.campSize * def.bossThreshold);
        // 0..1 toward the boss; for a UI meter.
        public float Progress => KillsForBoss > 0 ? Mathf.Clamp01((float)Kills / KillsForBoss) : 1f;

        void Awake()
        {
            Debug.Assert(def != null, "PortalCamp needs a PortalCampDef", this);
            Debug.Assert(player != null, "PortalCamp needs the player wired in the inspector", this);
            Debug.Assert(swirl != null, "PortalCamp needs the portal's swirl renderer to grey it out when sealed", this);
            swirlBlock = new MaterialPropertyBlock();
        }

        void Update()
        {
            // The phase was changed in the inspector; enter it properly so its spawns and events run.
            if (phase != enteredPhase) SetPhase(phase);

            switch (phase)
            {
                case PortalPhase.Open:
                    if (PlayerInRange()) SetPhase(PortalPhase.Contested);
                    break;

                case PortalPhase.Contested:
                    CountDeaths();
                    if (Kills >= KillsForBoss) SetPhase(PortalPhase.SpawnBoss);
                    else SpawnMinions();
                    break;

                case PortalPhase.SpawnBoss:
                    CountDeaths();
                    if (def.spawnDuringBoss) SpawnMinions();
                    // A boss that never spawned is a setup error, not a win; the camp waits.
                    if (boss != null && IsDead(boss)) SetPhase(PortalPhase.Sealed);
                    break;
            }
        }

        // The one place the camp changes phase; each entry runs once.
        void SetPhase(PortalPhase next)
        {
            phase = next;
            enteredPhase = next;
            // Every phase repaints, so jumping back out of Sealed from the inspector restores the swirl.
            PaintSwirl(next == PortalPhase.Sealed);
            switch (next)
            {
                case PortalPhase.Contested:
                    nextSpawnAt = Time.time;
                    Debug.Log($"{name}: camp activated, boss after {KillsForBoss}/{def.campSize} kills", this);
                    Activated?.Invoke();
                    break;

                case PortalPhase.SpawnBoss:
                    Transform point = bossSpawnPoint != null ? bossSpawnPoint : transform;
                    boss = def.bossPrefab != null ? Spawn(def.bossPrefab, OnNavMesh(point.position), point.rotation, out _) : null;
                    if (def.bossPrefab == null) Debug.LogError($"{name}: no boss prefab in {def.name}, the camp cannot be sealed", this);
                    Debug.Log($"{name}: {Kills} kills, the boss is out", this);
                    BossSpawned?.Invoke();
                    break;

                case PortalPhase.Sealed:
                    RemoveMinions();
                    Debug.Log($"{name}: boss defeated, portal sealed", this);
                    Sealed?.Invoke();
                    break;
            }
            PhaseChanged?.Invoke(next);
        }

        // Overrides the swirl's colours on this renderer only, leaving the shared material green.
        void PaintSwirl(bool sealedLook)
        {
            if (swirl == null) return;
            if (!sealedLook)
            {
                swirl.SetPropertyBlock(null);
                return;
            }

            swirl.GetPropertyBlock(swirlBlock);
            swirlBlock.SetColor(ColorAId, def.sealedColorA);
            swirlBlock.SetColor(ColorBId, def.sealedColorB);
            swirlBlock.SetColor(GlowId, def.sealedGlow);
            swirlBlock.SetFloat(SpeedId, def.sealedSwirlSpeed);
            swirl.SetPropertyBlock(swirlBlock);
        }

        bool PlayerInRange()
        {
            if (player == null) return false;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude <= def.triggerRadius * def.triggerRadius;
        }

        // A minion counts once, the first frame it is dead.
        void CountDeaths()
        {
            for (int i = alive.Count - 1; i >= 0; i--)
            {
                if (IsDead(alive[i].Unit))
                {
                    alive.RemoveAt(i);
                    Kills++;
                }
            }
        }

        void SpawnMinions()
        {
            GameObject[] prefabs = def.enemyPrefabs;
            if (prefabs == null || prefabs.Length == 0) return;
            if (spawned >= def.campSize || alive.Count >= def.maxAlive || Time.time < nextSpawnAt) return;

            nextSpawnAt = Time.time + def.spawnInterval;
            GameObject prefab = prefabs[Random.Range(0, prefabs.Length)];
            Vector3 position = RandomSpawnPoint();
            Quaternion facing = Quaternion.LookRotation(Flat(position - transform.position, transform.forward));

            IDamageable unit = Spawn(prefab, position, facing, out GameObject root);
            spawned++;
            if (unit != null) alive.Add(new Minion(root, unit));
        }

        // The camp is won; whatever it still has out goes with it, uncounted.
        void RemoveMinions()
        {
            for (int i = 0; i < alive.Count; i++)
            {
                if (alive[i].Root != null) Destroy(alive[i].Root);
            }
            alive.Clear();
        }

        IDamageable Spawn(GameObject prefab, Vector3 position, Quaternion rotation, out GameObject instance)
        {
            instance = Instantiate(prefab, position, rotation);
            // The unit script may sit on a child, as on prefabs wrapping an imported model.
            IDamageable unit = instance.GetComponentInChildren<IDamageable>();
            if (unit != null) return unit;

            Debug.LogError($"{name}: {prefab.name} has no IDamageable on it or its children, so it can never be killed", this);
            return null;
        }

        // Dead once it stops being alive or is destroyed outright.
        static bool IsDead(IDamageable unit)
        {
            return (unit is Object obj && obj == null) || !unit.IsAlive;
        }

        Vector3 RandomSpawnPoint()
        {
            Vector2 circle = Random.insideUnitCircle.normalized * Random.Range(def.spawnRadius.x, def.spawnRadius.y);
            return OnNavMesh(transform.position + new Vector3(circle.x, 0f, circle.y));
        }

        // NavMeshAgents placed off the mesh never move, so snap to it when there is one nearby.
        Vector3 OnNavMesh(Vector3 position)
        {
            return NavMesh.SamplePosition(position, out NavMeshHit hit, def.navMeshSnapDistance, NavMesh.AllAreas)
                ? hit.position
                : position;
        }

        static Vector3 Flat(Vector3 direction, Vector3 fallback)
        {
            direction.y = 0f;
            return direction.sqrMagnitude > MinDirectionSqr ? direction : fallback;
        }

        // Trigger circle: yellow dormant, red active, blue sealed. Spawn rings in orange.
        void OnDrawGizmos()
        {
            if (def == null) return;
            Gizmos.color = phase switch
            {
                PortalPhase.Open => GizmoDormant,
                PortalPhase.Sealed => GizmoSealed,
                _ => GizmoActive,
            };
            DrawCircle(def.triggerRadius);
            Gizmos.color = GizmoSpawnRing;
            DrawCircle(def.spawnRadius.x);
            DrawCircle(def.spawnRadius.y);
        }

        void DrawCircle(float radius)
        {
            Vector3 center = transform.position;
            Vector3 previous = center + new Vector3(radius, 0f, 0f);
            for (int i = 1; i <= GizmoSegments; i++)
            {
                float angle = i * Mathf.PI * 2f / GizmoSegments;
                Vector3 next = center + new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
