using UnityEngine;

/// <summary>
/// Muestra un marcador en el minimapa apuntando a este GameObject mientras el paso de quest
/// asociado esté pendiente. Colocar en cualquier objeto del mundo: NPCs, cofres, tiendas,
/// marcadores vacíos, etc.
///
/// El marcador se oculta automáticamente cuando:
///   - el paso se completa (ej: el jugador recoge el objeto)
///   - la quest se completa
///   - la quest no está activa
///
/// Compatible con SimpleQuestPickup y QuestProgressOnTrigger: cuando cualquiera de ellos
/// llama MarkStepDone, este componente reacciona vía OnStepCompleted.
///
/// stepIndex  &lt; 0  → visible durante toda la quest activa, sin ligarse a un paso concreto.
/// showAfterStep &lt; 0  → visible desde el inicio de la quest (sin prerrequisito).
/// showAfterStep = N  → solo aparece una vez completado el paso con índice N.
/// </summary>
public class QuestObjectiveMarker : MonoBehaviour
{
    [SerializeField] private string questId;

    [Tooltip("Índice del paso que representa este marcador.\n" +
             "-1 = visible durante toda la quest activa.")]
    [SerializeField] private int stepIndex = 0;

    [Tooltip("Índice del paso que debe completarse ANTES de que este marcador aparezca.\n" +
             "-1 = aparece desde el inicio de la quest (sin prerrequisito).\n" +
             "Ejemplo: poner 0 en el marcador del NPC de entrega para que solo aparezca\n" +
             "una vez recogida la caja (step 0 completado).")]
    [SerializeField] private int showAfterStep = -1;

    [SerializeField] private Sprite icon;
    [SerializeField] private Color color = Color.yellow;

    private MinimapMarker _marker;

    #region Unity

    private void Start()
    {
        // Hijo dedicado para el MinimapMarker: evita colisionar con el MinimapMarker
        // que NPCQuestIconManager añade dinámicamente al raíz del mismo NPC.
        var child = new GameObject("_QuestObjMarker").transform;
        child.SetParent(transform, false);
        _marker = child.gameObject.AddComponent<MinimapMarker>();
        _marker.SetIcon(icon, color);
        _marker.SetVisible(false);

        if (QuestManager.Instance != null)
            Subscribe();
        else
            GameBootService.OnProfileReady += OnProfileReady;

        Refresh();
    }

    private void OnDestroy()
    {
        Unsubscribe();
        GameBootService.OnProfileReady -= OnProfileReady;
    }

    #endregion

    #region Suscripciones

    private void OnProfileReady()
    {
        GameBootService.OnProfileReady -= OnProfileReady;
        Subscribe();
        Refresh();
    }

    private void Subscribe()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestStarted   += OnQuestEvent;
        QuestManager.Instance.OnQuestCompleted += OnQuestEvent;
        QuestManager.Instance.OnStepCompleted  += OnStepEvent;
        QuestManager.Instance.OnQuestsChanged  += Refresh;
    }

    private void Unsubscribe()
    {
        if (QuestManager.Instance == null) return;
        QuestManager.Instance.OnQuestStarted   -= OnQuestEvent;
        QuestManager.Instance.OnQuestCompleted -= OnQuestEvent;
        QuestManager.Instance.OnStepCompleted  -= OnStepEvent;
        QuestManager.Instance.OnQuestsChanged  -= Refresh;
    }

    private void OnQuestEvent(string id)
    {
        if (id == questId) Refresh();
    }

    private void OnStepEvent(string id, int _)
    {
        if (id == questId) Refresh();
    }

    #endregion

    #region Lógica de visibilidad

    private void Refresh()
    {
        if (_marker == null) return;
        _marker.SetVisible(EvalVisibility());
    }

    private bool EvalVisibility()
    {
        if (string.IsNullOrEmpty(questId) || QuestManager.Instance == null)
            return false;

        if (QuestManager.Instance.GetState(questId) != QuestState.Active)
            return false;

        // El paso prerrequisito debe estar completado antes de mostrar este marcador
        if (showAfterStep >= 0 && !QuestManager.Instance.IsStepCompleted(questId, showAfterStep))
            return false;

        // stepIndex < 0: visible durante toda la quest activa (cumplidos los prerrequisitos)
        if (stepIndex < 0)
            return true;

        // Visible mientras el paso propio no esté completado
        return !QuestManager.Instance.IsStepCompleted(questId, stepIndex);
    }

    #endregion

    [ContextMenu("Forzar refresco")]
    private void ForceRefresh() => Refresh();
}
