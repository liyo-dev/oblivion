using System.Collections.Generic;
using UnityEngine;

namespace Game.Core.Pooling
{
    /// <summary>
    /// Pool genérico de objetos reutilizable.
    /// CERO allocations después de la inicialización.
    /// </summary>
    /// <typeparam name="T">Tipo de componente a poolear (debe ser Component)</typeparam>
    public class ObjectPool<T> where T : Component
    {
        private readonly Stack<T> _available;
        private readonly HashSet<T> _inUse;
        private readonly T _prefab;
        private readonly Transform _parent;
        private readonly int _maxSize;
        private readonly bool _expandable;

        /// <summary>
        /// Número de objetos disponibles en el pool
        /// </summary>
        public int AvailableCount => _available.Count;

        /// <summary>
        /// Número de objetos actualmente en uso
        /// </summary>
        public int InUseCount => _inUse.Count;

        /// <summary>
        /// Total de objetos creados (disponibles + en uso)
        /// </summary>
        public int TotalCount => _available.Count + _inUse.Count;

        /// <summary>
        /// Constructor del pool
        /// </summary>
        /// <param name="prefab">Prefab a instanciar</param>
        /// <param name="initialSize">Cantidad inicial de objetos pre-creados</param>
        /// <param name="maxSize">Tamaño máximo (0 = ilimitado)</param>
        /// <param name="expandable">Si puede crear más objetos cuando se agota</param>
        /// <param name="parent">Transform padre para organización</param>
        public ObjectPool(T prefab, int initialSize = 10, int maxSize = 100, bool expandable = true, Transform parent = null)
        {
            _prefab = prefab;
            _maxSize = maxSize;
            _expandable = expandable;
            _parent = parent;
            
            _available = new Stack<T>(initialSize);
            _inUse = new HashSet<T>();

            // Pre-crear objetos iniciales
            for (int i = 0; i < initialSize; i++)
            {
                T obj = CreateNewObject();
                if (obj != null)
                {
                    obj.gameObject.SetActive(false);
                    _available.Push(obj);
                }
            }
        }

        /// <summary>
        /// Obtiene un objeto del pool
        /// </summary>
        public T Get()
        {
            T obj;

            // Intentar obtener del pool
            if (_available.Count > 0)
            {
                obj = _available.Pop();
            }
            else if (_expandable && (_maxSize == 0 || TotalCount < _maxSize))
            {
                // Pool vacío pero puede expandirse
                obj = CreateNewObject();
                if (obj == null)
                {
                    Debug.LogError($"[ObjectPool] No se pudo crear nuevo objeto de tipo {typeof(T).Name}");
                    return null;
                }
            }
            else
            {
                // Pool agotado y no puede expandirse
                Debug.LogWarning($"[ObjectPool] Pool de {typeof(T).Name} agotado! " +
                                $"(InUse: {InUseCount}, Max: {_maxSize})");
                return null;
            }

            // Activar y registrar como en uso
            obj.gameObject.SetActive(true);
            _inUse.Add(obj);
            
            return obj;
        }

        /// <summary>
        /// Devuelve un objeto al pool
        /// </summary>
        public void Return(T obj)
        {
            if (obj == null)
            {
                Debug.LogWarning($"[ObjectPool] Intento de devolver objeto nulo al pool de {typeof(T).Name}");
                return;
            }

            // Verificar que el objeto estaba en uso
            if (!_inUse.Remove(obj))
            {
                Debug.LogWarning($"[ObjectPool] Objeto {obj.name} no estaba registrado como en uso");
            }

            // Desactivar y devolver al pool
            obj.gameObject.SetActive(false);
            _available.Push(obj);
        }

        /// <summary>
        /// Limpia el pool, destruyendo todos los objetos
        /// </summary>
        public void Clear()
        {
            // Destruir objetos disponibles
            while (_available.Count > 0)
            {
                T obj = _available.Pop();
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }

            // Destruir objetos en uso (con advertencia)
            if (_inUse.Count > 0)
            {
                Debug.LogWarning($"[ObjectPool] Limpiando pool con {_inUse.Count} objetos aún en uso");
                foreach (var obj in _inUse)
                {
                    if (obj != null)
                    {
                        Object.Destroy(obj.gameObject);
                    }
                }
                _inUse.Clear();
            }
        }

        /// <summary>
        /// Pre-calienta el pool creando objetos adicionales
        /// </summary>
        public void Warmup(int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_maxSize > 0 && TotalCount >= _maxSize)
                    break;

                T obj = CreateNewObject();
                if (obj != null)
                {
                    obj.gameObject.SetActive(false);
                    _available.Push(obj);
                }
            }
        }

        /// <summary>
        /// Reduce el pool a un tamaño específico destruyendo objetos sobrantes
        /// </summary>
        public void Trim(int targetSize)
        {
            while (_available.Count > targetSize)
            {
                T obj = _available.Pop();
                if (obj != null)
                {
                    Object.Destroy(obj.gameObject);
                }
            }
        }

        /// <summary>
        /// Crea un nuevo objeto instanciando el prefab
        /// </summary>
        private T CreateNewObject()
        {
            if (_prefab == null)
            {
                Debug.LogError("[ObjectPool] Prefab es null, no se puede crear objeto");
                return null;
            }

            GameObject instance = Object.Instantiate(_prefab.gameObject, _parent);
            instance.name = $"{_prefab.name} (Pooled)";
            
            T component = instance.GetComponent<T>();
            if (component == null)
            {
                Debug.LogError($"[ObjectPool] GameObject instanciado no tiene componente {typeof(T).Name}");
                Object.Destroy(instance);
                return null;
            }

            return component;
        }

        /// <summary>
        /// Obtiene estadísticas del pool
        /// </summary>
        public PoolStats GetStats()
        {
            return new PoolStats
            {
                PrefabName = _prefab != null ? _prefab.name : "null",
                Available = AvailableCount,
                InUse = InUseCount,
                Total = TotalCount,
                MaxSize = _maxSize,
                Expandable = _expandable
            };
        }
    }

    /// <summary>
    /// Estructura de estadísticas del pool
    /// </summary>
    public struct PoolStats
    {
        public string PrefabName;
        public int Available;
        public int InUse;
        public int Total;
        public int MaxSize;
        public bool Expandable;

        public override string ToString()
        {
            return $"{PrefabName}: {InUse}/{Total} en uso, {Available} disponibles (Max: {(MaxSize == 0 ? "∞" : MaxSize.ToString())})";
        }
    }
}
