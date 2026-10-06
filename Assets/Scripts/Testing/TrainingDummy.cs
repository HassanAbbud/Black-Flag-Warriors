using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Testing
{
    // Throwaway: something for Zahim to hit before Enemies land. Delete before submission.
    // Flashes, slides back by reaction, logs each hit and refills when emptied.
    public sealed class TrainingDummy : MonoBehaviour, IDamageable
    {
        static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        [SerializeField] float maxHealth = 600f;
        [SerializeField] float flashSeconds = 0.1f;
        [SerializeField] float flinchSlide = 0.2f;
        [SerializeField] float knockbackSlide = 1.5f;
        [SerializeField] float returnSpeed = 2f;
        [SerializeField] Color flashColor = Color.red;
        [SerializeField] Renderer body;

        MaterialPropertyBlock block;
        Color baseColor;
        Vector3 home;
        float health;
        float flashUntil;
        int hits;

        public Faction Faction => Faction.Monsters;
        public bool IsAlive => true;

        void Awake()
        {
            block = new MaterialPropertyBlock();
            baseColor = body.sharedMaterial.HasProperty(BaseColor) ? body.sharedMaterial.GetColor(BaseColor) : Color.white;
            home = transform.position;
            health = maxHealth;
        }

        public void TakeDamage(in DamageInfo info)
        {
            hits++;
            health -= info.Amount;

            Vector3 away = transform.position - info.Origin;
            away.y = 0f;
            float slide = info.Reaction == HitReaction.Flinch ? flinchSlide
                        : info.Reaction == HitReaction.None ? 0f : knockbackSlide;
            if (away.sqrMagnitude > 0f) transform.position += away.normalized * slide;

            flashUntil = Time.time + flashSeconds;
            Debug.Log($"{name}: hit #{hits} for {info.Amount} ({info.Reaction}, hitstop {info.HitstopFrames}f, drain {info.WeakPointDrain})", this);

            if (health <= 0f)
            {
                Debug.Log($"{name}: KO after {hits} hits — refilling", this);
                health = maxHealth;
                hits = 0;
            }
        }

        void Update()
        {
            transform.position = Vector3.MoveTowards(transform.position, home, returnSpeed * Time.deltaTime);

            block.SetColor(BaseColor, Time.time < flashUntil ? flashColor : baseColor);
            body.SetPropertyBlock(block);
        }
    }
}
