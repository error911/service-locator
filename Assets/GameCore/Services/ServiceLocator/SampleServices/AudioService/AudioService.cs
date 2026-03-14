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
            _source.volume = volume;
        }
    }
}