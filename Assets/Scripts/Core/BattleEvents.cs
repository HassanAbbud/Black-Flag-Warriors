using System;
using UnityEngine;

namespace Ahoy.Core
{
    // Battle-wide news (Code Patterns 2.5), owned by the battle simulation. Raisers announce what
    // happened; nobody is commanded. Subscribe in OnEnable, unsubscribe in OnDisable.
    public static class BattleEvents
    {
        // Every counted death on either side, with its killer (null: Zahim, or unknown). The KO
        // count takes Zahim's kills; the battle simulation takes every loss off its location (R64).
        public static event Action<IUnit, IUnit> UnitKilled;
        // A battle location changed hands. A sealed portal whose location falls back to the
        // monsters reopens itself.
        public static event Action<int, Faction> LocationChanged;
        // A portal camp in the scene moved phase (locationId, phase).
        public static event Action<int, PortalPhase> PortalPhaseChanged;
        // A portal camp's meter toward its guardian, 0..1 (locationId, meter).
        public static event Action<int, float> PortalMeterChanged;
        // The battle is over: true when won, false when the village fell.
        public static event Action<bool> BattleEnded;

        public static void RaiseUnitKilled(IUnit unit, IUnit killer) => UnitKilled?.Invoke(unit, killer);
        public static void RaiseLocationChanged(int locationId, Faction holder) => LocationChanged?.Invoke(locationId, holder);
        public static void RaisePortalPhaseChanged(int locationId, PortalPhase phase) => PortalPhaseChanged?.Invoke(locationId, phase);
        public static void RaisePortalMeterChanged(int locationId, float meter) => PortalMeterChanged?.Invoke(locationId, meter);
        public static void RaiseBattleEnded(bool won) => BattleEnded?.Invoke(won);

        // The project enters Play Mode without a domain reload, so statics outlive the session.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            UnitKilled = null;
            LocationChanged = null;
            PortalPhaseChanged = null;
            PortalMeterChanged = null;
            BattleEnded = null;
        }
    }
}
