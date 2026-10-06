using UnityEngine;

public class ShipBullet : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void OnEnable()
    {
        Invoke("RemoveMe", 1f);
    }

    void RemoveMe()
    {
        string key = gameObject.name.Replace("(Clone)", "");
        PoolManager.Instance.ReturnObject(key, gameObject);
    }
}
