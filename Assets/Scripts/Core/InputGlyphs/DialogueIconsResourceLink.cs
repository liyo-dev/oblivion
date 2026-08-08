using TMPro;
using UnityEngine;

namespace Core.InputGlyphs
{
    /// <summary>
    /// Puntero minúsculo, colocado dentro de una carpeta Resources, que apunta al
    /// <c>DialogueIcons.asset</c> real (que vive en <c>Assets/Art/UI/DialogueIcons/</c>, fuera de
    /// cualquier carpeta Resources). Existe solo para poder hacer <c>Resources.Load</c> sin tener que
    /// mover el asset original de carpeta: moverlo físicamente sin pasar por el Editor de Unity (que
    /// es como se ha hecho este cambio, sin acceso al Editor en esta sesión) arriesgaría a que el
    /// archivo original y el movido coexistiesen un instante con el mismo GUID y Unity se liara con
    /// un conflicto de GUID duplicado. Con este puntero no hace falta tocar el asset original para
    /// nada — solo referenciarlo — así que ese riesgo desaparece del todo.
    /// </summary>
    public sealed class DialogueIconsResourceLink : ScriptableObject
    {
        public TMP_SpriteAsset dialogueIcons;
    }
}
