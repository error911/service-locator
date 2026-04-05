using UnityEngine;

namespace GameCore.Services.Samples
{
// Реализация аудио-сервиса (может быть MonoBehaviour)
    public class AudioService : MonoBehaviour, IAudioService
    {
        [SerializeField] private AudioSource _source;

        public void PlaySound(string soundName)
        {
            // Логика воспроизведения звука
            Debug.Log($"Playing sound: {soundName}");
        }

        public void SetVolume(float volume)
        {
            if (_source == null)
            {
                Debug.LogWarning("[AudioService] AudioSource не назначен в инспекторе.");
                return;
            }

            _source.volume = volume;
        }

        public void Run()
        {
            
        }

        public void Release()
        {
            
        }
    }
}