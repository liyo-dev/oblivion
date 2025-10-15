using System;
using System.Collections.Generic;
using UnityEngine;

// RoomDifficulty and RoomKind are defined centrally in Assets/Scripts/Core/Identifiers.cs

[CreateAssetMenu(menuName="Proc/Rooms Catalog")]
public class RoomCatalog : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public GameObject prefab;
        public RoomDifficulty difficulty;
        public RoomKind kind;
    }

    [Header("Prefabs especiales")]
    public GameObject startRoom;   // sala de inicio
    public GameObject bossRoom;    // sala de boss

    [Header("Todas las salas disponibles")]
    public List<Entry> rooms = new();
}