using UnityEngine;

namespace Ahoy.Core
{
    // A crowd unit that can be sent down a road: it walks the points in order while nothing is
    // in reach to fight, then holds at the last one. Battle fronts march their reinforcements with it.
    public interface IRouteFollower
    {
        void FollowRoute(Vector3[] route);
    }
}
