using UnityEngine;

/// <summary>
/// Resolución compartida de "en qué layer del Animator vive realmente este estado", para
/// personajes con una Base Layer (cuerpo completo, capa 0) y una UpperBody layer (torso/brazos,
/// sin piernas, vía AvatarMask — normalmente capa 1). Antes de esta clase, esta misma lógica de
/// 6 líneas (probar la capa preferida, si no existe caer a la capa 0) estaba copiada y pegada de
/// forma casi idéntica en NPCSimpleAnimator.PlaySocialGesture(), PromoVideo01Sequencer.CapaDelEstado()
/// y PlayerDialogueAnimator.CapaDelEstado() — ver claude/dedup-resolucion-layer-2026-08-30.md.
///
/// NO sustituye a AnimatorStateCache (que recorre TODAS las capas y cachea resultados para
/// nombres con alias/variantes de ruta) ni a los sistemas de resolución de capa de vuelo
/// (FollowPlayerState.DetectFlightLayer / PlayerFlyingController.DetectFlightLayer, que buscan
/// una capa que contenga uno de varios estados candidatos) — esos resuelven un problema distinto
/// y se quedan como están.
/// </summary>
public static class AnimatorLayerUtil
{
    /// <summary>
    /// Resuelve en qué layer vive realmente el estado con hash <paramref name="stateHash"/>:
    /// prueba primero <paramref name="preferredLayer"/> (por defecto 1, la UpperBody layer) y si
    /// no existe ahí cae al Base Layer (capa 0). Devuelve -1 si no existe en ninguna de las dos.
    /// </summary>
    public static int ResolveLayer(Animator animator, int stateHash, int preferredLayer = 1)
    {
        if (animator == null)
            return -1;

        if (preferredLayer > 0 && animator.layerCount > preferredLayer
            && animator.HasState(preferredLayer, stateHash))
        {
            return preferredLayer;
        }

        if (animator.HasState(0, stateHash))
            return 0;

        return -1;
    }

    /// <summary>
    /// Igual que <see cref="ResolveLayer(Animator, int, int)"/> pero a partir del nombre de
    /// estado en vez de su hash ya calculado.
    /// </summary>
    public static int ResolveLayer(Animator animator, string stateName, int preferredLayer = 1)
    {
        if (string.IsNullOrEmpty(stateName))
            return -1;

        return ResolveLayer(animator, Animator.StringToHash(stateName), preferredLayer);
    }
}
