namespace GameCore.Services.Samples
{
    // Пример интерфейса сервиса сохранений
    public interface ISaveService : IService
    {
        void Save<T>(string key, T data);
        T Load<T>(string key);
    }
}