using System.Collections;
using UnityEngine;
using Invector.vCharacterController;

/// <summary>
/// Lanza al objetivo hacia atrás describiendo un arco por el aire (estilo "knockback aéreo" de
/// Kingdom Hearts) en vez de un simple deslizamiento por el suelo. Pensado para reacciones a
/// impactos fuertes (choque de hechizos, etc.).
///
/// Es la ÚNICA fuente de movimiento durante el lanzamiento: desactiva Root Motion y el
/// vThirdPersonController (Invector) mientras dura, y empuja ActionMode.Stunned en
/// PlayerActionManager para que ningún otro sistema (input de movimiento, gravedad de Invector)
/// compita por mover al personaje al mismo tiempo. Antes de este componente, el roll hacia atrás
/// podía "irse hacia el lado" precisamente porque el controlador normal seguía aplicando el
/// movimiento/input del jugador en paralelo al desplazamiento del knockback.
///
/// IMPORTANTE: este rig (Invector LITE, ver `vThirdPersonMotor.cs`) NO usa CharacterController,
/// usa un Rigidbody no-kinemático con `useGravity = true`. Desactivar `vThirdPersonController`
/// detiene la lógica de movimiento del script, pero el motor de físicas de Unity le sigue
/// aplicando gravedad al Rigidbody en cada FixedUpdate durante todo el arco (~0.6s), acumulando
/// velocidad vertical negativa mientras este componente fuerza la posición por encima del suelo.
/// Al terminar la corrutina y devolver el control a Invector, esa velocidad acumulada se
/// descarga de golpe y el jugador se hunde/atraviesa el suelo. Por eso aquí también hay que
/// poner el Rigidbody en kinemático (sin gravedad, sin velocidad) mientras dura el lanzamiento.
/// </summary>
[DisallowMultipleComponent]
public class AerialKnockbackReceiver : MonoBehaviour
{
    private CharacterController _controller;
    private Rigidbody _rigidbody;
    private Animator _animator;
    private vThirdPersonController _thirdPersonController;
    private PlayerActionManager _actionManager;

    private bool _isLaunching;
    private bool _cachedApplyRootMotion;
    private bool _controllerWasEnabled;
    private bool _stunnedModePushed;
    private bool _cachedRigidbodyKinematic;
    private bool _cachedRigidbodyUseGravity;

    // Sondeo de suelo real durante el arco: sin esto, la Y de aterrizaje se calculaba como
    // "startPos.y + curva seno" (siempre vuelve a la altura de despegue), así que si el suelo
    // bajo el punto de caída no está exactamente a esa misma altura (escalones, pendiente,
    // terreno irregular, bordes de plataforma) el jugador terminaba flotando sobre el suelo o
    // incrustado en él al recuperar el control físico. Ahora cada frame se lanza un rayo hacia
    // abajo en la XZ del arco y la curva de altura se suma sobre el suelo real detectado, no
    // sobre la altura de despegue.
    private const float GroundProbeHeight = 3f;
    private const float GroundProbeMaxDistance = 10f;
    private const float GroundSkin = 0.05f;
    private float _lastGroundY;

    // Red de seguridad post-aterrizaje: tras devolver el control a la física normal, se vigila
    // durante un rato corto que la posición no quede por debajo del suelo detectado. Esto no
    // sustituye al arco (que ya debería aterrizar bien), es un catch-all para si, por lo que sea
    // (colisión con otro collider durante el tramo kinemático, orden de ejecución de scripts,
    // Invector reactivando su propia gravedad antes de que el frame se estabilice, etc.), el
    // jugador queda incrustado de todas formas: aquí se detecta y se corrige en vez de dejar que
    // el juego se rompa.
    private const float PostLandingWatchDuration = 0.4f;
    private const float SinkTolerance = 0.15f;

