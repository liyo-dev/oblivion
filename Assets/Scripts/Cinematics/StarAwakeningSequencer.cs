using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia del Despertar de la Estrella.
/// Señal de entrada: "AWAKEN_START".
/// Señal de salida (éxito): "AWAKEN_DONE"  → arranca el combate.
/// Señal de salida (fallo): "AWAKEN_FAILED" → grafo vuelve al diálogo con Eldran.
[DisallowMultipleComponent]
public class StarAwakeningSequencer : CinematicSequencerBase
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

    [Header("Cámara — planos")]
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

    [Header("Señales narrativas — salidas")]
    [SerializeField] private string signalOutDone = "AWAKEN_DONE";
    [SerializeField] private string signalOutFail = "AWAKEN_FAILED";

    // ── Fase 0 — Eldran avisa ─────────────────────────────────────────────────

    [Header("Fase 0 — Eldran avisa")]
    [SerializeField] private string     keyCompanionAlert  = "EVT_AWAKEN_01";
    [SerializeField] private NPCEmotion faceCompanionAlert = NPCEmotion.Scared;
    [SerializeField] private string     animCompanionAlert = "Question01";
    [SerializeField] private float      preDialogueDuration = 2f;

    // ── Fase 1 — Will reacciona al proyectil ─────────────────────────────────

    [Header("Fase 1 — Will reacciona al proyectil")]
    [SerializeField] private string     keyWillSurprise    = "EVT_AWAKEN_02";
    [SerializeField] private NPCEmotion faceWillSurprise   = NPCEmotion.Surprised;
    [SerializeField] private string     animWillSurprise   = "Fear01";
    [SerializeField] private float      willSurpriseDuration = 1.2f;

    // ── Fase 2 — Hint de Eldran + Panic input ────────────────────────────────

    [Header("Fase 2 — Hint de Eldran + Panic input")]
    [SerializeField] private string     keyEldranHint  = "EVT_08";
    [SerializeField] private NPCEmotion faceEldranHint = NPCEmotion.Happy;
    [SerializeField] private string     animEldranHint = "Cheer01";

    // ── Fase 3 — Will contraataca (éxito) ────────────────────────────────────

    [Header("Fase 3 — Will contraataca (éxito)")]
    [SerializeField] private string     keyWillSuccess    = "EVT_AWAKEN_03";
    [SerializeField] private NPCEmotion faceWillSuccess   = NPCEmotion.Happy;
    [SerializeField] private string     animWillSuccess   = "Cheer01";
    [SerializeField] private float      willSuccessDuration = 1.5f;

    // ── Fase 4 — Aftermath ───────────────────────────────────────────────────

    [Header("Fase 4 — Aftermath")]
    [SerializeField] private string     keyWillAfter    = "EVT_AWAKEN_05";
    [SerializeField] private NPCEmotion faceWillAfter   = NPCEmotion.Thinking;
    [SerializeField] private string     animWillAfter   = "Question01";
    [SerializeField] private float      willAfterDuration = 2.5f;

    // ── Timings generales ─────────────────────────────────────────────────────

    [Header("Timings generales")]
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

    // ── Estado ────────────────────────────────────────────────────────────────

    private SlowMotionFireProjectile _activeProjectile;
    private bool                     _collisionTriggered;
    private bool                     _sequenceFailed;
    private Transform                _collisionPointHelper;
    private CharacterController      _willCC;
    private NPCEmotionController     _companionEmotion;
    private NPCEmotionController     _willEmotion;

    protected override void Awake()
    {
        base.Awake();

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
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Cleanup();
        if (_collisionPointHelper != null)
            Destroy(_collisionPointHelper.gameObject);
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    protected override IEnumerator Co_Sequence()
    {
        _collisionTriggered = false;
        _sequenceFailed     = false;

        yield return Co_BeginCinematicWithTransition(camShotEldran);

        // ── Fase 0: Música + Eldran lanza la advertencia ──────────────────────
        if (willTransform != null && companionTransform != null)
            FaceTarget(willTransform, companionTransform.position);

        yield return Co_PreDialogue();

        // ── Fase 1: Slow-motion + spawn proyectil + Will reacciona ────────────
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
        _cinematicCamera.Cut(camShotWillProfile);

        yield return new WaitForSecondsRealtime(holdOnBlackDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeFromBlackDuration, fadeIn: false);

        FeedbackService.CameraShake(0.08f, 0.25f);

        // Will se gira hacia el proyectil (EVT_AWAKEN_02)
        StartCoroutine(Co_TurnTowards(willTransform, _activeProjectile.transform.position, willTurnDuration));
        yield return new WaitForSecondsRealtime(willTurnDuration * 0.5f);

        if (faceWillSurprise != NPCEmotion.None)
            _willEmotion?.SetEmotion(faceWillSurprise);

        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillSurprise),
            duration: willSurpriseDuration, emphasis: true, animTrigger: animWillSurprise);

        yield return new WaitForSecondsRealtime(willReactionDuration);

        if (camShotProjectile != null)
            yield return _cinematicCamera.MoveTo(camShotProjectile, projectileRevealDuration * 0.4f);

        yield return new WaitForSecondsRealtime(projectileRevealDuration * 0.6f);

        // ── Fase 2: Two-shot + hint de Eldran + Panic input ───────────────────
        _cinematicCamera.Cut(camShotTwoShot);

        if (companionTransform != null)
            StartCoroutine(Co_EldranHintDelayed(0.3f));

        if (playerSpawner != null) playerSpawner.enabled = false;

        if (panicInputUI == null)
            panicInputUI = PanicInputUI.GetOrCreate(panicButtonSprite);
        panicInputUI?.Activate(panicInputDetector);
        panicInputDetector.OnSuccess += OnPanicSuccess;
        panicInputDetector.OnFailure += OnPanicFailure;
        panicInputDetector.StartListening();

        yield return new WaitUntil(() => _collisionTriggered || _sequenceFailed);
        if (_sequenceFailed) yield break;

        // ── Fase 3 (éxito): Explosión + vuelta del tiempo ────────────────────
        shockEffects.HoldAt(1f);
        FeedbackService.CameraShake(0.35f, 0.5f);
        FeedbackService.ScreenFlash(new Color(1f, 0.6f, 0.2f, 1f), 0.25f);

        yield return Co_ReturnTime();

        // ── Fase 4: Aftermath — primer plano de Will ──────────────────────────
        if (camShotWillFinal != null)
            _cinematicCamera.Cut(camShotWillFinal);

        if (faceWillAfter != NPCEmotion.None)
            _willEmotion?.SetEmotion(faceWillAfter);

        bool willAfterDone = false;
        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillAfter),
            duration: willAfterDuration,
            onComplete: () => willAfterDone = true,
            animTrigger: animWillAfter);

        yield return new WaitUntil(() => willAfterDone);

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        shockEffects.PlayTinnitus();
        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(MusicRule?.fadeOut ?? 0.5f);

        yield return new WaitForSecondsRealtime(blackScreenDuration);

        shockEffects.ForceEnd();
        if (willAnimator != null)
            willAnimator.SetLayerWeight(willUpperBodyLayer, 0f);

        if (playerSpawner != null) playerSpawner.enabled = true;
        EndCinematic();
        RaiseSignal(signalOutDone);
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fases auxiliares
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_PreDialogue()
    {
        if (companionTransform == null) yield break;

        PlaySequenceMusic();
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
            duration: willSuccessDuration, emphasis: true, animTrigger: animWillSuccess);

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
            toProjectile.y = 0f;
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

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            Destroy(_activeProjectile.gameObject);
            _activeProjectile = null;
        }

        Time.timeScale = 1f;
        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(MusicRule?.fadeOut ?? 0.5f);

        _sequenceFailed = true;
        EndCinematic();

        yield return new WaitForSecondsRealtime(holdOnBlackDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeFromBlackDuration, fadeIn: false);

        RaiseSignal(signalOutFail);
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

    // ══════════════════════════════════════════════════════════════════════════
    // Callbacks
    // ══════════════════════════════════════════════════════════════════════════

    private void OnPanicSuccess()
    {
        CleanupPanicCallbacks();
        StartCoroutine(Co_WillFiresBack());
    }

    private void OnPanicFailure()
    {
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

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void TriggerExplosion()
    {
        if (_collisionTriggered) return;
        _collisionTriggered = true;

        Vector3 pos = _collisionPointHelper != null
            ? _collisionPointHelper.position
            : (willCastOrigin != null ? willCastOrigin.position + willTransform.forward * 3f : Vector3.zero);

        if (explosionVFX != null)
            VfxPoolService.Instance.Play(explosionVFX, pos, Quaternion.identity, 3f);

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            _activeProjectile.ForceCollide();
            _activeProjectile = null;
        }
    }

    private void CleanupPanicCallbacks()
    {
        panicInputDetector.OnSuccess -= OnPanicSuccess;
        panicInputDetector.OnFailure -= OnPanicFailure;
    }

    private void Cleanup()
    {
        Time.timeScale = 1f;
        _cinematicCamera?.Deactivate();
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
}
