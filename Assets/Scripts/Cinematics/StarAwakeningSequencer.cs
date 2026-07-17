using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia del Despertar de la Estrella.
/// Señal de entrada: "AWAKEN_START".
/// Señal de salida (éxito): "AWAKEN_DONE"  → arranca el combate.
/// Señal de salida (fallo): "AWAKEN_FAILED" → grafo vuelve al diálogo con Eldran.
[DisallowMultipleComponent]
public class StarAwakeningSequencer : MonoBehaviour
{
    [Header("Personajes")]
    [SerializeField] private Transform       willTransform;
    [SerializeField] private Animator        willAnimator;
    [SerializeField] private Transform       willCastOrigin;
    [SerializeField] private Transform       companionTransform;
    [SerializeField] private PlayerActionManager actionManager;
    [Tooltip("Posición y rotación exactas donde debe estar Will al inicio de la cinemática. " +
             "Se teleporta ahí durante el fade a negro para que los planos siempre encajen.")]
    [SerializeField] private Transform       willAnchor;

    [Header("Disparo de Will — sistema real del jugador")]
    [Tooltip("MagicProjectileSpawner del jugador; se llama en el último botón correcto del panic input")]
    [SerializeField] private MagicProjectileSpawner playerSpawner;
    [Tooltip("Slot que se dispara (Right = botón principal de magia)")]
    [SerializeField] private MagicSlot castSlot = MagicSlot.Right;
    [Tooltip("Hechizo de reserva cinemático: se usa si el jugador no tiene nada equipado en ese slot " +
             "o no ha desbloqueado la magia todavía. Nunca falla, ignora maná y cooldowns.")]
    [SerializeField] private MagicSpellSO cinematicSpellFallback;
    [SerializeField] private string castAnimState      = "MagicRight";
    [SerializeField] private int    willUpperBodyLayer = 1;

    [Header("Proyectil enemigo")]
    [SerializeField] private SlowMotionFireProjectile incomingProjectilePrefab;
    [SerializeField] private Transform  projectileSpawnPoint;
    [Tooltip("Transform hacia el que vuela el proyectil. Asigna aquí el GO del jugador (o un hijo suyo a la altura del pecho).")]
    [SerializeField] private Transform  projectileTarget;
    [Tooltip("Si > 0, ignora la posición exacta del SpawnPoint y coloca el proyectil a esta distancia de Will en la misma dirección. Permite afinar sin mover el GO en escena.")]
    [SerializeField] private float projectileSpawnDistance = 0f;
    [SerializeField] private GameObject explosionVFX;

    [Header("Música")]
    [SerializeField] private AudioGraphProfile audioProfile;
    [SerializeField] private string sequenceMusicId = "AWAKEN";

    [Header("Cámara — driver y planos")]
    [SerializeField] private CinematicCameraDriver cinematicCamera;
    [Tooltip("Primer plano de Eldran — EVT_AWAKEN_01")]
    [SerializeField] private Transform camShotEldran;
    [Tooltip("Perfil de Will girando hacia el proyectil — EVT_AWAKEN_02")]
    [SerializeField] private Transform camShotWillProfile;
    [Tooltip("Plano que muestra el proyectil entrante")]
    [SerializeField] private Transform camShotProjectile;
    [Tooltip("Encuadre two-shot: Will a un lado, proyectil al otro — panic input y explosión")]
    [SerializeField] private Transform camShotTwoShot;
    [Tooltip("Primer plano de Will — cierre exitoso")]
    [SerializeField] private Transform camShotWillFinal;

    [Header("Sistemas")]
    [SerializeField] private PanicInputDetector     panicInputDetector;
    [SerializeField] private PanicInputUI           panicInputUI;
    [SerializeField] private Sprite                 panicButtonSprite;
    [SerializeField] private ShockEffectsController shockEffects;

    [Header("Señales narrativas")]
    [SerializeField] private string signalIn      = "AWAKEN_START";
    [SerializeField] private string signalOutDone = "AWAKEN_DONE";
    [SerializeField] private string signalOutFail = "AWAKEN_FAILED";

