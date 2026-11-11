using Ahoy.Core;
using Ahoy.Feel;
using UnityEngine;

namespace Ahoy.Testing
{
    // Throwaway target for Zahim, and a reference for how a victim should feel to hit
    // (the victim owns its own reaction, as DamageInfo intends):
    //   hitstop  — freezes for DamageInfo.HitstopFrames
    //   flash    — tints for a few frames
    //   reaction — flinch nudges, knockback slides, launch pops up, blowaway sends flying;
    //              any hit while airborne keeps it up (juggle)
    // On KO it drops, waits, and respawns at full health. Delete before submission.
    public sealed class TrainingDummy : MonoBehaviour, IDamageable
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] Renderer body;
        [SerializeField] float maxHealth = 600f;
        [SerializeField] float respawnSeconds = 1.5f;

        [Header("Reactions (metres per second)")]
        [SerializeField] float flinchSpeed = 1.5f;
        [SerializeField] float knockbackSpeed = 7f;
        [SerializeField] float launchUpSpeed = 8f;
        [SerializeField] float juggleUpSpeed = 4f;
        [SerializeField] float blowawaySpeed = 14f;
        [SerializeField] float blowawayUpSpeed = 5f;
        [SerializeField] float groundFriction = 18f;
        [SerializeField] float gravity = -25f;
        [SerializeField] float returnHomeSpeed = 2f;

        [Header("Flash")]
        [SerializeField] float flashSeconds = 0.08f;
        [SerializeField] Color flashColor = Color.white;
        [SerializeField] Color koColor = new Color(0.3f, 0.3f, 0.3f);

        MaterialPropertyBlock block;
        Color baseColor;
        Vector3 home;
        Vector3 velocity;
        float health;
        float frozenUntil;
        float flashUntil;
        float respawnAt;
        bool airborne;
        bool knockedOut;
        int hits;

        public Faction Faction => Faction.Monsters;
        public bool IsAlive => !knockedOut;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseColor = body.sharedMaterial.HasProperty(BaseColor) ? body.sharedMaterial.GetColor(BaseColor) : Color.white;
            home = transform.position;
            health = maxHealth;
        }

        public void TakeDamage(in DamageInfo info)
        {
            if (knockedOut) return;

            hits++;
            health -= info.Amount;
            flashUntil = Time.time + flashSeconds;
            if (info.HitstopFrames > 0) frozenUntil = Time.time + HitstopReceiver.Seconds(info.HitstopFrames);

            Vector3 away = transform.position - info.Origin;
            away.y = 0f;
            away = away.sqrMagnitude > 0f ? away.normalized : -transform.forward;

            HitReaction reaction = health <= 0f ? HitReaction.Blowaway : info.Reaction;
            switch (reaction)
            {
                case HitReaction.Flinch:
                    velocity = away * flinchSpeed + Vector3.up * (airborne ? juggleUpSpeed : 0f);
                    break;
                case HitReaction.Knockback:
                    velocity = away * knockbackSpeed + Vector3.up * (airborne ? juggleUpSpeed : 0f);
                    break;
                case HitReaction.Launch:
                    velocity = away * flinchSpeed + Vector3.up * (airborne ? juggleUpSpeed : launchUpSpeed);
                    break;
                case HitReaction.Blowaway:
                    velocity = away * blowawaySpeed + Vector3.up * blowawayUpSpeed;
                    break;
            }
            airborne |= velocity.y > 0f;

            Debug.Log($"{name}: hit #{hits} for {info.Amount} ({info.Reaction}, hitstop {info.HitstopFrames}f)", this);
            if (health <= 0f)
            {
                knockedOut = true;
                respawnAt = Time.time + respawnSeconds;
                Debug.Log($"{name}: KO after {hits} hits", this);
            }
        }

        void Update()
        {
            float now = Time.time;
            Paint(now);

            if (now < frozenUntil) return;
            float dt = Time.deltaTime;

            if (knockedOut && !airborne && now >= respawnAt) Respawn();

            Vector3 position = transform.position;
            if (airborne)
            {
                velocity.y += gravity * dt;
                position += velocity * dt;
                if (position.y <= home.y)
                {
                    position.y = home.y;
                    velocity.y = 0f;
                    airborne = false;
                }
            }
            else
            {
                Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
                flat = Vector3.MoveTowards(flat, Vector3.zero, groundFriction * dt);
                velocity = flat;
                position += velocity * dt;
                if (!knockedOut && flat.sqrMagnitude == 0f)
                    position = Vector3.MoveTowards(position, home, returnHomeSpeed * dt);
            }
            transform.position = position;
        }

        void Respawn()
        {
            knockedOut = false;
            health = maxHealth;
            hits = 0;
            velocity = Vector3.zero;
            transform.position = home;
        }

        void Paint(float now)
        {
            Color color = now < flashUntil ? flashColor : knockedOut ? koColor : baseColor;
            block.SetColor(BaseColor, color);
            body.SetPropertyBlock(block);
        }
    }
}
