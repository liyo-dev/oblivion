using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// <summary>
/// Orquestador del prólogo — el sueño de Will. Sustituye al `DramaticText_Prolog` (solo texto)
/// por una escena real: Mago Oscuro y Will Original ocupando cada lado de la pantalla, con
/// parpadeos, blur y flashes crípticos de la guerra que los enfrentó (ver GDD, "La verdadera
/// historia de Will"). Sin diálogo ni texto — todo el peso es visual y sonoro.
///
/// Señal de entrada: la que se configure en el Inspector (_signalIn de CinematicSequencerBase).
/// Punto de integración: sustituye la llamada actual a
/// `DramaticTextOverlayUI.Instance.Play(DramaticText_Prolog, ...)` — donde antes se disparaba
/// esa llamada, ahora se debe lanzar `DefaultNarrativeSignals.EnsureInstance().RaiseCustom(signalIn)`.
/// Señal de salida: _signalOut → el grafo continúa hacia "La Casa de Will" (GDD escena 2).
///
/// Diseño completo (Materiales / Actores / Fases) documentado en el chat de diseño de esta
/// secuencia; la metodología general está en TDD.md § 10 "Metodología de diseño de secuencias".
[DisallowMultipleComponent]
public class PrologueDreamSequencer : CinematicSequencerBase
{
    // ── Actores ───────────────────────────────────────────────────────────────

    [Header("Actores — instancias ya colocadas en la escena")]
    [Tooltip("Instancia de _MAGO_OSCURO.prefab, encuadrada a la izquierda.")]
    [SerializeField] private GameObject magoOscuroActor;
    [SerializeField] private Animator   magoOscuroAnimator;
    [Tooltip("Nombre del estado del Animator compartido de NPC a reproducir (ej. un idle de conjurar).")]
    [SerializeField] private string     magoOscuroAnimState = "Cast";
    [SerializeField] private float      magoOscuroAnimSpeed = 0.6f;

    [Tooltip("Instancia de _WILL_ORIGINAL.prefab, encuadrada a la derecha.")]
    [SerializeField] private GameObject willOriginalActor;
    [SerializeField] private Animator   willOriginalAnimator;
    [Tooltip("Nombre del estado del Animator compartido de NPC a reproducir (ej. un idle de guardia/esfuerzo).")]
    [SerializeField] private string     willOriginalAnimState = "Guard";

    // ── Cámara ────────────────────────────────────────────────────────────────

    [Header("Cámara")]
    [Tooltip("Plano fijo con el encuadre partido: Mago Oscuro a la izquierda, Will Original a la derecha.")]
    [SerializeField] private Transform camShotDual;
    [Tooltip("Zoom rápido al centro para la colisión de hechizos (Fase 3).")]
    [SerializeField] private Transform camShotCollision;
    [SerializeField] private float     collisionZoomDuration = 0.5f;

    // ── Post-proceso / Shock ─────────────────────────────────────────────────

    [Header("Post-proceso — Volume dedicado")]
    [Tooltip("Motion Blur + Chromatic Aberration + Vignette + Film Grain al máximo. Se controla a tirones, nunca con fade continuo.")]
    [SerializeField] private ShockEffectsController shockEffects;

    [Header("Fase 1 — Parpadeo (Presentación dual)")]
    [SerializeField] private float presentationDuration = 3.7f;
    [SerializeField] private float flickerOnMin  = 0.10f;
    [SerializeField] private float flickerOnMax  = 0.30f;
    [SerializeField] private float flickerOffMin = 0.04f;
    [SerializeField] private float flickerOffMax = 0.12f;
    [SerializeField] private float shockWeightMin = 0.35f;
    [SerializeField] private float shockWeightMax = 0.85f;

    // ── Audio ─────────────────────────────────────────────────────────────────

    [Header("Audio")]
    [Tooltip("AudioSource dedicada al latido de fondo (loop), con su Output routeado al mixer en el Inspector.")]
    [SerializeField] private AudioSource heartbeatSource;
    [Tooltip("Golpes metálicos / gritos lejanos — uno se dispara en cada flash de guerra.")]
    [SerializeField] private AudioClip[] warClashStingers;

    // ── Fase 2 — Flashes de guerra ────────────────────────────────────────────

