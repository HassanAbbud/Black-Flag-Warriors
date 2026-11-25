using Ahoy.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainPanel : BasePanel
{
    public Button btnStart;
    public Button btnLoad;
    public Button btnSettings;
    public Button btnExit;


    public override void Init()
    {
        btnStart.onClick.AddListener(() => { 
            UImanager.Instance.HidePanel<MainPanel>();
            SceneManager.LoadScene(1);
        });

        btnLoad.onClick.AddListener(() => { });

        btnSettings.onClick.AddListener(() => { 
            UImanager.Instance.ShowPanel<SettingsPanel>();
        });

        btnExit.onClick.AddListener(() => {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        });
    }

}
