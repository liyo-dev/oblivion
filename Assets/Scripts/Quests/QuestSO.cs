using UnityEngine;

[CreateAssetMenu(menuName = "Game/Quest", fileName = "Quest_")]
public class QuestSO : ScriptableObject
{
    [Tooltip("ID único de la misión")]
    public string questId;

    public string title;

    [TextArea]
    public string description;

    [Min(1)]
    public int stages = 1; // nº de pasos; se completa al llegar a 'stages'
}