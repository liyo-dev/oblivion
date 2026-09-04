using UnityEngine;

/// <summary>
/// Ancla un personaje/montura "volando" a una posición fija en pantalla, relativa a la cámara
/// del backdrop del menú (ver MainMenuWorldCameraDrift), replicando el efecto de las pantallas
/// de título tipo JRPG: el personaje no se mueve por el mundo, es el mundo el que se desplaza
/// detrás de él. Añade además una animación de vuelo en bucle (aleteo/balanceo) para que no se
/// vea estático ni perfectamente rígido.
///
/// Importante: NO se parentea el personaje directamente a la cámara. Un parenteo rígido pega el
/// personaje 100% a la cámara y el pequeño swaying de MainMenuWorldCameraDrift se nota mecánico.
/// En su lugar, este script sigue el offset deseado con SmoothDamp, dando sensación de "flotar"
/// ligeramente por detrás del movimiento de cámara — como si el ala derrapase un poco.
/// </summary>
[DisallowMultipleComponent]
public class MainMenuFlyingCompanion : MonoBehaviour
{
    [Header("Referencias")]
    [Tooltip("Cámara del backdrop del menú (la que lleva MainMenuWorldCameraDrift). Si se deja vacío, usa Camera.main en Awake.")]
    [SerializeField] private Transform menuCamera;

    [Header("Posición en pantalla (espacio local de cámara)")]
    [Tooltip("Offset respecto a la cámara: X = izquierda/derecha, Y = arriba/abajo, Z = profundidad (distancia delante de la cámara). Para el segundo personaje, usa X positivo.")]
    [SerializeField] private Vector3 cameraLocalOffset = new Vector3(-1.5f, -0.5f, 6f);

    [Tooltip("Suavizado del seguimiento. Más alto = más 'a rastras' detrás del movimiento de cámara.")]
    [SerializeField] private float followSmoothTime = 0.35f;

    [Header("Animación de vuelo (idle) — sutil a propósito")]
    [SerializeField] private float bobAmplitude = 0.008f;
    [SerializeField] private float bobSpeed = 0.45f;
    [SerializeField] private float rollAmplitudeDegrees = 0.15f;
    [SerializeField] private float rollSpeed = 0.4f;
    [SerializeField] private float pitchAmplitudeDegrees = 0.12f;
    [SerializeField] private float pitchSpeed = 0.35f;

    [Tooltip("Desfase de tiempo respecto a otros MainMenuFlyingCompanion, para que dos personajes no aleteen sincronizados.")]
    [SerializeField] private float animationTimeOffset;

    [Header("Orientación")]
    [Tooltip("El personaje mira SIEMPRE hacia donde mira la cámara (hacia el reino), nunca hacia el jugador — no se usa la rotación de fábrica del prefab, que varía de un héroe a otro y a veces queda mirando para atrás. Si aun así alguno queda mirando al revés, corrígelo aquí con 180.")]
    [SerializeField] private float yawCorrectionDegrees = 0f;

    [Header("Pose de vuelo (Animator)")]
    [Tooltip("Si está activo, intenta poner en marcha la pose de vuelo del rig al arrancar (ver TryEnterFlyPose). Desactívalo si el personaje ya trae su propia animación de vuelo por otro medio.")]
    [SerializeField] private bool driveFlyAnimator = true;
    [Tooltip("Nombre del parámetro bool que el propio juego usa para marcar 'en vuelo' — visto en Assets/Scripts/Player/PlayerFlyingController.cs (Animator.SetBool(\"isFlying\", true)), no adivinado.")]
    [SerializeField] private string flyingBoolParam = "isFlying";
    [Tooltip("Nombre del estado de reposo en vuelo que usa el propio juego — visto en PlayerFlyingController.cs (flyIdleState = \"fly_idle\"). No tiene por qué estar en la capa 0: se busca en todas las capas, igual que hace PlayerFlyingController.DetectFlightLayer().")]
    [SerializeField] private string flyIdleStateName = "fly_idle";

    Vector3 _velocity; // usado por SmoothDamp, no reasignar a mano
    float _time;

    void Awake()
    {
        _time = animationTimeOffset;

        if (!menuCamera && Camera.main)
            menuCamera = Camera.main.transform;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        if (!menuCamera)
            Debug.LogWarning($"[MainMenuFlyingCompanion] {name}: no se ha asignado menuCamera y no hay Camera.main disponible en la escena.");
#endif

        // FIX 4 sep 2026, ver DisableConflictingGameplaySystems() más abajo. Debe ir ANTES de
        // TryEnterFlyPose(): si se desactivan estos componentes después, NPCSimpleAnimator.Start()
        // ya habría alcanzado a deshacer la pose de vuelo forzada aquí.
        DisableConflictingGameplaySystems();

        if (driveFlyAnimator) TryEnterFlyPose();
    }

