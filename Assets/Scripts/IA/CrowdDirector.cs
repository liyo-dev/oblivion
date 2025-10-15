using UnityEngine;
using System.Collections.Generic;

public class CrowdDirector : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject npcPrefab;
    public int count = 20;
    public Transform[] spawnPoints;

    [Header("Reloj simple")]
    [Range(0,23)] public int hour = 9;
    public float secondsPerGameHour = 60f;

    [Header("Datos")]
    public AmbientSchedule[] schedules;
    public string[] preferencePools = { "Market", "Plaza", "Bench" };

    readonly List<AmbientAgent> _agents = new();

    void Start()
    {
        for (int i = 0; i < count; i++)
            SpawnOne();
    }

    void Update()
    {
        // Avanza la hora de juego
        hour = Mathf.FloorToInt((Time.time / secondsPerGameHour)) % 24;
        BroadcastHour();
    }

    void SpawnOne()
    {
        if (npcPrefab == null || spawnPoints == null || spawnPoints.Length == 0) return;

        var p = spawnPoints[Random.Range(0, spawnPoints.Length)];
        var go = Instantiate(npcPrefab, p.position, p.rotation, transform);
        var agent = go.GetComponent<AmbientAgent>();
        if (agent != null)
        {
            if (schedules != null && schedules.Length > 0)
                agent.schedule = schedules[Random.Range(0, schedules.Length)];

            agent.preferredTags = preferencePools; // puedes variar por “barrio”
            agent.SetHour(hour);
            _agents.Add(agent);
        }
    }

    void BroadcastHour()
    {
        // Recorremos de atrás hacia adelante para poder eliminar agentes nulos sin romper el índice
        for (int i = _agents.Count - 1; i >= 0; i--)
        {
            var a = _agents[i];
            if (a == null) _agents.RemoveAt(i);
            else a.SetHour(hour);
        }
    }
}
