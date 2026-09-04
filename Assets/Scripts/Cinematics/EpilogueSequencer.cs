using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;
using Sendero.UI;

/// <summary>
/// Orquestador de la escena 22 del GDD — "Epílogo: el Adiós". Guion técnico completo:
/// guion-tecnico-batalla-final-2026-08-30.md en el proyecto de Cowork. Última pieza de la
/// Batalla Final y el desenlace (alcance decidido por Raúl, 30/08/2026: solo 20→22).
///
/// Continúa desde WillSacrificeSequencer (escena 21), que ya restauró/cortó la música al
/// terminar — este sequencer reproduce "EPILOGUE" (Créditos Finales Animados.mp3), la MISMA
/// pista que sceneMusic ya usa para la escena "Credits" (ver AudioGraphProfile.asset) — a
/// propósito, para que la transición a la escena de créditos al final no tenga un corte de
/// música, solo continúa.
///
/// Fases (ver guion técnico):
///   A. Liam revive junto a Estela
///   B. Estela le explica lo que pasó
///   C. Aparece el fantasma/espíritu de Will
///   D. Despedida
///   E. Will vuelve con su familia "en el cielo" — corte a créditos
///
/// Pendiente a mano en el Editor:
///   1) Confirmar el trigger de animación real de "revivir" de Liam (de partida, campo de texto
///      libre _liamReviveAnimTrigger, sin acoplar a un nombre de estado no confirmado).
///   2) Crear/asignar el visual del "espíritu de Will" — de partida se reutiliza el propio
///      _willActor con un material/VFX de fantasma superpuesto (_willSpiritVfx), no un prefab
///      nuevo, salvo que se prefiera un actor separado.
///   3) Confirmar cómo se dispara la transición real a la escena "Credits" — este script solo
///      levanta la señal de salida (_signalOut) al terminar; el nodo de grafo narrativo que
///      carga la escena de créditos debe existir en el capítulo nuevo del grafo (Cap7, ver
///      plan-batalla-final-y-final-2026-08-30.md).
/// </summary>
public class EpilogueSequencer : CinematicSequencerBase
{
    [Header("Actores")]
    [SerializeField] private Transform _liamActor;
    [SerializeField] private Transform _estelaActor;
    [Tooltip("Misma instancia de Will reutilizada como su propio 'fantasma' — ver punto 2 del pendiente.")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private GameObject _willSpiritVfx;
    private GameObject _willSpiritInstance;

    [Header("Cámara — shot points por fase")]
    [SerializeField] private Transform _shotLiamRevive;
    [SerializeField] private Transform _shotEstelaExplains;
    [SerializeField] private Transform _shotWillSpirit;
    [SerializeField] private Transform _shotFarewellWide;

    [Header("Fase A — Liam revive")]
    [SerializeField] private string _liamReviveAnimTrigger = "GetUp";
    [SerializeField] private float _liamReviveDuration = 2.5f;

    [Header("Fase B — Estela explica (vía localización, ver cinematics_es.json/cinematics_en.json)")]
    [Tooltip("Clave de localización, NO el texto en sí.")]
    [SerializeField] private string _estelaExplainsTextKey = "ESTELA_EXPLAINS_EPILOGUE";
    [SerializeField] private float _explainsHoldDuration = 4f;

    [Header("Fase C — Aparece el fantasma de Will")]
    [SerializeField] private float _spiritAppearDuration = 2f;

    [Header("Fase D — Despedida (vía localización)")]
    [Tooltip("Clave de localización, NO el texto en sí — ver cinematics_es.json/cinematics_en.json.")]
    [SerializeField] private string _willFarewellTextKey = "WILL_FAREWELL";
    [SerializeField] private float _farewellHoldDuration = 4f;

    [Header("Fase E — Will vuelve con su familia")]
    [SerializeField] private float _fadeToStarsDuration = 3f;
    [SerializeField] private Color _fadeColor = Color.white;

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition(_shotLiamRevive);
        PlaySequenceMusic("EPILOGUE");

        yield return Co_PhaseA_LiamRevives();
        yield return Co_PhaseB_EstelaExplains();
        yield return Co_PhaseC_WillSpiritAppears();
        yield return Co_PhaseD_Farewell();
        yield return Co_PhaseE_WillReturnsHome();

        // Sin Co_EndCinematicWithTransition: el corte a la escena de créditos lo gestiona el
        // grafo narrativo tras recibir esta señal, no este sequencer (ver punto 3 del pendiente).
        RaiseSignalOut();
    }

    private IEnumerator Co_PhaseA_LiamRevives()
    {
        _cinematicCamera?.Cut(_shotLiamRevive);
        if (_liamActor != null && !string.IsNullOrEmpty(_liamReviveAnimTrigger))
        {
            var animator = _liamActor.GetComponentInChildren<Animator>();
            animator?.SetTrigger(_liamReviveAnimTrigger);
        }
        yield return new WaitForSeconds(_liamReviveDuration);
    }

    private IEnumerator Co_PhaseB_EstelaExplains()
    {
        _cinematicCamera?.Cut(_shotEstelaExplains);
        yield return ShowBubblePaged(_estelaActor, Loc(_estelaExplainsTextKey), _explainsHoldDuration,
            animTrigger: "Talk03", loopAnim: true, speakerName: "Estela");
    }

    private IEnumerator Co_PhaseC_WillSpiritAppears()
    {
        _cinematicCamera?.Cut(_shotWillSpirit);
        if (_willSpiritVfx != null && _willActor != null)
            _willSpiritInstance = Instantiate(_willSpiritVfx, _willActor.position, _willActor.rotation);

        yield return new WaitForSeconds(_spiritAppearDuration);
    }

    private IEnumerator Co_PhaseD_Farewell()
    {
        yield return ShowBubblePaged(_willActor, Loc(_willFarewellTextKey), _farewellHoldDuration,
            animTrigger: "HandWave02", speakerName: "Will");
    }

    private IEnumerator Co_PhaseE_WillReturnsHome()
    {
        _cinematicCamera?.Cut(_shotFarewellWide);
        yield return FeedbackService.ScreenFadeAsync(_fadeColor, _fadeToStarsDuration, fadeIn: true);
    }

    protected override void OnSkipCleanup()
    {
        if (_willSpiritInstance != null) Destroy(_willSpiritInstance);
    }
}
