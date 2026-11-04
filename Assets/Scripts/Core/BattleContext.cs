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

        public static ICrowdQuery Crowd => crowd;
        public static IVillageTargets Village => village;

        public static void RegisterCrowd(ICrowdQuery service) => crowd = service ?? NullCrowd.Instance;

        public static void UnregisterCrowd(ICrowdQuery service)
        {
            if (crowd == service) crowd = NullCrowd.Instance;
        }

        // The project enters Play Mode without a domain reload, so statics outlive the session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            crowd = NullCrowd.Instance;
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
    }
}
