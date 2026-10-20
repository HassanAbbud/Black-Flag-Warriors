
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class OptionsMenu : MonoBehaviour
{
    [Header("UI References")]
    public GameObject optionsPanel;
    public Slider volumeSlider;
    public Toggle fullscreenToggle;
    public TMP_Dropdown resolutionDropdown;

    private Resolution[] resolutions;

    void Start()
    {
        // Set up available screen resolutions
        resolutions = Screen.resolutions;

        if (resolutionDropdown != null)
        {
            resolutionDropdown.ClearOptions();

            foreach (Resolution resolution in resolutions)
            {
                resolutionDropdown.options.Add(
                    new TMP_Dropdown.OptionData(
                        resolution.width + " x " + resolution.height
                    )
                );
            }

            resolutionDropdown.RefreshShownValue();
        }

        // Load saved settings
        float savedVolume = PlayerPrefs.GetFloat("Volume", 1f);
        bool savedFullscreen = PlayerPrefs.GetInt("Fullscreen", 1) == 1;

        if (volumeSlider != null)
        {
            volumeSlider.value = savedVolume;
            AudioListener.volume = savedVolume;
        }

        if (fullscreenToggle != null)
        {
            fullscreenToggle.isOn = savedFullscreen;
            Screen.fullScreen = savedFullscreen;
        }

        if (optionsPanel != null)
            optionsPanel.SetActive(false);
    }

    public void OpenOptions()
    {
        optionsPanel.SetActive(true);
    }

    public void CloseOptions()
    {
        optionsPanel.SetActive(false);
    }

    public void SetVolume(float volume)
    {
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("Volume", volume);
    }

    public void SetFullscreen(bool fullscreen)
    {
        Screen.fullScreen = fullscreen;
        PlayerPrefs.SetInt("Fullscreen", fullscreen ? 1 : 0);
    }

    public void SetResolution(int index)
    {
        if (index >= 0 && index < resolutions.Length)
        {
            Resolution resolution = resolutions[index];

            Screen.SetResolution(
                resolution.width,
                resolution.height,
                Screen.fullScreen
            );
        }
    }

    public void QuitGame()
    {
        PlayerPrefs.Save();
        Application.Quit();
    }
}