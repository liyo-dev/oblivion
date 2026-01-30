# Fix: NPCs con Movimiento Constante

## 🎯 SOLUCIÓN REAL ENCONTRADA ⭐

**CAUSA RAÍZ**: ¡`NavMeshObstacle` mal configurados en los árboles del bosque!

### El Problema Real:
- Los árboles tienen `NavMeshObstacle` con **Move Threshold muy bajo**
- Esto causa que el NavMesh se regenere constantemente
- Los NPCs son **empujados físicamente**, generando velocidades de 200-500 unidades/s
- **TODOS** los NPCs cerca de árboles están afectados (incluso la araña sin scripts)

### La Solución:
✅ Usar **`NavMeshObstacleFixer.cs`** para configurar correctamente:
- **Move Threshold**: 1000 (muy alto, los árboles no se mueven)
- **Carve Only Stationary**: True
- **Time To Stationary**: 0.1s

📄 **Ver documento completo**: `FIX_NAVMESH_OBSTACLE_ARBOLES.md`

---

## 🐛 Problema Original (Velocidad Residual)

Los NPCs que tienen `NPCBehaviourManagerV2` con el módulo de combate (`NPCCombatConfig`) estaban moviéndose constantemente incluso cuando deberían estar quietos (en `IdleState`). 

### Causa Raíz

1. **NavMeshAgent con velocidad residual**: El NavMeshAgent mantiene velocidad residual o paths activos incluso después de ser detenido
2. **Sincronización automática**: `NPCSimpleAnimator.Update()` llama a `SyncWithNavMeshAgent()` cada frame, que lee la velocidad del NavMeshAgent
3. **Animaciones activadas por error**: Cualquier velocidad > 0 activaba animaciones de movimiento

## ✅ Soluciones Implementadas

### 1. NPCSimpleAnimator.SyncWithNavMeshAgent()
**Archivo**: `Assets/Scripts/Behaviour NPC/NPCSimpleAnimator.cs` (línea ~1272)

**Cambio**: Añadida verificación para solo sincronizar cuando el agente tiene un path activo:

```csharp
private void SyncWithNavMeshAgent()
{
    if (!navAgent.enabled || !navAgent.isOnNavMesh)
        return;
    
    // ✅ FIX CRÍTICO: Solo sincronizar si el agente tiene un path activo y no está detenido
    if (navAgent.isStopped || !navAgent.hasPath)
    {
        SetMovementSpeed(0f);
        return;
    }
    
    // ✅ Threshold más estricto para velocidad baja
    if (agentSpeed < movementThreshold * 0.5f)
    {
        SetMovementSpeed(0f);
        return;
    }
    
    // ... resto del código
}
```

**Efecto**: Previene que velocidad residual cause animaciones de movimiento.

---

### 2. NavMeshAgentUtility.HardStop()
**Archivo**: `Assets/Scripts/Behaviour NPC/Common/NavMeshAgentUtility.cs` (línea ~45)

**Cambio**: Limpieza más agresiva de velocidad:

```csharp
public static void HardStop(NavMeshAgent agent)
{
    if (agent == null)
        return;

    if (agent.isOnNavMesh)
    {
        agent.isStopped = true;
        agent.ResetPath();
    }

    // ✅ LIMPIEZA AGRESIVA
    agent.velocity = Vector3.zero;
    agent.nextPosition = agent.transform.position;
    
    // ✅ También limpiar la velocidad deseada
    if (agent.isOnNavMesh)
    {
        agent.SetDestination(agent.transform.position);
    }
}
```

**Efecto**: Elimina completamente cualquier residuo de movimiento del NavMeshAgent.

---

### 3. IdleState.OnUpdate()
**Archivo**: `Assets/Scripts/Behaviour NPC/States/IdleState.cs` (línea ~48)

**Cambio**: Verificación continua de detención en cada frame:

```csharp
public override void OnUpdate(NPCStateContext context)
{
    base.OnUpdate(context);
    
    _idleTimer += Time.deltaTime;
    
    // ✅ FIX CRÍTICO: Asegurar que el NavMeshAgent permanezca detenido
    if (context.Agent != null && context.Agent.enabled && context.Agent.isOnNavMesh)
    {
        if (!context.Agent.isStopped || context.Agent.hasPath || 
            context.Agent.velocity.sqrMagnitude > 0.01f)
        {
            // Forzar detención completa
            context.Agent.isStopped = true;
            context.Agent.ResetPath();
            context.Agent.velocity = Vector3.zero;
        }
    }
    
    // ... resto del código
}
```

**Efecto**: Garantiza que el NavMeshAgent permanezca detenido durante todo el tiempo en IdleState.

---

