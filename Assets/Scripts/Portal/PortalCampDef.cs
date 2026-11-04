using Ahoy.Enemies;
using UnityEngine;

namespace Ahoy.Portal
{
    // Type Object: one asset per kind of camp. PortalCamp holds the behaviour; a small camp, a big
    // camp or a camp with a different boss is a new asset, never a new class.
    [CreateAssetMenu(menuName = "Ahoy/Portal Camp Def", fileName = "PTN_")]
    public sealed class PortalCampDef : ScriptableObject
    {
        [Header("Activation")]
        [Tooltip("Metres from the portal at which Zahim wakes the camp.")]
        public float triggerRadius = 15f;

        [Header("Minions")]
        [Tooltip("Crowd species, spawned from the MonsterManager's pool. Each needs a pool entry there.")]
        public MonsterDef[] monsterSpecies;
        [Tooltip("Non-pooled minions, Instantiated. Never put a Monster prefab here: use monsterSpecies.")]
        public GameObject[] enemyPrefabs;
        [Tooltip("Monsters the camp holds in total; the boss threshold is a share of this.")]
        public int campSize = 50;
        [Tooltip("Most minions alive at once.")]
        public int maxAlive = 12;
        [Tooltip("Seconds between two spawns while below maxAlive.")]
        public float spawnInterval = 0.5f;
        [Tooltip("Minions appear between these distances from the portal, metres.")]
        public Vector2 spawnRadius = new Vector2(2f, 6f);
        [Tooltip("Metres a spawn point may move to land on the NavMesh.")]
        public float navMeshSnapDistance = 4f;
        [Tooltip("Keep sending minions while the boss is out, until campSize have spawned.")]
        public bool spawnDuringBoss = true;

        [Header("Boss")]
        public GameObject bossPrefab;
        [Tooltip("Share of campSize that must be killed before the boss spawns.")]
        [Range(0f, 1f)] public float bossThreshold = 0.6f;

        [Header("Sealed look (Custom/URP Portal Swirl)")]
        [Tooltip("Replaces the swirl's Color A once sealed.")]
        public Color sealedColorA = new Color(0.5f, 0.5f, 0.5f, 1f);
        [Tooltip("Replaces the swirl's Color B once sealed.")]
        public Color sealedColorB = new Color(0.2f, 0.2f, 0.2f, 1f);
        [Tooltip("Replaces the swirl's Center Glow once sealed.")]
        [ColorUsage(true, true)] public Color sealedGlow = new Color(0.3f, 0.3f, 0.3f, 1f);
        [Tooltip("Swirl speed once sealed; 0 freezes it.")]
        public float sealedSwirlSpeed = 0.1f;

        [Header("Ground ring (PortalRing)")]
        [Tooltip("Ring colour before Zahim arrives. Alpha 0 hides it.")]
        public Color ringDormantColor = new Color(1f, 0.9f, 0.2f, 0.6f);
        [Tooltip("Ring colour while the camp is fighting (Contested and SpawnBoss).")]
        public Color ringActiveColor = new Color(1f, 0.1f, 0.1f, 0.9f);
        [Tooltip("Ring colour once sealed.")]
        public Color ringSealedColor = new Color(0.2f, 0.4f, 1f, 0.9f);
        [Tooltip("Line width, metres.")]
        public float ringWidth = 0.3f;
        [Tooltip("Points around the ring; more follows the ground more closely.")]
        public int ringSegments = 64;
        [Tooltip("Metres the ring floats above the ground, so it does not flicker into it.")]
        public float ringGroundOffset = 0.05f;
        [Tooltip("Metres above the portal each point looks down from to find the ground.")]
        public float ringGroundProbeHeight = 10f;
        [Tooltip("Layers the ring sits on. Leave out units and the portal itself.")]
        public LayerMask ringGroundMask = 1;

        void OnValidate()
        {
            triggerRadius = Mathf.Max(0f, triggerRadius);
            campSize = Mathf.Max(1, campSize);
            maxAlive = Mathf.Max(1, maxAlive);
            spawnRadius.x = Mathf.Max(0f, spawnRadius.x);
            spawnRadius.y = Mathf.Max(spawnRadius.x, spawnRadius.y);
            navMeshSnapDistance = Mathf.Max(0f, navMeshSnapDistance);
            ringWidth = Mathf.Max(0f, ringWidth);
            ringSegments = Mathf.Max(3, ringSegments);
            ringGroundProbeHeight = Mathf.Max(0f, ringGroundProbeHeight);
        }
    }
}
