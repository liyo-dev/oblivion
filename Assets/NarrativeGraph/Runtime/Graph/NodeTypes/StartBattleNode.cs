// StartBattleNode.cs
using System;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Nodo que inicia una batalla (boss arena) y espera a la señal de victoria para avanzar.
/// - Puede activar por battleId (vía BossArenaController.TryTriggerBattleById) o por referencia directa.
/// - Opcionalmente completa/avanza una misión/step cuando se gana la batalla.
/// </summary>
[Serializable]
public sealed class StartBattleNode : NarrativeNode
{
    [Tooltip("ID de la batalla (coincide con BossArenaController.BattleId).")]
    public string battleId;

    [Tooltip("Referencia directa a BossArenaController (fallback si no se activa por id).")]
    public BossArenaController bossArena;

    [Tooltip("Si true, intenta activar por battleId primero.")]
    public bool useBattleById = true;

    [Header("Misión opcional al ganar la batalla")]
    [FormerlySerializedAs("startMission")]
    public bool completeMission = false;

    [Tooltip("ID de misión/quest (string).")]
    public string missionStringId = "";

    [Tooltip("Entero auxiliar (por ejemplo stepIndex o missionId según tu sistema).")]
    public int missionId = 0;

    [Header("Contexto de la arena")]
    [Tooltip("Identificador que se usará al suscribirse a OnBattleWon. Si está vacío, se usará battleId.")]
    public string arenaContext = "";

