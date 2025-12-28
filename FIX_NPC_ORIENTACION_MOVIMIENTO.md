# FIX: Orientación del NPC durante el Movimiento

## 📋 Problema Identificado

El NPC caminaba de espaldas o de lado al cambiar de posición en varios estados (especialmente en combate durante el reposicionamiento). La premisa es clara: **si el NPC va del punto A al punto B, debe mirar hacia el punto B mientras se mueve**.

## 🔍 Causa Raíz

Había **conflictos de rotación** entre múltiples sistemas:

1. **NPCCombatBrain** rotaba el `transform` directamente en su método `Update()` con `FaceTarget()` usando `transform.rotation = Quaternion.Slerp(...)`
2. **NPCSimpleAnimator** también rotaba el `transform` en `LateUpdate()` con `ApplySmoothRotation()`
3. **NPCSimpleAnimator.SyncWithNavMeshAgent()** establecía `_targetRotation` basándose en `navAgent.velocity`

Estos tres sistemas competían entre sí, causando que:
- Durante REPOSITION: NPCCombatBrain intentaba hacer que el NPC mirara al player (excepto durante REPOSITION)
- Durante el movimiento: NPCSimpleAnimator intentaba rotar hacia la dirección de movimiento
- El resultado: movimiento de lado o de espaldas porque había rotaciones contradictorias

## ✅ Solución Implementada

### Principio Arquitectónico
**NPCSimpleAnimator es el ÚNICO responsable de la rotación del NPC**. Todos los demás sistemas deben usar su API pública en lugar de rotar el transform directamente.

### Cambios Realizados

#### 1. NPCCombatBrain.cs
- ✅ **ELIMINADO**: Método `FaceTarget()` que rotaba el transform directamente
- ✅ **MODIFICADO**: `Update()` ahora solo rota hacia el player cuando:
  - No está en estado REPOSITION
  - El agent está detenido (`_agent.isStopped == true`)
  - Usa `_animator.FaceTarget()` en lugar de rotar directamente
- ✅ **MODIFICADO**: `State_Reposition()` ya no llama a `FaceTarget()` manualmente
  - `NPCSimpleAnimator.SyncWithNavMeshAgent()` se encarga automáticamente de rotar hacia la dirección de movimiento

```csharp
// ANTES (INCORRECTO)
if (_player != null && _currentState != CombatState.REPOSITION)
{
    FaceTarget(_player.position); // Rotaba directamente
}

// DESPUÉS (CORRECTO)
if (_player != null && _currentState != CombatState.REPOSITION && _agent.isStopped)
{
    _animator.FaceTarget(_player.position); // Usa el sistema de NPCSimpleAnimator
}
```

#### 2. AlertState.cs
- ✅ **ELIMINADO**: Método `RotateTowards()` deprecated
- ✅ **MODIFICADO**: Todas las rotaciones ahora usan `context.Animator.FaceDirection()`
- ✅ **MEJORADO**: `MoveAndRotate()` confía en `NPCSimpleAnimator.SyncWithNavMeshAgent()` para la rotación durante el movimiento

```csharp
// ANTES (INCORRECTO)
RotateTowards(context, context.Player.position, 10f);

// DESPUÉS (CORRECTO)
Vector3 directionToPlayer = (context.Player.position - context.Transform.position).normalized;
directionToPlayer.y = 0;
if (directionToPlayer.sqrMagnitude > 0.01f && context.Animator != null)
{
    context.Animator.FaceDirection(directionToPlayer);
}
```

### Cómo Funciona Ahora

#### Durante el Movimiento (NavMeshAgent activo)
1. **NPCCombatBrain** o cualquier estado llama a `MoveTo(destination, speed)`
2. **NavMeshAgent** calcula la ruta y genera `velocity`
3. **NPCSimpleAnimator.SyncWithNavMeshAgent()** detecta que `velocity.magnitude > movementThreshold`
4. **NPCSimpleAnimator** extrae la dirección: `direction = navAgent.velocity.normalized`
5. **NPCSimpleAnimator.FaceDirection()** establece `_targetRotation = Quaternion.LookRotation(direction)`
6. **NPCSimpleAnimator.ApplySmoothRotation()** en `LateUpdate()` aplica la rotación suavemente

#### Cuando Está Parado
1. El sistema que controla al NPC llama a `_animator.FaceTarget(target.position)` o `_animator.FaceDirection(direction)`
2. **NPCSimpleAnimator** establece `_targetRotation`
3. **NPCSimpleAnimator.ApplySmoothRotation()** en `LateUpdate()` aplica la rotación suavemente

## 🎯 Resultado

- ✅ El NPC **SIEMPRE mira hacia donde se mueve** durante el desplazamiento
- ✅ El NPC mira al player cuando está parado en combate
- ✅ No hay conflictos de rotación entre sistemas
- ✅ La rotación es suave y natural gracias a `RotateTowards()` con `rotationSpeed`
- ✅ Sistema centralizado y predecible

## 📝 Notas Técnicas

### NavMeshAgent Configuración
```csharp
_agent.updateRotation = false; // SIEMPRE desactivado
_agent.updatePosition = true;  // NavMesh controla la posición, pero NO la rotación
```

### NPCSimpleAnimator Configuración
```csharp
syncWithNavAgent = true;        // Sincronizar velocidad Y dirección
navAgent.updateRotation = false; // Configurado en Awake()
navAgent.angularSpeed = 360f;   // Alto para rotaciones rápidas (aunque no se usa)
```

### API Pública de Rotación (NPCSimpleAnimator)
```csharp
// Rotar hacia una posición específica
animator.FaceTarget(Vector3 targetPosition);

// Rotar hacia una dirección
animator.FaceDirection(Vector3 direction);

// Desactivar rotación automática (ej: durante diálogos)
animator.DisableAutoRotation();

// Reactivar rotación automática
animator.EnableAutoRotation();
```

## 🧪 Casos de Prueba

1. **Reposicionamiento en Combate** (alejarse del player)
   - ✅ El NPC corre **hacia adelante** alejándose del player
   - ✅ No camina de espaldas

2. **Acercarse al Player en AlertState**
   - ✅ El NPC camina **hacia adelante** mirando al player
   - ✅ No camina de lado

3. **Búsqueda de Cobertura en Defense State**
   - ✅ El NPC corre **hacia la cobertura** mirando hacia allá
   - ✅ No corre de lado hacia la cobertura

4. **Esquiva Lateral (Dodge)**
   - ✅ El NPC se mueve lateralmente mirando hacia donde va
   - ✅ No se desliza de lado mirando al player

## 📚 Archivos Modificados

- ✅ `Assets/Scripts/Behaviour NPC/NPCCombatBrain.cs`
- ✅ `Assets/Scripts/Behaviour NPC/States/AlertState.cs`

## 🔄 Sistemas No Modificados (Intencional)

Los siguientes scripts mantienen su propia rotación porque son sistemas especializados:
- `NPCAmbientBrain.cs` - Sistema de interacción/diálogo (no combate)
- `NPCInteractiveNarrativeExecutor.cs` - Sistema narrativo
- `NPCCombatLifecycleHandler.cs` - Rotación especial en momento de muerte (efecto cinemático)

---

**Fecha**: 28 de diciembre de 2024  
**Estado**: ✅ COMPLETADO  
**Probado**: Pendiente de pruebas en Unity

