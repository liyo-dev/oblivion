using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// Implementado por componentes que viven en el mismo GameObject que un SceneBoundUI y que
/// necesitan poder posponer su ocultado automático por cambio de escena mientras muestran
/// contenido importante que el jugador todavía no ha tenido tiempo de ver/leer (ej:
/// AbilityUnlockPopupUI mientras el popup de "Bola Prisma" / "Llama Astral" está en pantalla).
/// Mientras BlocksSceneHide() devuelva true, SceneBoundUI.ApplySceneState() NO llamará a
/// gameObject.SetActive(false) aunque la escena activa ya no esté en allowedScenes — el corte de
/// cámara/carga de escena que dispara ese evento no debe poder tragarse un popup a medio leer.
/// Por el mismo motivo, SceneBoundUI.BeginBossIntro() tampoco opacará este objeto a alpha 0
/// mientras BlocksSceneHide() devuelva true (ver comentario en BeginBossIntro): el desbloqueo de
/// un hechizo puede encadenar síncronamente con el arranque de la intro de un boss en el mismo
/// frame (ej: "Despertar de la Estrella" → StarAwakeningSequencer → UnlockAbilitiesNode →
/// StartBattleNode → BossIntroPresentation), y el popup no debe perderse por esa carrera.
/// </summary>
public interface ISceneBoundUIHideGuard
{
    bool BlocksSceneHide();
}

public class SceneBoundUI : MonoBehaviour
{
    [SerializeField] private string uniqueId = string.Empty;
    [SerializeField] private List<string> allowedScenes = new();
    [SerializeField] private bool allowWhenListEmpty = true;
    [SerializeField] private bool persistAcrossScenes = true;
    [SerializeField] private bool detachFromParent = true;
    [Tooltip("Si true, no se oculta durante la intro de boss (ej: DramaticTextOverlayUI).")]
    [SerializeField] private bool excludeFromBossIntroHide = false;

    private static readonly Dictionary<string, SceneBoundUI> Instances = new();
    private static bool _bossIntroActive = false;
    private string instanceKey;
    private float _preBossAlpha = -1f;
    private ISceneBoundUIHideGuard _hideGuard;
    // True cuando un cambio de escena habría ocultado este objeto pero _hideGuard lo bloqueó.
    // El guard debe llamar a ReapplySceneState() en cuanto deje de bloquear para que el ocultado
    // pendiente (si sigue aplicando) se ejecute entonces, en vez de quedarse nunca aplicado.
    private bool _pendingHide;
    // Igual que _pendingHide pero para BeginBossIntro(): true cuando la intro de un boss quiso
    // opacar este objeto a alpha 0 pero _hideGuard lo bloqueó (popup a medio leer). Ver comentario
    // en BeginBossIntro() sobre por qué esto puede pasar en el MISMO frame que el propio popup
    // empieza a mostrarse.
    private bool _pendingBossIntroHide;

    /// <summary>True mientras una intro de boss tiene la UI persistente oculta (entre BeginBossIntro y EndBossIntro).</summary>
    public static bool IsBossIntroActive => _bossIntroActive;

    /// <summary>
    /// Se dispara justo al terminar EndBossIntro (UI restaurada). Pensado para que sistemas como
    /// AbilityUnlockPopupUI puedan diferir una animación hasta que la intro del boss haya acabado
    /// del todo, en vez de "gastar" su ventana de visibilidad oculta a alpha 0 durante la intro.
    /// </summary>
    public static event System.Action OnBossIntroEnded;

