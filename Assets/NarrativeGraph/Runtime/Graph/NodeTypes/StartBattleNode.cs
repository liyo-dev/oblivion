// StartBattleNode.cs
using System;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Serialization;

/// <summary>
/// Nodo que inicia una batalla (boss arena) y espera a la señal de victoria para avanzar.
/// - Activa por battleId buscando en escenas cargadas (tanto activos como inactivos), o por referencia directa.
/// - Opcionalmente completa/avanza una misión/step cuando se gana la batalla.
/// </summary>
[Serializable]
public sealed class StartBattleNode : NarrativeNode
{
    [Tooltip("ID de la batalla (coincide con BossArenaController.BattleId).")]
    public string battleId;

    [Tooltip("Referencia directa a una arena existente en la escena (fallback).")]
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
    [Tooltip("Identificador que se usará al suscribirse a OnBattleWon. Si está vacío, se usará battleId o el de la arena.")]
    public string arenaContext = "";

    // --- Estado interno ---
    Action _onBattleWonCb;
    INarrativeSignals _subscribedSignals;
    object _usedContextKey; // clave usada al suscribirse (para desuscribirse)
    bool _subscriptionOk;

    public override void Enter(NarrativeContext ctx, Action onReadyToAdvance)
    {
        // Proveedor de señales
        INarrativeSignals signals = ctx?.Signals ?? DefaultNarrativeSignals.Instance;
        if (signals == null)
        {
            Debug.LogWarning("[StartBattleNode] No hay proveedor de señales. Avanzo para no bloquear.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Resolver id de contexto
        string derivedId = !string.IsNullOrEmpty(arenaContext)
            ? arenaContext
            : (!string.IsNullOrEmpty(battleId) ? battleId
               : bossArena != null && !string.IsNullOrEmpty(bossArena.BattleId) ? bossArena.BattleId
               : "__DEFAULT_BOSS__");

        var contextKey = derivedId;
        _usedContextKey = contextKey;
        
        // Preparar callback de victoria
        _onBattleWonCb = () =>
        {
            try
            {
                if (completeMission)
                {
                    bool done = false;

                    // 1) step (string, int)
                    if (!string.IsNullOrEmpty(missionStringId) && missionId >= 0)
                    {
                        try
                        {
                            done = TryCompleteMissionStepByReflection(missionStringId, missionId);
                            if (done) Debug.Log($"[StartBattleNode] MarkStepDone('{missionStringId}', {missionId}) al ganar.");
                        }
                        catch (Exception e)
                        {
                            Debug.LogWarning($"[StartBattleNode] Error completando step (reflexión): {e.Message}");
                        }
                    }

                    // 2) quest por string vía señales/reflexión
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
                            Debug.LogWarning($"[StartBattleNode] Error completando misión (string): {e.Message}");
                        }
                    }

                    // 3) quest por int
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
                        Debug.LogWarning($"[StartBattleNode] No se pudo completar misión/step (string='{missionStringId}', id={missionId}).");
                    }
                }
            }
            finally
            {
                onReadyToAdvance?.Invoke();
                _onBattleWonCb = null;
            }
        };

        // Suscribirse antes de lanzar
        try
        {
            signals.OnBattleWon(contextKey, _onBattleWonCb);
            _subscribedSignals = signals;
            _subscriptionOk = true;
            Debug.Log($"[StartBattleNode] Suscrito a OnBattleWon con context='{contextKey}'.");
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error suscribiéndose a OnBattleWon: {ex.Message}. Avanzo.");
            onReadyToAdvance?.Invoke();
            return;
        }

        // Disparar batalla
        bool triggered = false;
        BossArenaController targetArena = null;

        // 1) Buscar por id en escenas cargadas (incluye inactivos)
        if (useBattleById && !string.IsNullOrEmpty(battleId))
        {
            targetArena = TryFindArenaInLoadedScenesById(battleId);
        }

        // 2) Fallback: referencia directa si es válida
        if (targetArena == null && bossArena != null && bossArena.gameObject.scene.IsValid())
        {
            targetArena = bossArena;
        }

        // 3) Verificar si la batalla ya fue ganada (al restaurar desde save)
        if (targetArena != null && IsBattleAlreadyWon(targetArena))
        {
            Debug.Log($"[StartBattleNode] Batalla '{battleId}' ya fue ganada. Avanzando inmediatamente sin disparar.");
            SafeUnsubscribe(ctx);
            onReadyToAdvance?.Invoke();
            return;
        }

        // 4) Trigger batalla
        if (targetArena != null)
        {
            triggered = TriggerArena(targetArena);
            
                    
            DefaultNarrativeSignals.Instance?.RaiseCustom($"BATTLE_START:{battleId}");
        }

        if (!triggered)
        {
            Debug.LogWarning("[StartBattleNode] No se activó la arena (id vacío/no encontrada y sin fallback). Desuscribo y avanzo.");
            SafeUnsubscribe(ctx);
            onReadyToAdvance?.Invoke();
        }
    }

    bool IsBattleAlreadyWon(BossArenaController arena)
    {
        if (arena == null) return false;
        
        // Verificar en el tracker si ya fue derrotado
        // Usar BattleId que es público (bossId es privado)
        if (!string.IsNullOrEmpty(arena.BattleId))
        {
            if (BossProgressTracker.TryGetInstance(out var tracker))
            {
                return tracker.IsDefeated(arena.BattleId);
            }
        }
        
        return false;
    }

    bool TriggerArena(BossArenaController arena)
    {
        if (arena == null) return false;
        try
        {
            arena.TriggerStartBattle();
            Debug.Log("[StartBattleNode] TriggerStartBattle() ejecutado en arena encontrada.");
            return true;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[StartBattleNode] Error en TriggerStartBattle(): {ex.Message}");
            return false;
        }
    }

    public override void Exit(NarrativeContext ctx)
    {
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

    // -------- BÚSQUEDA EN ESCENAS CARGADAS --------

    static BossArenaController TryFindArenaInLoadedScenesById(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        // 1) Búsqueda rápida global (incluye inactivos) si tu versión la soporta
        try
        {
            var all = ServiceLocator.GetAll<BossArenaController>();
            foreach (var a in all)
            {
                if (a != null && !string.IsNullOrEmpty(a.BattleId) && string.Equals(a.BattleId, id, StringComparison.Ordinal))
                    return a;
            }
        }
        catch { /* fallback abajo */ }

        // 2) Fallback: iterar todas las escenas cargadas (root → children)
        int count = SceneManager.sceneCount;
        for (int i = 0; i < count; i++)
        {
            Scene sc = SceneManager.GetSceneAt(i);
            if (!sc.IsValid() || !sc.isLoaded) continue;

            var roots = sc.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                var arena = roots[r].GetComponentInChildren<BossArenaController>(true);
                if (arena != null && !string.IsNullOrEmpty(arena.BattleId) &&
                    string.Equals(arena.BattleId, id, StringComparison.Ordinal))
                {
                    return arena;
                }
            }
        }

        return null;
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
        var behaviours = ServiceLocator.GetAll<MonoBehaviour>();
        foreach (var mb in behaviours)
        {
            var t = mb.GetType();
            if (t == mgrType || t.IsSubclassOf(mgrType))
                return mb;
        }
        return null;
    }
}