    [Header("Timings")]
    [SerializeField] private float preDialogueDuration      = 2f;
    [SerializeField] private float willTurnDuration         = 0.4f;
    [SerializeField] private float willReactionDuration     = 0.6f;
    [SerializeField] private float projectileRevealDuration = 1.2f;
    [SerializeField] private float slowMotionScale          = 0.2f;
    [SerializeField] private float fadeToBlackDuration      = 0.2f;
    [SerializeField] private float holdOnBlackDuration      = 0.4f;
    [SerializeField] private float fadeFromBlackDuration    = 0.35f;
    [Tooltip("Segundos reales máximos esperando colisión entre los dos proyectiles antes de forzar explosión.")]
    [SerializeField] private float collisionWaitUnscaled    = 3.0f;
    [SerializeField] private float timeReturnDuration       = 0.9f;
    [Tooltip("Tiempo en negro antes de que suene el pitido y arranque el combate")]
    [SerializeField] private float blackScreenDuration      = 1.2f;
    [Tooltip("Segundos de tiempo escalado entre que arranca la animación de lanzamiento de Will " +
             "y el spawn del proyectil. Usar tiempo escalado hace que se mantenga sincronizado " +
             "con la animación también en cámara lenta. Debe coincidir con el frame de release de MagicRight.")]
    [SerializeField] private float castAnimDelay            = 0.3f;

    [Header("SpeechBubble — claves de localización")]
    [SerializeField] private string keyCompanionAlert = "EVT_AWAKEN_01";
    [SerializeField] private string keyWillSurprise   = "EVT_AWAKEN_02";
    [SerializeField] private string keyWillSuccess    = "EVT_AWAKEN_03";
    [SerializeField] private string keyWillAfter      = "EVT_AWAKEN_05";
    [SerializeField] private string keyEldranHint     = "EVT_08";

    [Header("SpeechBubble — animaciones y caras")]
    [SerializeField] private string     animCompanionAlert  = "Question01";
    [SerializeField] private NPCEmotion faceCompanionAlert  = NPCEmotion.Scared;
    [SerializeField] private string     animWillSurprise    = "Fear01";
    [SerializeField] private NPCEmotion faceWillSurprise    = NPCEmotion.Surprised;
    [SerializeField] private string     animEldranHint      = "Cheer01";
    [SerializeField] private NPCEmotion faceEldranHint      = NPCEmotion.Happy;
    [SerializeField] private string     animWillSuccess     = "Cheer01";
    [SerializeField] private NPCEmotion faceWillSuccess     = NPCEmotion.Happy;
    [SerializeField] private string     animWillAfter       = "Question01";
    [SerializeField] private NPCEmotion faceWillAfter       = NPCEmotion.Thinking;

    // Estado
    private SlowMotionFireProjectile _activeProjectile;
    private bool                     _collisionTriggered;
    private bool                     _panicSuccess;
    private bool                     _sequenceFailed;
    private Transform                _collisionPointHelper;
    private CharacterController      _willCC;
    private NPCEmotionController     _companionEmotion;
    private NPCEmotionController     _willEmotion;

    void Awake()
    {
        // Garantizar que willTransform apunta al Player real (tag "Player")
        if (willTransform == null || !willTransform.CompareTag("Player"))
        {
            var playerGO = GameObject.FindWithTag("Player");
            if (playerGO != null)
                willTransform = playerGO.transform;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            else
                Debug.LogWarning("[StarAwakeningSequencer] No se encontró ningún GO con tag 'Player'.");
#endif
        }

        if (willTransform != null)
        {
            _willCC      = willTransform.GetComponent<CharacterController>();
            _willEmotion = willTransform.GetComponentInChildren<NPCEmotionController>();
        }
        if (companionTransform != null)
            _companionEmotion = companionTransform.GetComponentInChildren<NPCEmotionController>();

        var helperGO = new GameObject("__CollisionPoint") { hideFlags = HideFlags.HideAndDontSave };
        _collisionPointHelper = helperGO.transform;

        DefaultNarrativeSignals.EnsureInstance().OnCustom(signalIn,
            () => StartCoroutine(Co_Sequence()));
    }

    private string Loc(string key) => LocalizationManager.Instance != null
        ? LocalizationManager.Instance.Get(key, key)
        : key;

