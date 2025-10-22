using System;
using System.Reflection;
using UnityEngine;

// Puente pequeño y robusto para ejecutar acciones al terminar una cinemática.
// - Se suscribe al evento público OnCinematicFinished de AdditiveSceneCinematic si está asignado.
// - Expone un método público "OnCinematicFinishedInspector" para poder conectarlo desde el UnityEvent inspector.
// - Llama a BossArenaController.TriggerStartBattle() si se asigna.
// - Intenta iniciar la misión indicada por reflexión para no depender de un tipo concreto en tiempo de compilación.
public class CinematicEndBridge : MonoBehaviour
{
    [Tooltip("Referencia al componente AdditiveSceneCinematic (puedes dejarlo en null y conectar desde el inspector con el UnityEvent)")]
    public AdditiveSceneCinematic additiveCinematic;

    [Tooltip("Referencia al BossArenaController que debe arrancar la batalla")]
    public BossArenaController bossArena;

    [Header("Battle Target by Id")]
    [Tooltip("Si se desea, activar una arena por su BattleId en vez de referenciar directamente el BossArenaController.")]
    public bool triggerBattleById = true;
    [Tooltip("ID de la batalla a activar (coincide con BossArenaController.BattleId)")]
    public string targetBattleId;

    [Tooltip("ID numérico de la misión a iniciar (si tu sistema de misiones acepta un int).")]
    public int missionId = 3;

    [Tooltip("Si tu sistema de misiones usa string IDs (ej: 'ELDRAN_MISSION3'), escribe el id aquí. Si no está vacío, se intentará usarlo antes que el numeric id.")]
    public string missionStringId = "";
    [Tooltip("Si true, intentará iniciar la misión usando el missionStringId (si está definido).")]
    public bool startMissionByString = true;

    [Tooltip("Si true, llamará a TriggerStartBattle() en bossArena cuando la cinemática termine.")]
    public bool startBossBattle = true;

    [Tooltip("Si true, intentará iniciar la misión mediante reflexión.")]
    public bool startMission = true;

    void Reset()
    {
        if (additiveCinematic == null) additiveCinematic = GetComponentInChildren<AdditiveSceneCinematic>();
        if (bossArena == null) bossArena = GetComponentInParent<BossArenaController>();
    }

    void OnEnable()
    {
        if (additiveCinematic != null)
            additiveCinematic.OnCinematicFinished += HandleCinematicFinished;
    }

    void OnDisable()
    {
        if (additiveCinematic != null)
            additiveCinematic.OnCinematicFinished -= HandleCinematicFinished;
    }

    // Método que maneja el fin de la cinemática (suscrito por código)
    public void HandleCinematicFinished()
    {
        Debug.Log("[CinematicEndBridge] Cinemática finalizada. Ejecutando acciones de puente.");

        if (startBossBattle)
        {
            bool triggered = false;

            if (triggerBattleById && !string.IsNullOrEmpty(targetBattleId))
            {
                triggered = BossArenaController.TryTriggerBattleById(targetBattleId);
                if (triggered)
                    Debug.Log($"[CinematicEndBridge] Arena activada por id: {targetBattleId}");
                else
                    Debug.LogWarning($"[CinematicEndBridge] No se encontró arena con BattleId '{targetBattleId}'.");
            }

            // Fallback: si no se logró por id y hay referencia directa, usarla
            if (!triggered)
            {
                if (bossArena != null)
                {
                    bossArena.TriggerStartBattle();
                    Debug.Log("[CinematicEndBridge] TriggerStartBattle() llamado en BossArenaController (fallback referencia directa).");
                }
                else if (!triggered)
                {
                    Debug.LogWarning("[CinematicEndBridge] startBossBattle está activo pero no hay BossArenaController asignado ni se encontró arena por id.");
                }
            }
        }

        if (startMission)
        {
            bool startedOk = false;
            if (startMissionByString && !string.IsNullOrEmpty(missionStringId))
            {
                startedOk = TryStartMissionByReflectionString(missionStringId);
                if (!startedOk)
                    Debug.LogWarning($"[CinematicEndBridge] No se pudo iniciar la misión por string id '{missionStringId}'. Intentando por numeric id...");
            }

            if (!startedOk)
            {
                if (!TryStartMissionByReflection(missionId))
                {
                    Debug.LogWarning($"[CinematicEndBridge] No se pudo iniciar la misión {missionId}. Verifica el MissionManager o inicia la misión manualmente desde el inspector.");
                }
            }
        }
    }

    // Método público para poder conectarlo directamente desde el UnityEvent de AdditiveSceneCinematic
    public void OnCinematicFinishedInspector()
    {
        HandleCinematicFinished();
    }

    // Intenta encontrar un 'MissionManager' y llamar a StartMission(int) o métodos similares por reflexión.
    bool TryStartMissionByReflection(int id)
    {
        try
        {
            Type mgrType = null;

            // Intentar por nombre simple
            mgrType = Type.GetType("MissionManager");

            // Si no se encontró, buscar en los ensamblados cargados
            if (mgrType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    mgrType = asm.GetType("MissionManager");
                    if (mgrType != null) break;
                }
            }

            if (mgrType == null) return false;

            object instance = null;
            var prop = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) instance = prop.GetValue(null);
            else
            {
                var field = mgrType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (field != null) instance = field.GetValue(null);
            }

            // Si no hay singleton estático, intentar FindObjectOfType
            if (instance == null)
            {
                var monoBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in monoBehaviours)
                {
                    if (mb.GetType() == mgrType || mb.GetType().IsSubclassOf(mgrType))
                    {
                        instance = mb;
                        break;
                    }
                }
            }

            if (instance == null) return false;

            // Buscar métodos comunes
            string[] methodNames = new[] { "StartMission", "StartMissionById", "BeginMission", "ActivateMission" };
            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                {
                    method.Invoke(instance, new object[] { id });
                    Debug.Log($"[CinematicEndBridge] Invocado {name}({id}) en MissionManager.");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CinematicEndBridge] Error intentando iniciar misión por reflexión: {ex.Message}");
            return false;
        }
    }

    // Variante que busca métodos con firma (string) y los invoca con el missionStringId
    bool TryStartMissionByReflectionString(string id)
    {
        try
        {
            Type mgrType = null;

            mgrType = Type.GetType("MissionManager");
            if (mgrType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    mgrType = asm.GetType("MissionManager");
                    if (mgrType != null) break;
                }
            }

            if (mgrType == null) return false;

            object instance = null;
            var prop = mgrType.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            if (prop != null) instance = prop.GetValue(null);
            else
            {
                var field = mgrType.GetField("Instance", BindingFlags.Public | BindingFlags.Static);
                if (field != null) instance = field.GetValue(null);
            }

            if (instance == null)
            {
                var monoBehaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
                foreach (var mb in monoBehaviours)
                {
                    if (mb.GetType() == mgrType || mb.GetType().IsSubclassOf(mgrType))
                    {
                        instance = mb;
                        break;
                    }
                }
            }

            if (instance == null) return false;

            // Buscar métodos comunes que tomen string
            string[] methodNames = new[] { "StartQuest", "StartQuestById", "StartMissionById", "StartMission", "BeginMission", "ActivateMission" };
            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                {
                    method.Invoke(instance, new object[] { id });
                    Debug.Log($"[CinematicEndBridge] Invocado {name}(\"{id}\") en MissionManager.");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[CinematicEndBridge] Error intentando iniciar misión por reflexión (string): {ex.Message}");
            return false;
        }
    }
}
