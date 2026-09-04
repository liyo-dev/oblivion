using UnityEngine;

[System.Serializable]
public struct DialogueLine
{
    [Tooltip("ID de localización para el nombre del hablante (ej: 'CHAR_ALEX'). Si está vacío, usa speakerName directamente.")]
    public string speakerNameId;

    [Tooltip("ID de localización para el texto (ej: 'DLG_INTRO_01'). Si está vacío, usa 'text' directamente.")]
    public string textId;

    [TextArea]                            
    public string text;

    [Tooltip("Opcional")]
    public Sprite portrait;
    
    [Header("Cinematografía")]
    [Tooltip("¿Quién habla en esta línea? (true = jugador, false = NPC)")]
    public bool isPlayerSpeaking;

    [Tooltip("Solo diálogos grupales. Opcional: fuerza a quién mira quien habla en esta línea concreta, " +
             "por si el interlocutor natural (a quien respondería por defecto) no coincide con el " +
             "personaje al que en realidad se dirige el texto (ej: acusa a uno pero contesta a otro). " +
             "Mismo formato de ID que speakerNameId: DialogueCharacterId/PersistenceId/nombre del " +
             "personaje, o \"Player\"/\"MainNPC\". Vacío = comportamiento por defecto, sin cambios.")]
    public string lookAtOverrideId;

    [Header("Emociones")]
    [Tooltip("Emoción del NPC durante esta línea (None = sin cambio, mantiene la emoción anterior)")]
    public NPCEmotion emotion;
}