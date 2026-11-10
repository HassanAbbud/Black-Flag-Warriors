using System.Collections.Generic;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Ahoy.Allies
{
    // W5.6 ally reinforcements: while Zahim is near, keeps real allies fighting at one battle
    // location, at the rate BattleContext.Pacing sets from battle progress. It spawns while the
    // village holds the location, or while its portal camp is being fought over.
    // Allies are pooled (Code Patterns 2.7): created in Awake, deactivated on death, reused.
    public sealed class AllySpawner : MonoBehaviour
    {
        const int GizmoSegments = 48;
        static readonly Color GizmoColor = new Color(0.2f, 0.6f, 1f, 0.8f);

        [SerializeField] AllyUnit prefab;
        [Tooltip("Most allies this spawner can ever have out; the pacing's AllyMaxAlive caps it further.")]
        [SerializeField] int poolSize = 8;
        [Tooltip("The battle location they fight for (R8). Must match a BattleLocation's id.")]
        [SerializeField] int locationId = 1;
        [Tooltip("Zahim.")]
        [SerializeField] Transform player;
        [Tooltip("Zahim within this many metres keeps allies coming.")]
        [SerializeField] float activationRadius = 50f;
        [Tooltip("Allies appear within this many metres of this point.")]
        [SerializeField] float spawnRadius = 6f;
        [Tooltip("Metres a spawn point may move to land on the NavMesh.")]
        [SerializeField] float navMeshSnapDistance = 4f;

        readonly Stack<AllyUnit> free = new Stack<AllyUnit>();
        int aliveCount;
        float nextSpawnAt;

        public int AliveCount => aliveCount;

        void Awake()
        {
            Debug.Assert(prefab != null, "AllySpawner needs an ally prefab", this);
            Debug.Assert(player != null, "AllySpawner needs the player wired in the inspector", this);
            if (prefab == null) return;
            for (int i = 0; i < poolSize; i++)
            {
                AllyUnit ally = Instantiate(prefab, transform.position, transform.rotation, transform);
                ally.ReturnToPool = true;
                ally.Despawned += OnDespawned;
                ally.gameObject.SetActive(false);
                free.Push(ally);
            }
        }

        void Update()
        {
            ISpawnPacing pacing = BattleContext.Pacing;
            int cap = Mathf.Min(poolSize, pacing.AllyMaxAlive);
            if (aliveCount >= cap || free.Count == 0 || Time.time < nextSpawnAt) return;
            if (!PlayerInRange() || !LocationNeedsAllies()) return;

            nextSpawnAt = Time.time + pacing.AllyInterval;
            Vector2 circle = Random.insideUnitCircle * spawnRadius;
            Vector3 position = transform.position + new Vector3(circle.x, 0f, circle.y);
            if (!NavMesh.SamplePosition(position, out NavMeshHit hit, navMeshSnapDistance, NavMesh.AllAreas)) return;

            AllyUnit ally = free.Pop();
            ally.SetHome(locationId);
            ally.transform.SetPositionAndRotation(hit.position, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            ally.gameObject.SetActive(true);
            aliveCount++;
        }

        void OnDespawned(AllyUnit ally)
        {
            aliveCount = Mathf.Max(0, aliveCount - 1);
            free.Push(ally);
        }

        // Held by the village, or being fought over right now (a contested portal camp).
        bool LocationNeedsAllies()
        {
            if (!BattleContext.Progress.TryGetLocation(locationId, out BattleLocationStatus status)) return true;
            return status.Holder == Faction.Village || status.Fighting;
        }

        bool PlayerInRange()
        {
            if (player == null) return false;
            Vector3 to = player.position - transform.position;
            to.y = 0f;
            return to.sqrMagnitude <= activationRadius * activationRadius;
        }

        void OnDrawGizmos()
        {
            Gizmos.color = GizmoColor;
            DrawCircle(spawnRadius);
            DrawCircle(activationRadius);
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
