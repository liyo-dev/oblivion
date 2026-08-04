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

    public bool IsLaunching => _isLaunching;

    void Awake()
    {
        _controller = GetComponent<CharacterController>();
        _rigidbody = GetComponent<Rigidbody>();
        _animator = GetComponent<Animator>() ?? GetComponentInChildren<Animator>();
        _thirdPersonController = GetComponent<vThirdPersonController>() ?? GetComponentInParent<vThirdPersonController>();
        _actionManager = GetComponent<PlayerActionManager>() ?? GetComponentInParent<PlayerActionManager>();
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

        Vector3 startPos = transform.position;
        Vector3 lastIntendedPos = startPos;

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
            targetPos.y = startPos.y + verticalY;

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

        // Restaurar todo en orden inverso
        // El Rigidbody se restaura primero y con velocidad a cero: así Invector retoma el
        // control desde reposo, sin gravedad acumulada que descargar de golpe.
        if (_rigidbody != null)
        {
            _rigidbody.linearVelocity = Vector3.zero;
            _rigidbody.angularVelocity = Vector3.zero;
            _rigidbody.isKinematic = _cachedRigidbodyKinematic;
            _rigidbody.useGravity = _cachedRigidbodyUseGravity;
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
