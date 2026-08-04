using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Servicio estáatico para localizar y cachear instancias globales (managers) sin repetir FindObject calls.
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
            Debug.LogWarning($"[ServiceLocator] No se encontró servicio de tipo {typeof(T).Name}.");
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
        // Solo buscar y cachear servicios globales una vez. Evitar FindObjectOfType para mejorar el rendimiento.
        // Solo buscar servicios globales en Unity 2022.3 o superior. Para versiones anteriores, se debe registrar explícitamente.
    #if UNITY_2022_3_OR_NEWER
        T found = UnityEngine.Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
    #else
        T found = null;
    #endif
        if (found)
        {
            _services[typeof(T)] = found;
        }
        // Si no se encuentra, se debe registrar explícitamente el servicio.
        return found;
    }

    /// <summary>
    /// Devuelve todas las instancias del tipo indicado usando una única consulta centralizada.
    /// </summary>
    public static IReadOnlyList<T> GetAll<T>(bool includeInactive = true) where T : UnityEngine.Object
    {
#if UNITY_2022_3_OR_NEWER
        var results = UnityEngine.Object.FindObjectsByType<T>(includeInactive ? FindObjectsInactive.Include : FindObjectsInactive.Exclude);
#else
#pragma warning disable 618
        var results = UnityEngine.Object.FindObjectsOfType<T>(includeInactive);
#pragma warning restore 618
#endif
        return results ?? Array.Empty<T>();
    }
}
