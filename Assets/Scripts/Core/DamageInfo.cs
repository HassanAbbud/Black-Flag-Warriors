using UnityEngine;

namespace Ahoy.Core
{
    // Passed by `in` so a hit never copies or allocates.
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly Vector3 Origin;
        public readonly HitReaction Reaction;
        public readonly int HitstopFrames;     // 0 for every target but the first of a swing
        public readonly float WeakPointDrain;  // R22: light 4, strong 10, tool 15, special 35
        public readonly IUnit Source;          // null when the attacker is Zahim
        public readonly bool Unblockable;      // R51: some heavy attacks cannot be guarded

        public DamageInfo(float amount, Vector3 origin, HitReaction reaction, int hitstopFrames,
                          float weakPointDrain, IUnit source, bool unblockable = false)
        {
            Amount = amount;
            Origin = origin;
            Reaction = reaction;
            HitstopFrames = hitstopFrames;
            WeakPointDrain = weakPointDrain;
            Source = source;
            Unblockable = unblockable;
        }
    }
}
