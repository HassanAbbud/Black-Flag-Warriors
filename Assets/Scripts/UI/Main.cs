using UnityEngine;

namespace Ahoy.UI
{
    public class Main : MonoBehaviour
    {
        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            UImanager.Instance.ShowPanel<MainPanel>();
        }

    }
}

