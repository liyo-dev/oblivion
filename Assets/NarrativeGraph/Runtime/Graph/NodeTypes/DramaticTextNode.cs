using System;
using UnityEngine;

/// <summary>
/// Nodo del grafo narrativo que muestra frases dramáticas en pantalla (recuerdos, momentos épicos, urgencia).
/// Configura un DramaticPhraseConfig con las frases, estilos, animaciones y audio de cada frase.
/// </summary>
[Serializable]
public sealed class DramaticTextNode : NarrativeNode
{
    [Tooltip("Secuencia de frases dramáticas a mostrar.")]
    public DramaticPhraseConfig config;

    [Tooltip("Si true, el grafo espera a que terminen todas las frases antes de continuar. " +
             "Si false, el grafo avanza de inmediato mientras las frases se reproducen en paralelo.")]
    public bool waitForCompletion = true;

    [Header("Música")]
    [Tooltip("Si true, al terminar las frases restaura la música de la escena actual. " +
             "Requiere que el controlador previo haya activado AudioService.MuteNextBaseSceneMusic antes de cargar la escena.")]
    public bool restoreSceneMusicWhenDone = false;

    [Min(0f)]
    [Tooltip("Segundos de fade in al restaurar la música de la escena.")]
    public float restoreMusicFade = 2f;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        var ui = DramaticTextOverlayUI.Instance;

        if (ui == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[DramaticTextNode] ❌ DramaticTextOverlayUI.Instance es NULL. " +
                           "Añade el prefab al Canvas persistente en Start.unity.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("[DramaticTextNode] ❌ 'config' no está asignado en el nodo del grafo.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (config.phrases == null || config.phrases.Length == 0)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError($"[DramaticTextNode] ❌ El DramaticPhraseConfig '{config.name}' no tiene frases.");
#endif
            onReadyToAdvance?.Invoke();
            return;
        }

        if (waitForCompletion)
        {
            ui.Play(config, () =>
            {
                if (restoreSceneMusicWhenDone)
                    AudioService.Instance?.RestoreSceneMusic(restoreMusicFade);
                onReadyToAdvance?.Invoke();
            });
        }
        else
        {
            ui.Play(config, restoreSceneMusicWhenDone
                ? () => AudioService.Instance?.RestoreSceneMusic(restoreMusicFade)
                : null);
            onReadyToAdvance?.Invoke();
        }
    }
}
