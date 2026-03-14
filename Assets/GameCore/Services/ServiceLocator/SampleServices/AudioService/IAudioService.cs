namespace GameCore.Services.Samples
{
// Пример интерфейса сервиса аудио (узкоспециализирован, соблюдает ISP)
    public interface IAudioService : IService
    {
        void PlaySound(string soundName);
        void SetVolume(float volume);
    }
}