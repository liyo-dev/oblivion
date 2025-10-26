using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio est�tico para localizar y cachear instancias globales (managers) sin repetir FindObject calls.
/// </summary>
public static class ServiceLocator
{
    private static readonly Dictionary<Type, UnityEngine.Object> _services = new(32);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetCache()
    {
        _services.Clear();
    }

    /// <summary>Registra una instancia concreta para su tipo.</summary>
    public static void Register<T>(T instance) where T : UnityEngine.Object
    {
        if (!instance) return;
        _services[typeof(T)] = instance;
    }

    /// <summary>Desregistra la instancia si es la actualmente cacheada.</summary>
    public static void Unregister<T>(T instance) where T : UnityEngine.Object
    {
        if (!instance) return;
        var key = typeof(T);
        if (_services.TryGetValue(key, out var current) && current == instance)
        {
            _services.Remove(key);
        }
    }

    /// <summary>
    /// Devuelve el servicio solicitado. Si no est� registrado intenta localizarlo en escena una sola vez.
    /// </summary>
    public static T Get<T>(bool logIfMissing = true) where T : UnityEngine.Object
    {
        if (TryGet(out T service)) return service;
        if (logIfMissing)
            Debug.LogWarning($"[ServiceLocator] No se encontr� servicio de tipo {typeof(T).Name}.");
        return null;
    }

    /// <summary>
    /// Intentar obtener el servicio sin generar logs. Si no est� cacheado se localizar� autom�ticamente.
    /// </summary>
    public static bool TryGet<T>(out T service) where T : UnityEngine.Object
    {
        if (TryGetCached(out service))
        {
            return true;
        }

        service = FindAndCache<T>();
        return service;
    }

    /// <summary>
    /// Limpia una referencia si el objeto fue destruido (�til antes de cargar escenas).
    /// </summary>
    public static void RemoveIfMissing<T>() where T : UnityEngine.Object
    {
        var key = typeof(T);
        if (_services.TryGetValue(key, out var current) && !current)
        {
            _services.Remove(key);
        }
    }

    private static bool TryGetCached<T>(out T service) where T : UnityEngine.Object
    {
        var key = typeof(T);
        if (_services.TryGetValue(key, out var current))
        {
            if (current)
            {
                service = current as T;
                if (service) return true;
            }

            _services.Remove(key);
        }

        service = null;
        return false;
    }

    private static T FindAndCache<T>() where T : UnityEngine.Object
    {
        T found;
#if UNITY_2022_3_OR_NEWER
        found = UnityEngine.Object.FindFirstObjectByType<T>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        found = UnityEngine.Object.FindObjectOfType<T>(true);
#pragma warning restore 618
#endif
        if (found)
        {
            _services[typeof(T)] = found;
        }
        return found;
    }
}
