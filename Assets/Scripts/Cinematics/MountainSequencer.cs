using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia de la montaña:
///   0. Estela dice algo (opcional).
///   1. Dispara al objetivo de la montaña.
///   2. Flash + shake de impacto → fade a negro.
/// El grafo narrativo conecta MOUNTAIN_DONE con MOUNTAIN_EXPLOSION_EVENT.
/// Señal de entrada: "MOUNTAIN_START".
/// Señal de salida:  "MOUNTAIN_DONE".
[DisallowMultipleComponent]
public class MountainSequencer : CinematicSequencerBase
{
    [Header("Personaje — Estela")]
    [SerializeField] private Transform _estelaTransform;
    [Tooltip("Punto de lanzamiento del hechizo (hueso de mano o equivalente)")]
    [SerializeField] private Transform _estelaSpawnPoint;
    [SerializeField] private MagicSpellSO _bolaFuego;

    [Header("Cámara")]
    [Tooltip("Plano de Estela antes de disparar")]
    [SerializeField] private Transform _shotEstela;

    [Header("Objetivo")]
    [Tooltip("Transform clavado en el punto de impacto de la montaña")]
    [SerializeField] private Transform _mountainTarget;

    [Header("Diálogo — opcional")]
    [Tooltip("Clave de localización. Vacío = sin globo, dispara directamente.")]
    [SerializeField] private string     _keyLine      = "EVT_MOUNTAIN_01";
    [SerializeField] private NPCEmotion _emotionLine  = NPCEmotion.Angry;
    [SerializeField] private string     _animLine     = "Angry01";
    [SerializeField] private float      _lineDuration = 2.5f;

    [Header("Ráfaga")]
    [SerializeField] private float _aimDelay            = 0.5f;
    [Tooltip("Número de proyectiles de la ráfaga")]
    [SerializeField] private int   _rageShots           = 6;
    [Tooltip("Segundos entre cada proyectil")]
    [SerializeField] private float _rageShotInterval    = 0.2f;
    [Tooltip("Tiempo de vuelo del último proyectil antes del flash de impacto")]
    [SerializeField] private float _mountainImpactDelay = 1.0f;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private NPCEmotionController _estelaEmotion;
    private NPCSimpleAnimator    _estelaSimpleAnim;
    private int _enemyHitLayers;
    private int _enemyCollisionLayers;

    protected override void Awake()
    {
        base.Awake();
        if (_estelaTransform != null)
        {
            _estelaEmotion    = _estelaTransform.GetComponentInChildren<NPCEmotionController>();
            _estelaSimpleAnim = _estelaTransform.GetComponentInChildren<NPCSimpleAnimator>();
        }
        _enemyHitLayers       = LayerMask.GetMask("Enemy", "Boss");
        _enemyCollisionLayers = LayerMask.GetMask("Enemy", "Boss", "Default");
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [ContextMenu("Test — iniciar sin señal")]
    private void TestStartDirect() => StartCoroutine(Co_Sequence());
#endif

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition(_shotEstela);
        PlaySequenceMusic();

        FaceTarget(_estelaTransform, _mountainTarget);

        // ── Diálogo opcional ──────────────────────────────────────────────────
        if (!string.IsNullOrEmpty(_keyLine))
        {
            _estelaEmotion?.SetEmotion(_emotionLine);
            bool done = false;
            SpeechBubbleUI.Instance.Show(_estelaTransform, Loc(_keyLine),
                duration: _lineDuration,
                onComplete: () => done = true,
                animTrigger: _animLine);
            yield return new WaitUntil(() => done);
        }

        // ── Ráfaga ────────────────────────────────────────────────────────────
        yield return new WaitForSeconds(_aimDelay);
        for (int i = 0; i < _rageShots; i++)
        {
            FireAtMountain();
            yield return new WaitForSeconds(_rageShotInterval);
        }

        // ── Impacto ───────────────────────────────────────────────────────────
        yield return new WaitForSeconds(_mountainImpactDelay);
        FeedbackService.ScreenFlash(new Color(1f, 0.35f, 0.05f, 0.75f), 0.9f);
        FeedbackService.CameraShake(0.6f, 1.0f);

        yield return Co_EndCinematicWithTransition(RestoreMusic);
        RaiseSignalOut();
    }

    // ── Helper — disparo ──────────────────────────────────────────────────────

    private void FireAtMountain()
    {
        if (_bolaFuego == null || _bolaFuego.prefab == null || _mountainTarget == null) return;

        Transform origin  = _estelaSpawnPoint != null ? _estelaSpawnPoint : _estelaTransform;
        Vector3 spawnPos  = origin.position + origin.TransformDirection(_bolaFuego.positionOffset);
        Vector3 targetPos = _mountainTarget.position + Vector3.up;
        Vector3 dir       = (targetPos - spawnPos).normalized;

        if (_bolaFuego.flattenDirection) { dir.y = 0f; dir.Normalize(); }

        FaceTarget(_estelaTransform, _mountainTarget);
        _estelaSimpleAnim?.PlaySpellCast();

        var go = Instantiate(_bolaFuego.prefab, spawnPos, Quaternion.LookRotation(dir));

        int projLayer = LayerMask.NameToLayer("Projectile");
        if (projLayer != -1)
            foreach (var t in go.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = projLayer;

        if (go.TryGetComponent<MagicProjectile>(out var proj))
        {
            var cfg = new MagicProjectile.ProjectileConfig
            {
                damage          = _bolaFuego.damage,
                aoeRadius       = _bolaFuego.aoeRadius,
                knockbackForce  = _bolaFuego.knockbackForce,
                hitLayers       = _enemyHitLayers,
                collisionLayers = _enemyCollisionLayers,
                destroyOnHit    = _bolaFuego.destroyOnHit,
                lifeTime        = _bolaFuego.lifeTime,
                maxRange        = _bolaFuego.maxRange,
                initialSpeed    = _bolaFuego.initialSpeed,
                useGravity      = _bolaFuego.useGravity,
                element         = _bolaFuego.element,
                impactVFX       = _bolaFuego.impactVFX,
                despawnVFX      = _bolaFuego.despawnVFX,
                vfxLifetime     = _bolaFuego.vfxLifetime,
                impactSFXKey    = _bolaFuego.impactSFXKey
            };
            proj.Configure(cfg, _estelaTransform.gameObject);
            proj.Launch(dir, _bolaFuego.initialSpeed, _bolaFuego.useGravity);
        }
    }
}
