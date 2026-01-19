using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public class QuestManager : MonoBehaviour
{
    public static QuestManager Instance { get; private set; }

    [Tooltip("Catálogo opcional para arrancar quests por ID aunque no se hayan añadido antes.")]
    [SerializeField] private List<QuestData> questCatalog = new();

    // runtime: questId -> RuntimeQuest
    private readonly Dictionary<string, RuntimeQuest> _runtime = new(64);

    // visibilidad por quest (archivada vs visible)
    private readonly Dictionary<string, QuestVisibility> _visibility = new(StringComparer.Ordinal);
    // seguimiento ("seguir" misión en el tracker)
    private readonly HashSet<string> _followed = new(StringComparer.Ordinal);

    // índice: conditionId -> lista de (questId, stepIndex) para completar en O(1)
    private readonly Dictionary<string, List<StepRef>> _conditionIndex = new(64, StringComparer.Ordinal);

    // ✅ NUEVO: Referencia al inventario para detectar items añadidos
    private Inventory _cachedInventory;
    private bool _isSubscribedToInventory;
    
    // ✅ NUEVO: Referencia al wardrobe para detectar items de wardrobe añadidos
    private WardrobeInventory _cachedWardrobe;
    private bool _isSubscribedToWardrobe;

    // Eventos públicos para UI/lógica externa
    public event Action<string> OnQuestStarted;
    public event Action<string> OnQuestCompleted;
    public event Action<string, int> OnStepCompleted;
    public event Action OnQuestsChanged;
    public event Action<string, QuestVisibility> OnQuestVisibilityChanged;
    public event Action<string, bool> OnQuestFollowChanged;

    #region Unity
    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
    
    void Start()
    {
        Debug.Log("[QuestManager] 🚀 Start - Intentando suscribirse al inventario y wardrobe");
        // Intentar suscribirse al inventario y wardrobe
        TrySubscribeToInventory();
        TrySubscribeToWardrobe();
    }
    
    void OnEnable()
    {
        // Suscribirse cuando el player se registre
        PlayerService.OnPlayerRegistered += OnPlayerRegistered;
    }
    
    void OnDisable()
    {
        PlayerService.OnPlayerRegistered -= OnPlayerRegistered;
        UnsubscribeFromInventory();
        UnsubscribeFromWardrobe();
    }
    
    private void OnPlayerRegistered(GameObject player)
    {
        Debug.Log($"[QuestManager] 🎮 OnPlayerRegistered llamado para '{player?.name}'");
        // Re-intentar suscripción cuando el player está disponible
        TrySubscribeToInventory();
        TrySubscribeToWardrobe();
    }
    #endregion

    #region API básica
    public bool HasQuest(string questId) => _runtime.ContainsKey(questId);

    public QuestState GetState(string questId)
        => _runtime.TryGetValue(questId, out var rq) ? rq.State : QuestState.Inactive;

    public IEnumerable<RuntimeQuest> GetAll() => _runtime.Values;

    public QuestVisibility GetVisibility(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return QuestVisibility.Visible;
        if (_visibility.TryGetValue(questId, out var v)) return v;
        return QuestVisibility.Visible;
    }

    public void SetVisibility(string questId, QuestVisibility state)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (!_runtime.ContainsKey(questId)) return;

        var current = GetVisibility(questId);
        if (current == state) return;

        _visibility[questId] = state;
        OnQuestVisibilityChanged?.Invoke(questId, state);
        OnQuestsChanged?.Invoke();
    }

    public bool IsFollowed(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return false;
        return _followed.Contains(questId);
    }

    public void SetFollowed(string questId, bool followed)
    {
        if (string.IsNullOrEmpty(questId)) return;
        if (!_runtime.ContainsKey(questId)) return;

        bool changed;
        if (followed)
            changed = _followed.Add(questId);
        else
            changed = _followed.Remove(questId);

        if (changed)
        {
            OnQuestFollowChanged?.Invoke(questId, followed);
            OnQuestsChanged?.Invoke();
        }
    }

    public void AddQuest(QuestData data)
    {
        if (!data || string.IsNullOrEmpty(data.questId) || _runtime.ContainsKey(data.questId)) return;

        var rq = new RuntimeQuest(data);
        _runtime[data.questId] = rq;
        _visibility[data.questId] = QuestVisibility.Visible;
        _followed.Remove(data.questId);
        IndexQuestConditions(rq);
        OnQuestsChanged?.Invoke();
    }

    public void StartQuest(string questId)
    {
        if (!_runtime.TryGetValue(questId, out var rq))
        {
            var data = questCatalog.FirstOrDefault(q => q && q.questId == questId);
            if (!data) return;

            rq = new RuntimeQuest(data);
            _runtime[questId] = rq;
            _visibility[questId] = QuestVisibility.Visible;
            _followed.Remove(questId);
            IndexQuestConditions(rq);
        }

        if (rq.State == QuestState.Inactive)
        {
            rq.State = QuestState.Active;
            
            // Verificar si el jugador ya tiene items requeridos en el inventario
            CheckExistingItemsForQuest(rq);
            
            OnQuestStarted?.Invoke(questId);
            OnQuestsChanged?.Invoke();
        }
    }

    public void CompleteQuest(string questId)
    {
        if (!_runtime.TryGetValue(questId, out var rq)) return;
        if (rq.State == QuestState.Completed) return;

        // ✅ Consumir items del inventario si están configurados para ser consumidos
        ConsumeRequiredItems(questId);

        rq.State = QuestState.Completed;
        OnQuestCompleted?.Invoke(questId);
        ArchiveCompletedQuest(questId);
        OnQuestsChanged?.Invoke();
    }
    
    /// <summary>
    /// Consume los items requeridos de una quest del inventario del jugador.
    /// Solo consume items donde consumeOnComplete = true.
    /// </summary>
    private void ConsumeRequiredItems(string questId)
    {
        Debug.Log($"[QuestManager.ConsumeRequiredItems] 🔍 Iniciando para quest '{questId}'");
        
        var questEntry = FindQuestChainEntry(questId);
        if (questEntry == null)
        {
            Debug.LogWarning($"[QuestManager.ConsumeRequiredItems] ❌ No se encontró QuestChainEntry para quest '{questId}'");
            return;
        }
        
        if (questEntry.requiredItems == null || questEntry.requiredItems.Length == 0)
        {
            Debug.Log($"[QuestManager.ConsumeRequiredItems] ℹ️ Quest '{questId}' no tiene items requeridos");
            return;
        }

        Debug.Log($"[QuestManager.ConsumeRequiredItems] Quest '{questId}' tiene {questEntry.requiredItems.Length} items requeridos");

        // Obtener el inventario del jugador usando PlayerService (consistente con el resto del código)
        if (!PlayerService.TryGetComponent(out Inventory inventory, includeInactive: true, allowSceneLookup: true))
        {
            Debug.LogWarning($"[QuestManager.ConsumeRequiredItems] ❌ No se pudo obtener Inventory para consumir items de quest '{questId}'");
            return;
        }

        Debug.Log($"[QuestManager.ConsumeRequiredItems] ✅ Inventario obtenido, procesando items...");

        foreach (var itemReq in questEntry.requiredItems)
        {
            if (itemReq.item == null)
            {
                Debug.LogWarning($"[QuestManager.ConsumeRequiredItems] ⚠️ Item requerido es null en quest '{questId}'");
                continue;
            }

            Debug.Log($"[QuestManager.ConsumeRequiredItems] Procesando item '{itemReq.item.itemId}' - consumeOnComplete={itemReq.consumeOnComplete}");
            
            if (!itemReq.consumeOnComplete)
            {
                Debug.Log($"[QuestManager.ConsumeRequiredItems] ⏭️ Item '{itemReq.item.itemId}' no está configurado para ser consumido (consumeOnComplete=false)");
                continue;
            }

            // Verificar que el jugador tiene suficientes items antes de consumir
            int currentCount = inventory.Count(itemReq.item.itemId);
            Debug.Log($"[QuestManager.ConsumeRequiredItems] Cantidad en inventario de '{itemReq.item.itemId}': {currentCount}/{itemReq.amount}");
            
            if (currentCount < itemReq.amount)
            {
                Debug.LogWarning($"[QuestManager.ConsumeRequiredItems] ⚠️ El jugador no tiene suficientes '{itemReq.item.itemId}' para consumir ({currentCount}/{itemReq.amount})");
                continue;
            }

            // Consumir los items usando TryConsume
            Debug.Log($"[QuestManager.ConsumeRequiredItems] Intentando consumir {itemReq.amount}x '{itemReq.item.itemId}'...");
            
            if (inventory.TryConsume(itemReq.item, itemReq.amount, notifyChanges: true))
            {
                Debug.Log($"[QuestManager.ConsumeRequiredItems] ✅ Consumido {itemReq.amount}x '{itemReq.item.itemId}' del inventario al completar quest '{questId}'");
            }
            else
            {
                Debug.LogWarning($"[QuestManager.ConsumeRequiredItems] ❌ Falló al consumir {itemReq.amount}x '{itemReq.item.itemId}'");
            }
        }
        
        Debug.Log($"[QuestManager.ConsumeRequiredItems] ✅ Proceso completado para quest '{questId}'");
    }

    public void MarkStepDone(string questId, int stepIndex)
    {
        if (!_runtime.TryGetValue(questId, out var rq)) return;
        if (rq.State != QuestState.Active) return;
        if ((uint)stepIndex >= (uint)rq.Steps.Length) return;

        var step = rq.Steps[stepIndex];
        if (step.completed) return;

        step.completed = true;
        OnStepCompleted?.Invoke(questId, stepIndex);

        if (AllStepsCompleted(rq))
        {
            rq.State = QuestState.Completed;
            OnQuestCompleted?.Invoke(questId);
            ArchiveCompletedQuest(questId);
        }

        OnQuestsChanged?.Invoke();
    }

    /// <summary>
    /// Completa un step específico de una quest usando su Condition ID.
    /// Este es el método recomendado para usar desde nodos narrativos.
    /// </summary>
    public void CompleteQuestStepByConditionId(string questId, string stepConditionId)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(stepConditionId))
        {
            Debug.LogWarning($"[QuestManager] CompleteQuestStepByConditionId - questId o stepConditionId vacío");
            return;
        }

        if (!_runtime.TryGetValue(questId, out var rq))
        {
            Debug.LogWarning($"[QuestManager] CompleteQuestStepByConditionId - Quest '{questId}' no existe en runtime");
            return;
        }

        if (rq.Steps == null || rq.Steps.Length == 0)
        {
            Debug.LogWarning($"[QuestManager] CompleteQuestStepByConditionId - Quest '{questId}' no tiene steps");
            return;
        }

        // Buscar el step por su conditionId
        int stepIndex = -1;
        for (int i = 0; i < rq.Steps.Length; i++)
        {
            if (rq.Steps[i].conditionId == stepConditionId)
            {
                stepIndex = i;
                break;
            }
        }

        if (stepIndex < 0)
        {
            Debug.LogWarning($"[QuestManager] CompleteQuestStepByConditionId - No se encontró step con conditionId '{stepConditionId}' en quest '{questId}'");
            return;
        }

        // Usar el método existente para completar el step
        Debug.Log($"[QuestManager] ✅ Completando step '{stepConditionId}' (índice {stepIndex}) en quest '{questId}'");
        MarkStepDone(questId, stepIndex);
    }

    public bool IsStepCompleted(string questId, int stepIndex)
        => _runtime.TryGetValue(questId, out var rq)
           && (uint)stepIndex < (uint)rq.Steps.Length
           && rq.Steps[stepIndex].completed;

    public bool AreAllStepsCompleted(string questId)
        => _runtime.TryGetValue(questId, out var rq) && AllStepsCompleted(rq);

    /// <summary>
    /// Busca el índice de un step por su conditionId.
    /// Retorna -1 si no se encuentra.
    /// </summary>
    public int FindStepIndexByConditionId(string questId, string conditionId)
    {
        if (string.IsNullOrEmpty(questId) || string.IsNullOrEmpty(conditionId))
            return -1;
        
        if (!_runtime.TryGetValue(questId, out var rq) || rq.Steps == null)
            return -1;
        
        for (int i = 0; i < rq.Steps.Length; i++)
        {
            if (string.Equals(rq.Steps[i].conditionId, conditionId, StringComparison.OrdinalIgnoreCase))
                return i;
        }
        
        return -1;
    }

    public void CompleteByCondition(string conditionId)
    {
        if (string.IsNullOrEmpty(conditionId)) return;
        if (!_conditionIndex.TryGetValue(conditionId, out var list)) return;

        for (int i = 0; i < list.Count; i++)
        {
            var sr = list[i];
            if (GetState(sr.questId) != QuestState.Active) continue;
            MarkStepDone(sr.questId, sr.stepIndex);
        }
    }

    void ArchiveCompletedQuest(string questId)
    {
        if (string.IsNullOrEmpty(questId)) return;
        SetFollowed(questId, false);
        SetVisibility(questId, QuestVisibility.Hidden);
    }
    #endregion

    #region Persistencia vía flags (export/import)
    // Formato de flags:
    //   QUEST_COMPLETED:<questId>
    //   QUEST_ACTIVE:<questId>
    //   QUEST_STEP_DONE:<questId>:<stepIndex>
    //   QUEST_ARCHIVED:<questId>
    //   QUEST_FOLLOWED:<questId>

    private const string Q_COMPLETED = "QUEST_COMPLETED:";
    private const string Q_ACTIVE    = "QUEST_ACTIVE:";
    private const string Q_STEP_DONE = "QUEST_STEP_DONE:";
    private const string Q_ARCHIVED  = "QUEST_ARCHIVED:";
    private const string Q_FOLLOWED  = "QUEST_FOLLOWED:"; // legacy alias
    private const string Q_TRACKED   = "QUEST_TRACKED:";

    /// <summary>Reconstruye el estado a partir de flags del perfil.</summary>
    public void RestoreFromProfileFlags(IReadOnlyList<string> flags)
    {
        ResetAllQuests();

        if (flags == null || flags.Count == 0) return;

        var toActive = new HashSet<string>(StringComparer.Ordinal);
        var toArchived = new HashSet<string>(StringComparer.Ordinal);
        var toFollowed = new HashSet<string>(StringComparer.Ordinal);

        // 1) Marcar completadas / recopilar activas
        for (int i = 0; i < flags.Count; i++)
        {
            var f = flags[i];
            if (string.IsNullOrEmpty(f)) continue;

            if (f.StartsWith(Q_COMPLETED, StringComparison.Ordinal))
            {
                var qid = f.Substring(Q_COMPLETED.Length);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out var rq);
                rq.State = QuestState.Completed;
                _visibility[qid] = QuestVisibility.Hidden;
                // Marcar todos los pasos como completados si la misión está completada
                if (rq.Steps != null)
                {
                    for (int s = 0; s < rq.Steps.Length; s++)
                        rq.Steps[s].completed = true;
                }
            }
            else if (f.StartsWith(Q_ACTIVE, StringComparison.Ordinal))
            {
                var qid = f.Substring(Q_ACTIVE.Length);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out _);
                toActive.Add(qid);
            }
            else if (f.StartsWith(Q_ARCHIVED, StringComparison.Ordinal))
            {
                var qid = f.Substring(Q_ARCHIVED.Length);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out _);
                toArchived.Add(qid);
            }
            else if (f.StartsWith(Q_FOLLOWED, StringComparison.Ordinal) || f.StartsWith(Q_TRACKED, StringComparison.Ordinal))
            {
                var prefixLen = f.StartsWith(Q_FOLLOWED, StringComparison.Ordinal) ? Q_FOLLOWED.Length : Q_TRACKED.Length;
                var qid = f.Substring(prefixLen);
                if (string.IsNullOrEmpty(qid)) continue;
                EnsureRuntimeQuest(qid, out _);
                toFollowed.Add(qid);
            }
        }

        foreach (var qid in toActive)
        {
            if (_runtime.TryGetValue(qid, out var rq) && rq.State != QuestState.Completed)
                rq.State = QuestState.Active;
        }

        // 2) Aplicar visibilidad archivada
        foreach (var qid in toArchived)
        {
            if (_runtime.ContainsKey(qid))
                _visibility[qid] = QuestVisibility.Hidden;
        }

        // 3) Aplicar seguimiento
        _followed.Clear();
        foreach (var qid in toFollowed)
        {
            if (_runtime.ContainsKey(qid))
            {
                _followed.Add(qid);
                // No sobrescribir "Hidden" con "Tracked": si está archivada, mantener Hidden
                if (GetVisibility(qid) != QuestVisibility.Hidden)
                    _visibility[qid] = QuestVisibility.Tracked;
            }
        }

        // 4b) Auto-archivar completadas y limpiar seguimiento
        foreach (var kvp in _runtime)
        {
            if (kvp.Value.State == QuestState.Completed)
            {
                _followed.Remove(kvp.Key);
                _visibility[kvp.Key] = QuestVisibility.Hidden;
            }
        }

        // 4) Marcar pasos completados
        for (int i = 0; i < flags.Count; i++)
        {
            var f = flags[i];
            if (string.IsNullOrEmpty(f)) continue;
            if (!f.StartsWith(Q_STEP_DONE, StringComparison.Ordinal)) continue;

            var rest = f.Substring(Q_STEP_DONE.Length);
            var sep = rest.LastIndexOf(':');
            if (sep <= 0) continue;

            var qid = rest.Substring(0, sep);
            var idxStr = rest.Substring(sep + 1);
            if (!int.TryParse(idxStr, out int stepIdx)) continue;

            EnsureRuntimeQuest(qid, out var rq2);
            if (rq2.State == QuestState.Inactive) rq2.State = QuestState.Active;
            if ((uint)stepIdx < (uint)rq2.Steps.Length)
                rq2.Steps[stepIdx].completed = true;
        }

        OnQuestsChanged?.Invoke();

        // helper local
        void EnsureRuntimeQuest(string questId, out RuntimeQuest rqOut)
        {
            if (!_runtime.TryGetValue(questId, out rqOut))
            {
                var data = questCatalog.FirstOrDefault(q => q && q.questId == questId);
                if (data != null)
                {
                    rqOut = new RuntimeQuest(data);
                    _runtime[questId] = rqOut;
                    IndexQuestConditions(rqOut);
                }
            }
        }
    }

    /// <summary>Vuelca el estado actual a una lista de flags (determinista).</summary>
    public void ExportFlags(List<string> outFlags)
    {
        if (outFlags == null) return;

        foreach (var rq in _runtime.Values)
        {
            var state = rq.State;
            if (state == QuestState.Completed)
            {
                outFlags.Add(Q_COMPLETED + rq.Id);
            }
            else if (state == QuestState.Active)
            {
                outFlags.Add(Q_ACTIVE + rq.Id);
                for (int i = 0; i < rq.Steps.Length; i++)
                    if (rq.Steps[i].completed)
                        outFlags.Add($"{Q_STEP_DONE}{rq.Id}:{i}");
            }

            // Visibilidad: si está archivada, emitir flag incluso cuando esté completada
            var vis = GetVisibility(rq.Id);
            if (vis == QuestVisibility.Hidden)
            {
                outFlags.Add(Q_ARCHIVED + rq.Id);
            }
            else if (IsFollowed(rq.Id) || vis == QuestVisibility.Tracked)
            {
                // Seguimiento: emitir solo si NO está archivada. Evita restaurar archivadas como "tracked".
                outFlags.Add(Q_TRACKED + rq.Id);
            }
        }
    }
    #endregion

    #region Internals
    private static bool AllStepsCompleted(RuntimeQuest rq)
    {
        var steps = rq.Steps;
        for (int i = 0; i < steps.Length; i++)
            if (!steps[i].completed) return false;
        return true;
    }

    private void IndexQuestConditions(RuntimeQuest rq)
    {
        var steps = rq.Steps;
        for (int i = 0; i < steps.Length; i++)
        {
            var cid = steps[i].conditionId;
            if (string.IsNullOrEmpty(cid)) continue;

            if (!_conditionIndex.TryGetValue(cid, out var lst))
            {
                lst = new List<StepRef>(2);
                _conditionIndex[cid] = lst;
            }
            lst.Add(new StepRef(rq.Id, i));
        }
    }

    private readonly struct StepRef
    {
        public readonly string questId;
        public readonly int stepIndex;
        public StepRef(string q, int i) { questId = q; stepIndex = i; }
    }

    // ===== Runtime model =====
    public class RuntimeQuest
    {
        public string Id => Data.questId;
        public QuestData Data { get; }
        public QuestState State { get; set; }
        public QuestStep[] Steps { get; }

        public RuntimeQuest(QuestData data)
        {
            Data = data;
            State = QuestState.Inactive;

            if (data.steps == null || data.steps.Length == 0)
            {
                Steps = Array.Empty<QuestStep>();
                return;
            }

            Steps = new QuestStep[data.steps.Length];
            for (int i = 0; i < data.steps.Length; i++)
            {
                var s = data.steps[i];
                Steps[i] = new QuestStep
                {
                    description = s.description,
                    conditionId = s.conditionId,
                    completed = false
                };
            }
        }
    }
    #endregion

    /// <summary>
    /// Verifica si el jugador ya tiene items requeridos por la quest en su inventario y wardrobe
    /// y marca automáticamente los pasos correspondientes como completados.
    /// </summary>
    private void CheckExistingItemsForQuest(RuntimeQuest rq)
    {
        if (rq == null || rq.Steps == null || rq.Steps.Length == 0)
            return;
        
        Debug.Log($"[QuestManager] 🔍 CheckExistingItemsForQuest para quest '{rq.Id}'");
        
        int totalStepsCompleted = 0;
        
        // === Verificar items del inventario normal ===
        if (PlayerService.TryGetComponent(out Inventory inventory, includeInactive: true, allowSceneLookup: true))
        {
            Debug.Log($"[QuestManager] ✅ Inventory encontrado");
            
            var questEntry = FindQuestChainEntry(rq.Id);
            if (questEntry != null && questEntry.requiredItems != null && questEntry.requiredItems.Length > 0)
            {
                Debug.Log($"[QuestManager] 📦 Quest tiene {questEntry.requiredItems.Length} items de inventario requeridos");
                totalStepsCompleted += CheckInventoryItemsForQuest(rq, inventory, questEntry.requiredItems);
            }
        }
        else
        {
            Debug.LogWarning($"[QuestManager] ❌ No se pudo obtener Inventory");
        }
        
        // === Verificar items del wardrobe ===
        if (PlayerService.TryGetComponent(out WardrobeInventory wardrobe, includeInactive: true, allowSceneLookup: true))
        {
            Debug.Log($"[QuestManager] ✅ WardrobeInventory encontrado");
            
            var questEntry = FindQuestChainEntry(rq.Id);
            if (questEntry != null && questEntry.requiredWardrobeItems != null && questEntry.requiredWardrobeItems.Length > 0)
            {
                Debug.Log($"[QuestManager] 👗 Quest tiene {questEntry.requiredWardrobeItems.Length} items de wardrobe requeridos");
                totalStepsCompleted += CheckWardrobeItemsForQuest(rq, wardrobe, questEntry.requiredWardrobeItems);
            }
        }
        else
        {
            Debug.LogWarning($"[QuestManager] ⚠️ No se pudo obtener WardrobeInventory");
        }
        
        // Notificar cambios si se completó algún step
        if (totalStepsCompleted > 0)
        {
            Debug.Log($"[QuestManager] ✅ {totalStepsCompleted} steps marcados como completados. Jugador debe volver a hablar con el NPC para completar la quest.");
            OnQuestsChanged?.Invoke();
        }
    }
    
    /// <summary>
    /// Verifica items del inventario para una quest
    /// </summary>
    private int CheckInventoryItemsForQuest(RuntimeQuest rq, Inventory inventory, Game.NPC.Modules.ItemRequirement[] requiredItems)
    {
        int stepsCompleted = 0;
        
        foreach (var itemReq in requiredItems)
        {
            if (itemReq.item == null)
                continue;
            
            string conditionId = itemReq.GetStepConditionId();
            
            Debug.Log($"[QuestManager] Verificando item '{itemReq.item.itemId}' (cantidad requerida: {itemReq.amount})");
            Debug.Log($"[QuestManager]   stepIndex: {itemReq.stepIndex}, conditionId: '{conditionId ?? "null (usar index)"}'");
            
            int count = inventory.Count(itemReq.item.itemId);
            Debug.Log($"[QuestManager] Jugador tiene {count} de '{itemReq.item.itemId}'");
            
            if (count < itemReq.amount)
            {
                Debug.Log($"[QuestManager] ❌ Insuficiente (necesita {itemReq.amount})");
                continue;
            }
            
            Debug.Log($"[QuestManager] ✅ Suficiente! Buscando step asociado...");
            
            int stepIdx = FindStepIndex(rq, conditionId, itemReq.stepIndex);
            
            if (stepIdx >= 0 && !rq.Steps[stepIdx].completed)
            {
                rq.Steps[stepIdx].completed = true;
                stepsCompleted++;
                Debug.Log($"[QuestManager] ✅ Step {stepIdx} marcado como completado por item de inventario");
            }
        }
        
        return stepsCompleted;
    }
    
    /// <summary>
    /// Verifica items del wardrobe para una quest
    /// </summary>
    private int CheckWardrobeItemsForQuest(RuntimeQuest rq, WardrobeInventory wardrobe, Game.NPC.Modules.WardrobeItemRequirement[] requiredWardrobeItems)
    {
        int stepsCompleted = 0;
        
        foreach (var wardrobeReq in requiredWardrobeItems)
        {
            if (wardrobeReq.item == null)
                continue;
            
            string conditionId = wardrobeReq.GetStepConditionId();
            
            Debug.Log($"[QuestManager] Verificando item de wardrobe '{wardrobeReq.item.WardrobeId}'");
            Debug.Log($"[QuestManager]   Category: {wardrobeReq.item.Category}, PartName: {wardrobeReq.item.PartName}");
            Debug.Log($"[QuestManager]   stepIndex: {wardrobeReq.stepIndex}, conditionId: '{conditionId ?? "null (usar index)"}'");
            
            bool hasItem = wardrobe.TryGetEntry(wardrobeReq.item.Category, wardrobeReq.item.PartName, out _);
            
            if (!hasItem)
            {
                Debug.Log($"[QuestManager] ❌ Jugador no tiene '{wardrobeReq.item.WardrobeId}' desbloqueado");
                continue;
            }
            
            Debug.Log($"[QuestManager] ✅ Jugador tiene item desbloqueado! Buscando step asociado...");
            
            int stepIdx = FindStepIndex(rq, conditionId, wardrobeReq.stepIndex);
            
            if (stepIdx >= 0 && !rq.Steps[stepIdx].completed)
            {
                rq.Steps[stepIdx].completed = true;
                stepsCompleted++;
                Debug.Log($"[QuestManager] ✅ Step {stepIdx} marcado como completado por item de wardrobe");
            }
        }
        
        return stepsCompleted;
    }
    
    /// <summary>
    /// Busca el índice de un step basándose en conditionId o stepIndex
    /// PRIORIDAD: stepIndex > conditionId
    /// </summary>
    private int FindStepIndex(RuntimeQuest rq, string conditionId, int stepIndex)
    {
        // Prioridad 1: Usar stepIndex directamente si es válido
        if (stepIndex >= 0 && stepIndex < rq.Steps.Length && conditionId == null)
        {
            Debug.Log($"[QuestManager] ✅ Usando stepIndex directo: {stepIndex}");
            return stepIndex;
        }
        
        // Prioridad 2: Buscar por conditionId
        if (!string.IsNullOrEmpty(conditionId))
        {
            Debug.Log($"[QuestManager] Buscando step por conditionId: '{conditionId}'");
            
            for (int i = 0; i < rq.Steps.Length; i++)
            {
                if (rq.Steps[i].conditionId == conditionId)
                {
                    Debug.Log($"[QuestManager] ✅ Match encontrado en step {i}");
                    return i;
                }
            }
            
            Debug.LogWarning($"[QuestManager] ⚠️ No se encontró step con conditionId '{conditionId}'");
        }
        else
        {
            Debug.LogWarning($"[QuestManager] ⚠️ Item no tiene stepIndex válido ni conditionId");
        }
        
        return -1;
    }
    
    /// <summary>
    /// Busca el QuestChainEntry correspondiente a un questId en TODOS los NPCs de la escena.
    /// </summary>
    private Game.NPC.Modules.QuestChainEntry FindQuestChainEntry(string questId)
    {
        Debug.Log($"[QuestManager] 🔍 FindQuestChainEntry para '{questId}'");
        
        // Buscar en TODOS los NPCBehaviourManagerV2 de la escena (arquitectura correcta)
        var allNpcManagers = FindObjectsByType<Game.NPC.NPCBehaviourManagerV2>(FindObjectsSortMode.None);
        
        if (allNpcManagers == null || allNpcManagers.Length == 0)
        {
            Debug.LogWarning($"[QuestManager] ❌ No se encontraron NPCBehaviourManagerV2 en la escena");
            return null;
        }
        
        Debug.Log($"[QuestManager] Encontrados {allNpcManagers.Length} NPCs en la escena");
        
        foreach (var npcManager in allNpcManagers)
        {
            if (npcManager == null || npcManager.Configuration == null)
                continue;
            
            Debug.Log($"[QuestManager] Revisando NPC '{npcManager.name}'");
            
            if (npcManager.Configuration.questConfig == null)
            {
                Debug.Log($"[QuestManager]   ℹ️ NPC '{npcManager.name}' no tiene questConfig");
                continue;
            }
            
            var questConfig = npcManager.Configuration.questConfig;
            if (questConfig.questChain == null || questConfig.questChain.Length == 0)
            {
                Debug.Log($"[QuestManager]   ℹ️ NPC '{npcManager.name}' - questChain vacío");
                continue;
            }
            
            Debug.Log($"[QuestManager]   NPC '{npcManager.name}' tiene {questConfig.questChain.Length} quests");
            
            foreach (var entry in questConfig.questChain)
            {
                if (entry.questData == null)
                    continue;
                
                Debug.Log($"[QuestManager]     - Quest: '{entry.questData.questId}'");
                
                if (entry.questData.questId == questId)
                {
                    Debug.Log($"[QuestManager] ✅ Quest '{questId}' encontrada en NPC '{npcManager.name}'!");
                    Debug.Log($"[QuestManager]     RequiredItems: {(entry.requiredItems?.Length ?? 0)}");
                    return entry;
                }
            }
        }
        
        Debug.LogWarning($"[QuestManager] ❌ Quest '{questId}' NO encontrada en ningún NPC");
        return null;
    }

    /// <summary>
    /// Elimina todas las misiones activas y su progreso. Útil para nueva partida.
    /// </summary>
    public void ResetAllQuests()
    {
        _runtime.Clear();
        _conditionIndex.Clear();
        _visibility.Clear();
        _followed.Clear();
        OnQuestsChanged?.Invoke();
    }
    
    #region Inventory Item Detection
    
    /// <summary>
    /// Intenta suscribirse al evento OnItemAdded del inventario del jugador.
    /// Se llama automáticamente cuando el jugador está disponible.
    /// </summary>
    private void TrySubscribeToInventory()
    {
        if (!PlayerService.TryGetComponent(out Inventory inventory, includeInactive: true, allowSceneLookup: true))
        {
            // El inventario no está disponible todavía, se intentará más tarde
            Debug.Log("[QuestManager] ⏳ Inventory no disponible aún para suscripción");
            return;
        }
        
        // Verificar si el inventario cacheado fue destruido (Unity null check)
        bool cachedIsValid = _cachedInventory != null && _cachedInventory; // Unity operator check
        
        // Verificar si ya estamos suscritos al mismo inventario válido
        if (_isSubscribedToInventory && cachedIsValid && _cachedInventory == inventory)
        {
            Debug.Log("[QuestManager] ✅ Ya suscrito al mismo inventario válido");
            return; // Ya suscritos al mismo inventario válido
        }
        
        // Si estábamos suscritos a un inventario diferente o destruido, desuscribirse primero
        if (_isSubscribedToInventory && cachedIsValid && _cachedInventory != inventory)
        {
            Debug.Log("[QuestManager] 🔄 Inventario cambió, re-suscribiendo...");
            _cachedInventory.OnItemAdded -= OnInventoryItemAdded;
        }
        else if (_isSubscribedToInventory && !cachedIsValid)
        {
            Debug.Log("[QuestManager] ⚠️ Inventario cacheado fue destruido, re-suscribiendo a nuevo inventario...");
        }
        
        _cachedInventory = inventory;
        _cachedInventory.OnItemAdded += OnInventoryItemAdded;
        _isSubscribedToInventory = true;
        Debug.Log($"[QuestManager] ✅ Suscrito a Inventory.OnItemAdded (instancia: {inventory.GetInstanceID()}) para detectar items de quests");
    }
    
    /// <summary>
    /// Desuscribirse del inventario al deshabilitarse
    /// </summary>
    private void UnsubscribeFromInventory()
    {
        if (!_isSubscribedToInventory || _cachedInventory == null) return;
        
        _cachedInventory.OnItemAdded -= OnInventoryItemAdded;
        _isSubscribedToInventory = false;
        _cachedInventory = null;
        Debug.Log("[QuestManager] Desuscrito de Inventory.OnItemAdded");
    }
    
    /// <summary>
    /// Callback cuando se añade un item al inventario.
    /// Verifica si alguna quest activa requiere ese item.
    /// </summary>
    private void OnInventoryItemAdded(ItemData item, int addedAmount, int newTotal)
    {
        Debug.Log($"[QuestManager] 🔔 OnInventoryItemAdded LLAMADO - item={item?.itemId ?? "NULL"}, added={addedAmount}, total={newTotal}");
        
        if (item == null) return;
        
        // Solo procesar si hay quests activas
        var activeQuests = _runtime.Values.Where(rq => rq.State == QuestState.Active).ToList();
        if (activeQuests.Count == 0)
        {
            Debug.Log("[QuestManager] ℹ️ No hay quests activas, ignorando item");
            return;
        }
        
        Debug.Log($"[QuestManager] 📦 Item '{item.itemId}' añadido al inventario (cantidad: {addedAmount}, total: {newTotal})");
        Debug.Log($"[QuestManager] Verificando {activeQuests.Count} quests activas...");
        
        foreach (var rq in activeQuests)
        {
            CheckItemForQuest(rq, item, newTotal);
        }
    }
    
    /// <summary>
    /// Verifica si un item específico cumple algún requisito de una quest.
    /// </summary>
    private void CheckItemForQuest(RuntimeQuest rq, ItemData item, int currentCount)
    {
        // Buscar el QuestChainEntry para obtener los requiredItems
        var questEntry = FindQuestChainEntry(rq.Id);
        if (questEntry == null || questEntry.requiredItems == null || questEntry.requiredItems.Length == 0)
            return;
        
        foreach (var itemReq in questEntry.requiredItems)
        {
            if (itemReq.item == null) continue;
            if (itemReq.item.itemId != item.itemId) continue;
            
            // Este item es requerido por esta quest
            Debug.Log($"[QuestManager] 🎯 Item '{item.itemId}' es requerido por quest '{rq.Id}'");
            
            // Verificar si tenemos suficientes
            if (currentCount < itemReq.amount)
            {
                Debug.Log($"[QuestManager] ⏳ Aún faltan items ({currentCount}/{itemReq.amount})");
                continue;
            }
            
            Debug.Log($"[QuestManager] ✅ Requisito cumplido ({currentCount}/{itemReq.amount})");
            
            // Buscar el step correspondiente
            // PRIORIDAD: stepIndex (si conditionId es null) > conditionId
            int stepIdx = -1;
            string conditionId = itemReq.GetStepConditionId(); // ✅ Retorna null si stepIndex es válido
            
            if (conditionId == null && itemReq.stepIndex >= 0 && itemReq.stepIndex < rq.Steps.Length)
            {
                // Prioridad 1: Usar stepIndex directamente
                stepIdx = itemReq.stepIndex;
                Debug.Log($"[QuestManager] Usando stepIndex directo: {stepIdx}");
            }
            else if (!string.IsNullOrEmpty(conditionId))
            {
                // Prioridad 2: Buscar por conditionId
                for (int i = 0; i < rq.Steps.Length; i++)
                {
                    if (rq.Steps[i].conditionId == conditionId)
                    {
                        stepIdx = i;
                        break;
                    }
                }
            }
            
            // Completar el paso si se encontró y no estaba completado
            if (stepIdx >= 0 && !rq.Steps[stepIdx].completed)
            {
                Debug.Log($"[QuestManager] 🎉 Completando step {stepIdx} de quest '{rq.Id}' por item '{item.itemId}'");
                MarkStepDone(rq.Id, stepIdx);
            }
        }
    }
    
    #endregion
    
    #region Wardrobe Item Detection
    
    /// <summary>
    /// Intenta suscribirse al evento OnWardrobeChanged del wardrobe del jugador.
    /// Se llama automáticamente cuando el jugador está disponible.
    /// </summary>
    private void TrySubscribeToWardrobe()
    {
        if (!PlayerService.TryGetComponent(out WardrobeInventory wardrobe, includeInactive: true, allowSceneLookup: true))
        {
            // El wardrobe no está disponible todavía, se intentará más tarde
            Debug.Log("[QuestManager] ⏳ WardrobeInventory no disponible aún para suscripción");
            return;
        }
        
        // Verificar si el wardrobe cacheado fue destruido (Unity null check)
        bool cachedIsValid = _cachedWardrobe != null && _cachedWardrobe; // Unity operator check
        
        // Verificar si ya estamos suscritos al mismo wardrobe válido
        if (_isSubscribedToWardrobe && cachedIsValid && _cachedWardrobe == wardrobe)
        {
            Debug.Log("[QuestManager] ✅ Ya suscrito al mismo wardrobe válido");
            return; // Ya suscritos al mismo wardrobe válido
        }
        
        // Si estábamos suscritos a un wardrobe diferente o destruido, desuscribirse primero
        if (_isSubscribedToWardrobe && cachedIsValid && _cachedWardrobe != wardrobe)
        {
            Debug.Log("[QuestManager] 🔄 Wardrobe cambió, re-suscribiendo...");
            _cachedWardrobe.OnWardrobeChanged -= OnWardrobeChanged;
        }
        else if (_isSubscribedToWardrobe && !cachedIsValid)
        {
            Debug.Log("[QuestManager] ⚠️ Wardrobe cacheado fue destruido, re-suscribiendo a nuevo wardrobe...");
        }
        
        _cachedWardrobe = wardrobe;
        _cachedWardrobe.OnWardrobeChanged += OnWardrobeChanged;
        _isSubscribedToWardrobe = true;
        Debug.Log($"[QuestManager] ✅ Suscrito a WardrobeInventory.OnWardrobeChanged (instancia: {wardrobe.GetInstanceID()}) para detectar items de wardrobe de quests");
    }
    
    /// <summary>
    /// Desuscribirse del wardrobe al deshabilitarse
    /// </summary>
    private void UnsubscribeFromWardrobe()
    {
        if (!_isSubscribedToWardrobe || _cachedWardrobe == null) return;
        
        _cachedWardrobe.OnWardrobeChanged -= OnWardrobeChanged;
        _isSubscribedToWardrobe = false;
        _cachedWardrobe = null;
        Debug.Log("[QuestManager] Desuscrito de WardrobeInventory.OnWardrobeChanged");
    }
    
    /// <summary>
    /// Callback cuando cambia el wardrobe del jugador.
    /// Verifica si alguna quest activa requiere items del wardrobe.
    /// </summary>
    private void OnWardrobeChanged()
    {
        // Solo procesar si hay quests activas
        var activeQuests = _runtime.Values.Where(rq => rq.State == QuestState.Active).ToList();
        if (activeQuests.Count == 0) return;
        
        Debug.Log($"[QuestManager] 👗 Wardrobe cambió - Verificando {activeQuests.Count} quests activas...");
        
        // Verificar cada quest activa para ver si alguna requiere items del wardrobe
        foreach (var rq in activeQuests)
        {
            CheckWardrobeForQuest(rq);
        }
    }
    
    /// <summary>
    /// Verifica si el wardrobe del jugador cumple algún requisito de una quest.
    /// </summary>
    private void CheckWardrobeForQuest(RuntimeQuest rq)
    {
        if (_cachedWardrobe == null) return;
        
        // Buscar el QuestChainEntry para obtener los requiredItems
        var questEntry = FindQuestChainEntry(rq.Id);
        if (questEntry == null || questEntry.requiredWardrobeItems == null || questEntry.requiredWardrobeItems.Length == 0)
            return;
        
        Debug.Log($"[QuestManager] 🔍 Quest '{rq.Id}' requiere {questEntry.requiredWardrobeItems.Length} items de wardrobe");
        
        foreach (var wardrobeReq in questEntry.requiredWardrobeItems)
        {
            if (wardrobeReq.item == null) continue;
            
            Debug.Log($"[QuestManager] 🎯 Verificando item de wardrobe '{wardrobeReq.item.WardrobeId}' (Category: {wardrobeReq.item.Category}, PartName: {wardrobeReq.item.PartName})");
            
            // Verificar si el jugador tiene este item desbloqueado
            bool hasItem = _cachedWardrobe.TryGetEntry(wardrobeReq.item.Category, wardrobeReq.item.PartName, out _);
            
            if (!hasItem)
            {
                Debug.Log($"[QuestManager] ❌ Jugador no tiene '{wardrobeReq.item.WardrobeId}' desbloqueado");
                continue;
            }
            
            Debug.Log($"[QuestManager] ✅ Jugador tiene '{wardrobeReq.item.WardrobeId}' desbloqueado");
            
            // Buscar el step correspondiente
            // PRIORIDAD: stepIndex (si conditionId es null) > conditionId
            int stepIdx = -1;
            string conditionId = wardrobeReq.GetStepConditionId(); // ✅ Retorna null si stepIndex es válido
            
            if (conditionId == null && wardrobeReq.stepIndex >= 0 && wardrobeReq.stepIndex < rq.Steps.Length)
            {
                // Prioridad 1: Usar stepIndex directamente
                stepIdx = wardrobeReq.stepIndex;
                Debug.Log($"[QuestManager] Usando stepIndex directo: {stepIdx}");
            }
            else if (!string.IsNullOrEmpty(conditionId))
            {
                // Prioridad 2: Buscar por conditionId
                Debug.Log($"[QuestManager] Buscando step con conditionId: '{conditionId}'");
                for (int i = 0; i < rq.Steps.Length; i++)
                {
                    if (rq.Steps[i].conditionId == conditionId)
                    {
                        stepIdx = i;
                        Debug.Log($"[QuestManager] ✅ Step encontrado en índice {i}");
                        break;
                    }
                }
            }
            
            // Completar el paso si se encontró y no estaba completado
            if (stepIdx >= 0 && !rq.Steps[stepIdx].completed)
            {
                Debug.Log($"[QuestManager] 🎉 Completando step {stepIdx} de quest '{rq.Id}' por item de wardrobe '{wardrobeReq.item.WardrobeId}'");
                MarkStepDone(rq.Id, stepIdx);
            }
            else if (stepIdx < 0)
            {
                Debug.LogWarning($"[QuestManager] ⚠️ No se encontró step válido para item de wardrobe '{wardrobeReq.item.WardrobeId}'");
            }
            else
            {
                Debug.Log($"[QuestManager] ℹ️ Step {stepIdx} ya estaba completado");
            }
        }
    }
    
    #endregion
}
