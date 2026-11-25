using Codice.Client.Common.GameUI;
using System.Collections.Generic;
using UnityEngine;

namespace Ahoy.UI
{
    public class UImanager:MonoBehaviour
    {
        private static UImanager instance;
        public static UImanager Instance => instance;


        [SerializeField]
        private Transform canvasTrans;
        //private GameObject battleHUD;
        //private GameObject battleSystems;
        //private GameObject miniMap;


        private UImanager() 
        { 

            //battleHUD = GameObject.Find("BattleHUD");
            //battleSystems = GameObject.Find("BattleSystems");
            //miniMap = GameObject.Find("MiniMap");
        }

        private void Awake()
        {
            instance = this;
        }


        private Dictionary<string, BasePanel> panelDictionary = new Dictionary<string, BasePanel>();


        public T ShowPanel<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDictionary.ContainsKey(panelName))
            {
                return panelDictionary[panelName] as T;
            }
            else
            {
                GameObject panelPrefab = Resources.Load<GameObject>($"UI/{panelName}");
                if (panelPrefab != null)
                {
                    GameObject panelInstance = GameObject.Instantiate(panelPrefab, canvasTrans);
                    T panelComponent = panelInstance.GetComponent<T>();
                    if (panelComponent != null)
                    {
                        panelDictionary.Add(panelName, panelComponent);
                        panelComponent.Show();
                        //ToggleHUD();
                        return panelComponent;
                    }
                    else
                    {
                        Debug.LogError($"The prefab '{panelName}' does not have a component of type {typeof(T).Name}.");
                        GameObject.Destroy(panelInstance);
                        return null;
                    }
                }
                else
                {
                    Debug.LogError($"Panel prefab '{panelName}' not found in Resources/UI.");
                    return null;
                }
            }
        }


        public void HidePanel<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDictionary.ContainsKey(panelName))
            {
                BasePanel panel = panelDictionary[panelName];
                panel.Hide(() =>
                {
                    GameObject.Destroy(panel.gameObject);
                    panelDictionary.Remove(panelName);
                    //ToggleHUD();
                });
            }
            else
            {
                Debug.LogWarning($"Panel '{panelName}' is not currently shown.");
            }
        }

        public T GetPanel<T>() where T : BasePanel
        {
            string panelName = typeof(T).Name;
            if (panelDictionary.ContainsKey(panelName))
            {
                return panelDictionary[panelName] as T;
            }
            else
            {
                Debug.LogWarning($"Panel '{panelName}' is not currently shown.");
                return null;
            }
        }

        //public void ToggleHUD()
        //{
        //    if(panelDictionary.Count > 0)
        //    {
        //        battleHUD.SetActive(false);
        //        battleSystems.SetActive(false);
        //        miniMap.SetActive(false);
        //    }
        //    else
        //    {
        //        battleHUD.SetActive(true);
        //        battleSystems.SetActive(true);
        //        miniMap.SetActive(true);
        //    }
        //}
    }
}