    // Máscara de suelo cacheada en Awake (regla del proyecto: LayerMask.GetMask nunca en un
    // bucle por frame). Usar SOLO `vThirdPersonController.groundLayer` resultó insuficiente en
    // la práctica: si esa layer mask no incluye la layer real del suelo en algún escenario/arena
    // (p. ej. una zona de test cuyo suelo se quedó en "Default" en vez de "Floor"), el raycast no
    // golpea nada nunca y el aterrizaje vuelve a fallar exactamente igual que antes de este fix,
    // en silencio. Para no depender de que esa única layer esté bien configurada en cada escena,
    // se golpea "todo lo que no sea explícitamente no-sólido" (jugador, proyectiles, enemigos,
    // agua, UI, triggers de interacción...), igual que se calcularía por exclusión.
    private LayerMask _groundProbeMask;

    // Radio del SphereCast de suelo, ajustado en Awake al CapsuleCollider real del jugador.
    // Con un Raycast fino era posible colarse por las rendijas entre piezas del suelo (una losa
    // hecha de trozos sueltos, como la de la arena del Boss Demonio) y detectar lo que hay debajo
    // (agua, vacío) en vez de la losa real — eso es lo que causaba el hundimiento momentáneo.
    // Invector usa el mismo truco (SphereCast en vez de Raycast) en su propio ground check, ver
    // CheckGroundDistance en vThirdPersonMotor.cs.
    private float _groundProbeRadius = 0.3f;

    // Chequeo de obstrucción horizontal del arco: sin esto, el lanzamiento mueve al jugador de
    // forma puramente cinemática (Rigidbody.MovePosition/CharacterController.Move sobre una
    // posición ya calculada, ver comentario de clase) y puede empujarlo DENTRO de un collider
    // sólido que tenga detrás en vez de detenerse ante él — por ejemplo el collider de un
    // RoomExitBlocker bloqueado (una quest gate, como la que exige ayudar al niño pez antes de
    // entrar al agua: mientras está bloqueada su collider es sólido, no trigger). El síntoma es
    // exactamente "el choque de hechizos no me lanza por el aire, me mete en el suelo/geometría" y,
    // si el gate estaba detrás, dispara además el mensaje de bloqueo de esa quest al solaparse con
    // su collider. Se usa "todo" como máscara y se filtra en el propio chequeo, porque personajes y
    // geometría estática comparten la layer "Default" en este proyecto y no se pueden separar por
    // layer (mismo criterio que PlayerParty.FindClearDialogueFormationPosition, ver CLAUDE.md §2).
    private LayerMask _obstructionMask;
    private float _obstructionProbeHeight = 1f;

    // Buffer del SphereCast de obstrucción (regla del proyecto: nunca alloc en runtime, ver
    // MagicProjectil.cs). Se usa la variante *NonAlloc* y, sobre todo, la variante "All": con la
    // variante de un solo resultado el propio collider del jugador (el sphere-cast arranca
    // solapándolo, está justo en el origin) siempre habría sido el hit más cercano y habría tapado
    // cualquier obstáculo real detrás; con el buffer se recorren todos los hits y se descartan los
    // que sean personajes (ver ClampLaunchDistance).
    private readonly RaycastHit[] _obstructionHitsBuffer = new RaycastHit[8];

    public bool IsLaunching => _isLaunching;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _thirdPersonController = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();

        // Antes se usaba "todo menos lo explícitamente no-suelo" por si el suelo real no estaba
        // en la layer "Floor" en alguna escena. Confirmado que sí lo está siempre, así que se
        // restringe a las layers que son de verdad terreno sólido (Floor + Obstacle): con la
        // máscara amplia, el SphereCast (más ancho que un Raycast, necesario para no colarse por
        // las rendijas entre losas) empezaba a detectar props/decoración cercanos por encima del
        // suelo real como si fueran "suelo", aplastando la altura del arco (el lanzamiento se
        // quedaba casi en el sitio en vez de subir).
        _groundProbeMask = LayerMask.GetMask("Floor", "Obstacle");

        var capsule = GetComponent<CapsuleCollider>() ?? GetComponentInChildren<CapsuleCollider>();
        if (capsule != null)
        {
            _groundProbeRadius = Mathf.Max(0.15f, capsule.radius * 0.9f);
            _obstructionProbeHeight = capsule.center.y > 0f ? capsule.center.y : capsule.height * 0.5f;
        }