    [Header("Fase 2 — Flashes de guerra")]
    [Tooltip("Set pieces pre-colocados y desactivados en la escena: silueta de ejército, valle/aldea ardiendo, escudo deteniendo un golpe, etc. Se muestran uno a uno.")]
    [SerializeField] private GameObject[] warFlashVisuals;
    [SerializeField] private float flashOnDuration       = 0.15f;
    [SerializeField] private float backToActorsDuration  = 0.10f;
    [SerializeField] private float flashShakeIntensity   = 0.12f;

    // ── Fase 3 — Colisión ─────────────────────────────────────────────────────

    [Header("Fase 3 — Colisión de hechizos")]
    [Tooltip("VFX de energía clara (ej. Light Orb) — sale desde Will Original.")]
    [SerializeField] private GameObject lightImpactVfx;
    [Tooltip("VFX de energía oscura (ej. Plasma Sphere Cinematic) — sale desde el Mago Oscuro.")]
    [SerializeField] private GameObject darkImpactVfx;
    [SerializeField] private Transform  collisionPoint;
    [SerializeField] private Color      lightFlashColor = new Color(1f, 0.95f, 0.75f, 1f);
    [SerializeField] private Color      darkFlashColor  = new Color(0.25f, 0f, 0.4f, 1f);
    [SerializeField] private float      collisionHoldDuration = 1.2f;

    // ── Fase 4 — Despertar ────────────────────────────────────────────────────

    [Header("Fase 4 — Corte y despertar")]
    [SerializeField] private float fadeToBlackDuration = 0.15f;
    [SerializeField] private float silenceDuration      = 0.3f;

    // ── Estado ────────────────────────────────────────────────────────────────

    private Renderer[] _magoOscuroRenderers;
    private Renderer[] _willOriginalRenderers;
    private Coroutine  _flickerMago;
    private Coroutine  _flickerWill;

    protected override void Awake()
    {
        base.Awake();

        if (magoOscuroActor != null)
            _magoOscuroRenderers = magoOscuroActor.GetComponentsInChildren<Renderer>(true);
        if (willOriginalActor != null)
            _willOriginalRenderers = willOriginalActor.GetComponentsInChildren<Renderer>(true);

        SetActorsVisible(false);
        SetWarFlashVisualsActive(-1); // todos apagados al empezar
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        Cleanup();
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition(camShotDual);

        PlaySequenceMusic();
        heartbeatSource?.Play();
        shockEffects?.PlayTinnitus();

        // ── Fase 1: Presentación dual con parpadeo ───────────────────────────
        SetActorsVisible(true);
        magoOscuroAnimator?.Play(magoOscuroAnimState, 0, 0f);
        if (magoOscuroAnimator != null) magoOscuroAnimator.speed = magoOscuroAnimSpeed;
        willOriginalAnimator?.Play(willOriginalAnimState, 0, 0f);

        _flickerMago = StartCoroutine(Co_FlickerRenderers(_magoOscuroRenderers, presentationDuration));
        _flickerWill = StartCoroutine(Co_FlickerRenderers(_willOriginalRenderers, presentationDuration));
        yield return Co_FlickerShockWeight(presentationDuration);

        StopFlicker(ref _flickerMago);
        StopFlicker(ref _flickerWill);
        SetActorsVisible(true);

        // ── Fase 2: Flashes de guerra ─────────────────────────────────────────
        yield return Co_WarFlashes();

        // ── Fase 3: Colisión de hechizos ──────────────────────────────────────
        yield return Co_Collision();

        // ── Fase 4: Corte a negro y despertar ─────────────────────────────────
        yield return Co_Awaken();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Fases auxiliares
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_WarFlashes()
    {
        if (warFlashVisuals == null || warFlashVisuals.Length == 0) yield break;

        for (int i = 0; i < warFlashVisuals.Length; i++)
        {
            SetWarFlashVisualsActive(i);
            FeedbackService.CameraShake(flashShakeIntensity, flashOnDuration);
            PlayRandomStinger();

            yield return new WaitForSecondsRealtime(flashOnDuration);

            SetWarFlashVisualsActive(-1);
            // Vuelta breve a los actores centrales entre cada corte — "memoria interrumpida".
            yield return new WaitForSecondsRealtime(backToActorsDuration);
        }
    }

    private IEnumerator Co_Collision()
    {
        if (camShotCollision != null)
            yield return _cinematicCamera.MoveTo(camShotCollision, collisionZoomDuration);

        shockEffects?.HoldAt(1f);
        shockEffects?.PlayTinnitus(); // pico del pitido, coincide con el choque
        FeedbackService.CameraShake(0.4f, collisionHoldDuration);

        if (collisionPoint != null)
        {
            if (lightImpactVfx != null)
                VfxPoolService.Instance.Play(lightImpactVfx, collisionPoint.position, Quaternion.identity, collisionHoldDuration);
            if (darkImpactVfx != null)
                VfxPoolService.Instance.Play(darkImpactVfx, collisionPoint.position, Quaternion.identity, collisionHoldDuration);
        }

        FeedbackService.ScreenFlash(lightFlashColor, collisionHoldDuration * 0.4f);
        yield return new WaitForSecondsRealtime(collisionHoldDuration * 0.4f);
        FeedbackService.ScreenFlash(darkFlashColor, collisionHoldDuration * 0.6f);

        yield return new WaitForSecondsRealtime(collisionHoldDuration * 0.6f);
    }

    private IEnumerator Co_Awaken()
    {
        yield return FeedbackService.ScreenFadeAsync(Color.black, fadeToBlackDuration, fadeIn: true);

        // Corte de silencio total: vende el golpe del despertar más que cualquier sonido.
        heartbeatSource?.Stop();
        if (AudioService.Instance != null)
            AudioService.Instance.StopMusic(0.05f);
        shockEffects?.ForceEnd();
        SetActorsVisible(false);

        yield return new WaitForSecondsRealtime(silenceDuration);

        // No hacemos fundido de reveal aquí: igual que StarAwakeningSequencer, el sistema
        // siguiente ("La Casa de Will") gestiona su propia transición/fundido de entrada.
        // La pantalla queda en negro; EndCinematic() + RaiseSignalOut() entregan el control
        // al grafo narrativo, que revela la siguiente escena.
        EndCinematic();
        RaiseSignalOut();
    }

    // ── Parpadeo (Fase 1) ─────────────────────────────────────────────────────

    private IEnumerator Co_FlickerRenderers(Renderer[] renderers, float duration)
    {
        if (renderers == null || renderers.Length == 0) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            SetRenderersEnabled(renderers, true);
            float onTime = Random.Range(flickerOnMin, flickerOnMax);
            yield return new WaitForSecondsRealtime(onTime);
            elapsed += onTime;

            SetRenderersEnabled(renderers, false);
            float offTime = Random.Range(flickerOffMin, flickerOffMax);
            yield return new WaitForSecondsRealtime(offTime);
            elapsed += offTime;
        }
        SetRenderersEnabled(renderers, true);
    }

