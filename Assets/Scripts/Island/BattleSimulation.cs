using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // W5.2 Battleground Simulation: the whole island's battle, including the fights Zahim cannot see.
    //   Open portals send reinforcement waves; monsters beyond a location's hold march on along its
    //   route toward the village. Where both sides meet out of Zahim's sight, each loses strength in
    //   proportion to the other's (R64). A village-held location the monsters retake changes hands,
    //   so a sealed portal left undefended reopens; monsters in an undefended village wear down its
    //   integrity, and the battle is lost at zero.
    //   Where Zahim stands the real units fight instead: their deaths (BattleEvents.UnitKilled)
    //   come off that location's strength, and its portal camp reports its own phase and meter.
    // Registers as IBattleProgress so the HUD and the spawn pacing read it through BattleContext.
    // One Update for every location (Code Patterns 2.8), stepping every tickSeconds; no allocation.
    public sealed class BattleSimulation : MonoBehaviour, IBattleProgress
    {
        [SerializeField] BattleSimDef def;
        [Tooltip("Zahim.")]
        [SerializeField] Transform player;
        [Tooltip("The village the monsters march on. Must also be in Locations.")]
        [SerializeField] BattleLocation village;
        [SerializeField] BattleLocation[] locations;

        int[] indexById;
        float stepTimer;
        float integrity;
        float battleSeconds;
        int koCount;
        int portalCount;
        int sealedCount;
        int heldCount;
        int seenId = -1;
        bool ended;

        // ── IBattleProgress ──────────────────────────────────────────────────────────
        public int LocationCount => locations.Length;
        public BattleLocationStatus GetLocation(int index) => locations[index].Status();

        public bool TryGetLocation(int locationId, out BattleLocationStatus status)
        {
            BattleLocation location = Find(locationId);
            status = location != null ? location.Status() : default;
            return location != null;
        }

        public int PortalCount => portalCount;
        public int SealedCount => sealedCount;
        public int HeldCount => heldCount;
        public float Progress01 => locations.Length > 1 ? (float)heldCount / (locations.Length - 1) : 0f;
        public float VillageIntegrity01 => def != null ? integrity / def.villageIntegrity : 1f;
        public int KoCount => koCount;
        public float BattleSeconds => battleSeconds;
        public int SeenLocationId => seenId;
        public bool Ended => ended;

        void Awake()
        {
            Debug.Assert(def != null, "BattleSimulation needs a BattleSimDef", this);
            Debug.Assert(player != null, "BattleSimulation needs the player wired in the inspector", this);
            Debug.Assert(village != null, "BattleSimulation needs the village location", this);

            int maxId = 0;
            for (int i = 0; i < locations.Length; i++)
            {
                Debug.Assert(locations[i] != null && locations[i].Def != null, $"Location {i} is empty or has no def", this);
                maxId = Mathf.Max(maxId, locations[i].LocationId);
            }
            indexById = new int[maxId + 1];
            for (int i = 0; i < indexById.Length; i++) indexById[i] = -1;
            for (int i = 0; i < locations.Length; i++)
            {
                int id = locations[i].LocationId;
                if (indexById[id] >= 0) Debug.LogError($"{name}: two locations use id {id}", locations[i]);
                indexById[id] = i;
                if (locations[i].Def.kind != LocationKind.Village && locations[i].Def.kind != LocationKind.Stronghold
                    && locations[i].Def.kind != LocationKind.Outpost) portalCount++;
            }
            ResetBattle();
        }

        void OnEnable()
        {
            BattleContext.RegisterProgress(this);
            BattleEvents.UnitKilled += OnUnitKilled;
            BattleEvents.PortalPhaseChanged += OnPortalPhaseChanged;
            BattleEvents.PortalMeterChanged += OnPortalMeterChanged;
        }

        void OnDisable()
        {
            BattleContext.UnregisterProgress(this);
            BattleEvents.UnitKilled -= OnUnitKilled;
            BattleEvents.PortalPhaseChanged -= OnPortalPhaseChanged;
            BattleEvents.PortalMeterChanged -= OnPortalMeterChanged;
        }

        void ResetBattle()
        {
            for (int i = 0; i < locations.Length; i++) locations[i].ResetState();
            integrity = def.villageIntegrity;
            battleSeconds = 0f;
            koCount = 0;
            ended = false;
            stepTimer = 0f;
            Recount();
        }

        void Update()
        {
            if (ended) return;
            battleSeconds += Time.deltaTime;
            stepTimer += Time.deltaTime;
            if (stepTimer < def.tickSeconds) return;
            stepTimer -= def.tickSeconds;
            Step(def.tickSeconds);
        }

        // One step of the whole island, in a fixed order so a wave never skips a location.
        void Step(float dt)
        {
            ISpawnPacing pacing = BattleContext.Pacing;
            UpdateSeen();

            for (int i = 0; i < locations.Length; i++) Reinforce(locations[i], dt, pacing);
            for (int i = 0; i < locations.Length; i++) March(locations[i], dt);
            for (int i = 0; i < locations.Length; i++)
            {
                BattleLocation location = locations[i];
                location.monsters = Mathf.Min(location.monsters + location.incoming, location.Def.maxMonsterStrength);
                location.incoming = 0f;
            }
            for (int i = 0; i < locations.Length; i++)
            {
                Fight(locations[i], dt);
                Settle(locations[i]);
            }
            WearVillage(dt);
            Recount();
        }

        // Zahim is at the nearest location whose seen radius he is inside.
        void UpdateSeen()
        {
            seenId = -1;
            float best = float.MaxValue;
            Vector3 at = player != null ? player.position : new Vector3(float.MaxValue, 0f, 0f);
            for (int i = 0; i < locations.Length; i++)
            {
                BattleLocation location = locations[i];
                Vector3 to = at - location.transform.position;
                to.y = 0f;
                float sqr = to.sqrMagnitude;
                float radius = location.Def.seenRadius;
                location.seen = false;
                if (sqr <= radius * radius && sqr < best)
                {
                    best = sqr;
                    seenId = location.LocationId;
                }
            }
            if (seenId >= 0) locations[indexById[seenId]].seen = true;
        }

        // Waves from monster-held locations, and garrisons regrowing in village-held ones.
        void Reinforce(BattleLocation location, float dt, ISpawnPacing pacing)
        {
            BattleLocationDef d = location.Def;
            if (location.holder == Faction.Monsters)
            {
                if (d.waveStrength <= 0f || location.locked) return;
                location.waveTimer -= dt;
                if (location.waveTimer > 0f) return;
                location.waveTimer += d.waveInterval;
                location.monsters = Mathf.Min(location.monsters + d.waveStrength * pacing.WaveStrengthScale, d.maxMonsterStrength);
                return;
            }
            location.allies = Mathf.Min(location.allies + d.allyRegen * pacing.AllyRegenScale * dt, d.maxAllyStrength);
        }

        // Pressure from the open portals (GDD Core Loop): what a location cannot hold moves on.
        void March(BattleLocation location, float dt)
        {
            BattleLocation next = location.Next;
            if (next == null || location.locked) return;
            float spare = location.monsters - location.Def.holdStrength;
            if (spare <= 0f) return;
            float moving = Mathf.Min(spare, location.Def.marchRate * dt);
            location.monsters -= moving;
            next.incoming += moving;
        }

        // R64: out of sight, each side loses strength in proportion to the other's.
        // Where Zahim stands the real units fight, and their deaths arrive through OnUnitKilled.
        void Fight(BattleLocation location, float dt)
        {
            float empty = def.emptyStrength;
            location.fighting = (location.monsters > empty && location.allies > empty)
                                || (location.hasCamp && location.seen
                                    && (location.campPhase == PortalPhase.Contested || location.campPhase == PortalPhase.SpawnBoss));
            if (location.seen || location.monsters <= empty || location.allies <= empty) return;

            float monsterLoss = location.allies * def.killRate * dt;
            float allyLoss = location.monsters * def.killRate * dt;
            location.monsters = Mathf.Max(0f, location.monsters - monsterLoss);
            location.allies = Mathf.Max(0f, location.allies - allyLoss);
        }

        // Who holds it after the fighting.
        void Settle(BattleLocation location)
        {
            float empty = def.emptyStrength;
            BattleLocationDef d = location.Def;

            if (location.holder == Faction.Village)
            {
                // The village itself never changes hands; it loses integrity instead.
                if (d.kind == LocationKind.Village || location.seen) return;
                if (location.allies <= empty && location.monsters >= d.captureStrength)
                    SetHolder(location, Faction.Monsters);
                return;
            }

            // Portals are only sealed by beating their guardian (PortalCamp reports it). Outposts and
            // strongholds fall back to the village once Zahim clears them in person.
            bool portal = d.kind == LocationKind.Portal || d.kind == LocationKind.SeaPortal || d.kind == LocationKind.FinalBreach;
            if (!portal && location.seen && location.monsters <= empty) SetHolder(location, Faction.Village);
            location.meter = d.maxMonsterStrength > 0f && !location.hasCamp
                ? 1f - Mathf.Clamp01(location.monsters / d.maxMonsterStrength)
                : location.meter;
        }

        void WearVillage(float dt)
        {
            if (village == null || ended) return;
            if (village.allies > def.emptyStrength || village.monsters <= def.emptyStrength) return;
            integrity = Mathf.Max(0f, integrity - village.monsters * def.integrityDamage * dt);
            if (integrity > 0f || !def.loseWhenVillageFalls) return;

            ended = true;
            Debug.Log($"{name}: the village has fallen", this);
            BattleEvents.RaiseBattleEnded(false);
        }

        void SetHolder(BattleLocation location, Faction holder)
        {
            if (location.holder == holder) return;
            location.holder = holder;
            if (holder == Faction.Village)
            {
                location.monsters = 0f;
                location.allies = Mathf.Max(location.allies, location.Def.garrisonOnCapture);
                location.meter = 1f;
            }
            else
            {
                location.allies = 0f;
                location.meter = 0f;
                location.waveTimer = location.Def.waveInterval;
            }
            Debug.Log($"{name}: {location.DisplayName} is now held by {holder}", location);
            BattleEvents.RaiseLocationChanged(location.LocationId, holder);
        }

        void Recount()
        {
            sealedCount = 0;
            heldCount = 0;
            for (int i = 0; i < locations.Length; i++)
            {
                BattleLocation location = locations[i];
                if (location == village || location.holder != Faction.Village) continue;
                heldCount++;
                LocationKind kind = location.Def.kind;
                if (kind == LocationKind.Portal || kind == LocationKind.SeaPortal || kind == LocationKind.FinalBreach) sealedCount++;
            }
            for (int i = 0; i < locations.Length; i++)
            {
                if (locations[i].Def.kind == LocationKind.FinalBreach)
                    locations[i].locked = sealedCount < def.sealedForFinalBreach;
            }
        }

        // ── Real units and real portals ──────────────────────────────────────────────
        void OnUnitKilled(IUnit unit)
        {
            if (unit == null) return;
            if (unit.Faction == Faction.Monsters) koCount++;

            BattleLocation location = Find(unit.LocationId);
            if (location == null) return;
            if (unit.Faction == Faction.Monsters) location.monsters = Mathf.Max(0f, location.monsters - unit.Strength);
            else if (unit.Faction == Faction.Village) location.allies = Mathf.Max(0f, location.allies - unit.Strength);
        }

        void OnPortalPhaseChanged(int locationId, PortalPhase phase)
        {
            BattleLocation location = Find(locationId);
            if (location == null) return;
            location.hasCamp = true;
            location.campPhase = phase;
            if (phase == PortalPhase.Sealed) SetHolder(location, Faction.Village);
            else if (phase == PortalPhase.Open && location.holder == Faction.Monsters) location.meter = 0f;
            Recount();
        }

        void OnPortalMeterChanged(int locationId, float meter)
        {
            BattleLocation location = Find(locationId);
            if (location != null) location.meter = Mathf.Clamp01(meter);
        }

        BattleLocation Find(int locationId)
        {
            if (indexById == null || locationId < 0 || locationId >= indexById.Length) return null;
            int index = indexById[locationId];
            return index >= 0 ? locations[index] : null;
        }
    }
}
