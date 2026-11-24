using UnityEngine;

namespace Ahoy.Core
{
    public class GameDataMgr
    {
        private static GameDataMgr instance;
        public static GameDataMgr Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new GameDataMgr();
                }
                return instance;
            }
        }

        public AudioData audioData;

        private GameDataMgr() 
        { 
            audioData = JSONMgr.Instance.LoadJson<AudioData>("AudioData");
        }


        public void SaveAudioData()
        {
            JSONMgr.Instance.SaveJson("AudioData", audioData);
        }

        
    }
}
