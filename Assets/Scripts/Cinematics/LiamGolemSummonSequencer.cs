using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using Sendero.Core.Feedback;

/// Orquestador de la secuencia en la que Liam, viendo a Estela unirse a Will,
/// decide enviar un gólem para eliminar la amenaza.
/// Señal de entrada: "LIAM_GOLEM_SUMMON_START".
/// Señal de salida:  "LIAM_GOLEM_SUMMON_DONE".
[DisallowMultipleComponent]
public class LiamGolemSummonSequencer : CinematicSequencerBase
{
    [Header("Personaje — Liam")]
    [SerializeField] private Transform _liamTransform;

    [Header("Cámara — planos")]
    [Tooltip("Plano medio de Liam ante la bola de cristal")]
    [SerializeField] private Transform _shotCrystalBall;
    [Tooltip("Plano general: Liam rodeado del círculo mágico invocado")]
    [SerializeField] private Transform _shotWide;
    [Tooltip("Primer plano del rostro de Liam para la amenaza final")]
    [SerializeField] private Transform _shotLiamFace;

    [Header("VFX — conjuración")]
    [Tooltip("Prefab del círculo mágico en el suelo (se instancia en la posición de Liam)")]
    [SerializeField] private GameObject _magicCircleVFXPrefab;
    [Tooltip("Prefab del aura corporal de Liam (se instancia bajo _liamTransform)")]
    [SerializeField] private GameObject _bodyAuraVFXPrefab;

    [Header("Bola de cristal — visión del jugador y Estela")]
    [Tooltip("Transform del jugador para que la cámara de visión lo siga.")]
    [SerializeField] private Transform _playerTransform;
    [Tooltip("Transform de Estela (punto medio con el jugador para el encuadre).")]
    [SerializeField] private Transform _estelaTransform;
    [Tooltip("Cámara secundaria que renderiza a un RT asignado como Target Texture.")]
    [SerializeField] private CrystalBallVisionCamera _crystalVisionCamera;
    [Tooltip("Renderer de la bola de cristal. Su material necesita Emission ON y _EmissionMap = RT.")]
    [SerializeField] private Renderer _crystalBallRenderer;
    [Tooltip("Color HDR de la emisión mientras la visión está activa.")]
    [SerializeField] private Color _visionEmissionColor = new Color(0.15f, 1f, 0.45f, 1f);
    [SerializeField] private float _visionFadeIn  = 0.8f;
    [SerializeField] private float _visionFadeOut = 0.5f;

    [Header("Bola de cristal — pulso mágico")]
    [SerializeField] private float _pulseFrequency = 1.1f;
    [SerializeField, Range(0f, 1f)] private float _pulseMinScale = 0.55f;
    [SerializeField, Range(0f, 1f)] private float _colorShimmerAmount = 0.35f;
    [SerializeField] private Color _visionShimmerColor = new Color(0.05f, 0.55f, 1f, 1f);
    [Tooltip("Sistema de partículas sobre la bola (opcional). Se activa con la visión.")]
    [SerializeField] private ParticleSystem _crystalBallParticles;

    [Header("Flash de conjuración")]
    [Tooltip("Color del flash de pantalla cuando aparece el círculo mágico (alpha bajo para sutileza)")]
    [SerializeField] private Color _summonFlashColor   = new Color(0.5f, 0f, 0.8f, 0.15f);
    [SerializeField] private float _summonFlashFadeOut = 2.0f;

    // ── Fase 1 — Liam ve a Estela en la bola de cristal ──────────────────────

    [Header("Fase 1 — Liam ve a Estela en la bola de cristal")]
    [SerializeField] private string     _keyLine1      = "EVT_GOLEM_01";
    [SerializeField] private NPCEmotion _emotionLine1  = NPCEmotion.Angry;
    [SerializeField] private string     _animLine1     = "Angry01";
    [SerializeField] private float      _line1Duration = 3.5f;
    [Tooltip("Pausa de tensión tras la primera frase, antes de que aparezca el VFX")]
    [SerializeField] private float      _vfxAppearDelay = 0.6f;