        _obstructionMask = ~0;
    }

    /// <summary>
    /// Lanza al objetivo hacia atrás (dirección ya calculada por el llamador, normalmente
    /// -transform.forward del propio objetivo para garantizar que SIEMPRE va hacia atrás,
    /// sin depender de vectores geométricos como la posición del proyectil o del impacto).
    /// </summary>
    public void Launch(Vector3 backwardDirection, float distance, float height, float duration)
    {
        if (_isLaunching) return; // no solapar lanzamientos
        if (duration <= 0f) return;

        StopAllCoroutines();
        StartCoroutine(LaunchRoutine(backwardDirection, distance, height, duration));
    }

    private IEnumerator LaunchRoutine(Vector3 backwardDirection, float distance, float height, float duration)
    {
        _isLaunching = true;

        // Desactivar Root Motion mientras dura el lanzamiento: la trayectoria la controla
        // este script en exclusiva, no el clip de animación (evita el "tironeo" por doble
        // fuente de movimiento que causaba el desvío lateral).
        if (_animator != null)
        {
            _cachedApplyRootMotion = _animator.applyRootMotion;
            _animator.applyRootMotion = false;
        }

        // Desactivar el controlador de Invector: si sigue activo, su propia gravedad/input
        // seguiría llamando a CharacterController.Move() en paralelo a este script.
        if (_thirdPersonController != null)
        {
            _controllerWasEnabled = _thirdPersonController.enabled;
            _thirdPersonController.enabled = false;
        }

        if (_actionManager != null)
        {
            _actionManager.PushMode(ActionMode.Stunned);
            _stunnedModePushed = true;
        }

        // Neutralizar el Rigidbody de Invector: si se deja no-kinemático con gravedad activa,
        // el motor de físicas sigue acelerándolo hacia abajo durante todo el arco aunque el
        // script de Invector esté desactivado, y esa velocidad se descarga de golpe al terminar
        // (el jugador se hunde en el suelo). Kinemático + velocidad a cero = el Rigidbody no
        // acumula nada mientras este componente controla la posición a mano.
        if (_rigidbody != null)
        {
            _cachedRigidbodyKinematic = _rigidbody.isKinematic;
            _cachedRigidbodyUseGravity = _rigidbody.useGravity;
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.useGravity = false;
            _rigidbody.isKinematic = true;
        }

        Vector3 flatDir = backwardDirection;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude < 0.0001f) flatDir = -transform.forward;
        flatDir.Normalize();

        // Recortar la distancia si hay geometría sólida (pared, puerta, quest gate bloqueada...) en
        // el camino del arco: ver comentario de _obstructionMask. Sin esto la trayectoria se calcula
        // a ciegas y puede terminar empujando al jugador dentro de un collider sólido.
        Vector3 obstructionOrigin = transform.position + Vector3.up * _obstructionProbeHeight;
        distance = ClampLaunchDistance(obstructionOrigin, flatDir, distance);

        Vector3 startPos = transform.position;
        Vector3 lastIntendedPos = startPos;
        _lastGroundY = startPos.y;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);

            // Arco parabólico: avance horizontal con ease-out (rápido al inicio, se frena al final)
            // y altura con una curva de seno (sube, alcanza el pico, y vuelve a bajar).
            float horizontalT = 1f - (1f - t) * (1f - t);
            float verticalY = Mathf.Sin(t * Mathf.PI) * height;

            Vector3 targetPos = startPos + flatDir * (distance * horizontalT);

            // La curva de altura se apoya sobre el suelo real bajo la XZ actual del arco, no sobre
            // "startPos.y": así el aterrizaje (verticalY = 0 en t = 1) cae exactamente sobre el
            // suelo detectado aunque el terreno de llegada esté más alto/bajo que el de despegue.
            float groundY = SampleGroundY(targetPos.x, targetPos.z);
            targetPos.y = groundY + GroundSkin + verticalY;

            Vector3 frameDelta = targetPos - lastIntendedPos;
            lastIntendedPos = targetPos;

            if (_controller != null && _controller.enabled)
                _controller.Move(frameDelta);
            else if (_rigidbody != null && _rigidbody.isKinematic)
                _rigidbody.MovePosition(transform.position + frameDelta);
            else
                transform.position += frameDelta;

            yield return null;
        }

        // Snap final de seguridad: aunque la curva ya converge a "suelo + GroundSkin" en el
        // último frame, esto corrige cualquier residuo de redondeo por duración de frame y deja
        // al jugador exactamente sobre el suelo real antes de devolver el control a la física
        // normal (evita el hundimiento al restaurar el Rigidbody a no-kinemático).
        float finalGroundY = SampleGroundY(transform.position.x, transform.position.z);
        Vector3 finalPos = transform.position;
        finalPos.y = finalGroundY + GroundSkin;
        if (_rigidbody != null && _rigidbody.isKinematic)
            _rigidbody.MovePosition(finalPos);
        else
            transform.position = finalPos;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AerialKnockbackReceiver] Fin del arco. groundY detectado={finalGroundY:F2}, posición final={finalPos}, rb.isKinematic (antes de restaurar)={_rigidbody?.isKinematic}");
