// Assets/Scripts/UI/MainMenuController.cs
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using DG.Tweening;

[DisallowMultipleComponent]
public class MainMenuController : MonoBehaviour
{
    [Header("Refs")]
    private SaveSystem saveSystem;

    [Tooltip("Fila / contenedor de la opción CONTINUAR (con tu Text dentro). Si no hay partida, se oculta.")]
    [SerializeField] private GameObject continueRow;

    [Tooltip("Button de la fila CONTINUAR.")]
    [SerializeField] private Button continueButton;

    [Tooltip("Button de la fila NUEVA PARTIDA.")]
    [SerializeField] private Button newGameButton;

    [Header("World Scene")]
    [SerializeField] private string nextScene = "MainWorld";

    [Header("Loading Screen")]
    [SerializeField] private string loadingOverlayScene = "LoadingScreen";

    [Header("Fade override (opcional)")]
    [SerializeField] private EasyTransition.TransitionSettings fadeOverride;
    [Min(0f)] public float fadeDelay = 0f;

    [Header("UI / Intro (opcional)")]
    [SerializeField] private CanvasGroup rootGroup;          // si está vacío, se añade en Awake
    [SerializeField] private RectTransform[] animatedItems;  // hijos que caen en intro
    [Min(0f)] public float introDelay = 0.05f;
    [Min(0f)] public float introStagger = 0.04f;
    [Min(0f)] public float introDuration = 0.35f;
    public float introYOffset = 40f;

    private Sequence _introSeq;
    private bool _isLoading = false;

    void Awake()
    {
        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>();

        if (!rootGroup)
            rootGroup = gameObject.GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();

        // Autowire de botones si faltan referencias
        if (!continueButton && continueRow)
            continueButton = continueRow.GetComponent<Button>();

        if (!newGameButton)
            newGameButton = TryFindNewGameButton();

        WireButton(continueButton, OnClickContinue, "CONTINUE");
        WireButton(newGameButton, OnClickNewGame, "NEW GAME");
    }

    void OnEnable()
    {
        _isLoading = false; // reset por si vuelves al menú
        UpdateContinueVisibility();
        PlayIntro();
        AutoSelectFirstIfNeeded();
        SelfTestButtons();
    }

    void Start()
    {
        UpdateContinueVisibility();
        AutoSelectFirstIfNeeded();
    }

    void OnDisable()
    {
        _introSeq?.Kill(); _introSeq = null;
    }

    // ===== Visibilidad / Selección =========================================
    void UpdateContinueVisibility()
    {
        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        bool hasSave = saveSystem && saveSystem.HasSave();

        if (continueRow) continueRow.SetActive(hasSave);
        if (continueButton) continueButton.interactable = hasSave;
    }

    void AutoSelectFirstIfNeeded()
    {
        var es = EventSystem.current;
        if (!es) return;

        // Solo forzar selección si NO hay selección válida ya
        if (es.currentSelectedGameObject == null || !es.currentSelectedGameObject.activeInHierarchy)
        {
            // Si hay partida y existe CONTINUAR, lo seleccionamos; si no, buscamos el primer Button activo
            if (continueRow && continueRow.activeInHierarchy && continueButton && continueButton.interactable)
            {
                es.SetSelectedGameObject(continueButton.gameObject);
            }
            else
            {
                var firstBtn = FindFirstActiveButton();
                if (firstBtn) es.SetSelectedGameObject(firstBtn.gameObject);
            }
        }
    }

    // ===== Intro (CanvasGroup en cada item) =================================
    void PlayIntro()
    {
        if (!rootGroup) return;

        rootGroup.alpha = 0f;
        _introSeq?.Kill();
        _introSeq = DOTween.Sequence().AppendInterval(introDelay);

        if (animatedItems != null && animatedItems.Length > 0)
        {
            foreach (var item in animatedItems)
            {
                if (!item) continue;

                var cg = item.GetComponent<CanvasGroup>();
                if (!cg) cg = item.gameObject.AddComponent<CanvasGroup>();

                var startPos = item.anchoredPosition;
                item.anchoredPosition = startPos + new Vector2(0f, -introYOffset);
                cg.alpha = 0f;

                _introSeq.AppendCallback(() =>
                {
                    item.DOAnchorPos(startPos, introDuration).SetEase(Ease.OutCubic);
                    cg.DOFade(1f, introDuration);
                });

                _introSeq.AppendInterval(introStagger);
            }
        }

        _introSeq.Insert(0f, rootGroup.DOFade(1f, introDuration));
    }

