using UnityEngine;

namespace GameCore.Services.Samples
{
// Реализация сервиса сохранений
    public class SaveService : ISaveService
    {
        // JsonUtility подходит для простых [Serializable]-типов; словари и полиморфизм не поддерживает.
        public void Save<T>(string key, T data)
        {
            string json = JsonUtility.ToJson(data);
            PlayerPrefs.SetString(key, json);
            PlayerPrefs.Save();
        }

        public T Load<T>(string key)
        {
            if (!PlayerPrefs.HasKey(key))
                return default;

            string json = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<T>(json);
        }
        
        public void Run()
        {
            
        }

        public void Release()
        {
            
        }
    }
}