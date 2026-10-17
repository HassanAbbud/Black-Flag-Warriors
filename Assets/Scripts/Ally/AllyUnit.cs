using System;
using Ahoy.Core;
using UnityEngine;
using UnityEngine.AI;

namespace Ahoy.Allies
{
    // Ally's body: health and identity. Implements IUnit (which includes IDamageable) so nothing
    // outside ever needs to know this is an ally.
    public sealed class AllyUnit : MonoBehaviour, IUnit
    {
        static readonly int DieHash = Animator.StringToHash("Die");

        [Header("Identity")]
        [SerializeField] Faction faction = Faction.Village;
        [SerializeField] Tier tier = Tier.Brute;
        [SerializeField] int locationId;           // home portal (R8)：只有它自己的击杀会推动那个进度条
        [SerializeField] float strength = 10f;     // 看不见的战斗里的战力值 (R64)

        [Header("Health")]
        [SerializeField] float maxHealth = 100f;
        [SerializeField] float destroyDelay = 3f;

        [Header("Animation (optional)")]
        [SerializeField] Animator animator;        // trigger "Die"

        float health;

        // 受击表现由动画/特效系统订阅处理，这里只负责广播（M1-04：受害者自己决定移动多远）
        public event Action<HitReaction> Hit;
        public event Action Died;

        // IDamageable
        public Faction Faction => faction;
        public bool IsAlive => health > 0f;

        // IUnit
        public Tier Tier => tier;
        public int LocationId => locationId;
        public float Strength => strength;
        public IWeakPoint WeakPoint => null;       // 友军没有弱点

        public float HealthNormalized => health / maxHealth;

        void Awake() => health = maxHealth;

        public void TakeDamage(in DamageInfo info)
        {
            if (!IsAlive) return;

            health = Mathf.Max(0f, health - info.Amount);

            if (!IsAlive)
            {
                Die();
                return;
            }

            if (info.Reaction != HitReaction.None) Hit?.Invoke(info.Reaction);
        }

        void Die()
        {
            // 关掉导航和碰撞，怪物就不会再把它当目标，也不会挡路
            if (TryGetComponent(out NavMeshAgent agent)) agent.enabled = false;
            foreach (Collider col in GetComponentsInChildren<Collider>()) col.enabled = false;

            if (animator != null) animator.SetTrigger(DieHash);

            Died?.Invoke();
            Destroy(gameObject, destroyDelay);
        }
    }
}