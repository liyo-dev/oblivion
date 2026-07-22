using System.Collections;
using UnityEngine;

/// <summary>
/// Marca un punto del mundo como referencia de foco para la cámara.
/// Otros sistemas (cinemáticas, cutscenes) buscan este componente por nombre
/// para saber dónde apuntar la cámara.
/// </summary>
public class CameraFocusPoint : MonoBehaviour
{
    [Tooltip("ID opcional para que los sistemas de cámara localicen este punto por nombre.")]
    [SerializeField] public string focusId;

    /// <summary>
    /// Posición del punto de foco en el mundo.
    /// </summary>
    public Vector3 WorldPosition => transform.position;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [Header("Test en Play Mode")]
    [Tooltip("Segundos que la cámara permanece cortada a este punto al usar 'Probar este plano' (menú contextual del componente).")]
    public float testHoldSeconds = 3f;

    /// <summary>
    /// Corta Camera.main a este punto durante testHoldSeconds y la devuelve a su estado anterior.
    /// Permite iterar el encuadre (posición/rotación de este transform) sin tener que disparar
    /// el evento narrativo real ni recorrer todo el nodo de cinemática que lo usa.
    /// Ajusta el transform en el Inspector mientras estás en Play Mode y vuelve a probar;
    /// copia los valores finales (Copy Component / anota posición y rotación) antes de salir
    /// de Play Mode para no perder el ajuste.
    /// </summary>
    [ContextMenu("Probar este plano (Play Mode)")]
    void ProbarPlano()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("[CameraFocusPoint] Entra en Play Mode para poder probar el plano de cámara.");
            return;
        }

        StartCoroutine(Co_ProbarPlano());
    }

    IEnumerator Co_ProbarPlano()
    {
        var cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[CameraFocusPoint] No se encontró Camera.main.");
            yield break;
        }

        bool wasLocked = vThirdPersonCamera.lockCameraForCinematic;
        vThirdPersonCamera.lockCameraForCinematic = true;
        cam.transform.SetPositionAndRotation(transform.position, transform.rotation);

        Debug.Log($"[CameraFocusPoint] Probando plano '{focusId}' durante {testHoldSeconds}s.");

        yield return new WaitForSecondsRealtime(testHoldSeconds);

        vThirdPersonCamera.lockCameraForCinematic = wasLocked;
    }
#endif
}
