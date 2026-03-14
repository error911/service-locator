using GameCore.Services.Samples;
using UnityEngine;

namespace GameCore.Services.SampleServices
{
    // Пример клиента, использующего сервисы
    public class SamplePlayer : MonoBehaviour
    {
        private IAudioService _audio;
        private ISaveService _save;

        private void Start()
        {
            // Получаем сервисы (лучше кешировать в Start/Awake)
            _audio = ServiceLocator.Get<IAudioService>();
            _save = ServiceLocator.Get<ISaveService>();

            // Используем
            _audio.PlaySound("step");
            _save.Save("playerPosition", transform.position);
        }
    }
}