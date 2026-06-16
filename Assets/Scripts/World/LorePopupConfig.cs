using UnityEngine;

[System.Serializable]
public struct LoreEntry
{
    [Tooltip("Retrato del personaje. Opcional.")]
    public Sprite portrait;

    [Tooltip("ID de localización del texto (ej: 'LORE_TEMPLE_01'). Si vacío, usa text.")]
    public string textId;

    [Tooltip("Texto de la entrada de lore.")]
    [TextArea(2, 6)]
    public string text;

    [Tooltip("Segundos que esta entrada permanece visible antes de pasar a la siguiente (o cerrar).")]
    [Min(0.5f)]
    public float duration;
}

[CreateAssetMenu(menuName = "El Sendero/Juego/Lore Popup Config", fileName = "LoreCfg_")]
public class LorePopupConfig : ScriptableObject
{
    [Tooltip("Segundos de espera desde que se lanza el evento hasta que aparece el popup.")]
    [Min(0f)]
    public float delay = 0f;

    [Tooltip("Entradas de lore. Cada entrada es una página del popup.")]
    public LoreEntry[] entries;
}