    // ===== Acciones de menú =================================================
    public void OnClickContinue()
    {
        if (_isLoading) return;
        _isLoading = true;
        Debug.Log("[MainMenu] CONTINUE click");

        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        if (saveSystem != null && saveSystem.HasSave())
        {
            if (GameBootService.IsAvailable && GameBootService.Profile != null)
            {
                bool loaded = GameBootService.Profile.LoadProfile(saveSystem);
                if (!loaded)
                    Debug.LogWarning("[MainMenu] No se pudo cargar el save antes de ir a la escena del mundo.");
            }
            else
            {
                Debug.LogWarning("[MainMenu] GameBootService no esta listo; se continuara con la sincronizacion en el mundo.");
            }
        }
        else
        {
            Debug.LogWarning("[MainMenu] CONTINUE pulsado sin save disponible.");
        }

        LoadNextScene();
    }

    public void OnClickNewGame()
    {
        if (_isLoading) return;
        _isLoading = true;

        Debug.Log("[MainMenu] NEW GAME click -> reiniciando perfil y cargando escena");

        if (!saveSystem)
            saveSystem = ServiceLocator.Get<SaveSystem>(logIfMissing: false);

        if (GameBootService.IsAvailable)
        {
            GameBootService.NewGameReset();
        }
        else
        {
            Debug.LogWarning("[MainMenu] GameBootService no esta listo; se borra el save directamente.");
            if (saveSystem != null)
            {
                bool ok = saveSystem.Delete();
                if (!ok) Debug.LogWarning("[MainMenu] SaveSystem.Delete() devolvio false (algun fichero no se pudo borrar).");
            }
        }

        LoadNextScene();
    }

    public void OnClickExit()
    {
        Application.Quit();
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#endif
    }

    void LoadNextScene()
    {
        bool useOverlay = !string.IsNullOrEmpty(loadingOverlayScene);
        if (useOverlay)
        {
            if (fadeOverride != null)
                SceneTransitionLoader.LoadWithOverlay(nextScene, loadingOverlayScene, fadeOverride, fadeDelay);
            else
                SceneTransitionLoader.LoadWithOverlay(nextScene, loadingOverlayScene);
        }
        else
        {
            if (fadeOverride != null)
                SceneTransitionLoader.Load(nextScene, fadeOverride, fadeDelay);
            else
                SceneTransitionLoader.Load(nextScene);
        }
    }

    // ===== Utilidades =======================================================
    void WireButton(Button btn, UnityEngine.Events.UnityAction action, string label)
    {
        if (!btn)
        {
            Debug.LogWarning($"[MainMenu] Botón {label} no asignado/encontrado.");
            return;
        }

        btn.onClick.RemoveListener(action);
        btn.onClick.AddListener(action);

        if (!btn.interactable)
            Debug.LogWarning($"[MainMenu] Botón {label} no está interactable.");

        if (!btn.gameObject.activeInHierarchy)
            Debug.LogWarning($"[MainMenu] Botón {label} está inactivo en jerarquía.");
    }

    Button TryFindNewGameButton()
    {
        // Busca en hijos un botón cuyo nombre sugiera "New Game"
        var all = GetComponentsInChildren<Button>(true);
        foreach (var b in all)
        {
            var n = b.gameObject.name.ToLowerInvariant();
            if (n.Contains("new") || n.Contains("nueva") || n.Contains("newgame") || n.Contains("nueva_partida"))
                return b;
        }
        return null;
    }

    Button FindFirstActiveButton()
    {
        var all = GetComponentsInChildren<Button>(true);
        foreach (var b in all)
        {
            if (b.gameObject.activeInHierarchy && b.interactable)
                return b;
        }
        return null;
    }

    void SelfTestButtons()
    {
        // Pequeño chequeo de ambiente para raycasts/interactables.
        if (newGameButton)
        {
            bool active = newGameButton.gameObject.activeInHierarchy;
            bool interact = newGameButton.interactable;
            bool raycastOK = true;

            foreach (var cg in newGameButton.GetComponentsInParent<CanvasGroup>(true))
            {
                if (!cg.blocksRaycasts) { raycastOK = false; break; }
            }

            Debug.Log($"[MainMenu][SelfTest] NewGame active={active}, interactable={interact}, blocksRaycasts={raycastOK}");
        }
    }
}
