// ============================================================================
// SERVICE LOCATOR (Singleton, MonoBehaviour)
// ============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace GameCore.Services
{
    public interface IService
    {
        void Run();
        void Release();
    }

    /// <summary>
    /// Глобальный реестр сервисов. Позволяет регистрировать и получать реализации интерфейсов.
    /// Соответствует SOLID:
    /// - SRP: только хранение и предоставление сервисов.
    /// - OCP: новые сервисы добавляются без изменения класса.
    /// - LSP: можно подменить реализацию интерфейса любой другой.
    /// - ISP: клиенты запрашивают только нужные им интерфейсы.
    /// - DIP: частично соблюдён – клиенты зависят от абстракций (интерфейсов), но всё ещё обращаются к конкретному локатору.
    ///   Для полного DIP потребовалось бы внедрение IServiceLocator через конструкторы, что в Unity с MonoBehaviour затруднительно.
    /// </summary>
    public class ServiceLocator : MonoBehaviour
    {
        private static ServiceLocator _instance;
        private readonly Dictionary<Type, object> _services = new Dictionary<Type, object>();
        private readonly List<IUpdatable> _updatableServices = new List<IUpdatable>();
        private readonly List<IUpdatable> _toAdd = new List<IUpdatable>(); // буфер для безопасного добавления
        private readonly List<IUpdatable> _toRemove = new List<IUpdatable>(); // буфер для безопасного удаления
        private readonly List<IFixedUpdatable> _fixedUpdatableServices = new List<IFixedUpdatable>();
        private readonly List<IFixedUpdatable> _toAddFixed = new List<IFixedUpdatable>(); // буфер для безопасного добавления (Fixed)
        private readonly List<IFixedUpdatable> _toRemoveFixed = new List<IFixedUpdatable>(); // буфер для безопасного удаления (Fixed)
        private bool _isUpdating = false;

        public static ServiceLocator Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = FindFirstObjectByType<ServiceLocator>();
                    if (_instance == null)
                    {
                        GameObject go = new GameObject("[ServiceLocator]");
                        _instance = go.AddComponent<ServiceLocator>();
                        DontDestroyOnLoad(go);
                    }
                }

                return _instance;
            }
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        // ---- Статические обёртки ----
        public static void Register<T>(T service) where T : class => Instance.RegisterInternal(service);
        public static T Get<T>() where T : class => Instance.GetInternal<T>();
        public static bool TryGet<T>(out T service) where T : class => Instance.TryGetInternal(out service);
        public static void Unregister<T>() where T : class => Instance.UnregisterInternal<T>();

        /// <summary>Вызывает <see cref="IService.Run"/> у каждого зарегистрированного сервиса (каждый объект — один раз).</summary>
        public static void Run() => Instance.RunInternal();

        /// <summary>Вызывает <see cref="IService.Release"/> у каждого сервиса и очищает реестр.</summary>
        public static void Release() => Instance.ReleaseInternal();

        /// <summary>Только снимает регистрации; <see cref="IService.Release"/> не вызывается. Для штатного завершения используйте <see cref="Release"/>.</summary>
        public static void Clear() => Instance.ClearInternal();

        // ---- Внутренние методы ----
        private void RegisterInternal<T>(T service) where T : class
        {
            Type type = typeof(T);
            if (!type.IsInterface)
                throw new ArgumentException($"[ServiceLocator] Регистрируемый тип {type} должен быть интерфейсом.");

            if (_services.ContainsKey(type))
            {
                Debug.LogWarning($"[ServiceLocator] Сервис типа {type} уже зарегистрирован. Заменяем.");
                UnregisterInternal<T>(); // удалим старый, чтобы корректно обработать IUpdatable
            }

            _services[type] = service;

            // Если сервис реализует IUpdatable, добавим в список обновляемых
            if (service is IUpdatable updatable)
            {
                AddUpdatable(updatable);
            }

            // Если сервис реализует IFixedUpdatable, добавим в список фиксированного обновления
            if (service is IFixedUpdatable fixedUpdatable)
            {
                AddFixedUpdatable(fixedUpdatable);
            }
        }

        private T GetInternal<T>() where T : class
        {
            Type type = typeof(T);
            if (!type.IsInterface)
                throw new InvalidOperationException(
                    $"[ServiceLocator] Запрашиваемый тип {type} должен быть интерфейсом.");

            if (_services.TryGetValue(type, out object serviceObj))
            {
                if (serviceObj is T result)
                    return result;
                throw new InvalidOperationException(
                    $"[ServiceLocator] Сервис для {type} зарегистрирован с несовместимой реализацией ({serviceObj?.GetType()}).");
            }

            throw new InvalidOperationException($"[ServiceLocator] Сервис типа {type} не зарегистрирован.");
        }

        private bool TryGetInternal<T>(out T service) where T : class
        {
            Type type = typeof(T);
            if (!type.IsInterface)
            {
                service = null;
                return false;
            }

            if (_services.TryGetValue(type, out object obj) && obj is T result)
            {
                service = result;
                return true;
            }

            service = null;
            return false;
        }

        private void UnregisterInternal<T>() where T : class
        {
            Type type = typeof(T);
            if (!type.IsInterface || !_services.TryGetValue(type, out object service))
                return;

            // Если удаляемый сервис реализует IUpdatable, убираем его из списка
            if (service is IUpdatable updatable)
            {
                RemoveUpdatable(updatable);
            }

            // Если удаляемый сервис реализует IFixedUpdatable, убираем его из списка фиксированного обновления
            if (service is IFixedUpdatable fixedUpdatable)
            {
                RemoveFixedUpdatable(fixedUpdatable);
            }

            _services.Remove(type);
        }

        /// <summary>Уникальные экземпляры <see cref="IService"/> (один объект может быть зарегистрирован под несколькими интерфейсами).</summary>
        private List<IService> CollectDistinctServices()
        {
            var list = new List<IService>();
            var seen = new HashSet<object>();
            foreach (object o in _services.Values)
            {
                if (o is IService s && seen.Add(o))
                    list.Add(s);
            }

            return list;
        }

        private void ResetRegistryInternal()
        {
            _services.Clear();
            _updatableServices.Clear();
            _toAdd.Clear();
            _toRemove.Clear();
        }

        private void RunInternal()
        {
            List<IService> snapshot = CollectDistinctServices();
            foreach (IService service in snapshot)
            {
                try
                {
                    service.Run();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ServiceLocator] Ошибка в Run сервиса {service.GetType()}: {e}");
                }
            }
        }

        private void ReleaseInternal()
        {
            List<IService> snapshot = CollectDistinctServices();
            foreach (IService service in snapshot)
            {
                try
                {
                    service.Release();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ServiceLocator] Ошибка в Release сервиса {service.GetType()}: {e}");
                }
            }

            ResetRegistryInternal();
        }

        private void ClearInternal()
        {
            ResetRegistryInternal();
        }

        // ---- Управление списком обновляемых сервисов ----
        private void AddUpdatable(IUpdatable updatable)
        {
            if (_isUpdating)
            {
                // Если сейчас идёт перебор, добавляем в буфер, чтобы не сломать итерацию
                if (!_toAdd.Contains(updatable))
                    _toAdd.Add(updatable);
            }
            else
            {
                if (!_updatableServices.Contains(updatable))
                    _updatableServices.Add(updatable);
            }
        }

        private void RemoveUpdatable(IUpdatable updatable)
        {
            if (_isUpdating)
            {
                if (!_toRemove.Contains(updatable))
                    _toRemove.Add(updatable);
            }
            else
            {
                _updatableServices.Remove(updatable);
            }
        }

        // Методы управления списком FixedUpdatable
        private void AddFixedUpdatable(IFixedUpdatable updatable)
        {
            if (_isUpdating)
            {
                // Если сейчас идёт перебор, добавляем в буфер, чтобы не сломать итерацию
                if (!_toAddFixed.Contains(updatable))
                    _toAddFixed.Add(updatable);
            }
            else
            {
                if (!_fixedUpdatableServices.Contains(updatable))
                    _fixedUpdatableServices.Add(updatable);
            }
        }

        private void RemoveFixedUpdatable(IFixedUpdatable updatable)
        {
            if (_isUpdating)
            {
                if (!_toRemoveFixed.Contains(updatable))
                    _toRemoveFixed.Add(updatable);
            }
            else
            {
                _fixedUpdatableServices.Remove(updatable);
            }
        }

        // ---- Update driver ----
        private void Update()
        {
            _isUpdating = true;

            // Вызываем OnUpdate у всех обновляемых сервисов
            foreach (var updatable in _updatableServices)
            {
                try
                {
                    updatable.OnUpdate();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ServiceLocator] Ошибка в OnUpdate сервиса {updatable.GetType()}: {e}");
                }
            }

            _isUpdating = false;

            // Применяем отложенные добавления и удаления
            if (_toAdd.Count > 0)
            {
                foreach (var item in _toAdd)
                    if (!_updatableServices.Contains(item))
                        _updatableServices.Add(item);
                _toAdd.Clear();
            }

            if (_toRemove.Count > 0)
            {
                foreach (var item in _toRemove)
                    _updatableServices.Remove(item);
                _toRemove.Clear();
            }
        }

        private void FixedUpdate()
        {
            _isUpdating = true;

            // Вызываем OnFixedUpdate у всех фиксированных обновляемых сервисов
            foreach (var updatable in _fixedUpdatableServices)
            {
                try
                {
                    updatable.OnFixedUpdate();
                }
                catch (Exception e)
                {
                    Debug.LogError($"[ServiceLocator] Ошибка в OnFixedUpdate сервиса {updatable.GetType()}: {e}");
                }
            }

            _isUpdating = false;

            // Применяем отложенные добавления и удаления для FixedUpdatable
            if (_toAddFixed.Count > 0)
            {
                foreach (var item in _toAddFixed)
                    if (!_fixedUpdatableServices.Contains(item))
                        _fixedUpdatableServices.Add(item);
                _toAddFixed.Clear();
            }

            if (_toRemoveFixed.Count > 0)
            {
                foreach (var item in _toRemoveFixed)
                    _fixedUpdatableServices.Remove(item);
                _toRemoveFixed.Clear();
            }
        }
        }
    }

    /*
    // ============================================================================
    // ПРИМЕР ИСПОЛЬЗОВАНИЯ
    // ============================================================================

    // Компонент, который регистрирует сервисы при старте сцены
    public class GameBootstrap : MonoBehaviour
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

    // Пример клиента, использующего сервисы
    public class Player : MonoBehaviour
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

    */
}