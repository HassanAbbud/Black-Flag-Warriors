using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // W5.2: where a location's battle stands on the field. The location's strength (R64 units: one
    // Imp, one soldier) is fielded as real crowds, monsters at the entry and village soldiers at the
    // post, fighting each other and Zahim all over the island at once, musou style. Every death
    // comes off the location's strength (BattleEvents.UnitKilled), so the numbers and the field
    // always agree. How many each side fields, and how fast, comes from the crowd director by the
    // front's distance rank (ISpawnPacing, W5.6): the fronts near Zahim are big, far ones small.
    // Only a locked final breach, or the end of the battle, sends the crowds back to the pools.
    // No Update: BattleSimulation ticks every front (Code Patterns 2.8).
    public sealed class BattleFront : MonoBehaviour
    {
        const int MaxPerSide = 128;
        const int SpawnTries = 6;
        const float ClearanceHeight = 1f;   // body height checked for trees and rocks
        const float ClearanceRadius = 0.6f;

        [SerializeField] BattleLocation location;
        [Tooltip("Where the monsters come from: the portal, or the road they march in on.")]
        [SerializeField] Transform monsterEntry;
        [Tooltip("Where the village's soldiers hold the line.")]
        [SerializeField] Transform allyPost;
        [Tooltip("MonsterDef assets, one picked at random per spawn. Each needs a pool in the MonsterManager.")]
        [SerializeField] ScriptableObject[] monsterSpecies;
        [Tooltip("AllySoldierDef assets, one picked at random per spawn. Each needs a pool in the AllyCrowd.")]
        [SerializeField] ScriptableObject[] allySpecies;
        [Tooltip("Metres around the entry and the post that units appear in.")]
        [SerializeField] float spawnSpread = 7f;
        [Tooltip("Props a unit must not appear inside: trees, rocks, buildings.")]
        [SerializeField] LayerMask obstacleMask = 1;

        readonly IUnit[] monsters = new IUnit[MaxPerSide];
        readonly IUnit[] allies = new IUnit[MaxPerSide];
        int monsterCount;
        int allyCount;
        float nextMonsterAt;
        float nextAllyAt;

        public BattleLocation Location => location;
        public bool Active { get; private set; }
        public int FieldMonsters => monsterCount;
        public int FieldAllies => allyCount;

        // Called by BattleSimulation each step while Zahim is near. Rank 0 is the nearest front.
        internal void Tick(float now, int rank, ISpawnPacing pacing)
        {
            Active = true;
            Prune(monsters, ref monsterCount);
            Prune(allies, ref allyCount);

            // Each side fields what the location holds: a garrison, monsters marching in, or the
            // soldiers a village-held neighbour sent to take it.
            int monsterWant = Mathf.Min(pacing.FrontMonsterCap(rank), Mathf.CeilToInt(location.monsters - 0.01f));
            int allyWant = Mathf.Min(pacing.FrontAllyCap(rank), Mathf.CeilToInt(location.allies - 0.01f));

            if (now >= nextMonsterAt && monsterCount < monsterWant)
            {
                nextMonsterAt = now + pacing.FrontMonsterInterval;
                Spawn(Faction.Monsters, monsterSpecies, monsterEntry, monsters, ref monsterCount, monsterWant, pacing.FrontBurst);
            }
            if (now >= nextAllyAt && allyCount < allyWant)
            {
                nextAllyAt = now + pacing.FrontAllyInterval;
                Spawn(Faction.Village, allySpecies, allyPost, allies, ref allyCount, allyWant, pacing.FrontBurst);
            }
        }

        // The breach locked again or the battle ended: everyone still standing goes back to the
        // pools, still counted in the numbers.
        internal void Deactivate()
        {
            if (!Active) return;
            Active = false;
            Release(Faction.Monsters, monsters, ref monsterCount);
            Release(Faction.Village, allies, ref allyCount);
        }

        void Spawn(Faction side, ScriptableObject[] species, Transform at, IUnit[] list, ref int count, int want, int burst)
        {
            if (species == null || species.Length == 0 || at == null) return;
            ISpawnService spawner = BattleContext.Spawner(side);
            for (int n = 0; n < burst && count < want && count < list.Length; n++)
            {
                if (!TryClearPoint(at.position, out Vector3 position)) return;   // all tries blocked: next burst
                Quaternion facing = at.rotation * Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f);
                IUnit unit = spawner.SpawnCrowd(species[Random.Range(0, species.Length)], position, facing, location.LocationId);
                if (unit == null) return;   // pool dry or crowd at capacity: next burst
                list[count++] = unit;
            }
        }

        static void Release(Faction side, IUnit[] list, ref int count)
        {
            ISpawnService spawner = BattleContext.Spawner(side);
            for (int i = 0; i < count; i++)
            {
                if (list[i] != null && list[i].IsAlive) spawner.Despawn(list[i]);
                list[i] = null;
            }
            count = 0;
        }

        // Dead, or taken back to the pool by someone else (a sealed portal clears its ground).
        static void Prune(IUnit[] list, ref int count)
        {
            for (int i = count - 1; i >= 0; i--)
            {
                IUnit unit = list[i];
                bool gone = unit == null || !unit.IsAlive || (unit is Component body && !body.gameObject.activeInHierarchy);
                if (!gone) continue;
                list[i] = list[--count];
                list[count] = null;
            }
        }

        // A random point around the entry or post, on the terrain, with nothing standing in it.
        bool TryClearPoint(Vector3 around, out Vector3 position)
        {
            for (int i = 0; i < SpawnTries; i++)
            {
                Vector2 circle = Random.insideUnitCircle * spawnSpread;
                position = Ground(around + new Vector3(circle.x, 0f, circle.y));
                Vector3 chest = position + Vector3.up * ClearanceHeight;
                if (!Physics.CheckSphere(chest, ClearanceRadius, obstacleMask, QueryTriggerInteraction.Ignore)) return true;
            }
            position = around;
            return false;
        }

        // On the terrain itself, never on a tree or rock collider, so nobody drops from a canopy.
        static Vector3 Ground(Vector3 point)
        {
            Terrain terrain = Terrain.activeTerrain;
            if (terrain == null) return point;
            point.y = terrain.SampleHeight(point) + terrain.transform.position.y;
            return point;
        }

        // Monster entry in red, ally post in blue.
        void OnDrawGizmos()
        {
            if (monsterEntry != null)
            {
                Gizmos.color = new Color(0.9f, 0.2f, 0.2f, 0.8f);
                Gizmos.DrawWireSphere(monsterEntry.position, spawnSpread);
            }
            if (allyPost != null)
            {
                Gizmos.color = new Color(0.2f, 0.5f, 1f, 0.8f);
                Gizmos.DrawWireSphere(allyPost.position, spawnSpread);
            }
            if (monsterEntry != null && allyPost != null)
            {
                Gizmos.color = Color.white;
                Gizmos.DrawLine(monsterEntry.position + Vector3.up, allyPost.position + Vector3.up);
            }
        }
    }
}
