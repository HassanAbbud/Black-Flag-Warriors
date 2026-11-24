using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace Ahoy.Island
{
    // W6.1: the battle on the horizon. Musou games draw full units near the player and cheaper ones
    // further out, so the field always looks full and nothing pops in close by. This draws, with GPU
    // instancing, every front's strength that is not already on the field as real units: monsters
    // filling the road from the portal end, soldiers from the village end, meeting at a battle line
    // that slides toward whoever is losing. Two tiers, like Hyrule Warriors' LODs: the real designs
    // (mixed) out to proxyDistance, plain silhouettes beyond it. Nothing is drawn near Zahim, where the
    // real units are. Rebuilt a few times a second from the simulation's numbers; no AI, no allocation.
    // Each unit sways and bobs a little (a 16-step table, one matrix multiply per unit) so the far
    // field looks alive. shortcut: a sway, not a walk cycle; bake vertex animation if it ever has to walk.
    public sealed class BattleBackdrop : MonoBehaviour
    {
        [Serializable]
        struct Look
        {
            public Mesh mesh;
            [Tooltip("One per submesh, with GPU instancing on.")]
            public Material[] materials;
        }

        const int PerLook = 2000;
        const int Batch = 1023;               // instances per draw call
        const float RebuildSeconds = 0.25f;

        [SerializeField] BattleSimulation simulation;
        [Tooltip("Zahim.")]
        [SerializeField] Transform player;
        [Tooltip("The monster designs, mixed.")]
        [SerializeField] Look[] monsterLooks;
        [Tooltip("The soldier designs, mixed.")]
        [SerializeField] Look[] allyLooks;
        [Tooltip("What far monsters become past proxyDistance.")]
        [SerializeField] Look monsterProxy;
        [SerializeField] Look allyProxy;
        [Tooltip("Metres from Zahim past which units are drawn as silhouettes.")]
        [SerializeField] float proxyDistance = 150f;
        [Tooltip("Most backdrop units per side at one front.")]
        [SerializeField] int maxPerSide = 100;
        [Tooltip("Metres around Zahim with no backdrop: the real crowds fight there.")]
        [SerializeField] float hideNear = 60f;
        [Tooltip("Metres each side of the road the backdrop spreads over.")]
        [SerializeField] float roadWidth = 9f;
        [Tooltip("Share of each side camped around its base; the rest line the road. Bases with short roads camp everyone.")]
        [Range(0f, 1f)] [SerializeField] float campShare = 0.4f;
        [Tooltip("Metres around a base its camp spreads over.")]
        [SerializeField] float campRadius = 16f;

        Matrix4x4[][] monsterBuckets;   // one per look, the proxy last
        Matrix4x4[][] allyBuckets;
        int[] monsterCounts;
        int[] allyCounts;
        RenderParams[][] monsterParams;
        RenderParams[][] allyParams;
        readonly Matrix4x4[] batch = new Matrix4x4[Batch];
        const int SwaySteps = 16;
        const float SwayStepSeconds = 0.08f;
        readonly Matrix4x4[] sway = new Matrix4x4[SwaySteps];
        float nextRebuildAt;

        void Awake()
        {
            Debug.Assert(simulation != null && player != null && monsterLooks.Length > 0 && allyLooks.Length > 0, "BattleBackdrop is missing references", this);
            Prepare(monsterLooks, monsterProxy, out monsterBuckets, out monsterCounts, out monsterParams);
            Prepare(allyLooks, allyProxy, out allyBuckets, out allyCounts, out allyParams);
            for (int i = 0; i < SwaySteps; i++)
            {
                float a = i * Mathf.PI * 2f / SwaySteps;
                sway[i] = Matrix4x4.TRS(new Vector3(0f, Mathf.Abs(Mathf.Sin(a)) * 0.08f, 0f), Quaternion.Euler(0f, Mathf.Sin(a) * 6f, Mathf.Sin(a * 2f) * 2f), Vector3.one);
            }
        }

        static void Prepare(Look[] looks, Look proxy, out Matrix4x4[][] buckets, out int[] counts, out RenderParams[][] passes)
        {
            int n = looks.Length + 1;
            buckets = new Matrix4x4[n][];
            counts = new int[n];
            passes = new RenderParams[n][];
            for (int i = 0; i < n; i++)
            {
                Look look = i < looks.Length ? looks[i] : proxy;
                buckets[i] = new Matrix4x4[PerLook];
                passes[i] = new RenderParams[look.materials.Length];
                for (int m = 0; m < look.materials.Length; m++)
                    passes[i][m] = new RenderParams(look.materials[m]) { shadowCastingMode = ShadowCastingMode.Off, receiveShadows = true };
            }
        }

        void Update()
        {
            if (Time.time >= nextRebuildAt)
            {
                nextRebuildAt = Time.time + RebuildSeconds;
                Rebuild();
            }
            Draw(monsterLooks, monsterProxy, monsterParams, monsterBuckets, monsterCounts);
            Draw(allyLooks, allyProxy, allyParams, allyBuckets, allyCounts);
        }

        void Rebuild()
        {
            Array.Clear(monsterCounts, 0, monsterCounts.Length);
            Array.Clear(allyCounts, 0, allyCounts.Length);
            BattleLocation[] locations = simulation.Locations;
            Terrain terrain = Terrain.activeTerrain;
            if (locations == null || terrain == null) return;
            int perSide = Mathf.RoundToInt(maxPerSide * BattleContext.Pacing.CrowdScale);   // R69: thins out when the frame rate drops

            for (int f = 0; f < locations.Length; f++)
            {
                BattleLocation location = locations[f];
                BattleFront front = location.front;
                if (front == null || location.locked || !location.OnLand) continue;
                Vector3[] road = front.Road;
                if (road == null || road.Length < 2) continue;

                int monsters = Mathf.Clamp(Mathf.RoundToInt(location.monsters) - front.FieldMonsters, 0, perSide);
                int allies = Mathf.Clamp(Mathf.RoundToInt(location.allies) - front.FieldAllies, 0, perSide);
                if (monsters + allies == 0) continue;
                // The battle line sits where the two sides' strengths balance along the road.
                float line = Mathf.Clamp(location.monsters / Mathf.Max(0.01f, location.monsters + location.allies), 0.15f, 0.85f);
                if (allies == 0) line = 1f;
                if (monsters == 0) line = 0f;

                // Each side camps around its base (the blobs on a musou map) and lines the road to the fight.
                bool shortRoad = (road[road.Length - 1] - road[0]).sqrMagnitude < campRadius * campRadius * 4f;
                int monsterCamp = shortRoad ? monsters : Mathf.RoundToInt(monsters * campShare);
                int allyCamp = shortRoad ? allies : Mathf.RoundToInt(allies * campShare);
                Vector3 toAllies = front.PostPosition - front.EntryPosition;
                Camp(front.EntryPosition, toAllies, f, 0, monsterCamp, terrain, monsterBuckets, monsterCounts);
                Camp(front.PostPosition, -toAllies, f, 1, allyCamp, terrain, allyBuckets, allyCounts);
                Place(road, f, 0f, line, monsters - monsterCamp, true, terrain, monsterBuckets, monsterCounts);
                Place(road, f, line, 1f, allies - allyCamp, false, terrain, allyBuckets, allyCounts);
            }
        }

        // Spreads count units between road fractions from..to, each at a fixed spot and design per
        // index so they do not jitter between rebuilds; skips any too close to Zahim.
        void Place(Vector3[] road, int seed, float from, float to, int count, bool facingOut, Terrain terrain,
                   Matrix4x4[][] buckets, int[] counts)
        {
            Vector3 at = player.position;
            float hideSqr = hideNear * hideNear;
            float proxySqr = proxyDistance * proxyDistance;
            int proxy = buckets.Length - 1;
            for (int i = 0; i < count; i++)
            {
                float u = Mathf.Lerp(from, to, Hash(seed, i, 1));
                Vector3 point = Along(road, u, out Vector3 forward);
                Vector3 side = new Vector3(forward.z, 0f, -forward.x);
                point += side * ((Hash(seed, i, 2) * 2f - 1f) * roadWidth) + forward * ((Hash(seed, i, 3) * 2f - 1f) * 2f);
                float dx = point.x - at.x, dz = point.z - at.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < hideSqr) continue;
                int bucket = sqr > proxySqr ? proxy : (int)(Hash(seed, i, 5) * proxy) % proxy;
                if (counts[bucket] >= PerLook) continue;
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                Vector3 face = facingOut ? forward : -forward;
                float turn = (Hash(seed, i, 4) * 2f - 1f) * 35f;
                buckets[bucket][counts[bucket]++] = Matrix4x4.TRS(point, Quaternion.LookRotation(face) * Quaternion.Euler(0f, turn, 0f), Vector3.one);
            }
        }

        // A camp: units scattered in a disc around a base, facing the enemy side.
        void Camp(Vector3 center, Vector3 facing, int seed, int side, int count, Terrain terrain, Matrix4x4[][] buckets, int[] counts)
        {
            Vector3 at = player.position;
            float hideSqr = hideNear * hideNear;
            float proxySqr = proxyDistance * proxyDistance;
            int proxy = buckets.Length - 1;
            facing.y = 0f;
            Quaternion look = facing.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(facing) : Quaternion.identity;
            int salt = 10 + side * 10;
            for (int i = 0; i < count; i++)
            {
                float angle = Hash(seed, i, salt) * Mathf.PI * 2f;
                float radius = Mathf.Sqrt(Hash(seed, i, salt + 1)) * campRadius;
                Vector3 point = center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                float dx = point.x - at.x, dz = point.z - at.z;
                float sqr = dx * dx + dz * dz;
                if (sqr < hideSqr) continue;
                int bucket = sqr > proxySqr ? proxy : (int)(Hash(seed, i, salt + 2) * proxy) % proxy;
                if (counts[bucket] >= PerLook) continue;
                point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
                float turn = (Hash(seed, i, salt + 3) * 2f - 1f) * 60f;
                buckets[bucket][counts[bucket]++] = Matrix4x4.TRS(point, look * Quaternion.Euler(0f, turn, 0f), Vector3.one);
            }
        }

        // A point at fraction u of the road's points, and the road's direction there.
        static Vector3 Along(Vector3[] road, float u, out Vector3 forward)
        {
            float scaled = u * (road.Length - 1);
            int i = Mathf.Min((int)scaled, road.Length - 2);
            Vector3 a = road[i], b = road[i + 1];
            forward = b - a;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
            return Vector3.Lerp(a, b, scaled - i);
        }

        // A stable 0..1 number per front, unit and use: the same unit stands in the same place.
        static float Hash(int seed, int index, int salt)
        {
            uint h = (uint)(seed * 73856093) ^ (uint)(index * 19349663) ^ (uint)(salt * 83492791);
            h ^= h >> 13;
            h *= 0x5bd1e995;
            h ^= h >> 15;
            return (h & 0xFFFFFF) / 16777215f;
        }

        void Draw(Look[] looks, Look proxy, RenderParams[][] passes, Matrix4x4[][] buckets, int[] counts)
        {
            for (int b = 0; b < buckets.Length; b++)
            {
                Mesh mesh = b < looks.Length ? looks[b].mesh : proxy.mesh;
                int step = (int)(Time.time / SwayStepSeconds);
                for (int start = 0; start < counts[b]; start += Batch)
                {
                    int n = Mathf.Min(Batch, counts[b] - start);
                    Matrix4x4[] source = buckets[b];
                    for (int k = 0; k < n; k++) batch[k] = source[start + k] * sway[(step + (start + k) * 7) & (SwaySteps - 1)];
                    for (int sub = 0; sub < passes[b].Length && sub < mesh.subMeshCount; sub++)
                        Graphics.RenderMeshInstanced(passes[b][sub], mesh, sub, batch, n);
                }
            }
        }
    }
}
