using Ahoy.Core;
using UnityEngine;

namespace Ahoy.Ships
{
    public class ShipBullet : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void OnEnable()
        {
            Invoke("RemoveMe", 1f);
        }

        void RemoveMe()
        {
            PoolManager.Instance.ReturnObject(gameObject.name, gameObject);
        }
    }
}

