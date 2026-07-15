using System.Collections;
using DG.Tweening;
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

    [Header("Hechizo de Will")]
    [SerializeField] private MagicSpellSO willFireballSpell;
    [SerializeField] private string castAnimState      = "MagicRight";
    [SerializeField] private int    willUpperBodyLayer = 1;

    [Header("Proyectil enemigo")]
    [SerializeField] private SlowMotionFireProjectile incomingProjectilePrefab;
    [SerializeField] private Transform  projectileSpawnPoint;
    [SerializeField] private GameObject explosionVFX;

    [Header("Música")]
    [SerializeField] private AudioGraphProfile audioProfile;
    [SerializeField] private string sequenceMusicId = "AWAKEN";

    [Header("Cámara")]
    [SerializeField] private vThirdPersonCamera thirdPersonCamera;
    [SerializeField] private float twoShotDistance = 3.5f;   // plano dos personajes
    [SerializeField] private float twoShotHeight   = 1.4f;
    [SerializeField] private float twoShotPitch    = 12f;
    [SerializeField] private float closeCamDistance = 1.5f;  // primer plano Will
    [SerializeField] private float closeCamHeight   = 1.6f;
    [SerializeField] private float closeCamPitch    = 12f;
    [SerializeField] private float camZoomDuration  = 0.6f;

    [Header("Sistemas")]
    [SerializeField] private PanicInputDetector     panicInputDetector;
    [SerializeField] private PanicInputUI           panicInputUI;       // Opcional: se auto-crea si es null
    [SerializeField] private Sprite                 panicButtonSprite;
    [SerializeField] private ShockEffectsController shockEffects;

    [Header("Timings")]
    [SerializeField] private float preDialogueDuration   = 2f;
    [SerializeField] private float slowMotionScale       = 0.2f;
    [SerializeField] private float fadeToBlackDuration   = 0.2f;
    [SerializeField] private float holdOnBlackDuration   = 0.4f;
    [SerializeField] private float fadeFromBlackDuration = 0.35f;
    [SerializeField] [Range(0f,1f)] private float castReleaseNormTime = 0.55f; // punto del clip donde la mano suelta el hechizo
    [SerializeField] private float castTimeoutUnscaled   = 5f;   // safety: máximo tiempo de espera al punto de lanzamiento
    [SerializeField] private float castDelayUnscaled     = 0.8f; // fallback si no hay animator
    [SerializeField] private float collisionWaitUnscaled = 1.8f;
    [SerializeField] private float timeReturnDuration    = 0.9f;
    [SerializeField] private float postShockDuration     = 2.5f;

    [Header("SpeechBubble — claves de localización")]
    [SerializeField] private string keyCompanionAlert = "EVT_AWAKEN_01";
    [SerializeField] private string keyWillSurprise   = "EVT_AWAKEN_02";
    [SerializeField] private string keyWillSuccess    = "EVT_AWAKEN_03";
    [SerializeField] private string keyWillAfter      = "EVT_AWAKEN_05";
    [SerializeField] private string keyEldranHint     = "EVT_08";

    [Header("SpeechBubble — animaciones")]
    [SerializeField] private string animCompanionAlert = "Question01";
    [SerializeField] private string animWillSurprise   = "Fear01";
    [SerializeField] private string animEldranHint     = "Cheer01";
    [SerializeField] private string animWillSuccess    = "Cheer01";
    [SerializeField] private string animWillAfter      = "Question01";

    // Estado
    private SlowMotionFireProjectile _activeProjectile;
    private GameObject               _willFireball;
    private bool                     _collisionTriggered;
    private bool                     _panicSuccess;
    private bool                     _sequenceFailed;    // true → Co_Sequence hace yield break

    // Cámara
    private Transform _midpointHelper;   // sigue el punto medio entre los dos proyectiles
    private float     _savedCamDistance;
    private float     _savedCamHeight;

    private LayerMask _noDamageMask;

    void Awake()
    {
        _noDamageMask = 0;

        if (thirdPersonCamera != null)
        {
            _savedCamDistance = thirdPersonCamera.defaultDistance;
            _savedCamHeight   = thirdPersonCamera.height;
        }

        var helperGO = new GameObject("__CamMidpoint") { hideFlags = HideFlags.HideAndDontSave };
        _midpointHelper = helperGO.transform;

        DefaultNarrativeSignals.EnsureInstance().OnCustom("AWAKEN_START",
            () => StartCoroutine(Co_Sequence()));
    }

    private string Loc(string key) => LocalizationManager.Instance != null
        ? LocalizationManager.Instance.Get(key, key)
        : key;

    void OnDestroy()
    {
        Cleanup();
        if (_midpointHelper != null)
            Destroy(_midpointHelper.gameObject);
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    private IEnumerator Co_Sequence()
    {
        _collisionTriggered = false;
        _panicSuccess       = false;
        _sequenceFailed     = false;

        var musicRule = audioProfile?.GetSequenceRule(sequenceMusicId);

        // FASE 0: Diálogo previo
        yield return Co_PreDialogue();

        // FASE 1: Slow-motion + proyectil entrante
        actionManager.PushMode(ActionMode.Cinematic);
        Time.timeScale = slowMotionScale;

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        _activeProjectile = Instantiate(incomingProjectilePrefab,
            projectileSpawnPoint.position, projectileSpawnPoint.rotation);
        _activeProjectile.Launch(willTransform);
        _activeProjectile.OnHitByPlayerFireball += OnPhysicsCollision;

        thirdPersonCamera?.SetLockTarget(_activeProjectile.transform);

        if (musicRule?.music != null && AudioService.Instance != null)
            AudioService.Instance.PlayMusic(musicRule.music, musicRule.fadeIn);

        yield return new WaitForSecondsRealtime(holdOnBlackDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeFromBlackDuration, fadeIn: false);

        FeedbackService.CameraShake(0.08f, 0.25f);

        // FASE 2: Will reacciona + Eldran da instrucciones + panic input
        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillSurprise),
            duration: 1.2f, emphasis: true, animTrigger: animWillSurprise);

        if (companionTransform != null)
            StartCoroutine(Co_EldranHintDelayed(1.4f));

        if (panicInputUI == null)
            panicInputUI = PanicInputUI.GetOrCreate(panicButtonSprite);
        panicInputUI?.Activate(panicInputDetector);
        panicInputDetector.OnSuccess += OnPanicSuccess;
        panicInputDetector.OnFailure += OnPanicFailure;
        panicInputDetector.StartListening();

        // Espera: el jugador pulsa X (éxito→Co_WillFiresBack) o timeout (fallo→Co_FailedSequence)
        yield return new WaitUntil(() => _collisionTriggered || _sequenceFailed);
        if (_sequenceFailed) yield break;   // Co_FailedSequence ya limpió todo

        // FASE 4: Shock + vuelta del tiempo — cámara apunta al midpoint (explosión en centro)
        shockEffects.PlayShockSequence();
        FeedbackService.CameraShake(0.35f, 0.5f);
        FeedbackService.ScreenFlash(new Color(1f, 0.6f, 0.2f, 1f), 0.25f);

        yield return Co_ReturnTime();

        // FASE 5: Primer plano en Will para su última frase
        yield return new WaitForSecondsRealtime(0.4f);

        // Salir del lock-on y apuntar la cámara a la CARA de Will (yaw+180 = cámara en frente)
        thirdPersonCamera?.ClearLockTarget();
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetAngles(willTransform.eulerAngles.y + 180f, closeCamPitch);

            DOTween.To(() => thirdPersonCamera.defaultDistance,
                       x  => thirdPersonCamera.defaultDistance = x,
                       closeCamDistance, camZoomDuration).SetUpdate(true);
            DOTween.To(() => thirdPersonCamera.height,
                       x  => thirdPersonCamera.height = x,
                       closeCamHeight, camZoomDuration).SetUpdate(true);
        }

        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillAfter),
            duration: postShockDuration, animTrigger: animWillAfter);

        yield return new WaitForSecondsRealtime(postShockDuration);

        // Restaurar cámara y pasar al combate
        if (thirdPersonCamera != null && _savedCamDistance > 0f)
        {
            DOTween.To(() => thirdPersonCamera.defaultDistance,
                       x  => thirdPersonCamera.defaultDistance = x,
                       _savedCamDistance, camZoomDuration).SetUpdate(true);
            DOTween.To(() => thirdPersonCamera.height,
                       x  => thirdPersonCamera.height = x,
                       _savedCamHeight, camZoomDuration).SetUpdate(true);
        }
        thirdPersonCamera?.ClearLockTarget();

        if (willAnimator != null)
            willAnimator.SetLayerWeight(willUpperBodyLayer, 0f);

        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(musicRule?.fadeOut ?? 0.8f);

        actionManager.PopMode(ActionMode.Cinematic);
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom("AWAKEN_DONE");
    }

    // ── Fases auxiliares ──────────────────────────────────────────────────────

    private IEnumerator Co_PreDialogue()
    {
        if (companionTransform == null) yield break;

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

        SpeechBubbleUI.Instance.Show(companionTransform, Loc(keyEldranHint),
            duration: Mathf.Max(1.5f, hintDuration),
            animTrigger: animEldranHint);
    }

    private IEnumerator Co_WillFiresBack()
    {
        SpeechBubbleUI.Instance.Show(willTransform, Loc(keyWillSuccess),
            duration: 1.5f, emphasis: true, animTrigger: animWillSuccess);

        if (_activeProjectile != null)
            FaceTarget(willTransform, _activeProjectile.transform.position);

        if (willAnimator != null)
        {
            willAnimator.SetLayerWeight(willUpperBodyLayer, 1f);
            willAnimator.Play(castAnimState, willUpperBodyLayer);
        }

        // Plano dos personajes: cámara lateral fija (90° izq del eje de ataque)
        // → proyectil entra por la izquierda, Will queda a la derecha, explosión en el centro
        thirdPersonCamera?.ClearLockTarget();
        if (thirdPersonCamera != null)
        {
            float shotYaw = willTransform.eulerAngles.y - 90f;
            thirdPersonCamera.SetAngles(shotYaw, twoShotPitch);
            DOTween.To(() => thirdPersonCamera.defaultDistance,
                       x  => thirdPersonCamera.defaultDistance = x,
                       twoShotDistance, camZoomDuration).SetUpdate(true);
            DOTween.To(() => thirdPersonCamera.height,
                       x  => thirdPersonCamera.height = x,
                       twoShotHeight, camZoomDuration).SetUpdate(true);
        }

        // Esperar al punto de la animación donde Will suelta el hechizo
        if (willAnimator != null)
        {
            yield return null; // un frame para que el animator refleje el Play()
            int castHash = Animator.StringToHash(castAnimState);
            float waitElapsed = 0f;
            while (waitElapsed < castTimeoutUnscaled)
            {
                waitElapsed += Time.unscaledDeltaTime;
                var info = willAnimator.GetCurrentAnimatorStateInfo(willUpperBodyLayer);
                if (info.shortNameHash == castHash && info.normalizedTime >= castReleaseNormTime)
                    break;
                yield return null;
            }
        }
        else
        {
            yield return new WaitForSecondsRealtime(castDelayUnscaled);
        }

        Vector3 targetPos = _activeProjectile != null
            ? _activeProjectile.transform.position
            : willTransform.position + willTransform.forward * 6f;

        Vector3 dir = (targetPos - willCastOrigin.position).normalized;
        if (dir.sqrMagnitude < 0.001f) dir = willTransform.forward;

        _willFireball = SpawnWillFireball(willCastOrigin.position, dir);

        // Mantener midpoint actualizado como referencia de posición para la explosión
        float elapsed = 0f;
        while (elapsed < collisionWaitUnscaled && !_collisionTriggered)
        {
            elapsed += Time.unscaledDeltaTime;
            if (_activeProjectile != null && _willFireball != null)
                _midpointHelper.position =
                    (_activeProjectile.transform.position + _willFireball.transform.position) * 0.5f;
            yield return null;
        }

        if (!_collisionTriggered)
            TriggerExplosion();
    }

    /// Se lanza cuando el jugador no pulsa X a tiempo.
    /// Fade a negro, limpia la secuencia y devuelve al jugador al punto previo.
    private IEnumerator Co_FailedSequence()
    {
        var musicRule = audioProfile?.GetSequenceRule(sequenceMusicId);

        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            Destroy(_activeProjectile.gameObject);
            _activeProjectile = null;
        }
        thirdPersonCamera?.ClearLockTarget();
        Time.timeScale = 1f;

        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(musicRule?.fadeOut ?? 0.5f);

        // Señalizar a Co_Sequence que salga y devolver el control al jugador
        _sequenceFailed = true;
        actionManager.PopMode(ActionMode.Cinematic);

        yield return new WaitForSecondsRealtime(holdOnBlackDuration);
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeFromBlackDuration, fadeIn: false);

        // El grafo narrativo resetea el nodo de trigger de Eldran para que el jugador pueda
        // hablar con él de nuevo y relanzar la secuencia
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom("AWAKEN_FAILED");
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
        _activeProjectile = null;
        TriggerExplosion();
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void TriggerExplosion()
    {
        if (_collisionTriggered) return;
        _collisionTriggered = true;

        // Calcular posición de la explosión
        Vector3 pos;
        if (_activeProjectile != null && _willFireball != null)
            pos = (_activeProjectile.transform.position + _willFireball.transform.position) * 0.5f;
        else if (_midpointHelper != null)
            pos = _midpointHelper.position;  // última posición conocida del midpoint
        else
            pos = willCastOrigin.position + willTransform.forward * 3f;

        if (explosionVFX != null)
            Destroy(Instantiate(explosionVFX, pos, Quaternion.identity), 3f);

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            Destroy(_activeProjectile.gameObject);
            _activeProjectile = null;
        }

        if (_willFireball != null)
        {
            Destroy(_willFireball);
            _willFireball = null;
        }
        // NO limpiar lock de cámara aquí — Co_Sequence transicionará a Will en la FASE 5
    }

    private GameObject SpawnWillFireball(Vector3 position, Vector3 direction)
    {
        if (!willFireballSpell || !willFireballSpell.prefab) return null;

        direction = direction.normalized;
        Quaternion rot = direction.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(direction, Vector3.up) *
              Quaternion.Euler(willFireballSpell.visualRotationOffsetEuler)
            : Quaternion.identity;

        Vector3 spawnPos = position + direction * willFireballSpell.forwardOffset;
        var go = Instantiate(willFireballSpell.prefab, spawnPos, rot);

        if (willFireballSpell.spawnVFX != null)
        {
            float vfxTtl = willFireballSpell.vfxLifetime > 0f ? willFireballSpell.vfxLifetime : 3f;
            Destroy(Instantiate(willFireballSpell.spawnVFX, spawnPos, rot), vfxTtl);
        }

        if (!string.IsNullOrEmpty(willFireballSpell.castSFXKey) && AudioService.Instance != null)
            AudioService.Instance.PlaySFX(willFireballSpell.castSFXKey);

        if (!go.TryGetComponent<MagicProjectile>(out var mp)) return go;

        var cfg = new MagicProjectile.ProjectileConfig
        {
            damage          = willFireballSpell.damage,
            aoeRadius       = willFireballSpell.aoeRadius,
            knockbackForce  = 0f,
            hitLayers       = _noDamageMask,
            collisionLayers = _noDamageMask,
            destroyOnHit    = false,
            lifeTime        = collisionWaitUnscaled + 2f,
            maxRange        = willFireballSpell.maxRange,
            initialSpeed    = willFireballSpell.initialSpeed,
            useGravity      = false,
            impactVFX       = willFireballSpell.impactVFX,
            despawnVFX      = willFireballSpell.despawnVFX,
            vfxLifetime     = willFireballSpell.vfxLifetime,
            impactSFXKey    = willFireballSpell.impactSFXKey,
            element         = willFireballSpell.element
        };

        mp.Configure(cfg, willTransform.gameObject);
        mp.Launch(direction, willFireballSpell.initialSpeed, false);

        return go;
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
        thirdPersonCamera?.ClearLockTarget();
        if (thirdPersonCamera != null && _savedCamDistance > 0f)
        {
            thirdPersonCamera.defaultDistance = _savedCamDistance;
            thirdPersonCamera.height          = _savedCamHeight;
        }

        if (_activeProjectile != null)
        {
            _activeProjectile.OnHitByPlayerFireball -= OnPhysicsCollision;
            if (_activeProjectile.gameObject != null)
                Destroy(_activeProjectile.gameObject);
        }

        if (_willFireball != null)
            Destroy(_willFireball);

        panicInputDetector?.StopListening();
        CleanupPanicCallbacks();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Simular secuencia")]
    void SimulateSequence() =>
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom("AWAKEN_START");
#endif
}
