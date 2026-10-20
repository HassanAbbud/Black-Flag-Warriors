using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Enemies
{
    // Shared by the crowd monster and the boss so the two cannot drift apart.
    static class EnemyCombat
    {
        // True if the target stood inside the attack's hitbox and took the hit. A box test
        // against Zahim's position rather than a physics query, so a crowd of attackers costs
        // nothing. The target's own i-frames and guard (R51) still apply inside TakeDamage.
        public static bool TryStrike(IUnit attacker, Transform origin, MonsterAttackDef attack,
                                     IDamageable target, Vector3 targetPosition, float padding)
        {
            if (!target.IsAlive) return false;

            Vector3 local = Quaternion.Inverse(origin.rotation) * (targetPosition - origin.position)
                            - attack.hitboxCenter;
            Vector3 half = attack.hitboxHalfExtents;
            if (Mathf.Abs(local.x) > half.x + padding ||
                Mathf.Abs(local.y) > half.y + padding ||
                Mathf.Abs(local.z) > half.z + padding) return false;

            var info = new DamageInfo(attack.damage, origin.position, attack.reaction,
                                      attack.hitstopFrames, 0f, attacker, attack.unblockable);
            target.TakeDamage(in info);
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