    private IEnumerator Co_FlickerShockWeight(float duration)
    {
        if (shockEffects == null) { yield return new WaitForSecondsRealtime(duration); yield break; }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            shockEffects.HoldAt(Random.Range(shockWeightMin, shockWeightMax));
            float interval = Random.Range(flickerOnMin, flickerOnMax);
            yield return new WaitForSecondsRealtime(interval);
            elapsed += interval;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Helpers
    // ══════════════════════════════════════════════════════════════════════════

    private void PlayRandomStinger()
    {
        if (warClashStingers == null || warClashStingers.Length == 0) return;
        if (AudioService.Instance == null) return;
        AudioService.Instance.PlaySFX(warClashStingers[Random.Range(0, warClashStingers.Length)]);
    }

    private void SetActorsVisible(bool visible)
    {
        SetRenderersEnabled(_magoOscuroRenderers, visible);
        SetRenderersEnabled(_willOriginalRenderers, visible);
    }

    private static void SetRenderersEnabled(Renderer[] renderers, bool enabled)
    {
        if (renderers == null) return;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null) renderers[i].enabled = enabled;
        }
    }

    private void SetWarFlashVisualsActive(int index)
    {
        if (warFlashVisuals == null) return;
        for (int i = 0; i < warFlashVisuals.Length; i++)
        {
            if (warFlashVisuals[i] != null)
                warFlashVisuals[i].SetActive(i == index);
        }
    }

    private void StopFlicker(ref Coroutine routine)
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
    }

    private void Cleanup()
    {
        StopFlicker(ref _flickerMago);
        StopFlicker(ref _flickerWill);
        heartbeatSource?.Stop();
        shockEffects?.ForceEnd();
        _cinematicCamera?.Deactivate();
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    void OnValidate()
    {
        if (warFlashVisuals != null && warFlashVisuals.Length == 0)
            Debug.LogWarning("[PrologueDreamSequencer] No hay warFlashVisuals asignados — la Fase 2 (flashes de guerra) no mostrará nada.", this);
    }
#endif
}
