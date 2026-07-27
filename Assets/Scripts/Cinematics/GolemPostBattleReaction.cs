using UnityEngine;

/// <summary>
/// Muestra un bocadillo de Will justo después de derrotar al Golem, comentando lo raro
/// que es que aparezca un segundo enemigo de este tipo y sugiriendo volver con Eldran.
///
/// Se engancha por código al mismo sistema de señales narrativas que usa BossArenaController
/// (DefaultNarrativeSignals.RaiseBattleWon) usando el battleId "Golem_1" configurado en la
/// escena. Al ser un bootstrap estático no requiere añadir ningún GameObject a mano en el editor.
/// </summary>
public static class GolemPostBattleReaction
{
    private const string GolemBattleId = "Golem_1";
    private const float DelayAfterVictory = 1.2f;
    private const float BubbleDuration = 4.5f;
    private const string LineKey = "EVT_WILL_POST_GOLEM";
    private const string LineFallback = "¿Has visto, Estela? ¡Ahora un golem! Esto nunca había pasado... ¡Corre, volvamos con Eldran!";
    // FIX INC-056: gesto de sorpresa/urgencia mientras Will dice la línea (misma convención de
    // triggers de gesto social que usan los demás Sequencer, ej. animWillSurprise en StarAwakeningSequencer).
    private const string ReactionAnimTrigger = "Fear01";

    private static bool _subscribed;
    private static Runner _runner;
    // Solo liberamos el lock-on de cámara si fuimos nosotros quienes lo pusimos (evita robarle
    // el lock a CombatCameraTargeting u otro sistema si Co_ShowReaction se corta a mitad).
    private static bool _weSetCameraLock;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        _subscribed = false;
        _runner = null;
        _weSetCameraLock = false;
        TrySubscribe();
    }

    private static void TrySubscribe()
    {
        if (_subscribed) return;
        var signals = DefaultNarrativeSignals.EnsureInstance();
        if (signals == null) return;

        signals.OnBattleWon(GolemBattleId, OnGolemDefeated);
        _subscribed = true;
    }

    private static void OnGolemDefeated()
    {
        if (_runner == null)
        {
            var go = new GameObject("[GolemPostBattleReaction]");
            Object.DontDestroyOnLoad(go);
            _runner = go.AddComponent<Runner>();
        }

        _runner.StartCoroutine(Co_ShowReaction());
    }

    private static System.Collections.IEnumerator Co_ShowReaction()
    {
        yield return new WaitForSeconds(DelayAfterVictory);

        // FIX INC-054: la línea está escrita desde la perspectiva de Will ("¿Has visto, Estela?"),
        // así que el bocadillo debe aparecer siempre sobre Will, sea o no el personaje activo.
        // Antes se usaba PlayerService.Player.transform, que apunta al personaje CONTROLADO
        // (Estela si el jugador había cambiado a ella), haciendo que el bocadillo saliera mal.
        Transform willTransform = GetWillTransform();
        if (willTransform == null || SpeechBubbleUI.Instance == null) yield break;

        string text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.Get(LineKey, LineFallback)
            : LineFallback;

        // FIX INC-056: antes el bocadillo aparecía "en frío", sin que la cámara enfocara a Will
        // ni que este gesticulara. Usamos el lock-on suave de vThirdPersonCamera (mismo mecanismo
        // que CombatCameraTargeting/TagMinigameController) para que la cámara gire hacia Will
        // mientras dura la línea, y el animTrigger de SpeechBubbleUI.Show para que gesticule
        // (funciona tanto si Will es NPC como si es el personaje controlado).
        var thirdPersonCamera = ServiceLocator.Get<vThirdPersonCamera>(false);
        if (thirdPersonCamera != null)
        {
            thirdPersonCamera.SetLockTarget(willTransform);
            _weSetCameraLock = true;
        }

        bool bubbleDone = false;
        SpeechBubbleUI.Instance.Show(willTransform, text, BubbleDuration,
            onComplete: () => bubbleDone = true,
            animTrigger: ReactionAnimTrigger);

        yield return new WaitUntil(() => bubbleDone);

        ReleaseCameraLockIfOurs(thirdPersonCamera);
    }

    private static void ReleaseCameraLockIfOurs(vThirdPersonCamera cam)
    {
        if (!_weSetCameraLock) return;
        _weSetCameraLock = false;
        if (cam != null) cam.ClearLockTarget();
    }

    /// <summary>
    /// Devuelve el Transform de Will independientemente del personaje activo:
    /// si Will no es el personaje controlado, existe como NPC instanciado (WillNpcInstance);
    /// si Will SÍ es el personaje activo, es directamente el jugador.
    /// </summary>
    private static Transform GetWillTransform()
    {
        var willNpc = ActiveCharacterSwapper.Instance != null ? ActiveCharacterSwapper.Instance.WillNpcInstance : null;
        if (willNpc != null) return willNpc.transform;

        return PlayerService.Player != null ? PlayerService.Player.transform : null;
    }

    /// <summary>MonoBehaviour mínimo, creado solo para poder lanzar la corrutina.</summary>
    private class Runner : MonoBehaviour
    {
        // Red de seguridad: si el Runner se destruye a mitad de Co_ShowReaction (ej. cierre del
        // juego) y nosotros habíamos puesto el lock-on de cámara, lo liberamos para no dejar la
        // cámara bloqueada en Will.
        void OnDestroy()
        {
            ReleaseCameraLockIfOurs(ServiceLocator.Get<vThirdPersonCamera>(false));
        }
    }
}
