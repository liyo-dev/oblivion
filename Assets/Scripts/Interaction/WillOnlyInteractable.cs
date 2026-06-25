using Game.NPC.Common;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Marca un NPC como accesible únicamente cuando Will es el personaje activo.
///
/// Si el jugador intenta interactuar con otro personaje activo:
///   - Se muestra un mensaje HUD no bloqueante ("Solo Will puede hablar con este personaje").
///   - Se reproduce un leve shake de cámara.
///   - La interacción no avanza.
///
/// Configuración en el Inspector:
///   • Will Indicator Icon Prefab — icono que flota sobre el NPC indicando que requiere Will
///     (opcional: si no se asigna, no se muestra icono persistente).
///   • Show Persistent Icon — activa/desactiva el icono flotante.
/// </summary>
[RequireComponent(typeof(Interactable))]
public class WillOnlyInteractable : MonoBehaviour
{
    private const string FeedbackLocKey = "NPC_REQUIRES_WILL";

    [Header("Indicador visual")]
    [Tooltip("Prefab del icono que aparece flotando sobre el NPC. Si está vacío, no se muestra icono.")]
    [SerializeField] private GameObject willIndicatorIconPrefab;

    [Tooltip("Si true, muestra el icono de Will mientras el jugador se acerca.")]
    [SerializeField] private bool showPersistentIcon = true;

    private NPCAlertIconController _iconController;

    // ────────────────────────────────────────────────────────────────────────
    #region Lifecycle

    private void Start()
    {
        if (showPersistentIcon && willIndicatorIconPrefab != null)
            StartCoroutine(SetupPersistentIconDeferred());
    }

    private void Update()
    {
        if (!showPersistentIcon || willIndicatorIconPrefab == null) return;
        if (_iconController != null && !_iconController.HasPersistentIcon)
            _iconController.ShowPersistentIcon(willIndicatorIconPrefab);
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region API

    /// <summary>True si Will es el personaje activo en este momento.</summary>
    public bool IsWillActive()
        => PartyControlManager.Instance?.ActiveSlot == PartyControlManager.CharacterSlot.Will;

    /// <summary>
    /// Muestra el feedback al jugador cuando intenta interactuar con el personaje equivocado.
    /// Llamado desde Interactable.Interact().
    /// </summary>
    public void ShowFeedback()
    {
        HudToastService.Instance?.Show(FeedbackLocKey);
        FeedbackService.CameraShake(0.15f, 0.2f);
    }

    #endregion

    // ────────────────────────────────────────────────────────────────────────
    #region Internals

    private System.Collections.IEnumerator SetupPersistentIconDeferred()
    {
        yield return null; // esperar un frame para que todos los Start() hayan corrido
        SetupPersistentIcon();
    }

    private void SetupPersistentIcon()
    {
        if (_iconController == null)
        {
            // Usar un hijo dedicado para no compartir el NPCAlertIconController del raíz
            // con otros sistemas (NPCInteractiveNarrativeExecutor, AlertState, etc.)
            const string childName = "_WillIconController";
            var iconChild = transform.Find(childName);
            if (iconChild == null)
            {
                iconChild = new GameObject(childName).transform;
                iconChild.SetParent(transform, false);
            }
            _iconController = iconChild.GetComponent<NPCAlertIconController>()
                ?? iconChild.gameObject.AddComponent<NPCAlertIconController>();
        }

        _iconController.ShowPersistentIcon(willIndicatorIconPrefab);
    }

    #endregion
}
