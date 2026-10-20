namespace Ahoy.Core
{
    public static class FactionRules
    {
        // Neutral has no enemies; the other factions are hostile to each other.
        public static bool AreHostile(Faction a, Faction b)
        {
            return a != Faction.Neutral && b != Faction.Neutral && a != b;
        }
    }
}