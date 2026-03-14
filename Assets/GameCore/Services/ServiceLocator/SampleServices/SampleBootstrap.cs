using GameCore.Services;
using GameCore.Services.Samples;
using UnityEngine;

namespace GameCore.Services.SampleServices
{
    public class SampleBootstrap : MonoBehaviour
    {
        [SerializeField] private AudioService _audioService;
        [SerializeField] private SaveService _saveService;

        private void Awake()
        {
            // Регистрируем сервисы по интерфейсам
            ServiceLocator.Register<IAudioService>(_audioService);
            ServiceLocator.Register<ISaveService>(_saveService);
        }
    }
}