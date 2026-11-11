using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Feel
{
    // Object Pool for one burst effect (hit sparks, block sparks, dust). Instances are created
    // once at load, parented to their own root so they do not ride along with the owner, and
    // reused round-robin: the oldest burst is recycled when all are busy. Never Instantiate
    // or Destroy mid-battle.
    public sealed class VfxPool : MonoBehaviour
    {
        [SerializeField] BalanceConfig balance;
        [SerializeField] ParticleSystem prefab;

        ParticleSystem[] items;
        Transform root;
        int next;

        void Awake()
        {
            root = new GameObject($"[Pool] {prefab.name}").transform;
            items = new ParticleSystem[Mathf.Max(1, balance.hitVfxPoolSize)];
            for (int i = 0; i < items.Length; i++)
            {
                items[i] = Instantiate(prefab, root);
                items[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        void OnDestroy()
        {
            if (root != null) Destroy(root.gameObject);
        }

        public void Play(Vector3 position, Quaternion rotation)
        {
            ParticleSystem item = items[next];
            next = (next + 1) % items.Length;
            item.transform.SetPositionAndRotation(position, rotation);
            item.Clear(true);
            item.Play(true);
        }
    }
}
