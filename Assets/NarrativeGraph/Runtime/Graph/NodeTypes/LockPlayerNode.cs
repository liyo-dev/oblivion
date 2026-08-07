using System;
using UnityEngine;
using Sendero.UI;

/// <summary>
/// Bloquea o desbloquea el movimiento del jugador empujando/sacando ActionMode.Cinematic.
/// Bloquea: Move, Sprint, Jump, Interact.
/// Colocar un nodo con bloquear=true antes del momento crítico y otro con bloquear=false después.
///
/// FIX: PlayerActionManager.OnTopModeChanged no tiene ningún suscriptor que oculte el HUD —
/// PlayerHUDV2 se oculta/muestra solo cuando cada sistema (DialogueManager,
/// CinematicSequencerBase, etc.) lo llama explícitamente. Este nodo es el bloqueo genérico
/// recomendado por CLAUDE.md §7 para secuencias construidas en NarrativeGraph (ej. combinado con
/// ShowSpeechBubbleNode), así que sin este HideHUD/ShowHUD el HUD se quedaba visible durante esas
/// secuencias. HideHUD()/ShowHUD() son idempotentes (guard interno _isVisible), igual que en
/// CinematicSequencerBase.LockCinematic/EndCinematic, así que es seguro emparejarlos aquí con el
/// Push/Pop sin coordinarse con los demás sistemas que también ocultan el HUD.
/// </summary>
[Serializable]
public sealed class LockPlayerNode : NarrativeNode
{
    public bool bloquear = true;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        if (PlayerService.TryGetPlayer(out var playerGo, false))
        {
            var pam = playerGo.GetComponent<PlayerActionManager>();
            if (pam != null)
            {
                if (bloquear)
                    pam.PushMode(ActionMode.Cinematic);
                else
                    pam.PopMode(ActionMode.Cinematic);
            }
        }

        if (bloquear)
            PlayerHUDV2.Instance?.HideHUD();
        else
            PlayerHUDV2.Instance?.ShowHUD();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LockPlayerNode] jugador {(bloquear ? "bloqueado" : "desbloqueado")} (ActionMode.Cinematic), HUD {(bloquear ? "oculto" : "restaurado")}");
#endif
        onReadyToAdvance?.Invoke();
    }
}
