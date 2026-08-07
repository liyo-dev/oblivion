using UnityEngine;

/// <summary>
/// Componente central y reutilizable para decidir CÓMO un trigger afecta el movimiento del
/// jugador, en vez de que cada script de trigger reimplemente PlayerLockService/Collider a mano
/// (lo que ya ha provocado inconsistencias: RoomExitBlocker, KingdomBoundaryTrigger,
/// DayOnlyInspectionTrigger y PortalTrigger tenían cada uno su propia versión, y otros
/// (LevelExit, SavePoint, BossArenaController, OnTriggerEnter_Event) no tenían ninguna).
///
/// Añade este componente al mismo GameObject que el trigger, configura el modo en el Inspector,
/// y desde el script del trigger llama a los métodos públicos en el momento adecuado.
///
/// Distinción de vocabulario (importante, no confundir):
/// - BLOQUEAR = muro direccional. Un Collider sólido impide avanzar en esa dirección concreta;
///   el jugador se mueve con total libertad en cualquier otra. Es lo que necesita, p. ej.,
///   la orilla del agua o un room exit.
/// - PARAR = freeze total temporal. El jugador deja de responder a CUALQUIER input (aunque se
///   mantenga el stick) durante el tiempo que dura una secuencia/cinemática que necesita que
///   esté quieto en ese punto exacto. Es lo que necesita, p. ej., cruzar el límite del Reino
///   en el final, o cualquier evento narrativo que dispare un diálogo/cinemática justo al
///   activarse.
///
/// Un trigger puede necesitar uno, otro, ambos, o ninguno — no asumir automáticamente que
/// "activar una secuencia" implica Parar: algunos triggers (p. ej. LorePopupPoint en su modo
/// popup ambiental) están pensados para disparar sin tocar al jugador en absoluto.
///
/// Nota de implementación: este componente SIEMPRE se registra en PlayerLockService usando su
/// propia instancia como owner (nunca un owner externo). Para IniciarParada()/TerminarParada()
/// esto permite que OnDisable()/OnDestroy() liberen el lock exacto que adquirieron sin depender
/// de que quien lo llamó siga vivo. IniciarParadaMomentanea() es distinto: delega la espera y la
/// liberación en PlayerLockService.AcquireBridgeUntilCinematic(), que vive en un objeto
/// persistente (DontDestroyOnLoad) precisamente porque varios triggers de este proyecto hacen
/// Destroy(gameObject) el mismo frame en que disparan su evento — si la espera viviera aquí, se
/// abortaría con el objeto antes de llegar a comprobar si el grafo narrativo tomó el control.
/// </summary>
public class TriggerPlayerStop : MonoBehaviour
{
    public enum PlayerStopMode
    {
        Ninguno,
        Bloquear,
        Parar,
        BloquearYParar
    }

    [Tooltip("Cómo debe afectar este trigger al movimiento del jugador.")]
    [SerializeField] private PlayerStopMode mode = PlayerStopMode.Ninguno;

    [Header("Bloquear (muro direccional)")]
    [Tooltip("Collider sólido — normalmente un hijo dedicado, NO el collider de detección del " +
             "trigger — que actúa de muro real. Solo se usa en modo Bloquear / BloquearYParar. " +
             "No reutilices aquí un collider que también sea la malla visual de otra cosa (p. ej. " +
             "el agua): su forma/posición rara vez coincide con por dónde camina el jugador.")]
    [SerializeField] private Collider physicalBlocker;

    [Tooltip("Estado inicial del bloqueo, antes de que nada lo cambie con SetBloqueado(). " +
             "Normalmente lo pisa la condición real del trigger (misión completada, hora del día, etc.).")]
    [SerializeField] private bool bloqueadoPorDefecto = true;

    public PlayerStopMode Mode => mode;
    public bool EstaParado => _lockAcquired;

    private bool _lockAcquired;
    private bool _bloqueado;

