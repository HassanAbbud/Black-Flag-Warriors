namespace Ahoy.Core
{
    // The only way to hurt anything. Never cast to a concrete Monster or AllyUnit.
    public interface IDamageable
    {
        Faction Faction { get; }
        bool IsAlive { get; }
        void TakeDamage(in DamageInfo info);
    }
}
