using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Enemies.Testing
{
    // Throwaway: fill Test_Enemies with a crowd from the keyboard.
    //   1  a ring of `count` monsters around the target
    //   3  a big wave of `waveCount`, spread over several rings (the real musou load)
    //   2  send everything back to the pool
    // Delete before submission.
    public sealed class EnemyTestSpawner : MonoBehaviour
    {
        [SerializeField] MonsterManager manager;
        [SerializeField] MonsterDef species;
        [Tooltip("Usually Zahim.")]
        [SerializeField] Transform around;
        [SerializeField] int count = 20;
        [SerializeField] int waveCount = 100;
        [SerializeField] float radius = 12f;
        [Tooltip("Metres between the rings of a big wave.")]
        [SerializeField] float ringSpacing = 3f;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) Spawn(count);
            if (keyboard.digit3Key.wasPressedThisFrame) Spawn(waveCount);
            if (keyboard.digit2Key.wasPressedThisFrame) manager.ReleaseAll();
        }

        void Spawn(int total)
        {
            int perRing = Mathf.Max(1, count);
            int spawned = 0;
            for (int ring = 0; spawned < total; ring++)
            {
                float ringRadius = radius + ring * ringSpacing;
                int inThisRing = Mathf.Min(perRing + ring * 6, total - spawned);
                for (int i = 0; i < inThisRing; i++, spawned++)
                {
                    float angle = i * Mathf.PI * 2f / inThisRing;
                    Vector3 position = around.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * ringRadius;
                    Quaternion facing = Quaternion.LookRotation(around.position - position);
                    if (manager.Spawn(species, position, facing, 0) == null)
                    {
                        Debug.LogWarning($"{name}: crowd is at capacity or the pool is dry.", this);
                        Debug.Log($"{name}: {manager.ActiveCount} monsters alive.", this);
                        return;
                    }
                }
            }
            Debug.Log($"{name}: {manager.ActiveCount} monsters alive.", this);
        }
    }
}