    void Awake()
    {
        _bloqueado = bloqueadoPorDefecto;
        AplicarEstadoBloqueo();
    }

    void OnDisable() => TerminarParada();
    void OnDestroy() => TerminarParada();

    /// <summary>Activa/desactiva el muro físico. Sin efecto si el modo no incluye Bloquear.</summary>
    public void SetBloqueado(bool bloqueado)
    {
        _bloqueado = bloqueado;
        AplicarEstadoBloqueo();
    }

    private void AplicarEstadoBloqueo()
    {
        if (mode != PlayerStopMode.Bloquear && mode != PlayerStopMode.BloquearYParar) return;
        if (physicalBlocker == null) return;

        bool shouldBeTrigger = !_bloqueado;
        if (physicalBlocker.isTrigger != shouldBeTrigger)
            physicalBlocker.isTrigger = shouldBeTrigger;
    }

    /// <summary>
    /// Congela al jugador (PlayerLockService) hasta que se llame a TerminarParada(). Sin efecto
    /// si el modo no incluye Parar. Idempotente: llamar dos veces seguidas no hace nada la segunda.
    /// </summary>
    public void IniciarParada()
    {
        if (mode != PlayerStopMode.Parar && mode != PlayerStopMode.BloquearYParar) return;
        if (_lockAcquired) return;

        var lockService = PlayerLockService.Instance;
        if (lockService == null) return;

        lockService.Acquire(this);
        _lockAcquired = true;
    }

    /// <summary>Libera una parada iniciada con IniciarParada(). Seguro de llamar sin parada activa.</summary>
    public void TerminarParada()
    {
        if (!_lockAcquired) return;
        if (PlayerLockService.HasInstance)
            PlayerLockService.Instance.Release(this);
        _lockAcquired = false;
    }

    /// <summary>
    /// Atajo para el caso típico de "trigger de un solo disparo que lanza un evento narrativo":
    /// congela al jugador YA (puente inmediato) y libera en cuanto el sistema narrativo
    /// (diálogo/cinemática) tome el control con su propio PushMode(ActionMode.Cinematic) — o tras
    /// un timeout de seguridad si eso nunca llega a pasar. Mismo patrón ya probado en
    /// KingdomBoundaryTrigger y DayOnlyInspectionTrigger. Sin efecto si el modo no incluye Parar.
    ///
    /// FIX (Agosto 2026): antes liberaba el lock un frame fijo después vía una corrutina alojada
    /// en ESTE componente. Eso se quedaba corto en dos casos reales del proyecto: (1) el grafo
    /// narrativo puede tardar más de 1 frame en encadenar desde el WaitCustomEventNode que
    /// consume este evento hasta el nodo que realmente bloquea (cada salto de
    /// NarrativeRunner.RunSubGraph cede como mínimo 1 frame, aunque el nodo resuelva al instante);
    /// (2) triggers con DestroyElement=1 en OnTriggerEnter_Event destruían este mismo GameObject
    /// el mismo frame, abortando la corrutina antes de que llegara a esperar. En ambos casos el
    /// freeze se soltaba antes de que la cinemática tomara el control de verdad, y el jugador
    /// quedaba mal ubicado. Ver PlayerLockService.AcquireBridgeUntilCinematic para el detalle:
    /// ahora la espera vive en PlayerLockService (persistente), no aquí, así que sobrevive aunque
    /// este componente se destruya, y comprueba el estado real de ActionMode.Cinematic en vez de
    /// asumir que 1 frame siempre alcanza. No usa _lockAcquired/TerminarParada(): PlayerLockService
    /// libera este lock por su cuenta cuando corresponde.
    /// </summary>
    public void IniciarParadaMomentanea()
    {
        if (mode != PlayerStopMode.Parar && mode != PlayerStopMode.BloquearYParar) return;

        var lockService = PlayerLockService.Instance;
        if (lockService == null) return;

        lockService.AcquireBridgeUntilCinematic(this);
    }
}
