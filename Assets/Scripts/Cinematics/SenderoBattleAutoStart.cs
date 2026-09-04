using System.Collections;
using UnityEngine;

/// <summary>
/// Arranca automáticamente la cinemática de la Batalla Final al cargar `Sendero.unity`, sin
/// depender de que el grafo narrativo real (que hoy no llega hasta aquí — ver
/// `biblia-del-universo.md`, escenas 15-22 son "especulativo, no implementado todavía") dispare
/// la señal de entrada. Decisión de Raúl (30/08/2026): como esta escena es dedicada solo a este
/// tramo, no hace falta enganche narrativo real para poder montarla y probarla — basta con
/// lanzar la primera señal en cuanto la escena arranca.
///
/// Encadenamiento de señales (mismo sistema `DefaultNarrativeSignals`/`RaiseSignalOut()` que ya
/// usa el resto de cinemáticas del proyecto — ver `CinematicSequencerBase.cs`):
///
///   (este script) --SENDERO_FINAL_START--> MagoOscuroFinalBattleSequencer
///   MagoOscuroFinalBattleSequencer --SENDERO_SACRIFICIO_START--> WillSacrificeSequencer
///   WillSacrificeSequencer --SENDERO_EPILOGO_START--> EpilogueSequencer
///
/// Para que esto funcione hay que escribir estos mismos strings, EXACTOS y sin errores, en el
/// Inspector de cada componente:
///   - `MagoOscuroFinalBattleSequencer._signalIn`  = "SENDERO_FINAL_START"
///   - `MagoOscuroFinalBattleSequencer._signalOut` = "SENDERO_SACRIFICIO_START"
///   - `WillSacrificeSequencer._signalIn`          = "SENDERO_SACRIFICIO_START"
///   - `WillSacrificeSequencer._signalOut`         = "SENDERO_EPILOGO_START"
///   - `EpilogueSequencer._signalIn`               = "SENDERO_EPILOGO_START"
///   - `EpilogueSequencer._signalOut`              — libre, nada lo escucha todavía (no pasa
///     nada, `RaiseSignalOut()` sobre una señal sin oyentes no da error).
///
/// Colocar este componente en cualquier GameObject de la escena (p. ej. junto al resto de
/// managers ya existentes: `DayNightSystem`, `StarWorldLighting`, `WorldBootstrap`...).
///
/// Nota importante: los tres sequencers se suscriben a su `_signalIn` en su propio `Awake()`
/// (`CinematicSequencerBase.Awake()`). Unity garantiza que TODOS los `Awake()` de la escena
/// terminan antes de que se ejecute NINGÚN `Start()` — así que lanzar la señal desde `Start()`
/// aquí es seguro sin necesidad de `[DefaultExecutionOrder]` ni de esperar un frame extra. El
/// pequeño retraso opcional (`startDelaySeconds`) es solo para dar margen a que el jugador/cámara
/// terminen de colocarse (spawn del `WorldBootstrap`) antes de que la cámara cinemática tome el
/// control — auméntalo si ves que la cinemática arranca antes de que el jugador esté listo.
/// </summary>
public class SenderoBattleAutoStart : MonoBehaviour
{
    [Tooltip("Señal que dispara MagoOscuroFinalBattleSequencer al arrancar la escena. Debe coincidir EXACTAMENTE con el campo _signalIn de ese componente en el Inspector.")]
    [SerializeField] private string _startSignal = "SENDERO_FINAL_START";

    [Tooltip("Margen antes de lanzar la señal, para dar tiempo a que el jugador/cámara terminen de colocarse (spawn de WorldBootstrap) antes de que la cinemática tome el control. Sube este valor si la cinemática arranca demasiado pronto.")]
    [SerializeField] private float _startDelaySeconds = 0.3f;

    private bool _fired;

    private void Start()
    {
        StartCoroutine(Co_FireStartSignal());
    }

    private IEnumerator Co_FireStartSignal()
    {
        if (_fired) yield break;

        if (_startDelaySeconds > 0f)
            yield return new WaitForSeconds(_startDelaySeconds);

        if (_fired) yield break; // por si algo más ya la disparó mientras esperábamos
        _fired = true;

        if (string.IsNullOrEmpty(_startSignal))
        {
            Debug.LogWarning("[SenderoBattleAutoStart] _startSignal está vacío — no se lanza ninguna señal. Rellénalo en el Inspector (por defecto \"SENDERO_FINAL_START\").");
            yield break;
        }

        Debug.Log($"[SenderoBattleAutoStart] Lanzando señal de entrada '{_startSignal}' para arrancar la Batalla Final.");
        DefaultNarrativeSignals.EnsureInstance().RaiseCustom(_startSignal);
    }
}