    #if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Instances.Clear();
        _bossIntroActive = false;
        OnBossIntroEnded = null;
    }
    #endif

    // ── API de boss intro ──────────────────────────────────────────────────

    /// <summary>
    /// Oculta con DOTween todos los elementos UI activos con SceneBoundUI.
    /// Llamar al inicio de la intro de un boss para limpiar la pantalla.
    /// </summary>
    public static void BeginBossIntro(float fadeDuration = 0.3f)
    {
        _bossIntroActive = true;
        foreach (var inst in Instances.Values)
        {
            if (inst == null || !inst.gameObject.activeSelf) continue;
            if (inst.excludeFromBossIntroHide) continue;

            // BUG (ago 2026): un popup importante en pantalla (ej: AbilityUnlockPopupUI mostrando
            // "Bola Prisma" recién desbloqueada) podía arrancar su animación de entrada y, en el
            // MISMO frame — porque UnlockAbilitiesNode → StartQuestNode → StartBattleNode →
            // BossIntroPresentation se ejecutan síncronamente detrás del mismo evento narrativo —
            // esta llamada lo apagaba a alpha 0 igualmente. El guard de ApplySceneState() (más
            // abajo) no protegía este camino porque BeginBossIntro nunca lo consultaba: el jugador
            // nunca llegaba a ver el popup pese a que HidePopup/AnimateIn se ejecutaban bien.
            // Mismo criterio que ApplySceneState ya aplica para cambios de escena: si el guard
            // sigue bloqueando, no tocar el CanvasGroup — se deja pendiente y el propio guard debe
            // llamar a ReapplySceneState() cuando termine de mostrar su contenido.
            if (inst._hideGuard != null && inst._hideGuard.BlocksSceneHide())
            {
                inst._pendingBossIntroHide = true;
                continue;
            }

            var cg = inst.GetOrAddCanvasGroup();
            inst._preBossAlpha = cg.alpha;
            cg.DOKill();
            cg.DOFade(0f, fadeDuration).SetUpdate(true);
        }
    }

    /// <summary>
    /// Restaura con DOTween los elementos UI que corresponden a la escena actual.
    /// Llamar al finalizar la intro del boss.
    /// </summary>
    public static void EndBossIntro(float fadeDuration = 0.35f)
    {
        _bossIntroActive = false;
        foreach (var inst in Instances.Values)
        {
            if (inst == null) continue;
            // Si seguía pendiente de opacar (el guard bloqueó durante toda la intro, ver
            // BeginBossIntro), su CanvasGroup nunca se tocó — queda en su alpha normal, que es
            // justo el estado en el que debe quedar ahora que la intro termina. Solo hace falta
            // limpiar el flag.
            inst._pendingBossIntroHide = false;
            bool allowed = inst.IsAllowedInCurrentScene();
            if (!allowed) { inst.gameObject.SetActive(false); continue; }
            inst.gameObject.SetActive(true);
            var cg = inst.GetOrAddCanvasGroup();
            cg.DOKill();
            float targetAlpha = inst._preBossAlpha >= 0f ? inst._preBossAlpha : 1f;
            cg.DOFade(targetAlpha, fadeDuration).SetUpdate(true);
            inst._preBossAlpha = -1f;
        }

        OnBossIntroEnded?.Invoke();
    }

    /// <summary>Marca este SceneBoundUI para no ser ocultado durante la intro de boss.</summary>
    public void ExcludeFromBossIntro() => excludeFromBossIntroHide = true;

    private CanvasGroup GetOrAddCanvasGroup()
    {
        var cg = GetComponent<CanvasGroup>();
        if (cg == null) cg = gameObject.AddComponent<CanvasGroup>();
        return cg;
    }

    private bool IsAllowedInCurrentScene()
    {
        if (allowedScenes.Count == 0) return allowWhenListEmpty;
        for (int i = 0; i < SceneManager.sceneCount; i++)
            if (allowedScenes.Contains(SceneManager.GetSceneAt(i).name))
                return true;
        return false;
    }

    private void Awake()
    {
        instanceKey = string.IsNullOrEmpty(uniqueId) ? name : uniqueId;
        if (Instances.TryGetValue(instanceKey, out var existing) && existing != null && existing != this)
        {
            Destroy(gameObject);
            return;
        }

        Instances[instanceKey] = this;
        _hideGuard = GetComponent<ISceneBoundUIHideGuard>();

        if (detachFromParent && transform.parent != null)
            transform.SetParent(null, worldPositionStays: false);

        if (persistAcrossScenes)
            DontDestroyOnLoad(gameObject);

        SceneManager.activeSceneChanged += OnActiveSceneChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
        ApplySceneState();
    }

    private void OnDestroy()
    {
        if (Instances.TryGetValue(instanceKey, out var existing) && existing == this)
            Instances.Remove(instanceKey);

        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
    }

    private void OnActiveSceneChanged(Scene _, Scene newScene) => ApplySceneState();
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySceneState();
    private void OnSceneUnloaded(Scene scene) => ApplySceneState();

    private void ApplySceneState()
    {
        // Durante la intro de un boss, el estado lo controla BeginBossIntro/EndBossIntro
        if (_bossIntroActive) return;

        bool allowed = IsAllowedInCurrentScene();

        // No cortar en seco contenido importante que el jugador todavía está viendo (ej: el
        // popup de desbloqueo de habilidad). El corte de cámara / carga de escena que dispara
        // este evento no es motivo para tragarse un popup a medio leer: se deja el GameObject
        // activo y se marca el ocultado como pendiente — el propio guard debe llamar a
        // ReapplySceneState() en cuanto termine de mostrar su contenido.
        if (!allowed && gameObject.activeSelf && _hideGuard != null && _hideGuard.BlocksSceneHide())
        {
            _pendingHide = true;
            return;
        }

        _pendingHide = false;
        if (gameObject.activeSelf != allowed)
            gameObject.SetActive(allowed);
    }

    /// <summary>
    /// Reintenta aplicar el estado de escena si había un ocultado pendiente (ver ApplySceneState),
    /// y aplica el fundido a 0 de una intro de boss si BeginBossIntro() lo dejó pendiente (ver
    /// BeginBossIntro). Debe llamarla el propio ISceneBoundUIHideGuard en cuanto BlocksSceneHide()
    /// deje de devolver true, para que ninguno de los dos ocultados pospuestos se quede sin aplicar
    /// nunca (ej: AbilityUnlockPopupUI.HidePopup() al terminar de mostrar el popup).
    /// </summary>
    public void ReapplySceneState()
    {
        if (_pendingHide) ApplySceneState();

        if (_pendingBossIntroHide)
        {
            _pendingBossIntroHide = false;
            // Solo aplicar el fundido a 0 si la intro del boss sigue en marcha — si ya terminó,
            // EndBossIntro() ya limpió el flag y dejó el CanvasGroup en su estado normal.
            if (_bossIntroActive)
            {
                var cg = GetOrAddCanvasGroup();
                _preBossAlpha = cg.alpha;
                cg.DOKill();
                cg.DOFade(0f, 0.25f).SetUpdate(true);
            }
        }
    }
}
