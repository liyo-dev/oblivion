using UnityEngine;

public enum QuestState { NotStarted, Active, Completed }

[CreateAssetMenu(menuName="Game/Quest", fileName="Quest_")]
public class QuestSO : ScriptableObject
{
    public string questId;     // único
    public string title;
    [TextArea] public string description;
    [Min(1)] public int stages = 1; // nº pasos; completa al llegar a stages
}