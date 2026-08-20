// Assets/Scripts/UI/LoadingScreenController.cs
using System.Collections;
using UnityEngine;
using TMPro;

public interface ILoadingUI
{
    void ShowImmediate();
    void HideImmediate();
    void SetProgress(float t);
    float FadeDuration { get; }
    float MinVisibleTime { get; }
    float GestureHoldTime { get; }
}

/// <summary>
/// Pantalla de carga rediseñada: fondo en blanco y Will, Estela y Liam corriendo en el sitio,
/// mirando a la derecha, en el "escenario" 3D de Assets/Scenes/Systems/LoadingScreen.unity
/// (ver LoadingCharacterStage / LoadingShowcaseCharacter). Al llegar al 100% de progreso, los
/// tres se giran hacia cámara y hacen su gesto gracioso.
/// </summary>
public class LoadingScreenController : MonoBehaviour, ILoadingUI
{
    [Header("UI References")]
    public CanvasGroup panel;
    public TMP_Text progressText;

    [Header("Personajes (escenario 3D)")]
    [Tooltip("Orquesta a Will, Estela y Liam en el escenario de la pantalla de carga. Vive en la propia " +
             "escena LoadingScreen.unity (objeto CharacterStage), no en este prefab.")]
    public LoadingCharacterStage characterStage;

    [Header("Timing")]
    public float fadeDuration = 0.25f;
    public float minVisibleTime = 1f;
    [Tooltip("Tiempo mínimo que se mantiene visible el gesto final (personajes ya girados hacia cámara) " +
             "antes de que SceneTransitionLoader pueda tapar la pantalla en negro. Igual que minVisibleTime, " +
             "protege el momento del gesto aunque la escena destino cargue muy rápido.")]
    public float gestureHoldTime = 0.9f;

    public float FadeDuration => fadeDuration;
    public float MinVisibleTime => minVisibleTime;
    public float GestureHoldTime => gestureHoldTime;

    float _lastProgress;

    void Awake()
    {
        // Autodescubrimiento de respaldo: el escenario de personajes vive suelto en la propia
        // escena LoadingScreen.unity (objeto "CharacterStage"), no dentro de este prefab, así que
        // normalmente no se puede arrastrar la referencia a mano en el prefab. Si no se ha
        // asignado ya (p.ej. en el propio objeto de escena), se busca una vez aquí — no es un
        // FindObjectOfType en Update/LateUpdate, así que no choca con la regla de AGENTS.md.
        if (!characterStage)
            characterStage = FindAnyObjectByType<LoadingCharacterStage>();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // DIAGNÓSTICO TEMPORAL (20 ago 2026): ver comentario en LoadingCharacterStage.Awake().
        Debug.Log(characterStage
            ? $"[LoadingScreen] characterStage resuelto: '{characterStage.name}' (entityId {characterStage.GetEntityId()}) en escena '{characterStage.gameObject.scene.name}'"
            : "[LoadingScreen] characterStage NO encontrado (ni asignado ni por FindAnyObjectByType).");
#endif

        if (panel != null)
        {
            panel.alpha = 0f;
            panel.gameObject.SetActive(false);
        }
    }

    public void ShowImmediate()
    {
        if (!panel) { Debug.LogWarning("[LoadingScreen] Panel not assigned."); return; }
        panel.gameObject.SetActive(true);
        panel.alpha = 1f;
        _lastProgress = 0f;
        characterStage?.ResetToRunning();
        SetProgress(0f);
    }

    public void HideImmediate()
    {
        if (!panel) return;
        panel.alpha = 0f;
        panel.gameObject.SetActive(false);
    }

    public void SetProgress(float t)
    {
        t = Mathf.Clamp01(t);
        if (progressText) progressText.text = Mathf.RoundToInt(t * 100f) + "%";

        // Flanco de subida a 100%: los personajes se giran hacia cámara y hacen su gesto.
        if (t >= 1f && _lastProgress < 1f)
            characterStage?.PlayReveal();

        _lastProgress = t;
    }

    public IEnumerator Fade(float from, float to)
    {
        if (!panel) yield break;
        float elapsed = 0f;
        panel.alpha = from;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            panel.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / fadeDuration));
            yield return null;
        }
        panel.alpha = to;
    }

    // ====================== Botones de prueba (solo Editor) ======================
    // Para probar sin disparar una carga de escena real: entra en Play sobre
    // LoadingScreen.unity, selecciona el objeto "LoadingOverlay" en la Hierarchy, y en el
    // Inspector haz clic en los tres puntos (⋮) / botón derecho sobre la cabecera del
    // componente "Loading Screen Controller" para ver estas opciones en el menú contextual.
#if UNITY_EDITOR
    [ContextMenu("TEST: 1) Mostrar (corriendo)")]
    void Test_Show() => ShowImmediate();

    [ContextMenu("TEST: 2) Reveal (giro + gesto)")]
    void Test_Reveal() => characterStage?.PlayReveal();

    [ContextMenu("TEST: 3) Ocultar")]
    void Test_Hide() => HideImmediate();
#endif
}
