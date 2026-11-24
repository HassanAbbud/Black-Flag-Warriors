using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Island
{
    // W7.1: a sea portal is sealed by sinking its guardian (a boss warship or a sea dragon), fought
    // from Zahim's ship. Watches the guardian through IShipDamageable and, the moment it is gone,
    // tells the battle the portal is sealed, as a PortalCamp does on land.
    public sealed class SeaPortalGuardian : MonoBehaviour
    {
        [Tooltip("The sea location this guardian holds (2, 5 or 8).")]
        [SerializeField] int locationId;
        [Tooltip("The boss: anything that is an IShipDamageable (EnemyShipController, SeaDragonBoss).")]
        [SerializeField] Component guardian;

        IShipDamageable boss;
        bool sealedPortal;

        void Awake()
        {
            boss = guardian as IShipDamageable;
            Debug.Assert(boss != null, "SeaPortalGuardian needs an IShipDamageable guardian", this);
        }

        void Update()
        {
            if (sealedPortal || boss == null || boss.IsAlive) return;
            sealedPortal = true;
            Debug.Log($"{name}: the sea guardian is sunk, sea portal {locationId} is sealed", this);
            BattleEvents.RaisePortalPhaseChanged(locationId, PortalPhase.Sealed);
        }
    }
}
