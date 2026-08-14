using UnityEngine;

/// <summary>
/// Control manual de lluvia para demos/presentaciones en directo: evita depender del sorteo de
/// rainChance/forceRain de DayNightCycle (ver DayNightCycle.ApplyTimeOfDay) para poder enseñar el
/// refugio de NPCs (NPCShelterPoint / SeekShelterState) cuando el jurado esté jugando, sin
/// esperar a que llueva por azar durante la sesión.
///
/// Añadir a cualquier GameObject de la escena de la demo (uno solo por escena basta, p.ej. el
/// mismo objeto donde vive WorldBootstrap). Pulsar la tecla configurada llama a
/// DayNightCycle.ToggleRain() — la misma acción explícita que ya expone el propio DayNightCycle,
/// no duplica lógica de lluvia. ToggleRain() ya se encarga de no arrancar lluvia si hay un
/// minijuego activo (TagMinigameController.IsAnyMinigameActive) ni de reiniciarla si ya está
/// lloviendo, así que esta tecla es segura de pulsar en cualquier momento.
///
/// Nota de duración: StartRain() sin argumento usa 60s por defecto — si el jurado tarda en fijarse
/// en los NPCs, pulsar la tecla otra vez para parar la lluvia (o para forzarla de nuevo) en vez de
/// esperar a que se corte sola.
///
/// Solo para build de demo/presentación, no para el juego final: activo únicamente en el Editor o
/// en development builds (Application.isEditor || Debug.isDebugBuild) para no quedar expuesto sin
/// querer en una build de release.
/// </summary>
public class RainDemoTrigger : MonoBehaviour
{
    [Tooltip("Tecla que alterna la lluvia (empieza/para) durante la demo.")]
    public KeyCode toggleKey = KeyCode.F6;

    private DayNightCycle _cycle;

    void Awake()
    {
        _cycle = FindAnyObjectByType<DayNightCycle>();
        if (_cycle == null)
            Debug.LogWarning("[RainDemoTrigger] No se encontró ningún DayNightCycle en esta escena — la tecla de lluvia de demo no hará nada aquí.");
    }

    void Update()
    {
        if (!Application.isEditor && !Debug.isDebugBuild) return;
        if (_cycle == null) return;

        if (Input.GetKeyDown(toggleKey))
        {
            _cycle.ToggleRain();
            Debug.Log($"[RainDemoTrigger] Lluvia {(_cycle.IsRaining ? "activada" : "detenida")} manualmente ({toggleKey}).");
        }
    }
}
