using UnityEngine;

[CreateAssetMenu(menuName="Game/Config", fileName="GameConfig")]
public class GameConfigSO : ScriptableObject
{
    [Header("Spawn inicial si no hay partida")]
    public string defaultSpawnAnchorId = "Bedroom";
    [Tooltip("Tag del jugador para localizarlo")]
    public string playerTag = "Player";
}