#endif

        // Restaurar todo en orden inverso
        // El Rigidbody se restaura primero y con velocidad a cero: así Invector retoma el
        // control desde reposo, sin gravedad acumulada que descargar de golpe.
        // IMPORTANTE: isKinematic hay que restaurarlo ANTES de tocar linearVelocity/angularVelocity.
        // Unity ignora (con warning) cualquier asignación de velocidad mientras el Rigidbody sigue
        // siendo kinemático, así que poner primero la velocidad a cero y el kinemático después
        // (como estaba antes) no tenía ningún efecto real: el Rigidbody volvía a dinámico con la
        // velocidad que tuviera cacheada por el motor de físicas, no con cero.
        if (_rigidbody != null)
        {
            _rigidbody.isKinematic = _cachedRigidbodyKinematic;
            _rigidbody.useGravity = _cachedRigidbodyUseGravity;
            if (!_cachedRigidbodyKinematic)
            {
                _rigidbody.linearVelocity = Vector3.zero;
                _rigidbody.angularVelocity = Vector3.zero;
            }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[AerialKnockbackReceiver] Rigidbody restaurado: isKinematic={_rigidbody.isKinematic}, useGravity={_rigidbody.useGravity}, posición={_rigidbody.position}");
#endif
        }

        if (_thirdPersonController != null)
            _thirdPersonController.enabled = _controllerWasEnabled;

        if (_animator != null)
            _animator.applyRootMotion = _cachedApplyRootMotion;

        if (_stunnedModePushed && _actionManager != null)
        {
            _actionManager.PopMode(ActionMode.Stunned);
            _stunnedModePushed = false;
        }

        _isLaunching = false;

        StartCoroutine(PostLandingSafetyNet());
    }

    /// <summary>
    /// Vigila un instante corto tras devolver el control a la física normal y corrige la posición
    /// si el jugador queda por debajo del suelo detectado (ver comentario del campo
    /// PostLandingWatchDuration). No debería activarse nunca si el arco aterriza bien; si se ve
    /// el warning en consola con frecuencia, es la pista de que el problema real está en otro
    /// sitio (otro sistema tocando el Rigidbody justo al recuperar el control, por ejemplo).
    /// </summary>
    private IEnumerator PostLandingSafetyNet()
    {
        float watched = 0f;
        while (watched < PostLandingWatchDuration)
        {
            watched += Time.deltaTime;

            if (_rigidbody != null && !_rigidbody.isKinematic)
            {
                float groundY = SampleGroundY(transform.position.x, transform.position.z);
                float sinkAmount = groundY - transform.position.y;

                if (sinkAmount > SinkTolerance)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.LogWarning($"[AerialKnockbackReceiver] Corrección post-aterrizaje: jugador {sinkAmount:F2}m por debajo del suelo detectado (rb.isKinematic={_rigidbody.isKinematic}, useGravity={_rigidbody.useGravity}). Reposicionando.");
#endif
                    Vector3 corrected = _rigidbody.position;
                    corrected.y = groundY + GroundSkin;
                    _rigidbody.position = corrected;

                    if (_rigidbody.linearVelocity.y < 0f)
                    {
                        Vector3 vel = _rigidbody.linearVelocity;
                        vel.y = 0f;
                        _rigidbody.linearVelocity = vel;
                    }
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// Sondea hacia abajo en la XZ dada para encontrar la altura real del suelo. Usa un
    /// SphereCast (no un Raycast fino) precisamente para no colarse por las rendijas entre piezas
    /// del suelo. Si no detecta nada (hueco real, borde de plataforma, vacío), devuelve la última
    /// altura de suelo válida conocida en vez de dejar caer el arco al vacío.
    /// </summary>
    private float SampleGroundY(float x, float z)
    {
        Vector3 origin = new Vector3(x, _lastGroundY + GroundProbeHeight, z);

        if (Physics.SphereCast(origin, _groundProbeRadius, Vector3.down, out RaycastHit hit, GroundProbeHeight + GroundProbeMaxDistance, _groundProbeMask, QueryTriggerInteraction.Ignore))
        {
            // Truco conocido de Unity: si el SphereCast arranca ya solapando un collider (p. ej.
            // un banner/arco/estructura por encima de la arena, dentro del radio de la esfera en
            // el punto de origen), hit.point/hit.normal vienen a (0,0,0) en vez del punto real de
            // contacto. Sin filtrar esto, _lastGroundY se corrompía a 0 (altura de mundo, no del
            // suelo) y el arco entero se aplastaba contra esa referencia falsa: por eso el
            // lanzamiento se quedaba casi en el sitio en vez de subir. Se descarta ese resultado y
            // se mantiene la última altura de suelo válida.
            bool isBogusZeroHit = hit.point == Vector3.zero && hit.normal == Vector3.zero;
            if (!isBogusZeroHit)
                _lastGroundY = hit.point.y;
        }

        return _lastGroundY;
    }

    /// <summary>
    /// Recorta la distancia horizontal solicitada si hay un collider sólido en el camino (ver
    /// comentario de _obstructionMask). Ignora triggers (QueryTriggerInteraction.Ignore) y también
    /// ignora impactos contra otros personajes: NPCs y player comparten la layer "Default" con la
    /// geometría estática, así que se filtran por el marcador NPCSimpleAnimator (mismo criterio que
    /// PlayerParty.FindClearDialogueFormationPosition) en vez de por layer. Usa la variante "All"
    /// (NonAlloc) precisamente porque el propio collider del jugador está en el origin del cast y
    /// sería el hit más cercano con la variante de un solo resultado, tapando cualquier obstáculo
    /// real que hubiera detrás.
    /// </summary>
    private float ClampLaunchDistance(Vector3 origin, Vector3 direction, float requestedDistance)
    {
        if (requestedDistance <= 0f) return requestedDistance;

        int count = Physics.SphereCastNonAlloc(origin, _groundProbeRadius, direction, _obstructionHitsBuffer, requestedDistance, _obstructionMask, QueryTriggerInteraction.Ignore);
        if (count <= 0) return requestedDistance;

        bool foundObstruction = false;
        float closestDistance = requestedDistance;
        string hitName = null;

        for (int i = 0; i < count; i++)
        {
            RaycastHit hit = _obstructionHitsBuffer[i];
            Transform root = hit.collider.transform.root;
            if (root.GetComponent<NPCSimpleAnimator>() != null)
                continue; // es un personaje (incluye al propio jugador), no un obstáculo real

            if (hit.distance < closestDistance)
            {
                closestDistance = hit.distance;
                foundObstruction = true;
                hitName = hit.collider.name;
            }
        }

        if (!foundObstruction) return requestedDistance;

        float clamped = Mathf.Max(0f, closestDistance - GroundSkin);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[AerialKnockbackReceiver] Obstrucción detectada en el arco ('{hitName}'), distancia recortada de {requestedDistance:F2}m a {clamped:F2}m.");
#endif
        return clamped;
    }

#if UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        // Este componente no tiene estado estático, pero se documenta el patrón por si en el
        // futuro se añade algo compartido entre sesiones de PlayMode (ver CLAUDE.md §3).
    }
#endif
}
