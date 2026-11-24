using System;
using Ahoy.Core;
using Ahoy.Ships;
using UnityEngine;

namespace Ahoy.Bosses
{
    // W7.1 flying sea boss: a dragon that guards a sea portal and is fought from Zahim's ship.
    //   Patrol: circles its lair in the air.
    //   Engage: circles the player ship and, every volleyCooldown, holds still and breathes a volley
    //           of fireballs: an aimed fan, and every third time a full ring (bullet hell to weave through).
    //   Dead:   falls into the sea, then is switched off (never destroyed).
    // Hit by the player's cannons through IShipDamageable, like an enemy ship; its fireballs fly
    // through a CannonballPool, so they hurt the ship (and Zahim on land) like enemy cannon fire.
    [RequireComponent(typeof(Collider))]
    public sealed class SeaDragonBoss : MonoBehaviour, IShipDamageable
    {
        enum DragonState { Patrol, Engage, Volley, Dead }

        static readonly int FlyId = Animator.StringToHash("Fly");
        static readonly int ShootId = Animator.StringToHash("Shoot");
        static readonly int DieId = Animator.StringToHash("Die");
        static readonly int GlideId = Animator.StringToHash("Glide");
        const float HitJolt = 1.2f;          // metres a cannon hit knocks it back
        const float FallPitch = 50f;         // degrees nose-down while it falls
        const float SinkDepth = 6f;          // metres it sinks once it hits the water
        const float CrossFade = 0.15f;
        const string PlayerShipTag = "PlayerShip";
        const float GroundGravity = 9.81f;

        [SerializeField] SeaDragonDef def;
        [Tooltip("States: Fly (default), Shoot, Glide (falling), Die.")]
        [SerializeField] Animator animator;
        [Tooltip("Where fireballs leave from: its mouth.")]
        [SerializeField] Transform muzzle;
        [SerializeField] CannonballPool fireballs;
        [Tooltip("Zahim's ship. Left empty, the ship tagged PlayerShip is found on its own, as enemy ships do.")]
        [SerializeField] Transform target;

        DragonState state;
        Vector3 lair;
        float orbitAngle;
        float stateTime;
        float nextVolleyAt;
        float nextSearchAt;
        int volleys;
        int health;
        bool splashed;
        Vector3 jolt;

        public bool IsAlive => state != DragonState.Dead;
        public float Health01 => (float)health / def.maxHealth;
        public event Action Died;

        public void SetTarget(Transform ship) => target = ship;

        void Awake()
        {
            Debug.Assert(def != null && fireballs != null && muzzle != null, "SeaDragonBoss is missing references", this);
            lair = transform.position;
        }

        void OnEnable()
        {
            health = def.maxHealth;
            splashed = false;
            jolt = Vector3.zero;
            nextVolleyAt = Time.time + def.volleyCooldown;
            SetState(DragonState.Patrol);
        }

        public void TakeDamage(int damage)
        {
            if (state == DragonState.Dead) return;
            health -= damage;
            if (health > 0)
            {
                jolt = -transform.forward * HitJolt;   // knocked back by the cannon, then it rights itself
                return;
            }
            health = 0;
            SetState(DragonState.Dead);
            Died?.Invoke();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            stateTime += dt;
            switch (state)
            {
                case DragonState.Patrol:
                    Circle(lair, def.patrolRadius, dt);
                    // Same lookup as EnemyShipController, so it fights the ship whenever one is in the scene.
                    if (target == null && Time.time >= nextSearchAt)
                    {
                        nextSearchAt = Time.time + 1f;
                        GameObject ship = GameObject.FindWithTag(PlayerShipTag);
                        if (ship != null) target = ship.transform;
                    }
                    if (TargetInRange()) SetState(DragonState.Engage);
                    break;

                case DragonState.Engage:
                    if (!TargetInRange()) { SetState(DragonState.Patrol); break; }
                    Circle(target.position, def.engageRadius, dt);
                    if (Time.time >= nextVolleyAt) SetState(DragonState.Volley);
                    break;

                case DragonState.Volley:
                    if (target != null) Face(target.position - transform.position, dt);
                    if (stateTime >= def.windUpSeconds)
                    {
                        Breathe();
                        nextVolleyAt = Time.time + def.volleyCooldown;
                        SetState(DragonState.Engage);
                    }
                    break;

                case DragonState.Dead:
                    Fall(dt);
                    if (stateTime >= def.deathSeconds) gameObject.SetActive(false);
                    break;
            }
            if (state != DragonState.Dead && jolt.sqrMagnitude > 0.0001f)
            {
                transform.position += jolt * (dt * 6f);
                jolt = Vector3.MoveTowards(jolt, Vector3.zero, dt * 6f);
            }
        }

