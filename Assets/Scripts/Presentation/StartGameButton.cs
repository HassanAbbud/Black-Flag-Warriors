using UnityEngine;
using UnityEngine.SceneManagement;

namespace Ahoy.Presentation
{
    // Main menu "start" button: loads the level. The scene must be listed in Build Settings.
    public sealed class StartGameButton : MonoBehaviour
    {
        [Tooltip("Scene the start button loads.")]
        [SerializeField] string levelScene = "IslandMap_Level";

        public void StartGame() => SceneManager.LoadScene(levelScene);
    }
}
