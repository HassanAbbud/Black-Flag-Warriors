using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // Shared by the crowd monster and the boss so the two cannot drift apart.
    static class EnemyCombat
    {
        // True if the point stood inside the attack's hitbox. A box test rather than a physics
        // query, so a crowd of attackers costs nothing.
        public static bool InHitbox(Transform origin, MonsterAttackDef attack, Vector3 point, float padding)
        {
            Vector3 local = Quaternion.Inverse(origin.rotation) * (point - origin.position)
                            - attack.hitboxCenter;
            Vector3 half = attack.hitboxHalfExtents;
            return Mathf.Abs(local.x) <= half.x + padding &&
                   Mathf.Abs(local.y) <= half.y + padding &&
                   Mathf.Abs(local.z) <= half.z + padding;
        }

        // Deals the attack's damage. The target's own i-frames and guard (R51) still apply
        // inside TakeDamage. damageScale is the difficulty dial from BalanceConfig.
        public static void Strike(IUnit attacker, Transform origin, MonsterAttackDef attack,
                                  IDamageable target, float damageScale)
        {
            var info = new DamageInfo(attack.damage * damageScale, origin.position, attack.reaction,
                                      attack.hitstopFrames, 0f, attacker, attack.unblockable);
            target.TakeDamage(in info);
        }

        // One target: true if it stood inside the hitbox and took the hit.
        public static bool TryStrike(IUnit attacker, Transform origin, MonsterAttackDef attack,
                                     IDamageable target, Vector3 targetPosition, float padding,
                                     float damageScale)
        {
            if (!target.IsAlive || !InHitbox(origin, attack, targetPosition, padding)) return false;
            Strike(attacker, origin, attack, target, damageScale);
            return true;
        }

        // Turns toward a flat direction. A speed of 0 means "do not turn", which is how an
        // attack locks its aim.
        public static void Turn(Transform body, Vector3 direction, float degreesPerSecond, float dt)
        {
            if (degreesPerSecond <= 0f || direction.sqrMagnitude <= 0f) return;
            body.rotation = Quaternion.RotateTowards(
                body.rotation, Quaternion.LookRotation(direction), degreesPerSecond * dt);
        }

        public static void Play(Animator animator, int stateHash, float crossFade)
        {
            if (animator != null) animator.CrossFadeInFixedTime(stateHash, crossFade, 0, 0f);
        }

        // Flat (ignoring height) distance, for deciding who is near whom.
        public static float FlatDistance(Vector3 a, Vector3 b)
        {
            float dx = a.x - b.x, dz = a.z - b.z;
            return Mathf.Sqrt(dx * dx + dz * dz);
        }

#if UNITY_EDITOR
        public static void DrawHitbox(Transform origin, MonsterAttackDef attack, bool live)
        {
            if (attack == null) return;
            Gizmos.color = live ? Color.red : Color.yellow;
            Gizmos.matrix = Matrix4x4.TRS(origin.position, origin.rotation, Vector3.one);
            Gizmos.DrawWireCube(attack.hitboxCenter, attack.hitboxHalfExtents * 2f);
        }
#endif
    }

    // "Move this way for this long": lunges and knockbacks. Same maths as PlayerMotor.Travel,
    // so a slide covers its distance exactly at any frame rate.
    struct Slide
    {
        Vector3 direction;
        float speed;
        float seconds;

        public void Begin(Vector3 heading, float distance, float duration)
        {
            direction = heading;
            seconds = duration;
            speed = duration > 0f ? distance / duration : 0f;
        }

        public void Clear() => seconds = 0f;

        // stateTime already includes this frame's dt.
        public Vector3 Velocity(float stateTime, float dt)
        {
            float active = Mathf.Clamp(seconds - (stateTime - dt), 0f, dt);
            return active > 0f ? direction * (speed * active / dt) : Vector3.zero;
        }
    }
}
