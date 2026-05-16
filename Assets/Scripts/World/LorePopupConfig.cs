using UnityEngine;

[System.Serializable]
public struct LoreEntry
{
    [Tooltip("Retrato del personaje. Opcional.")]
    public Sprite portrait;

    [Tooltip("ID de localización del nombre del hablante (ej: 'CHAR_ESTELA'). Si vacío, usa speakerName.")]
    public string speakerNameId;

    [Tooltip("Nombre del hablante (fallback si speakerNameId está vacío).")]
    public string speakerName;

    [Tooltip("ID de localización del texto (ej: 'LORE_TEMPLE_01'). Si vacío, usa text.")]
    public string textId;

    [Tooltip("Texto de la entrada de lore.")]
    [TextArea(2, 6)]
    public string text;

    [Tooltip("Segundos que esta entrada permanece visible antes de pasar a la siguiente (o cerrar).")]
    [Min(0.5f)]
    public float duration;
}

[CreateAssetMenu(menuName = "Game/Lore Popup Config", fileName = "LoreCfg_")]
public class LorePopupConfig : ScriptableObject
{
    [Tooltip("Entradas de lore. Cada entrada es una página del popup que el jugador avanza manualmente.")]
    public LoreEntry[] entries;
}