    void OnDestroy()
    {
        Cleanup();
        if (_collisionPointHelper != null)
            Destroy(_collisionPointHelper.gameObject);
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    private IEnumerator Co_Sequence()
    {
        _collisionTriggered = false;
        _panicSuccess       = false;
        _sequenceFailed     = false;

        var musicRule = audioProfile?.GetSequenceRule(sequenceMusicId);

        actionManager.PushMode(ActionMode.Cinematic);
        cinematicCamera.Activate();

        // FASE 0: Música + primer plano de Eldran con temblor (EVT_AWAKEN_01)
        // Will mira a Eldran mientras Eldran lanza la advertencia
        if (willTransform != null && companionTransform != null)
            FaceTarget(willTransform, companionTransform.position);

        yield return Co_PreDialogue();

        // FASE 1: Slow-motion + spawn proyectil
        Time.timeScale = slowMotionScale;

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        // Pantalla en negro: teleportar a Will al anchor para que los planos siempre encajen
        if (willAnchor != null && willTransform != null)
        {
            if (_willCC) _willCC.enabled = false;
            willTransform.SetPositionAndRotation(willAnchor.position, willAnchor.rotation);
            if (_willCC) _willCC.enabled = true;
        }

        Transform launchTarget = projectileTarget != null ? projectileTarget : willTransform;

        Vector3 spawnPos = projectileSpawnPoint.position;
        if (projectileSpawnDistance > 0f && launchTarget != null)
        {
            Vector3 dir = (projectileSpawnPoint.position - launchTarget.position);
            dir.y = 0f;
            if (dir.sqrMagnitude > 0.001f)
                spawnPos = launchTarget.position + dir.normalized * projectileSpawnDistance;
        }

        Quaternion spawnRot = projectileSpawnPoint.rotation;
        if (launchTarget != null)
        {
            Vector3 toTarget = launchTarget.position - spawnPos;
            if (toTarget.sqrMagnitude > 0.001f)
                spawnRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        }

        _activeProjectile = Instantiate(incomingProjectilePrefab, spawnPos, spawnRot);
        _activeProjectile.Launch(launchTarget);
        _activeProjectile.OnHitByPlayerFireball += OnPhysicsCollision;

        // Corte mientras la pantalla está negra: Will de perfil, listo para ver el giro
        cinematicCamera.Cut(camShotWillProfile);

        yield return new WaitForSecondsRealtime(holdOnBlackDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeFromBlackDuration, fadeIn: false);

        FeedbackService.CameraShake(0.08f, 0.25f);

        // FASE 2: Will se gira hacia el proyectil — la cámara lo captura de perfil (EVT_AWAKEN_02)
        StartCoroutine(Co_TurnTowards(willTransform, _activeProjectile.transform.position, willTurnDuration));
        yield return new WaitForSecondsRealtime(willTurnDuration * 0.5f);

        if (faceWillSurprise != NPCEmotion.None)
            _willEmotion?.SetEmotion(faceWillSurprise);

        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillSurprise),
            duration: 1.2f, emphasis: true, animTrigger: animWillSurprise);

        yield return new WaitForSecondsRealtime(willReactionDuration);

        // Barrido suave de Will al proyectil; la cámara sube/barre para mostrarlo
        if (camShotProjectile != null)
            yield return cinematicCamera.MoveTo(camShotProjectile, projectileRevealDuration * 0.4f);

        yield return new WaitForSecondsRealtime(projectileRevealDuration * 0.6f);

        // FASE 3: Encuadre two-shot — proyectil acercándose, Will preparado
        // El proyectil sigue moviéndose en cámara lenta para que el jugador lo vea venir
        cinematicCamera.Cut(camShotTwoShot);

        if (companionTransform != null)
            StartCoroutine(Co_EldranHintDelayed(0.3f));

        // Deshabilitar el spawner para que los botones del panic input no lancen proyectiles reales
        // (el botón X del panic input es el mismo que el de magia)
        if (playerSpawner != null) playerSpawner.enabled = false;

        if (panicInputUI == null)
            panicInputUI = PanicInputUI.GetOrCreate(panicButtonSprite);
        panicInputUI?.Activate(panicInputDetector);
        panicInputDetector.OnSuccess += OnPanicSuccess;
        panicInputDetector.OnFailure += OnPanicFailure;
        panicInputDetector.StartListening();

        // Espera: jugador completa el panic input o falla
        yield return new WaitUntil(() => _collisionTriggered || _sequenceFailed);
        if (_sequenceFailed) yield break;

        // FASE 4: Explosión — volumen de shock al máximo, shake y vuelta del tiempo
        shockEffects.HoldAt(1f);
        FeedbackService.CameraShake(0.35f, 0.5f);
        FeedbackService.ScreenFlash(new Color(1f, 0.6f, 0.2f, 1f), 0.25f);

        yield return Co_ReturnTime();

        // FASE 5: Primer plano de Will tras la explosión (el volumen sigue activo — blur dramático)
        if (camShotWillFinal != null)
            cinematicCamera.Cut(camShotWillFinal);

        if (faceWillAfter != NPCEmotion.None)
            _willEmotion?.SetEmotion(faceWillAfter);

