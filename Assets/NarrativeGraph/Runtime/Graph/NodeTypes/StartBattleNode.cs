// StartBattleNode.cs
using System;
using UnityEngine.Serialization;
using UnityEngine;

[Serializable]
public sealed class StartBattleNode : NarrativeNode
{
    [Tooltip("ID de la batalla (coincide con BossArenaController.BattleId). Si se usa, el nodo intentará activar la arena por id.")]
    public string battleId;

    [Tooltip("Referencia directa a BossArenaController (fallback si no se activa la arena por id)")]
    public BossArenaController bossArena;

    [Tooltip("Si true, intenta activar por battleId primero.")]
    public bool useBattleById = true;

    [Tooltip("Opcional: completar/avanzar una misión o step asociado tras activar la arena (si tu sistema soporta esto desde el grafo).")]
    [FormerlySerializedAs("startMission")]
    public bool completeMission = false;
    public string missionStringId = "";
    public int missionId = 0;

    // Contexto que se pasará a las señales para identificar la arena (puede ser el battleId o un objeto serializable)
    public object arenaContext;
    Action _onBattleWonCb;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        bool triggered = false;

        if (useBattleById && !string.IsNullOrEmpty(battleId))
        {
            try
            {
                // Diagnostic log: mostrar el valor que intentamos activar
                Debug.Log($"[StartBattleNode] Intentando activar arena por id: '{battleId}' (length={battleId.Length})");

                triggered = BossArenaController.TryTriggerBattleById(battleId);
                if (triggered)
                    Debug.Log($"[StartBattleNode] Arena activada por id: {battleId}");
                else
                    Debug.LogWarning($"[StartBattleNode] No se encontró arena con BattleId '{battleId}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StartBattleNode] Error intentando activar arena por id '{battleId}': {ex.Message}");
            }
        }

        if (!triggered && bossArena != null)
        {
            try
            {
                bossArena.TriggerStartBattle();
                triggered = true;
                Debug.Log("[StartBattleNode] TriggerStartBattle() llamado en BossArenaController (referencia directa).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StartBattleNode] Error llamando TriggerStartBattle(): {ex.Message}");
            }
        }

        if (!triggered)
        {
            Debug.LogWarning("[StartBattleNode] No se activó la arena (battleId vacío / no encontrado y bossArena null).");
        }

        // Opcional: completar misión/avanzar steps (si lo configuras en el nodo)
        if (completeMission)
        {
            // Intentamos, mediante reflexión, invocar métodos comunes que completen/avancen misiones
            bool completed = false;
            if (!string.IsNullOrEmpty(missionStringId))
            {
                completed = TryCompleteMissionByReflectionString(missionStringId);
            }
            if (!completed && missionId != 0)
            {
                completed = TryCompleteMissionByReflection(missionId);
            }
            if (!completed)
            {
                Debug.LogWarning($"[StartBattleNode] No se pudo completar la misión (string='{missionStringId}', id={missionId}).");
            }
        }

        // Cuando iniciamos la batalla, no avanzamos inmediatamente: nos suscribimos a la señal de victoria
        // y llamaremos a onReadyToAdvance sólo cuando el boss sea vencido.
        _onBattleWonCb = () =>
        {
            try { onReadyToAdvance?.Invoke(); }
            finally { _onBattleWonCb = null; }
        };

        // Si no se proporcionó arenaContext, usar battleId como identificador por defecto
        if (arenaContext == null && !string.IsNullOrEmpty(battleId)) arenaContext = battleId;

        try
        {
            ctx.Signals.OnBattleWon(arenaContext, _onBattleWonCb);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error suscribiéndose a OnBattleWon: {ex.Message}");
            // En caso de error, avanzamos para no bloquear el grafo
            onReadyToAdvance?.Invoke();
        }
    }

    public override void Exit(NarrativeContext ctx)
    {
        if (_onBattleWonCb != null)
        {
            try { ctx.Signals.OffBattleWon(arenaContext, _onBattleWonCb); } catch { }
            _onBattleWonCb = null;
        }
    }

    // Copiadas utilidades de CinematicEndBridge para intentar iniciar misiones por reflexión
    bool TryCompleteMissionByReflection(int id)
    {
        try
        {
            Type mgrType = Type.GetType("MissionManager");
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
            var prop = mgrType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null) instance = prop.GetValue(null);
            else
            {
                var field = mgrType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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

            // Métodos que podrían usarse para completar/avanzar misiones por id (int)
            string[] methodNames = new[] { "CompleteMission", "CompleteMissionById", "CompleteQuest", "CompleteQuestById", "CompleteMissionStep", "AdvanceMissionStep", "AdvanceMission", "CompleteStep", "ActivateMission" };
            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                {
                    method.Invoke(instance, new object[] { id });
                    Debug.Log($"[StartBattleNode] Invocado {name}({id}) en MissionManager (completar/avanzar). ");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error intentando completar misión por reflexión: {ex.Message}");
            return false;
        }
    }

    bool TryCompleteMissionByReflectionString(string id)
    {
        try
        {
            Type mgrType = Type.GetType("MissionManager");
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
            var prop = mgrType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (prop != null) instance = prop.GetValue(null);
            else
            {
                var field = mgrType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
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

            // Métodos que podrían usarse para completar/avanzar misiones por id (string)
            string[] methodNames = new[] { "CompleteQuest", "CompleteQuestById", "CompleteMission", "CompleteMissionById", "CompleteMissionStep", "AdvanceMissionStep", "AdvanceMission", "CompleteStep", "StartMission", "ActivateMission" };
            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                {
                    method.Invoke(instance, new object[] { id });
                    Debug.Log($"[StartBattleNode] Invocado {name}(\"{id}\") en MissionManager (completar/avanzar). ");
                    return true;
                }
            }

            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error intentando completar misión por reflexión (string): {ex.Message}");
            return false;
        }
    }
}
