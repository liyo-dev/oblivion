using System.Collections;
using UnityEngine;
using Sendero.Core.Feedback;
using Sendero.UI;

/// <summary>
/// Orquestador de la escena 21 del GDD — "El Final: el Sacrificio". Guion técnico completo:
/// guion-tecnico-batalla-final-2026-08-30.md en el proyecto de Cowork. Continúa directamente
/// desde MagoOscuroFinalBattleSequencer (misma sesión de cinemática encadenada, mismo patrón que
/// ya usa el proyecto para secuencias consecutivas — ver s_activeSequenceCount en
/// CinematicSequencerBase, pensado exactamente para esto).
///
/// Deliberadamente NO llama a PlaySequenceMusic() al empezar: continúa "MAGOOSCURO_CLIMAX"
/// (El Sendero de las Estrellas - Final_1_Sincoros.mp3) ya sonando desde la escena 20 sin cortar
/// — es el mismo pico emocional, cortar la música entre 20 y 21 rompería la continuidad. Si en
/// el montaje real se prefiere un cambio de pista aquí, añadir una entrada "WILL_SACRIFICE" en
/// AudioGraphProfile.sequences y llamar a PlaySequenceMusic("WILL_SACRIFICE") al principio de
/// Co_Sequence().
///
/// Fases (ver guion técnico):
///   A. Derrota/desaparición del Mago Oscuro — resolución visual pendiente de decidir con Raúl
///      (guion técnico, pregunta 3): de partida se implementa como disolución con VFX + fundido,
///      ajustable sin tocar la estructura de fases.
///   B. El deseo — Will pide curar al hermano de Liam (diálogo)
///   C. Colapso del Sendero empieza (temblor, grietas de luz)
///   D. Will empuja a Estela fuera del portal (diálogo + salida forzada de Estela)
///   E. Will solo, ejecuta el Hechizo Prohibido de Resurrección — sacrificio final
///
/// Pendiente a mano en el Editor:
///   1) Colocar/confirmar los Transforms de Will, Estela y el Mago Oscuro ya presentes desde la
///      escena 20 (se asume la MISMA instancia, no una nueva).
///   2) VFX de portal para la Fase D (recomendado en el guion técnico: vfx_Portal_01/02 de
///      GabrielAguiarProductions, por continuidad de estilo con el resto de hechizos).
///   3) VFX de resurrección/sacrificio de la Fase E (no existe nada reutilizable en el proyecto
///      todavía — candidato a diseñar, ver guion técnico).
///   4) Confirmar el nombre real del trigger de animación de "salida corriendo/empujada" de
///      Estela y de "caída/reposo" de Liam si se le ve en plano — de partida se dejan como
///      campos de texto libres (animTrigger) para no acoplar este script a nombres de estado que
///      no se han podido confirmar sin el Editor abierto.
/// </summary>
public class WillSacrificeSequencer : CinematicSequencerBase
{
    [Header("Actores (misma instancia que en la escena 20)")]
    [SerializeField] private Transform _willActor;
    [SerializeField] private Transform _estelaActor;
    [SerializeField] private Transform _magoOscuroActor;
    [SerializeField] private Transform _portalTransform;
    [SerializeField] private Transform _portalExitPoint;

    [Header("Cámara — shot points por fase")]
    [SerializeField] private Transform _shotMagoDefeat;
    [SerializeField] private Transform _shotWillEstelaClose;
    [SerializeField] private Transform _shotPortalCollapse;
    [SerializeField] private Transform _shotWillAlone;

    [Header("Fase A — Derrota del Mago Oscuro")]
    [SerializeField] private GameObject _magoDefeatVfx;
    [SerializeField] private float _magoDefeatDuration = 3f;

    [Header("Fase B — El deseo (vía localización, ver cinematics_es.json/cinematics_en.json)")]
    [Tooltip("Clave de localización, NO el texto en sí.")]
    [SerializeField] private string _willWishTextKey = "WILL_FINAL_WISH";
    [SerializeField] private float _wishHoldDuration = 3f;

    [Header("Fase C — Colapso del Sendero")]
    [SerializeField] private GameObject _collapseAmbientVfx;
    [SerializeField] private float _collapseBuildupDuration = 2.5f;
    [SerializeField] private float _collapseShakeIntensity = 0.5f;

    [Header("Fase D — Will empuja a Estela fuera del portal (vía localización)")]
    [SerializeField] private GameObject _portalVfx;
    [Tooltip("Claves de localización, NO el texto en sí — ver cinematics_es.json/cinematics_en.json.")]
    [SerializeField] private string _estelaProtestTextKey = "ESTELA_PROTEST_PORTAL";
    [SerializeField] private string _willCalmReplyTextKey = "WILL_CALM_REPLY";
    [SerializeField] private float _estelaExitDuration = 2f;

