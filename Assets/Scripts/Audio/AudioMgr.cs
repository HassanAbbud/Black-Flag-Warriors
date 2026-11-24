using UnityEngine;
using Ahoy.Core;

namespace Ahoy.Audio
{
    public class AudioMgr : MonoBehaviour
    {
        private static AudioMgr instance;
        public static AudioMgr Instance => instance;

        public AudioData audioData;
        public AudioSource musicSource;

        private void Awake()
        {
            if (instance == null)
            {
                instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }


            audioData = GameDataMgr.Instance.audioData;
            musicSource = GetComponent<AudioSource>();
            SetMusicOpen(audioData.isMusicOn);
            SetSFXOpen(audioData.isSFXOn);
            SetMusicVolume(audioData.musicVolume);
            SetSFXVolume(audioData.sfxVolume);
        }

        public void SetMusicOpen(bool isOpen)
        {
            audioData.isMusicOn = isOpen;
            musicSource.mute = !isOpen;
        }

        //TO DO: Implement SFX functionality
        public void SetSFXOpen(bool isOpen)
        {
            audioData.isSFXOn = isOpen;
        }

        public void SetMusicVolume(float volume)
        {
            audioData.musicVolume = volume;
            musicSource.volume = volume;
        }

        public void SetSFXVolume(float volume)
        {
            audioData.sfxVolume = volume;
        }

        public void SaveAudioData()
        {
            GameDataMgr.Instance.SaveAudioData();
        }

    }
}