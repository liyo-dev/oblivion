// Assets/Scripts/UI/LoadingShowcaseCharacter.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Personaje decorativo del escenario de la pantalla de carga: corre en el sitio (mirando
/// hacia la derecha de cámara) mientras dura la carga, y al terminar se gira hacia cámara y
/// reproduce un gesto gracioso propio (Animator Controller ligero "LoadingShowcase.controller",
/// con estados Run / Gesture — nada que ver con el controller de gameplay del personaje real).
///
/// IMPORTANTE — por qué esto NO es una instancia del prefab de gameplay a pelo:
/// _WILL.prefab / _ESTELA.prefab / _LIAM.prefab arrastran ~30-40 scripts de gameplay
/// (PlayerPresetService, controlador Invector, FSM, inventario, ServiceLocator...). Instanciar
/// esos prefabs tal cual —aunque sea un momento, en una escena decorativa— dispara su Awake()
/// real y puede pisar registros del ServiceLocator del jugador de verdad. Por eso este objeto
/// vive sobre una copia recortada del héroe (PrefabInstance con m_RemovedComponents quitando
/// todos esos scripts, dejando solo malla + Animator) — mismo patrón ya usado en
/// Assets/Scenes/Systems/MainMenu.unity para los "héroes voladores" del menú principal.
/// </summary>
[DisallowMultipleComponent]
public class LoadingShowcaseCharacter : MonoBehaviour
{
    [Tooltip("0 = gesto de Will (Dance), 1 = gesto de Estela (Victory), 2 = gesto de Liam (Greeting) " +
             "— debe coincidir con el parámetro GestureIndex de LoadingShowcase.controller.")]
    public int gestureIndex;

    // La orientación "de fábrica" del rig no es la misma para los 3 héroes (ver MainMenuFlyingCompanion,
    // mismo problema ya documentado allí). runYaw/revealYaw son la mejor estimación a partir de cómo
    // quedaban Will/Estela/Liam en MainMenu.unity, pero conviene comprobarlos a ojo la primera vez que se
    // abra esta escena en el editor y corregirlos aquí si el personaje mira para el lado que no toca.
    [Header("Orientación (AJUSTAR A OJO EN EDITOR)")]
    [Tooltip("Rotación Y (grados, mundo) mientras corre: debe dejarlo mirando hacia la derecha de cámara.")]
    public float runYaw = 90f;
    [Tooltip("Rotación Y (grados, mundo) al girarse hacia cámara para el gesto final.")]
    public float revealYaw = 180f;
    [Tooltip("Duración del giro hacia cámara al terminar la carga.")]
    public float turnDuration = 0.25f;

    [Tooltip("Layer a la que se pasa este personaje (y todos sus hijos) para que solo lo vea la StageCamera " +
             "del escenario de carga, no ninguna otra cámara de la escena.")]
    public string renderLayerName = "UI_Portrait";

    Animator _animator;
    Coroutine _turnRoutine;

    static readonly int HashReveal = Animator.StringToHash("Reveal");
    static readonly int HashGestureIndex = Animator.StringToHash("GestureIndex");

    void Awake()
    {
        ApplyRenderLayerRecursive();

        _animator = GetComponentInChildren<Animator>(true);
        if (!_animator)
        {
            Debug.LogWarning($"[LoadingShowcaseCharacter] {name}: no se encontró Animator en la jerarquía.");
            return;
        }
        _animator.applyRootMotion = false;
        _animator.SetInteger(HashGestureIndex, gestureIndex);

        transform.localRotation = Quaternion.Euler(0f, runYaw, 0f);
    }

    void ApplyRenderLayerRecursive()
    {
        int layer = LayerMask.NameToLayer(renderLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[LoadingShowcaseCharacter] {name}: la layer '{renderLayerName}' no existe en el proyecto.");
            return;
        }
        SetLayerRecursive(transform, layer);
    }

    static void SetLayerRecursive(Transform root, int layer)
    {
        root.gameObject.layer = layer;
        int childCount = root.childCount;
        for (int i = 0; i < childCount; i++)
            SetLayerRecursive(root.GetChild(i), layer);
    }

    /// <summary>Vuelve a correr en el sitio mirando a la derecha. Llamado al mostrar la pantalla de carga.</summary>
    public void ResetToRunning()
    {
        if (_turnRoutine != null) { StopCoroutine(_turnRoutine); _turnRoutine = null; }
        transform.localRotation = Quaternion.Euler(0f, runYaw, 0f);
        if (_animator)
        {
            _animator.Rebind();
            _animator.Update(0f);
            _animator.SetInteger(HashGestureIndex, gestureIndex);
        }
    }

    /// <summary>Se gira hacia cámara y dispara su gesto gracioso.</summary>
    public void PlayReveal()
    {
        if (_animator) _animator.SetTrigger(HashReveal);
        if (_turnRoutine != null) StopCoroutine(_turnRoutine);
        _turnRoutine = StartCoroutine(TurnToCamera());
    }

    IEnumerator TurnToCamera()
    {
        Quaternion from = transform.localRotation;
        Quaternion to = Quaternion.Euler(0f, revealYaw, 0f);
        float t = 0f;
        while (t < turnDuration)
        {
            t += Time.unscaledDeltaTime;
            transform.localRotation = Quaternion.Slerp(from, to, Mathf.Clamp01(t / turnDuration));
            yield return null;
        }
        transform.localRotation = to;
        _turnRoutine = null;
    }
}