    // --- Estado interno ---
    Action _onBattleWonCb;
    INarrativeSignals _subscribedSignals;
    object _usedContextKey; // la clave exacta usada al suscribirse (para desuscribirse)
    bool _subscriptionOk;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Proveedor de señales seguro: preferir el del runner; fallback a global
        var signals = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("[StartBattleNode] No hay proveedor de señales (ctx.Signals == null y DefaultNarrativeSignals.Instance == null). Avanzando para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Resolver la clave/objeto de contexto que usará la señal
        // Nota: aquí usamos string; si tu implementación de señales soporta objetos, esta clave debe ser la misma que emita la arena.
        var contextKey = string.IsNullOrEmpty(arenaContext) ? (string.IsNullOrEmpty(battleId) ? (object)"__DEFAULT_BOSS__" : battleId) : arenaContext;
        _usedContextKey = contextKey;

        // Preparar callback ANTES de disparar la batalla para evitar condiciones de carrera
        _onBattleWonCb = () =>
        {
            try
            {
                if (completeMission)
                {
                    bool done = false;

                    // 1) Intentar completar step por (string,int)
                    if (!string.IsNullOrEmpty(missionStringId) && missionId >= 0)
                    {
                        try
                        {
                            done = TryCompleteMissionStepByReflection(missionStringId, missionId);
                            if (done) Debug.Log($"[StartBattleNode] MarkStepDone('{missionStringId}', {missionId}) ejecutado al ganar la batalla.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[StartBattleNode] Error completando step por reflexión: {e.Message}");
                        }
                    }

                    // 2) Si no, intentar completar quest por string vía señales o reflexión
                    if (!done && !string.IsNullOrEmpty(missionStringId))
                    {
                        try
                        {
                            var s = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
                            if (s != null)
                            {
                                s.CompleteQuest(missionStringId);
                                Debug.Log($"[StartBattleNode] CompleteQuest('{missionStringId}') vía Signals al ganar.");
                                done = true;
                            }
                            else
                            {
                                done = TryCompleteMissionByReflectionString(missionStringId);
                            }
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[StartBattleNode] Error completando misión por string: {e.Message}");
                        }
                    }

                    // 3) Fallback: completar por id (int) si procede
                    if (!done && missionId != 0)
                    {
                        try
                        {
                            done = TryCompleteMissionByReflection(missionId);
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[StartBattleNode] Error completando misión por id: {e.Message}");
                        }
                    }

                    if (!done)
                    {
                        Debug.LogWarning($"[StartBattleNode] No se pudo completar la misión/step (string='{missionStringId}', id={missionId}).");
                    }
                }
            }
            finally
            {
                // Avanzar SIEMPRE el grafo aunque la misión falle para no bloquear narrativa
                onReadyToAdvance?.Invoke();
                _onBattleWonCb = null;
            }
        };

        // Suscribirse primero
        try
        {
            signals.OnBattleWon(contextKey, _onBattleWonCb);
            _subscribedSignals = signals;
            _subscriptionOk = true;
            Debug.Log($"[StartBattleNode] Suscrito a OnBattleWon con context='{contextKey}'.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error suscribiéndose a OnBattleWon: {ex.Message}. Avanzando para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Ahora, intentar disparar la batalla
        bool triggered = false;

        if (useBattleById && !string.IsNullOrEmpty(battleId))
        {
            try
            {
                Debug.Log($"[StartBattleNode] Intentando activar arena por id: '{battleId}' (len={battleId.Length}).");
                triggered = BossArenaController.TryTriggerBattleById(battleId);
                if (triggered)
                    Debug.Log($"[StartBattleNode] Arena activada por id: {battleId}");
                else
                    Debug.LogWarning($"[StartBattleNode] No se encontró arena con BattleId '{battleId}'.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[StartBattleNode] Error intentando activar por id '{battleId}': {ex.Message}");
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
            Debug.LogWarning("[StartBattleNode] No se activó la arena (battleId vacío/no encontrado y bossArena null). Para no bloquear, avanzamos y limpiamos suscripción.");
            // Nos desuscribimos y avanzamos para no romper flujo si no se pudo iniciar la batalla
            SafeUnsubscribe(ctx);
            onReadyToAdvance?.Invoke();
        }
    }

    public override void Exit(NarrativeContext ctx)
    {
        // Limpieza: desuscribir si sigue activa la suscripción
        SafeUnsubscribe(ctx);
    }

    void SafeUnsubscribe(NarrativeContext ctx)
    {
        if (_onBattleWonCb != null && _subscriptionOk)
        {
            try
            {
                var s = _subscribedSignals ?? ctx?.Signals ?? DefaultNarrativeSignals.Instance;
                s?.OffBattleWon(_usedContextKey, _onBattleWonCb);
                Debug.Log($"[StartBattleNode] Desuscrito de OnBattleWon para context='{_usedContextKey}'.");
            }
            catch { /* silencioso */ }
            finally
            {
                _onBattleWonCb = null;
                _subscriptionOk = false;
                _subscribedSignals = null;
                _usedContextKey = null;
            }
        }
    }

    // ===== Utilidades por reflexión (compatibilidad con distintos gestores) =====

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

            object instance = GetSingletonOrSceneInstance(mgrType);
            if (instance == null) return false;

            string[] methodNames = {
                "CompleteMission","CompleteMissionById","CompleteQuest","CompleteQuestById",
                "CompleteMissionStep","AdvanceMissionStep","AdvanceMission","CompleteStep","ActivateMission"
            };

            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(int))
                {
                    method.Invoke(instance, new object[] { id });
                    Debug.Log($"[StartBattleNode] Invocado {name}({id}) en MissionManager.");
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error completando misión por reflexión: {ex.Message}");
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

            object instance = GetSingletonOrSceneInstance(mgrType);
            if (instance == null) return false;

            string[] methodNames = {
                "CompleteQuest","CompleteQuestById","CompleteMission","CompleteMissionById",
                "CompleteMissionStep","AdvanceMissionStep","AdvanceMission","CompleteStep","StartMission","ActivateMission"
            };

            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 1 && pars[0].ParameterType == typeof(string))
                {
                    method.Invoke(instance, new object[] { id });
                    Debug.Log($"[StartBattleNode] Invocado {name}(\"{id}\") en MissionManager.");
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error completando misión por reflexión (string): {ex.Message}");
            return false;
        }
    }

    bool TryCompleteMissionStepByReflection(string questId, int stepIndex)
    {
        try
        {
            var mgrType = Type.GetType("QuestManager");
            if (mgrType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    mgrType = asm.GetType("QuestManager");
                    if (mgrType != null) break;
                }
            }
            if (mgrType == null) return false;

            object instance = GetSingletonOrSceneInstance(mgrType);
            if (instance == null) return false;

            string[] methodNames = { "MarkStepDone","CompleteStep","CompleteMissionStep","AdvanceMissionStep","OnStepCompleted","MarkStep" };
            foreach (var name in methodNames)
            {
                var method = mgrType.GetMethod(name, System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Static);
                if (method == null) continue;
                var pars = method.GetParameters();
                if (pars.Length == 2 && pars[0].ParameterType == typeof(string) && pars[1].ParameterType == typeof(int))
                {
                    method.Invoke(instance, new object[] { questId, stepIndex });
                    Debug.Log($"[StartBattleNode] Invocado {name}(\"{questId}\", {stepIndex}) en QuestManager.");
                    return true;
                }
            }
            return false;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error completando step por reflexión: {ex.Message}");
            return false;
        }
    }

    static object GetSingletonOrSceneInstance(Type mgrType)
    {
        object instance = null;
        var prop = mgrType.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        if (prop != null) instance = prop.GetValue(null);
        else
        {
            var field = mgrType.GetField("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            if (field != null) instance = field.GetValue(null);
        }

        if (instance != null) return instance;

        // Buscar en escena algún componente de ese tipo (o derivado)
        var behaviours = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>();
        foreach (var mb in behaviours)
        {
            var t = mb.GetType();
            if (t == mgrType || t.IsSubclassOf(mgrType))
                return mb;
        }
        return null;
    }
}
