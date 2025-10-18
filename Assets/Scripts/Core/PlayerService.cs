using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Singleton service that exposes Player references without repeating expensive lookups.
/// Allows explicit player registration and performs lazy lookups when needed.
/// </summary>
[DefaultExecutionOrder(-600)]
[DisallowMultipleComponent]
public sealed class PlayerService : MonoBehaviour
{
    const string DefaultPlayerTag = "Player";

    static PlayerService _instance;

    [Tooltip("Optional manual reference to the player root. When empty the service locates it lazily.")]
    [SerializeField] private GameObject playerRoot;

    [Tooltip("Keep this service across scene loads (DontDestroyOnLoad).")]
    [SerializeField] private bool persistAcrossScenes = true;

    [Tooltip("Tag fallback used to locate the player when there is no explicit registration.")]
    [SerializeField] private string playerTag = DefaultPlayerTag;

    readonly Dictionary<Type, Component> _componentCache = new();

    public static event Action<GameObject> OnPlayerRegistered;
    public static event Action OnPlayerUnregistered;

    public static PlayerService Instance => _instance != null ? _instance : CreateRuntimeInstance();
    public static bool HasInstance => _instance != null;
    public static GameObject Player => Instance.playerRoot;
    public static Transform PlayerTransform => TryGetComponent(out Transform result) ? result : null;

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogWarning($"[PlayerService] Instancia duplicada detectada en '{name}'. Se destruye el duplicado.");
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        if (playerRoot != null)
            InternalRegister(playerRoot, true);
    }

    void OnDestroy()
    {
        if (_instance == this)
        {
            _instance = null;
            _componentCache.Clear();
            playerRoot = null;
        }
    }

    static PlayerService CreateRuntimeInstance()
    {
        var go = new GameObject(nameof(PlayerService));
        return go.AddComponent<PlayerService>();
    }

    /// <summary>
    /// Registra el GameObject del jugador y cachea componentes comunes.
    /// </summary>
    public static void RegisterPlayer(GameObject player, bool overwriteExisting = true)
    {
        if (player == null) return;
        Instance.InternalRegister(player, overwriteExisting);
    }

    /// <summary>
    /// Registra manualmente un componente del jugador (Aotil para componentes creados en runtime).
    /// </summary>
    public static void RegisterComponent(Component component, bool overwriteExisting = true)
    {
        if (component == null) return;
        Instance.InternalRegisterComponent(component, overwriteExisting);
    }

    /// <summary>
    /// Limpia la referencia al jugador si coincide con la instancia registrada.
    /// </summary>
    public static void UnregisterPlayer(GameObject player)
    {
        if (!HasInstance || player == null) return;
        Instance.InternalUnregister(player);
    }

    /// <summary>
    /// Obtiene el GameObject del jugador. Si no existe registro, intenta localizarlo en escena (una Aonica vez).
    /// </summary>
    public static bool TryGetPlayer(out GameObject player, bool allowSceneLookup = true)
    {
        return Instance.TryGetPlayerInternal(out player, allowSceneLookup);
    }

    /// <summary>
    /// Obtiene un componente asociado al jugador utilizando la cachA interna. Si no estA disponible, se intenta localizar y cachear.
    /// </summary>
    public static bool TryGetComponent<T>(out T component, bool includeInactive = true, bool allowSceneLookup = true) where T : Component
    {
        return Instance.TryGetComponentInternal(out component, includeInactive, allowSceneLookup);
    }

    /// <summary>
    /// Variante que lanza excepciA3n si el componente no existe. Astil para dependencias obligatorias.
    /// </summary>
    public static T RequireComponent<T>(bool includeInactive = true, bool allowSceneLookup = true) where T : Component
    {
        if (TryGetComponent<T>(out var component, includeInactive, allowSceneLookup))
            return component;

        throw new InvalidOperationException($"[PlayerService] No se pudo resolver el componente '{typeof(T).Name}'. AsegAorate de registrar el Player correctamente.");
    }

    void InternalRegister(GameObject player, bool overwriteExisting)
    {
        if (!overwriteExisting && playerRoot != null && playerRoot != player)
            return;

        playerRoot = player;
        _componentCache.Clear();
        CacheDefaultComponents(playerRoot);
        OnPlayerRegistered?.Invoke(playerRoot);
    }

    void InternalRegisterComponent(Component component, bool overwriteExisting)
    {
        if (component == null) return;

        var type = component.GetType();
        if (!overwriteExisting && _componentCache.TryGetValue(type, out var existing) && existing != null && existing != component)
            return;

        _componentCache[type] = component;
    }

    void InternalUnregister(GameObject player)
    {
        if (playerRoot != player) return;

        playerRoot = null;
        _componentCache.Clear();
        OnPlayerUnregistered?.Invoke();
    }

    bool TryGetPlayerInternal(out GameObject player, bool allowSceneLookup)
    {
        if (playerRoot == null && allowSceneLookup)
        {
            var located = LocatePlayerInScene();
            if (located != null)
                InternalRegister(located, true);
        }

        player = playerRoot;
        return player != null;
    }

    bool TryGetComponentInternal<T>(out T component, bool includeInactive, bool allowSceneLookup) where T : Component
    {
        if (_componentCache.TryGetValue(typeof(T), out var cached) && cached != null)
        {
            component = cached as T;
            if (component != null && component) return true;
            _componentCache.Remove(typeof(T));
        }

        if (playerRoot == null && allowSceneLookup)
        {
            TryGetPlayerInternal(out _, true);
        }

        if (playerRoot != null)
        {
            component = includeInactive
                ? playerRoot.GetComponentInChildren<T>(true)
                : playerRoot.GetComponentInChildren<T>();

            if (component != null)
            {
                _componentCache[typeof(T)] = component;
                return true;
            }
        }

        component = null;
        return false;
    }

    void CacheDefaultComponents(GameObject player)
    {
        if (player == null) return;

        InternalRegisterComponent(player.transform, true);
        CacheIfPresent<PlayerHealthSystem>(player);
        CacheIfPresent<ManaPool>(player);
        CacheIfPresent<MagicCaster>(player);
        CacheIfPresent<PlayerActionManager>(player);
    }

    void CacheIfPresent<T>(GameObject root) where T : Component
    {
        var comp = root.GetComponentInChildren<T>(true);
        if (comp != null)
            _componentCache[typeof(T)] = comp;
    }

    GameObject LocatePlayerInScene()
    {
        GameObject byTag = null;
        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            try
            {
                byTag = GameObject.FindGameObjectWithTag(playerTag);
            }
            catch (UnityException)
            {
                // El Tag no existe: silenciar la excepciA3n y continuar con los fallback.
            }
        }

        if (byTag != null) return byTag;

#if UNITY_2022_3_OR_NEWER
        var health = UnityEngine.Object.FindFirstObjectByType<PlayerHealthSystem>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        var health = UnityEngine.Object.FindObjectOfType<PlayerHealthSystem>(true);
#pragma warning restore 618
#endif
        if (health != null) return health.gameObject;

#if UNITY_2022_3_OR_NEWER
        var manaPool = UnityEngine.Object.FindFirstObjectByType<ManaPool>(FindObjectsInactive.Include);
#else
#pragma warning disable 618
        var manaPool = UnityEngine.Object.FindObjectOfType<ManaPool>(true);
#pragma warning restore 618
#endif
        if (manaPool != null) return manaPool.gameObject;

        return null;
    }
}