### 4. NPCBehaviourManagerV2.LateUpdate() - **NUEVO**
**Archivo**: `Assets/Scripts/Behaviour NPC/NPCBehaviourManagerV2.cs` (línea ~160)

**Cambio**: Safety check al final de cada frame:

```csharp
void LateUpdate()
{
    // Si está en IdleState pero el agente no está detenido, forzar detención
    if (_brain?.CurrentState?.StateName == "Idle" &&
        _agent != null && _agent.enabled && _agent.isOnNavMesh)
    {
        if (!_agent.isStopped || _agent.velocity.sqrMagnitude > 0.01f)
        {
            _agent.isStopped = true;
            _agent.velocity = Vector3.zero;
            if (_agent.hasPath)
                _agent.ResetPath();
        }
    }
}
```

**Efecto**: Captura y corrige CUALQUIER reactivación del agente que ocurra durante el frame.

---

### 5. NPCMovementDebugger Mejorado - **NUEVO**
**Archivo**: `Assets/Scripts/Behaviour NPC/Debug/NPCMovementDebugger.cs`

**Cambios**:
- Detecta cambios de estado automáticamente
- Muestra tipo real del estado (no solo nombre)
- Muestra status del party (si es miembro)
- Cuenta cambios de estado para detectar bucles

**Uso**: Añadir al NPC problemático para diagnóstico detallado.

---

### 6. NavMeshAgentForceStop (Script de Emergencia) - **NUEVO**
**Archivo**: `Assets/Scripts/Behaviour NPC/Debug/NavMeshAgentForceStop.cs`

**Propósito**: Solución de última instancia si el problema persiste.

**Modos**:
1. **Moderado**: Forzar detención en cada LateUpdate
2. **Extremo**: Desactivar NavMeshAgent completamente en IdleState

**Uso**: Solo usar si los otros fixes no funcionan.

---

## 🧪 Cómo Probar

1. **Escena de prueba**: Coloca varios NPCs con `NPCBehaviourManagerV2` y `NPCCombatConfig`
2. **Sin combate activo**: Los NPCs deberían estar completamente quietos (sin animación de caminar/correr)
3. **Inspector**: Verifica que `NavMeshAgent.velocity` sea `(0, 0, 0)` y `hasPath` sea `false`
4. **Animator**: Verifica que el parámetro `InputMagnitude` sea `0`

## 📊 Estados Afectados

- ✅ **IdleState**: Fix implementado directamente
- ✅ **DeadState**: Ya usa `HardStop()` mejorado
- ✅ **CinematicState**: Ya usa `HardStop()` mejorado
- ⚠️ **WanderState**: NO necesita fix (movimiento intencional)
- ⚠️ **AlertState**: NO necesita fix (puede moverse hacia jugador)
- ⚠️ **CombatState**: NO necesita fix (movimiento controlado por NPCCombatBrain)

## 🔍 Diagnóstico Adicional

Si el problema persiste, verifica:

1. **NPCCombatBrain._isActive**: Debería ser `false` cuando NO está en combate
2. **NPCBehaviourManagerV2.Brain.CurrentState**: Debería ser `IdleState`
3. **NavMeshAgent en Inspector**:
   - `Is Stopped`: ✅ true
   - `Has Path`: ❌ false
   - `Velocity`: (0, 0, 0)
   - `Remaining Distance`: 0

## 💡 Recomendaciones

### Para Debug
Activa `debugMode = true` en `NPCSimpleAnimator` para ver logs de sincronización:
```csharp
[SerializeField] private bool debugMode = true; // ⚠️ Solo para debug
```

Esto mostrará información cada 30 frames sobre:
- Estado del NavMeshAgent
- Velocidad y dirección
- Flags de rotación

### Optimización Futura
Considera añadir un flag global en `NPCSimpleAnimator` para desactivar completamente la sincronización automática cuando el NPC esté en estados estáticos:

```csharp
public bool EnableAutoSync { get; set; } = true;

void Update()
{
    // ...
    if (EnableAutoSync && navAgent != null && navAgent.enabled && navAgent.isOnNavMesh)
    {
        SyncWithNavMeshAgent();
    }
}
```

Y desde `IdleState.OnEnter()`:
```csharp
context.Animator.EnableAutoSync = false;
```

---

## ✅ Checklist de Verificación

- [x] `NPCSimpleAnimator.SyncWithNavMeshAgent()` modificado
- [x] `NavMeshAgentUtility.HardStop()` mejorado
- [x] `IdleState.OnUpdate()` con verificación continua
- [ ] Pruebas en Unity Editor
- [ ] Verificación con NPCs enemigos
- [ ] Verificación con NPCs del party
- [ ] Verificación después de combate

---

**Fecha**: 2026-01-27
**Versión**: Fix v1.0