    // ── Fase 2 — Conjuración del gólem ───────────────────────────────────────

    [Header("Fase 2 — Conjuración del gólem")]
    [SerializeField] private string     _keyLine2      = "EVT_LIAM_GOLEM_01";
    [SerializeField] private NPCEmotion _emotionLine2  = NPCEmotion.Happy;
    [SerializeField] private string     _animLine2     = "Smug01";
    [SerializeField] private float      _line2Duration = 4.0f;

    // ── Fase 3 — Amenaza personal ─────────────────────────────────────────────

    [Header("Fase 3 — Amenaza personal de Liam")]
    [SerializeField] private string     _keyLine3      = "EVT_LIAM_GOLEM_02";
    [SerializeField] private NPCEmotion _emotionLine3  = NPCEmotion.Thinking;
    [SerializeField] private string     _animLine3     = "Thinking01";
    [SerializeField] private float      _line3Duration = 3.0f;
    [SerializeField] private float      _holdAfterEnd  = 1.2f;

    // ── Cache ─────────────────────────────────────────────────────────────────

    private NPCEmotionController _liamEmotion;
    private Game.NPC.NPCBehaviourManagerV2 _liamNpc;
    private NavMeshAgent _liamAgent;
    private ObstacleAvoidanceType _liamOriginalAvoidance;
    private Vector3 _liamDesignPosition;
    private Quaternion _liamDesignRotation;
    private ParticleSystem _magicCircleVFX;
    private ParticleSystem _bodyAuraVFX;
    private MaterialPropertyBlock _mpb;
    private bool _visionPulsing;
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    protected override void Awake()
    {
        base.Awake();
        if (_liamTransform != null)
        {
            _liamEmotion = _liamTransform.GetComponentInChildren<NPCEmotionController>();
            _liamNpc     = _liamTransform.GetComponent<Game.NPC.NPCBehaviourManagerV2>();
            _liamAgent   = _liamTransform.GetComponent<NavMeshAgent>();

            // FIX: capturar la posición/rotación colocadas a mano en el editor ANTES de que el
            // NavMeshAgent de Liam pueda corregirlas él solo al activarse. Si el punto exacto donde
            // está colocado (pegado a la mesa/alfombra de esta habitación) no cae sobre el NavMesh
            // baked, Unity lo desplaza al punto válido más cercano en cuanto el agente se activa —
            // esto ocurre a nivel de motor, nada más arrancar la partida, sin pasar por ningún
            // script nuestro. Ver Co_RestoreLiamDesignPosition().
            _liamDesignPosition = _liamTransform.position;
            _liamDesignRotation = _liamTransform.rotation;
        }
        if (_crystalBallRenderer != null)
            _mpb = new MaterialPropertyBlock();
    }

    private void Start()
    {
        if (_liamTransform != null)
            StartCoroutine(Co_RestoreLiamDesignPosition());
    }

    /// Un frame después de Awake, el NavMeshAgent de Liam ya habrá aplicado (si iba a hacerlo) su
    /// corrección automática al activarse. Si su posición se alejó de donde lo colocamos a mano,
    /// lo devolvemos ahí con Warp (evita que NavMesh/física generen un nuevo path).
    private IEnumerator Co_RestoreLiamDesignPosition()
    {
        yield return null;
        if (_liamTransform == null) yield break;

        if ((_liamTransform.position - _liamDesignPosition).sqrMagnitude > 0.0001f)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning($"[LiamGolemSummonSequencer] Liam se había desplazado de su posición diseñada " +
                $"({_liamTransform.position} → objetivo {_liamDesignPosition}), probablemente por corrección " +
                $"automática del NavMeshAgent al activarse. Restaurando.");
#endif
            if (_liamAgent != null && _liamAgent.isOnNavMesh)
                _liamAgent.Warp(_liamDesignPosition);
            else
                _liamTransform.position = _liamDesignPosition;
        }

