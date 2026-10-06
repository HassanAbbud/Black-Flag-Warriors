using System.Collections.Generic;
using UnityEngine;

public class PoolManager : BaseManager<PoolManager>
{
    private PoolManager() { }
    private Dictionary<string,Stack<GameObject>> ObjectPool = new Dictionary<string, Stack<GameObject>>();

    public GameObject GetObject(string prefabName, GameObject prefab, Vector3 position, Quaternion rotation)
    {
        if (!ObjectPool.ContainsKey(prefabName))
        {
            ObjectPool[prefabName] = new Stack<GameObject>();
        }
        if (ObjectPool[prefabName].Count > 0)
        {
            GameObject obj = ObjectPool[prefabName].Pop();
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);
            return obj;
        }
        else
        {
            GameObject newObj = GameObject.Instantiate(prefab, position, rotation);
            return newObj;
        }
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
}
