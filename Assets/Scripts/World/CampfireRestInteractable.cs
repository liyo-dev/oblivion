using System.Collections;
using Sendero.Core.Feedback;
using UnityEngine;

/// <summary>
/// Añadir a la hoguera junto al componente Interactable (modo OpenDialogueWithOptions).
/// - OnInteract: elige el diálogo de pregunta correcto según sea de día o de noche.
/// - OnConfirm:  fundido a negro → cambia la hora → fundido de vuelta.
///
/// Setup en Inspector:
///   Interactable → Mode: ConfirmationPopup (igual que en los puntos de guardado; el script lo fuerza en Awake)
///   Interactable → Dialogue: (dejar vacío; este script lo asigna dinámicamente)
///   Interactable → OnConfirm → CampfireRestInteractable.DoTimeSkip
///   CampfireRestInteractable → asignar waitForNightDialogue y waitForDayDialogue
/// </summary>
[RequireComponent(typeof(Interactable))]
[DisallowMultipleComponent]
public class CampfireRestInteractable : MonoBehaviour
{
    [Header("Diálogos de pregunta")]
    [Tooltip("DialogueAsset con la pregunta cuando es de día (ej: '¿Descansar y esperar a que anochezca?').")]
    [SerializeField] private DialogueAsset waitForNightDialogue;

    [Tooltip("DialogueAsset con la pregunta cuando es de noche (ej: '¿Descansar y esperar a que amanezca?').")]
    [SerializeField] private DialogueAsset waitForDayDialogue;

    [Header("Tiempo objetivo")]
    [Tooltip("Periodo al que saltar al confirmar cuando es de día.")]
    [SerializeField] private DayNightCycle.TimeOfDay nightTarget = DayNightCycle.TimeOfDay.Night;

    [Tooltip("Periodo al que saltar al confirmar cuando es de noche.")]
    [SerializeField] private DayNightCycle.TimeOfDay dayTarget = DayNightCycle.TimeOfDay.Morning;

    [Header("Fundido")]
    [Tooltip("Segundos que tarda en fundirse a negro.")]
    [SerializeField] private float fadeOutDuration = 0.4f;

    [Tooltip("Segundos que la pantalla permanece en negro (el cambio de hora ocurre aquí).")]
    [SerializeField] private float holdDuration = 0.15f;

    [Tooltip("Segundos que tarda en volver desde el negro.")]
    [SerializeField] private float fadeInDuration = 0.6f;

    private Interactable _interactable;
    private DayNightCycle _dayNightCycle;
    private DayNightCycle.TimeOfDay _pendingTarget;

    void Awake()
    {
        _interactable = GetComponent<Interactable>();
        // Mismo popup de confirmación reutilizable que usan los puntos de guardado (ConfirmationPopupUI),
        // en vez del cuadro de diálogo con opciones (INC-048 aplicado también a la hoguera).
        _interactable.SetMode(InteractableMode.ConfirmationPopup);
        _interactable.OnInteract.AddListener(OnInteractStarted);
        _interactable.OnConfirm.AddListener(DoTimeSkip);

        _dayNightCycle = Object.FindAnyObjectByType<DayNightCycle>();
    }

    void OnDestroy()
    {
        if (_interactable == null) return;
        _interactable.OnInteract.RemoveListener(OnInteractStarted);
        _interactable.OnConfirm.RemoveListener(DoTimeSkip);
    }

    void OnInteractStarted(GameObject interactor)
    {
        if (_dayNightCycle == null)
            _dayNightCycle = Object.FindAnyObjectByType<DayNightCycle>();

        bool isNight = IsNightTime();
        _pendingTarget = isNight ? dayTarget : nightTarget;
        _interactable.SetDialogue(isNight ? waitForDayDialogue : waitForNightDialogue);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        string timeLabel = isNight ? "noche" : "día";
        Debug.Log($"[CampfireRest:{name}] Es {timeLabel} → preguntando por {_pendingTarget}.");
#endif
    }

    // Llamado por Interactable.OnConfirm cuando el jugador elige Sí.
    // Conectar en Inspector: Interactable → OnConfirm → CampfireRestInteractable.DoTimeSkip
    // (también se suscribe por código en Awake; no conectar en Inspector para evitar doble llamada).
    public void DoTimeSkip() => StartCoroutine(DoTimeSkipRoutine());

    private IEnumerator DoTimeSkipRoutine()
    {
        // Fundir a negro
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeOutDuration, fadeIn: true);

        // Cambiar hora de forma instantánea bajo el negro
        if (_dayNightCycle != null)
            _dayNightCycle.SetTimeOfDay(_pendingTarget, immediate: true);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[CampfireRest:{name}] Hora cambiada a {_pendingTarget} bajo el negro.");
#endif

        // Pausa breve en negro
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        // Volver desde el negro
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeInDuration, fadeIn: false);
    }

    bool IsNightTime() =>
        _dayNightCycle != null &&
        (_dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Night ||
         _dayNightCycle.CurrentTimeOfDay == DayNightCycle.TimeOfDay.Midnight);
}