        _liamTransform.rotation = _liamDesignRotation;
    }

    // FIX: HardStop (isStopped=true) no evita que un NavMeshAgent con obstacle avoidance activo
    // (aquí HighQuality) siga siendo empujado por la avoidance del jugador u otros agentes cercanos
    // — es el mismo motivo por el que CinematicState.MoveToPositionSequence desactiva la avoidance
    // durante el movimiento cinemático (ver su comentario "para que Player y Party Members no
    // bloqueen al NPC"). Sin esto Liam podía seguir desplazándose ligeramente aunque su FSM
    // estuviera desactivada y el agente "detenido".
    private void FreezeLiamNavigation()
    {
        _liamNpc?.ForceIdle();
        if (_liamAgent != null)
        {
            _liamOriginalAvoidance = _liamAgent.obstacleAvoidanceType;
            _liamAgent.obstacleAvoidanceType = ObstacleAvoidanceType.NoObstacleAvoidance;
        }
        if (_liamNpc != null) _liamNpc.enabled = false;
    }

    private void UnfreezeLiamNavigation()
    {
        if (_liamNpc != null)
        {
            _liamNpc.enabled = true;
            _liamNpc.ForceIdle();
        }
        if (_liamAgent != null)
            _liamAgent.obstacleAvoidanceType = _liamOriginalAvoidance;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        _visionPulsing = false;
        _crystalVisionCamera?.Deactivate();
        _crystalBallParticles?.Stop();
        if (_crystalBallRenderer != null)
            _crystalBallRenderer.SetPropertyBlock(null);
        DestroyVFX();
    }

    // ── Secuencia principal ───────────────────────────────────────────────────

    protected override IEnumerator Co_Sequence()
    {
        // Congelar FSM + navegación de Liam en el cut point (pantalla cubierta), igual que
        // hacen TabernaSequencer/EstelaAppearsSequencer con sus NPCs. Ver FreezeLiamNavigation().
        yield return Co_BeginCinematicWithTransition(_shotCrystalBall, FreezeLiamNavigation);

        PlaySequenceMusic();

        // ── Fase 1: Plano bola de cristal — Liam ve a Estela unirse a Will ────
        StartCoroutine(Co_ShowCrystalVision());
        _liamEmotion?.SetEmotion(_emotionLine1);

        bool line1Done = false;
        SpeechBubbleUI.Instance.Show(_liamTransform, Loc(_keyLine1),
            duration: _line1Duration,
            onComplete: () => line1Done = true,
            animTrigger: _animLine1);
        yield return new WaitUntil(() => line1Done);

        // Apagar la visión antes de cortar al plano de la conjuración
        yield return StartCoroutine(Co_HideCrystalVision());

        yield return new WaitForSeconds(_vfxAppearDelay);

        // ── Fase 2: Plano general — aparece el círculo mágico + aura corporal ─
        _cinematicCamera.Cut(_shotWide);

        SpawnVFX();
        _magicCircleVFX?.Play();
        _bodyAuraVFX?.Play();
        FeedbackService.ScreenFlash(_summonFlashColor, _summonFlashFadeOut);

        _liamEmotion?.SetEmotion(_emotionLine2);

        bool line2Done = false;
        SpeechBubbleUI.Instance.Show(_liamTransform, Loc(_keyLine2),
            duration: _line2Duration,
            onComplete: () => line2Done = true,
            animTrigger: _animLine2);
        yield return new WaitUntil(() => line2Done);

        // ── Fase 3: Primer plano — amenaza personal de Liam ──────────────────
        _cinematicCamera.Cut(_shotLiamFace);
        _liamEmotion?.SetEmotion(_emotionLine3);

        bool line3Done = false;
        SpeechBubbleUI.Instance.Show(_liamTransform, Loc(_keyLine3),
            duration: _line3Duration,
            onComplete: () => line3Done = true,
            animTrigger: _animLine3);
        yield return new WaitUntil(() => line3Done);

        yield return new WaitForSeconds(_holdAfterEnd);

        DestroyVFX();

        yield return Co_EndCinematicStayBlack(() =>
        {
            RestoreMusic();
            // Reactivar navegación de Liam mientras la pantalla sigue cubierta (el sistema
            // siguiente, ej. la intro del gólem, se encarga de revelar), igual que se congeló
            // al entrar.
            UnfreezeLiamNavigation();
        });

        RaiseSignalOut();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Visión del jugador y Estela en la bola de cristal
    // ══════════════════════════════════════════════════════════════════════════

    private IEnumerator Co_ShowCrystalVision()
    {
        if (_crystalVisionCamera == null || _crystalBallRenderer == null || _mpb == null)
            yield break;

        _crystalVisionCamera.Activate(_playerTransform, _estelaTransform);

        float elapsed = 0f;
        while (elapsed < _visionFadeIn)
        {
            elapsed += Time.deltaTime;
            _mpb.SetColor(EmissionColorId,
                Color.Lerp(Color.black, _visionEmissionColor, elapsed / _visionFadeIn));
            _crystalBallRenderer.SetPropertyBlock(_mpb);
            yield return null;
        }
        _mpb.SetColor(EmissionColorId, _visionEmissionColor);
        _crystalBallRenderer.SetPropertyBlock(_mpb);

        _crystalBallParticles?.Play();
        StartCoroutine(Co_PulseCrystalVision());
    }

    private IEnumerator Co_PulseCrystalVision()
    {
        _visionPulsing = true;
        while (_visionPulsing)
        {
            float t = (Mathf.Sin(Time.time * _pulseFrequency * Mathf.PI * 2f) + 1f) * 0.5f;
            float scale = Mathf.Lerp(_pulseMinScale, 1f, t);
            Color emissive = _colorShimmerAmount > 0f
                ? Color.Lerp(_visionEmissionColor, _visionShimmerColor, t * _colorShimmerAmount)
                : _visionEmissionColor;
            _mpb.SetColor(EmissionColorId, emissive * scale);
            _crystalBallRenderer.SetPropertyBlock(_mpb);
            yield return null;
        }
    }

    private IEnumerator Co_HideCrystalVision()
    {
        if (_crystalBallRenderer == null || _mpb == null) yield break;

        _visionPulsing = false;
        yield return null; // deja que el loop de pulso salga antes de leer el color

        _crystalBallParticles?.Stop();

        _crystalBallRenderer.GetPropertyBlock(_mpb);
        Color startColor = _mpb.GetColor(EmissionColorId);

        float elapsed = 0f;
        while (elapsed < _visionFadeOut)
        {
            elapsed += Time.deltaTime;
            _mpb.SetColor(EmissionColorId,
                Color.Lerp(startColor, Color.black, elapsed / _visionFadeOut));
            _crystalBallRenderer.SetPropertyBlock(_mpb);
            yield return null;
        }
        _crystalBallRenderer.SetPropertyBlock(null);
        _crystalVisionCamera?.Deactivate();
    }

    // ══════════════════════════════════════════════════════════════════════════
    // VFX
    // ══════════════════════════════════════════════════════════════════════════

    private void SpawnVFX()
    {
        var liamRoot = _liamTransform != null ? _liamTransform : transform;

        if (_magicCircleVFXPrefab != null && _magicCircleVFX == null)
        {
            var go = Instantiate(_magicCircleVFXPrefab, liamRoot.position, Quaternion.identity);
            _magicCircleVFX = go.GetComponent<ParticleSystem>();
        }

        if (_bodyAuraVFXPrefab != null && _bodyAuraVFX == null)
        {
            var go = Instantiate(_bodyAuraVFXPrefab, liamRoot.position, Quaternion.identity, liamRoot);
            _bodyAuraVFX = go.GetComponent<ParticleSystem>();
        }
    }

    private void DestroyVFX()
    {
        if (_magicCircleVFX != null)
        {
            Destroy(_magicCircleVFX.gameObject);
            _magicCircleVFX = null;
        }
        if (_bodyAuraVFX != null)
        {
            Destroy(_bodyAuraVFX.gameObject);
            _bodyAuraVFX = null;
        }
    }
}