    /// <summary>
    /// FIX 4 sep 2026 (petición de Raúl: "will y liam en el MainMenu de pronto se ponen a temblar
    /// como si hubiera animaciones en conflicto"): este script se añade sobre el prefab COMPLETO
    /// del personaje jugable (el mismo que se usa en partida), que trae su propio cerebro de IA
    /// activo — NPCBehaviourManagerV2 + NPCSimpleAnimator + NavMeshAgent + Rigidbody — y nadie lo
    /// desactivaba para este uso puramente decorativo del menú. Concretamente:
    /// NPCSimpleAnimator.Start() llama a TransitionToIdle() nada más arrancar (deshaciendo la pose
    /// de vuelo forzada por TryEnterFlyPose()) y su propio LateUpdate() aplica ApplySmoothRotation()
    /// cada frame, escribiendo transform.rotation en competencia directa con el LateUpdate() de
    /// este script (que fija la rotación hacia la cámara) — dos sistemas peleando por la misma
    /// rotación cada frame es exactamente lo que se ve como "temblor"/animaciones en conflicto.
    /// Se desactivan (no se destruyen: reversible, sin tocar el prefab) todos los componentes de
    /// IA/física en vivo que este personaje no necesita como decorado de fondo — de paso evita que
    /// intente patrullar/perseguir o suelte diálogo aleatorio (LiamIdleCommentary) mientras "vuela"
    /// en el menú.
    /// </summary>
    private void DisableConflictingGameplaySystems()
    {
        var brain = GetComponent<Game.NPC.NPCBehaviourManagerV2>();
        if (brain != null) brain.enabled = false;

        var simpleAnimator = GetComponent<NPCSimpleAnimator>();
        if (simpleAnimator != null) simpleAnimator.enabled = false;

        var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null) agent.enabled = false;

        var rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    /// <summary>
    /// Reproduce EXACTAMENTE lo que hace el jugador real al entrar en vuelo (doble salto), leído
    /// directamente de Assets/Scripts/Player/PlayerFlyingController.cs::EnterFlight() — no es un
    /// intento a ciegas: mismo nombre de parámetro bool, mismo nombre de estado, y misma
    /// autodetección de capa (el estado de vuelo no vive necesariamente en la capa 0 del Animator,
    /// así que se busca en todas como hace el propio PlayerFlyingController.DetectFlightLayer()).
    /// También se sube el peso de esa capa a 1 por si arranca a 0 (el juego real lo hace también al
    /// entrar en vuelo, con SetLayerWeight) — sin eso la pose puede estar "activa" y aun así no
    /// verse, mezclada al 0% con la capa base.
    /// </summary>
    void TryEnterFlyPose()
    {
        var animators = GetComponentsInChildren<Animator>(true);
        foreach (var anim in animators)
        {
            if (anim == null) continue;

            // El rig trae varias capas (cuerpo, y variantes de pelo/equipo de las que solo una
            // está activa a la vez — el resto se quedan desactivadas por diseño, no es un error).
            // Forzar Play()/SetBool() en un Animator cuyo GameObject está inactivo, o cuyo
            // controller ni siquiera tiene el estado que buscamos, no hace nada útil y solo
            // ensucia la consola — así que las saltamos.
            if (!anim.gameObject.activeInHierarchy) continue;
            if (anim.runtimeAnimatorController == null) continue;

            bool hasBoolParam = false;
            foreach (var p in anim.parameters)
            {
                if (p.type == AnimatorControllerParameterType.Bool && p.name == flyingBoolParam)
                {
                    hasBoolParam = true;
                    break;
                }
            }
            if (hasBoolParam)
                anim.SetBool(flyingBoolParam, true);

            if (string.IsNullOrEmpty(flyIdleStateName)) continue;

            int flightLayer = -1;
            int hash = Animator.StringToHash(flyIdleStateName);
            for (int layer = 0; layer < anim.layerCount; layer++)
            {
                if (anim.HasState(layer, hash)) { flightLayer = layer; break; }
            }
            if (flightLayer < 0) continue; // este Animator concreto no tiene el estado de vuelo

            anim.SetLayerWeight(flightLayer, 1f);
            anim.Play(flyIdleStateName, flightLayer);
        }
    }

    void LateUpdate()
    {
        if (!menuCamera) return;

        _time += Time.deltaTime;

        Vector3 targetPos = menuCamera.TransformPoint(cameraLocalOffset);
        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref _velocity, followSmoothTime);

        float roll = Mathf.Sin(_time * rollSpeed * Mathf.PI * 2f) * rollAmplitudeDegrees;
        float pitch = Mathf.Sin(_time * pitchSpeed * Mathf.PI * 2f + 1.3f) * pitchAmplitudeDegrees;
        float bob = Mathf.Sin(_time * bobSpeed * Mathf.PI * 2f) * bobAmplitude;

        transform.position += menuCamera.up * bob;

        // Mira hacia donde mira la cámara (hacia el reino), no hacia el jugador. Antes se
        // multiplicaba por la rotación local original del prefab, pero esa rotación de fábrica no
        // es consistente entre los 3 héroes (algunos quedaban mirando hacia la cámara en vez de
        // hacia el mundo) — así que se ignora y, si hace falta, se corrige a mano con
        // yawCorrectionDegrees por personaje.
        transform.rotation = menuCamera.rotation * Quaternion.Euler(pitch, yawCorrectionDegrees, roll);
    }
}
