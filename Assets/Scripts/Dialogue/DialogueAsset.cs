using UnityEngine;

[CreateAssetMenu(menuName = "Game/Dialogue", fileName = "DG_")]
public class DialogueAsset : ScriptableObject
{
    public DialogueLine[] lines;

    [Header("Cinematografía")]
    [Tooltip("Activa el modo de conversación grupal: cámara elevada y alejada que encuadra a todos los personajes.")]
    public bool isGroupConversation = false;
}