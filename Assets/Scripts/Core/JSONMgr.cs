using UnityEngine;

namespace Ahoy.Core
{
    public class JSONMgr
    {
        private static JSONMgr instance;
        public static JSONMgr Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = new JSONMgr();
                }
                return instance;
            }
        }

        private string path = Application.persistentDataPath + "/";

        public T LoadJson<T>(string filename) where T: new()
        {
            string newPath = path + filename + ".json";
            if (!System.IO.File.Exists(newPath))
            {
                newPath = Application.streamingAssetsPath + "/" + filename + ".json";
                if (!System.IO.File.Exists(newPath))
                {
                    Debug.Log($"File not found: {newPath}");
                    return new T();
                }
                return JsonUtility.FromJson<T>(System.IO.File.ReadAllText(newPath));
            }
            string json = System.IO.File.ReadAllText(newPath);
            return JsonUtility.FromJson<T>(json);
        }

        public void SaveJson<T>(string filename, T data)
        {
            string newPath = path + filename + ".json";
            string json = JsonUtility.ToJson(data, true);
            System.IO.File.WriteAllText(newPath, json);
        }
    }
}
