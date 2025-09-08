using System;
using System.Collections.Generic;

public static class ServiceLocator
{
    private static readonly Dictionary<Type, object> _services = new();

    public static void Register<T>(T service)
    {
        var type = typeof(T);
        if (_services.ContainsKey(type))
            throw new Exception($"Service {type.Name} ya registrado.");
        _services[type] = service!;
    }

    public static T Get<T>()
    {
        var type = typeof(T);
        if (_services.TryGetValue(type, out var s))
            return (T)s;
        throw new Exception($"Service {type.Name} no encontrado. ¿Falta instalarlo?");
    }

    public static bool TryGet<T>(out T service)
    {
        var type = typeof(T);
        if (_services.TryGetValue(type, out var s))
        {
            service = (T)s;
            return true;
        }
        service = default!;
        return false;
    }

    public static void Clear() => _services.Clear();
}