    [Header("Fase E — Hechizo Prohibido de Resurrección")]
    [Tooltip("VFX del sacrificio final — luz consumiéndose a sí misma. No existe nada reutilizable en el proyecto todavía, ver punto 3 del pendiente.")]
    [SerializeField] private GameObject _resurrectionVfx;
    [SerializeField] private float _resurrectionDuration = 5f;
    [Tooltip("VFX de la destrucción final del Sendero, tras completarse el sacrificio.")]
    [SerializeField] private GameObject _senderoDestructionVfx;

    protected override IEnumerator Co_Sequence()
    {
        yield return Co_BeginCinematicWithTransition(_shotMagoDefeat);
        // Sin PlaySequenceMusic() a propósito — ver comentario de cabecera.

        yield return Co_PhaseA_MagoDefeat();
        yield return Co_PhaseB_TheWish();
        yield return Co_PhaseC_SenderoCollapseBegins();
        yield return Co_PhaseD_PushEstelaOut();
        yield return Co_PhaseE_Resurrection();

        yield return Co_EndCinematicStayBlack();
        RestoreMusic(); // aquí sí se restaura/corta — el epílogo (escena 22) toma su propia música
        RaiseSignalOut();
    }

    private IEnumerator Co_PhaseA_MagoDefeat()
    {
        _cinematicCamera?.Cut(_shotMagoDefeat);
        if (_magoDefeatVfx != null && _magoOscuroActor != null)
            VfxPoolService.Instance?.Play(_magoDefeatVfx, _magoOscuroActor.position, Quaternion.identity, _magoDefeatDuration);

        yield return new WaitForSeconds(_magoDefeatDuration);
        if (_magoOscuroActor != null) _magoOscuroActor.gameObject.SetActive(false);
    }

    private IEnumerator Co_PhaseB_TheWish()
    {
        _cinematicCamera?.Cut(_shotWillEstelaClose);
        yield return ShowBubblePaged(_willActor, Loc(_willWishTextKey), _wishHoldDuration,
            animTrigger: "Beg01", speakerName: "Will");
    }

    private IEnumerator Co_PhaseC_SenderoCollapseBegins()
    {
        _cinematicCamera?.Cut(_shotPortalCollapse);
        FeedbackService.CameraShake(_collapseShakeIntensity, _collapseBuildupDuration);
        if (_collapseAmbientVfx != null && _portalTransform != null)
            VfxPoolService.Instance?.Play(_collapseAmbientVfx, _portalTransform.position, Quaternion.identity, _collapseBuildupDuration + 1f);

        yield return new WaitForSeconds(_collapseBuildupDuration);
    }

    private IEnumerator Co_PhaseD_PushEstelaOut()
    {
        yield return ShowBubblePaged(_estelaActor, Loc(_estelaProtestTextKey), 2f,
            animTrigger: "HeadShake01", speakerName: "Estela");
        yield return ShowBubblePaged(_willActor, Loc(_willCalmReplyTextKey), 2.5f,
            animTrigger: "Talk01", speakerName: "Will");

        if (_portalVfx != null && _portalTransform != null)
            VfxPoolService.Instance?.Play(_portalVfx, _portalTransform.position, Quaternion.identity, _estelaExitDuration + 1f);

        if (_estelaActor != null && _portalExitPoint != null)
        {
            float elapsed = 0f;
            Vector3 from = _estelaActor.position;
            while (elapsed < _estelaExitDuration)
            {
                _estelaActor.position = Vector3.Lerp(from, _portalExitPoint.position, elapsed / _estelaExitDuration);
                elapsed += Time.deltaTime;
                yield return null;
            }
            _estelaActor.position = _portalExitPoint.position;
            _estelaActor.gameObject.SetActive(false); // sale del Sendero, ya no está en esta escena
        }
    }

    private IEnumerator Co_PhaseE_Resurrection()
    {
        _cinematicCamera?.Cut(_shotWillAlone);

        if (_resurrectionVfx != null && _willActor != null)
            VfxPoolService.Instance?.Play(_resurrectionVfx, _willActor.position, Quaternion.identity, _resurrectionDuration);

        yield return new WaitForSeconds(_resurrectionDuration);

        if (_senderoDestructionVfx != null && _portalTransform != null)
            VfxPoolService.Instance?.Play(_senderoDestructionVfx, _portalTransform.position, Quaternion.identity, 4f);

        yield return new WaitForSeconds(2f);
    }

    protected override bool SkipRestoresMusic => true;
}
