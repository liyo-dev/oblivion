# 🔥 Diagnóstico Adicional: Velocidad Masiva en IdleState

## 🐛 Problema Observado

El NPC "Lety" muestra:
- **Estado**: `IdleState`
- **Agent.isStopped**: `False` ❌
- **Agent.velocity**: 200-500 unidades/segundo (¡MASIVO!)
- **Agent.hasPath**: `False`
- **Animator InputMagnitude**: `0.000`

## 🔍 Causas Posibles

### 1. **Algo está reactivando el NavMeshAgent constantemente**
- Otro script está llamando a `agent.isStopped = false`
- Verificar: `FollowPlayerState`, `NPCCombatTeam`, `NPCCombatLifecycleHandler`

### 2. **Física del Rigidbody interferiendo**
- El Rigidbody puede estar en modo no-kinematic
- Colisiones físicas están empujando al NPC
- **SOLUCIÓN**: Verificar que `Rigidbody.isKinematic = true`

### 3. **NavMeshAgent con configuración incorrecta**
- `avoidancePriority` muy bajo causando empujones
- `obstacleAvoidanceType` muy agresivo
- **SOLUCIÓN**: Ajustar configuración del NavMeshAgent en Inspector

### 4. **Animator con Root Motion activo**
- El Animator puede estar aplicando movimiento
- **SOLUCIÓN**: Verificar `Animator.applyRootMotion = false`

### 5. **Múltiples scripts controlando el agente**
- Conflicto entre sistemas (FSM vs Party vs Combat)
- **SOLUCIÓN**: Un solo sistema debe controlar el agente en cada momento

## ✅ Fixes Aplicados

### Fix #1: IdleState más agresivo
```csharp
// En IdleState.OnUpdate() - ahora con logging
if (!context.Agent.isStopped)
{
    context.Log("[IdleState] ⚠️ Agent NO ESTABA DETENIDO - forzando detención");
    context.Agent.isStopped = true;
}
```

### Fix #2: Safety check en LateUpdate
```csharp
// En NPCBehaviourManagerV2.LateUpdate()
if (_brain.CurrentState.StateName == "Idle" && !_agent.isStopped)
{
    // Forzar detención DESPUÉS de todos los Updates
    _agent.isStopped = true;
    _agent.velocity = Vector3.zero;
}
```

### Fix #3: Debugger mejorado
Ahora detecta:
- Tipo real del estado (no solo el nombre)
- Status del party
- Cambios de estado

## 🔧 Pasos de Diagnóstico

### En Unity Editor:

1. **Seleccionar el NPC "Lety"**

2. **Inspector - NavMeshAgent:**
   - ✅ `Update Rotation`: False (controlado por NPCSimpleAnimator)
   - ✅ `Update Position`: True
   - ✅ `Stopping Distance`: ~0.5-1.0
   - ✅ `Auto Braking`: True
   - ⚠️ **`Avoidance Priority`**: 50 (rango 0-99, menor = mayor prioridad)
   - ⚠️ **`Obstacle Avoidance Type`**: Low Quality o None

3. **Inspector - Rigidbody:**
   - ✅ `Is Kinematic`: **DEBE SER TRUE**
   - ✅ `Use Gravity`: False
   - ✅ `Interpolate`: None
   - ✅ `Collision Detection`: Discrete

4. **Inspector - Animator:**
   - ✅ `Apply Root Motion`: **DEBE SER FALSE**
   - ✅ `Update Mode`: Normal
   - ✅ `Culling Mode`: Always Animate

5. **Inspector - NPCPartyMember:**
   - Verificar `Is In Party`: ¿True o False?
   - Si es True, debería estar en `FollowPlayerState`, no `IdleState`

6. **Console Logs con debugMode:**
   - Buscar mensajes de `[IdleState]` indicando correcciones
   - Buscar mensajes de `[NPCManager] LateUpdate Safety`
   - Buscar cambios de estado frecuentes

## 🎯 Acción Inmediata

### Si el problema persiste, hacer esto:

1. **En Play Mode**, seleccionar "Lety"
2. **Inspector → NavMeshAgent**:
   - Marcar `Is Stopped` manualmente
   - Observar si se desactiva solo
3. **Si se desactiva**: Buscar en la Consola quién lo reactivó
4. **Si NO se desactiva**: El problema es física/Rigidbody

### Script de Emergencia:

Añadir este componente temporal a "Lety":

```csharp
public class NavMeshAgentForceStop : MonoBehaviour
{
    private NavMeshAgent _agent;
    
    void Start() => _agent = GetComponent<NavMeshAgent>();
    
    void LateUpdate()
    {
        if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
        {
            if (!_agent.isStopped)
            {
                Debug.LogError($"[ForceStop] ⚠️ Alguien reactivó el agente!");
                _agent.isStopped = true;
                _agent.velocity = Vector3.zero;
                _agent.ResetPath();
            }
        }
    }
}
```

## 📊 Métricas Esperadas

Después de los fixes, el NPC en IdleState debería mostrar:
- ✅ `Agent.isStopped`: True
- ✅ `Agent.velocity`: (0, 0, 0)
- ✅ `Agent.hasPath`: False
- ✅ `Animator InputMagnitude`: 0

## 🔄 Próximos Pasos

1. ✅ Aplicar fixes (HECHO)
2. ⏳ Probar en Unity y observar logs
3. ⏳ Si persiste, revisar Rigidbody/Colisiones
4. ⏳ Última opción: Desactivar NavMeshAgent cuando esté en Idle

---

**Fecha**: 2025-01-27
**Estado**: Fixes aplicados, esperando pruebas
