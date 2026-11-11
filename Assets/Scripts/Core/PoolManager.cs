using System.Collections.Generic;
using UnityEngine;

namespace Ahoy.Core
{
    public class PoolManager : BaseManager<PoolManager>
    {
        private PoolManager() { }
        private Dictionary<string, Stack<GameObject>> ObjectPool = new Dictionary<string, Stack<GameObject>>();

        // The project enters Play Mode without a domain reload, so this static instance outlives
        // the play session; empty it each time Play starts or it hands out destroyed objects.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Instance.ObjectPool.Clear();


        #region public pool functions
        public GameObject GetObject(string prefabName, GameObject prefab, Vector3 position, Quaternion rotation)
        {
            if (!ObjectPool.ContainsKey(prefabName))
            {
                ObjectPool[prefabName] = new Stack<GameObject>();
            }
            while (ObjectPool[prefabName].Count > 0)
            {
                GameObject obj = ObjectPool[prefabName].Pop();
                if (obj == null) continue; // destroyed with its scene; drop it
                obj.transform.position = position;
                obj.transform.rotation = rotation;
                obj.SetActive(true);
                return obj;
            }

            GameObject newObj = GameObject.Instantiate(prefab, position, rotation);
            newObj.name = prefabName; // Remove "(Clone)" from the name
            return newObj;
        }

        public void ReturnObject(string prefabName, GameObject obj)
        {
            obj.SetActive(false);
            if (!ObjectPool.ContainsKey(prefabName))
            {
                ObjectPool[prefabName] = new Stack<GameObject>();
            }
            ObjectPool[prefabName].Push(obj);
        }

        public void ClearPool()
        {
            foreach (var stack in ObjectPool.Values)
            {
                while (stack.Count > 0)
                {
                    GameObject obj = stack.Pop();
                    GameObject.Destroy(obj);
                }
            }
            ObjectPool.Clear();
        }

        #endregion
    }
}

