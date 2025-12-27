# Limpieza de Variables de Movimiento en Narrativa Interactiva

**Fecha**: 2025-12-26
**Estado**: ✅ Completado

## 🎯 Problema
En `NarrativeChainEntry` había dos variables relacionadas con el tiempo de movimiento del NPC que generaban confusión en el inspector:

1. **`maxMovementDuration`**: "Duración máxima del movimiento"
2. **`walkDisplayDuration`**: "Tiempo que camina visible antes del fade+teleport"

Esto hacía confuso configurar el comportamiento de movimiento en las narrativas.

## 🔧 Solución
Se eliminó la variable `walkDisplayDuration` y se dejó solo **`maxMovementDuration`** con un tooltip más claro.

### Cambios Realizados

#### 1. `NarrativeChainEntry.cs`
**ANTES**:
```csharp
[Tooltip("Duración máxima del movimiento")]
[Min(1f)]
public float maxMovementDuration = 15f;

[Tooltip("Tiempo que camina visible antes del fade+teleport (999 = camina todo el trayecto)")]
[Min(0.5f)]
public float walkDisplayDuration = 999f;
```

**DESPUÉS**:
```csharp
[Tooltip("Tiempo máximo permitido para que el NPC complete el movimiento (en segundos)")]
[Min(1f)]
public float maxMovementDuration = 15f;
```

#### 2. `NPCInteractiveNarrativeExecutor.cs`
En el método `ExecuteStandardMove()`, se eliminó el uso de `entry.walkDisplayDuration` y se reemplazó por un valor fijo de `999f` (que hace que el NPC camine todo el trayecto sin fade+teleport):

```csharp
var moveSequence = new States.MoveToPoscionSequence(
    _npcManager,
    targetPosition,
    entry.maxMovementDuration,
    entry.turnAroundOnArrival,
    999f // walkDisplayDuration - valor alto para caminar todo el trayecto
);
```

## ✅ Resultado
Ahora el inspector es más claro:
- **Solo una variable de tiempo**: `maxMovementDuration`
- **Tooltip claro**: "Tiempo máximo permitido para que el NPC complete el movimiento (en segundos)"
- **Comportamiento consistente**: El NPC siempre camina todo el trayecto sin fade (sistema antiguo deshabilitado)

## 📝 Notas
- La variable `walkDisplayDuration` aún existe en otros sistemas (`QuestChainEntry`, `CinematicState`) pero NO se usa en las narrativas interactivas.
- Si en el futuro se necesita volver a usar el sistema de fade+teleport, se puede agregar como una opción booleana separada (ej: `useFadeTeleport`).

## ⚠️ Migración
**Acción requerida**: Los prefabs/escenas existentes con narrativas configuradas **no requieren cambios**, ya que Unity mantendrá el valor de `maxMovementDuration` y simplemente ignorará `walkDisplayDuration` (que ya no existe).

## 🔍 Testing
Verificar que:
- [ ] Las narrativas con movimiento funcionan correctamente
- [ ] El NPC se mueve completamente al destino sin teleport
- [ ] El timeout funciona correctamente con `maxMovementDuration`

