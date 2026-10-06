namespace Ahoy.Core
{
    public enum Faction { Village, Monsters, Neutral }

    public enum Tier { Scuttler, Brute, Captain, Guardian, Giant, Herald }

    public enum PortalPhase { Open, Contested, GuardianOut, Sealed }

    public enum ToolType { Bomb, Bow, Boomerang, Harpoon }

    public enum WeakPointState { Hidden, Telegraphing, Exposed, Smashing }

    public enum PlayMode { OnFoot, Ship }

    public enum ExposeSource { ToolCounter, FocusSpiritAttack, PunishWindow }

    // M1-04: flinch, knockback, launch and blowaway. The victim decides how far each one moves it.
    public enum HitReaction { None, Flinch, Knockback, Launch, Blowaway }
}
