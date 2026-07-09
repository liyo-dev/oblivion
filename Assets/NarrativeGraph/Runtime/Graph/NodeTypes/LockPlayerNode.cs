using System;
using UnityEngine;

/// <summary>
/// Bloquea o desbloquea el movimiento del jugador empujando/sacando ActionMode.Cinematic.
/// Bloquea: Move, Sprint, Jump, Interact.
/// Colocar un nodo con bloquear=true antes del momento crítico y otro con bloquear=false después.
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        Debug.Log($"[LockPlayerNode] jugador {(bloquear ? "bloqueado" : "desbloqueado")} (ActionMode.Cinematic)");
#endif
        onReadyToAdvance?.Invoke();
    }
}
