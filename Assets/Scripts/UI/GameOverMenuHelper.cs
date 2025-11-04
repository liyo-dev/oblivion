using UnityEngine;
using UnityEngine.UI;
using EasyTransition;

// Coloca este componente en el GameObject raíz del panel de Game Over (el que se activa/desactiva).
// - Aplica un pequeño debounce al abrir para evitar que el primer Submit confirme "Continuar".
// - Configura la transición por defecto de SceneTransitionLoader (overlay/fade) vía Inspector.
[DisallowMultipleComponent]
public class GameOverMenuHelper : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private CanvasGroup rootGroup; // CanvasGroup del panel (para blocksRaycasts)
    [SerializeField] private Button continueButton; // botón que reanuda/continúa
    [SerializeField] private Button backToMenuButton; // botón volver al menú principal

    [Header("Input Debounce")]
    public bool enableInputDebounce = false;
    [Min(0f)] public float inputArmDelay = 0.2f;

    [Header("Transition/Overlay Defaults")]
    [Tooltip("Nombre de la escena de overlay para cargas (opcional). Si se define, cualquier SceneTransitionLoader.Load() usará overlay.")]
    [SerializeField] private string loadingOverlayScene = ""; // asigna por inspector si quieres overlay
    [SerializeField] private TransitionSettings fadeOverride; // usado cuando no hay overlay
    [Min(0f)] public float fadeDelay = 0f;

    void Awake()
    {
        // Configurar defaults globales del SceneTransitionLoader.
        SceneTransitionLoader.DefaultOverlayScene = string.IsNullOrEmpty(loadingOverlayScene) ? null : loadingOverlayScene;
        SceneTransitionLoader.DefaultFade = fadeOverride;
        SceneTransitionLoader.DefaultFadeDelay = fadeDelay;
    }

    void OnEnable()
    {
        // Cuando el panel se active, aplicar debounce de interacción.
        if (enableInputDebounce)
            StartCoroutine(ArmAfterDelay());
    }

    void OnDisable()
    {
        // Restaurar por seguridad si el objeto se desactiva durante el debounce
        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = true;
            rootGroup.interactable = true;
        }
        if (continueButton) continueButton.interactable = true;
        if (backToMenuButton) backToMenuButton.interactable = true;
    }

    System.Collections.IEnumerator ArmAfterDelay()
    {
        bool hadGroup = rootGroup != null;
        if (!hadGroup)
            rootGroup = GetComponent<CanvasGroup>();

        bool prevCR = true;
        if (rootGroup != null)
        {
            prevCR = rootGroup.blocksRaycasts;
            rootGroup.blocksRaycasts = false;
            rootGroup.interactable = false;
        }

        bool prevCont = continueButton ? continueButton.interactable : true;
        bool prevBack = backToMenuButton ? backToMenuButton.interactable : true;
        if (continueButton) continueButton.interactable = false;
        if (backToMenuButton) backToMenuButton.interactable = false;

        float t = 0f;
        while (t < inputArmDelay)
        {
            t += Time.unscaledDeltaTime;
            yield return null;
        }

        if (rootGroup != null)
        {
            rootGroup.blocksRaycasts = prevCR;
            rootGroup.interactable = true;
        }
        if (continueButton) continueButton.interactable = prevCont;
        if (backToMenuButton) backToMenuButton.interactable = prevBack;

        // Reasignar comportamiento del botón Continuar para que cargue el último guardado
        if (continueButton != null && GameOverManager.Instance != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(GameOverManager.Instance.OnLoadLastSave);
        }
    }
}
