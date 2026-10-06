namespace Ahoy.Core
{
    public interface IUnit : IDamageable
    {
        Tier Tier { get; }
        int LocationId { get; }        // home portal — only its own kills move that meter (R8)
        float Strength { get; }        // unseen-battle value (R64)
        IWeakPoint WeakPoint { get; }  // null for scuttlers (R19)
    }
}
