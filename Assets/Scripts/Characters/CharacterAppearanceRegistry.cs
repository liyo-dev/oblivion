using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Almacena y gestiona la apariencia visual de cada personaje del equipo.
/// Actúa como puente entre el ModularAutoBuilder (único, en Will) y las
/// apariencias individuales de Liam, Will y Estela, incluyendo cambios
/// de vestuario durante la aventura.
///
/// El ModularAutoBuilder se resuelve en tiempo de ejecución vía PlayerService,
/// por lo que este componente no necesita referencias de escena al prefab de Will.
///
/// Uso típico:
///   CharacterAppearanceRegistry.Instance.ApplyAppearance(CharacterSlot.Liam);
///   CharacterAppearanceRegistry.Instance.UpdatePart(CharacterSlot.Estela, PartCategory.Hat, "Hat03");
/// </summary>
[DefaultExecutionOrder(-100)]
public class CharacterAppearanceRegistry : MonoBehaviour
{
    #region Singleton
    public static CharacterAppearanceRegistry Instance { get; private set; }

#if UNITY_EDITOR
    [UnityEngine.RuntimeInitializeOnLoadMethod(UnityEngine.RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;
#endif
    #endregion

    [Header("Apariencia inicial de Liam")]
    [Tooltip("Partes activas de Liam. Deben coincidir con los nombres en la jerarquía de Will.")]
    [SerializeField] private List<AppearanceEntry> liamDefault = new();

    [Header("Apariencia inicial de Estela")]
    [Tooltip("Partes activas de Estela. Deben coincidir con los nombres en la jerarquía de Will.")]
    [SerializeField] private List<AppearanceEntry> estelaDefault = new();

    // Una entrada por slot (indexado por CharacterSlot int)
    private readonly Dictionary<PartCategory, string>[] _appearances = new Dictionary<PartCategory, string>[3];

    // Builder resuelto lazily desde PlayerService cuando el jugador ya está en escena
    private ModularAutoBuilder _builder;
    private bool _willSnapshotTaken;

    public bool HasWillSnapshot => _willSnapshotTaken;

    private ModularAutoBuilder Builder
    {
        get
        {
            if (_builder != null) return _builder;
            PlayerService.TryGetComponent(out _builder);
            return _builder;
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _appearances[(int)PartyControlManager.CharacterSlot.Liam]   = ToDict(liamDefault);
        _appearances[(int)PartyControlManager.CharacterSlot.Estela] = ToDict(estelaDefault);
    }

    // ─── API ──────────────────────────────────────────────────────────────────

    /// <summary>
    /// Aplica la apariencia almacenada del personaje al builder (cambia el modelo visual de Will).
    /// Llama a DeactivateAllCategories() antes para limpiar el estado anterior.
    /// </summary>
    public void ApplyAppearance(PartyControlManager.CharacterSlot slot)
    {
        var b = Builder;
        if (b == null) return;

        EnsureWillSnapshot(b);
        b.DeactivateAllCategories();
        var app = _appearances[(int)slot];
        Debug.Log($"[CharacterAppearanceRegistry] ApplyAppearance({slot}) — partes: [{(app != null ? string.Join(", ", System.Linq.Enumerable.Select(app, kv => $"{kv.Key}:{kv.Value}")) : "NULL")}]");
        b.ApplySelection(app);
    }

    /// <summary>
    /// Lee la selección actual del builder y la guarda como apariencia de este slot.
    /// Útil para capturar cambios de vestuario hechos desde el menú de inventario.
    /// </summary>
    public void CaptureCurrentAppearance(PartyControlManager.CharacterSlot slot)
    {
        var b = Builder;
        if (b == null) return;

        EnsureWillSnapshot(b);
        _appearances[(int)slot] = b.GetSelection();
    }

    /// <summary>
    /// Actualiza una pieza individual para un personaje.
    /// Pasar partName vacío o null elimina la categoría (la desactiva).
    /// </summary>
    public void UpdatePart(PartyControlManager.CharacterSlot slot, PartCategory category, string partName)
    {
        var dict = _appearances[(int)slot];
        if (dict == null) return;

        if (string.IsNullOrEmpty(partName))
            dict.Remove(category);
        else
            dict[category] = partName;
    }

    /// <summary>
    /// Captura la apariencia actual del builder para el personaje activo.
    /// Llamar después de cualquier cambio de vestuario (menú de equipamiento, etc.)
    /// para que al hacer switch de personaje se mantenga la apariencia correcta.
    /// </summary>
    public void CaptureActiveCharacterAppearance()
    {
        var slot = PartyControlManager.Instance?.ActiveSlot ?? PartyControlManager.CharacterSlot.Will;
        CaptureCurrentAppearance(slot);
    }

    /// <summary>Devuelve una copia del diccionario de apariencia del slot.</summary>
    public Dictionary<PartCategory, string> GetAppearance(PartyControlManager.CharacterSlot slot)
    {
        var src = _appearances[(int)slot];
        return src != null
            ? new Dictionary<PartCategory, string>(src)
            : new Dictionary<PartCategory, string>();
    }

    /// <summary>
    /// Devuelve la apariencia como List&lt;AppearanceEntry&gt; para serialización en save data.
    /// </summary>
    public List<AppearanceEntry> GetAppearanceAsList(PartyControlManager.CharacterSlot slot)
    {
        var list = new List<AppearanceEntry>();
        var dict = _appearances[(int)slot];
        if (dict == null) return list;
        foreach (var kv in dict)
            list.Add(new AppearanceEntry { category = kv.Key, partName = kv.Value });
        return list;
    }

    /// <summary>
    /// Carga una apariencia desde datos guardados (p.ej. al continuar una partida).
    /// No aplica el cambio al builder — llama a ApplyAppearance() después si es necesario.
    /// Si slot es Will, marca el snapshot como tomado para que EnsureWillSnapshot
    /// no sobreescriba estos datos con el estado actual del builder.
    /// </summary>
    public void SetAppearanceFromList(PartyControlManager.CharacterSlot slot, List<AppearanceEntry> entries)
    {
        _appearances[(int)slot] = ToDict(entries);
        if (slot == PartyControlManager.CharacterSlot.Will)
            _willSnapshotTaken = true;
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Captura el snapshot de Will desde el builder ANTES de que se aplique cualquier preset.
    /// Usar el builder proporcionado directamente evita dependencias de timing con PlayerService.
    /// </summary>
    public void SnapshotWillFromBuilderIfNeeded(ModularAutoBuilder b)
    {
        if (_willSnapshotTaken) return;
        if (b == null) return;
        _appearances[(int)PartyControlManager.CharacterSlot.Will] = b.GetSelection();
        _willSnapshotTaken = true;
        Debug.Log($"[CharacterAppearanceRegistry] 📸 Pre-snapshot Will tomado: [{string.Join(", ", System.Linq.Enumerable.Select(_appearances[(int)PartyControlManager.CharacterSlot.Will], kv => $"{kv.Key}:{kv.Value}"))}]");
    }

    // Captura el snapshot de Will la primera vez que el builder está disponible.
    private void EnsureWillSnapshot(ModularAutoBuilder b)
    {
        if (_willSnapshotTaken) return;
        _appearances[(int)PartyControlManager.CharacterSlot.Will] = b.GetSelection();
        _willSnapshotTaken = true;
    }

    private static Dictionary<PartCategory, string> ToDict(List<AppearanceEntry> entries)
    {
        var dict = new Dictionary<PartCategory, string>();
        if (entries == null) return dict;
        foreach (var e in entries)
            if (!string.IsNullOrEmpty(e.partName))
                dict[e.category] = e.partName;
        return dict;
    }
}
