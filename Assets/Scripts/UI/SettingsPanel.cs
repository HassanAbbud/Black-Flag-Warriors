using Ahoy.Core;
using Ahoy.UI;
using Ahoy.Audio;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : BasePanel
{
    public Button btnClose;
    public Toggle toggleMusic;
    public Toggle toggleSound;
    public Slider sliderMusic;
    public Slider sliderSound;

    public override void Init()
    {
        Debug.Log($"AudioMgr: {AudioMgr.Instance}, data: {AudioMgr.Instance?.audioData}");
        toggleMusic.isOn = AudioMgr.Instance.audioData.isMusicOn;
        toggleSound.isOn = AudioMgr.Instance.audioData.isSFXOn;
        sliderMusic.value = AudioMgr.Instance.audioData.musicVolume;
        sliderSound.value = AudioMgr.Instance.audioData.sfxVolume;


        btnClose.onClick.AddListener(() =>
        {
            AudioMgr.Instance.SaveAudioData();
            UImanager.Instance.HidePanel<SettingsPanel>();
        });

        toggleMusic.onValueChanged.AddListener((isOn) =>
        {
            AudioMgr.Instance.SetMusicOpen(isOn);
        });

        toggleSound.onValueChanged.AddListener((isOn) =>
        {
            AudioMgr.Instance.SetSFXOpen(isOn);
        });

        sliderMusic.onValueChanged.AddListener((value) =>
        {
            AudioMgr.Instance.SetMusicVolume(value);
        });

        sliderSound.onValueChanged.AddListener((value) =>
        {
            AudioMgr.Instance.SetSFXVolume(value);
        });
    }

}
