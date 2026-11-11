using System.Collections.Generic;
using UnityEngine;

namespace Ahoy.Core
{
    // What lives in the village list: the allies and villagers monsters can attack.
    public interface IVillageTargets
    {
        int Count { get; }
        ITargetable this[int index] { get; }   // indexed, so a loop never allocates an enumerator
        void Add(ITargetable target);
        void Remove(ITargetable target);
    }

    // The one service locator (Code Patterns 2.6): interfaces only, and every service has a
    // do-nothing stub, so any track runs its own test scene alone. Services register when they
    // enable and unregister when they disable.
    public static class BattleContext
    {
        static readonly VillageList village = new VillageList();
        static ICrowdQuery crowd = NullCrowd.Instance;
        static IBattleProgress progress = NullProgress.Instance;
        static ISpawnPacing pacing = NullPacing.Instance;

        public static ICrowdQuery Crowd => crowd;
        public static IVillageTargets Village => village;
        public static IBattleProgress Progress => progress;
        public static ISpawnPacing Pacing => pacing;

        public static void RegisterCrowd(ICrowdQuery service) => crowd = service ?? NullCrowd.Instance;

        public static void UnregisterCrowd(ICrowdQuery service)
        {
            if (crowd == service) crowd = NullCrowd.Instance;
        }

        public static void RegisterProgress(IBattleProgress service) => progress = service ?? NullProgress.Instance;

        public static void UnregisterProgress(IBattleProgress service)
        {
            if (progress == service) progress = NullProgress.Instance;
        }

        public static void RegisterPacing(ISpawnPacing service) => pacing = service ?? NullPacing.Instance;

        public static void UnregisterPacing(ISpawnPacing service)
        {
            if (pacing == service) pacing = NullPacing.Instance;
        }

        // The project enters Play Mode without a domain reload, so statics outlive the session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            crowd = NullCrowd.Instance;
            progress = NullProgress.Instance;
            pacing = NullPacing.Instance;
            village.Clear();
        }

        sealed class VillageList : IVillageTargets
        {
            readonly List<ITargetable> list = new List<ITargetable>(64);

            public int Count => list.Count;
            public ITargetable this[int index] => list[index];

            public void Add(ITargetable target)
            {
                if (target != null && !list.Contains(target)) list.Add(target);
            }

            public void Remove(ITargetable target) => list.Remove(target);
            public void Clear() => list.Clear();
        }

        // Stub: no crowd in this scene.
        sealed class NullCrowd : ICrowdQuery
        {
            public static readonly NullCrowd Instance = new NullCrowd();

            public int QueryBox(Vector3 center, Vector3 halfExtents, Quaternion rotation, IDamageable[] results) => 0;

            public bool TryFindNearest(Vector3 from, float maxDistance, out IDamageable nearest)
            {
                nearest = null;
                return false;
            }
        }

        // Stub: no battle simulation in this scene.
        sealed class NullProgress : IBattleProgress
        {
            public static readonly NullProgress Instance = new NullProgress();

            public int LocationCount => 0;
            public BattleLocationStatus GetLocation(int index) => default;

            public bool TryGetLocation(int locationId, out BattleLocationStatus status)
            {
                status = default;
                return false;
            }

            public int PortalCount => 0;
            public int SealedCount => 0;
            public int HeldCount => 0;
            public float Progress01 => 0f;
            public float VillageIntegrity01 => 1f;
            public int KoCount => 0;
            public float BattleSeconds => 0f;
            public int SeenLocationId => -1;
            public bool Ended => false;
        }

        // Stub: authored spawn rates, no allies.
        sealed class NullPacing : ISpawnPacing
        {
            public static readonly NullPacing Instance = new NullPacing();

            public float MonsterIntervalScale => 1f;
            public float MonsterCapScale => 1f;
            public float WaveStrengthScale => 1f;
            public float AllyInterval => 0f;
            public int AllyMaxAlive => 0;
            public float AllyRegenScale => 1f;
        }
    }
}
