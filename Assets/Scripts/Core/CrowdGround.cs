using UnityEngine;

namespace Ahoy.Core
{
    // Where a crowd unit stands. The terrain is always the floor; a collider above it counts only
    // when it is low enough to step onto (a rock, a step, a bridge), so nobody ends up on a treetop
    // or a roof, and a unit that missed the ground (knocked off a ledge) drops back to the terrain.
    public static class CrowdGround
    {
        // The terrain under a point: cheap, so units follow it every frame.
        public static float Floor(Vector3 position)
        {
            Terrain terrain = Terrain.activeTerrain;
            return terrain != null ? terrain.SampleHeight(position) + terrain.transform.position.y : position.y;
        }

        // How far above the terrain the unit stands on a collider (0 on bare ground). A raycast:
        // called every few frames, and the unit keeps this offset on top of Floor in between.
        public static float OffsetAboveFloor(Vector3 position, BalanceConfig balance)
        {
            float floor = Floor(position);
            return Height(position, balance, floor) - floor;
        }

        static float Height(Vector3 position, BalanceConfig balance, float floor)
        {
            Vector3 origin = new Vector3(position.x, floor + balance.crowdMaxGroundAboveTerrain + 0.1f, position.z);
            float reach = balance.crowdMaxGroundAboveTerrain + 0.2f;
            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, reach, balance.groundMask, QueryTriggerInteraction.Ignore))
                return Mathf.Max(floor, hit.point.y);
            return floor;
        }
    }
}
