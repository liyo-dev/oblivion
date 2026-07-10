using Core;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Trigger colocado en un puesto del mercado. El jugador entra en la zona,
/// pulsa el botón indicado y recoge el ítem (si ningún NPC está demasiado cerca).
/// Se registra automáticamente en ForagingMinigameController al activarse.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ForagingStallTrigger : MonoBehaviour
{
    [Header("Identificación")]
    [Tooltip("ID único de este puesto dentro del minijuego.")]
    [SerializeField] private string stallId;
    [Tooltip("ID del minijuego al que pertenece este puesto.")]
    [SerializeField] private string minigameId = "FORAGE_MINIGAME_01";

    [Header("Detección de NPCs")]
    [Tooltip("Radio en el que un NPC bloquea la recogida.")]
    [SerializeField] private float npcDetectionRadius = 3f;
    [Tooltip("LayerMask de los NPCs. Si no está asignado, usa la capa 'NPC'.")]
    [SerializeField] private LayerMask npcLayerMask;
    [Tooltip("Mensaje de localización cuando hay un NPC cerca. Vacío = sin mensaje.")]
    [SerializeField] private string npcNearbyMessageKey = "FORAGE_NPC_NEARBY";

    [Header("UI — Prompt")]
    [Tooltip("Canvas world-space con el prompt de interacción.")]
    [SerializeField] private GameObject promptRoot;
    [SerializeField] private Image promptButtonIcon;
    [Tooltip("Sprite del botón A del gamepad.")]
    [SerializeField] private Sprite buttonASprite;

    [Header("Feedback de recogida")]
    [Tooltip("Objeto que se activa al recoger (checkmark, VFX, etc.).")]
    [SerializeField] private GameObject collectedFeedback;
    [Tooltip("Tiempo en segundos antes de desactivar el trigger tras la recogida.")]
    [SerializeField] private float collectedDisableDelay = 0.5f;

    // Referencia al controlador (resuelta en Awake o bajo demanda)
    private ForagingMinigameController _controller;
    private bool _playerInside = false;
    private bool _collected = false;
    private bool _interactable = false;

    // Buffer para OverlapSphereNonAlloc (regla CLAUDE.md)
    private readonly Collider[] _npcBuffer = new Collider[16];

    public string StallId => stallId;

    void Awake()
    {
        var col = GetComponent<Collider>();
        if (!col.isTrigger) col.isTrigger = true;

        if (npcLayerMask == 0)
            npcLayerMask = LayerMask.GetMask("NPC");

        if (string.IsNullOrEmpty(stallId))
            stallId = name;
    }

    void OnEnable()
    {
        ResolveController();
        _controller?.RegisterStall(this);
    }

    void OnDisable()
    {
        _controller?.UnregisterStall(this);
        HidePrompt();
    }

    void Update()
    {
        if (!_interactable || !_playerInside || _collected) return;
        if (!_controller.IsPlaying) return;

        if (GamepadInputReader.JumpPressed)
            TryCollect();
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = true;
        if (_interactable && !_collected)
            ShowPrompt();
    }

    void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        _playerInside = false;
        HidePrompt();
    }

    // -------------------------------------------------------------------------
    // Interacción
    // -------------------------------------------------------------------------

    private void TryCollect()
    {
        if (IsNPCNearby())
        {
            _controller.OnPlayerDetectedByNPC();
            if (!string.IsNullOrEmpty(npcNearbyMessageKey))
                HudToastService.Instance?.Show(npcNearbyMessageKey);
            return;
        }

        HidePrompt();
        _controller.OnStallCollected(this);
    }

    private bool IsNPCNearby()
    {
        int count = Physics.OverlapSphereNonAlloc(transform.position, npcDetectionRadius, _npcBuffer, npcLayerMask);
        return count > 0;
    }

    // -------------------------------------------------------------------------
    // API pública
    // -------------------------------------------------------------------------

    /// <summary>Marca este puesto como recogido y muestra feedback visual.</summary>
    public void MarkCollected()
    {
        _collected = true;
        HidePrompt();

        if (collectedFeedback != null && !collectedFeedback.activeSelf)
            collectedFeedback.SetActive(true);

        if (collectedDisableDelay > 0f)
            Invoke(nameof(DisableSelf), collectedDisableDelay);
        else
            DisableSelf();
    }

    /// <summary>Activa o desactiva la posibilidad de interactuar con este puesto.</summary>
    public void SetInteractable(bool interactable)
    {
        _interactable = interactable;

        if (!interactable || !_playerInside || _collected)
            HidePrompt();
        else if (_playerInside)
            ShowPrompt();
    }

    // -------------------------------------------------------------------------
    // UI
    // -------------------------------------------------------------------------

    private void ShowPrompt()
    {
        if (promptRoot == null) return;
        if (!promptRoot.activeSelf) promptRoot.SetActive(true);
        if (promptButtonIcon != null && buttonASprite != null)
            promptButtonIcon.sprite = buttonASprite;
    }

    private void HidePrompt()
    {
        if (promptRoot != null && promptRoot.activeSelf)
            promptRoot.SetActive(false);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private void DisableSelf()
    {
        gameObject.SetActive(false);
    }

    private void ResolveController()
    {
        if (_controller != null) return;

        var all = FindObjectsByType<ForagingMinigameController>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var c in all)
        {
            if (c.MinigameId == minigameId)
            {
                _controller = c;
                return;
            }
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.LogWarning($"[ForagingStallTrigger:{name}] No se encontró ForagingMinigameController con id='{minigameId}'");
#endif
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.3f, 0.3f, 0.3f);
        Gizmos.DrawSphere(transform.position, npcDetectionRadius);
    }
#endif
}
