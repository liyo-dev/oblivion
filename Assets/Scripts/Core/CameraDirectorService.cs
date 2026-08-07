using System.Collections;
using UnityEngine;

/// <summary>
/// Árbitro central de "quién tiene el control de la cámara cinemática" (el flag estático
/// <see cref="vThirdPersonCamera.lockCameraForCinematic"/>).
///
/// Motivación: hoy cada sistema de cámara (CinematicCameraDriver, FocusCameraNode,
/// KingdomExitTransitionNode, BossIntroPresentation, etc.) pone y quita ese flag por su cuenta.
/// Cuando dos sistemas se entregan el control casi en el mismo instante (ej: una secuencia
/// termina y, uno o dos frames después, un nodo del grafo narrativo corta a otro plano),
/// el hueco entre "suelto el flag" y "el siguiente sistema lo vuelve a tomar" lo ocupa
/// vThirdPersonCamera, que retoma el control de gameplay durante ese instante — el salto
/// brusco a modo gameplay que se ve entre dos cortes que deberían sentirse como uno solo.
///
/// Este servicio no sustituye a vThirdPersonCamera ni reimplementa el corte de cámara de nadie:
/// solo decide CUÁNDO se suelta realmente el flag. Los sistemas de cámara deben:
///   - Al tomar el control:  CameraDirectorService.Claim(this)   en vez de poner el flag a true.
///   - Al soltarlo:          CameraDirectorService.Release(this) en vez de ponerlo a false.
///
/// Claim() cancela cualquier liberación pendiente de forma inmediata (aunque sea de otro owner),
/// así que si un segundo sistema reclama la cámara mientras el primero todavía está "soltando",
/// el flag nunca llega a bajar y vThirdPersonCamera nunca ve un frame para meter baza.
/// Release() no baja el flag al instante: espera <see cref="ReleaseGraceSeconds"/> por si alguien
/// reclama la cámara en ese margen (coalescing). Si nadie la reclama, entonces sí se libera y
/// vThirdPersonCamera recupera el control con su propio suavizado existente (_doSmoothSnap).
///
/// Migración incremental (ver CLAUDE.md §7 sobre por qué no se toca todo de golpe): mientras no
/// todos los sistemas de cámara pasen por aquí, el flag de vThirdPersonCamera sigue siendo la
/// única fuente de verdad real. Los sistemas que aún no se han migrado a Claim()/Release() siguen
/// funcionando exactamente igual que antes; para ellos este servicio es invisible.
/// </summary>
public static class CameraDirectorService
{
    /// Ventana de gracia por defecto tras un Release(): si nadie reclama la cámara en este
    /// margen, se suelta de verdad. Deliberadamente corta (solo cubre la latencia normal de
    /// procesado de eventos/grafo entre un "suelto" y un "reclamo" inmediato); no está pensada
    /// para tapar transiciones largas — para eso sigue siendo correcto usar el patrón
    /// Co_EndCinematicStayBlack + FeedbackService.IsScreenFaded (pantalla cubierta, así que da
    /// igual cuánto tarde el siguiente sistema en reclamar).
    private const float ReleaseGraceSeconds = 0.15f;

    private static object s_currentOwner;
    private static Coroutine s_pendingRelease;
    private static Runner s_runner;

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        s_currentOwner = null;
        s_pendingRelease = null;
        s_runner = null;
    }
#endif

    /// True si hay algún owner con el control reclamado en este momento (incluida la ventana de gracia).
    public static bool HasOwner => s_currentOwner != null;

    /// Fuerza el reseteo completo del estado interno (owner + liberación pendiente) y suelta el
    /// flag real, ignorando quién sea el owner actual. Pensado para los mismos puntos de entrada
    /// seguros de sesión que GameBootService.ResetTransientSessionState() (MainMenu, arranque de
    /// partida): ahí es seguro asumir que no hay ninguna cámara bloqueada legítimamente en curso,
    /// así que no hace falta (ni conviene) respetar el ownership actual como sí hace Release().
    /// Sin esto, un owner de una sesión anterior que nunca llegó a soltar (ej:
    /// KingdomExitTransitionNode.CloseDemo(), que deja el candado true a propósito porque la
    /// escena va a cambiar) dejaría s_currentOwner apuntando para siempre a un objeto ya
    /// destruido de la sesión anterior.
    public static void ForceResetState()
    {
        CancelPendingRelease();
        s_currentOwner = null;
        vThirdPersonCamera.lockCameraForCinematic = false;
    }

    /// Reclama el control de la cámara cinemática para <paramref name="owner"/>. Cancela
    /// cualquier liberación pendiente (propia o de otro owner) antes de que llegue a aplicarse,
    /// de forma que el flag nunca baja entre un handoff y el siguiente.
    public static void Claim(object owner)
    {
        if (owner == null) return;
        CancelPendingRelease();
        s_currentOwner = owner;
        vThirdPersonCamera.lockCameraForCinematic = true;
    }

    /// Pide soltar el control en nombre de <paramref name="owner"/>. Solo tiene efecto si
    /// <paramref name="owner"/> es quien tiene el control ahora mismo. No libera al instante:
    /// programa la liberación real tras ReleaseGraceSeconds para dar tiempo a un posible
    /// siguiente Claim() a coalescer con este handoff.
    public static void Release(object owner)
    {
        if (owner == null || !Equals(s_currentOwner, owner)) return;
        CancelPendingRelease();
        EnsureRunner();
        s_pendingRelease = s_runner.StartCoroutine(Co_DeferredRelease(owner));
    }

    private static IEnumerator Co_DeferredRelease(object owner)
    {
        yield return new WaitForSecondsRealtime(ReleaseGraceSeconds);
        // Si nadie ha reclamado la cámara durante la ventana de gracia, soltar de verdad.
        if (Equals(s_currentOwner, owner))
        {
            s_currentOwner = null;
            vThirdPersonCamera.lockCameraForCinematic = false;
        }
        s_pendingRelease = null;
    }

    private static void CancelPendingRelease()
    {
        if (s_pendingRelease != null && s_runner != null)
            s_runner.StopCoroutine(s_pendingRelease);
        s_pendingRelease = null;
    }

    private static void EnsureRunner()
    {
        if (s_runner != null) return;
        var go = new GameObject("CameraDirectorService");
        Object.DontDestroyOnLoad(go);
        s_runner = go.AddComponent<Runner>();
    }

    /// MonoBehaviour mínimo: una clase estática no puede alojar coroutines, así que este
    /// componente solo existe para darle un StartCoroutine/StopCoroutine al servicio.
    private class Runner : MonoBehaviour
    {
    }
}