        // Glides down nose-first, crashes into the sea with its death, then sinks.
        void Fall(float dt)
        {
            float half = def.deathSeconds * 0.5f;
            if (!splashed)
            {
                float t = Mathf.Clamp01(stateTime / half);
                transform.position += Vector3.down * (def.altitude / half) * (0.5f + t) * dt;
                Vector3 e = transform.eulerAngles;
                transform.rotation = Quaternion.Euler(Mathf.LerpAngle(e.x, FallPitch, dt * 2f), e.y + 20f * dt, e.z);
                if (transform.position.y <= lair.y)
                {
                    splashed = true;
                    Play(DieId);
                }
                return;
            }
            transform.position += Vector3.down * (SinkDepth / half) * dt;
        }

        bool TargetInRange()
        {
            if (target == null || !target.gameObject.activeInHierarchy) return false;
            Vector3 fromLair = target.position - lair;
            fromLair.y = 0f;
            float range = state == DragonState.Patrol ? def.detectRange : def.leashRange;
            return fromLair.sqrMagnitude <= range * range;
        }

        // Flies around a centre at a radius and its altitude above the sea.
        void Circle(Vector3 centre, float radius, float dt)
        {
            orbitAngle += def.flySpeed / Mathf.Max(1f, radius) * dt;
            Vector3 goal = new Vector3(centre.x + Mathf.Cos(orbitAngle) * radius, lair.y + def.altitude, centre.z + Mathf.Sin(orbitAngle) * radius);
            Vector3 to = goal - transform.position;
            Vector3 step = Vector3.ClampMagnitude(to, def.flySpeed * 1.5f * dt);
            transform.position += step;
            if (to.sqrMagnitude > 0.01f) Face(to, dt);
        }

        void Face(Vector3 direction, float dt)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction), def.turnSpeed * dt);
        }

        // An aimed fan, and every third volley a ring all around: weave between them.
        void Breathe()
        {
            volleys++;
            bool ring = volleys % 3 == 0;
            int count = ring ? def.ringCount : def.fanCount;
            float spread = ring ? 360f : def.fanSpread;
            Vector3 aim = Aim();
            for (int i = 0; i < count; i++)
            {
                float angle = ring ? i * spread / count : (count == 1 ? 0f : -spread * 0.5f + i * spread / (count - 1));
                Vector3 velocity = Quaternion.Euler(0f, angle, 0f) * aim;
                fireballs.Launch(muzzle.position, velocity, def.fireballDamage, def.fireballReaction, transform);
            }
        }

        // Velocity that lands a fireball on the ship from the muzzle at fireballSpeed (flat), arcing under gravity.
        Vector3 Aim()
        {
            Vector3 to = (target != null ? target.position : transform.position + transform.forward * def.engageRadius) - muzzle.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            float time = Mathf.Max(0.2f, flat.magnitude / def.fireballSpeed);
            float gravity = fireballs.Gravity > 0f ? fireballs.Gravity : GroundGravity;
            return flat / time + Vector3.up * (to.y / time + 0.5f * gravity * time);
        }

        void SetState(DragonState next)
        {
            state = next;
            stateTime = 0f;
            Play(next == DragonState.Volley ? ShootId : next == DragonState.Dead ? GlideId : FlyId);
        }

        void Play(int stateHash)
        {
            if (animator != null && animator.isActiveAndEnabled) animator.CrossFadeInFixedTime(stateHash, CrossFade);
        }
    }
}
