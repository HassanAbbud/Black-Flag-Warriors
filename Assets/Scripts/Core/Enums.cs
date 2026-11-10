namespace Ahoy.Core
{
    public enum Faction { Village, Monsters, Neutral }

    public enum Tier { Scuttler, Brute, Captain, Guardian, Giant, Herald }

    public enum PortalPhase { Open, Contested, SpawnBoss, Sealed }

    public enum ToolType { Bomb, Bow, Boomerang, Harpoon }

    // Vulnerable (R20c): the boss stands open after its opening attack; Zahim's next hit exposes
    // the gauge. Appended last so assets that stored the older values keep their meaning.
    public enum WeakPointState { Hidden, Telegraphing, Exposed, Smashing, Vulnerable }

    public enum PlayMode { OnFoot, Ship }

    // TelegraphHit: Zahim struck the boss while it was winding up (the red diamond). Appended last
    // so stored values keep their meaning.
    public enum ExposeSource { ToolCounter, FocusSpiritAttack, PunishWindow, TelegraphHit }

    // M1-04: flinch, knockback, launch and blowaway. The victim decides how far each one moves it.
    public enum HitReaction { None, Flinch, Knockback, Launch, Blowaway }
}