        bool willAfterDone = false;
        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillAfter),
            duration: 2.5f,
            onComplete: () => willAfterDone = true,
            animTrigger: animWillAfter);

        yield return new WaitUntil(() => willAfterDone);

        // Fade a negro con el efecto de volumen todavía activo
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        // En negro: pitido y espera antes de arrancar el combate
        shockEffects.PlayTinnitus();
        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(musicRule?.fadeOut ?? 0.5f);

        yield return new WaitForSecondsRealtime(blackScreenDuration);

        // Limpiar y transición al combate
        shockEffects.ForceEnd();
        cinematicCamera.Deactivate();

        if (willAnimator != null)
            willAnimator.SetLayerWeight(willUpperBodyLayer, 0f);

        if (playerSpawner != null) playerSpawner.enabled = true;
        actionManager.PopMode(ActionMode.Cinematic);
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signalOutDone);
    }

    // ── Fases auxiliares ──────────────────────────────────────────────────────

    private IEnumerator Co_PreDialogue()
    {
        if (companionTransform == null) yield break;

        var musicRule = audioProfile?.GetSequenceRule(sequenceMusicId);
        if (musicRule?.music != null && AudioService.Instance != null)
            AudioService.Instance.PlayMusic(musicRule.music, musicRule.fadeIn);

        cinematicCamera.Cut(camShotEldran);
        FeedbackService.CameraShake(0.06f, preDialogueDuration);

        if (faceCompanionAlert != NPCEmotion.None)
            _companionEmotion?.SetEmotion(faceCompanionAlert);

        bool done = false;
        SpeechBubbleUI.Instance.Show(companionTransform, Loc(keyCompanionAlert),
            duration: preDialogueDuration,
            onComplete: () => done = true,
            animTrigger: animCompanionAlert);

        yield return new WaitUntil(() => done);
    }

    private IEnumerator Co_EldranHintDelayed(float delayUnscaled)
    {
        yield return new WaitForSecondsRealtime(delayUnscaled);
        if (companionTransform == null) yield break;

        float hintDuration = panicInputDetector.TimeRemaining > 0f
            ? panicInputDetector.TimeRemaining - 0.3f
            : 2f;

        if (faceEldranHint != NPCEmotion.None)
            _companionEmotion?.SetEmotion(faceEldranHint);

        SpeechBubbleUI.Instance.Show(companionTransform, Loc(keyEldranHint),
            duration: Mathf.Max(1.5f, hintDuration),
            animTrigger: animEldranHint);
    }

    private IEnumerator Co_WillFiresBack()
    {
        // Spawner permanece deshabilitado. SpawnForCinematic funciona como llamada directa,
        // no necesita enabled=true. Re-habilitarlo aquí haría que un botón mantenido pulsado
        // del panic input disparase un MagicProjectile accidental que destruiría el proyectil
        // enemigo antes de que Will pueda lanzar el intencionado.

        if (faceWillSuccess != NPCEmotion.None)
            _willEmotion?.SetEmotion(faceWillSuccess);

        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillSuccess),
            duration: 1.5f, emphasis: true, animTrigger: animWillSuccess);

        if (_activeProjectile != null)
            FaceTarget(willTransform, _activeProjectile.transform.position);

        if (willAnimator != null)
        {
            willAnimator.SetLayerWeight(willUpperBodyLayer, 1f);
            willAnimator.Play(castAnimState, willUpperBodyLayer);
        }

        if (castAnimDelay > 0f)
            yield return new WaitForSeconds(castAnimDelay);
        else
            yield return null;

        Transform castOrigin = willCastOrigin != null ? willCastOrigin : willTransform;
        Vector3 castDir = willTransform.forward;

        if (_activeProjectile != null)
        {
            Vector3 toProjectile = _activeProjectile.transform.position - castOrigin.position;
            toProjectile.y = 0f; // horizontal: evita que el fireball suba hacia el proyectil
            if (toProjectile.sqrMagnitude > 0.001f)
            {
                castDir = toProjectile.normalized;
                FaceTarget(willTransform, _activeProjectile.transform.position);
            }
        }

        GameObject fireball = playerSpawner?.SpawnForCinematic(
            castSlot, cinematicSpellFallback, willCastOrigin, castDir);

        if (fireball != null && fireball.TryGetComponent<Rigidbody>(out var fireballRb) && !fireballRb.isKinematic)
        {
            float spd = fireballRb.linearVelocity.magnitude;
            if (spd < 0.1f) spd = 10f;
            fireballRb.linearVelocity = castDir * spd;
        }

        // Esperar a que los dos proyectiles choquen (por proximidad).
        // El proyectil enemigo sigue moviéndose hacia Will y el fireball hacia él,
        // así que se acercan el uno al otro. El umbral de 1.5 m es generoso para
        // compensar que en cámara lenta la física no actualiza tan fino.
        float elapsed = 0f;
        while (elapsed < collisionWaitUnscaled && !_collisionTriggered)
        {
            if (fireball != null && _activeProjectile != null)
            {
                float dist = Vector3.Distance(fireball.transform.position,
                                              _activeProjectile.transform.position);
                if (dist < 1.5f)
                {
                    _collisionPointHelper.position =
                        (fireball.transform.position + _activeProjectile.transform.position) * 0.5f;
                    TriggerExplosion();
                    break;
                }
            }
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        if (!_collisionTriggered)
            TriggerExplosion();

        if (fireball != null) Destroy(fireball);
    }

    private IEnumerator Co_FailedSequence()
    {
        if (playerSpawner != null) playerSpawner.enabled = true;

        var musicRule = audioProfile?.GetSequenceRule(sequenceMusicId);

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            Destroy(_activeProjectile.gameObject);
            _activeProjectile = null;
        }

        cinematicCamera.Deactivate();
        Time.timeScale = 1f;

        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(musicRule?.fadeOut ?? 0.5f);

        _sequenceFailed = true;
        actionManager.PopMode(ActionMode.Cinematic);

        yield return new WaitForSecondsRealtime(holdOnBlackDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeFromBlackDuration, fadeIn: false);

        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signalOutFail);
    }

    private IEnumerator Co_ReturnTime()
    {
        float start   = Time.timeScale;
        float elapsed = 0f;
        while (elapsed < timeReturnDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            Time.timeScale = Mathf.Lerp(start, 1f, elapsed / timeReturnDuration);
            yield return null;
        }
        Time.timeScale = 1f;
    }

    /// Giro suave de un personaje hacia una posición en el plano horizontal.
    private static IEnumerator Co_TurnTowards(Transform character, Vector3 targetPos, float duration)
    {
        Vector3 dir = targetPos - character.position;
        dir.y = 0f;
        if (dir.sqrMagnitude < 0.001f) yield break;

        Quaternion startRot  = character.rotation;
        Quaternion targetRot = Quaternion.LookRotation(dir, Vector3.up);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            character.rotation = Quaternion.Slerp(startRot, targetRot, elapsed / duration);
            yield return null;
        }
        character.rotation = targetRot;
    }

    // ── Callbacks ─────────────────────────────────────────────────────────────

    private void OnPanicSuccess()
    {
        _panicSuccess = true;
        CleanupPanicCallbacks();
        StartCoroutine(Co_WillFiresBack());
    }

    private void OnPanicFailure()
    {
        _panicSuccess = false;
        CleanupPanicCallbacks();
        StartCoroutine(Co_FailedSequence());
    }

    private void OnPhysicsCollision()
    {
        if (_activeProjectile != null)
            _collisionPointHelper.position = _activeProjectile.transform.position;
        _activeProjectile = null;
        TriggerExplosion();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TriggerExplosion()
    {
        if (_collisionTriggered) return;
        _collisionTriggered = true;

        Vector3 pos = _collisionPointHelper != null
            ? _collisionPointHelper.position
            : (willCastOrigin != null ? willCastOrigin.position + willTransform.forward * 3f : Vector3.zero);

        if (explosionVFX != null)
            Destroy(Instantiate(explosionVFX, pos, Quaternion.identity), 3f);

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            // ForceCollide dispara el collisionVFX del propio proyectil y lo destruye
            _activeProjectile.ForceCollide();
            _activeProjectile = null;
        }
    }

    private static void FaceTarget(Transform character, Vector3 worldPos)
    {
        Vector3 dir = worldPos - character.position;
        dir.y = 0f;
        if (dir.sqrMagnitude > 0.001f)
            character.rotation = Quaternion.LookRotation(dir, Vector3.up);
    }

    private void CleanupPanicCallbacks()
    {
        panicInputDetector.OnSuccess -= OnPanicSuccess;
        panicInputDetector.OnFailure -= OnPanicFailure;
    }

    private void Cleanup()
    {
        Time.timeScale = 1f;
        cinematicCamera?.Deactivate();
        if (playerSpawner != null) playerSpawner.enabled = true;
        _companionEmotion?.ForceReset();
        _willEmotion?.ForceReset();

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            if (_activeProjectile.gameObject != null)
                Destroy(_activeProjectile.gameObject);
        }

        panicInputDetector?.StopListening();
        CleanupPanicCallbacks();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular secuencia")]
    void SimulateSequence() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signalIn);
#endif
}
