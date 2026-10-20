using Ahoy.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ahoy.Enemies.Testing
{
    // Throwaway: fill Test_Enemies with a crowd from the keyboard. 1 spawns a ring around the
    // target, 2 sends everything back to the pool. Delete before submission.
    public sealed class EnemyTestSpawner : MonoBehaviour
    {
        [SerializeField] MonsterManager manager;
        [SerializeField] MonsterDef species;
        [Tooltip("Usually Zahim.")]
        [SerializeField] Transform around;
        [SerializeField] int count = 20;
        [SerializeField] float radius = 12f;

        void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) SpawnRing();
            if (keyboard.digit2Key.wasPressedThisFrame) manager.ReleaseAll();
        }

        void SpawnRing()
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * Mathf.PI * 2f / count;
                Vector3 position = around.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Quaternion facing = Quaternion.LookRotation(around.position - position);
                if (manager.Spawn(species, position, facing, 0) == null)
                {
                    Debug.LogWarning($"{name}: crowd is at capacity or the pool is dry.", this);
                    break;
                }
            }
            Debug.Log($"{name}: {manager.ActiveCount} monsters alive.", this);
        }
    }
}
